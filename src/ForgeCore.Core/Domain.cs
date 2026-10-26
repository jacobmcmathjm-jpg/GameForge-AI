namespace ForgeCore.Core;

public enum ForgeTaskState { Pending, Planning, AwaitingApproval, Running, Completed, Failed, Cancelled }
public enum PermissionKind { Read, Write, Network, Execute, Administrator }
public enum ToolKind { ListFiles, ReadTextFile, WriteTextFile, ApplyTextPatch, RestoreTextSnapshot, CreateDirectory, ScaffoldProject, StageWebAsset, Generate3DAsset, DotnetBuild, DotnetTest, RunCommand, IndexProject, SearchProject, SearchAssetCatalog, InspectStagedAsset, ImportStagedAsset, InstallUnityBridge, ScaffoldUnityMapDemo, ScaffoldUnityGameTemplate, UnityEditorAction, UnityMcpAction, UnityPlayerSmokeTest, UnityPlayerProfileTest, ReviewGameArtAsset, StoreReviewedAsset, SearchAssetVault, ImportVaultAsset, CreateBlenderAsset, ImportBlenderAsset }
public enum SecurityDecision { Allowed, ApprovalRequired, Denied }
public enum ApprovalDecision { Approved, Rejected, Cancelled }

public sealed record ToolCall(Guid Id, ToolKind Kind, IReadOnlyDictionary<string, string> Arguments,
    PermissionKind RequiredPermission, string Description)
{
    public static ToolCall Create(ToolKind kind, PermissionKind permission, string description,
        params (string Key, string Value)[] arguments) =>
        new(Guid.NewGuid(), kind, arguments.ToDictionary(x => x.Key, x => x.Value), permission, description);
}

public sealed record ConversationTurn(string Role, string Content);
public sealed record ToolObservation(
    Guid ToolCallId,
    ToolKind Kind,
    string Description,
    IReadOnlyDictionary<string, string> Arguments,
    string Status,
    bool Success,
    string Detail);
public sealed record ModelRequest(
    string UserMessage,
    IReadOnlyList<ConversationTurn> History,
    IReadOnlyList<ToolObservation> Observations,
    int PlanningIteration,
    int RemainingActions)
{
    public ModelRequest(string userMessage, IReadOnlyList<ConversationTurn> history)
        : this(userMessage, history, [], 1, int.MaxValue) { }
}
public sealed record AgentPlan(string Response, IReadOnlyList<ToolCall> Actions);
public sealed record AgentRunResult(string Response, IReadOnlyList<ToolResult> ToolResults,
    Guid? WorkQueueId = null, ForgeTaskState State = ForgeTaskState.Completed);
public sealed record ToolResult(Guid ToolCallId, bool Success, string Output, string? Error = null);
public sealed record PolicyResult(SecurityDecision Decision, string Reason);
public sealed record ApprovalRequest(ToolCall Call, string Reason, Guid? TaskId = null);
public enum ApprovalInboxStatus { Pending, Approved, Rejected, Cancelled, Expired, Executing, Executed, Failed }
public sealed record ApprovalInboxItem(
    Guid Id, Guid TaskId, ToolCall Call, string Reason, ApprovalInboxStatus Status,
    DateTimeOffset CreatedUtc, DateTimeOffset ExpiresUtc, DateTimeOffset? ResolvedUtc);
public sealed record ApprovalRecoveryResult(bool Claimed, bool Success, string Message, ToolResult? ToolResult = null);
public enum WorkQueueState { Pending, Running, AwaitingApproval, Completed, Failed, Cancelled, RequiresReview }
public enum WorkQueueStepState { Pending, Executing, AwaitingApproval, Succeeded, Failed, Cancelled, Blocked, RequiresReview }
public sealed record WorkQueueStepDefinition(ToolCall Call, IReadOnlyList<Guid> DependsOn);
public sealed record WorkQueueDefinition(string Name, IReadOnlyList<WorkQueueStepDefinition> Steps)
{
    public const int MaximumSteps = 32;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 120 || Name.Any(char.IsControl))
            throw new ArgumentException("A work queue name must contain between 1 and 120 printable characters.", nameof(Name));
        if (Steps.Count is < 1 or > MaximumSteps)
            throw new ArgumentOutOfRangeException(nameof(Steps), $"A work queue must contain between 1 and {MaximumSteps} steps.");
        var ids = Steps.Select(x => x.Call.Id).ToHashSet();
        if (ids.Count != Steps.Count || ids.Contains(Guid.Empty))
            throw new ArgumentException("Every work queue step requires a unique non-empty tool-call id.", nameof(Steps));
        foreach (var step in Steps)
        {
            if (step.Call.RequiredPermission is PermissionKind.Network or PermissionKind.Administrator ||
                step.Call.Kind is ToolKind.Generate3DAsset or ToolKind.ReviewGameArtAsset or ToolKind.SearchAssetCatalog or ToolKind.StageWebAsset)
                throw new ArgumentException("Persistent work queues are offline-only and cannot contain network, administrator, cloud-generation, catalogue, staging, or human-attestation actions.", nameof(Steps));
            if (string.IsNullOrWhiteSpace(step.Call.Description) || step.Call.Description.Length > 400 || step.Call.Description.Any(char.IsControl))
                throw new ArgumentException("Queued action descriptions must contain between 1 and 400 printable characters.", nameof(Steps));
            if (step.Call.Arguments.Count > 32 || step.Call.Arguments.Any(x => x.Key.Length is < 1 or > 80 || x.Key.Any(char.IsControl) || x.Value.Length > 12_000 || x.Value.Any(char.IsControl)) ||
                step.Call.Arguments.Sum(x => x.Key.Length + x.Value.Length) > 64_000)
                throw new ArgumentException("Queued action arguments exceed the bounded persistence limits.", nameof(Steps));
            if (step.DependsOn.Count > MaximumSteps || step.DependsOn.Distinct().Count() != step.DependsOn.Count ||
                step.DependsOn.Any(x => x == step.Call.Id || !ids.Contains(x)))
                throw new ArgumentException("Every queue dependency must identify a distinct different step in the same queue.", nameof(Steps));
        }

        var dependencies = Steps.ToDictionary(x => x.Call.Id, x => x.DependsOn);
        var visiting = new HashSet<Guid>(); var visited = new HashSet<Guid>();
        foreach (var id in ids) Visit(id);

        void Visit(Guid id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Work queue dependencies must form an acyclic graph.", nameof(Steps));
            foreach (var dependency in dependencies[id]) Visit(dependency);
            visiting.Remove(id); visited.Add(id);
        }
    }
}
public sealed record WorkQueueStep(
    Guid Id, Guid QueueId, int Ordinal, ToolCall Call, IReadOnlyList<Guid> DependsOn,
    WorkQueueStepState State, ToolResult? Result, string? ExecutionOwner, DateTimeOffset UpdatedUtc);
public sealed record WorkQueueSnapshot(
    Guid Id, Guid ConversationId, string Name, WorkQueueState State,
    IReadOnlyList<WorkQueueStep> Steps, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);
public sealed record WorkQueueRunResult(Guid QueueId, WorkQueueState State, int ExecutedSteps, string Message);
public sealed record ModelStatus(string Provider, bool IsAvailable, string Detail);
public sealed record AuditEntry(DateTimeOffset Timestamp, string EventType, string SubjectId, string Detail);
public sealed record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedUtc);
public sealed record StoredMessage(long Id, Guid ConversationId, string Role, string Content, DateTimeOffset CreatedUtc);
public sealed record AuditEvent(long Id, DateTimeOffset Timestamp, string EventType, string SubjectId, string Detail);
public sealed record TaskSummary(Guid Id, Guid ConversationId, string Request, ForgeTaskState State, DateTimeOffset UpdatedUtc);
public sealed record SystemSnapshot(double ProcessCpuPercent, long ProcessWorkingSetBytes, string GpuStatus);
public sealed record ProjectIndexDocument(
    string ProjectPath, string RelativePath, string Extension, string Symbols, string SearchTerms,
    string Sha256, long ByteLength, DateTimeOffset LastWriteUtc);
public sealed record ProjectIndexSummary(
    string ProjectPath, int FileCount, int SkippedCount, long TotalBytes, bool IsTruncated, DateTimeOffset IndexedUtc);

public sealed record ProjectScaffoldSpec(string Path, string Template, string Name, string? UnityVersion)
{
    public static readonly IReadOnlySet<string> SupportedTemplates = new HashSet<string>(StringComparer.Ordinal)
    {
        "dotnet-console", "dotnet-classlib", "unity-basic"
    };

    public static ProjectScaffoldSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ScaffoldProject) throw new ArgumentException("Tool call is not a project scaffold.");
        var allowedKeys = new HashSet<string>(["path", "template", "name", "unity_version"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key))) throw new ArgumentException("Project scaffold contains unexpected arguments.");
        var path = Get("path"); var template = Get("template"); var name = Get("name");
        call.Arguments.TryGetValue("unity_version", out var unityVersion);
        if (!SupportedTemplates.Contains(template)) throw new ArgumentException($"Unsupported project template '{template}'.");
        if (name.Length is < 1 or > 64 || !char.IsLetter(name[0]) || name.Any(c => !char.IsLetterOrDigit(c) && c is not '_' and not '-' and not '.'))
            throw new ArgumentException("Project name must begin with a letter and contain only letters, digits, dot, dash, or underscore (maximum 64 characters).");
        if (template == "unity-basic")
        {
            if (string.IsNullOrWhiteSpace(unityVersion) || !System.Text.RegularExpressions.Regex.IsMatch(unityVersion, @"^\d{4}\.\d+\.\d+[a-z]\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw new ArgumentException("unity-basic requires a Unity editor version such as 6000.0.50f1.");
        }
        else if (unityVersion is not null) throw new ArgumentException("unity_version is only valid for unity-basic.");
        return new(path, template, name, unityVersion);

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record WebAssetSpec(string ProjectPath, string FileName, Uri DownloadUri, Uri SourcePage, string License, string? ExpectedSha256)
{
    public static readonly IReadOnlySet<string> PassiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".wav", ".ogg", ".mp3", ".obj", ".glb"
    };

    public static WebAssetSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.StageWebAsset) throw new ArgumentException("Tool call is not a web asset stage.");
        var allowedKeys = new HashSet<string>(["project_path", "file_name", "url", "source_page", "license", "sha256"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key))) throw new ArgumentException("Web asset action contains unexpected arguments.");
        var projectPath = Get("project_path"); var fileName = Get("file_name");
        var download = ParseHttps(Get("url"), "url"); var sourcePage = ParseHttps(Get("source_page"), "source_page");
        var license = Get("license");
        if (license.Length > 200) throw new ArgumentException("Asset license label exceeds 200 characters.");
        ValidatePassiveFileName(fileName);
        call.Arguments.TryGetValue("sha256", out var sha256);
        if (!string.IsNullOrWhiteSpace(sha256) && (sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c))))
            throw new ArgumentException("sha256 must contain exactly 64 hexadecimal characters when supplied.");
        return new(projectPath, fileName, download, sourcePage, license, string.IsNullOrWhiteSpace(sha256) ? null : sha256.ToLowerInvariant());

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }

    private static Uri ParseHttps(string value, string argument)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || (!uri.IsDefaultPort && uri.Port != 443))
            throw new ArgumentException($"{argument} must be an HTTPS URL without embedded credentials or a nonstandard port.");
        return uri;
    }

    public static void ValidatePassiveFileName(string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Asset file_name must be a plain file name without directories.");
        if (!PassiveExtensions.Contains(Path.GetExtension(fileName)))
            throw new ArgumentException("Only passive image, audio, OBJ, and GLB assets are supported in this release.");
    }
}

public sealed record StagedAssetInspectionSpec(string ProjectPath, string FileName)
{
    public static StagedAssetInspectionSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.InspectStagedAsset) throw new ArgumentException("Tool call is not a staged-asset inspection.");
        RequireOnly(call, "project_path", "file_name");
        var projectPath = Get(call, "project_path"); var fileName = Get(call, "file_name");
        WebAssetSpec.ValidatePassiveFileName(fileName);
        return new(projectPath, fileName);
    }

    internal static void RequireOnly(ToolCall call, params string[] keys)
    {
        if (call.Arguments.Keys.Any(key => !keys.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Staged-asset action contains unexpected arguments.");
    }

    internal static string Get(ToolCall call, string key) =>
        call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
}

public sealed record StagedAssetImportSpec(string ProjectPath, string FileName, string DestinationPath, string ExpectedSha256)
{
    public static StagedAssetImportSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ImportStagedAsset) throw new ArgumentException("Tool call is not a staged-asset import.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "file_name", "destination_path", "expected_sha256");
        var projectPath = StagedAssetInspectionSpec.Get(call, "project_path");
        var fileName = StagedAssetInspectionSpec.Get(call, "file_name");
        var destinationPath = StagedAssetInspectionSpec.Get(call, "destination_path");
        var expectedSha256 = StagedAssetInspectionSpec.Get(call, "expected_sha256");
        WebAssetSpec.ValidatePassiveFileName(fileName);
        if (!string.Equals(Path.GetFileName(destinationPath), fileName, StringComparison.Ordinal))
            throw new ArgumentException("destination_path must end with the staged file_name; renaming during import is not supported.");
        if (expectedSha256.Length != 64 || expectedSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("expected_sha256 must contain exactly 64 hexadecimal characters.");
        return new(projectPath, fileName, destinationPath, expectedSha256.ToLowerInvariant());
    }
}

public sealed record AssetCatalogSearchSpec(string Query, string MediaType, string LicenseFilter, int MaxResults)
{
    public static readonly IReadOnlySet<string> SupportedMediaTypes =
        new HashSet<string>(["image", "audio"], StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> SupportedLicenseFilters =
        new HashSet<string>(["commercial", "cc0"], StringComparer.Ordinal);

    public static AssetCatalogSearchSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.SearchAssetCatalog) throw new ArgumentException("Tool call is not an asset catalogue search.");
        var allowedKeys = new HashSet<string>(["query", "media_type", "license_filter", "max_results"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key)))
            throw new ArgumentException("Asset catalogue search contains unexpected arguments.");
        var query = Get("query"); var mediaType = Get("media_type"); var licenseFilter = Get("license_filter");
        if (query.Length > 200 || query.Any(char.IsControl))
            throw new ArgumentException("Asset catalogue query is limited to 200 printable characters.");
        if (!SupportedMediaTypes.Contains(mediaType))
            throw new ArgumentException("media_type must be image or audio.");
        if (!SupportedLicenseFilters.Contains(licenseFilter))
            throw new ArgumentException("license_filter must be commercial or cc0.");
        var maxText = Get("max_results");
        if (!int.TryParse(maxText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var maxResults) || maxResults is < 1 or > 10)
            throw new ArgumentException("max_results must be an integer between 1 and 10.");
        return new(query.Trim(), mediaType, licenseFilter, maxResults);

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record ReviewedAssetStoreSpec(
    string ProjectPath,
    string AssetSetId,
    string ExpectedAssetSha256,
    IReadOnlyList<string> Tags)
{
    public static ReviewedAssetStoreSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.StoreReviewedAsset) throw new ArgumentException("Tool call is not a reviewed-asset store action.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "asset_set_id", "expected_asset_sha256", "tags");
        var projectPath = StagedAssetInspectionSpec.Get(call, "project_path");
        var assetSetId = StagedAssetInspectionSpec.Get(call, "asset_set_id");
        var expectedHash = NormalizeHash(StagedAssetInspectionSpec.Get(call, "expected_asset_sha256"), "expected_asset_sha256");
        var tagsText = StagedAssetInspectionSpec.Get(call, "tags");
        if (!System.Text.RegularExpressions.Regex.IsMatch(assetSetId, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("asset_set_id must be a bounded identifier.");
        var tags = tagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (tags.Length is < 1 or > 16 || tags.Any(x => !System.Text.RegularExpressions.Regex.IsMatch(x, "^[a-z0-9][a-z0-9_-]{1,31}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
            throw new ArgumentException("tags must contain one to sixteen distinct comma-separated lowercase identifiers (2-32 characters each).");
        return new(projectPath, assetSetId, expectedHash, tags);
    }

    internal static string NormalizeHash(string value, string name)
    {
        if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException($"{name} must contain exactly 64 hexadecimal characters.");
        return value.ToLowerInvariant();
    }
}

public sealed record AssetVaultSearchSpec(
    string Query,
    string? AssetRole,
    string? StyleFamily,
    int MaxResults)
{
    public static AssetVaultSearchSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.SearchAssetVault) throw new ArgumentException("Tool call is not an asset-vault search.");
        StagedAssetInspectionSpec.RequireOnly(call, "query", "asset_role", "style_family", "max_results");
        var query = StagedAssetInspectionSpec.Get(call, "query").Trim();
        if (query.Length is < 2 or > 120 || query.Any(char.IsControl))
            throw new ArgumentException("query must contain 2-120 printable characters.");
        call.Arguments.TryGetValue("asset_role", out var role);
        call.Arguments.TryGetValue("style_family", out var style);
        if (!string.IsNullOrWhiteSpace(role) && !GameArtSetPlanner.TryParseRole(role, out _))
            throw new ArgumentException("asset_role is not a supported game-art role.");
        if (!string.IsNullOrWhiteSpace(style) && !GameArtSetPlanner.AllowedStyleFamilies.Contains(style, StringComparer.Ordinal))
            throw new ArgumentException("style_family is not a supported game-art style family.");
        var maxText = StagedAssetInspectionSpec.Get(call, "max_results");
        if (!int.TryParse(maxText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var maxResults) || maxResults is < 1 or > 20)
            throw new ArgumentException("max_results must be an integer between 1 and 20.");
        return new(query, string.IsNullOrWhiteSpace(role) ? null : role, string.IsNullOrWhiteSpace(style) ? null : style, maxResults);
    }
}

public sealed record VaultAssetImportSpec(
    string ProjectPath,
    string BundleSha256,
    string ExpectedAssetSha256,
    string DestinationDirectory)
{
    public static VaultAssetImportSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ImportVaultAsset) throw new ArgumentException("Tool call is not an asset-vault import.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "bundle_sha256", "expected_asset_sha256", "destination_directory");
        var projectPath = StagedAssetInspectionSpec.Get(call, "project_path");
        var bundleHash = ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "bundle_sha256"), "bundle_sha256");
        var assetHash = ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "expected_asset_sha256"), "expected_asset_sha256");
        var destination = StagedAssetInspectionSpec.Get(call, "destination_directory").Replace('\\', '/').TrimEnd('/');
        if (!destination.StartsWith("Assets/ForgeCore/Generated/Vault/", StringComparison.Ordinal) ||
            destination.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(destination) ||
            destination.Length > 220 || destination.Any(char.IsControl) || destination.Split('/').Any(x => x.Length is < 1 or > 64))
            throw new ArgumentException("destination_directory must be a bounded new directory under Assets/ForgeCore/Generated/Vault.");
        return new(projectPath, bundleHash, assetHash, destination);
    }
}

public sealed record Generated3DAssetSpec(string ProjectPath, string Provider, string AssetName, string Prompt, string Model)
{
    public static readonly IReadOnlySet<string> SupportedProviders = new HashSet<string>(StringComparer.Ordinal)
    {
        "seele", "meshy"
    };
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SupportedModels =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["seele"] = new HashSet<string>(["Seele02-flash", "Seele02-pro"], StringComparer.Ordinal),
            ["meshy"] = new HashSet<string>(["meshy-6"], StringComparer.Ordinal)
        };

    public static Generated3DAssetSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.Generate3DAsset) throw new ArgumentException("Tool call is not a 3D asset generation request.");
        var allowedKeys = new HashSet<string>(["project_path", "provider", "asset_name", "prompt", "model"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key)))
            throw new ArgumentException("3D asset generation contains unexpected arguments.");
        var projectPath = Get("project_path"); var provider = Get("provider"); var assetName = Get("asset_name");
        var prompt = Get("prompt").Trim(); var model = Get("model");
        if (!SupportedProviders.Contains(provider)) throw new ArgumentException("Unsupported 3D generation provider.");
        if (assetName.Length is < 2 or > 48 || !char.IsLetter(assetName[0]) || assetName.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("asset_name must begin with a letter and contain only letters, digits, dash, or underscore (2-48 characters).");
        var maxPromptLength = provider == "meshy" ? 300 : 2_000;
        if (prompt.Length < 20 || prompt.Length > maxPromptLength || prompt.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException($"3D generation prompt must contain 20-{maxPromptLength} safe text characters for {provider}.");
        if (!SupportedModels[provider].Contains(model)) throw new ArgumentException($"Unsupported {provider} generation model.");
        return new(projectPath, provider, assetName, prompt, model);

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record TextPatchSpec(string Path, string OldText, string NewText, string? ExpectedSha256)
{
    public static TextPatchSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ApplyTextPatch) throw new ArgumentException("Tool call is not a text patch.");
        var allowedKeys = new HashSet<string>(["path", "old_text", "new_text", "expected_sha256"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key))) throw new ArgumentException("Text patch contains unexpected arguments.");
        var path = Get("path"); var oldText = Get("old_text");
        if (!call.Arguments.TryGetValue("new_text", out var newText)) throw new ArgumentException("Missing 'new_text' argument.");
        if (oldText.Length > 100_000 || newText.Length > 100_000) throw new ArgumentException("Patch text is limited to 100000 characters per side.");
        call.Arguments.TryGetValue("expected_sha256", out var expectedSha256);
        if (!string.IsNullOrWhiteSpace(expectedSha256) && (expectedSha256.Length != 64 || expectedSha256.Any(c => !Uri.IsHexDigit(c))))
            throw new ArgumentException("expected_sha256 must contain exactly 64 hexadecimal characters when supplied.");
        return new(path, oldText, newText, string.IsNullOrWhiteSpace(expectedSha256) ? null : expectedSha256.ToLowerInvariant());

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record TextSnapshotRestoreSpec(string Path, string SnapshotPath, string ExpectedCurrentSha256)
{
    public static TextSnapshotRestoreSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.RestoreTextSnapshot) throw new ArgumentException("Tool call is not a text snapshot restore.");
        var allowedKeys = new HashSet<string>(["path", "snapshot_path", "expected_current_sha256"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key))) throw new ArgumentException("Snapshot restore contains unexpected arguments.");
        var path = Get("path"); var snapshotPath = Get("snapshot_path"); var hash = Get("expected_current_sha256");
        if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("expected_current_sha256 must contain exactly 64 hexadecimal characters.");
        return new(path, snapshotPath, hash.ToLowerInvariant());

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record DotnetCommandSpec(string ProjectPath, string Configuration)
{
    public static DotnetCommandSpec From(ToolCall call)
    {
        if (call.Kind is not ToolKind.DotnetBuild and not ToolKind.DotnetTest) throw new ArgumentException("Tool call is not a dedicated .NET action.");
        var allowedKeys = new HashSet<string>(["project_path", "configuration"], StringComparer.Ordinal);
        if (call.Arguments.Keys.Any(key => !allowedKeys.Contains(key))) throw new ArgumentException(".NET action contains unexpected arguments.");
        var projectPath = Get("project_path"); var configuration = Get("configuration");
        if (configuration is not "Debug" and not "Release") throw new ArgumentException(".NET configuration must be Debug or Release.");
        var extension = Path.GetExtension(projectPath);
        if (!extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(".NET project_path must identify a .csproj, .sln, or .slnx file.");
        return new(projectPath, configuration);

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }
}

public sealed record ProjectIndexSpec(string Path)
{
    public static ProjectIndexSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.IndexProject) throw new ArgumentException("Tool call is not a project index action.");
        RequireOnly(call, "path");
        return new(GetRequired(call, "path"));
    }

    internal static void RequireOnly(ToolCall call, params string[] keys)
    {
        if (call.Arguments.Keys.Any(key => !keys.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Project retrieval action contains unexpected arguments.");
    }

    internal static string GetRequired(ToolCall call, string key) =>
        call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
}

public sealed record ProjectSearchSpec(string Path, string Query, int MaxResults)
{
    public static ProjectSearchSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.SearchProject) throw new ArgumentException("Tool call is not a project search action.");
        ProjectIndexSpec.RequireOnly(call, "path", "query", "max_results");
        var path = ProjectIndexSpec.GetRequired(call, "path");
        var query = ProjectIndexSpec.GetRequired(call, "query");
        if (query.Length > 200) throw new ArgumentException("Project search query is limited to 200 characters.");
        var text = ProjectIndexSpec.GetRequired(call, "max_results");
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var maxResults) || maxResults is < 1 or > 20)
            throw new ArgumentException("max_results must be an integer between 1 and 20.");
        return new(path, query, maxResults);
    }
}

public sealed record UnityBridgeInstallSpec(string ProjectPath)
{
    public static UnityBridgeInstallSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.InstallUnityBridge) throw new ArgumentException("Tool call is not a Unity bridge installation.");
        RequireOnly(call, "project_path");
        return new(Get(call, "project_path"));
    }

    internal static void RequireOnly(ToolCall call, params string[] keys)
    {
        if (call.Arguments.Keys.Any(key => !keys.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Unity action contains unexpected arguments.");
    }

    internal static string Get(ToolCall call, string key) =>
        call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
}

public sealed record UnityMapDemoSpec(string ProjectPath, string MapTemplate)
{
    public const string FrontierValley = "frontier_valley";

    public static UnityMapDemoSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ScaffoldUnityMapDemo) throw new ArgumentException("Tool call is not a Unity map-demo scaffold.");
        UnityBridgeInstallSpec.RequireOnly(call, "project_path", "map_template");
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var mapTemplate = UnityBridgeInstallSpec.Get(call, "map_template");
        if (mapTemplate != FrontierValley) throw new ArgumentException("map_template must be frontier_valley.");
        return new(projectPath, mapTemplate);
    }
}

public sealed record UnityGameTemplateSpec(string ProjectPath, string TemplateId)
{
    public static readonly IReadOnlySet<string> SupportedTemplates = new HashSet<string>(
        ["first_person_action", "platformer", "top_down_rpg", "racing"], StringComparer.Ordinal);

    public static UnityGameTemplateSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ScaffoldUnityGameTemplate) throw new ArgumentException("Tool call is not a Unity game-template scaffold.");
        UnityBridgeInstallSpec.RequireOnly(call, "project_path", "template_id");
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var templateId = UnityBridgeInstallSpec.Get(call, "template_id");
        if (!SupportedTemplates.Contains(templateId))
            throw new ArgumentException("template_id must be first_person_action, platformer, top_down_rpg, or racing.");
        return new(projectPath, templateId);
    }
}

public sealed record UnityPlayerSmokeTestSpec(string ProjectPath, string OutputPath, int DurationSeconds)
{
    public static UnityPlayerSmokeTestSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.UnityPlayerSmokeTest) throw new ArgumentException("Tool call is not a Unity player smoke test.");
        UnityBridgeInstallSpec.RequireOnly(call, "project_path", "output_path", "duration_seconds");
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var outputPath = UnityBridgeInstallSpec.Get(call, "output_path");
        var normalized = outputPath.Replace('\\', '/');
        if (!normalized.StartsWith("Builds/", StringComparison.Ordinal) || !normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(outputPath) || outputPath.Any(char.IsControl))
            throw new ArgumentException("output_path must be a traversal-free Builds/... .exe path.");
        var durationText = UnityBridgeInstallSpec.Get(call, "duration_seconds");
        if (!int.TryParse(durationText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var duration) || duration is < 5 or > 30)
            throw new ArgumentException("duration_seconds must be an integer between 5 and 30.");
        return new(projectPath, outputPath, duration);
    }
}

public sealed record UnityPlayerProfileTestSpec(string ProjectPath, string OutputPath, int DurationSeconds)
{
    public const int Width = 1280;
    public const int Height = 720;

    public static UnityPlayerProfileTestSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.UnityPlayerProfileTest) throw new ArgumentException("Tool call is not a Unity player profile test.");
        UnityBridgeInstallSpec.RequireOnly(call, "project_path", "output_path", "duration_seconds");
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var outputPath = UnityBridgeInstallSpec.Get(call, "output_path");
        var normalized = outputPath.Replace('\\', '/');
        if (!normalized.StartsWith("Builds/", StringComparison.Ordinal) || !normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(outputPath) || outputPath.Any(char.IsControl))
            throw new ArgumentException("output_path must be a traversal-free Builds/... .exe path.");
        var durationText = UnityBridgeInstallSpec.Get(call, "duration_seconds");
        if (!int.TryParse(durationText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var duration) || duration is < 5 or > 30)
            throw new ArgumentException("duration_seconds must be an integer between 5 and 30.");
        return new(projectPath, outputPath, duration);
    }
}

public sealed record UnityMcpActionSpec(string ProjectPath, string Operation, string? ToolName, string? ArgumentsJson)
{
    public static readonly IReadOnlySet<string> Operations = new HashSet<string>(["discover", "call"], StringComparer.Ordinal);

    public static UnityMcpActionSpec From(ToolCall call, int maxArgumentsCharacters = 16_000)
    {
        if (call.Kind != ToolKind.UnityMcpAction) throw new ArgumentException("Tool call is not a Unity MCP action.");
        if (maxArgumentsCharacters is < 2 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(maxArgumentsCharacters));
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var operation = UnityBridgeInstallSpec.Get(call, "operation");
        if (!Operations.Contains(operation)) throw new ArgumentException("Unity MCP operation must be discover or call.");
        UnityBridgeInstallSpec.RequireOnly(call, operation == "discover"
            ? ["project_path", "operation"]
            : ["project_path", "operation", "tool_name", "arguments_json"]);
        if (operation == "discover") return new(projectPath, operation, null, null);

        var toolName = UnityBridgeInstallSpec.Get(call, "tool_name");
        var argumentsJson = UnityBridgeInstallSpec.Get(call, "arguments_json");
        if (toolName.Length > 128 || toolName.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
            throw new ArgumentException("Unity MCP tool_name must be a printable identifier without whitespace (maximum 128 characters).");
        if (argumentsJson.Length > maxArgumentsCharacters)
            throw new ArgumentException($"Unity MCP arguments_json exceeds the {maxArgumentsCharacters}-character limit.");
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(argumentsJson, new System.Text.Json.JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new ArgumentException("Unity MCP arguments_json must contain one JSON object.");
        }
        catch (System.Text.Json.JsonException ex) { throw new ArgumentException("Unity MCP arguments_json is not valid bounded JSON.", ex); }
        return new(projectPath, operation, toolName, argumentsJson);
    }
}

public sealed record UnityEditorActionSpec(
    string ProjectPath, string Operation, string? ScenePath, string? SceneTemplate, string? ObjectName,
    string? Primitive, double? PositionX, double? PositionY, double? PositionZ, string? ComponentType,
    string? TestMode, int? DurationSeconds, string? OutputPath, bool? DevelopmentBuild,
    string? MapTemplate, string? TemplateId, int? Seed, string? AssetPath, string? ExpectedSha256, string? ParentObjectName,
    double? TargetHeight, string? ColliderMode, bool? ReplacePlaceholder, int? LevelCount,
    string? AssetSetId, string? AssetRole, string? StyleFamily)
{
    public static readonly IReadOnlySet<string> Operations = new HashSet<string>(
        ["capabilities", "compile", "create_scene", "create_map_demo", "create_game_template", "create_game_campaign", "create_primitive", "add_component", "prepare_generated_asset", "inspect_game_art_asset", "capture_game_art_preview", "run_tests", "play_mode", "verify_gameplay", "profile_scene", "capture_scene_preview", "build_windows"],
        StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> SceneTemplates = new HashSet<string>(["empty", "basic_2d", "basic_3d"], StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> Primitives = new HashSet<string>(["empty", "cube", "sphere", "capsule", "cylinder", "plane", "quad"], StringComparer.Ordinal);

    public static UnityEditorActionSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.UnityEditorAction) throw new ArgumentException("Tool call is not a Unity Editor action.");
        var projectPath = UnityBridgeInstallSpec.Get(call, "project_path");
        var operation = UnityBridgeInstallSpec.Get(call, "operation");
        if (!Operations.Contains(operation)) throw new ArgumentException("Unsupported Unity operation.");
        var allowed = operation switch
        {
            "capabilities" or "compile" => new[] { "project_path", "operation" },
            "create_scene" => new[] { "project_path", "operation", "scene_path", "scene_template" },
            "create_map_demo" => new[] { "project_path", "operation", "scene_path", "map_template", "seed" },
            "create_game_template" => new[] { "project_path", "operation", "scene_path", "template_id", "seed" },
            "create_game_campaign" => new[] { "project_path", "operation", "scene_path", "template_id", "seed", "level_count" },
            "create_primitive" => new[] { "project_path", "operation", "scene_path", "object_name", "primitive", "position_x", "position_y", "position_z" },
            "add_component" => new[] { "project_path", "operation", "scene_path", "object_name", "component_type" },
            "prepare_generated_asset" => new[] { "project_path", "operation", "scene_path", "asset_path", "expected_sha256", "parent_object_name", "object_name", "target_height", "collider_mode", "replace_placeholder" },
            "inspect_game_art_asset" => new[] { "project_path", "operation", "asset_path", "expected_sha256", "asset_set_id", "asset_role", "style_family" },
            "capture_game_art_preview" => new[] { "project_path", "operation", "asset_path", "expected_sha256", "asset_set_id", "output_path" },
            "run_tests" => new[] { "project_path", "operation", "test_mode" },
            "play_mode" => new[] { "project_path", "operation", "scene_path", "duration_seconds" },
            "verify_gameplay" => new[] { "project_path", "operation", "scene_path", "duration_seconds" },
            "profile_scene" => new[] { "project_path", "operation", "scene_path", "duration_seconds" },
            "capture_scene_preview" => new[] { "project_path", "operation", "scene_path", "output_path", "duration_seconds" },
            "build_windows" => new[] { "project_path", "operation", "output_path", "development_build", "scene_path", "level_count" },
            _ => throw new ArgumentException("Unsupported Unity operation.")
        };
        UnityBridgeInstallSpec.RequireOnly(call, allowed);

        var scenePath = GetOptional(call, "scene_path"); var sceneTemplate = GetOptional(call, "scene_template");
        var objectName = GetOptional(call, "object_name"); var primitive = GetOptional(call, "primitive");
        var componentType = GetOptional(call, "component_type"); var testMode = GetOptional(call, "test_mode");
        var outputPath = GetOptional(call, "output_path"); var mapTemplate = GetOptional(call, "map_template");
        var templateId = GetOptional(call, "template_id");
        var assetPath = GetOptional(call, "asset_path"); var expectedSha256 = GetOptional(call, "expected_sha256");
        var parentObjectName = GetOptional(call, "parent_object_name"); var colliderMode = GetOptional(call, "collider_mode");
        var assetSetId = GetOptional(call, "asset_set_id"); var assetRole = GetOptional(call, "asset_role");
        var styleFamily = GetOptional(call, "style_family");
        if (scenePath is not null) ValidateScenePath(scenePath);
        if (sceneTemplate is not null && !SceneTemplates.Contains(sceneTemplate)) throw new ArgumentException("scene_template must be empty, basic_2d, or basic_3d.");
        if (objectName is not null) ValidateObjectName(objectName);
        if (primitive is not null && !Primitives.Contains(primitive)) throw new ArgumentException("primitive is not supported.");
        if (componentType is not null && (componentType.Length > 160 || !System.Text.RegularExpressions.Regex.IsMatch(componentType, @"^[A-Za-z_][A-Za-z0-9_.]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
            throw new ArgumentException("component_type must be a bounded C# type name.");
        if (testMode is not null && testMode is not "edit" and not "play") throw new ArgumentException("test_mode must be edit or play.");
        if (outputPath is not null)
        {
            if (operation is "capture_scene_preview" or "capture_game_art_preview") ValidatePreviewPath(outputPath);
            else ValidateBuildPath(outputPath);
        }
        if (mapTemplate is not null && mapTemplate != UnityMapDemoSpec.FrontierValley) throw new ArgumentException("map_template must be frontier_valley.");
        if (templateId is not null && !UnityGameTemplateSpec.SupportedTemplates.Contains(templateId))
            throw new ArgumentException("template_id is not a supported Unity game template.");
        if (assetPath is not null) ValidateGeneratedAssetPath(assetPath);
        if (expectedSha256 is not null && (expectedSha256.Length != 64 || expectedSha256.Any(c => !Uri.IsHexDigit(c))))
            throw new ArgumentException("expected_sha256 must contain exactly 64 hexadecimal characters.");
        if (parentObjectName is not null) ValidateObjectName(parentObjectName);
        if (colliderMode is not null && colliderMode is not "mesh" and not "box") throw new ArgumentException("collider_mode must be mesh or box.");
        if (assetSetId is not null && !System.Text.RegularExpressions.Regex.IsMatch(assetSetId, @"^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("asset_set_id must be a bounded identifier.");
        if (assetRole is not null && !GameArtSetPlanner.TryParseRole(assetRole, out _))
            throw new ArgumentException("asset_role is not a supported game-art role.");
        if (styleFamily is not null && !GameArtSetPlanner.AllowedStyleFamilies.Contains(styleFamily, StringComparer.Ordinal))
            throw new ArgumentException("style_family is not a supported game-art style family.");
        var seed = ParseSeed(call); var levelCount = ParseLevelCount(call);
        if (operation == "create_game_campaign" && (scenePath is null || templateId is null || seed is null || levelCount is null))
            throw new ArgumentException("create_game_campaign requires scene_path, template_id, seed, and level_count.");
        if (operation == "build_windows" && levelCount is not null && scenePath is null)
            throw new ArgumentException("A campaign build requires scene_path when level_count is supplied.");
        if (operation == "capture_scene_preview" && (scenePath is null || outputPath is null || !call.Arguments.ContainsKey("duration_seconds")))
            throw new ArgumentException("capture_scene_preview requires scene_path, output_path, and duration_seconds.");
        if (operation == "verify_gameplay" && (scenePath is null || !call.Arguments.ContainsKey("duration_seconds")))
            throw new ArgumentException("verify_gameplay requires scene_path and duration_seconds.");
        if (operation == "profile_scene" && (scenePath is null || !call.Arguments.ContainsKey("duration_seconds")))
            throw new ArgumentException("profile_scene requires scene_path and duration_seconds.");
        if (operation == "inspect_game_art_asset" && (assetPath is null || expectedSha256 is null || assetSetId is null || assetRole is null || styleFamily is null))
            throw new ArgumentException("inspect_game_art_asset requires asset_path, expected_sha256, asset_set_id, asset_role, and style_family.");
        if (operation == "capture_game_art_preview" && (assetPath is null || expectedSha256 is null || assetSetId is null || outputPath is null))
            throw new ArgumentException("capture_game_art_preview requires asset_path, expected_sha256, asset_set_id, and output_path.");

        var spec = new UnityEditorActionSpec(projectPath, operation, scenePath, sceneTemplate, objectName, primitive,
            ParseCoordinate(call, "position_x"), ParseCoordinate(call, "position_y"), ParseCoordinate(call, "position_z"),
            componentType, testMode, ParseDuration(call), outputPath, ParseDevelopment(call), mapTemplate, templateId, seed,
            assetPath, expectedSha256?.ToLowerInvariant(), parentObjectName, ParseTargetHeight(call), colliderMode, ParseBoolean(call, "replace_placeholder"), levelCount,
            assetSetId, assetRole, styleFamily);
        if (operation == "verify_gameplay" && spec.DurationSeconds is < 2)
            throw new ArgumentException("verify_gameplay duration_seconds must be between 2 and 30.");
        if (operation == "profile_scene" && spec.DurationSeconds is < 5)
            throw new ArgumentException("profile_scene duration_seconds must be between 5 and 30.");
        if (operation == "create_game_campaign" || operation == "build_windows" && levelCount is not null) _ = spec.GetCampaignScenePaths();
        return spec;
    }

    private static string? GetOptional(ToolCall call, string key) => call.Arguments.TryGetValue(key, out var value) ? value : null;
    private static double? ParseCoordinate(ToolCall call, string key)
    {
        if (!call.Arguments.TryGetValue(key, out var text)) return null;
        if (!double.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value is < -100_000 or > 100_000)
            throw new ArgumentException($"{key} must be a finite decimal between -100000 and 100000.");
        return value;
    }
    private static int? ParseDuration(ToolCall call)
    {
        if (!call.Arguments.TryGetValue("duration_seconds", out var text)) return null;
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value is < 1 or > 30)
            throw new ArgumentException("duration_seconds must be an integer between 1 and 30.");
        return value;
    }
    private static double? ParseTargetHeight(ToolCall call)
    {
        if (!call.Arguments.TryGetValue("target_height", out var text)) return null;
        if (!double.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value) || value is < 1 or > 100)
            throw new ArgumentException("target_height must be a decimal between 1 and 100 metres.");
        return value;
    }
    private static bool? ParseBoolean(ToolCall call, string key)
    {
        if (!call.Arguments.TryGetValue(key, out var text)) return null;
        if (text is not "true" and not "false") throw new ArgumentException($"{key} must be true or false.");
        return text == "true";
    }
    private static bool? ParseDevelopment(ToolCall call)
    {
        if (!call.Arguments.TryGetValue("development_build", out var text)) return null;
        if (!bool.TryParse(text, out var value) || text is not "true" and not "false") throw new ArgumentException("development_build must be true or false.");
        return value;
    }
    private static int? ParseSeed(ToolCall call)
    {
        if (!call.Arguments.TryGetValue("seed", out var text)) return null;
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value is < 1 or > 999_999)
            throw new ArgumentException("seed must be an integer between 1 and 999999.");
        return value;
    }
    private static int? ParseLevelCount(ToolCall call)
    {
        if (!call.Arguments.TryGetValue("level_count", out var text)) return null;
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || value is < 1 or > 12)
            throw new ArgumentException("level_count must be an integer between 1 and 12.");
        return value;
    }

    public IReadOnlyList<string> GetCampaignScenePaths()
    {
        if (Operation is not "create_game_campaign" and not "build_windows" || ScenePath is null || LevelCount is null)
            throw new InvalidOperationException("Campaign scene paths require a complete campaign creation or build specification.");
        var normalized = ScenePath.Replace('\\', '/'); var slash = normalized.LastIndexOf('/');
        var directory = normalized[..(slash + 1)]; var file = normalized[(slash + 1)..];
        const string suffix = "-Level01.unity";
        if (!file.EndsWith(suffix, StringComparison.Ordinal) || file.Length <= suffix.Length)
            throw new ArgumentException("Campaign scene_path must end with a bounded '-Level01.unity' filename.");
        var stem = file[..^"01.unity".Length];
        if (!System.Text.RegularExpressions.Regex.IsMatch(file[..^suffix.Length], @"^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("Campaign scene filename prefix must contain only letters, digits, underscores, or hyphens.");
        return Enumerable.Range(1, LevelCount.Value).Select(index => $"{directory}{stem}{index:00}.unity").ToArray();
    }
    private static void ValidateScenePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || !normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(path) || path.Any(char.IsControl))
            throw new ArgumentException("scene_path must be a traversal-free Assets/... .unity path.");
    }
    private static void ValidateBuildPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Builds/", StringComparison.Ordinal) || !normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(path) || path.Any(char.IsControl))
            throw new ArgumentException("output_path must be a traversal-free Builds/... .exe path.");
    }
    private static void ValidatePreviewPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Previews/", StringComparison.Ordinal) || !normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(path) || path.Length > 220 || path.Any(char.IsControl))
            throw new ArgumentException("preview output_path must be a traversal-free Previews/... .png path.");
    }
    private static void ValidateGeneratedAssetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(path) || path.Length > 240 || path.Any(char.IsControl))
            throw new ArgumentException("asset_path must be a generated GLB under Assets/ForgeCore/Generated.");
    }
    private static void ValidateObjectName(string name)
    {
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl) || name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException("object_name must be a plain printable Unity object name up to 100 characters.");
    }
}

/// <summary>
/// A live human attestation request for one exact imported-art identity. Ordinary model JSON cannot
/// express this tool kind. A proposal intentionally contains no decision; AgentRunner binds the
/// live approval result immediately before execution so the model cannot certify its own output.
/// </summary>
public sealed record GameArtReviewSpec(
    string ProjectPath,
    string ReviewKind,
    string AssetSetId,
    string AssetPath,
    string ExpectedAssetSha256,
    string PreviewPath,
    string ExpectedPreviewSha256,
    string ExpectedPreviewProvenanceSha256,
    string? ReviewDecision,
    string? AttestationSource)
{
    public const string Visual = "visual";
    public const string Rights = "rights";
    public const string UserLiveApproval = "user_live_approval";
    public static readonly IReadOnlySet<string> ReviewKinds = new HashSet<string>([Visual, Rights], StringComparer.Ordinal);

    public static GameArtReviewSpec FromProposal(ToolCall call) => Parse(call, execution: false);
    public static GameArtReviewSpec FromExecution(ToolCall call) => Parse(call, execution: true);

    public static ToolCall BindUserDecision(ToolCall proposal, ApprovalDecision decision)
    {
        _ = FromProposal(proposal);
        if (decision == ApprovalDecision.Cancelled)
            throw new ArgumentException("A cancelled approval cannot be bound as a game-art review decision.");
        var arguments = new Dictionary<string, string>(proposal.Arguments, StringComparer.Ordinal)
        {
            ["review_decision"] = decision == ApprovalDecision.Approved ? "approved" : "rejected",
            ["attestation_source"] = UserLiveApproval
        };
        var bound = proposal with { Arguments = arguments };
        _ = FromExecution(bound);
        return bound;
    }

    private static GameArtReviewSpec Parse(ToolCall call, bool execution)
    {
        if (call.Kind != ToolKind.ReviewGameArtAsset) throw new ArgumentException("Tool call is not a game-art review.");
        var allowed = execution
            ? new[] { "project_path", "review_kind", "asset_set_id", "asset_path", "expected_asset_sha256", "preview_path", "expected_preview_sha256", "expected_preview_provenance_sha256", "review_decision", "attestation_source" }
            : new[] { "project_path", "review_kind", "asset_set_id", "asset_path", "expected_asset_sha256", "preview_path", "expected_preview_sha256", "expected_preview_provenance_sha256" };
        if (call.Arguments.Keys.Any(key => !allowed.Contains(key, StringComparer.Ordinal)))
            throw new ArgumentException("Game-art review contains unexpected arguments.");
        var projectPath = Get("project_path");
        var reviewKind = Get("review_kind");
        var assetSetId = Get("asset_set_id");
        var assetPath = Get("asset_path");
        var assetHash = ValidateHash(Get("expected_asset_sha256"), "expected_asset_sha256");
        var previewPath = Get("preview_path");
        var previewHash = ValidateHash(Get("expected_preview_sha256"), "expected_preview_sha256");
        var previewProvenanceHash = ValidateHash(Get("expected_preview_provenance_sha256"), "expected_preview_provenance_sha256");
        if (!ReviewKinds.Contains(reviewKind)) throw new ArgumentException("review_kind must be visual or rights.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(assetSetId, @"^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("asset_set_id must be a bounded identifier.");
        ValidateAssetPath(assetPath);
        ValidatePreviewPath(previewPath);

        string? reviewDecision = null; string? attestationSource = null;
        if (execution)
        {
            reviewDecision = Get("review_decision");
            attestationSource = Get("attestation_source");
            if (reviewDecision is not "approved" and not "rejected")
                throw new ArgumentException("review_decision must be approved or rejected.");
            if (attestationSource != UserLiveApproval)
                throw new ArgumentException("Game-art review execution requires a live user-attestation source.");
        }
        return new(projectPath, reviewKind, assetSetId, assetPath.Replace('\\', '/'), assetHash,
            previewPath.Replace('\\', '/'), previewHash, previewProvenanceHash, reviewDecision, attestationSource);

        string Get(string key) => call.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing '{key}' argument.");
    }

    private static string ValidateHash(string value, string name)
    {
        if (value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException($"{name} must contain exactly 64 hexadecimal characters.");
        return value.ToLowerInvariant();
    }

    private static void ValidateAssetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(path) || path.Length > 240 || path.Any(char.IsControl))
            throw new ArgumentException("asset_path must be a generated GLB under Assets/ForgeCore/Generated.");
    }

    private static void ValidatePreviewPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("Previews/", StringComparison.Ordinal) || !normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(path) || path.Length > 220 || path.Any(char.IsControl))
            throw new ArgumentException("preview_path must be a traversal-free Previews/... .png path.");
    }
}

public static class TaskStateMachine
{
    private static readonly IReadOnlyDictionary<ForgeTaskState, ForgeTaskState[]> Allowed =
        new Dictionary<ForgeTaskState, ForgeTaskState[]>
        {
            [ForgeTaskState.Pending] = [ForgeTaskState.Planning, ForgeTaskState.Cancelled],
            [ForgeTaskState.Planning] = [ForgeTaskState.AwaitingApproval, ForgeTaskState.Running, ForgeTaskState.Failed, ForgeTaskState.Cancelled],
            [ForgeTaskState.AwaitingApproval] = [ForgeTaskState.Running, ForgeTaskState.Failed, ForgeTaskState.Cancelled],
            [ForgeTaskState.Running] = [ForgeTaskState.AwaitingApproval, ForgeTaskState.Completed, ForgeTaskState.Failed, ForgeTaskState.Cancelled],
            [ForgeTaskState.Completed] = [], [ForgeTaskState.Failed] = [], [ForgeTaskState.Cancelled] = []
        };

    public static bool CanTransition(ForgeTaskState from, ForgeTaskState to) => Allowed[from].Contains(to);
    public static void EnsureTransition(ForgeTaskState from, ForgeTaskState to)
    {
        if (!CanTransition(from, to)) throw new InvalidOperationException($"Invalid task transition: {from} -> {to}.");
    }
}
