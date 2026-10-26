using System.Runtime.CompilerServices;
using System.Text;
using ForgeCore.Core;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;

namespace ForgeCore.LocalAI;

public sealed class LlamaSharpInferenceBackend : ILocalInferenceBackend
{
    private readonly LocalModelOptions _options; private readonly ILogger<LlamaSharpInferenceBackend> _logger;
    private readonly ModelParams _parameters; private readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private LLamaWeights? _weights; private StatelessExecutor? _executor; private bool _disposed; private string? _loadError;

    public LlamaSharpInferenceBackend(string modelPath, LocalModelOptions options, ILogger<LlamaSharpInferenceBackend> logger)
    {
        _options = options; _logger = logger;
        _parameters = new ModelParams(modelPath) { ContextSize = options.ContextSize, GpuLayerCount = options.GpuLayerCount };
    }

    public ModelStatus Status => _disposed
        ? new("LLamaSharp", false, "Model backend has been disposed.")
        : _loadError is not null ? new("LLamaSharp/Vulkan", false, $"Model load failed: {_loadError}")
        : new("LLamaSharp/Vulkan", true, _weights is null ? "Model file ready; weights load lazily on first request." : "Model loaded locally and ready.");

    public async IAsyncEnumerable<string> InferAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); cancellationToken.ThrowIfCancellationRequested();
        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureLoadedAsync(cancellationToken);
            var promptTokens = _weights!.Tokenize(prompt, add_bos: true, special: true, Encoding.UTF8);
            if ((long)promptTokens.Length + _options.MaxTokens > _options.ContextSize)
                throw new InvalidOperationException($"Prompt ({promptTokens.Length} tokens) plus MaxTokens ({_options.MaxTokens}) exceeds ContextSize ({_options.ContextSize}).");
            var inference = new InferenceParams
            {
                MaxTokens = _options.MaxTokens,
                AntiPrompts = ["\nUser:", "<|eot_id|>"],
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.1f,
                    TopP = 0.9f,
                    Grammar = _options.EnableJsonGrammar ? new Grammar(PlanGrammar.Gbnf, "root") : null
                }
            };
            await foreach (var chunk in _executor!.InferAsync(prompt, inference, cancellationToken).WithCancellation(cancellationToken))
                yield return chunk;
        }
        finally { _inferenceLock.Release(); }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_weights is not null) return;
        _logger.LogInformation("Loading local GGUF model {ModelPath} with context {ContextSize} and {GpuLayers} GPU layers", _parameters.ModelPath, _options.ContextSize, _options.GpuLayerCount);
        try
        {
            var weights = await LLamaWeights.LoadFromFileAsync(_parameters, cancellationToken);
            _weights = weights; _executor = new StatelessExecutor(weights, _parameters);
            _logger.LogInformation("Local GGUF model loaded successfully");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _loadError = ex.Message; _logger.LogError(ex, "Local GGUF model load failed");
            throw new InvalidOperationException("The configured local model could not be loaded. Check the model file, Vulkan-capable graphics driver, and GPU layer setting.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true; await _inferenceLock.WaitAsync();
        try { _executor = null; _weights?.Dispose(); _weights = null; }
        finally { _inferenceLock.Release(); _inferenceLock.Dispose(); }
    }
}

internal static class PlanGrammar
{
    public const string Gbnf = """
        root ::= ws "{" ws "\"Response\"" ws ":" ws string ws "," ws "\"Actions\"" ws ":" ws "[" ws (action (ws "," ws action)*)? ws "]" ws "}" ws
        action ::= "{" ws "\"Kind\"" ws ":" ws kind ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws arguments ws "}" | map_action | game_template_action | gameplay_action | preview_action
        map_action ::= "{" ws "\"Kind\"" ws ":" ws "\"scaffold_unity_map_demo\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"map_template\"" ws ":" ws "\"frontier_valley\"" ws "}" ws "}" | "{" ws "\"Kind\"" ws ":" ws "\"unity_editor_action\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws "\"create_map_demo\"" ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"map_template\"" ws ":" ws "\"frontier_valley\"" ws "," ws "\"seed\"" ws ":" ws string ws "}" ws "}"
        game_template_action ::= "{" ws "\"Kind\"" ws ":" ws "\"scaffold_unity_game_template\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"template_id\"" ws ":" ws game_template ws "}" ws "}" | "{" ws "\"Kind\"" ws ":" ws "\"unity_editor_action\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws "\"create_game_template\"" ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"template_id\"" ws ":" ws game_template ws "," ws "\"seed\"" ws ":" ws string ws "}" ws "}" | "{" ws "\"Kind\"" ws ":" ws "\"unity_editor_action\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws "\"create_game_campaign\"" ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"template_id\"" ws ":" ws game_template ws "," ws "\"seed\"" ws ":" ws string ws "," ws "\"level_count\"" ws ":" ws string ws "}" ws "}"
        gameplay_action ::= "{" ws "\"Kind\"" ws ":" ws "\"unity_editor_action\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws "\"verify_gameplay\"" ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"duration_seconds\"" ws ":" ws string ws "}" ws "}"
        preview_action ::= "{" ws "\"Kind\"" ws ":" ws "\"unity_editor_action\"" ws "," ws "\"Description\"" ws ":" ws string ws "," ws "\"Arguments\"" ws ":" ws "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws "\"capture_scene_preview\"" ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"output_path\"" ws ":" ws string ws "," ws "\"duration_seconds\"" ws ":" ws string ws "}" ws "}"
        kind ::= "\"list_files\"" | "\"read_text_file\"" | "\"write_text_file\"" | "\"apply_text_patch\"" | "\"restore_text_snapshot\"" | "\"create_directory\"" | "\"scaffold_project\"" | "\"stage_web_asset\"" | "\"dotnet_build\"" | "\"dotnet_test\"" | "\"run_command\"" | "\"index_project\"" | "\"search_project\"" | "\"search_asset_catalog\"" | "\"inspect_staged_asset\"" | "\"import_staged_asset\"" | "\"install_unity_bridge\"" | "\"unity_editor_action\"" | "\"unity_mcp_action\"" | "\"unity_player_smoke_test\"" | "\"unity_player_profile_test\""
        arguments ::= "{" ws "\"path\"" ws ":" ws string ws "}" | "{" ws "\"path\"" ws ":" ws string ws "," ws "\"content\"" ws ":" ws string ws "}" | "{" ws "\"path\"" ws ":" ws string ws "," ws "\"old_text\"" ws ":" ws string ws "," ws "\"new_text\"" ws ":" ws string (ws "," ws "\"expected_sha256\"" ws ":" ws string)? ws "}" | "{" ws "\"path\"" ws ":" ws string ws "," ws "\"snapshot_path\"" ws ":" ws string ws "," ws "\"expected_current_sha256\"" ws ":" ws string ws "}" | "{" ws "\"path\"" ws ":" ws string ws "," ws "\"query\"" ws ":" ws string ws "," ws "\"max_results\"" ws ":" ws string ws "}" | "{" ws "\"query\"" ws ":" ws string ws "," ws "\"media_type\"" ws ":" ws string ws "," ws "\"license_filter\"" ws ":" ws string ws "," ws "\"max_results\"" ws ":" ws string ws "}" | "{" ws "\"command\"" ws ":" ws string ws "}" | "{" ws "\"path\"" ws ":" ws string ws "," ws "\"template\"" ws ":" ws template ws "," ws "\"name\"" ws ":" ws string (ws "," ws "\"unity_version\"" ws ":" ws string)? ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"tool_name\"" ws ":" ws string ws "," ws "\"arguments_json\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"scene_template\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"object_name\"" ws ":" ws string ws "," ws "\"primitive\"" ws ":" ws string ws "," ws "\"position_x\"" ws ":" ws string ws "," ws "\"position_y\"" ws ":" ws string ws "," ws "\"position_z\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"object_name\"" ws ":" ws string ws "," ws "\"component_type\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"test_mode\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"scene_path\"" ws ":" ws string ws "," ws "\"duration_seconds\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"operation\"" ws ":" ws string ws "," ws "\"output_path\"" ws ":" ws string ws "," ws "\"development_build\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"output_path\"" ws ":" ws string ws "," ws "\"duration_seconds\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"file_name\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"file_name\"" ws ":" ws string ws "," ws "\"destination_path\"" ws ":" ws string ws "," ws "\"expected_sha256\"" ws ":" ws string ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"file_name\"" ws ":" ws string ws "," ws "\"url\"" ws ":" ws string ws "," ws "\"source_page\"" ws ":" ws string ws "," ws "\"license\"" ws ":" ws string (ws "," ws "\"sha256\"" ws ":" ws string)? ws "}" | "{" ws "\"project_path\"" ws ":" ws string ws "," ws "\"configuration\"" ws ":" ws configuration ws "}"
        template ::= "\"dotnet-console\"" | "\"dotnet-classlib\"" | "\"unity-basic\""
        configuration ::= "\"Debug\"" | "\"Release\""
        game_template ::= "\"first_person_action\"" | "\"platformer\"" | "\"top_down_rpg\"" | "\"racing\""
        string ::= "\"" char* "\""
        char ::= [^"\\\x7F\x00-\x1F] | "\\" (["\\/bfnrt] | "u" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F])
        ws ::= [ \t\n\r]*
        """;
}
