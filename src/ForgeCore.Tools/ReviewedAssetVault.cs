using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeCore.Core;
using ForgeCore.Security;

namespace ForgeCore.Tools;

internal sealed class ReviewedAssetVault(WorkspacePathGuard paths, long maximumAssetBytes)
{
    private const int SchemaVersion = 1;
    private const int MaximumEntries = 2_000;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true
    };

    public async Task<string> StoreAsync(ReviewedAssetStoreSpec spec, CancellationToken token)
    {
        var projectRoot = paths.Resolve(spec.ProjectPath);
        if (!Directory.Exists(projectRoot)) throw new DirectoryNotFoundException("The source Unity project does not exist.");
        var evidenceFile = await FindLatestEvidenceAsync(spec, token);
        using var evidenceDocument = evidenceFile.Document;
        var roleId = RequiredString(evidenceFile.Document.RootElement, "assetRole");
        var styleFamily = RequiredString(evidenceFile.Document.RootElement, "styleFamily");
        if (!GameArtSetPlanner.TryParseRole(roleId, out var role)) throw new InvalidDataException("Reviewed asset evidence has an unsupported role.");
        if (!GameArtSetPlanner.AllowedStyleFamilies.Contains(styleFamily, StringComparer.Ordinal))
            throw new InvalidDataException("Reviewed asset evidence has an unsupported style family.");
        if (!RequiredBoolean(evidenceFile.Document.RootElement, "structuralReady"))
            throw new InvalidDataException("Only structurally ready assets can enter the reviewed-asset vault.");

        var catalog = new UnityGameArtEvidenceCatalog(paths, spec.ProjectPath);
        var snapshot = await catalog.ReadAsync(CreateValidationPlan(styleFamily, role), token);
        var current = snapshot.CurrentEvidence.SingleOrDefault(x => x.AssetSetId == spec.AssetSetId && x.Role == role &&
            x.StyleFamily == styleFamily && x.RightsReviewed && x.VisualReviewed && x.PerformanceMeasured && x.ConventionsMeasured)
            ?? throw new InvalidDataException("The exact asset does not have current structural, visual, rights, performance, and convention evidence.");

        var assetPath = RequiredString(evidenceFile.Document.RootElement, "assetPath").Replace('/', Path.DirectorySeparatorChar);
        var expectedProvenanceHash = RequiredHash(evidenceFile.Document.RootElement, "provenanceSha256");
        var expectedMetaHash = RequiredHash(evidenceFile.Document.RootElement, "metaSha256");
        var asset = paths.Resolve(Path.Combine(spec.ProjectPath, assetPath));
        var provenance = paths.Resolve(Path.Combine(spec.ProjectPath, assetPath + ".asset.json"));
        var meta = paths.Resolve(Path.Combine(spec.ProjectPath, assetPath + ".meta"));
        EnsureWithin(projectRoot, asset); EnsureWithin(projectRoot, provenance); EnsureWithin(projectRoot, meta);
        await RequireHashAsync(asset, spec.ExpectedAssetSha256, 1, maximumAssetBytes, token);
        await RequireHashAsync(provenance, expectedProvenanceHash, 2, 1_048_576, token);
        await RequireHashAsync(meta, expectedMetaHash, 1, 1_048_576, token);

        var sourceProvenance = await ReadJsonAsync<ImportedAssetProvenance>(provenance, 1_048_576, token);
        if (sourceProvenance.Sha256 != spec.ExpectedAssetSha256 || sourceProvenance.DestinationPath.Replace('\\', '/') != assetPath.Replace('\\', '/') ||
            string.IsNullOrWhiteSpace(sourceProvenance.License) || sourceProvenance.License.Length > 200 ||
            !Uri.TryCreate(sourceProvenance.SourcePage, UriKind.Absolute, out var sourcePage) || sourcePage.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The reviewed asset provenance does not match the exact source asset or bounded HTTPS rights metadata.");

        var visual = await FindLatestApprovedReviewAsync(spec, GameArtReviewSpec.Visual, expectedProvenanceHash, expectedMetaHash, token);
        var rights = await FindLatestApprovedReviewAsync(spec, GameArtReviewSpec.Rights, expectedProvenanceHash, expectedMetaHash, token);
        if (visual.Record.PreviewPath != rights.Record.PreviewPath || visual.Record.PreviewSha256 != rights.Record.PreviewSha256 ||
            visual.Record.PreviewProvenanceSha256 != rights.Record.PreviewProvenanceSha256)
            throw new InvalidDataException("Visual and rights reviews do not identify the same exact preview.");
        var previewPath = visual.Record.PreviewPath.Replace('/', Path.DirectorySeparatorChar);
        var preview = paths.Resolve(Path.Combine(spec.ProjectPath, previewPath));
        var previewProvenance = paths.Resolve(Path.Combine(spec.ProjectPath, previewPath + ".forgecore-art-preview.json"));
        EnsureWithin(projectRoot, preview); EnsureWithin(projectRoot, previewProvenance);
        await RequireHashAsync(preview, visual.Record.PreviewSha256, 24, 33_554_432, token);
        await RequireHashAsync(previewProvenance, visual.Record.PreviewProvenanceSha256, 2, 1_048_576, token);

        var evidenceSha = await HashFileAsync(evidenceFile.Path, token);
        var visualReviewSha = await HashFileAsync(visual.Path, token);
        var rightsReviewSha = await HashFileAsync(rights.Path, token);
        var identity = new BundleIdentity(SchemaVersion, spec.AssetSetId, spec.ExpectedAssetSha256, expectedProvenanceHash,
            expectedMetaHash, visual.Record.PreviewSha256, visual.Record.PreviewProvenanceSha256, evidenceSha,
            visualReviewSha, rightsReviewSha, roleId, styleFamily, sourceProvenance.License, sourceProvenance.SourcePage,
            spec.Tags.Order(StringComparer.Ordinal).ToArray(), current.VertexCount, current.TriangleCount,
            current.MaterialCount, current.TextureCount, current.EstimatedTextureMemoryBytes,
            RequiredString(evidenceFile.Document.RootElement, "editorVersion"), RequiredString(evidenceFile.Document.RootElement, "bridgeVersion"));
        var bundleHash = HashBytes(JsonSerializer.SerializeToUtf8Bytes(identity, Json));
        var entry = new VaultEntry(SchemaVersion, bundleHash, identity, Path.GetFileName(asset), DateTimeOffset.UtcNow);

        var vaultRoot = paths.Resolve(Path.Combine(".forgecore", "asset-vault"), allowMissingLeaf: true);
        var objects = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects"), allowMissingLeaf: true);
        var stagingRoot = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "staging"), allowMissingLeaf: true);
        Directory.CreateDirectory(vaultRoot); Directory.CreateDirectory(objects); Directory.CreateDirectory(stagingRoot);
        var destination = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects", bundleHash), allowMissingLeaf: true);
        if (Directory.Exists(destination))
        {
            var existing = await ReadEntryAsync(destination, token);
            ValidateEntry(existing, bundleHash);
            return FormatStored(existing, alreadyPresent: true);
        }
        if (File.Exists(destination)) throw new InvalidDataException("The immutable vault destination is occupied by a file.");

        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            await CopyVerifiedAsync(asset, Path.Combine(staging, "asset.glb"), spec.ExpectedAssetSha256, token);
            await CopyVerifiedAsync(provenance, Path.Combine(staging, "asset.glb.asset.json"), expectedProvenanceHash, token);
            await CopyVerifiedAsync(meta, Path.Combine(staging, "source.meta"), expectedMetaHash, token);
            await CopyVerifiedAsync(preview, Path.Combine(staging, "preview.png"), visual.Record.PreviewSha256, token);
            await CopyVerifiedAsync(previewProvenance, Path.Combine(staging, "preview.provenance.json"), visual.Record.PreviewProvenanceSha256, token);
            await CopyVerifiedAsync(evidenceFile.Path, Path.Combine(staging, "unity.evidence.json"), evidenceSha, token);
            await CopyVerifiedAsync(visual.Path, Path.Combine(staging, "visual.review.json"), visualReviewSha, token);
            await CopyVerifiedAsync(rights.Path, Path.Combine(staging, "rights.review.json"), rightsReviewSha, token);
            await File.WriteAllTextAsync(Path.Combine(staging, "entry.json"), JsonSerializer.Serialize(entry, Json), new UTF8Encoding(false), token);
            Directory.Move(staging, destination);
        }
        finally { DeleteOwnedDirectory(staging); }
        return FormatStored(entry, alreadyPresent: false);
    }

    public async Task<string> SearchAsync(AssetVaultSearchSpec spec, CancellationToken token)
    {
        var objects = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects"), allowMissingLeaf: true);
        if (!Directory.Exists(objects)) return "{\n  \"schemaVersion\": 1,\n  \"results\": []\n}";
        var queryTokens = spec.Query.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        var results = new List<(int Score, VaultEntry Entry)>(); var inspected = 0;
        foreach (var directory in Directory.EnumerateDirectories(objects).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (++inspected > MaximumEntries) break;
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || !IsHash(info.Name)) continue;
            try
            {
                var entry = await ReadEntryAsync(directory, token); ValidateEntry(entry, info.Name);
                if (spec.AssetRole is not null && entry.Identity.AssetRole != spec.AssetRole) continue;
                if (spec.StyleFamily is not null && entry.Identity.StyleFamily != spec.StyleFamily) continue;
                var haystack = string.Join(' ', new[] { entry.Identity.AssetSetId, entry.Identity.AssetRole, entry.Identity.StyleFamily }
                    .Concat(entry.Identity.Tags)).ToLowerInvariant();
                if (queryTokens.Any(tokenValue => !haystack.Contains(tokenValue, StringComparison.Ordinal))) continue;
                var score = queryTokens.Count(tokenValue => entry.Identity.AssetSetId.Contains(tokenValue, StringComparison.OrdinalIgnoreCase)) * 4 +
                    queryTokens.Count(tokenValue => entry.Identity.Tags.Contains(tokenValue, StringComparer.Ordinal)) * 2;
                results.Add((score, entry));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException) { }
        }
        var payload = new
        {
            schemaVersion = SchemaVersion,
            inspectedEntries = inspected,
            results = results.OrderByDescending(x => x.Score).ThenBy(x => x.Entry.Identity.AssetSetId, StringComparer.Ordinal)
                .ThenBy(x => x.Entry.BundleSha256, StringComparer.Ordinal).Take(spec.MaxResults).Select(x => new
                {
                    bundleSha256 = x.Entry.BundleSha256,
                    assetSetId = x.Entry.Identity.AssetSetId,
                    assetSha256 = x.Entry.Identity.AssetSha256,
                    assetRole = x.Entry.Identity.AssetRole,
                    styleFamily = x.Entry.Identity.StyleFamily,
                    tags = x.Entry.Identity.Tags,
                    license = x.Entry.Identity.License,
                    sourcePage = x.Entry.Identity.SourcePage,
                    x.Entry.Identity.VertexCount,
                    x.Entry.Identity.TriangleCount,
                    x.Entry.Identity.MaterialCount,
                    x.Entry.Identity.TextureCount,
                    x.Entry.Identity.EstimatedTextureMemoryBytes,
                    x.Entry.CreatedUtc
                })
        };
        return JsonSerializer.Serialize(payload, Json);
    }

    public async Task<string> ImportAsync(VaultAssetImportSpec spec, CancellationToken token)
    {
        var objectDirectory = paths.Resolve(Path.Combine(".forgecore", "asset-vault", "objects", spec.BundleSha256));
        var entry = await ReadEntryAsync(objectDirectory, token); ValidateEntry(entry, spec.BundleSha256);
        if (entry.Identity.AssetSha256 != spec.ExpectedAssetSha256)
            throw new InvalidDataException("The requested asset hash does not match the immutable vault entry.");
        await VerifyBundleFilesAsync(objectDirectory, entry, token);

        var projectRoot = paths.Resolve(spec.ProjectPath);
        var destination = paths.Resolve(Path.Combine(spec.ProjectPath, spec.DestinationDirectory), allowMissingLeaf: true);
        EnsureWithin(projectRoot, destination);
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new InvalidOperationException("Asset-vault import destination already exists; it was not overwritten.");
        var destinationParent = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Asset-vault import destination has no parent.");
        Directory.CreateDirectory(destinationParent);
        var stagingRoot = paths.Resolve(Path.Combine(spec.ProjectPath, ".forgecore", "vault-import-staging"), allowMissingLeaf: true);
        Directory.CreateDirectory(stagingRoot);
        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var safeName = entry.Identity.AssetSetId + ".glb";
        var relativeAssetPath = (spec.DestinationDirectory.TrimEnd('/') + "/" + safeName).Replace('\\', '/');
        try
        {
            await CopyVerifiedAsync(Path.Combine(objectDirectory, "asset.glb"), Path.Combine(staging, safeName), entry.Identity.AssetSha256, token);
            var source = await ReadJsonAsync<ImportedAssetProvenance>(Path.Combine(objectDirectory, "asset.glb.asset.json"), 1_048_576, token);
            var imported = source with { File = safeName, DestinationPath = relativeAssetPath, ImportedUtc = DateTimeOffset.UtcNow };
            await File.WriteAllTextAsync(Path.Combine(staging, safeName + ".asset.json"), JsonSerializer.Serialize(imported, Json), new UTF8Encoding(false), token);
            var importRecord = new VaultImportRecord(SchemaVersion, entry.BundleSha256, entry.Identity.AssetSha256,
                entry.Identity.AssetSetId, relativeAssetPath, true, DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(Path.Combine(staging, "forgecore-vault-import.json"), JsonSerializer.Serialize(importRecord, Json), new UTF8Encoding(false), token);
            Directory.Move(staging, destination);
        }
        finally { DeleteOwnedDirectory(staging); }
        return $"Imported immutable reviewed-asset candidate {entry.Identity.AssetSetId} to {relativeAssetPath} " +
            $"(bundle SHA-256 {entry.BundleSha256}; asset SHA-256 {entry.Identity.AssetSha256}). Fresh target-project Unity inspection is required before placement or reuse.";
    }

    private async Task VerifyBundleFilesAsync(string directory, VaultEntry entry, CancellationToken token)
    {
        await RequireHashAsync(Path.Combine(directory, "asset.glb"), entry.Identity.AssetSha256, 1, maximumAssetBytes, token);
        await RequireHashAsync(Path.Combine(directory, "asset.glb.asset.json"), entry.Identity.ProvenanceSha256, 2, 1_048_576, token);
        await RequireHashAsync(Path.Combine(directory, "source.meta"), entry.Identity.SourceMetaSha256, 1, 1_048_576, token);
        await RequireHashAsync(Path.Combine(directory, "preview.png"), entry.Identity.PreviewSha256, 24, 33_554_432, token);
        await RequireHashAsync(Path.Combine(directory, "preview.provenance.json"), entry.Identity.PreviewProvenanceSha256, 2, 1_048_576, token);
        await RequireHashAsync(Path.Combine(directory, "unity.evidence.json"), entry.Identity.EvidenceSha256, 2, 1_048_576, token);
        await RequireHashAsync(Path.Combine(directory, "visual.review.json"), entry.Identity.VisualReviewSha256, 2, 1_048_576, token);
        await RequireHashAsync(Path.Combine(directory, "rights.review.json"), entry.Identity.RightsReviewSha256, 2, 1_048_576, token);
    }

    private async Task<EvidenceFile> FindLatestEvidenceAsync(ReviewedAssetStoreSpec spec, CancellationToken token)
    {
        var directory = paths.Resolve(Path.Combine(spec.ProjectPath, ".forgecore", "unity", "art-evidence"));
        if (!Directory.Exists(directory)) throw new InvalidDataException("The source project has no Unity game-art evidence.");
        EvidenceFile? latest = null;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Take(501))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var bytes = await ReadBoundedBytesAsync(path, 1_048_576, token); var document = JsonDocument.Parse(bytes);
                var root = document.RootElement;
                if (RequiredInt(root, "schemaVersion") != 2 || RequiredString(root, "assetSetId") != spec.AssetSetId ||
                    RequiredHash(root, "assetSha256") != spec.ExpectedAssetSha256)
                { document.Dispose(); continue; }
                var created = RequiredDate(root, "createdUtc");
                if (latest is null || created > latest.CreatedUtc) { latest?.Document.Dispose(); latest = new(path, document, created); }
                else document.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException) { }
        }
        return latest ?? throw new InvalidDataException("No current schema-v2 structural evidence matches the exact requested asset.");
    }

    private async Task<ReviewFile> FindLatestApprovedReviewAsync(ReviewedAssetStoreSpec spec, string reviewKind,
        string provenanceHash, string metaHash, CancellationToken token)
    {
        var directory = paths.Resolve(Path.Combine(spec.ProjectPath, ".forgecore", "unity", "art-reviews"));
        if (!Directory.Exists(directory)) throw new InvalidDataException("The source project has no human game-art reviews.");
        ReviewFile? latest = null;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Take(501))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var record = await ReadJsonAsync<ReviewRecord>(path, 1_048_576, token);
                if (record.SchemaVersion != 1 || record.StatementVersion != 1 || record.ReviewKind != reviewKind ||
                    record.AssetSetId != spec.AssetSetId || record.AssetSha256 != spec.ExpectedAssetSha256 ||
                    record.ProvenanceSha256 != provenanceHash || record.MetaSha256 != metaHash ||
                    record.AttestationSource != GameArtReviewSpec.UserLiveApproval) continue;
                if (latest is null || record.CreatedUtc > latest.Record.CreatedUtc) latest = new(path, record);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException) { }
        }
        if (latest is null || latest.Record.Decision != "approved")
            throw new InvalidDataException($"The latest exact {reviewKind} review is missing or not approved.");
        return latest;
    }

    private static GameArtSetPlan CreateValidationPlan(string styleFamily, GameArtAssetRole requestedRole)
    {
        var roles = new[] { requestedRole }.Concat(Enum.GetValues<GameArtAssetRole>().Where(x => x != requestedRole)).Take(4).ToArray();
        var slots = roles.Select(role => new GameArtAssetSlot(GameArtSetPlanner.RoleId(role), role, 1, false, false,
            role == GameArtAssetRole.CharacterAnimations ? 1 : 0, false, false, ["vault evidence remains current"])).ToArray();
        return new(GameArtSetPlan.CurrentSchemaVersion, styleFamily, slots, ["vault candidates remain hash verified"]);
    }

    private async Task<VaultEntry> ReadEntryAsync(string directory, CancellationToken token) =>
        await ReadJsonAsync<VaultEntry>(Path.Combine(directory, "entry.json"), 131_072, token);

    private static void ValidateEntry(VaultEntry entry, string expectedBundleHash)
    {
        if (entry.SchemaVersion != SchemaVersion || entry.BundleSha256 != expectedBundleHash || !IsHash(entry.BundleSha256) ||
            entry.Identity.SchemaVersion != SchemaVersion || !IsHash(entry.Identity.AssetSha256) ||
            !IsHash(entry.Identity.ProvenanceSha256) || !IsHash(entry.Identity.SourceMetaSha256) ||
            !IsHash(entry.Identity.PreviewSha256) || !IsHash(entry.Identity.PreviewProvenanceSha256) ||
            !IsHash(entry.Identity.EvidenceSha256) || !IsHash(entry.Identity.VisualReviewSha256) || !IsHash(entry.Identity.RightsReviewSha256) ||
            HashBytes(JsonSerializer.SerializeToUtf8Bytes(entry.Identity, Json)) != expectedBundleHash ||
            !System.Text.RegularExpressions.Regex.IsMatch(entry.Identity.AssetSetId ?? string.Empty, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
            Path.GetFileName(entry.SourceFileName) != entry.SourceFileName || !entry.SourceFileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
            !GameArtSetPlanner.TryParseRole(entry.Identity.AssetRole, out _) ||
            !GameArtSetPlanner.AllowedStyleFamilies.Contains(entry.Identity.StyleFamily, StringComparer.Ordinal) ||
            entry.Identity.Tags.Count is < 1 or > 16 || entry.Identity.Tags.SequenceEqual(entry.Identity.Tags.Order(StringComparer.Ordinal)) == false ||
            entry.Identity.Tags.Any(x => !System.Text.RegularExpressions.Regex.IsMatch(x, "^[a-z0-9][a-z0-9_-]{1,31}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) ||
            string.IsNullOrWhiteSpace(entry.Identity.License) || entry.Identity.License.Length > 200 ||
            !Uri.TryCreate(entry.Identity.SourcePage, UriKind.Absolute, out var sourcePage) || sourcePage.Scheme != Uri.UriSchemeHttps ||
            entry.CreatedUtc == default || entry.Identity.VertexCount < 0 || entry.Identity.TriangleCount < 0 ||
            entry.Identity.MaterialCount < 0 || entry.Identity.TextureCount < 0 || entry.Identity.EstimatedTextureMemoryBytes < 0 ||
            string.IsNullOrWhiteSpace(entry.Identity.EditorVersion) || string.IsNullOrWhiteSpace(entry.Identity.BridgeVersion))
            throw new InvalidDataException("Asset-vault entry is malformed or does not match its content address.");
    }

    private static string FormatStored(VaultEntry entry, bool alreadyPresent) =>
        $"Reviewed asset stored in immutable local vault: asset_set_id={entry.Identity.AssetSetId}; bundle_sha256={entry.BundleSha256}; " +
        $"asset_sha256={entry.Identity.AssetSha256}; role={entry.Identity.AssetRole}; style={entry.Identity.StyleFamily}; already_present={alreadyPresent.ToString().ToLowerInvariant()}.";

    private static async Task<T> ReadJsonAsync<T>(string path, long maximumBytes, CancellationToken token)
    {
        var bytes = await ReadBoundedBytesAsync(path, maximumBytes, token);
        try { return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("JSON document is empty."); }
        catch (JsonException ex) { throw new InvalidDataException("JSON document is malformed or contains unexpected fields.", ex); }
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(string path, long maximumBytes, CancellationToken token)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required reviewed-asset evidence is missing.", path);
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is < 2 || info.Length > maximumBytes)
            throw new InvalidDataException("Reviewed-asset evidence is outside its bounded file contract.");
        return await File.ReadAllBytesAsync(path, token);
    }

    private static async Task CopyVerifiedAsync(string source, string destination, string expectedHash, CancellationToken token)
    {
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await input.CopyToAsync(output, token);
        await RequireHashAsync(destination, expectedHash, 1, long.MaxValue, token);
    }

    private static async Task RequireHashAsync(string path, string expectedHash, long minimumBytes, long maximumBytes, CancellationToken token)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("A hash-bound reviewed-asset file is missing.", path);
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length < minimumBytes || info.Length > maximumBytes)
            throw new InvalidDataException("A hash-bound reviewed-asset file is outside its bounded size contract.");
        var actual = await HashFileAsync(path, token);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedHash)))
            throw new InvalidDataException("A hash-bound reviewed-asset file changed.");
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool IsHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit) && value == value.ToLowerInvariant();
    private static string RequiredHash(JsonElement root, string property)
    {
        var value = RequiredString(root, property);
        if (!IsHash(value)) throw new InvalidDataException($"{property} is not a canonical SHA-256 value.");
        return value;
    }
    private static string RequiredString(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new InvalidDataException($"Missing {property}.");
    private static int RequiredInt(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
        ? result : throw new InvalidDataException($"Missing {property}.");
    private static bool RequiredBoolean(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        ? value.GetBoolean() : throw new InvalidDataException($"Missing {property}.");
    private static DateTimeOffset RequiredDate(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.TryGetDateTimeOffset(out var result) && result != default
        ? result : throw new InvalidDataException($"Missing {property}.");

    private static void EnsureWithin(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathFullyQualified(relative))
            throw new UnauthorizedAccessException("Asset-vault path escaped its approved root.");
    }

    private static void DeleteOwnedDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed record EvidenceFile(string Path, JsonDocument Document, DateTimeOffset CreatedUtc);
    private sealed record ReviewFile(string Path, ReviewRecord Record);
    private sealed record BundleIdentity(int SchemaVersion, string AssetSetId, string AssetSha256, string ProvenanceSha256,
        string SourceMetaSha256, string PreviewSha256, string PreviewProvenanceSha256, string EvidenceSha256,
        string VisualReviewSha256, string RightsReviewSha256, string AssetRole, string StyleFamily, string License,
        string SourcePage, IReadOnlyList<string> Tags, long VertexCount, long TriangleCount, int MaterialCount,
        int TextureCount, long EstimatedTextureMemoryBytes, string EditorVersion, string BridgeVersion);
    private sealed record VaultEntry(int SchemaVersion, string BundleSha256, BundleIdentity Identity, string SourceFileName,
        DateTimeOffset CreatedUtc);
    private sealed record VaultImportRecord(int SchemaVersion, string BundleSha256, string AssetSha256, string AssetSetId,
        string AssetPath, bool FreshUnityInspectionRequired, DateTimeOffset ImportedUtc);
    private sealed record ImportedAssetProvenance(string File, string DestinationPath, string SourcePage, string DownloadUrl,
        string License, string Sha256, long Bytes, string Format, string Inspection, DateTimeOffset StagedUtc, DateTimeOffset ImportedUtc);
    private sealed record ReviewRecord(int SchemaVersion, int StatementVersion, string ReviewKind, string Decision,
        string AttestationSource, string AssetSetId, string AssetPath, string AssetSha256, string ProvenanceSha256,
        string MetaSha256, string PreviewPath, string PreviewSha256, string PreviewProvenanceSha256,
        string ProvenanceLicense, string ProvenanceSourcePage, DateTimeOffset CreatedUtc);
}
