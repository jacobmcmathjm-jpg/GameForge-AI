using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

public sealed class UnityMcpClient
{
    public const string ProtocolVersion = "2025-11-25";
    private static readonly IReadOnlySet<string> SupportedProtocolVersions = new HashSet<string>(StringComparer.Ordinal)
    {
        "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"
    };
    private readonly bool _enabled;
    private readonly string? _relayPath;
    private readonly HashSet<string> _allowedTools;
    private readonly TimeSpan _timeout;
    private readonly int _maxResponseCharacters;

    public UnityMcpClient(bool enabled, string? relayPath, IEnumerable<string>? allowedTools = null,
        int timeoutSeconds = 120, int maxResponseCharacters = 100_000)
    {
        if (timeoutSeconds is < 5 or > 1800) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (maxResponseCharacters is < 1_000 or > 2_000_000) throw new ArgumentOutOfRangeException(nameof(maxResponseCharacters));
        _enabled = enabled;
        _relayPath = NormalizeRelayPath(relayPath);
        _allowedTools = new(allowedTools ?? [], StringComparer.Ordinal);
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _maxResponseCharacters = maxResponseCharacters;
    }

    public string StatusDetail => !_enabled ? "Unity MCP is disabled."
        : _relayPath is null || !File.Exists(_relayPath) ? "Unity MCP relay is not installed."
        : "Unity MCP relay is configured; open the intended Unity project and approve the first connection in Unity.";

    public async Task<string> ExecuteAsync(UnityMcpActionSpec spec, string projectRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_enabled) throw new InvalidOperationException("Unity MCP is disabled in configuration.");
        if (_relayPath is null || !File.Exists(_relayPath))
            throw new InvalidOperationException("The configured Unity MCP relay does not exist. Install Unity AI Assistant in Unity first.");
        if (spec.Operation == "call" && !_allowedTools.Contains(spec.ToolName!))
            throw new InvalidOperationException("Unity MCP tool is not on the explicit allow-list.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try { return await RunSessionAsync(_relayPath, projectRoot, spec, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Unity MCP did not complete within {_timeout.TotalSeconds:0} seconds.");
        }
    }

    private async Task<string> RunSessionAsync(string relayPath, string projectRoot, UnityMcpActionSpec spec, CancellationToken token)
    {
        var start = new ProcessStartInfo
        {
            FileName = relayPath,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--mcp");
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Unity MCP relay did not start.");
        }
        catch (Win32Exception ex) { throw new InvalidOperationException("Unity MCP relay could not be started.", ex); }

        var stderrTask = ReadBoundedAsync(process.StandardError, 16_000, token);
        try
        {
            await WriteMessageAsync(process.StandardInput, new
            {
                jsonrpc = "2.0", id = 1, method = "initialize",
                @params = new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = "ForgeCore", title = "ForgeCore supervised Unity worker", version = "0.10.0" }
                }
            }, token);
            using var initialize = await ReadResponseAsync(process, 1, token);
            var initializedResult = RequireResult(initialize.RootElement);
            var negotiated = initializedResult.TryGetProperty("protocolVersion", out var version) ? version.GetString() : null;
            if (negotiated is null || !SupportedProtocolVersions.Contains(negotiated))
                throw new InvalidDataException($"Unity MCP negotiated unsupported protocol version '{negotiated ?? "(missing)"}'.");
            if (!initializedResult.TryGetProperty("capabilities", out var capabilities) ||
                !capabilities.TryGetProperty("tools", out _))
                throw new InvalidDataException("Unity MCP server did not advertise the tools capability.");

            await WriteMessageAsync(process.StandardInput,
                new { jsonrpc = "2.0", method = "notifications/initialized" }, token);

            if (spec.Operation == "discover")
            {
                await WriteMessageAsync(process.StandardInput,
                    new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } }, token);
            }
            else
            {
                using var arguments = JsonDocument.Parse(spec.ArgumentsJson!, new JsonDocumentOptions { MaxDepth = 16 });
                await WriteMessageAsync(process.StandardInput, new
                {
                    jsonrpc = "2.0", id = 2, method = "tools/call",
                    @params = new { name = spec.ToolName, arguments = arguments.RootElement }
                }, token);
            }

            using var response = await ReadResponseAsync(process, 2, token);
            var result = RequireResult(response.RootElement);
            if (spec.Operation == "call" && result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
                throw new InvalidOperationException("Unity MCP tool reported an error: " + Bound(result.GetRawText()));
            return spec.Operation == "discover"
                ? "Unity MCP tools exposed by the currently approved Unity Editor session:\n" + Bound(result.GetRawText())
                : "Unity MCP tool completed through the approved Editor session:\n" + Bound(result.GetRawText());
        }
        finally
        {
            try { process.StandardInput.Close(); } catch (InvalidOperationException) { }
            if (!process.HasExited)
            {
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
                catch (TimeoutException) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
            }
            try { _ = await stderrTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task<JsonDocument> ReadResponseAsync(Process process, int expectedId, CancellationToken token)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(token);
            if (line is null)
                throw new InvalidDataException("Unity MCP relay closed before responding; inspect the Unity Console and ForgeCore log.");
            if (line.Length > _maxResponseCharacters) throw new InvalidDataException("Unity MCP response exceeded the configured size limit.");
            JsonDocument message;
            try { message = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 }); }
            catch (JsonException ex) { throw new InvalidDataException("Unity MCP stdout contained a non-JSON protocol message.", ex); }
            var root = message.RootElement;
            if (!root.TryGetProperty("jsonrpc", out var jsonRpc) || jsonRpc.GetString() != "2.0")
            {
                message.Dispose(); throw new InvalidDataException("Unity MCP response is not JSON-RPC 2.0.");
            }
            if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number) && number == expectedId)
                return message;
            message.Dispose(); // Notifications and unrelated server messages are deliberately ignored; no client capabilities were advertised.
        }
    }

    private static JsonElement RequireResult(JsonElement response)
    {
        if (response.TryGetProperty("error", out var error))
            throw new InvalidOperationException("Unity MCP protocol error: " + error.GetRawText());
        if (!response.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Unity MCP response did not contain an object result.");
        return result;
    }

    private static async Task WriteMessageAsync(StreamWriter writer, object message, CancellationToken token)
    {
        var json = JsonSerializer.Serialize(message);
        await writer.WriteLineAsync(json.AsMemory(), token);
        await writer.FlushAsync(token);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maxCharacters, CancellationToken token)
    {
        var output = new StringBuilder(Math.Min(maxCharacters, 4096));
        var buffer = new char[2048];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) break;
            var remaining = maxCharacters - output.Length;
            if (remaining > 0) output.Append(buffer, 0, Math.Min(read, remaining));
        }
        return output.ToString();
    }

    private string Bound(string value) => value.Length <= _maxResponseCharacters
        ? value : value[.._maxResponseCharacters] + "\n[Unity MCP response truncated]";

    public static string? NormalizeRelayPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathFullyQualified(candidate) || candidate.StartsWith("\\\\", StringComparison.Ordinal)) return null;
        try
        {
            var full = Path.GetFullPath(candidate);
            return Path.GetFileName(full).Equals("relay_win.exe", StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
}
