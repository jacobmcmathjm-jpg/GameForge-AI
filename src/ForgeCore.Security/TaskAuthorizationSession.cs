using ForgeCore.Core;

namespace ForgeCore.Security;

public sealed record TaskAuthorizationSnapshot(bool Active, string ProjectPath, DateTimeOffset? ExpiresUtc, int RemainingActions)
{
    public static TaskAuthorizationSnapshot Inactive { get; } = new(false, string.Empty, null, 0);
}

/// <summary>
/// A deliberately narrow, in-memory pre-authorization grant. Security policy validation still runs
/// before this service is consulted, and the grant is never persisted across application restarts.
/// </summary>
public sealed class TaskAuthorizationSession : IAutomaticApprovalGrant
{
    private static readonly IReadOnlySet<string> AllowedUnityOperations = new HashSet<string>(StringComparer.Ordinal)
    {
        "compile", "create_map_demo", "create_game_template", "create_game_campaign", "prepare_generated_asset", "inspect_game_art_asset", "capture_game_art_preview", "play_mode", "verify_gameplay", "profile_scene", "capture_scene_preview", "run_tests", "build_windows"
    };
    private static readonly IReadOnlySet<string> Cc0Labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CC0", "CC0 1.0", "Creative Commons Zero", "Creative Commons CC0"
    };

    private readonly object _gate = new();
    private TaskAuthorizationSnapshot _state = TaskAuthorizationSnapshot.Inactive;

    public event EventHandler? Changed;

    public TaskAuthorizationSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                ExpireIfNeeded();
                return _state;
            }
        }
    }

    public void Activate(string projectPath, TimeSpan duration, int maxActions)
    {
        var normalized = NormalizeProject(projectPath);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(12)) throw new ArgumentOutOfRangeException(nameof(duration));
        if (maxActions is < 1 or > 150) throw new ArgumentOutOfRangeException(nameof(maxActions));
        lock (_gate) _state = new(true, normalized, DateTimeOffset.UtcNow.Add(duration), maxActions);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Revoke()
    {
        lock (_gate) _state = TaskAuthorizationSnapshot.Inactive;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryConsume(ApprovalRequest request)
    {
        var changed = false;
        lock (_gate)
        {
            ExpireIfNeeded();
            if (!_state.Active) return false;
            try
            {
                if (!IsWithinScope(request.Call, _state.ProjectPath)) return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            var remaining = _state.RemainingActions - 1;
            _state = remaining == 0 ? TaskAuthorizationSnapshot.Inactive : _state with { RemainingActions = remaining };
            changed = true;
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool IsWithinScope(ToolCall call, string projectPath)
    {
        if (call.RequiredPermission == PermissionKind.Administrator || call.Kind == ToolKind.RunCommand) return false;
        if (call.Kind == ToolKind.SearchAssetCatalog)
            return AssetCatalogSearchSpec.From(call) is { LicenseFilter: "cc0", MaxResults: <= 8 };
        if (call.Kind == ToolKind.StageWebAsset)
        {
            var asset = WebAssetSpec.From(call);
            return SameProject(asset.ProjectPath, projectPath) && Cc0Labels.Contains(asset.License.Trim());
        }
        if (!call.Arguments.TryGetValue("project_path", out var candidate) || !SameProject(candidate, projectPath)) return false;
        return call.Kind switch
        {
            ToolKind.InstallUnityBridge or ToolKind.ScaffoldUnityMapDemo or ToolKind.ScaffoldUnityGameTemplate or ToolKind.InspectStagedAsset or
                ToolKind.ImportStagedAsset or ToolKind.StoreReviewedAsset or ToolKind.ImportVaultAsset or ToolKind.CreateBlenderAsset or ToolKind.ImportBlenderAsset or
                ToolKind.DotnetBuild or ToolKind.DotnetTest or ToolKind.UnityPlayerSmokeTest or ToolKind.UnityPlayerProfileTest => true,
            ToolKind.UnityEditorAction => call.Arguments.TryGetValue("operation", out var operation) && AllowedUnityOperations.Contains(operation),
            _ => false
        };
    }

    private void ExpireIfNeeded()
    {
        if (_state.Active && _state.ExpiresUtc <= DateTimeOffset.UtcNow) _state = TaskAuthorizationSnapshot.Inactive;
    }

    private static bool SameProject(string candidate, string expected) =>
        string.Equals(NormalizeProject(candidate), expected, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path is required.", nameof(path));
        return path.Trim().Replace('\\', '/').Trim('/');
    }
}
