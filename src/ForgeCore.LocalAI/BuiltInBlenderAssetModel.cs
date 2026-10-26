using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

/// <summary>One explicit offline recipe. An explicit unsupported Blender creation request never falls through to cloud.</summary>
public sealed class BuiltInBlenderAssetModel(ILocalModel inner, string projectPath) : ILocalModel, IAsyncDisposable
{
    private static readonly Regex Prompt = new(@"^\s*(?:create|make|generate)\s+(?:an?\s+)?(?:local\s+)?(?:weathered\s+)?barrel\s+(?:in|using|with)\s+blender(?:\s+and\s+import\s+it)?(?:[.;]\s*|\s+)name\s+it\s+([a-z][a-z0-9_-]{1,47})[.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public ModelStatus Status => inner.Status;
    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var match = Prompt.Match(request.UserMessage);
        if (!match.Success)
        {
            if (Regex.IsMatch(request.UserMessage, @"\bblender\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                Regex.IsMatch(request.UserMessage, @"\b(create|make|generate)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return Task.FromResult(new AgentPlan("The offline Blender adapter currently supports one fixed recipe only. Use: Create a weathered barrel in Blender. Name it yard-barrel. Characters, arbitrary scripts, animations and other asset recipes are not implemented, and this request will not be sent to a cloud generator.", []));
            return inner.CreatePlanAsync(request, cancellationToken);
        }
        var name = match.Groups[1].Value.ToLowerInvariant();
        var imported = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ImportBlenderAsset);
        if (imported is not null)
        {
            if (!imported.Success) return Task.FromResult(new AgentPlan("The local Blender import stopped: " + imported.Detail, []));
            try { return Task.FromResult(InspectImported(request, imported, name)); }
            catch (ArgumentException) { return Task.FromResult(new AgentPlan("The local import identity is invalid; Unity preparation stopped.", [])); }
        }
        var created = request.Observations.LastOrDefault(x => x.Kind == ToolKind.CreateBlenderAsset);
        if (created is null)
        {
            var call = ToolCall.Create(ToolKind.CreateBlenderAsset, PermissionKind.Execute,
                "Create or reuse the exact original local weathered-barrel recipe; no paid generation or downloads.",
                ("project_path", projectPath), ("asset_name", name), ("recipe", BlenderAssetSpec.SupportedRecipe),
                ("seed", "41"), ("height_mm", "1100"), ("radius_mm", "340"));
            _ = BlenderAssetSpec.From(call);
            return Task.FromResult(new AgentPlan("ForgeCore will run its bounded original barrel recipe in installed Blender, or reuse the exact verified local recipe bundle. Execution and subsequent Unity import use separate scoped approvals; visual review remains pending.", [call]));
        }
        if (!created.Success) return Task.FromResult(new AgentPlan("Local Blender creation stopped; no import will run: " + created.Detail, []));
        try
        {
            using var receipt = JsonDocument.Parse(created.Detail);
            var call = ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write,
                "Import the exact locally generated barrel GLB and provenance without changing its reusable source.",
                ("project_path", projectPath), ("asset_name", name),
                ("bundle_sha256", receipt.RootElement.GetProperty("bundleSha256").GetString()!),
                ("expected_asset_sha256", receipt.RootElement.GetProperty("assetSha256").GetString()!));
            _ = BlenderAssetImportSpec.From(call);
            return Task.FromResult(new AgentPlan("The local source bundle is ready. ForgeCore will re-verify all bundle hashes and import only this exact GLB to a fresh Unity folder. No visual or rights approval is implied.", [call]));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(new AgentPlan("Blender returned no valid exact-hash receipt; no import will be proposed.", []));
        }
    }
    private AgentPlan InspectImported(ModelRequest request, ToolObservation imported, string name)
    {
        var spec = BlenderAssetImportSpec.From(new(imported.ToolCallId, imported.Kind, imported.Arguments, PermissionKind.Write, imported.Description));
        if (spec.ProjectPath != projectPath || spec.AssetName != name) throw new ArgumentException("Import scope mismatch.");
        AgentPlan Step(ToolKind kind, PermissionKind permission, string description, params (string Key, string Value)[] fields) =>
            new(description, [ToolCall.Create(kind, permission, description, new[] { ("project_path", projectPath) }.Concat(fields).ToArray())]);
        var install = request.Observations.LastOrDefault(x => x.Kind == ToolKind.InstallUnityBridge);
        if (install is null) return Step(ToolKind.InstallUnityBridge, PermissionKind.Write, "Install or safely upgrade the hash-verified Unity inspection bridge; preserve unknown files.");
        if (!install.Success) return new("Local source and import are retained, but Unity bridge installation stopped: " + install.Detail, []);
        ToolObservation? Find(string operation) => request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var value) && value == operation);
        var compile = Find("compile");
        if (compile is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile the trusted bridge and synchronously import the local GLB in Unity.", ("operation", "compile"));
        if (!compile.Success) return new("Local source and import are retained, but Unity compilation stopped: " + compile.Detail, []);
        var inspection = Find("inspect_game_art_asset");
        if (inspection is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Measure the exact local barrel's Unity geometry, PBR resources and canonical transforms; no approval is implied.",
            ("operation", "inspect_game_art_asset"), ("asset_path", spec.DestinationPath), ("expected_sha256", spec.ExpectedAssetSha256),
            ("asset_set_id", name), ("asset_role", "environment_props"), ("style_family", "realistic_pbr"));
        if (!inspection.Success || !inspection.Detail.Contains("structural_ready=true", StringComparison.OrdinalIgnoreCase))
            return new("Unity structural readiness did not pass; no scene placement or quality approval was made. " + inspection.Detail, []);
        var preview = Find("capture_game_art_preview");
        if (preview is null) return Step(ToolKind.UnityEditorAction, PermissionKind.Execute, "Render a fresh four-view Unity review sheet of the exact imported barrel.",
            ("operation", "capture_game_art_preview"), ("asset_path", spec.DestinationPath), ("expected_sha256", spec.ExpectedAssetSha256),
            ("asset_set_id", name), ("output_path", $"Previews/Blender/{name}-{spec.ExpectedAssetSha256[..12]}.png"));
        return preview.Success
            ? new("The original local Blender barrel was created or exact-source reused, retained as editable source, hash-verified and imported. Unity structural inspection and a four-view preview passed. Human visual/rights approval, collision, LOD, game-specific placement and runtime performance acceptance remain pending. No scene or existing game was changed by this asset request.", [])
            : new("The barrel source and Unity inspection are retained, but its review render failed: " + preview.Detail, []);
    }
    public async ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (inner is IDisposable disposable) disposable.Dispose();
    }
}
