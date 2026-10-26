using System.Text.RegularExpressions;

namespace ForgeCore.Core;

public enum GameArtAssetRole
{
    EnvironmentArchitecture,
    EnvironmentProps,
    GameplayObjects,
    Characters,
    CharacterAnimations,
    WeaponsAndTools,
    Vehicles,
    Effects,
    UserInterface
}

public sealed record GameArtAssetSlot(
    string Id,
    GameArtAssetRole Role,
    int MinimumAssetCount,
    bool RequiresPbrMaterials,
    bool RequiresRig,
    int MinimumAnimationClips,
    bool RequiresLods,
    bool RequiresCollision,
    IReadOnlyList<string> ValidationGates);

public sealed record GameArtSetPlan(
    int SchemaVersion,
    string StyleFamily,
    IReadOnlyList<GameArtAssetSlot> Slots,
    IReadOnlyList<string> SharedValidationGates)
{
    public const int CurrentSchemaVersion = 1;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidOperationException($"Unsupported game art-set schema version '{SchemaVersion}'.");
        if (!GameArtSetPlanner.AllowedStyleFamilies.Contains(StyleFamily, StringComparer.Ordinal))
            throw new InvalidOperationException($"Unsupported game art style family '{StyleFamily}'.");
        if (Slots.Count is < 4 or > 12) throw new InvalidOperationException("A game art set requires between four and twelve bounded asset slots.");
        if (Slots.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Slots.Count)
            throw new InvalidOperationException("A game art set cannot contain duplicate slot ids.");
        if (Slots.Select(x => x.Role).Distinct().Count() != Slots.Count)
            throw new InvalidOperationException("A game art set cannot contain duplicate asset roles.");
        foreach (var slot in Slots)
        {
            if (!Regex.IsMatch(slot.Id, "^[a-z][a-z0-9_]{2,47}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Game art slot ids must use bounded lowercase identifiers.");
            if (slot.MinimumAssetCount is < 1 or > 256) throw new InvalidOperationException("Game art slot asset counts must be between one and 256.");
            if (slot.MinimumAnimationClips is < 0 or > 128) throw new InvalidOperationException("Game art animation requirements must be between zero and 128 clips.");
            if (slot.Role == GameArtAssetRole.CharacterAnimations && slot.MinimumAnimationClips == 0)
                throw new InvalidOperationException("A character-animation slot requires at least one animation clip.");
            ValidateGates(slot.ValidationGates, "slot");
        }
        ValidateGates(SharedValidationGates, "shared");
    }

    private static void ValidateGates(IReadOnlyList<string> gates, string label)
    {
        if (gates.Count is < 1 or > 16 || gates.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 160))
            throw new InvalidOperationException($"Game art {label} validation gates must contain one to sixteen bounded entries.");
        if (gates.Distinct(StringComparer.Ordinal).Count() != gates.Count)
            throw new InvalidOperationException($"Game art {label} validation gates cannot contain duplicates.");
    }
}

public sealed record GameArtAssetEvidence(
    string AssetSetId,
    GameArtAssetRole Role,
    string StyleFamily,
    int AssetCount,
    bool HasPbrMaterials,
    bool IsRigged,
    int AnimationClipCount,
    bool HasLods,
    bool HasCollision,
    bool RightsReviewed,
    bool VisualReviewed,
    bool PerformanceMeasured = false,
    long VertexCount = 0,
    long TriangleCount = 0,
    int MaterialCount = 0,
    int TextureCount = 0,
    long TexturePixelCount = 0,
    long EstimatedTextureMemoryBytes = 0,
    int MaximumTextureWidth = 0,
    int MaximumTextureHeight = 0,
    bool ConventionsMeasured = false,
    bool HasGroundedPivot = false,
    bool HasUniformRootScale = false,
    bool HasCanonicalRootRotation = false)
{
    public void Validate()
    {
        if (!Regex.IsMatch(AssetSetId, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Game art evidence requires a bounded asset-set id.");
        if (!GameArtSetPlanner.AllowedStyleFamilies.Contains(StyleFamily, StringComparer.Ordinal))
            throw new InvalidOperationException("Game art evidence uses an unsupported style family.");
        if (AssetCount is < 1 or > 10000) throw new InvalidOperationException("Game art evidence asset count must be between one and 10000.");
        if (AnimationClipCount is < 0 or > 10000) throw new InvalidOperationException("Game art evidence animation count must be between zero and 10000.");
        if (VertexCount is < 0 or > 2_000_000_000 || TriangleCount is < 0 or > 2_000_000_000)
            throw new InvalidOperationException("Game art evidence geometry counts are outside the bounded range.");
        if (MaterialCount is < 0 or > 100_000 || TextureCount is < 0 or > 100_000)
            throw new InvalidOperationException("Game art evidence material or texture counts are outside the bounded range.");
        if (TexturePixelCount is < 0 or > 1_000_000_000_000 || EstimatedTextureMemoryBytes is < 0 or > 137_438_953_472 ||
            MaximumTextureWidth is < 0 or > 65_536 || MaximumTextureHeight is < 0 or > 65_536)
            throw new InvalidOperationException("Game art texture-resource evidence is outside the bounded range.");
        if (TextureCount == 0 && (TexturePixelCount != 0 || EstimatedTextureMemoryBytes != 0 || MaximumTextureWidth != 0 || MaximumTextureHeight != 0))
            throw new InvalidOperationException("Texture-resource evidence cannot exist when the texture count is zero.");
        if (TextureCount > 0 && (TexturePixelCount == 0 || EstimatedTextureMemoryBytes == 0 || MaximumTextureWidth == 0 || MaximumTextureHeight == 0))
            throw new InvalidOperationException("Measured textures require pixel, memory, and maximum-dimension evidence.");
        if (!PerformanceMeasured && (VertexCount != 0 || TriangleCount != 0 || MaterialCount != 0 || TextureCount != 0 ||
            TexturePixelCount != 0 || EstimatedTextureMemoryBytes != 0 || MaximumTextureWidth != 0 || MaximumTextureHeight != 0))
            throw new InvalidOperationException("Unmeasured game art evidence cannot carry performance counts.");
        if (!ConventionsMeasured && (HasGroundedPivot || HasUniformRootScale || HasCanonicalRootRotation))
            throw new InvalidOperationException("Unmeasured game art conventions cannot be marked complete.");
    }
}

public sealed record GameArtSetAssessment(
    bool Ready,
    int SatisfiedSlots,
    int TotalSlots,
    IReadOnlyList<string> Issues);

public static class GameArtSetPlanner
{
    private sealed record PerformanceBudget(long MaximumTotalVertices, long MaximumTotalTriangles,
        int MaximumTotalMaterials, int MaximumTotalTextures, long MaximumTotalTextureMemoryBytes);

    private sealed record RolePerformanceBudget(long MaximumVerticesPerAsset, long MaximumTrianglesPerAsset,
        int MaximumMaterialsPerAsset, int MaximumTexturesPerAsset, long MaximumTextureMemoryBytesPerAsset,
        int MaximumTextureDimension);

    public static IReadOnlyList<string> AllowedStyleFamilies { get; } =
        ["coherent_3d", "realistic_pbr", "stylized_3d", "illustrated_2d", "pixel_art_2d", "flexible_prototype"];
    public static IReadOnlyDictionary<string, GameArtAssetRole> RoleIds { get; } = new Dictionary<string, GameArtAssetRole>(StringComparer.Ordinal)
    {
        ["environment_architecture"] = GameArtAssetRole.EnvironmentArchitecture,
        ["environment_props"] = GameArtAssetRole.EnvironmentProps,
        ["gameplay_objects"] = GameArtAssetRole.GameplayObjects,
        ["characters"] = GameArtAssetRole.Characters,
        ["character_animations"] = GameArtAssetRole.CharacterAnimations,
        ["weapons_and_tools"] = GameArtAssetRole.WeaponsAndTools,
        ["vehicles"] = GameArtAssetRole.Vehicles,
        ["effects"] = GameArtAssetRole.Effects,
        ["user_interface"] = GameArtAssetRole.UserInterface
    };

    public static bool TryParseRole(string roleId, out GameArtAssetRole role) => RoleIds.TryGetValue(roleId, out role);
    public static string RoleId(GameArtAssetRole role) => RoleIds.Single(x => x.Value == role).Key;

    public static GameArtSetPlan Create(GameGenreSelection selection, string? prompt = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var plan = Create(selection.Dimension, selection.SystemPackIds, DetermineStyleFamily(selection.Dimension, prompt));
        plan.Validate();
        return plan;
    }

    public static GameArtSetPlan Create(GameBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        var plan = Create(blueprint.Dimension, blueprint.SystemPackIds, DetermineStyleFamily(blueprint.Dimension, null));
        plan.Validate();
        return plan;
    }

    public static GameArtSetAssessment Assess(GameArtSetPlan plan, IEnumerable<GameArtAssetEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(evidence);
        plan.Validate();
        var records = evidence.ToArray();
        if (records.Length > 64) throw new InvalidOperationException("A game art readiness assessment accepts at most 64 evidence records.");
        foreach (var record in records) record.Validate();
        if (records.Select(x => x.AssetSetId).Distinct(StringComparer.Ordinal).Count() != records.Length)
            throw new InvalidOperationException("Game art readiness evidence cannot contain duplicate asset-set ids.");

        var issues = new List<string>();
        foreach (var record in records.OrderBy(x => x.AssetSetId, StringComparer.Ordinal))
        {
            if (record.StyleFamily != plan.StyleFamily) issues.Add($"{record.AssetSetId}: style family does not match {plan.StyleFamily}.");
            if (!record.RightsReviewed) issues.Add($"{record.AssetSetId}: rights review is incomplete.");
            if (!record.VisualReviewed) issues.Add($"{record.AssetSetId}: visual review is incomplete.");
        }

        var satisfied = 0;
        var requiresPerformanceEvidence = IsThreeDimensionalStyle(plan.StyleFamily);
        foreach (var slot in plan.Slots)
        {
            var matches = records.Where(x => x.Role == slot.Role && x.StyleFamily == plan.StyleFamily).ToArray();
            var count = matches.Sum(x => x.AssetCount);
            var slotIssues = new List<string>();
            if (count < slot.MinimumAssetCount) slotIssues.Add($"requires {slot.MinimumAssetCount} assets; found {count}");
            if (matches.Length > 0 && slot.RequiresPbrMaterials && matches.Any(x => !x.HasPbrMaterials)) slotIssues.Add("PBR materials are incomplete");
            if (matches.Length > 0 && slot.RequiresRig && matches.Any(x => !x.IsRigged)) slotIssues.Add("rigging is incomplete");
            if (matches.Sum(x => x.AnimationClipCount) < slot.MinimumAnimationClips)
                slotIssues.Add($"requires {slot.MinimumAnimationClips} animation clips; found {matches.Sum(x => x.AnimationClipCount)}");
            if (matches.Length > 0 && slot.RequiresLods && matches.Any(x => !x.HasLods)) slotIssues.Add("LOD coverage is incomplete");
            if (matches.Length > 0 && slot.RequiresCollision && matches.Any(x => !x.HasCollision)) slotIssues.Add("collision coverage is incomplete");
            if (matches.Any(x => !x.RightsReviewed || !x.VisualReviewed)) slotIssues.Add("review evidence is incomplete");
            if (requiresPerformanceEvidence && matches.Any(x => !x.PerformanceMeasured))
                slotIssues.Add("performance measurements are incomplete");
            if (requiresPerformanceEvidence && matches.Any(x => ExceedsRoleBudget(x, slot.Role, static (record, budget) =>
                    record.VertexCount > budget.MaximumVerticesPerAsset * record.AssetCount)))
                slotIssues.Add("per-asset vertex budget is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => ExceedsRoleBudget(x, slot.Role, static (record, budget) =>
                    record.TriangleCount > budget.MaximumTrianglesPerAsset * record.AssetCount)))
                slotIssues.Add("per-asset triangle budget is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => ExceedsRoleBudget(x, slot.Role, static (record, budget) =>
                    record.MaterialCount > budget.MaximumMaterialsPerAsset * record.AssetCount)))
                slotIssues.Add("per-asset material budget is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => ExceedsRoleBudget(x, slot.Role, static (record, budget) =>
                    record.TextureCount > budget.MaximumTexturesPerAsset * record.AssetCount)))
                slotIssues.Add("per-asset texture budget is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => ExceedsRoleBudget(x, slot.Role, static (record, budget) =>
                    record.EstimatedTextureMemoryBytes > budget.MaximumTextureMemoryBytesPerAsset * record.AssetCount)))
                slotIssues.Add("per-asset texture-memory budget is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => x.PerformanceMeasured &&
                    (x.MaximumTextureWidth > RolePerformanceBudgetFor(slot.Role).MaximumTextureDimension ||
                     x.MaximumTextureHeight > RolePerformanceBudgetFor(slot.Role).MaximumTextureDimension)))
                slotIssues.Add("maximum texture dimension is exceeded");
            if (requiresPerformanceEvidence && matches.Any(x => !x.ConventionsMeasured))
                slotIssues.Add("pivot scale and orientation measurements are incomplete");
            if (requiresPerformanceEvidence && RequiresGroundedPivot(slot.Role) && matches.Any(x => x.ConventionsMeasured && !x.HasGroundedPivot))
                slotIssues.Add("grounded pivot convention is incomplete");
            if (requiresPerformanceEvidence && matches.Any(x => x.ConventionsMeasured && !x.HasUniformRootScale))
                slotIssues.Add("uniform root-scale convention is incomplete");
            if (requiresPerformanceEvidence && matches.Any(x => x.ConventionsMeasured && !x.HasCanonicalRootRotation))
                slotIssues.Add("canonical root-orientation convention is incomplete");
            if (slotIssues.Count == 0) satisfied++;
            else issues.Add($"{slot.Id}: {string.Join("; ", slotIssues)}.");
        }

        var performanceRecords = records.Where(x => x.StyleFamily == plan.StyleFamily).ToArray();
        if (requiresPerformanceEvidence && performanceRecords.Length > 0 && performanceRecords.All(x => x.PerformanceMeasured))
        {
            var budget = PerformanceBudgetFor(plan.StyleFamily);
            var vertices = performanceRecords.Sum(x => x.VertexCount);
            var triangles = performanceRecords.Sum(x => x.TriangleCount);
            var materials = performanceRecords.Sum(x => (long)x.MaterialCount);
            var textures = performanceRecords.Sum(x => (long)x.TextureCount);
            var textureMemory = performanceRecords.Sum(x => x.EstimatedTextureMemoryBytes);
            if (vertices > budget.MaximumTotalVertices)
                issues.Add($"art_set_performance: vertex budget exceeded ({vertices} > {budget.MaximumTotalVertices}).");
            if (triangles > budget.MaximumTotalTriangles)
                issues.Add($"art_set_performance: triangle budget exceeded ({triangles} > {budget.MaximumTotalTriangles}).");
            if (materials > budget.MaximumTotalMaterials)
                issues.Add($"art_set_performance: material budget exceeded ({materials} > {budget.MaximumTotalMaterials}).");
            if (textures > budget.MaximumTotalTextures)
                issues.Add($"art_set_performance: texture budget exceeded ({textures} > {budget.MaximumTotalTextures}).");
            if (textureMemory > budget.MaximumTotalTextureMemoryBytes)
                issues.Add($"art_set_performance: texture-memory budget exceeded ({textureMemory} > {budget.MaximumTotalTextureMemoryBytes}).");
        }

        var orderedIssues = issues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(orderedIssues.Length == 0 && satisfied == plan.Slots.Count, satisfied, plan.Slots.Count, orderedIssues);
    }

    internal static IReadOnlySet<GameArtAssetRole> RequiredRoles(GameDimension dimension, IReadOnlyList<string> systemPackIds)
    {
        var roles = new HashSet<GameArtAssetRole>
        {
            GameArtAssetRole.EnvironmentArchitecture,
            GameArtAssetRole.EnvironmentProps,
            GameArtAssetRole.GameplayObjects,
            GameArtAssetRole.Effects,
            GameArtAssetRole.UserInterface
        };
        if (systemPackIds.Contains("health_damage", StringComparer.Ordinal) || systemPackIds.Contains("enemy_ai", StringComparer.Ordinal) ||
            systemPackIds.Contains("dialogue_quests", StringComparer.Ordinal) || systemPackIds.Contains("strategy_units", StringComparer.Ordinal) ||
            systemPackIds.Contains("combat_melee", StringComparer.Ordinal) || systemPackIds.Contains("combat_ranged", StringComparer.Ordinal))
        {
            roles.Add(GameArtAssetRole.Characters);
            roles.Add(GameArtAssetRole.CharacterAnimations);
        }
        if (systemPackIds.Contains("combat_melee", StringComparer.Ordinal) || systemPackIds.Contains("combat_ranged", StringComparer.Ordinal) ||
            systemPackIds.Contains("interaction", StringComparer.Ordinal))
            roles.Add(GameArtAssetRole.WeaponsAndTools);
        if (systemPackIds.Contains("vehicle_physics", StringComparer.Ordinal)) roles.Add(GameArtAssetRole.Vehicles);
        return roles;
    }

    private static GameArtSetPlan Create(GameDimension dimension, IReadOnlyList<string> systemPackIds, string styleFamily)
    {
        var is3d = dimension is GameDimension.ThreeD or GameDimension.Hybrid;
        var pbr = is3d && styleFamily == "realistic_pbr";
        var slots = new List<GameArtAssetSlot>();
        void Add(string id, GameArtAssetRole role, int count, bool rig = false, int clips = 0, bool lods = false, bool collision = false, params string[] gates) =>
            slots.Add(new(id, role, count, pbr && role is not GameArtAssetRole.UserInterface and not GameArtAssetRole.Effects,
                rig, clips, lods, collision, gates));

        Add("environment_architecture", GameArtAssetRole.EnvironmentArchitecture, is3d ? 12 : 4, lods: is3d, collision: is3d,
            gates: ["modules align to a shared grid and scale", "materials render correctly in the target pipeline"]);
        Add("environment_props", GameArtAssetRole.EnvironmentProps, is3d ? 20 : 8, lods: is3d, collision: is3d,
            gates: ["props share scale and texture density", "hero and background silhouettes remain readable"]);
        Add("gameplay_objects", GameArtAssetRole.GameplayObjects, 6, lods: is3d, collision: is3d,
            gates: ["interactive objects have stable pivots", "objective state remains visually legible"]);
        Add("effects", GameArtAssetRole.Effects, 6, gates: ["combat and objective effects remain readable", "effects stay within the frame-time budget"]);
        Add("user_interface", GameArtAssetRole.UserInterface, 1, gates: ["HUD assets support target resolutions", "critical state is not conveyed by colour alone"]);

        var roles = RequiredRoles(dimension, systemPackIds);
        if (roles.Contains(GameArtAssetRole.Characters))
            Add("characters", GameArtAssetRole.Characters, 3, rig: is3d, lods: is3d, collision: is3d,
                gates: ["characters share a compatible skeleton convention", "character materials and proportions match the art direction"]);
        if (roles.Contains(GameArtAssetRole.CharacterAnimations))
            Add("character_animations", GameArtAssetRole.CharacterAnimations, 1, rig: is3d, clips: is3d ? 10 : 6,
                gates: ["locomotion and combat clips have compatible roots", "animation transitions have no visible discontinuities"]);
        if (roles.Contains(GameArtAssetRole.WeaponsAndTools))
            Add("weapons_and_tools", GameArtAssetRole.WeaponsAndTools, 3, lods: is3d, collision: is3d,
                gates: ["first-person and world scales agree", "attachment pivots follow a shared convention"]);
        if (roles.Contains(GameArtAssetRole.Vehicles))
            Add("vehicles", GameArtAssetRole.Vehicles, 3, lods: is3d, collision: is3d,
                gates: ["wheel and body pivots support the vehicle controller", "vehicle proportions match track and camera scale"]);

        return new(GameArtSetPlan.CurrentSchemaVersion, styleFamily, slots.OrderBy(x => x.Role).ToArray(),
        [
            "every source has immutable provenance and a reviewed licence record",
            "all accepted assets match one bounded style family",
            "scale pivots naming and orientation follow the project convention",
            "rendered previews pass visual review without missing materials",
            "combined scene budgets pass target-platform performance gates"
        ]);
    }

    private static string DetermineStyleFamily(GameDimension dimension, string? prompt)
    {
        var normalized = string.IsNullOrWhiteSpace(prompt) ? string.Empty : " " + Regex.Replace(prompt.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim() + " ";
        if (Contains(normalized, "pixel art") || Contains(normalized, "pixelated")) return "pixel_art_2d";
        if (Contains(normalized, "realistic") || Contains(normalized, "photorealistic") || Contains(normalized, "photoreal") ||
            Contains(normalized, "lifelike") || Contains(normalized, "real looking")) return "realistic_pbr";
        if (Contains(normalized, "stylized") || Contains(normalized, "cartoon") || Contains(normalized, "low poly")) return "stylized_3d";
        return dimension == GameDimension.TwoD ? "illustrated_2d" : dimension == GameDimension.ThreeD ? "coherent_3d" : "flexible_prototype";
    }

    private static bool Contains(string normalized, string phrase) =>
        normalized.Contains(" " + phrase + " ", StringComparison.Ordinal);

    private static bool IsThreeDimensionalStyle(string styleFamily) =>
        styleFamily is "coherent_3d" or "realistic_pbr" or "stylized_3d";

    private static bool ExceedsRoleBudget(GameArtAssetEvidence record, GameArtAssetRole role,
        Func<GameArtAssetEvidence, RolePerformanceBudget, bool> predicate) =>
        record.PerformanceMeasured && predicate(record, RolePerformanceBudgetFor(role));

    private static RolePerformanceBudget RolePerformanceBudgetFor(GameArtAssetRole role) => role switch
    {
        GameArtAssetRole.EnvironmentArchitecture => new(500_000, 300_000, 24, 64, 536_870_912, 8_192),
        GameArtAssetRole.EnvironmentProps => new(250_000, 150_000, 16, 48, 268_435_456, 8_192),
        GameArtAssetRole.GameplayObjects => new(250_000, 150_000, 16, 48, 268_435_456, 8_192),
        GameArtAssetRole.Characters => new(350_000, 200_000, 24, 64, 536_870_912, 8_192),
        GameArtAssetRole.CharacterAnimations => new(350_000, 200_000, 24, 64, 536_870_912, 8_192),
        GameArtAssetRole.WeaponsAndTools => new(250_000, 150_000, 16, 48, 268_435_456, 8_192),
        GameArtAssetRole.Vehicles => new(500_000, 300_000, 24, 64, 536_870_912, 8_192),
        GameArtAssetRole.Effects => new(200_000, 120_000, 24, 64, 268_435_456, 8_192),
        GameArtAssetRole.UserInterface => new(100_000, 60_000, 16, 48, 268_435_456, 8_192),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static PerformanceBudget PerformanceBudgetFor(string styleFamily) => styleFamily switch
    {
        "realistic_pbr" => new(18_000_000, 12_000_000, 1_536, 4_096, 8_589_934_592),
        "coherent_3d" => new(12_000_000, 8_000_000, 1_024, 3_072, 6_442_450_944),
        "stylized_3d" => new(8_000_000, 5_000_000, 768, 2_048, 4_294_967_296),
        _ => throw new ArgumentOutOfRangeException(nameof(styleFamily))
    };

    private static bool RequiresGroundedPivot(GameArtAssetRole role) => role is
        GameArtAssetRole.EnvironmentArchitecture or GameArtAssetRole.EnvironmentProps or GameArtAssetRole.GameplayObjects or
        GameArtAssetRole.Characters or GameArtAssetRole.WeaponsAndTools or GameArtAssetRole.Vehicles;
}
