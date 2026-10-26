using System.Security.Cryptography;
using ForgeCore.Core;

namespace ForgeCore.Security;

public sealed class SecurityPolicy(WorkspacePathGuard paths, IEnumerable<string> allowedCommands, IEnumerable<string>? allowedAssetHosts = null,
    bool enableOnlineCatalogSearch = false, string? unityEditorPath = null, string? unityEditorVersion = null,
    bool enableUnityMcp = false, string? unityMcpRelayPath = null, IEnumerable<string>? allowedUnityMcpTools = null,
    int maxUnityMcpArgumentsCharacters = 16_000, bool enableSeeleGeneration = false,
    string seeleApiKeyEnvironmentVariable = "SEELE_API_KEY", bool enableMeshyGeneration = false,
    string meshyApiKeyEnvironmentVariable = "MESHY_API_KEY", string? blenderPath = null, string blenderVersion = "5.2") : ISecurityPolicy
{
    private readonly HashSet<string> _allowed = new(allowedCommands, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _assetHosts = new(allowedAssetHosts ?? [], StringComparer.OrdinalIgnoreCase);
    private readonly string? _unityEditorPath = ValidateUnityEditorPath(unityEditorPath);
    private readonly string? _unityEditorVersion = ValidateUnityEditorVersion(unityEditorVersion);
    private readonly string? _unityMcpRelayPath = ValidateUnityMcpRelayPath(unityMcpRelayPath);
    private readonly HashSet<string> _allowedUnityMcpTools = new(allowedUnityMcpTools ?? [], StringComparer.Ordinal);
    private readonly string _seeleApiKeyEnvironmentVariable = ValidateEnvironmentVariableName(seeleApiKeyEnvironmentVariable);
    private readonly string _meshyApiKeyEnvironmentVariable = ValidateEnvironmentVariableName(meshyApiKeyEnvironmentVariable);

    public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (call.RequiredPermission == PermissionKind.Administrator)
            return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Administrator actions are not implemented."));

        try
        {
            var expectedPermission = call.Kind switch
            {
                ToolKind.ListFiles or ToolKind.ReadTextFile or ToolKind.IndexProject or ToolKind.SearchProject or ToolKind.InspectStagedAsset or ToolKind.SearchAssetVault => PermissionKind.Read,
                ToolKind.WriteTextFile or ToolKind.ApplyTextPatch or ToolKind.RestoreTextSnapshot or ToolKind.CreateDirectory or ToolKind.ScaffoldProject or ToolKind.ImportStagedAsset or ToolKind.InstallUnityBridge or ToolKind.ScaffoldUnityMapDemo or ToolKind.ScaffoldUnityGameTemplate or ToolKind.ReviewGameArtAsset or ToolKind.StoreReviewedAsset or ToolKind.ImportVaultAsset or ToolKind.ImportBlenderAsset => PermissionKind.Write,
                ToolKind.StageWebAsset or ToolKind.SearchAssetCatalog or ToolKind.Generate3DAsset => PermissionKind.Network,
                ToolKind.DotnetBuild or ToolKind.DotnetTest or ToolKind.RunCommand or ToolKind.UnityEditorAction or ToolKind.UnityMcpAction or ToolKind.UnityPlayerSmokeTest or ToolKind.UnityPlayerProfileTest or ToolKind.CreateBlenderAsset => PermissionKind.Execute,
                _ => throw new ArgumentException("Unknown tool.")
            };
            if (call.RequiredPermission != expectedPermission)
                return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, $"Tool requires {expectedPermission} permission."));
            switch (call.Kind)
            {
                case ToolKind.CreateBlenderAsset:
                    var blender = BlenderAssetSpec.From(call);
                    _ = RequireUnityProject(blender.ProjectPath);
                    if (string.IsNullOrWhiteSpace(blenderPath) || !Path.IsPathFullyQualified(blenderPath) ||
                        !string.Equals(Path.GetFileName(blenderPath), "blender.exe", StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(blenderPath) || blenderVersion != "5.2")
                        throw new ArgumentException("Local generation requires a configured existing Blender 5.2 executable; it will not install or download one.");
                    _ = paths.Resolve(".forgecore/blender-library", true);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Run the bundled fixed {blender.Recipe} recipe in local Blender 5.2, seed {blender.Seed}, {blender.HeightMillimetres} mm tall and {blender.RadiusMillimetres} mm radius. Retain source, GLB, PBR images and preview as an unreviewed immutable local bundle; reuse only an exact verified recipe match. No cloud, API key, download, arbitrary Python, or Unity import is included."));
                case ToolKind.ImportBlenderAsset:
                    var blenderImport = BlenderAssetImportSpec.From(call);
                    var blenderProject = RequireUnityProject(blenderImport.ProjectPath);
                    var blenderEntry = paths.Resolve($".forgecore/blender-library/objects/{blenderImport.BundleSha256}/entry.json", false);
                    if (!File.Exists(blenderEntry)) throw new ArgumentException("The exact local Blender bundle is missing.");
                    var blenderTarget = RequireUserPath(Path.Combine(blenderImport.ProjectPath, blenderImport.DestinationPath), true);
                    EnsureWithinProject(blenderProject, blenderTarget);
                    var blenderFolder = Path.GetDirectoryName(blenderTarget)!;
                    if (File.Exists(blenderFolder) || Directory.Exists(blenderFolder) || File.Exists(blenderFolder + ".meta"))
                        throw new ArgumentException("Local Blender import destination already exists.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Re-verify local bundle {blenderImport.BundleSha256}, editable source and exact GLB SHA-256 {blenderImport.ExpectedAssetSha256}; import without overwrite to {blenderImport.DestinationPath}. Source remains reusable; human visual and rights review are still pending."));
                case ToolKind.ListFiles:
                case ToolKind.ReadTextFile:
                    RequirePath(call, allowMissingLeaf: false);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Allowed, "Read-only workspace action."));
                case ToolKind.IndexProject:
                    var index = ProjectIndexSpec.From(call); var indexPath = RequireUserPath(index.Path, allowMissingLeaf: false);
                    if (!Directory.Exists(indexPath)) throw new ArgumentException("Project index path must identify an existing directory.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Allowed,
                        "Build a local metadata-only index of an existing workspace directory; source contents are not stored in the index."));
                case ToolKind.SearchProject:
                    var search = ProjectSearchSpec.From(call); var searchPath = RequireUserPath(search.Path, allowMissingLeaf: false);
                    if (!Directory.Exists(searchPath)) throw new ArgumentException("Project search path must identify an existing directory.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Allowed, "Search the local project metadata index."));
                case ToolKind.WriteTextFile:
                case ToolKind.CreateDirectory:
                    RequireUserPath(Get(call, "path"), allowMissingLeaf: true);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired, "This action changes workspace contents."));
                case ToolKind.ApplyTextPatch:
                    var patch = TextPatchSpec.From(call); var patchPath = RequireUserPath(patch.Path, allowMissingLeaf: false);
                    if (!File.Exists(patchPath)) throw new ArgumentException("Text patch path must identify an existing file.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        "Apply one exact text replacement and create a workspace-local backup snapshot."));
                case ToolKind.RestoreTextSnapshot:
                    var restore = TextSnapshotRestoreSpec.From(call); var restorePath = RequireUserPath(restore.Path, allowMissingLeaf: false);
                    if (!File.Exists(restorePath)) throw new ArgumentException("Snapshot restore target must identify an existing file.");
                    RequireBackupPath(restore.SnapshotPath, restorePath);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        "Restore a ForgeCore text backup after verifying the current file hash; the current version is snapshotted first."));
                case ToolKind.ScaffoldProject:
                    var project = ProjectScaffoldSpec.From(call);
                    RequireUserPath(project.Path, allowMissingLeaf: true);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Create a new {project.Template} project inside the approved workspace."));
                case ToolKind.StageWebAsset:
                    var asset = WebAssetSpec.From(call);
                    var projectRoot = RequireUserPath(asset.ProjectPath, allowMissingLeaf: false);
                    if (!Directory.Exists(projectRoot)) throw new ArgumentException("Asset project_path must identify an existing project directory.");
                    paths.Resolve(Path.Combine(asset.ProjectPath, ".forgecore", "staging", asset.FileName), allowMissingLeaf: true);
                    if (!_assetHosts.Contains(asset.DownloadUri.IdnHost) || !_assetHosts.Contains(asset.SourcePage.IdnHost))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Asset download and source-page hosts must both be on the explicit asset host allow-list."));
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Download a passive asset from {asset.DownloadUri.IdnHost} into the project's quarantine staging directory. License: {asset.License}."));
                case ToolKind.Generate3DAsset:
                    var generated = Generated3DAssetSpec.From(call);
                    var generatedProject = RequireUnityProject(generated.ProjectPath);
                    var providerEnabled = generated.Provider switch
                    {
                        "seele" => enableSeeleGeneration,
                        "meshy" => enableMeshyGeneration,
                        _ => false
                    };
                    var providerName = generated.Provider switch { "seele" => "Seele", "meshy" => "Meshy", _ => generated.Provider };
                    var keyVariable = generated.Provider switch
                    {
                        "seele" => _seeleApiKeyEnvironmentVariable,
                        "meshy" => _meshyApiKeyEnvironmentVariable,
                        _ => throw new ArgumentException("Unsupported 3D generation provider.")
                    };
                    if (!providerEnabled)
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, $"{providerName} 3D generation is disabled in configuration."));
                    var apiKey = Environment.GetEnvironmentVariable(keyVariable);
                    if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length is < 16 or > 512 || apiKey.Any(char.IsWhiteSpace))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied,
                            $"{providerName} API access requires a valid key in the {keyVariable} user environment variable."));
                    _ = generatedProject;
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Send this original 3D asset brief to {providerName} model {generated.Model}. The prompt leaves this PC, cloud credits may be consumed, and only supported passive outputs will be quarantined; Unity import remains a separate approval."));
                case ToolKind.InspectStagedAsset:
                    var inspection = StagedAssetInspectionSpec.From(call);
                    var inspectionProject = RequireUserPath(inspection.ProjectPath, allowMissingLeaf: false);
                    if (!Directory.Exists(inspectionProject)) throw new ArgumentException("Asset project_path must identify an existing project directory.");
                    var staged = paths.Resolve(Path.Combine(inspection.ProjectPath, ".forgecore", "staging", inspection.FileName));
                    var stagedManifest = paths.Resolve(Path.Combine(inspection.ProjectPath, ".forgecore", "staging", inspection.FileName + ".asset.json"));
                    EnsureWithinProject(inspectionProject, staged); EnsureWithinProject(inspectionProject, stagedManifest);
                    if (!File.Exists(staged) || !File.Exists(stagedManifest)) throw new ArgumentException("Staged asset and provenance manifest must both exist.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Allowed,
                        "Read and structurally inspect one quarantined passive asset; no project file is changed."));
                case ToolKind.ImportStagedAsset:
                    var import = StagedAssetImportSpec.From(call);
                    var importProject = RequireUserPath(import.ProjectPath, allowMissingLeaf: false);
                    if (!Directory.Exists(importProject)) throw new ArgumentException("Asset project_path must identify an existing project directory.");
                    var importSource = paths.Resolve(Path.Combine(import.ProjectPath, ".forgecore", "staging", import.FileName));
                    var importManifest = paths.Resolve(Path.Combine(import.ProjectPath, ".forgecore", "staging", import.FileName + ".asset.json"));
                    EnsureWithinProject(importProject, importSource); EnsureWithinProject(importProject, importManifest);
                    if (!File.Exists(importSource) || !File.Exists(importManifest)) throw new ArgumentException("Staged asset and provenance manifest must both exist.");
                    var importTarget = RequireUserPath(Path.Combine(import.ProjectPath, import.DestinationPath), allowMissingLeaf: true);
                    EnsureWithinProject(importProject, importTarget);
                    if (File.Exists(importTarget) || Directory.Exists(importTarget) || File.Exists(importTarget + ".asset.json") || Directory.Exists(importTarget + ".asset.json"))
                        throw new ArgumentException("Import destination or provenance sidecar already exists.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Re-inspect the quarantined asset, verify SHA-256 {import.ExpectedSha256}, and import it without overwrite to {import.DestinationPath} with a licence sidecar."));
                case ToolKind.SearchAssetVault:
                    var vaultSearch = AssetVaultSearchSpec.From(call);
                    _ = paths.Resolve(Path.Combine(".forgecore", "asset-vault"), allowMissingLeaf: true);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Allowed,
                        $"Search immutable local reviewed-asset metadata offline for '{vaultSearch.Query}'; no project or vault file is changed."));
                case ToolKind.StoreReviewedAsset:
                    var vaultStore = ReviewedAssetStoreSpec.From(call);
                    _ = RequireUnityProject(vaultStore.ProjectPath);
                    _ = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects"), allowMissingLeaf: true);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Verify current structural, visual, and rights evidence for asset set '{vaultStore.AssetSetId}' and archive the exact asset SHA-256 {vaultStore.ExpectedAssetSha256} into immutable local storage without changing the source project."));
                case ToolKind.ImportVaultAsset:
                    var vaultImport = VaultAssetImportSpec.From(call);
                    var vaultProject = RequireUnityProject(vaultImport.ProjectPath);
                    var vaultObject = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects", vaultImport.BundleSha256), allowMissingLeaf: false);
                    if (!Directory.Exists(vaultObject) || !File.Exists(Path.Combine(vaultObject, "entry.json")))
                        throw new ArgumentException("The exact asset-vault bundle does not exist.");
                    var vaultDestination = RequireUserPath(Path.Combine(vaultImport.ProjectPath, vaultImport.DestinationDirectory), allowMissingLeaf: true);
                    EnsureWithinProject(vaultProject, vaultDestination);
                    if (File.Exists(vaultDestination) || Directory.Exists(vaultDestination))
                        throw new ArgumentException("Asset-vault import destination already exists.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Re-verify immutable vault bundle {vaultImport.BundleSha256} and asset SHA-256 {vaultImport.ExpectedAssetSha256}, then copy it atomically without overwrite. The target project must perform a fresh Unity technical inspection before use."));
                case ToolKind.InstallUnityBridge:
                    var bridge = UnityBridgeInstallSpec.From(call);
                    var bridgeProject = RequireUnityProject(bridge.ProjectPath);
                    var bridgeFile = RequireUserPath(Path.Combine(bridge.ProjectPath, "Assets", "ForgeCore", "Editor", "ForgeCoreBridge.cs"), allowMissingLeaf: true);
                    EnsureWithinProject(bridgeProject, bridgeFile);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        "Install ForgeCore's deterministic, versioned Unity Editor bridge source inside this project. An exact hash-verified supported prior bridge may be upgraded with a protected workspace backup; unknown or modified bridge files are never overwritten."));
                case ToolKind.ScaffoldUnityMapDemo:
                    var mapDemo = UnityMapDemoSpec.From(call);
                    var mapProject = RequireUnityProject(mapDemo.ProjectPath);
                    var mapRoot = RequireUserPath(Path.Combine(mapDemo.ProjectPath, "Assets", "ForgeCoreMapDemo"), allowMissingLeaf: true);
                    EnsureWithinProject(mapProject, mapRoot);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        "Install ForgeCore's fixed, hash-verified Frontier Valley runtime and Editor map kit. Existing files are never overwritten."));
                case ToolKind.ScaffoldUnityGameTemplate:
                    var gameTemplate = UnityGameTemplateSpec.From(call);
                    var gameProject = RequireUnityProject(gameTemplate.ProjectPath);
                    var gameRoot = RequireUserPath(Path.Combine(gameTemplate.ProjectPath, "Assets", "ForgeCoreGameTemplates"), allowMissingLeaf: true);
                    EnsureWithinProject(gameProject, gameRoot);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Install ForgeCore's fixed, hash-verified shared prototype kit for the exact '{gameTemplate.TemplateId}' template. The kit supports only four reviewed prototype families and never overwrites unknown or modified files."));
                case ToolKind.ReviewGameArtAsset:
                    var review = GameArtReviewSpec.FromProposal(call);
                    var reviewProject = RequireUnityProject(review.ProjectPath);
                    var reviewAsset = RequireUserPath(Path.Combine(review.ProjectPath, review.AssetPath), allowMissingLeaf: false);
                    var reviewPreview = RequireUserPath(Path.Combine(review.ProjectPath, review.PreviewPath), allowMissingLeaf: false);
                    var reviewPreviewProvenance = RequireUserPath(Path.Combine(review.ProjectPath, review.PreviewPath + ".forgecore-art-preview.json"), allowMissingLeaf: false);
                    EnsureWithinProject(reviewProject, reviewAsset); EnsureWithinProject(reviewProject, reviewPreview); EnsureWithinProject(reviewProject, reviewPreviewProvenance);
                    if (!HashMatches(reviewAsset, review.ExpectedAssetSha256, 1, 536_870_912) ||
                        !HashMatches(reviewPreview, review.ExpectedPreviewSha256, 24, 33_554_432) ||
                        !HashMatches(reviewPreviewProvenance, review.ExpectedPreviewProvenanceSha256, 2, 1_048_576))
                        throw new ArgumentException("The exact game-art asset, preview, or preview provenance changed before human review.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        review.ReviewKind == GameArtReviewSpec.Visual
                            ? $"HUMAN ATTESTATION ONLY: inspect the exact four-view preview SHA-256 {review.ExpectedPreviewSha256} for asset SHA-256 {review.ExpectedAssetSha256}. Approve accepts visible quality and project suitability; reject records a failed visual review. Closing records nothing."
                            : $"HUMAN ATTESTATION ONLY: independently review the imported provenance and applicable licence/provider terms for asset SHA-256 {review.ExpectedAssetSha256}, bound through preview-provenance SHA-256 {review.ExpectedPreviewProvenanceSha256}. Approve accepts project use; reject records a failed rights review. Closing records nothing."));
                case ToolKind.UnityEditorAction:
                    var unity = UnityEditorActionSpec.From(call);
                    var unityProject = RequireUnityProject(unity.ProjectPath);
                    if (_unityEditorPath is null || !File.Exists(_unityEditorPath) || _unityEditorVersion is null)
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "UnityEditorPath and UnityEditorVersion are not configured to an existing local Unity.exe and explicit version."));
                    var projectVersion = ReadUnityProjectVersion(unityProject);
                    if (!string.Equals(projectVersion, _unityEditorVersion, StringComparison.Ordinal))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, $"Unity project version {projectVersion} does not match configured Editor version {_unityEditorVersion}."));
                    var installedBridge = paths.Resolve(Path.Combine(unity.ProjectPath, "Assets", "ForgeCore", "Editor", "ForgeCoreBridge.cs"));
                    EnsureWithinProject(unityProject, installedBridge);
                    if (!File.Exists(installedBridge))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "The versioned ForgeCore Unity bridge is not installed in this project."));
                    if (unity.Operation is "create_game_template" or "create_game_campaign" or "verify_gameplay" or "profile_scene")
                    {
                        var gameManifest = paths.Resolve(Path.Combine(unity.ProjectPath, "Assets", "ForgeCoreGameTemplates", "forgecore-game-templates.manifest.json"));
                        EnsureWithinProject(unityProject, gameManifest);
                        if (!File.Exists(gameManifest))
                            return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "The ForgeCore game-template kit is not installed in this project."));
                    }
                    if (unity.Operation == "create_game_campaign")
                    {
                        foreach (var campaignScenePath in unity.GetCampaignScenePaths())
                        {
                            var campaignScene = RequireUserPath(Path.Combine(unity.ProjectPath, campaignScenePath), allowMissingLeaf: true);
                            EnsureWithinProject(unityProject, campaignScene);
                            if (File.Exists(campaignScene) || Directory.Exists(campaignScene)) throw new ArgumentException("A Unity campaign scene target already exists.");
                        }
                    }
                    else if (unity.Operation == "build_windows" && unity.LevelCount is not null)
                    {
                        foreach (var campaignScenePath in unity.GetCampaignScenePaths())
                        {
                            var campaignScene = RequireUserPath(Path.Combine(unity.ProjectPath, campaignScenePath), allowMissingLeaf: false);
                            EnsureWithinProject(unityProject, campaignScene);
                            if (!File.Exists(campaignScene)) throw new ArgumentException("A requested Unity campaign build scene does not exist.");
                        }
                    }
                    else if (unity.ScenePath is not null)
                    {
                        var scene = RequireUserPath(Path.Combine(unity.ProjectPath, unity.ScenePath), allowMissingLeaf: unity.Operation is "create_scene" or "create_map_demo" or "create_game_template");
                        EnsureWithinProject(unityProject, scene);
                        if (unity.Operation is "create_scene" or "create_map_demo" or "create_game_template" && (File.Exists(scene) || Directory.Exists(scene))) throw new ArgumentException("Unity scene creation target already exists.");
                        if (unity.Operation is not "create_scene" and not "create_map_demo" and not "create_game_template" && !File.Exists(scene)) throw new ArgumentException("Unity scene_path does not exist.");
                    }
                    if (unity.Operation is "prepare_generated_asset" or "inspect_game_art_asset" or "capture_game_art_preview")
                    {
                        var generatedAsset = RequireUserPath(Path.Combine(unity.ProjectPath, unity.AssetPath!), allowMissingLeaf: false);
                        var provenance = RequireUserPath(Path.Combine(unity.ProjectPath, unity.AssetPath! + ".asset.json"), allowMissingLeaf: false);
                        EnsureWithinProject(unityProject, generatedAsset); EnsureWithinProject(unityProject, provenance);
                        if (!File.Exists(generatedAsset) || !File.Exists(provenance))
                            throw new ArgumentException("Generated asset work requires the imported GLB and provenance sidecar.");
                        if (unity.Operation is "inspect_game_art_asset" or "capture_game_art_preview")
                        {
                            var meta = RequireUserPath(Path.Combine(unity.ProjectPath, unity.AssetPath! + ".meta"), allowMissingLeaf: false);
                            EnsureWithinProject(unityProject, meta);
                            if (!File.Exists(meta)) throw new ArgumentException("Game-art inspection or preview requires existing Unity metadata for the imported GLB.");
                        }
                    }
                    if (unity.OutputPath is not null)
                    {
                        var output = RequireUserPath(Path.Combine(unity.ProjectPath, unity.OutputPath), allowMissingLeaf: true);
                        EnsureWithinProject(unityProject, output);
                        if (unity.Operation is "capture_scene_preview" or "capture_game_art_preview")
                        {
                            var previewProvenance = output + (unity.Operation == "capture_game_art_preview" ? ".forgecore-art-preview.json" : ".forgecore-preview.json");
                            EnsureWithinProject(unityProject, previewProvenance);
                            if (File.Exists(output) || Directory.Exists(output) || File.Exists(previewProvenance) || Directory.Exists(previewProvenance))
                                throw new ArgumentException("Unity scene preview output or ForgeCore provenance already exists; previews never overwrite.");
                        }
                        else
                        {
                            var dataDirectory = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "_Data");
                            var buildProvenance = output + ".forgecore-build.json";
                            EnsureWithinProject(unityProject, buildProvenance);
                            if (File.Exists(output) || Directory.Exists(output) || File.Exists(dataDirectory) || Directory.Exists(dataDirectory) ||
                                File.Exists(buildProvenance) || Directory.Exists(buildProvenance))
                                throw new ArgumentException("Unity build output, its _Data directory, or ForgeCore provenance already exists; builds never overwrite.");
                        }
                    }
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        unity.Operation == "prepare_generated_asset"
                            ? $"Launch Unity in bounded batch mode to verify the exact generated GLB hash, instantiate it under '{unity.ParentObjectName}', scale it to {unity.TargetHeight:0.##} metres, add {unity.ColliderMode} collision, optionally hide placeholder visuals, and save the backed-up scene. No package is installed."
                            : unity.Operation == "inspect_game_art_asset"
                                ? $"Launch Unity in bounded batch mode to hash-verify and inspect the exact imported GLB as art role '{unity.AssetRole}' in style family '{unity.StyleFamily}'. Mesh, material, rig, animation, LOD, collider, bounds, and missing-resource evidence is written locally; the asset and scene are not modified, and rights/visual review remain incomplete."
                            : unity.Operation == "capture_game_art_preview"
                                ? $"Launch Unity in bounded batch mode to hash-verify the exact imported GLB and render one non-overwriting local four-view preview at '{unity.OutputPath}'. The GLB, provenance, and Unity metadata are protected; no scene is saved, nothing is uploaded, and the image does not itself approve visual quality or rights."
                            : unity.Operation == "create_game_campaign"
                                ? $"Launch Unity in bounded batch mode to create {unity.LevelCount} new non-overwriting linked '{unity.TemplateId}' campaign scenes from {unity.ScenePath}. Each scene receives a deterministic derived seed and reviewed completion transition; project settings are backed up."
                                : unity.Operation == "build_windows" && unity.LevelCount is not null
                                    ? $"Launch Unity in bounded batch mode to build exactly {unity.LevelCount} linked campaign scenes beginning at {unity.ScenePath} into the displayed non-overwriting Windows output."
                                : unity.Operation == "capture_scene_preview"
                                    ? $"Launch the configured Unity Editor, run only {unity.ScenePath} for {unity.DurationSeconds} seconds, and create one non-overwriting PNG preview at {unity.OutputPath}. The preview stays local and is not uploaded."
                                : unity.Operation == "verify_gameplay"
                                    ? $"Launch the configured Unity Editor for a bounded {unity.DurationSeconds}-second deterministic runtime verification of {unity.ScenePath}. The reviewed template contract exercises player movement, respawn, ordered objectives, and completion without saving the Play Mode state."
                                : unity.Operation == "profile_scene"
                                    ? $"Launch the configured Unity Editor for a bounded {unity.DurationSeconds}-second Play Mode profile of {unity.ScenePath}. Record editor-update interval distribution plus peak draw calls, SetPass calls, triangles, vertices, and allocated memory without saving Play Mode state; this is a prototype budget gate, not rendered-frame or target-hardware certification."
                            : $"Launch the configured Unity Editor in bounded batch mode for the typed '{unity.Operation}' operation. Project code executes with normal-user permissions; no shell or package installation is requested."));
                case ToolKind.UnityPlayerSmokeTest:
                    var smoke = UnityPlayerSmokeTestSpec.From(call);
                    var smokeProject = RequireUnityProject(smoke.ProjectPath);
                    var smokeExecutable = RequireUserPath(Path.Combine(smoke.ProjectPath, smoke.OutputPath), allowMissingLeaf: false);
                    EnsureWithinProject(smokeProject, smokeExecutable);
                    var smokeData = Path.Combine(Path.GetDirectoryName(smokeExecutable)!, Path.GetFileNameWithoutExtension(smokeExecutable) + "_Data");
                    var smokeProvenance = smokeExecutable + ".forgecore-build.json";
                    EnsureWithinProject(smokeProject, smokeData); EnsureWithinProject(smokeProject, smokeProvenance);
                    if (!File.Exists(smokeExecutable) || !Directory.Exists(smokeData) || !File.Exists(Path.Combine(smokeData, "level0")) || !File.Exists(smokeProvenance))
                        throw new ArgumentException("Unity player smoke test requires an existing ForgeCore-provenance Windows build and level0 data.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Launch the exact ForgeCore-provenance Windows player in bounded headless mode for {smoke.DurationSeconds} seconds, scan its local log for startup failures, then terminate only that test process. Project code executes as the current normal user; no shell, network, or package installation is requested."));
                case ToolKind.UnityPlayerProfileTest:
                    var profile = UnityPlayerProfileTestSpec.From(call);
                    var profileProject = RequireUnityProject(profile.ProjectPath);
                    var profileExecutable = RequireUserPath(Path.Combine(profile.ProjectPath, profile.OutputPath), allowMissingLeaf: false);
                    EnsureWithinProject(profileProject, profileExecutable);
                    var profileData = Path.Combine(Path.GetDirectoryName(profileExecutable)!, Path.GetFileNameWithoutExtension(profileExecutable) + "_Data");
                    var profileProvenance = profileExecutable + ".forgecore-build.json";
                    EnsureWithinProject(profileProject, profileData); EnsureWithinProject(profileProject, profileProvenance);
                    if (!File.Exists(profileExecutable) || !Directory.Exists(profileData) || !File.Exists(Path.Combine(profileData, "level0")) || !File.Exists(profileProvenance))
                        throw new ArgumentException("Unity player profile test requires an existing ForgeCore-provenance Windows build and level0 data.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Launch the exact hash-verified ForgeCore Windows player with visible rendering at {UnityPlayerProfileTestSpec.Width}x{UnityPlayerProfileTestSpec.Height} for a bounded {profile.DurationSeconds}-second measurement. A hash-bound local request drives the fixed scripted_gameplay_v1 input scenario and collects standalone frame, optional GPU, memory, scene-complexity, movement, and genre-action evidence; only the launched process may be terminated. No shell, network, package installation, project edit, or upload is requested."));
                case ToolKind.UnityMcpAction:
                    var mcp = UnityMcpActionSpec.From(call, maxUnityMcpArgumentsCharacters);
                    var mcpProject = RequireUnityProject(mcp.ProjectPath);
                    if (!enableUnityMcp)
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Unity MCP is disabled in configuration."));
                    if (_unityMcpRelayPath is null || !File.Exists(_unityMcpRelayPath))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "The configured Unity MCP relay_win.exe does not exist."));
                    if (_unityEditorVersion is null || !string.Equals(ReadUnityProjectVersion(mcpProject), _unityEditorVersion, StringComparison.Ordinal))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Unity MCP project version must exactly match the configured UnityEditorVersion."));
                    var packageManifest = paths.Resolve(Path.Combine(mcp.ProjectPath, "Packages", "manifest.json"));
                    EnsureWithinProject(mcpProject, packageManifest);
                    if (!HasUnityAssistantPackage(packageManifest))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "The selected project does not declare the official com.unity.ai.assistant package."));
                    if (mcp.Operation == "call" && !_allowedUnityMcpTools.Contains(mcp.ToolName!))
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Unity MCP tool is not on the explicit AllowedUnityMcpTools allow-list."));
                    if (mcp.Operation == "call") ValidateUnityMcpArguments(mcp, mcpProject);
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        mcp.Operation == "discover"
                            ? "Start Unity's official local MCP relay and list the tools exposed by the Unity Editor session you approve. No tool is invoked."
                            : $"Invoke the exact allow-listed Unity MCP tool '{mcp.ToolName}' in the Unity Editor session you approve. Arguments are displayed below; project code and Unity AI services may run as the current user."));
                case ToolKind.SearchAssetCatalog:
                    var catalog = AssetCatalogSearchSpec.From(call);
                    if (!enableOnlineCatalogSearch)
                        return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Online asset catalogue search is disabled in configuration."));
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Send the approved {catalog.MediaType} query to the fixed Openverse API. Results are untrusted metadata and the process-memory cache expires automatically."));
                case ToolKind.DotnetBuild:
                case ToolKind.DotnetTest:
                    var dotnet = DotnetCommandSpec.From(call); var dotnetPath = RequireUserPath(dotnet.ProjectPath, allowMissingLeaf: false);
                    if (!File.Exists(dotnetPath)) throw new ArgumentException(".NET project_path does not exist.");
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.ApprovalRequired,
                        $"Run a bounded .NET {(call.Kind == ToolKind.DotnetBuild ? "build" : "test")} with package restore restricted to an empty local feed."));
                case ToolKind.RunCommand:
                    var command = Get(call, "command");
                    return ValueTask.FromResult(_allowed.Contains(command)
                        ? new PolicyResult(SecurityDecision.ApprovalRequired, "Allow-listed process execution requires approval.")
                        : new PolicyResult(SecurityDecision.Denied, "Command is not on the explicit allow-list."));
                default:
                    return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, "Unknown tool."));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or IOException)
        {
            return ValueTask.FromResult(new PolicyResult(SecurityDecision.Denied, ex.Message));
        }
    }

    private void RequirePath(ToolCall call, bool allowMissingLeaf) => paths.Resolve(Get(call, "path"), allowMissingLeaf);
    private string RequireUserPath(string candidate, bool allowMissingLeaf)
    {
        var fullPath = paths.Resolve(candidate, allowMissingLeaf);
        var relative = Path.GetRelativePath(paths.WorkspaceRoot, fullPath);
        if (ContainsForgeCoreSegment(relative))
            throw new UnauthorizedAccessException("Every .forgecore directory is reserved for internal snapshots, staging, and metadata.");
        return fullPath;
    }
    private static bool ContainsForgeCoreSegment(string relative) =>
        relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(".forgecore", StringComparison.OrdinalIgnoreCase));

    private static bool HashMatches(string path, string expectedHash, long minimumBytes, long maximumBytes)
    {
        if (!File.Exists(path)) return false;
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length < minimumBytes || info.Length > maximumBytes) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.SequentialScan);
        var actual = SHA256.HashData(stream);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHash));
    }

    private static string ValidateEnvironmentVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || !char.IsLetter(value[0]) ||
            value.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("API key environment-variable name is invalid.", nameof(value));
        return value;
    }
    private string RequireBackupPath(string candidate, string targetPath)
    {
        var fullPath = paths.Resolve(candidate);
        var relative = Path.GetRelativePath(paths.WorkspaceRoot, fullPath);
        var segments = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var forgeCoreIndex = Array.FindIndex(segments, segment => segment.Equals(".forgecore", StringComparison.OrdinalIgnoreCase));
        var generalBackup = forgeCoreIndex == 0 && segments.Length >= 3 && segments[1].Equals("backups", StringComparison.OrdinalIgnoreCase);
        var unityBackup = forgeCoreIndex >= 0 && segments.Length > forgeCoreIndex + 3 &&
                          segments[forgeCoreIndex + 1].Equals("unity", StringComparison.OrdinalIgnoreCase) &&
                          segments[forgeCoreIndex + 2].Equals("backups", StringComparison.OrdinalIgnoreCase);
        if (unityBackup && forgeCoreIndex > 0)
        {
            var projectRelative = Path.Combine(segments[..forgeCoreIndex]);
            var targetRelative = Path.GetRelativePath(paths.WorkspaceRoot, targetPath);
            unityBackup = targetRelative.StartsWith(projectRelative + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        if ((!generalBackup && !unityBackup) || !Path.GetExtension(fullPath).Equals(".bak", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            throw new UnauthorizedAccessException("snapshot_path must identify an existing ForgeCore .bak file under the general or same-project Unity backup directory.");
        return fullPath;
    }
    private static void EnsureWithinProject(string projectRoot, string candidate)
    {
        var relative = Path.GetRelativePath(projectRoot, candidate);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("Asset path escapes the selected project directory.");
    }
    private static string Get(ToolCall call, string key) => call.Arguments.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing '{key}' argument.");

    private string RequireUnityProject(string projectPath)
    {
        var project = RequireUserPath(projectPath, allowMissingLeaf: false);
        if (!Directory.Exists(project) || !Directory.Exists(paths.Resolve(Path.Combine(projectPath, "Assets"))) ||
            !File.Exists(paths.Resolve(Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt"))))
            throw new ArgumentException("project_path must identify an existing Unity project with Assets and ProjectSettings/ProjectVersion.txt.");
        return project;
    }

    private static string? ValidateUnityEditorPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (!Path.IsPathFullyQualified(candidate) || candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            !Path.GetFileName(candidate).Equals("Unity.exe", StringComparison.OrdinalIgnoreCase)) return null;
        try { return Path.GetFullPath(candidate); } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static string? ValidateUnityEditorVersion(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^\d{4}\.\d+\.\d+[a-z]\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? candidate : null;

    private static string? ValidateUnityMcpRelayPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathFullyQualified(candidate) || candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            !Path.GetFileName(candidate).Equals("relay_win.exe", StringComparison.OrdinalIgnoreCase)) return null;
        try { return Path.GetFullPath(candidate); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static bool HasUnityAssistantPackage(string manifestPath)
    {
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 1_048_576) return false;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(manifestPath),
                new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("dependencies", out var dependencies) ||
                dependencies.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !dependencies.TryGetProperty("com.unity.ai.assistant", out var version) ||
                version.ValueKind != System.Text.Json.JsonValueKind.String) return false;
            var value = version.GetString();
            return !string.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value,
                @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private void ValidateUnityMcpArguments(UnityMcpActionSpec spec, string projectRoot)
    {
        using var document = System.Text.Json.JsonDocument.Parse(spec.ArgumentsJson!,
            new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 });
        Walk(document.RootElement, null);

        void Walk(System.Text.Json.JsonElement element, string? propertyName)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name;
                    if (name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("credential", StringComparison.OrdinalIgnoreCase) || name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("command", StringComparison.OrdinalIgnoreCase) || name.Equals("executable", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("shell", StringComparison.OrdinalIgnoreCase) || name.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("registry", StringComparison.OrdinalIgnoreCase) || name.Equals("package", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("url", StringComparison.OrdinalIgnoreCase) || name.EndsWith("uri", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"Unity MCP argument field '{name}' is not permitted.");
                    Walk(property.Value, name);
                }
            }
            else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray()) Walk(item, propertyName);
            }
            else if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = element.GetString() ?? string.Empty;
                if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(value) || value.StartsWith("\\\\", StringComparison.Ordinal))
                    throw new ArgumentException("Unity MCP arguments cannot contain absolute, file-URI, device, or UNC paths.");
                if (propertyName is not null && IsPathField(propertyName) && value.Length > 0)
                {
                    var candidate = paths.Resolve(Path.Combine(spec.ProjectPath, value), allowMissingLeaf: true);
                    EnsureWithinProject(projectRoot, candidate);
                }
            }
        }
    }

    private static bool IsPathField(string name) =>
        name.Contains("path", StringComparison.OrdinalIgnoreCase) || name.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("folder", StringComparison.OrdinalIgnoreCase) || name.EndsWith("file", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("filename", StringComparison.OrdinalIgnoreCase);

    private static string ReadUnityProjectVersion(string projectRoot)
    {
        var line = File.ReadLines(Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt")).FirstOrDefault();
        const string prefix = "m_EditorVersion: ";
        if (line is null || !line.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException("Unity ProjectVersion.txt does not declare m_EditorVersion.");
        var value = line[prefix.Length..].Trim();
        return ValidateUnityEditorVersion(value) ?? throw new ArgumentException("Unity ProjectVersion.txt contains an invalid editor version.");
    }
}
