using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ForgeCore.Core;

public enum GameDimension { TwoD, ThreeD, Hybrid }
public enum GenreCertification { Prototype, Certified }

public sealed record GameSystemPack(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> ValidationGates);

public sealed record GameGenrePack(
    string Id,
    string DisplayName,
    GameDimension Dimension,
    string Perspective,
    GenreCertification Certification,
    IReadOnlyDictionary<string, int> Keywords,
    IReadOnlyList<string> SystemPackIds);

public sealed record GameGenreSelection(
    string PrimaryGenreId,
    IReadOnlyList<string> SecondaryGenreIds,
    GameDimension Dimension,
    string Perspective,
    GenreCertification Certification,
    IReadOnlyList<string> SystemPackIds,
    IReadOnlyList<string> ValidationGates,
    double Confidence,
    bool UsesFallback,
    IReadOnlyList<string> Rationale);

public sealed record GameLevelBlueprint(
    int Index,
    string Id,
    string Role,
    int Seed,
    IReadOnlyList<string> FocusSystemPackIds,
    IReadOnlyList<string> ValidationGates);

public sealed record GameLevelTransition(string FromLevelId, string ToLevelId, string Condition);

public sealed record GameCampaignBlueprint(
    string ProgressionMode,
    IReadOnlyList<GameLevelBlueprint> Levels,
    IReadOnlyList<GameLevelTransition> Transitions);

public sealed record GameBlueprint(
    int SchemaVersion,
    string PrimaryGenreId,
    IReadOnlyList<string> SecondaryGenreIds,
    GameDimension Dimension,
    string Perspective,
    GenreCertification Certification,
    IReadOnlyList<string> SystemPackIds,
    IReadOnlyList<string> ValidationGates,
    GameCampaignBlueprint Campaign,
    double Confidence,
    bool UsesFallback,
    IReadOnlyList<string> Rationale,
    DateTimeOffset CreatedUtc,
    GameArtSetPlan? ArtSet = null)
{
    public const int CurrentSchemaVersion = 3;

    public static GameBlueprint FromSelection(GameGenreSelection selection, DateTimeOffset? createdUtc = null, int levelCount = 3,
        GameArtSetPlan? artSet = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new(CurrentSchemaVersion, selection.PrimaryGenreId, selection.SecondaryGenreIds.ToArray(),
            selection.Dimension, selection.Perspective, selection.Certification, selection.SystemPackIds.ToArray(),
            selection.ValidationGates.ToArray(), GameGenreRegistry.CreateCampaignPlan(selection, levelCount), selection.Confidence, selection.UsesFallback,
            selection.Rationale.ToArray(), createdUtc ?? DateTimeOffset.UtcNow, artSet ?? GameArtSetPlanner.Create(selection));
    }

    public static GameBlueprint FromPrompt(string prompt, DateTimeOffset? createdUtc = null)
    {
        var selection = GameGenreRegistry.Analyze(prompt);
        return FromSelection(selection, createdUtc, GameGenreRegistry.DetermineLevelCount(prompt), GameArtSetPlanner.Create(selection, prompt));
    }

    public GameBlueprint UpgradeToCurrent()
    {
        if (SchemaVersion == CurrentSchemaVersion && ArtSet is not null) return this;
        if (SchemaVersion == 2 && ArtSet is null)
            return this with { SchemaVersion = CurrentSchemaVersion, ArtSet = GameArtSetPlanner.Create(this) };
        throw new InvalidOperationException($"Unsupported game blueprint schema version '{SchemaVersion}'.");
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidOperationException($"Unsupported game blueprint schema version '{SchemaVersion}'.");
        if (string.IsNullOrWhiteSpace(PrimaryGenreId)) throw new InvalidOperationException("A game blueprint requires a primary genre.");
        if (string.IsNullOrWhiteSpace(Perspective)) throw new InvalidOperationException("A game blueprint requires a perspective.");
        if (SystemPackIds.Count == 0) throw new InvalidOperationException("A game blueprint requires at least one system pack.");
        if (ValidationGates.Count == 0) throw new InvalidOperationException("A game blueprint requires at least one validation gate.");
        if (Confidence is < 0 or > 1) throw new InvalidOperationException("Game blueprint confidence must be between zero and one.");
        if (SystemPackIds.Distinct(StringComparer.Ordinal).Count() != SystemPackIds.Count)
            throw new InvalidOperationException("A game blueprint cannot contain duplicate system packs.");
        if (ValidationGates.Distinct(StringComparer.Ordinal).Count() != ValidationGates.Count)
            throw new InvalidOperationException("A game blueprint cannot contain duplicate validation gates.");
        if (ArtSet is null) throw new InvalidOperationException("A game blueprint requires a coherent art-set plan.");
        ArtSet.Validate();
        var plannedRoles = ArtSet.Slots.Select(x => x.Role).ToHashSet();
        var missingRoles = GameArtSetPlanner.RequiredRoles(Dimension, SystemPackIds).Where(x => !plannedRoles.Contains(x)).ToArray();
        if (missingRoles.Length > 0)
            throw new InvalidOperationException("A game blueprint art set is missing required roles: " + string.Join(", ", missingRoles) + ".");
        if (Campaign is null || Campaign.ProgressionMode != "linear") throw new InvalidOperationException("A game blueprint requires a linear campaign plan.");
        if (Campaign.Levels.Count is < 1 or > 12) throw new InvalidOperationException("A campaign requires between one and twelve levels.");
        if (Campaign.Transitions.Count != Campaign.Levels.Count - 1) throw new InvalidOperationException("A linear campaign requires exactly one transition between adjacent levels.");
        var levelIds = new HashSet<string>(StringComparer.Ordinal); var seeds = new HashSet<int>();
        for (var index = 0; index < Campaign.Levels.Count; index++)
        {
            var level = Campaign.Levels[index];
            if (level.Index != index + 1 || !Regex.IsMatch(level.Id, @"^level-[0-9]{2}-(onboarding|core|escalation|finale)$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Campaign levels require contiguous indices and bounded ids.");
            if (!levelIds.Add(level.Id) || level.Seed is < 1 or > 999999 || !seeds.Add(level.Seed))
                throw new InvalidOperationException("Campaign level ids and seeds must be unique and bounded.");
            if (level.FocusSystemPackIds.Count == 0 || level.FocusSystemPackIds.Any(x => !SystemPackIds.Contains(x, StringComparer.Ordinal)))
                throw new InvalidOperationException("Campaign level focus systems must belong to the game blueprint.");
            if (level.ValidationGates.Count == 0 || level.ValidationGates.Any(x => !ValidationGates.Contains(x, StringComparer.Ordinal)))
                throw new InvalidOperationException("Campaign level validation gates must belong to the game blueprint.");
            if (index < Campaign.Transitions.Count)
            {
                var transition = Campaign.Transitions[index];
                if (transition.FromLevelId != level.Id || transition.ToLevelId != Campaign.Levels[index + 1].Id || transition.Condition != "complete_required_objectives")
                    throw new InvalidOperationException("Campaign transitions must connect adjacent levels through reviewed completion state.");
            }
        }
    }
}

public static partial class GameGenreRegistry
{
    private const string FallbackGenreId = "sandbox_prototype";

    public static IReadOnlyList<GameSystemPack> SystemPacks { get; } =
    [
        Pack("core_runtime", "Core runtime", [], "startup scene loads", "no unhandled runtime exceptions"),
        Pack("input_actions", "Input actions", ["core_runtime"], "keyboard input works", "input bindings are discoverable"),
        Pack("ui_hud", "HUD and menus", ["core_runtime"], "HUD renders at supported resolutions", "pause and restart remain available"),
        Pack("audio_feedback", "Audio feedback", ["core_runtime"], "critical actions produce feedback", "missing audio never blocks play"),
        Pack("save_progress", "Save and progress", ["core_runtime"], "save data round-trips", "invalid save data fails safely"),
        Pack("accessibility", "Accessibility baseline", ["input_actions", "ui_hud"], "essential actions can be rebound", "status is not conveyed by colour alone"),
        Pack("locomotion_3d", "3D locomotion", ["input_actions"], "player moves and stops smoothly", "player can recover from invalid world position"),
        Pack("locomotion_2d", "2D locomotion", ["input_actions"], "player movement respects the game plane", "movement is frame-rate independent"),
        Pack("camera_third_person", "Third-person camera", ["locomotion_3d"], "camera follows the player", "camera resolves world obstruction"),
        Pack("camera_first_person", "First-person camera", ["locomotion_3d"], "camera rotation is bounded", "camera and player facing remain synchronized"),
        Pack("camera_top_down", "Top-down camera", ["core_runtime"], "playfield remains visible", "camera bounds are enforced"),
        Pack("platforming", "Platforming", ["locomotion_2d"], "jump arc is deterministic", "checkpoints recover failed jumps"),
        Pack("interaction", "World interaction", ["input_actions", "ui_hud"], "valid targets show an interaction prompt", "invalid targets cannot activate"),
        Pack("objectives", "Objectives and progression", ["interaction", "save_progress"], "objective order is valid", "completion state is reachable"),
        Pack("health_damage", "Health and damage", ["core_runtime", "ui_hud"], "damage cannot underflow health", "death has a recovery or terminal state"),
        Pack("combat_melee", "Melee combat", ["health_damage", "input_actions"], "attack cooldown is enforced", "hits require a valid target in range"),
        Pack("combat_ranged", "Ranged combat", ["health_damage", "input_actions"], "projectiles or traces respect collision", "ammo state cannot become negative"),
        Pack("enemy_ai", "Enemy state AI", ["health_damage"], "enemy state transitions are bounded", "enemies respect spawn or leash constraints"),
        Pack("inventory", "Inventory and items", ["save_progress", "ui_hud"], "inventory capacity rules hold", "item identity survives save and load"),
        Pack("crafting", "Crafting", ["inventory"], "recipes consume exact ingredients", "invalid recipes cannot create items"),
        Pack("dialogue_quests", "Dialogue and quests", ["objectives", "ui_hud"], "dialogue branches reference valid nodes", "quest state cannot regress unexpectedly"),
        Pack("collectibles", "Collectibles", ["objectives"], "collectibles count once", "all required collectibles are reachable"),
        Pack("vehicle_physics", "Vehicle physics", ["input_actions"], "vehicle can accelerate brake and steer", "vehicle can recover from rollover"),
        Pack("race_rules", "Race rules", ["vehicle_physics", "objectives"], "lap and checkpoint order is enforced", "finish placement is deterministic"),
        Pack("strategy_units", "Strategy units", ["input_actions", "camera_top_down", "health_damage"], "selection and commands target valid units", "unit ownership is enforced"),
        Pack("economy_simulation", "Economy simulation", ["save_progress", "ui_hud"], "resources cannot become invalid", "simulation advances deterministically"),
        Pack("puzzle_logic", "Puzzle logic", ["interaction", "objectives"], "puzzle has at least one valid solution", "reset restores the initial state"),
        Pack("procedural_generation", "Procedural generation", ["core_runtime"], "same seed reproduces the layout", "generated objectives remain reachable"),
        Pack("stealth", "Stealth", ["enemy_ai", "ui_hud"], "detection state has bounded transitions", "line-of-sight respects collision"),
        Pack("atmosphere", "Atmosphere and tension", ["audio_feedback"], "critical gameplay remains readable", "effects remain within performance budget"),
        Pack("rhythm_timing", "Rhythm timing", ["input_actions", "audio_feedback"], "input judgement uses a bounded timing window", "audio and chart time stay synchronized"),
        Pack("networking", "Multiplayer networking", ["core_runtime"], "authority rules are explicit", "disconnect has a safe recovery path"),
        Pack("touch_input", "Touch input", ["input_actions", "ui_hud"], "essential controls fit supported screens", "multi-touch gestures are bounded"),
        Pack("controller_support", "Controller support", ["input_actions"], "all essential actions have controller bindings", "disconnect falls back safely"),
        Pack("xr_interaction", "XR interaction", ["interaction", "input_actions"], "tracked interaction has a safe fallback", "comfort constraints are documented")
    ];

    public static IReadOnlyList<GameGenrePack> GenrePacks { get; } =
    [
        Genre("third_person_action", "Third-person action adventure", GameDimension.ThreeD, "third-person", GenreCertification.Certified,
            Keywords(("third person", 6), ("action adventure", 5), ("adventure", 2), ("melee", 3), ("exploration", 2)),
            "locomotion_3d", "camera_third_person", "interaction", "objectives", "health_damage", "combat_melee", "enemy_ai"),
        Genre("first_person_action", "First-person action", GameDimension.ThreeD, "first-person", GenreCertification.Prototype,
            Keywords(("first person", 6), ("fps", 6), ("boomer shooter", 6), ("shooter", 3), ("gun", 2)),
            "locomotion_3d", "camera_first_person", "interaction", "objectives", "health_damage", "combat_ranged", "enemy_ai"),
        Genre("platformer", "Platformer", GameDimension.Hybrid, "side-view or third-person", GenreCertification.Prototype,
            Keywords(("platformer", 7), ("platform game", 6), ("metroidvania", 7), ("jumping", 2), ("side scroller", 6)),
            "locomotion_2d", "platforming", "collectibles", "objectives"),
        Genre("top_down_rpg", "Top-down RPG", GameDimension.Hybrid, "top-down or isometric", GenreCertification.Prototype,
            Keywords(("top down", 5), ("isometric", 5), ("rpg", 5), ("role playing", 6), ("dungeon crawler", 7), ("action rpg", 7)),
            "locomotion_2d", "camera_top_down", "interaction", "objectives", "health_damage", "combat_melee", "enemy_ai", "inventory", "dialogue_quests"),
        Genre("survival_crafting", "Survival crafting", GameDimension.ThreeD, "first-person or third-person", GenreCertification.Prototype,
            Keywords(("survival", 6), ("crafting", 5), ("craft", 3), ("base building", 6), ("hunger", 4), ("open world", 2)),
            "locomotion_3d", "camera_third_person", "interaction", "objectives", "health_damage", "enemy_ai", "inventory", "crafting", "procedural_generation"),
        Genre("racing", "Racing", GameDimension.ThreeD, "chase or cockpit", GenreCertification.Prototype,
            Keywords(("racing", 7), ("race", 4), ("kart", 6), ("driving", 5), ("motorsport", 7), ("rally", 6)),
            "vehicle_physics", "race_rules", "camera_third_person", "objectives"),
        Genre("strategy", "Strategy", GameDimension.Hybrid, "strategic top-down", GenreCertification.Prototype,
            Keywords(("strategy", 6), ("rts", 7), ("turn based", 6), ("tower defense", 7), ("tactics", 6), ("4x", 7)),
            "camera_top_down", "strategy_units", "objectives", "economy_simulation"),
        Genre("simulation_management", "Simulation and management", GameDimension.Hybrid, "free or top-down", GenreCertification.Prototype,
            Keywords(("simulation", 6), ("simulator", 6), ("management", 6), ("tycoon", 7), ("city builder", 7), ("automation", 5)),
            "camera_top_down", "interaction", "objectives", "economy_simulation"),
        Genre("puzzle", "Puzzle", GameDimension.Hybrid, "flexible", GenreCertification.Prototype,
            Keywords(("puzzle", 7), ("logic game", 6), ("escape room", 7), ("match three", 7), ("word game", 6)),
            "interaction", "objectives", "puzzle_logic"),
        Genre("narrative_adventure", "Narrative adventure", GameDimension.Hybrid, "flexible", GenreCertification.Prototype,
            Keywords(("visual novel", 8), ("narrative", 6), ("story driven", 6), ("interactive fiction", 8), ("dialogue", 4), ("walking simulator", 7)),
            "interaction", "objectives", "dialogue_quests", "atmosphere"),
        Genre("sports", "Sports", GameDimension.ThreeD, "broadcast or player-follow", GenreCertification.Prototype,
            Keywords(("sports", 6), ("football", 6), ("soccer", 6), ("basketball", 6), ("tennis", 6), ("golf", 6)),
            "locomotion_3d", "camera_third_person", "objectives", "health_damage"),
        Genre("rhythm", "Rhythm", GameDimension.Hybrid, "fixed or lane-based", GenreCertification.Prototype,
            Keywords(("rhythm", 8), ("music game", 7), ("beat game", 7), ("dance game", 7)),
            "rhythm_timing", "ui_hud", "objectives"),
        Genre(FallbackGenreId, "Sandbox prototype", GameDimension.Hybrid, "flexible", GenreCertification.Prototype,
            Keywords(), "interaction", "objectives", "procedural_generation")
    ];

    private static readonly IReadOnlyDictionary<string, string[]> FeatureSignals = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["networking"] = ["multiplayer", "co-op", "coop", "online pvp", "online game"],
        ["procedural_generation"] = ["procedural", "roguelike", "randomly generated", "infinite world"],
        ["dialogue_quests"] = ["dialogue", "quest", "story", "npc conversation"],
        ["inventory"] = ["inventory", "loot", "items", "equipment"],
        ["crafting"] = ["craft", "crafting", "recipes", "base building"],
        ["stealth"] = ["stealth", "sneak", "detection", "infiltration"],
        ["atmosphere"] = ["horror", "scary", "tense", "atmospheric"],
        ["vehicle_physics"] = ["vehicle", "car", "truck", "motorcycle", "spaceship"],
        ["controller_support"] = ["controller", "gamepad", "console"],
        ["touch_input"] = ["mobile", "touchscreen", "phone", "tablet"],
        ["xr_interaction"] = ["virtual reality", "vr game", "mixed reality", "xr"]
    };

    private static readonly IReadOnlyDictionary<string, GameSystemPack> SystemsById = SystemPacks.ToDictionary(x => x.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, GameGenrePack> GenresById = GenrePacks.ToDictionary(x => x.Id, StringComparer.Ordinal);

    static GameGenreRegistry() => ValidateCatalog();

    public static bool IsGameCreationPrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (prompt.Length > 100_000) throw new ArgumentException("Game prompt exceeds 100000 characters.", nameof(prompt));
        var normalized = Normalize(prompt);
        return ContainsPhrase(normalized, "game") || ContainsPhrase(normalized, "games") ||
               ContainsPhrase(normalized, "playable demo") ||
               ContainsPhrase(normalized, "unity project") || ContainsPhrase(normalized, "gameplay");
    }

    public static GameGenreSelection Analyze(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (prompt.Length > 100_000) throw new ArgumentException("Game prompt exceeds 100000 characters.", nameof(prompt));
        var normalized = Normalize(prompt);
        var scored = GenrePacks.Where(x => x.Id != FallbackGenreId)
            .Select(genre => new ScoredGenre(genre, Score(genre, normalized)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Genre.Id, StringComparer.Ordinal)
            .ToArray();

        if (scored.Length == 0)
            return BuildSelection(GenresById[FallbackGenreId], [], normalized, 0.25, true,
                ["No certified genre vocabulary matched; selected the bounded sandbox prototype fallback."]);

        var primary = scored[0];
        var secondaries = scored.Skip(1)
            .Where(x => x.Score >= 3 && x.Score >= primary.Score * 0.55)
            .Take(2)
            .Select(x => x.Genre)
            .ToArray();
        var confidence = Math.Clamp(0.35 + primary.Score / 16d, 0.35, 0.99);
        var rationale = new List<string> { $"Matched {primary.Genre.DisplayName} with score {primary.Score}." };
        if (secondaries.Length > 0) rationale.Add("Hybrid signals: " + string.Join(", ", secondaries.Select(x => x.DisplayName)) + ".");
        return BuildSelection(primary.Genre, secondaries, normalized, confidence, false, rationale);
    }

    public static int DetermineLevelCount(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (prompt.Length > 100_000) throw new ArgumentException("Game prompt exceeds 100000 characters.", nameof(prompt));
        var match = LevelCountRegex().Match(prompt);
        return match.Success && int.TryParse(match.Groups["count"].Value, out var value) ? Math.Clamp(value, 1, 12) : 3;
    }

    public static GameCampaignBlueprint CreateCampaignPlan(GameGenreSelection selection, int levelCount)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (levelCount is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(levelCount), "Campaigns support between one and twelve levels.");
        var mechanics = selection.SystemPackIds.Where(x => x is not "core_runtime" and not "input_actions" and not "ui_hud" and not "audio_feedback" and not "save_progress" and not "accessibility").ToArray();
        if (mechanics.Length == 0) mechanics = selection.SystemPackIds.ToArray();
        var levels = new List<GameLevelBlueprint>(levelCount);
        for (var index = 0; index < levelCount; index++)
        {
            var role = levelCount == 1 ? "core" : index == 0 ? "onboarding" : index == levelCount - 1 ? "finale" : "escalation";
            var focus = new[] { mechanics[index % mechanics.Length], mechanics[(index + 1) % mechanics.Length] }.Distinct(StringComparer.Ordinal).ToArray();
            var gates = focus.SelectMany(id => SystemsById[id].ValidationGates).Distinct(StringComparer.Ordinal).Take(4).ToArray();
            var number = index + 1; var id = $"level-{number:00}-{role}";
            levels.Add(new(number, id, role, DeterministicLevelSeed(selection, number), focus, gates));
        }
        var transitions = levels.Zip(levels.Skip(1), (from, to) =>
            new GameLevelTransition(from.Id, to.Id, "complete_required_objectives")).ToArray();
        return new("linear", levels, transitions);
    }

    public static string CreatePlanningBrief(string prompt)
    {
        if (!IsGameCreationPrompt(prompt)) return "(not a game-generation request)";
        var selection = Analyze(prompt);
        var builder = new StringBuilder();
        builder.AppendLine("Deterministic game-design classification (advisory; it grants no tool permissions):");
        builder.AppendLine($"- primary genre: {selection.PrimaryGenreId}");
        builder.AppendLine($"- secondary genres: {(selection.SecondaryGenreIds.Count == 0 ? "none" : string.Join(", ", selection.SecondaryGenreIds))}");
        builder.AppendLine($"- dimension/perspective: {selection.Dimension} / {selection.Perspective}");
        builder.AppendLine($"- template certification: {selection.Certification}");
        builder.AppendLine($"- required system packs: {string.Join(", ", selection.SystemPackIds)}");
        builder.AppendLine($"- validation gates: {string.Join("; ", selection.ValidationGates)}");
        var artSet = GameArtSetPlanner.Create(selection, prompt);
        builder.AppendLine($"- bounded art direction: {artSet.StyleFamily}");
        builder.AppendLine($"- coherent art-set slots: {string.Join(", ", artSet.Slots.Select(x => $"{x.Id}:{x.MinimumAssetCount}"))}");
        builder.AppendLine("- art-set readiness requires provenance, rights review, visual review, consistent scale/materials, and performance evidence; this grants no asset or network permission.");
        builder.AppendLine(selection.UsesFallback
            ? "- use a bounded basic scene and prototype missing systems; do not claim a certified complete-game template."
            : "- implement and verify one system pack at a time; use only currently allow-listed tools and certified templates.");
        return builder.ToString().TrimEnd();
    }

    public static void ValidateCatalog()
    {
        EnsureUnique(SystemPacks.Select(x => x.Id), "system pack");
        EnsureUnique(GenrePacks.Select(x => x.Id), "genre pack");
        foreach (var system in SystemPacks)
            foreach (var dependency in system.Dependencies)
                if (!SystemsById.ContainsKey(dependency)) throw new InvalidOperationException($"System pack '{system.Id}' has unknown dependency '{dependency}'.");
        foreach (var genre in GenrePacks)
            foreach (var systemId in genre.SystemPackIds)
                if (!SystemsById.ContainsKey(systemId)) throw new InvalidOperationException($"Genre pack '{genre.Id}' references unknown system '{systemId}'.");
    }

    private static GameGenreSelection BuildSelection(GameGenrePack primary, IReadOnlyList<GameGenrePack> secondaries,
        string normalizedPrompt, double confidence, bool fallback, IReadOnlyList<string> rationale)
    {
        var requested = new HashSet<string>(StringComparer.Ordinal)
        {
            "core_runtime", "input_actions", "ui_hud", "audio_feedback", "save_progress", "accessibility"
        };
        AddAll(requested, primary.SystemPackIds);
        foreach (var secondary in secondaries) AddAll(requested, secondary.SystemPackIds);
        foreach (var signal in FeatureSignals)
            if (signal.Value.Any(keyword => ContainsPhrase(normalizedPrompt, keyword))) requested.Add(signal.Key);

        var resolved = ResolveDependencies(requested);
        var validations = resolved.SelectMany(id => SystemsById[id].ValidationGates).Distinct(StringComparer.Ordinal).ToArray();
        var certification = fallback || secondaries.Any(x => x.Certification == GenreCertification.Prototype)
            ? GenreCertification.Prototype
            : primary.Certification;
        return new(primary.Id, secondaries.Select(x => x.Id).ToArray(), primary.Dimension, primary.Perspective,
            certification, resolved, validations, confidence, fallback, rationale);
    }

    private static IReadOnlyList<string> ResolveDependencies(IEnumerable<string> requested)
    {
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        void Add(string id)
        {
            if (!SystemsById.TryGetValue(id, out var pack)) throw new InvalidOperationException($"Unknown system pack '{id}'.");
            if (!resolved.Add(id)) return;
            foreach (var dependency in pack.Dependencies) Add(dependency);
        }
        foreach (var id in requested.Order(StringComparer.Ordinal)) Add(id);
        return SystemPacks.Where(x => resolved.Contains(x.Id)).Select(x => x.Id).ToArray();
    }

    private static int Score(GameGenrePack genre, string normalizedPrompt) =>
        genre.Keywords.Where(x => ContainsPhrase(normalizedPrompt, x.Key)).Sum(x => x.Value);

    private static string Normalize(string value) => " " + NonWordRegex().Replace(value.ToLowerInvariant(), " ").Trim() + " ";
    private static bool ContainsPhrase(string normalized, string phrase) => normalized.Contains(" " + Normalize(phrase).Trim() + " ", StringComparison.Ordinal);
    private static void AddAll(HashSet<string> target, IEnumerable<string> values) { foreach (var value in values) target.Add(value); }

    private static void EnsureUnique(IEnumerable<string> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
            if (!seen.Add(value)) throw new InvalidOperationException($"Duplicate {label} id '{value}'.");
    }

    private static GameSystemPack Pack(string id, string displayName, IReadOnlyList<string> dependencies, params string[] validations) =>
        new(id, displayName, dependencies, validations);
    private static GameGenrePack Genre(string id, string displayName, GameDimension dimension, string perspective,
        GenreCertification certification, IReadOnlyDictionary<string, int> keywords, params string[] systems) =>
        new(id, displayName, dimension, perspective, certification, keywords, systems);
    private static IReadOnlyDictionary<string, int> Keywords(params (string Text, int Weight)[] values) =>
        values.ToDictionary(x => x.Text, x => x.Weight, StringComparer.Ordinal);

    private sealed record ScoredGenre(GameGenrePack Genre, int Score);

    private static int DeterministicLevelSeed(GameGenreSelection selection, int index)
    {
        var material = selection.PrimaryGenreId + "|" + string.Join(',', selection.SecondaryGenreIds) + "|" + string.Join(',', selection.SystemPackIds) + "|" + index;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return (int)(BitConverter.ToUInt32(hash, 0) % 999999) + 1;
    }

    [GeneratedRegex("[^a-z0-9+]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordRegex();
    [GeneratedRegex(@"\b(?<count>[1-9][0-9]?)\s+(?:playable\s+)?levels?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LevelCountRegex();
}
