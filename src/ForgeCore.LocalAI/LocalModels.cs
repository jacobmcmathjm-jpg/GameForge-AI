using System.Runtime.CompilerServices;
using System.Text;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed class MockLocalModel : ILocalModel
{
    public ModelStatus Status => new("Mock", true, "Mock model ready; no model file required.");

    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var action = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read,
            "Inspect the approved workspace to establish local context.", ("path", "."));
        return Task.FromResult(new AgentPlan($"Mock mode received: {request.UserMessage}", [action]));
    }
}

public interface ILocalInferenceBackend : IAsyncDisposable
{
    ModelStatus Status { get; }
    IAsyncEnumerable<string> InferAsync(string prompt, CancellationToken cancellationToken);
}

public sealed class LlamaSharpLocalModel(ILocalInferenceBackend backend, LocalModelOptions options) : ILocalModel, IAsyncDisposable
{
    public ModelStatus Status => backend.Status;

    public async Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserMessage);
        if (request.UserMessage.Length > options.MaxRequestCharacters)
            throw new ArgumentException($"Request exceeds the {options.MaxRequestCharacters} character local-model limit.", nameof(request));
        if (!Status.IsAvailable) throw new InvalidOperationException(Status.Detail);

        var response = new StringBuilder();
        await foreach (var chunk in backend.InferAsync(PlanPrompt.Create(request, options.MaxPlanActions, options.MaxHistoryMessages,
                           options.MaxHistoryCharacters, options.MaxObservationCharacters), cancellationToken).WithCancellation(cancellationToken))
        {
            if (response.Length + chunk.Length > options.MaxResponseCharacters)
                throw new InvalidOperationException($"Model response exceeded {options.MaxResponseCharacters} characters.");
            response.Append(chunk);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return StructuredPlanParser.Parse(response.ToString(), options.MaxPlanActions);
    }

    public ValueTask DisposeAsync() => backend.DisposeAsync();
}

public sealed class UnavailableInferenceBackend(string provider, string detail) : ILocalInferenceBackend
{
    public ModelStatus Status => new(provider, false, detail);
    public async IAsyncEnumerable<string> InferAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); await Task.CompletedTask; yield break;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class LocalModelOptions
{
    public string Provider { get; set; } = "Mock";
    public string ModelPath { get; set; } = string.Empty;
    public bool EnableLlamaSharp { get; set; }
    public uint ContextSize { get; set; } = 4096;
    public int GpuLayerCount { get; set; } = 33;
    public int MaxTokens { get; set; } = 1024;
    public int MaxRequestCharacters { get; set; } = 16_000;
    public int MaxResponseCharacters { get; set; } = 32_000;
    public int MaxPlanActions { get; set; } = 12;
    public int MaxHistoryMessages { get; set; } = 12;
    public int MaxHistoryCharacters { get; set; } = 12_000;
    public int MaxObservationCharacters { get; set; } = 6_000;
    public bool EnableJsonGrammar { get; set; } = true;
}

public static class LocalInferenceBackendFactory
{
    public static ILocalInferenceBackend Create(LocalModelOptions options, Func<string, LocalModelOptions, ILocalInferenceBackend> createAvailable)
    {
        if (!options.EnableLlamaSharp) return new UnavailableInferenceBackend("LLamaSharp", "Feature flag is disabled; no model was loaded.");
        if (!options.TryValidate(out var configurationError)) return new UnavailableInferenceBackend("LLamaSharp", configurationError);
        if (string.IsNullOrWhiteSpace(options.ModelPath)) return new UnavailableInferenceBackend("LLamaSharp", "No model path configured; no download was attempted.");
        var path = Environment.ExpandEnvironmentVariables(options.ModelPath);
        if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) return new UnavailableInferenceBackend("LLamaSharp", "UNC and device model paths are not allowed.");
        if (!Path.IsPathFullyQualified(path)) return new UnavailableInferenceBackend("LLamaSharp", "Model path must be absolute.");
        if (!string.Equals(Path.GetExtension(path), ".gguf", StringComparison.OrdinalIgnoreCase)) return new UnavailableInferenceBackend("LLamaSharp", "Model must be a local .gguf file.");
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) return new UnavailableInferenceBackend("LLamaSharp", "Configured model file was not found; no download was attempted.");
        return createAvailable(path, options);
    }
}

internal static class LocalModelOptionsValidation
{
    public static bool TryValidate(this LocalModelOptions options, out string error)
    {
        error = options.ContextSize is < 512 or > 32768 ? "ContextSize must be between 512 and 32768."
            : options.GpuLayerCount is < 0 or > 512 ? "GpuLayerCount must be between 0 and 512."
            : options.MaxTokens is < 1 or > 4096 ? "MaxTokens must be between 1 and 4096."
            : options.MaxRequestCharacters is < 1 or > 100_000 ? "MaxRequestCharacters must be between 1 and 100000."
            : options.MaxResponseCharacters is < 1 or > 100_000 ? "MaxResponseCharacters must be between 1 and 100000."
            : options.MaxPlanActions is < 1 or > 100 ? "MaxPlanActions must be between 1 and 100."
            : options.MaxHistoryMessages is < 0 or > 100 ? "MaxHistoryMessages must be between 0 and 100."
            : options.MaxHistoryCharacters is < 0 or > 100_000 ? "MaxHistoryCharacters must be between 0 and 100000."
            : options.MaxObservationCharacters is < 0 or > 100_000 ? "MaxObservationCharacters must be between 0 and 100000."
            : string.Empty;
        return error.Length == 0;
    }
}
