using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;
using ForgeCore.Security;

namespace ForgeCore.Tools;

public sealed record UnityGameplayEvidenceSnapshot(
    int TotalRecords,
    int CurrentRecords,
    int StaleRecords,
    int InvalidRecords,
    DateTimeOffset? LatestCreatedUtc)
{
    public static UnityGameplayEvidenceSnapshot Empty { get; } = new(0, 0, 0, 0, null);
}

public interface IUnityGameplayEvidenceCatalog
{
    Task<UnityGameplayEvidenceSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class UnityGameplayEvidenceCatalog(WorkspacePathGuard paths, string projectPath, int maxRecords = 500) : IUnityGameplayEvidenceCatalog
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
    private static readonly Regex SummaryPattern = new(
        @"\ADeterministic runtime gameplay check passed for (first_person_action|platformer|top_down_rpg|racing): movement=([0-9]+\.[0-9]{2})m, respawn=passed, objectives=([0-9]+)/([0-9]+)\.\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<UnityGameplayEvidenceSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        if (maxRecords is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(maxRecords));
        var projectRoot = paths.Resolve(projectPath);
        if (!Directory.Exists(projectRoot)) return UnityGameplayEvidenceSnapshot.Empty;
        var evidenceDirectory = paths.Resolve(Path.Combine(projectPath, ".forgecore", "unity", "gameplay-evidence"), allowMissingLeaf: true);
        if (!Directory.Exists(evidenceDirectory)) return UnityGameplayEvidenceSnapshot.Empty;
        var files = Directory.EnumerateFiles(evidenceDirectory, "*.json", SearchOption.TopDirectoryOnly).Take(maxRecords + 1).ToArray();
        var overflow = files.Length > maxRecords; if (overflow) files = files[..maxRecords];
        var manifest = paths.Resolve(Path.Combine(projectPath, "Assets", "ForgeCoreGameTemplates", "forgecore-game-templates.manifest.json"), allowMissingLeaf: true);
        var manifestHash = File.Exists(manifest) ? await HashFileAsync(manifest, cancellationToken) : null;
        var current = 0; var stale = 0; var malformed = overflow ? 1 : 0; DateTimeOffset? latest = null;
        var latestByScene = new Dictionary<string, GameplayEvidence>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length is < 2 or > 32_768) throw new InvalidDataException();
                var evidence = JsonSerializer.Deserialize<GameplayEvidence>(await File.ReadAllTextAsync(file, cancellationToken), StrictJson)
                    ?? throw new InvalidDataException();
                ValidateShape(evidence);
                latest = latest is null || evidence.CreatedUtc > latest ? evidence.CreatedUtc : latest;
                var sceneKey = evidence.ScenePath.Replace('\\', '/');
                if (!latestByScene.TryGetValue(sceneKey, out var prior) || evidence.CreatedUtc > prior.CreatedUtc)
                    latestByScene[sceneKey] = evidence;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {
                malformed++;
            }
        }
        var invalid = malformed;
        foreach (var evidence in latestByScene.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var scene = paths.Resolve(Path.Combine(projectPath, evidence.ScenePath.Replace('/', Path.DirectorySeparatorChar)), allowMissingLeaf: true);
                var hashesCurrent = File.Exists(scene) && manifestHash is not null &&
                    string.Equals(evidence.SceneSha256, await HashFileAsync(scene, cancellationToken), StringComparison.Ordinal) &&
                    string.Equals(evidence.TemplateKitManifestSha256, manifestHash, StringComparison.Ordinal);
                if (hashesCurrent && evidence.EditorVersion.Length <= 40 &&
                    evidence.BridgeVersion == UnityBridgeTemplate.BridgeVersion && evidence.TemplateKitVersion == UnityGameTemplateKit.Version) current++;
                else stale++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
            {
                invalid++;
            }
        }
        return new(latestByScene.Count + malformed, current, stale, invalid, latest);
    }

    private static void ValidateShape(GameplayEvidence evidence)
    {
        var normalized = (evidence.ScenePath ?? string.Empty).Replace('\\', '/');
        if (evidence.SchemaVersion != 1 || string.IsNullOrWhiteSpace(evidence.ScenePath) || !normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(evidence.ScenePath) || evidence.ScenePath.Any(char.IsControl) || evidence.CreatedUtc == default ||
            string.IsNullOrWhiteSpace(evidence.EditorVersion) || string.IsNullOrWhiteSpace(evidence.BridgeVersion) || string.IsNullOrWhiteSpace(evidence.TemplateKitVersion) ||
            !IsHash(evidence.SceneSha256) || !IsHash(evidence.TemplateKitManifestSha256) ||
            evidence.DurationSeconds is < 2 or > 30 || !UnityGameTemplateSpec.SupportedTemplates.Contains(evidence.TemplateId) ||
            evidence.MovementMetres is < 0.5 or > 1000 || evidence.RequiredObjectives is < 1 or > 100 ||
            evidence.CompletedObjectives != evidence.RequiredObjectives)
            throw new InvalidDataException("Gameplay evidence has an invalid shape.");
        var match = SummaryPattern.Match(evidence.Summary ?? string.Empty);
        if (!match.Success || !string.Equals(match.Groups[1].Value, evidence.TemplateId, StringComparison.Ordinal) ||
            !double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var movement) || Math.Abs(movement - evidence.MovementMetres) > 0.0001 ||
            !int.TryParse(match.Groups[3].Value, out var completed) || !int.TryParse(match.Groups[4].Value, out var required) ||
            completed != evidence.CompletedObjectives || required != evidence.RequiredObjectives)
            throw new InvalidDataException("Gameplay evidence summary does not match its structured fields.");
    }

    private static bool IsHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit) && value == value.ToLowerInvariant();
    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private sealed record GameplayEvidence(
        int SchemaVersion,
        string ScenePath,
        string EditorVersion,
        string BridgeVersion,
        string TemplateKitVersion,
        string SceneSha256,
        string TemplateKitManifestSha256,
        int DurationSeconds,
        string TemplateId,
        double MovementMetres,
        int CompletedObjectives,
        int RequiredObjectives,
        string Summary,
        DateTimeOffset CreatedUtc);
}
