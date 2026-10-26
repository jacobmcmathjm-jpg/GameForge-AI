using System.Text.RegularExpressions;
using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

/// <summary>
/// Routes a deliberately narrow, explicitly named 3D-generation request through
/// deterministic typed calls. This avoids placing a credit-consuming cloud tool
/// inside the native llama grammar while preserving the ordinary policy,
/// approval, audit, quarantine, inspection, and import boundaries.
/// </summary>
public sealed class BuiltInSupervised3DModel(ILocalModel inner, string unityProjectPath,
    string preferredProvider = "seele") : ILocalModel, IAsyncDisposable
{
    private static readonly Regex AssetNamePattern = new(@"\bname\s+it\s+([A-Za-z][A-Za-z0-9_-]{1,47})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrepareAssetPattern = new(@"\bprepare\s+(?:the\s+)?imported\s+(?:3d\s+)?asset(?:\s+named)?\s+([A-Za-z][A-Za-z0-9_-]{1,47})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Sha256Pattern = new(@"\b[0-9a-f]{64}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PreviewIdentityPattern = new(@"four views, [0-9]+ bytes, SHA-256 (?<preview>[0-9a-f]{64})\); provenance .+? \(SHA-256 (?<provenance>[0-9a-f]{64})\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly string _unityProjectPath = string.IsNullOrWhiteSpace(unityProjectPath)
        ? throw new ArgumentException("A relative Unity project path is required.", nameof(unityProjectPath))
        : unityProjectPath.Trim();
    private readonly string _provider = Generated3DAssetSpec.SupportedProviders.Contains(preferredProvider)
        ? preferredProvider : throw new ArgumentException("Preferred 3D generation provider is unsupported.", nameof(preferredProvider));
    private string Model => _provider == "meshy" ? "meshy-6" : "Seele02-flash";
    private string ProviderName => _provider == "meshy" ? "Meshy" : "Seele";

    public ModelStatus Status => inner.Status;

    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preparedAssetName = RecognizePreparation(request.UserMessage);
        if (preparedAssetName is not null) return Task.FromResult(CreatePreparationPlan(request, preparedAssetName, null));
        var assetName = Recognize(request.UserMessage);
        if (assetName is null) return inner.CreatePlanAsync(request, cancellationToken);
        var fileName = assetName + ".glb";

        var vaultImported = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ImportVaultAsset);
        if (vaultImported is not null)
        {
            if (!vaultImported.Success) return Task.FromResult(new AgentPlan($"The reviewed vault asset was not imported: {Truncate(vaultImported.Detail)}", []));
            if (!vaultImported.Arguments.TryGetValue("expected_asset_sha256", out var vaultHash) || vaultHash.Length != 64 ||
                !vaultImported.Arguments.TryGetValue("destination_directory", out var vaultDestination))
                return Task.FromResult(new AgentPlan("The vault import succeeded but its exact asset hash or destination identity was unavailable, so ForgeCore will not prepare it.", []));
            var vaultAssetPath = vaultDestination.Replace('\\', '/').TrimEnd('/') + "/" + fileName;
            return Task.FromResult(CreatePreparationPlan(request, assetName, vaultHash.ToLowerInvariant(), vaultAssetPath, fromVault: true));
        }

        var imported = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ImportStagedAsset);
        if (imported is not null)
            return Task.FromResult(imported.Success
                ? CreatePreparationPlan(request, assetName, imported.Arguments.TryGetValue("expected_sha256", out var importedHash) ? importedHash : null)
                : new AgentPlan($"The generated asset was not imported: {Truncate(imported.Detail)}", []));

        var inspected = request.Observations.LastOrDefault(x => x.Kind == ToolKind.InspectStagedAsset);
        if (inspected is not null)
        {
            if (!inspected.Success) return Task.FromResult(new AgentPlan($"The quarantined generated asset failed inspection: {Truncate(inspected.Detail)}", []));
            var hash = Sha256Pattern.Match(inspected.Detail).Value.ToLowerInvariant();
            if (hash.Length != 64) return Task.FromResult(new AgentPlan("Inspection succeeded but did not return an exact SHA-256, so ForgeCore will not propose an import.", []));
            var import = ToolCall.Create(ToolKind.ImportStagedAsset, PermissionKind.Write,
                "Import the exact inspected generated GLB into the approved Unity project without overwriting.",
                ("project_path", _unityProjectPath), ("file_name", fileName),
                ("destination_path", $"Assets/ForgeCore/Generated/{fileName}"), ("expected_sha256", hash));
            return Task.FromResult(new AgentPlan("The quarantined GLB passed structural and hash inspection. Review the separate exact-file Unity import approval.", [import]));
        }

        var generated = request.Observations.LastOrDefault(x => x.Kind == ToolKind.Generate3DAsset);
        if (generated is not null)
        {
            if (!generated.Success) return Task.FromResult(new AgentPlan($"The approved {ProviderName} generation did not complete: {Truncate(generated.Detail)}", []));
            if (!generated.Detail.Contains(fileName + " (", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new AgentPlan($"{ProviderName} finished, but no structurally valid downloadable GLB was quarantined. Review the provider result; ForgeCore will not import a preview or unsupported artifact.", []));
            var inspect = ToolCall.Create(ToolKind.InspectStagedAsset, PermissionKind.Read,
                "Re-inspect the exact quarantined generated GLB before any project import.",
                ("project_path", _unityProjectPath), ("file_name", fileName));
            return Task.FromResult(new AgentPlan("The generated GLB is quarantined. ForgeCore will now verify its manifest, exact hash, byte count, and bounded GLB structure.", [inspect]));
        }

        var vaultSearch = request.Observations.LastOrDefault(x => x.Kind == ToolKind.SearchAssetVault);
        if (vaultSearch is null)
        {
            var search = ToolCall.Create(ToolKind.SearchAssetVault, PermissionKind.Read,
                "Search ForgeCore's offline reviewed-asset vault before spending cloud credits on another model.",
                ("query", assetName.Replace('-', ' ').Replace('_', ' ')), ("asset_role", "environment_props"),
                ("style_family", "realistic_pbr"), ("max_results", "5"));
            _ = AssetVaultSearchSpec.From(search);
            return Task.FromResult(new AgentPlan("ForgeCore will first check its private local reviewed-asset vault for the same named prop. This is offline and read-only; a matching immutable bundle can be reused only by exact hash and a separate non-overwriting project import.", [search]));
        }
        if (vaultSearch.Success && TryReadVaultCandidate(vaultSearch.Detail, assetName, out var bundleHash, out var vaultAssetHash))
        {
            var destination = $"Assets/ForgeCore/Generated/Vault/{assetName}-{bundleHash[..8]}";
            var importVault = ToolCall.Create(ToolKind.ImportVaultAsset, PermissionKind.Write,
                "Import the exact immutable reviewed asset from local storage without overwriting target-project files.",
                ("project_path", _unityProjectPath), ("bundle_sha256", bundleHash),
                ("expected_asset_sha256", vaultAssetHash), ("destination_directory", destination));
            _ = VaultAssetImportSpec.From(importVault);
            return Task.FromResult(new AgentPlan("ForgeCore found an exact named, role-compatible, realistic reviewed asset in local storage. Review the non-overwriting target-project import; ForgeCore will still run fresh Unity inspection in this project before placement.", [importVault]));
        }

        var prompt = request.UserMessage.Trim();
        if (prompt.Length is < 20 or > 2_000) return inner.CreatePlanAsync(request, cancellationToken);
        var model = _provider == "seele" && prompt.Contains("Seele02-pro", StringComparison.OrdinalIgnoreCase) ? "Seele02-pro" : Model;
        var generate = ToolCall.Create(ToolKind.Generate3DAsset, PermissionKind.Network,
            $"Generate one original 3D asset through the configured supervised {ProviderName} provider.",
            ("project_path", _unityProjectPath), ("provider", _provider), ("asset_name", assetName),
            ("prompt", prompt), ("model", model));
        _ = Generated3DAssetSpec.From(generate);
        return Task.FromResult(new AgentPlan(
            $"ForgeCore can submit this exact original-asset brief to {ProviderName}. Review the full outbound prompt and possible credit use in the one-action approval window; output remains quarantined and cannot be imported automatically.", [generate]));
    }

    private static string? Recognize(string message)
    {
        if (!message.Contains("generate", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("create", StringComparison.OrdinalIgnoreCase)) return null;
        if (!message.Contains("GLB", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("3D", StringComparison.OrdinalIgnoreCase)) return null;
        var match = AssetNamePattern.Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? RecognizePreparation(string message)
    {
        var match = PrepareAssetPattern.Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    private AgentPlan CreatePreparationPlan(ModelRequest request, string assetName, string? knownHash,
        string? knownAssetPath = null, bool fromVault = false)
    {
        var fileName = assetName + ".glb";
        var importedAssetPath = knownAssetPath ?? $"Assets/ForgeCore/Generated/{fileName}";
        var prepared = request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var operation) && operation == "prepare_generated_asset");
        if (prepared is not null)
            return new AgentPlan(prepared.Success
                ? "The exact generated watchtower was placed under the Frontier Valley watchtower anchor, scaled to 16 metres, marked static, given mesh collision, and saved after a protected scene backup. Its structural evidence and separate human visual and rights decisions remain hash-bound; final in-scene runtime performance certification is still required."
                : $"Unity could not prepare the generated asset: {Truncate(prepared.Detail)}", []);

        var installed = request.Observations.LastOrDefault(x => x.Kind == ToolKind.InstallUnityBridge);
        if (installed is null)
        {
            var install = ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write,
                "Install or safely upgrade the exact hash-verified Unity bridge required for generated-asset inspection and preparation.",
                ("project_path", _unityProjectPath));
            return new AgentPlan("ForgeCore will first install or safely upgrade its deterministic Unity bridge. Only the known hash-verified prior bridge can be replaced, and a protected backup is retained.", [install]);
        }
        if (!installed.Success) return new AgentPlan($"The generated asset was not prepared because the Unity bridge could not be installed or safely upgraded: {Truncate(installed.Detail)}", []);

        var hash = knownHash;
        if (string.IsNullOrEmpty(hash))
        {
            var sidecarPath = $"{_unityProjectPath}/{importedAssetPath}.asset.json";
            var read = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ReadTextFile &&
                x.Arguments.TryGetValue("path", out var path) && string.Equals(path.Replace('\\', '/'), sidecarPath, StringComparison.OrdinalIgnoreCase));
            if (read is null)
            {
                var readSidecar = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read,
                    "Read the local imported-asset provenance before proposing Unity inspection or placement.", ("path", sidecarPath));
                return new AgentPlan("ForgeCore will read the local provenance sidecar to bind inspection and preparation to the exact imported GLB hash.", [readSidecar]);
            }
            if (!read.Success) return new AgentPlan($"The generated asset was not prepared because its provenance could not be read: {Truncate(read.Detail)}", []);
            hash = Sha256Pattern.Match(read.Detail).Value.ToLowerInvariant();
        }
        if (hash is null || hash.Length != 64)
            return new AgentPlan("The generated asset provenance did not contain an exact SHA-256, so ForgeCore will not place it in the scene.", []);

        var compiled = request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var operation) && operation == "compile");
        if (compiled is null)
        {
            var compile = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
                "Compile the exact bridge and synchronously import the new GLB before read-only structural inspection.",
                ("project_path", _unityProjectPath), ("operation", "compile"));
            return new AgentPlan("The bridge and provenance are ready. ForgeCore must now let Unity import the GLB and compile the reviewed bridge before it can protect and inspect the resulting metadata.", [compile]);
        }
        if (!compiled.Success)
            return new AgentPlan($"The generated asset was not inspected because Unity could not import it and compile the bridge: {Truncate(compiled.Detail)}", []);

        var artInspection = request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var operation) && operation == "inspect_game_art_asset" &&
            x.Arguments.TryGetValue("asset_set_id", out var inspectedAssetSet) && string.Equals(inspectedAssetSet, assetName, StringComparison.Ordinal));
        if (artInspection is null)
        {
            var inspect = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
                "Collect hash-bound, read-only Unity structural evidence for the imported watchtower before scene placement.",
                ("project_path", _unityProjectPath), ("operation", "inspect_game_art_asset"),
                ("asset_path", importedAssetPath), ("expected_sha256", hash),
                ("asset_set_id", assetName), ("asset_role", "environment_props"), ("style_family", "realistic_pbr"));
            _ = UnityEditorActionSpec.From(inspect);
            return new AgentPlan("Unity imported the exact GLB. ForgeCore can now inspect its meshes, materials, PBR coverage, textures, rig, animations, LODs, collision, bounds, and missing resources without saving project content. Rights and visual review remain separate.", [inspect]);
        }
        if (!artInspection.Success)
            return new AgentPlan($"The generated asset will not be placed because Unity structural inspection failed: {Truncate(artInspection.Detail)}", []);
        if (!artInspection.Detail.Contains("structural_ready=true", StringComparison.OrdinalIgnoreCase))
            return new AgentPlan("Unity inspection completed, but the exact evidence did not pass the bounded structural-ready gate. ForgeCore will not place the asset in a scene. Rights and visual review also remain incomplete.", []);

        var artPreview = request.Observations.LastOrDefault(x => x.Kind == ToolKind.UnityEditorAction &&
            x.Arguments.TryGetValue("operation", out var operation) && operation == "capture_game_art_preview" &&
            x.Arguments.TryGetValue("asset_set_id", out var previewAssetSet) && string.Equals(previewAssetSet, assetName, StringComparison.Ordinal));
        if (artPreview is null)
        {
            var preview = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
                "Render a local non-overwriting four-view review sheet for the structurally valid imported watchtower.",
                ("project_path", _unityProjectPath), ("operation", "capture_game_art_preview"),
                ("asset_path", importedAssetPath), ("expected_sha256", hash),
                ("asset_set_id", assetName), ("output_path", $"Previews/GameArt/{assetName}-Review1.png"));
            _ = UnityEditorActionSpec.From(preview);
            return new AgentPlan("The asset passed the structural gate. Before scene placement, ForgeCore can render a local four-view review sheet with blank-frame and shader-magenta checks. The image remains evidence for human review; it does not grant rights or approve quality.", [preview]);
        }
        if (!artPreview.Success)
            return new AgentPlan($"The generated asset will not be placed because its local visual review sheet could not be produced safely: {Truncate(artPreview.Detail)}", []);

        var previewIdentity = PreviewIdentityPattern.Match(artPreview.Detail);
        if (!previewIdentity.Success)
            return new AgentPlan("The four-view preview exists, but its result did not expose both the exact PNG and preview-provenance hashes required for a human attestation. ForgeCore will not place or certify the asset.", []);
        var previewHash = previewIdentity.Groups["preview"].Value.ToLowerInvariant();
        var previewProvenanceHash = previewIdentity.Groups["provenance"].Value.ToLowerInvariant();
        var previewPath = $"Previews/GameArt/{assetName}-Review1.png";

        var visualReview = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ReviewGameArtAsset &&
            x.Arguments.TryGetValue("review_kind", out var reviewKind) && reviewKind == GameArtReviewSpec.Visual &&
            x.Arguments.TryGetValue("asset_set_id", out var reviewedAssetSet) && string.Equals(reviewedAssetSet, assetName, StringComparison.Ordinal));
        if (visualReview is null)
        {
            var review = CreateReviewCall(assetName, importedAssetPath, hash, previewPath, previewHash, previewProvenanceHash, GameArtReviewSpec.Visual);
            return new AgentPlan("Structural inspection and automated preview checks passed. ForgeCore now needs your separate visual decision for the exact four-view image. The model cannot answer this attestation, and trusted-session approval cannot bypass it.", [review]);
        }
        if (!visualReview.Success)
            return new AgentPlan($"No visual review was recorded, so ForgeCore will not place the asset: {Truncate(visualReview.Detail)}", []);
        if (visualReview.Detail.Contains("decision=rejected", StringComparison.Ordinal))
            return new AgentPlan("You recorded a rejected visual review for this exact asset and preview. ForgeCore will keep the evidence but will not place or certify the asset.", []);
        if (!visualReview.Detail.Contains("decision=approved", StringComparison.Ordinal))
            return new AgentPlan("The visual review result did not contain an explicit approved decision, so ForgeCore will not place the asset.", []);

        var rightsReview = request.Observations.LastOrDefault(x => x.Kind == ToolKind.ReviewGameArtAsset &&
            x.Arguments.TryGetValue("review_kind", out var reviewKind) && reviewKind == GameArtReviewSpec.Rights &&
            x.Arguments.TryGetValue("asset_set_id", out var reviewedAssetSet) && string.Equals(reviewedAssetSet, assetName, StringComparison.Ordinal));
        if (rightsReview is null)
        {
            var review = CreateReviewCall(assetName, importedAssetPath, hash, previewPath, previewHash, previewProvenanceHash, GameArtReviewSpec.Rights);
            return new AgentPlan("Your visual approval is recorded. ForgeCore separately needs your rights decision after you review the imported provenance and applicable provider/licence terms. This is not legal advice, and the model cannot make the decision.", [review]);
        }
        if (!rightsReview.Success)
            return new AgentPlan($"No rights review was recorded, so ForgeCore will not place the asset: {Truncate(rightsReview.Detail)}", []);
        if (rightsReview.Detail.Contains("decision=rejected", StringComparison.Ordinal))
            return new AgentPlan("You recorded a rejected rights review for this exact asset. ForgeCore will preserve the audit record but will not use the asset.", []);
        if (!rightsReview.Detail.Contains("decision=approved", StringComparison.Ordinal))
            return new AgentPlan("The rights review result did not contain an explicit approved decision, so ForgeCore will not place the asset.", []);

        var stored = request.Observations.LastOrDefault(x => x.Kind == ToolKind.StoreReviewedAsset &&
            x.Arguments.TryGetValue("asset_set_id", out var storedAssetSet) && string.Equals(storedAssetSet, assetName, StringComparison.Ordinal));
        if (stored is null && !fromVault)
        {
            var tags = string.Join(',', new[] { assetName.ToLowerInvariant(), "environment_prop", "realistic" }.Distinct(StringComparer.Ordinal));
            var store = ToolCall.Create(ToolKind.StoreReviewedAsset, PermissionKind.Write,
                "Archive the exact fully reviewed asset in ForgeCore's immutable local asset vault for later games.",
                ("project_path", _unityProjectPath), ("asset_set_id", assetName),
                ("expected_asset_sha256", hash), ("tags", tags));
            _ = ReviewedAssetStoreSpec.From(store);
            return new AgentPlan("The exact asset now has current structural evidence and your separate visual and rights approvals. ForgeCore can preserve that reviewed identity in its offline immutable asset vault so later projects can find it before spending credits on another model.", [store]);
        }

        var prepare = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
            "Prepare the exact imported watchtower under the existing Frontier Valley landmark anchor.",
            ("project_path", _unityProjectPath), ("operation", "prepare_generated_asset"),
            ("scene_path", "Assets/Scenes/FrontierValley.unity"),
            ("asset_path", importedAssetPath), ("expected_sha256", hash),
            ("parent_object_name", "Forest Watchtower"), ("object_name", "Generated Frontier Watchtower"),
            ("target_height", "16"), ("collider_mode", "mesh"), ("replace_placeholder", "true"));
        _ = UnityEditorActionSpec.From(prepare);
        var vaultStatus = fromVault ? "The immutable vault source was verified and this target project completed fresh review. " :
            stored is { Success: true } ? "The reusable vault copy was also verified. " : "Vault archival did not complete, but it did not alter the source asset. ";
        return new AgentPlan(vaultStatus + "The bridge, exact provenance, structural evidence, four-view preview, and your separate visual and rights approvals are bound to the same exact hashes. ForgeCore can back up Frontier Valley, instantiate the generated GLB under the existing watchtower objective, scale it to 16 metres, add static mesh collision, hide the primitive placeholder visuals, report mesh/material counts, and save the scene.", [prepare]);
    }

    private ToolCall CreateReviewCall(string assetName, string assetPath, string assetHash, string previewPath, string previewHash,
        string previewProvenanceHash, string reviewKind)
    {
        var review = ToolCall.Create(ToolKind.ReviewGameArtAsset, PermissionKind.Write,
            reviewKind == GameArtReviewSpec.Visual
                ? "Record your visual accept/reject decision for the exact imported asset and four-view preview."
                : "Record your rights accept/reject decision for the exact imported asset after reviewing provenance and applicable terms.",
            ("project_path", _unityProjectPath), ("review_kind", reviewKind), ("asset_set_id", assetName),
            ("asset_path", assetPath), ("expected_asset_sha256", assetHash),
            ("preview_path", previewPath), ("expected_preview_sha256", previewHash),
            ("expected_preview_provenance_sha256", previewProvenanceHash));
        _ = GameArtReviewSpec.FromProposal(review);
        return review;
    }

    private static bool TryReadVaultCandidate(string detail, string assetName, out string bundleHash, out string assetHash)
    {
        bundleHash = string.Empty; assetHash = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(detail);
            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return false;
            foreach (var result in results.EnumerateArray())
            {
                if (!result.TryGetProperty("assetSetId", out var id) || !string.Equals(id.GetString(), assetName, StringComparison.OrdinalIgnoreCase) ||
                    !result.TryGetProperty("bundleSha256", out var bundle) || !result.TryGetProperty("assetSha256", out var asset)) continue;
                var candidateBundle = bundle.GetString()?.ToLowerInvariant() ?? string.Empty;
                var candidateAsset = asset.GetString()?.ToLowerInvariant() ?? string.Empty;
                if (candidateBundle.Length != 64 || candidateAsset.Length != 64 || candidateBundle.Any(c => !Uri.IsHexDigit(c)) || candidateAsset.Any(c => !Uri.IsHexDigit(c))) continue;
                bundleHash = candidateBundle; assetHash = candidateAsset; return true;
            }
        }
        catch (JsonException) { }
        return false;
    }

    private static string Truncate(string value) => value.Length <= 1_500 ? value : value[..1_500] + "…";

    public async ValueTask DisposeAsync()
    {
        if (inner is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (inner is IDisposable disposable) disposable.Dispose();
    }
}
