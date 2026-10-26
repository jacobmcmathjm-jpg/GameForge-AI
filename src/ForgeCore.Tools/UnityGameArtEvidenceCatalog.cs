using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;
using ForgeCore.Security;

namespace ForgeCore.Tools;

public sealed record UnityGameArtEvidenceSnapshot(
    int TotalRecords,
    int CurrentRecords,
    int StaleRecords,
    int InvalidRecords,
    int TotalReviewRecords,
    int CurrentReviewRecords,
    int StaleReviewRecords,
    int InvalidReviewRecords,
    DateTimeOffset? LatestCreatedUtc,
    IReadOnlyList<GameArtAssetEvidence> CurrentEvidence,
    GameArtSetAssessment Assessment)
{
    public static UnityGameArtEvidenceSnapshot Empty(GameArtSetPlan plan) =>
        new(0, 0, 0, 0, 0, 0, 0, 0, null, [], GameArtSetPlanner.Assess(plan, []));
}

public interface IUnityGameArtEvidenceCatalog
{
    Task<UnityGameArtEvidenceSnapshot> ReadAsync(GameArtSetPlan plan, CancellationToken cancellationToken);
}

public sealed class UnityGameArtEvidenceCatalog(WorkspacePathGuard paths, string projectPath, int maxRecords = 500) : IUnityGameArtEvidenceCatalog
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public async Task<UnityGameArtEvidenceSnapshot> ReadAsync(GameArtSetPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        if (maxRecords is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(maxRecords));
        var projectRoot = paths.Resolve(projectPath);
        if (!Directory.Exists(projectRoot)) return UnityGameArtEvidenceSnapshot.Empty(plan);
        var evidenceDirectory = paths.Resolve(Path.Combine(projectPath, ".forgecore", "unity", "art-evidence"), allowMissingLeaf: true);
        if (!Directory.Exists(evidenceDirectory)) return UnityGameArtEvidenceSnapshot.Empty(plan);

        var files = Directory.EnumerateFiles(evidenceDirectory, "*.json", SearchOption.TopDirectoryOnly).Take(maxRecords + 1).ToArray();
        var overflow = files.Length > maxRecords;
        if (overflow) files = files[..maxRecords];
        var malformed = overflow ? 1 : 0;
        DateTimeOffset? latest = null;
        var latestByAssetSet = new Dictionary<string, ArtEvidence>(StringComparer.Ordinal);
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is < 2 or > 65_536) throw new InvalidDataException();
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                using (var header = JsonDocument.Parse(json))
                {
                    if (header.RootElement.ValueKind != JsonValueKind.Object ||
                        !header.RootElement.TryGetProperty("schemaVersion", out var schemaElement) ||
                        !schemaElement.TryGetInt32(out var schemaVersion)) throw new InvalidDataException();
                    if (schemaVersion == 1)
                    {
                        var legacy = JsonSerializer.Deserialize<LegacyArtEvidence>(json, StrictJson) ?? throw new InvalidDataException();
                        ValidateLegacyShape(legacy);
                        continue;
                    }
                    if (schemaVersion != 2) throw new InvalidDataException();
                }
                var evidence = JsonSerializer.Deserialize<ArtEvidence>(json, StrictJson)
                    ?? throw new InvalidDataException();
                ValidateShape(evidence);
                latest = latest is null || evidence.CreatedUtc > latest ? evidence.CreatedUtc : latest;
                if (!latestByAssetSet.TryGetValue(evidence.AssetSetId, out var prior) || evidence.CreatedUtc > prior.CreatedUtc)
                    latestByAssetSet[evidence.AssetSetId] = evidence;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {
                malformed++;
            }
        }

        var reviewDirectory = paths.Resolve(Path.Combine(projectPath, ".forgecore", "unity", "art-reviews"), allowMissingLeaf: true);
        var reviewFiles = Directory.Exists(reviewDirectory)
            ? Directory.EnumerateFiles(reviewDirectory, "*.json", SearchOption.TopDirectoryOnly).Take(maxRecords + 1).ToArray()
            : [];
        var reviewOverflow = reviewFiles.Length > maxRecords;
        if (reviewOverflow) reviewFiles = reviewFiles[..maxRecords];
        var malformedReviews = reviewOverflow ? 1 : 0;
        var latestReviews = new Dictionary<string, ArtReview>(StringComparer.Ordinal);
        foreach (var file in reviewFiles.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is < 2 or > 65_536) throw new InvalidDataException();
                var review = JsonSerializer.Deserialize<ArtReview>(await File.ReadAllTextAsync(file, cancellationToken), StrictJson)
                    ?? throw new InvalidDataException();
                ValidateReviewShape(review);
                latest = latest is null || review.CreatedUtc > latest ? review.CreatedUtc : latest;
                var key = ReviewKey(review.AssetSetId, review.ReviewKind);
                if (!latestReviews.TryGetValue(key, out var prior) || review.CreatedUtc > prior.CreatedUtc)
                    latestReviews[key] = review;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {
                malformedReviews++;
            }
        }

        var current = new List<GameArtAssetEvidence>();
        var stale = 0;
        var invalid = malformed;
        var currentReviews = 0;
        var staleReviews = 0;
        var consumedReviews = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in latestByAssetSet.Values.OrderBy(x => x.AssetSetId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var asset = paths.Resolve(Path.Combine(projectPath, evidence.AssetPath.Replace('/', Path.DirectorySeparatorChar)), allowMissingLeaf: true);
                var provenance = paths.Resolve(Path.Combine(projectPath, evidence.AssetPath.Replace('/', Path.DirectorySeparatorChar) + ".asset.json"), allowMissingLeaf: true);
                var meta = paths.Resolve(Path.Combine(projectPath, evidence.AssetPath.Replace('/', Path.DirectorySeparatorChar) + ".meta"), allowMissingLeaf: true);
                var hashesCurrent = await HashMatchesAsync(asset, evidence.AssetSha256, 1, 536_870_912, cancellationToken) &&
                    await HashMatchesAsync(provenance, evidence.ProvenanceSha256, 2, 1_048_576, cancellationToken) &&
                    await HashMatchesAsync(meta, evidence.MetaSha256, 1, 1_048_576, cancellationToken);
                if (!hashesCurrent || evidence.EditorVersion.Length > 40 || evidence.BridgeVersion != UnityBridgeTemplate.BridgeVersion)
                {
                    stale++;
                    continue;
                }
                if (!GameArtSetPlanner.TryParseRole(evidence.AssetRole, out var role)) throw new InvalidDataException();
                var visualReviewed = false; var rightsReviewed = false;
                foreach (var reviewKind in new[] { GameArtReviewSpec.Visual, GameArtReviewSpec.Rights })
                {
                    var reviewKey = ReviewKey(evidence.AssetSetId, reviewKind);
                    if (!latestReviews.TryGetValue(reviewKey, out var review)) continue;
                    consumedReviews.Add(reviewKey);
                    if (await ReviewIsCurrentAsync(review, evidence, projectRoot, cancellationToken))
                    {
                        currentReviews++;
                        var approved = review.Decision == "approved";
                        if (reviewKind == GameArtReviewSpec.Visual) visualReviewed = approved;
                        else rightsReviewed = approved;
                    }
                    else staleReviews++;
                }
                current.Add(new(evidence.AssetSetId, role, evidence.StyleFamily, evidence.AssetCount, evidence.HasPbrMaterials,
                    evidence.IsRigged, evidence.AnimationClipCount, evidence.HasLods, evidence.HasCollision,
                    RightsReviewed: rightsReviewed, VisualReviewed: visualReviewed, PerformanceMeasured: true,
                    VertexCount: evidence.VertexCount, TriangleCount: evidence.TriangleCount,
                    MaterialCount: evidence.MaterialCount, TextureCount: evidence.TextureCount,
                    TexturePixelCount: evidence.TexturePixelCount, EstimatedTextureMemoryBytes: evidence.EstimatedTextureMemoryBytes,
                    MaximumTextureWidth: evidence.MaximumTextureWidth, MaximumTextureHeight: evidence.MaximumTextureHeight,
                    ConventionsMeasured: true, HasGroundedPivot: evidence.HasGroundedPivot,
                    HasUniformRootScale: evidence.HasUniformRootScale, HasCanonicalRootRotation: evidence.HasCanonicalRootRotation));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                invalid++;
            }
        }
        staleReviews += latestReviews.Keys.Count(key => !consumedReviews.Contains(key));

        var ordered = current.OrderBy(x => x.AssetSetId, StringComparer.Ordinal).ToArray();
        return new(latestByAssetSet.Count + malformed, ordered.Length, stale, invalid,
            latestReviews.Count + malformedReviews, currentReviews, staleReviews, malformedReviews, latest, ordered,
            GameArtSetPlanner.Assess(plan, ordered));
    }

    private async Task<bool> ReviewIsCurrentAsync(ArtReview review, ArtEvidence evidence, string projectRoot,
        CancellationToken cancellationToken)
    {
        if (review.AssetSetId != evidence.AssetSetId || review.AssetPath.Replace('\\', '/') != evidence.AssetPath.Replace('\\', '/') ||
            review.AssetSha256 != evidence.AssetSha256 || review.ProvenanceSha256 != evidence.ProvenanceSha256 || review.MetaSha256 != evidence.MetaSha256)
            return false;
        var preview = paths.Resolve(Path.Combine(projectPath, review.PreviewPath.Replace('/', Path.DirectorySeparatorChar)), allowMissingLeaf: true);
        var previewProvenancePath = paths.Resolve(Path.Combine(projectPath, review.PreviewPath.Replace('/', Path.DirectorySeparatorChar) + ".forgecore-art-preview.json"), allowMissingLeaf: true);
        if (!IsWithin(projectRoot, preview) || !IsWithin(projectRoot, previewProvenancePath) ||
            !await HashMatchesAsync(preview, review.PreviewSha256, 24, 33_554_432, cancellationToken) ||
            !await HashMatchesAsync(previewProvenancePath, review.PreviewProvenanceSha256, 2, 1_048_576, cancellationToken)) return false;
        try
        {
            var provenance = JsonSerializer.Deserialize<ArtPreviewProvenance>(
                await File.ReadAllTextAsync(previewProvenancePath, cancellationToken), StrictJson) ?? throw new InvalidDataException();
            return provenance.SchemaVersion == 1 && provenance.AssetSetId == review.AssetSetId &&
                provenance.AssetPath.Replace('\\', '/') == review.AssetPath.Replace('\\', '/') &&
                provenance.AssetSha256 == review.AssetSha256 && provenance.ProvenanceSha256 == review.ProvenanceSha256 &&
                provenance.MetaSha256 == review.MetaSha256 && provenance.PreviewPath.Replace('\\', '/') == review.PreviewPath.Replace('\\', '/') &&
                provenance.PreviewSha256 == review.PreviewSha256 && provenance.BridgeVersion == UnityBridgeTemplate.BridgeVersion &&
                !string.IsNullOrWhiteSpace(provenance.EditorVersion) && provenance.EditorVersion.Length <= 40 &&
                provenance.Width == 1280 && provenance.Height == 720 && provenance.ViewCount == 4 &&
                !provenance.RightsReviewed && !provenance.VisualReviewed && provenance.CreatedUtc != default &&
                review.CreatedUtc >= provenance.CreatedUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    private static void ValidateReviewShape(ArtReview review)
    {
        var assetPath = (review.AssetPath ?? string.Empty).Replace('\\', '/');
        var previewPath = (review.PreviewPath ?? string.Empty).Replace('\\', '/');
        if (review.SchemaVersion != 1 || review.StatementVersion != 1 ||
            !GameArtReviewSpec.ReviewKinds.Contains(review.ReviewKind ?? string.Empty) ||
            review.Decision is not "approved" and not "rejected" || review.AttestationSource != GameArtReviewSpec.UserLiveApproval ||
            !Regex.IsMatch(review.AssetSetId ?? string.Empty, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant) ||
            !assetPath.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) || !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
            assetPath.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(review.AssetPath ?? string.Empty) || assetPath.Any(char.IsControl) ||
            !previewPath.StartsWith("Previews/", StringComparison.Ordinal) || !previewPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            previewPath.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(review.PreviewPath ?? string.Empty) || previewPath.Any(char.IsControl) ||
            !IsHash(review.AssetSha256) || !IsHash(review.ProvenanceSha256) || !IsHash(review.MetaSha256) ||
            !IsHash(review.PreviewSha256) || !IsHash(review.PreviewProvenanceSha256) ||
            string.IsNullOrWhiteSpace(review.ProvenanceLicense) || review.ProvenanceLicense.Length > 200 || review.ProvenanceLicense.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(review.ProvenanceSourcePage) || review.ProvenanceSourcePage.Length > 2_000 || review.ProvenanceSourcePage.Any(char.IsControl) ||
            !Uri.TryCreate(review.ProvenanceSourcePage, UriKind.Absolute, out var sourcePage) || sourcePage.Scheme != Uri.UriSchemeHttps ||
            review.CreatedUtc == default)
            throw new InvalidDataException("Game-art review has an invalid shape.");
    }

    private static string ReviewKey(string assetSetId, string reviewKind) => assetSetId + "\0" + reviewKind;
    private static bool IsWithin(string projectRoot, string candidate)
    {
        var relative = Path.GetRelativePath(projectRoot, candidate);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathFullyQualified(relative);
    }

    private static void ValidateShape(ArtEvidence evidence)
    {
        var normalized = (evidence.AssetPath ?? string.Empty).Replace('\\', '/');
        var expectedPbr = evidence.MaterialCount > 0 && evidence.MissingMaterialCount == 0 &&
            evidence.MissingShaderCount == 0 && evidence.PbrMaterialCount == evidence.MaterialCount;
        if (!GameArtSetPlanner.TryParseRole(evidence.AssetRole ?? string.Empty, out var parsedRole)) throw new InvalidDataException("Game-art evidence has an invalid role.");
        var largestScale = Math.Max(evidence.RootScaleX, Math.Max(evidence.RootScaleY, evidence.RootScaleZ));
        var smallestScale = Math.Min(evidence.RootScaleX, Math.Min(evidence.RootScaleY, evidence.RootScaleZ));
        var expectedUniformScale = largestScale - smallestScale <= largestScale * 0.001;
        var expectedCanonicalRotation = evidence.RootRotationDegrees <= 0.5;
        var expectedGroundedPivot = evidence.PivotBottomOffsetNormalized <= 0.1 && evidence.PivotHorizontalOffsetNormalized <= 0.25;
        var requiresGroundedPivot = parsedRole is not GameArtAssetRole.Effects and not GameArtAssetRole.UserInterface and not GameArtAssetRole.CharacterAnimations;
        var expectedStructural = evidence.MaterialCount > 0 && evidence.MissingMaterialCount == 0 && evidence.MissingShaderCount == 0 &&
            expectedUniformScale && expectedCanonicalRotation && (!requiresGroundedPivot || expectedGroundedPivot);
        if (evidence.SchemaVersion != 2 ||
            !Regex.IsMatch(evidence.AssetSetId ?? string.Empty, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(evidence.AssetPath) || !normalized.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(evidence.AssetPath) || evidence.AssetPath.Any(char.IsControl) ||
            !IsHash(evidence.AssetSha256) || !IsHash(evidence.ProvenanceSha256) || !IsHash(evidence.MetaSha256) ||
            !GameArtSetPlanner.AllowedStyleFamilies.Contains(evidence.StyleFamily, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(evidence.EditorVersion) || string.IsNullOrWhiteSpace(evidence.BridgeVersion) ||
            evidence.AssetCount != 1 || evidence.RendererCount is < 1 or > 100_000 || evidence.MeshCount is < 1 or > 100_000 ||
            evidence.SkinnedMeshRendererCount < 0 || evidence.SkinnedMeshRendererCount > evidence.RendererCount ||
            evidence.AnimatorCount is < 0 or > 100_000 || evidence.AvatarCount < 0 || evidence.AvatarCount > evidence.AnimatorCount ||
            evidence.MaterialCount is < 0 or > 100_000 || evidence.PbrMaterialCount < 0 || evidence.PbrMaterialCount > evidence.MaterialCount ||
            evidence.TextureCount is < 0 or > 100_000 || evidence.MissingMaterialCount is < 0 or > 100_000 ||
            evidence.TexturePixelCount is < 0 or > 1_000_000_000_000 || evidence.EstimatedTextureMemoryBytes is < 0 or > 137_438_953_472 ||
            evidence.MaximumTextureWidth is < 0 or > 65_536 || evidence.MaximumTextureHeight is < 0 or > 65_536 ||
            evidence.TextureCount == 0 && (evidence.TexturePixelCount != 0 || evidence.EstimatedTextureMemoryBytes != 0 ||
                evidence.MaximumTextureWidth != 0 || evidence.MaximumTextureHeight != 0) ||
            evidence.TextureCount > 0 && (evidence.TexturePixelCount == 0 || evidence.EstimatedTextureMemoryBytes == 0 ||
                evidence.MaximumTextureWidth == 0 || evidence.MaximumTextureHeight == 0) ||
            evidence.MissingShaderCount < 0 || evidence.MissingShaderCount > evidence.MaterialCount ||
            evidence.VertexCount is < 1 or > 2_000_000_000 || evidence.TriangleCount is < 1 or > 2_000_000_000 ||
            evidence.AnimationClipCount is < 0 or > 10_000 || evidence.LodGroupCount is < 0 or > 100_000 ||
            evidence.LodLevelCount is < 0 or > 1_000_000 || evidence.ColliderCount is < 0 or > 100_000 ||
            !double.IsFinite(evidence.BoundsWidth) || !double.IsFinite(evidence.BoundsHeight) || !double.IsFinite(evidence.BoundsDepth) ||
            evidence.BoundsWidth is <= 0 or > 1_000_000 || evidence.BoundsHeight is <= 0 or > 1_000_000 || evidence.BoundsDepth is <= 0 or > 1_000_000 ||
            !double.IsFinite(evidence.PivotBottomOffsetNormalized) || evidence.PivotBottomOffsetNormalized is < 0 or > 1_000_000 ||
            !double.IsFinite(evidence.PivotHorizontalOffsetNormalized) || evidence.PivotHorizontalOffsetNormalized is < 0 or > 1_000_000 ||
            !double.IsFinite(evidence.RootScaleX) || !double.IsFinite(evidence.RootScaleY) || !double.IsFinite(evidence.RootScaleZ) ||
            evidence.RootScaleX is <= 0 or > 1_000_000 || evidence.RootScaleY is <= 0 or > 1_000_000 || evidence.RootScaleZ is <= 0 or > 1_000_000 ||
            !double.IsFinite(evidence.RootRotationDegrees) || evidence.RootRotationDegrees is < 0 or > 180 ||
            evidence.HasPbrMaterials != expectedPbr || evidence.HasLods != (evidence.LodGroupCount > 0 && evidence.LodLevelCount >= 2) ||
            evidence.HasCollision != (evidence.ColliderCount > 0) || evidence.IsRigged && evidence.SkinnedMeshRendererCount == 0 ||
            evidence.HasGroundedPivot != expectedGroundedPivot || evidence.HasUniformRootScale != expectedUniformScale ||
            evidence.HasCanonicalRootRotation != expectedCanonicalRotation ||
            evidence.StructuralReady != expectedStructural || evidence.RightsReviewed || evidence.VisualReviewed || evidence.CreatedUtc == default)
            throw new InvalidDataException("Game-art evidence has an invalid shape.");
    }

    private static void ValidateLegacyShape(LegacyArtEvidence evidence)
    {
        var normalized = (evidence.AssetPath ?? string.Empty).Replace('\\', '/');
        var expectedPbr = evidence.MaterialCount > 0 && evidence.MissingMaterialCount == 0 &&
            evidence.MissingShaderCount == 0 && evidence.PbrMaterialCount == evidence.MaterialCount;
        var expectedStructural = evidence.MaterialCount > 0 && evidence.MissingMaterialCount == 0 && evidence.MissingShaderCount == 0;
        if (evidence.SchemaVersion != 1 ||
            !Regex.IsMatch(evidence.AssetSetId ?? string.Empty, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(evidence.AssetPath) || !normalized.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(evidence.AssetPath) || evidence.AssetPath.Any(char.IsControl) ||
            !IsHash(evidence.AssetSha256) || !IsHash(evidence.ProvenanceSha256) || !IsHash(evidence.MetaSha256) ||
            !GameArtSetPlanner.TryParseRole(evidence.AssetRole ?? string.Empty, out _) ||
            !GameArtSetPlanner.AllowedStyleFamilies.Contains(evidence.StyleFamily, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(evidence.EditorVersion) || string.IsNullOrWhiteSpace(evidence.BridgeVersion) ||
            evidence.AssetCount != 1 || evidence.RendererCount is < 1 or > 100_000 || evidence.MeshCount is < 1 or > 100_000 ||
            evidence.SkinnedMeshRendererCount < 0 || evidence.SkinnedMeshRendererCount > evidence.RendererCount ||
            evidence.AnimatorCount is < 0 or > 100_000 || evidence.AvatarCount < 0 || evidence.AvatarCount > evidence.AnimatorCount ||
            evidence.MaterialCount is < 0 or > 100_000 || evidence.PbrMaterialCount < 0 || evidence.PbrMaterialCount > evidence.MaterialCount ||
            evidence.TextureCount is < 0 or > 100_000 || evidence.MissingMaterialCount is < 0 or > 100_000 ||
            evidence.MissingShaderCount < 0 || evidence.MissingShaderCount > evidence.MaterialCount ||
            evidence.VertexCount is < 1 or > 2_000_000_000 || evidence.TriangleCount is < 1 or > 2_000_000_000 ||
            evidence.AnimationClipCount is < 0 or > 10_000 || evidence.LodGroupCount is < 0 or > 100_000 ||
            evidence.LodLevelCount is < 0 or > 1_000_000 || evidence.ColliderCount is < 0 or > 100_000 ||
            !double.IsFinite(evidence.BoundsWidth) || !double.IsFinite(evidence.BoundsHeight) || !double.IsFinite(evidence.BoundsDepth) ||
            evidence.BoundsWidth is <= 0 or > 1_000_000 || evidence.BoundsHeight is <= 0 or > 1_000_000 || evidence.BoundsDepth is <= 0 or > 1_000_000 ||
            evidence.HasPbrMaterials != expectedPbr || evidence.HasLods != (evidence.LodGroupCount > 0 && evidence.LodLevelCount >= 2) ||
            evidence.HasCollision != (evidence.ColliderCount > 0) || evidence.IsRigged && evidence.SkinnedMeshRendererCount == 0 ||
            evidence.StructuralReady != expectedStructural || evidence.RightsReviewed || evidence.VisualReviewed || evidence.CreatedUtc == default)
            throw new InvalidDataException("Legacy game-art evidence has an invalid shape.");
    }

    private static bool IsHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit) && value == value.ToLowerInvariant();

    private static async Task<bool> HashMatchesAsync(string path, string expectedHash, long minimumBytes, long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length < minimumBytes || info.Length > maximumBytes) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedHash));
    }

    private sealed record ArtReview(
        int SchemaVersion,
        int StatementVersion,
        string ReviewKind,
        string Decision,
        string AttestationSource,
        string AssetSetId,
        string AssetPath,
        string AssetSha256,
        string ProvenanceSha256,
        string MetaSha256,
        string PreviewPath,
        string PreviewSha256,
        string PreviewProvenanceSha256,
        string ProvenanceLicense,
        string ProvenanceSourcePage,
        DateTimeOffset CreatedUtc);

    private sealed record ArtPreviewProvenance(
        int SchemaVersion,
        string AssetSetId,
        string AssetPath,
        string AssetSha256,
        string ProvenanceSha256,
        string MetaSha256,
        string PreviewPath,
        string PreviewSha256,
        string EditorVersion,
        string BridgeVersion,
        int Width,
        int Height,
        int ViewCount,
        bool RightsReviewed,
        bool VisualReviewed,
        DateTimeOffset CreatedUtc);

    private sealed record ArtEvidence(
        int SchemaVersion,
        string AssetSetId,
        string AssetPath,
        string AssetSha256,
        string ProvenanceSha256,
        string MetaSha256,
        string AssetRole,
        string StyleFamily,
        string EditorVersion,
        string BridgeVersion,
        int AssetCount,
        int RendererCount,
        int MeshCount,
        int SkinnedMeshRendererCount,
        int AnimatorCount,
        int AvatarCount,
        int MaterialCount,
        int PbrMaterialCount,
        int TextureCount,
        long TexturePixelCount,
        long EstimatedTextureMemoryBytes,
        int MaximumTextureWidth,
        int MaximumTextureHeight,
        int MissingMaterialCount,
        int MissingShaderCount,
        long VertexCount,
        long TriangleCount,
        int AnimationClipCount,
        int LodGroupCount,
        int LodLevelCount,
        int ColliderCount,
        double BoundsWidth,
        double BoundsHeight,
        double BoundsDepth,
        double PivotBottomOffsetNormalized,
        double PivotHorizontalOffsetNormalized,
        double RootScaleX,
        double RootScaleY,
        double RootScaleZ,
        double RootRotationDegrees,
        bool HasPbrMaterials,
        bool IsRigged,
        bool HasLods,
        bool HasCollision,
        bool HasGroundedPivot,
        bool HasUniformRootScale,
        bool HasCanonicalRootRotation,
        bool StructuralReady,
        bool RightsReviewed,
        bool VisualReviewed,
        DateTimeOffset CreatedUtc);

    private sealed record LegacyArtEvidence(
        int SchemaVersion,
        string AssetSetId,
        string AssetPath,
        string AssetSha256,
        string ProvenanceSha256,
        string MetaSha256,
        string AssetRole,
        string StyleFamily,
        string EditorVersion,
        string BridgeVersion,
        int AssetCount,
        int RendererCount,
        int MeshCount,
        int SkinnedMeshRendererCount,
        int AnimatorCount,
        int AvatarCount,
        int MaterialCount,
        int PbrMaterialCount,
        int TextureCount,
        int MissingMaterialCount,
        int MissingShaderCount,
        long VertexCount,
        long TriangleCount,
        int AnimationClipCount,
        int LodGroupCount,
        int LodLevelCount,
        int ColliderCount,
        double BoundsWidth,
        double BoundsHeight,
        double BoundsDepth,
        bool HasPbrMaterials,
        bool IsRigged,
        bool HasLods,
        bool HasCollision,
        bool StructuralReady,
        bool RightsReviewed,
        bool VisualReviewed,
        DateTimeOffset CreatedUtc);
}
