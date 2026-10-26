using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

public sealed record Generated3DArtifact(string Name, Uri DownloadUri);
public sealed record Generated3DResult(string JobId, Uri? PlatformUri, int? TotalCredits,
    IReadOnlyList<Generated3DArtifact> Artifacts);

public interface I3DGenerationProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken);
}

public sealed class Seele3DGenerationProvider : I3DGenerationProvider
{
    private static readonly Uri JobsEndpoint = new("https://openapi.seeles.ai/v2/api/jobs");
    private readonly HttpClient _http;
    private readonly string _apiKeyEnvironmentVariable;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxResponseBytes;

    public Seele3DGenerationProvider(HttpClient http, string apiKeyEnvironmentVariable = "SEELE_API_KEY",
        int timeoutSeconds = 1_800, int pollSeconds = 15, int maxResponseBytes = 1_000_000)
    {
        _http = http;
        if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable) || apiKeyEnvironmentVariable.Length > 64 ||
            !char.IsLetter(apiKeyEnvironmentVariable[0]) || apiKeyEnvironmentVariable.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Seele API key environment-variable name is invalid.", nameof(apiKeyEnvironmentVariable));
        if (timeoutSeconds is < 60 or > 3_600) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (pollSeconds is < 2 or > 60) throw new ArgumentOutOfRangeException(nameof(pollSeconds));
        if (maxResponseBytes is < 16_384 or > 10 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
        _apiKeyEnvironmentVariable = apiKeyEnvironmentVariable;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);
        _maxResponseBytes = maxResponseBytes;
    }

    public string Name => "seele";
    public bool IsConfigured => ReadApiKey() is not null;

    public async Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken)
    {
        if (request.Provider != Name) throw new ArgumentException("This provider only accepts Seele generation requests.", nameof(request));
        var key = ReadApiKey() ?? throw new UnauthorizedAccessException(
            $"Seele API key is missing from the {_apiKeyEnvironmentVariable} user environment variable.");
        var guardedPrompt = "Create one original, game-ready 3D asset for a Unity project. Deliver a standalone textured GLB with embedded resources. " +
            "Use sensible real-world scale, a centered ground-level pivot, efficient topology, PBR materials, and no protected characters, logos, code, scripts, project packages, executables, or archives. Asset brief: " + request.Prompt;
        using var create = new HttpRequestMessage(HttpMethod.Post, JobsEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { prompt = guardedPrompt, model = request.Model, engine = "unity" }), Encoding.UTF8, "application/json")
        };
        Authorize(create, key);
        using var createResponse = await _http.SendAsync(create, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await ReadBoundedUtf8Async(createResponse.Content, cancellationToken), new JsonDocumentOptions { MaxDepth = 32 });
        var jobId = ReadCreatedJobId(createJson.RootElement);

        var watch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (watch.Elapsed >= _timeout) throw new InvalidOperationException($"Seele generation exceeded the {_timeout.TotalMinutes:0}-minute limit.");
            using var poll = new HttpRequestMessage(HttpMethod.Get, new Uri(JobsEndpoint.AbsoluteUri + "/" + Uri.EscapeDataString(jobId)));
            Authorize(poll, key);
            using var pollResponse = await _http.SendAsync(poll, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            pollResponse.EnsureSuccessStatusCode();
            using var pollJson = JsonDocument.Parse(await ReadBoundedUtf8Async(pollResponse.Content, cancellationToken), new JsonDocumentOptions { MaxDepth = 48 });
            var result = ReadStatus(pollJson.RootElement, jobId);
            if (result is not null) return result;
            var remaining = _timeout - watch.Elapsed;
            await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, cancellationToken);
        }
    }

    private string? ReadApiKey()
    {
        var value = Environment.GetEnvironmentVariable(_apiKeyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 512 && !value.Any(char.IsWhiteSpace) ? value : null;
    }

    private static void Authorize(HttpRequestMessage request, string key)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("ForgeCore/0.14");
    }

    private static string ReadCreatedJobId(JsonElement root)
    {
        EnsureOk(root);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("job_id", out var job) || job.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Seele create-job response did not contain a job_id.");
        var value = job.GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl))
            throw new InvalidOperationException("Seele returned an invalid job_id.");
        return value;
    }

    private static Generated3DResult? ReadStatus(JsonElement root, string expectedJobId)
    {
        EnsureOk(root);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Seele job response did not contain a data object.");
        var responseId = ReadString(data, "job_id", 200);
        if (responseId is not null && !string.Equals(responseId, expectedJobId, StringComparison.Ordinal))
            throw new InvalidOperationException("Seele job response identity did not match the requested job.");
        var status = ReadString(data, "status", 32) ?? throw new InvalidOperationException("Seele job response did not contain status.");
        if (status == "processing") return null;
        if (status != "finished") throw new InvalidOperationException($"Seele returned unsupported job status '{status}'.");
        Uri? platform = null;
        if (data.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
            platform = ReadHttpsUri(meta, "platform_url");
        int? credits = data.TryGetProperty("total_koin", out var total) && total.TryGetInt32(out var amount) && amount >= 0 ? amount : null;
        var artifacts = new List<Generated3DArtifact>();
        if (data.TryGetProperty("artifacts", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            foreach (var artifact in values.EnumerateArray())
            {
                if (artifacts.Count >= 8 || artifact.ValueKind != JsonValueKind.Object) break;
                var name = ReadString(artifact, "name", 160) ?? "Generated asset";
                if (!artifact.TryGetProperty("urls", out var urls) || urls.ValueKind != JsonValueKind.Array) continue;
                foreach (var url in urls.EnumerateArray())
                {
                    if (artifacts.Count >= 8) break;
                    if (url.ValueKind != JsonValueKind.String || !TryHttps(url.GetString(), out var uri)) continue;
                    artifacts.Add(new(name, uri!));
                }
            }
        }
        return new(expectedJobId, platform, credits, artifacts);
    }

    private static void EnsureOk(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            var error = root.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.Object
                ? ReadString(value, "message", 500) : null;
            throw new InvalidOperationException("Seele API rejected the request" + (error is null ? "." : ": " + error));
        }
    }

    private static string? ReadString(JsonElement element, string property, int maxLength)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength || text.Any(char.IsControl)) return null;
        return text;
    }

    private static Uri? ReadHttpsUri(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && TryHttps(value.GetString(), out var uri) ? uri : null;

    private static bool TryHttps(string? value, out Uri? uri)
    {
        var valid = Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) && (uri.IsDefaultPort || uri.Port == 443);
        if (!valid) uri = null;
        return valid;
    }

    private async Task<string> ReadBoundedUtf8Async(HttpContent content, CancellationToken token)
    {
        var declared = content.Headers.ContentLength;
        if (declared.HasValue && declared.Value > _maxResponseBytes)
            throw new InvalidOperationException($"Seele response exceeds the {_maxResponseBytes} byte limit.");
        await using var input = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[16_384]; var total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token); if (read == 0) break;
            total += read; if (total > _maxResponseBytes) throw new InvalidOperationException($"Seele response exceeds the {_maxResponseBytes} byte limit.");
            output.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }
}

public sealed class Meshy3DGenerationProvider : I3DGenerationProvider
{
    private static readonly Uri TasksEndpoint = new("https://api.meshy.ai/openapi/v2/text-to-3d");
    private readonly HttpClient _http;
    private readonly string _apiKeyEnvironmentVariable;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxResponseBytes;

    public Meshy3DGenerationProvider(HttpClient http, string apiKeyEnvironmentVariable = "MESHY_API_KEY",
        int timeoutSeconds = 1_800, int pollSeconds = 10, int maxResponseBytes = 1_000_000)
    {
        _http = http;
        if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable) || apiKeyEnvironmentVariable.Length > 64 ||
            !char.IsLetter(apiKeyEnvironmentVariable[0]) || apiKeyEnvironmentVariable.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Meshy API key environment-variable name is invalid.", nameof(apiKeyEnvironmentVariable));
        if (timeoutSeconds is < 60 or > 3_600) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (pollSeconds is < 2 or > 60) throw new ArgumentOutOfRangeException(nameof(pollSeconds));
        if (maxResponseBytes is < 16_384 or > 10 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
        _apiKeyEnvironmentVariable = apiKeyEnvironmentVariable;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);
        _maxResponseBytes = maxResponseBytes;
    }

    public string Name => "meshy";
    public bool IsConfigured => ReadApiKey() is not null;

    public async Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken)
    {
        if (request.Provider != Name || request.Model != "meshy-6")
            throw new ArgumentException("This provider accepts only Meshy 6 generation requests.", nameof(request));
        var key = ReadApiKey() ?? throw new UnauthorizedAccessException(
            $"Meshy API key is missing from the {_apiKeyEnvironmentVariable} user environment variable.");
        var guardedPrompt = "One original game-ready standalone prop for a Unity project. Complete object, no environment or ground plane. " +
            "Efficient topology, clean silhouette, realistic scale, centered bottom pivot, no protected characters, brands, logos, text, code, scripts, packages, executables, or archives. Asset brief: " + request.Prompt;
        if (guardedPrompt.Length > 600) throw new ArgumentException("Meshy guarded prompt exceeds the 600-character provider limit.", nameof(request));
        var watch = Stopwatch.StartNew();

        var previewId = await CreateTaskAsync(new
        {
            mode = "preview", prompt = guardedPrompt, ai_model = request.Model,
            should_remesh = true, target_polycount = 30_000, target_formats = new[] { "glb" },
            moderation = true, auto_size = true, origin_at = "bottom"
        }, key, cancellationToken);
        var preview = await PollTaskAsync(previewId, key, watch, cancellationToken);

        var refineId = await CreateTaskAsync(new
        {
            mode = "refine", preview_task_id = previewId, enable_pbr = true,
            texture_resolution = "2k", ai_model = request.Model, moderation = true,
            remove_lighting = true, target_formats = new[] { "glb" }, auto_size = true, origin_at = "bottom"
        }, key, cancellationToken);
        var refined = await PollTaskAsync(refineId, key, watch, cancellationToken);
        if (refined.GlbUri is null) throw new InvalidOperationException("Meshy refinement succeeded without a downloadable GLB URL.");
        return new($"{previewId}/{refineId}", null, checked(preview.Credits + refined.Credits),
            [new Generated3DArtifact("Meshy refined PBR GLB", refined.GlbUri)]);
    }

    private async Task<string> CreateTaskAsync(object payload, string key, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TasksEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        Authorize(request, key);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        var body = await ReadBoundedUtf8Async(response.Content, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Meshy rejected task creation with HTTP {(int)response.StatusCode}.");
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 24 });
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Meshy create-task response did not contain a task id.");
        return ValidateTaskId(result.GetString());
    }

    private async Task<MeshyTaskResult> PollTaskAsync(string taskId, string key, Stopwatch watch, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (watch.Elapsed >= _timeout) throw new InvalidOperationException($"Meshy generation exceeded the {_timeout.TotalMinutes:0}-minute limit.");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TasksEndpoint.AbsoluteUri + "/" + Uri.EscapeDataString(taskId)));
            Authorize(request, key);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var body = await ReadBoundedUtf8Async(response.Content, token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Meshy task polling failed with HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 48 });
            var root = document.RootElement;
            var responseId = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? ValidateTaskId(id.GetString()) : taskId;
            if (!string.Equals(responseId, taskId, StringComparison.Ordinal)) throw new InvalidOperationException("Meshy task response identity did not match the requested task.");
            var status = ReadString(root, "status", 32) ?? throw new InvalidOperationException("Meshy task response did not contain status.");
            if (status is "PENDING" or "IN_PROGRESS")
            {
                var remaining = _timeout - watch.Elapsed;
                await Task.Delay(remaining < _pollInterval ? remaining : _pollInterval, token);
                continue;
            }
            if (status is "FAILED" or "CANCELED")
            {
                var error = root.TryGetProperty("task_error", out var taskError) && taskError.ValueKind == JsonValueKind.Object
                    ? ReadString(taskError, "message", 500) : null;
                throw new InvalidOperationException($"Meshy task {status.ToLowerInvariant()}" + (error is null ? "." : $": {error}"));
            }
            if (status != "SUCCEEDED") throw new InvalidOperationException($"Meshy returned unsupported task status '{status}'.");
            var credits = root.TryGetProperty("consumed_credits", out var consumed) && consumed.TryGetInt32(out var amount) && amount >= 0 ? amount : 0;
            Uri? glb = null;
            if (root.TryGetProperty("model_urls", out var modelUrls) && modelUrls.ValueKind == JsonValueKind.Object &&
                modelUrls.TryGetProperty("glb", out var value) && value.ValueKind == JsonValueKind.String && TryHttps(value.GetString(), out var parsed)) glb = parsed;
            return new(credits, glb);
        }
    }

    private string? ReadApiKey()
    {
        var value = Environment.GetEnvironmentVariable(_apiKeyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 512 && !value.Any(char.IsWhiteSpace) ? value : null;
    }

    private static void Authorize(HttpRequestMessage request, string key)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("ForgeCore/0.14");
    }

    private static string ValidateTaskId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl))
            throw new InvalidOperationException("Meshy returned an invalid task id.");
        return value;
    }

    private static string? ReadString(JsonElement element, string property, int maxLength)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text.Length > maxLength || text.Any(char.IsControl) ? null : text;
    }

    private static bool TryHttps(string? value, out Uri? uri)
    {
        var valid = Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) && (uri.IsDefaultPort || uri.Port == 443);
        if (!valid) uri = null;
        return valid;
    }

    private async Task<string> ReadBoundedUtf8Async(HttpContent content, CancellationToken token)
    {
        if (content.Headers.ContentLength is { } declared && declared > _maxResponseBytes)
            throw new InvalidOperationException($"Meshy response exceeds the {_maxResponseBytes} byte limit.");
        await using var input = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[16_384]; var total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token); if (read == 0) break;
            total += read; if (total > _maxResponseBytes) throw new InvalidOperationException($"Meshy response exceeds the {_maxResponseBytes} byte limit.");
            output.Write(buffer, 0, read);
        }
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length));
    }

    private sealed record MeshyTaskResult(int Credits, Uri? GlbUri);
}
