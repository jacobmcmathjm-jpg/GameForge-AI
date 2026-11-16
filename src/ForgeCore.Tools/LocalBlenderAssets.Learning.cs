using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

internal sealed partial class LocalBlenderAssets
{
    private const string ExperienceDirectory = Library + "/experience/v1";
    private const int ExperienceLimit = 256;
    private static readonly SemaphoreSlim ExperienceGate = new(1, 1);

    // The journal is advisory data, never executable instructions or an approval authority.
    // Retaining experience is part of the approved generation operation. Reads never backfill it.
    public async Task<string> RecordSourceExperienceAsync(ToolCall call, ToolResult result, CancellationToken token)
    {
        var spec = BlenderAssetSpec.From(call);
        var recipeHash = Hash(RecipeFor(spec.Recipe));
        var configuration = Hash(Encoding.UTF8.GetBytes($"source|{spec.Recipe}|{recipeHash}|{spec.Seed}|{spec.HeightMillimetres}|{spec.RadiusMillimetres}"));
        var comparison = Hash(Encoding.UTF8.GetBytes($"source|{spec.Recipe}|{recipeHash}|{spec.HeightMillimetres}|{spec.RadiusMillimetres}"));
        string? asset = null, bundleHash = null, evidence = null;
        var measurements = new Dictionary<string, double>(); var reused = false;
        if (result.Success)
        {
            using var receipt = JsonDocument.Parse(result.Output);
            bundleHash = receipt.RootElement.GetProperty("bundleSha256").GetString()!;
            var bundle = await VerifyBundle(bundleHash, token);
            if (bundle.Request.AssetName != spec.AssetName || bundle.Request.Recipe != spec.Recipe || bundle.Request.Seed != spec.Seed ||
                bundle.Request.HeightMillimetres != spec.HeightMillimetres || bundle.Request.RadiusMillimetres != spec.RadiusMillimetres)
                throw new InvalidDataException("Learning source identity differs from this call.");
            asset = bundle.Files.Single(f => f.Name == "asset.glb").Sha256;
            evidence = $"{Library}/objects/{bundleHash}/entry.json";
            reused = receipt.RootElement.GetProperty("reused").GetBoolean();
            measurements = SourceMeasurements(bundle.Metrics);
            // Executable identity matters: runs using different Blender binaries aren't like-for-like.
            comparison = Hash(Encoding.UTF8.GetBytes(comparison + bundle.Request.BlenderExecutableSha256));
        }
        return await StoreExperienceAsync(new(1, AssetImprovementModel.Version, call.Id.ToString("N"),
            AssetImprovementModel.FamilyFor(spec.Recipe), "source", configuration, comparison, asset, bundleHash,
            result.Success, reused, evidence, bundleHash, measurements), token);
    }

    private static Dictionary<string, double> SourceMeasurements(Metrics metrics) => new()
    {
        ["triangles"] = metrics.Triangles, ["vertices"] = metrics.Vertices,
        ["materials"] = metrics.Materials, ["textureResolution"] = metrics.TextureResolution
    };

    public async Task<string> RecordUnityExperienceAsync(UnityEditorActionSpec spec, string evidencePath,
        string eventId, string stage, CancellationToken token)
    {
        var assetPath = paths.Resolve(Path.Combine(spec.ProjectPath, spec.AssetPath!), false);
        if (await HashFile(assetPath, MaxFileBytes, token) != spec.ExpectedSha256)
            throw new InvalidDataException("Learning target asset changed.");
        using var provenance = JsonDocument.Parse(await File.ReadAllBytesAsync(paths.Resolve(assetPath + ".asset.json", false), token));
        var source = provenance.RootElement.GetProperty("sourcePage").GetString()!;
        const string prefix = "forgecore://local-blender/";
        if (!source.StartsWith(prefix, StringComparison.Ordinal)) return "unsupported_source_not_recorded";
        var bundleHash = source[prefix.Length..];
        var bundle = await VerifyBundle(bundleHash, token);
        if (bundle.Files.Single(f => f.Name == "asset.glb").Sha256 != spec.ExpectedSha256)
            throw new InvalidDataException("Learning target no longer matches its source bundle.");
        var file = paths.Resolve(evidencePath, false);
        var bytes = await ReadSourceStateBytes(file, 128 * 1024, token);
        using var evidence = JsonDocument.Parse(bytes); var root = evidence.RootElement;
        if (root.GetProperty("assetSha256").GetString() != spec.ExpectedSha256)
            throw new InvalidDataException("Learning evidence asset mismatch.");
        var report = root.GetProperty(stage == "rig" ? "rig" : "report");
        var measurements = new Dictionary<string, double>();
        foreach (var name in AssetExperience.AllowedMeasurements(stage))
            measurements[name] = stage == "rig" && name == "maximumEdgeStretch"
                ? report.GetProperty("poses").EnumerateArray().Max(p => p.GetProperty(name).GetDouble())
                : report.GetProperty(name).GetDouble();
        var bridge = root.GetProperty("bridgeSha256").GetString()!;
        var config = stage == "motion" ? root.GetProperty("templateKitManifestSha256").GetString()! : bridge;
        var comparison = Hash(Encoding.UTF8.GetBytes($"{stage}|bounded_v1|{spec.ExpectedSha256}|{bridge}"));
        var configuration = Hash(Encoding.UTF8.GetBytes(comparison + "|" + config));
        var relative = Path.GetRelativePath(paths.WorkspaceRoot, file).Replace('\\', '/');
        return await StoreExperienceAsync(new(1, AssetImprovementModel.Version, eventId,
            AssetImprovementModel.FamilyFor(bundle.Request.Recipe), stage, configuration, comparison,
            spec.ExpectedSha256, bundleHash, true, false, relative, Hash(bytes), measurements), token);
    }

    public async Task<string> RecordUnityFailureExperienceAsync(ToolCall call, CancellationToken token)
    {
        var spec = UnityEditorActionSpec.From(call);
        var stage = spec.Operation == "inspect_character_rig" ? "rig" : "motion";
        var sourcePath = paths.Resolve(Path.Combine(spec.ProjectPath, spec.AssetPath! + ".asset.json"), false);
        using var provenance = JsonDocument.Parse(await ReadSourceStateBytes(sourcePath, 16 * 1024, token));
        var source = provenance.RootElement.GetProperty("sourcePage").GetString()!;
        const string prefix = "forgecore://local-blender/";
        if (!source.StartsWith(prefix, StringComparison.Ordinal)) return "unsupported_source_not_recorded";
        var bundleHash = source[prefix.Length..]; var bundle = await VerifyBundle(bundleHash, token);
        if (bundle.Files.Single(f => f.Name == "asset.glb").Sha256 != spec.ExpectedSha256)
            throw new InvalidDataException("Failed attempt does not match an exact owned source.");
        var context = Hash(Encoding.UTF8.GetBytes($"{stage}|bounded_v1|{spec.ExpectedSha256}|{UnityBridgeTemplate.Sha256}"));
        var configuration = Hash(Encoding.UTF8.GetBytes(context + "|failed_execution_configuration_unmeasured|" + spec.Operation));
        return await StoreExperienceAsync(new(1, AssetImprovementModel.Version, call.Id.ToString("N"),
            AssetImprovementModel.FamilyFor(bundle.Request.Recipe), stage, configuration, context, spec.ExpectedSha256,
            bundleHash, false, false, null, null, []), token);
    }

    private async Task<string> StoreExperienceAsync(AssetExperience experience, CancellationToken token)
    {
        experience.Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(experience, Json);
        var hash = Hash(bytes);
        await ExperienceGate.WaitAsync(token);
        try
        {
            var directory = paths.Resolve(ExperienceDirectory, true);
            if (Directory.Exists(directory) && Directory.EnumerateFiles(directory).Take(ExperienceLimit + 1).Count() >= ExperienceLimit)
                return "journal_full_not_recorded";
            Directory.CreateDirectory(directory);
            var destination = paths.Resolve($"{ExperienceDirectory}/{hash}.json", true);
            if (File.Exists(destination))
            {
                if (await HashFile(destination, 16 * 1024, token) != hash) throw new InvalidDataException("Learning journal changed.");
                return "exact_event_already_recorded";
            }
            var temporary = paths.Resolve($"{ExperienceDirectory}/{Guid.NewGuid():N}.pending", true);
            try
            {
                await WriteNew(temporary, bytes, token);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, destination, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return "recorded_advisory_only";
        }
        finally { ExperienceGate.Release(); }
    }

    public async Task<object> ReadLearningPlanAsync(string bundleHash, CancellationToken token)
    {
        var current = await VerifyBundle(bundleHash, token);
        var family = AssetImprovementModel.FamilyFor(current.Request.Recipe);
        var asset = current.Files.Single(f => f.Name == "asset.glb").Sha256;
        var history = new List<AssetExperience>(); var rejected = 0; var scanned = 0; var truncated = false;
        long verificationBytes = 0;
        var directory = paths.Resolve(ExperienceDirectory, true);
        if (Directory.Exists(directory))
        {
            // Bounded discovery; a full journal is not silently called an exhaustive lifetime history.
            foreach (var path in Directory.EnumerateFiles(directory).Take(ExperienceLimit + 1))
            {
                token.ThrowIfCancellationRequested();
                if (scanned == ExperienceLimit) { truncated = true; break; }
                scanned++;
                try
                {
                    var file = paths.Resolve(path, false); var name = Path.GetFileNameWithoutExtension(file);
                    if (!file.EndsWith(".json", StringComparison.Ordinal) || name.Length != 64 ||
                        name.Any(c => !char.IsAsciiHexDigit(c)) || await HashFile(file, 16 * 1024, token) != name)
                        throw new InvalidDataException("Invalid learning record.");
                    var e = await ReadJson<AssetExperience>(file, 16 * 1024, token); e.Validate();
                    if (e.Family != family || e.Stage != "source" && e.AssetSha256 != asset) continue;
                    if (e.Success)
                    {
                        // Scope BEFORE opening any referenced file; a forged journal cannot direct reads at config/secrets.
                        var allowedSource = e.Stage == "source" && e.EvidencePath == $"{Library}/objects/{e.BundleSha256}/entry.json" && e.EvidenceSha256 == e.BundleSha256;
                        var allowedUnity = e.Stage != "source" && e.EvidencePath!.Contains("/.forgecore/unity/character-" +
                            (e.Stage == "rig" ? "rig" : "animation") + "-evidence/", StringComparison.Ordinal) &&
                            Guid.TryParseExact(Path.GetFileNameWithoutExtension(e.EvidencePath), "N", out _) && e.EvidencePath.EndsWith(".json", StringComparison.Ordinal);
                        if (!allowedSource && !allowedUnity) throw new InvalidDataException("Experience evidence is outside the fixed artifact namespaces.");
                        var proof = paths.Resolve(e.EvidencePath!, false);
                        var length = new FileInfo(proof).Length;
                        if (length > 128 * 1024 || verificationBytes + length > 4 * 1024 * 1024)
                        { truncated = true; continue; }
                        verificationBytes += length;
                        if (await HashFile(proof, 128 * 1024, token) != e.EvidenceSha256)
                            throw new InvalidDataException("Experience evidence is missing or stale.");
                        using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(proof, token));
                        if (e.Stage == "source")
                        {
                            var expected = $"{Library}/objects/{e.BundleSha256}/entry.json";
                            if (e.EvidencePath != expected || e.EvidenceSha256 != e.BundleSha256)
                                throw new InvalidDataException("Source learning evidence escaped its bundle.");
                            var metadata = JsonSerializer.Deserialize<Bundle>(doc.RootElement.GetRawText(), Json)!;
                            if (AssetImprovementModel.FamilyFor(metadata.Request.Recipe) != family ||
                                metadata.Files.Single(f => f.Name == "asset.glb").Sha256 != e.AssetSha256 ||
                                !SourceMeasurements(metadata.Metrics).OrderBy(p => p.Key).SequenceEqual(e.Measurements.OrderBy(p => p.Key)))
                                throw new InvalidDataException("Source learning measurements changed.");
                        }
                        else
                        {
                            if (!e.EvidencePath!.Contains("/.forgecore/unity/character-" + (e.Stage == "rig" ? "rig" : "animation") + "-evidence/", StringComparison.Ordinal) ||
                                doc.RootElement.GetProperty("assetSha256").GetString() != asset)
                                throw new InvalidDataException("Motion learning evidence escaped its scope.");
                            var report = doc.RootElement.GetProperty(e.Stage == "rig" ? "rig" : "report");
                            foreach (var pair in e.Measurements)
                            {
                                var value = e.Stage == "rig" && pair.Key == "maximumEdgeStretch"
                                    ? report.GetProperty("poses").EnumerateArray().Max(p => p.GetProperty(pair.Key).GetDouble())
                                    : report.GetProperty(pair.Key).GetDouble();
                                if (value != pair.Value) throw new InvalidDataException("Motion learning measurements changed.");
                            }
                        }
                    }
                    history.Add(e);
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
                { rejected++; }
            }
        }
        var identity = new LocalBlenderReviewIdentity(bundleHash, asset, current.Files.Single(f => f.Name == "model.blend").Sha256,
            current.Files.Single(f => f.Name == "preview.png").Sha256, current.Files.Single(f => f.Name == "metrics.json").Sha256);
        var visual = await LatestReviewAsync(identity, "visual", token); var rights = await LatestReviewAsync(identity, "rights", token);
        var next = visual == "rejected" || rights == "rejected" ? "revise_source_do_not_reuse_as_accepted" :
            visual == "approved" && rights == "approved" ? "source_reviewed_target_integration_still_requires_separate_checks" : "request_exact_source_visual_and_rights_reviews";
        return new
        {
            schemaVersion = 1, policyVersion = AssetImprovementModel.Version, family, bundleSha256 = bundleHash, assetSha256 = asset,
            scannedRecords = scanned, rejectedRecords = rejected, truncated, verificationBytes,
            stages = new[] { "source", "rig", "motion" }.Select(stage => AssetImprovementModel.Advise(stage, history)),
            sourceVisualReview = visual, sourceRightsReview = rights, nextStep = next,
            neuralWeightsTrained = false, autonomousCodeChanges = false, grantsApproval = false,
            qualification = "Local advisory experience, not neural training or guaranteed quality improvement. Recorded failures have no inferred cause. Source costs are not visual scores. Evidence hashes are rechecked, not a fresh Unity/runtime rerun. No actions execute from this report."
        };
    }
}
