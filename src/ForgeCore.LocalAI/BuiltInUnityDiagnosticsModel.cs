using ForgeCore.Core;

namespace ForgeCore.LocalAI;

/// <summary>
/// Provides deterministic, low-latency plans for a deliberately small set of
/// read-only Unity diagnostics. Every action still passes through the normal
/// security policy and approval service.
/// </summary>
public sealed class BuiltInUnityDiagnosticsModel(ILocalModel inner, string unityProjectPath) : ILocalModel, IAsyncDisposable
{
    private readonly string _unityProjectPath = string.IsNullOrWhiteSpace(unityProjectPath)
        ? throw new ArgumentException("A relative Unity project path is required.", nameof(unityProjectPath))
        : unityProjectPath.Trim();

    public ModelStatus Status => inner.Status;

    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostic = Recognize(request.UserMessage);
        if (diagnostic is null) return inner.CreatePlanAsync(request, cancellationToken);

        var previous = request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityMcpAction);
        if (previous is not null)
            return Task.FromResult(new AgentPlan(DescribeResult(diagnostic.Value, previous), []));

        var (toolName, description, arguments) = diagnostic.Value switch
        {
            UnityDiagnostic.Models => ("Unity_AssetGeneration_GetModels",
                "Read the Unity AI asset-generation models available to the approved ForgeCore Editor session.",
                "{\"includeAllModels\":false}"),
            UnityDiagnostic.Console => ("Unity_GetConsoleLogs",
                "Read recent warnings and errors from the approved ForgeCore Unity Editor session.",
                "{\"maxEntries\":50,\"includeStackTrace\":true,\"logTypes\":\"Error,Warning\"}"),
            _ => throw new InvalidOperationException("Unsupported Unity diagnostic.")
        };
        var action = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, description,
            ("project_path", _unityProjectPath), ("operation", "call"),
            ("tool_name", toolName), ("arguments_json", arguments));
        return Task.FromResult(new AgentPlan(
            "I can run that read-only check in the open ForgeCore Unity project. Review the exact request in the approval window.", [action]));
    }

    private static UnityDiagnostic? Recognize(string message)
    {
        if (!message.Contains("unity", StringComparison.OrdinalIgnoreCase)) return null;
        if (ContainsAny(message, "ai model", "generation model", "asset model")) return UnityDiagnostic.Models;
        if (ContainsAny(message, "console log", "console warning", "console error", "unity log")) return UnityDiagnostic.Console;
        return null;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string DescribeResult(UnityDiagnostic diagnostic, ToolObservation observation)
    {
        if (!observation.Success)
            return $"The Unity diagnostic was approved but did not complete: {Truncate(observation.Detail)}";
        if (diagnostic == UnityDiagnostic.Models &&
            (observation.Detail.Contains("\"Models\": []", StringComparison.Ordinal) ||
             observation.Detail.Contains("\\\"Models\\\": []", StringComparison.Ordinal)))
            return "ForgeCore's secured Unity connection is working. Unity returned no asset-generation models for this account/project, so generation remains unavailable until Unity enables models for it.";
        return $"The secured Unity diagnostic completed successfully. Unity reported: {Truncate(observation.Detail)}";
    }

    private static string Truncate(string value) => value.Length <= 1_500 ? value : value[..1_500] + "…";

    public async ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (inner is IDisposable disposable) disposable.Dispose();
    }

    private enum UnityDiagnostic { Models, Console }
}
