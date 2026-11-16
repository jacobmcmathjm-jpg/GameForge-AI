namespace ForgeCore.Core;

// Experience-based, deterministic advice. This does not train neural weights, execute code,
// confer human approval, or equate a cheaper mesh with better-looking art.
public sealed record AssetExperience(
    int SchemaVersion, string PolicyVersion, string EventId, string Family, string Stage,
    string ConfigurationSha256, string ComparisonSha256, string? AssetSha256, string? BundleSha256,
    bool Success, bool Reused, string? EvidencePath, string? EvidenceSha256,
    Dictionary<string, double> Measurements)
{
    public void Validate()
    {
        static bool Hash(string? s) => s is { Length: 64 } && s.All(c => char.IsAsciiHexDigit(c) && !char.IsUpper(c));
        if (SchemaVersion != 1 || PolicyVersion != AssetImprovementModel.Version ||
            !Guid.TryParseExact(EventId, "N", out _) || !AssetImprovementModel.Families.Contains(Family) ||
            Stage is not ("source" or "rig" or "motion") || !Hash(ConfigurationSha256) || !Hash(ComparisonSha256) ||
            (AssetSha256 is not null && !Hash(AssetSha256)) || (BundleSha256 is not null && !Hash(BundleSha256)) ||
            Measurements is null || Measurements.Count > 8 ||
            Measurements.Any(m => !AllowedMeasurements(Stage).Contains(m.Key) || !double.IsFinite(m.Value) || m.Value < 0 || m.Value > 1_000_000) ||
            (Success && (!Hash(AssetSha256) || !Hash(EvidenceSha256) || EvidencePath is null)) ||
            (!Success && (Measurements.Count != 0 || EvidencePath is not null || EvidenceSha256 is not null || Reused)))
            throw new ArgumentException("Invalid bounded asset experience. No free-text instructions or approval flags are accepted.");
        if (EvidencePath is not null && (EvidencePath.Length > 240 || EvidencePath.StartsWith('/') || EvidencePath.Contains('\\') ||
            EvidencePath.Contains(':') || EvidencePath.Split('/').Any(s => s is "" or "." or "..") || EvidencePath.Any(char.IsControl)))
            throw new ArgumentException("Experience evidence must be a bounded relative path.");
        if (Success && !AllowedMeasurements(Stage).Order().SequenceEqual(Measurements.Keys.Order()))
            throw new ArgumentException("Successful experience requires every stage measurement.");
    }

    public static IReadOnlyList<string> AllowedMeasurements(string stage) => stage switch
    {
        "source" => ["triangles", "vertices", "materials", "textureResolution"],
        "rig" => ["maximumEdgeStretch", "maximumWeightSumError", "maximumRestError", "maximumRecoveryError"],
        "motion" => ["maximumEdgeStretch", "maximumLoopError", "maximumStanceSlip"],
        _ => []
    };
}

public sealed record AssetImprovementAdvice(string Stage, int RecordedAttempts, int DistinctConfigurations,
    int DistinctSuccessfulCandidates, int FailedConfigurations, int CacheReuses,
    string[] TechnicalFrontier, string[] NextSteps);

public static class AssetImprovementModel
{
    public const string Version = "asset_experience_v1";
    public static IReadOnlySet<string> Families { get; } = new HashSet<string>(StringComparer.Ordinal)
        { "barrel", "wooden_crate", "concrete_barrier", "industrial_fence", "shipping_container", "humanoid", "industrial_security" };

    public static string FamilyFor(string recipe) => recipe switch
    {
        BlenderAssetSpec.SupportedRecipe => "barrel",
        BlenderAssetSpec.CharacterRecipe => "humanoid",
        BlenderAssetSpec.SecurityCharacterRecipe => "industrial_security",
        _ => IndustrialPropRecipes.Find(recipe)?.FamilyId switch
        {
            "industrial_barrel_v2" => "barrel", "wooden_crate_v1" => "wooden_crate",
            "concrete_barrier_v1" => "concrete_barrier", "industrial_fence_v1" => "industrial_fence",
            "shipping_container_v1" => "shipping_container", _ => throw new ArgumentException("Unsupported asset learning family.")
        }
    };

    public static AssetImprovementAdvice Advise(string stage, IReadOnlyList<AssetExperience> history)
    {
        if (stage is not ("source" or "rig" or "motion") || history.Count > 256)
            throw new ArgumentException("Invalid or unbounded learning query.");
        foreach (var e in history) e.Validate();
        var events = history.Where(e => e.Stage == stage).GroupBy(e => e.EventId).Select(g =>
        {
            // Conflicting replay IDs are not additional learning evidence.
            if (g.Select(e => (e.ConfigurationSha256, e.Success, e.EvidenceSha256)).Distinct().Count() != 1)
                throw new ArgumentException("Conflicting experience event identity.");
            return g.First();
        }).ToArray();
        var success = events.Where(e => e.Success).GroupBy(e => (e.ConfigurationSha256, e.EvidenceSha256)).Select(g => g.First()).ToArray();
        // Only pose/motion errors have a meaningful numeric direction. Source triangle counts
        // describe cost, not beauty. Never compare different rigs, recipes or test contexts.
        var frontier = stage == "source" ? [] : success.Where(candidate => !success.Any(other =>
            other != candidate && other.Family == candidate.Family && other.ComparisonSha256 == candidate.ComparisonSha256 &&
            candidate.Measurements.All(m => other.Measurements[m.Key] <= m.Value) &&
            candidate.Measurements.Any(m => other.Measurements[m.Key] < m.Value)))
            .Select(e => e.ConfigurationSha256).Distinct().Order(StringComparer.Ordinal).ToArray();
        var steps = new List<string>();
        if (events.Length == 0) steps.Add("collect_measured_baseline_no_history");
        if (events.Any(e => !e.Success)) steps.Add("diagnose_failed_configuration_before_repeating_no_cause_inferred");
        if (success.Length != 0)
        {
            steps.Add(stage == "source" ? "compare_exact_previews_with_human_no_geometry_count_quality_score" : "preserve_non_dominated_measured_baselines_recheck_runtime_transitions");
            steps.Add("require_separate_current_visual_rights_and_target_reviews");
        }
        if (success.Select(e => e.ComparisonSha256).Distinct().Count() > 1)
            steps.Add("different_test_contexts_are_not_ranked_against_each_other");
        return new(stage, events.Length, events.Select(e => e.ConfigurationSha256).Distinct().Count(),
            success.Select(e => e.AssetSha256).Distinct().Count(), events.Where(e => !e.Success).Select(e => e.ConfigurationSha256).Distinct().Count(),
            events.Count(e => e.Reused), frontier, steps.ToArray());
    }
}
