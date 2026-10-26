using System.Globalization;
using System.Text.RegularExpressions;

namespace ForgeCore.Core;

/// <summary>A persisted production pass; completing its build gates does not certify the full art/system brief.</summary>
public sealed record GameProductionPlan(
    GameBlueprint Blueprint, string ProjectPath, string FirstScenePath, string OutputPath,
    WorkQueueDefinition Workflow, IReadOnlyList<string> OutstandingRequirements)
{
    public const string QueuePrefix = "Game production: ";

    public static bool IsCreationRequest(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length > 100_000) throw new ArgumentException("Game prompt exceeds 100000 characters.");
        // Questions, reviews, and discussions must not launch Unity simply because they mention a game.
        var text = message.Trim();
        var match = Regex.Match(text,
            @"^(?:(?:please\s+)|(?:(?:can|could|would)\s+you\s+)|(?:i\s+(?:want|need)\s+you\s+to\s+))*(?:create|make|build|generate|develop)\b(?<brief>[^\r\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        var brief = match.Groups["brief"].Value;
        var target = Regex.Match(brief, @"\b(?:game|prototype|playable\s+demo|campaign)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!target.Success || Regex.IsMatch(brief, @"\bgame[- ]ready\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        return !Regex.IsMatch(brief[..target.Index],
            @"\b(?:asset|model|texture|material|shader|script|file|menu|map|scene|character|prop)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static GameProductionPlan Create(string prompt, string projectPath, Guid runId)
    {
        if (runId == Guid.Empty) throw new ArgumentException("A production pass requires a unique run id.");
        var project = projectPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(project) || project.StartsWith('/') || project.Contains(':') ||
            project.Split('/').Any(x => x is "" or "." or ".." || x.Equals(".forgecore", StringComparison.OrdinalIgnoreCase)) || project.Any(char.IsControl))
            throw new ArgumentException("Production requires the configured relative Unity project path.");
        var blueprint = GameBlueprint.FromPrompt(prompt); blueprint.Validate();
        if (!UnityGameTemplateSpec.SupportedTemplates.Contains(blueprint.PrimaryGenreId) || blueprint.SecondaryGenreIds.Count > 0)
            throw new NotSupportedException($"The requested genre '{blueprint.PrimaryGenreId}'" +
                (blueprint.SecondaryGenreIds.Count > 0 ? " and hybrid systems" : "") +
                " needs a gameplay builder before ForgeCore can execute its production workflow. The original brief has not been substituted with another genre.");

        var name = "Game-" + runId.ToString("N");
        // The reviewed campaign builder derives consecutive seeds from its approved base seed.
        var baseSeed = blueprint.Campaign.Levels[0].Seed;
        blueprint = blueprint with { Campaign = blueprint.Campaign with
        {
            Levels = blueprint.Campaign.Levels.Select((level, index) => level with { Seed = ((baseSeed - 1 + index) % 999999) + 1 }).ToArray()
        } };
        blueprint.Validate();
        var firstScene = $"Assets/Scenes/Productions/{name}-Level01.unity";
        var output = $"Builds/{name}/{name}.exe";
        var actions = new List<ToolCall>
        {
            ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Check the configured Unity project's Editor version.",
                ("path", project + "/ProjectSettings/ProjectVersion.txt"))
        };
        foreach (var slot in blueprint.ArtSet!.Slots)
        {
            var role = GameArtSetPlanner.RoleId(slot.Role);
            actions.Add(ToolCall.Create(ToolKind.SearchAssetVault, PermissionKind.Read,
                "Discover reviewed reusable assets for " + role + ".",
                ("query", role), ("asset_role", role), ("style_family", blueprint.ArtSet.StyleFamily), ("max_results", "8")));
        }
        actions.Add(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "Install or safely upgrade the reviewed Unity bridge.", ("project_path", project)));
        actions.Add(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "Install or safely upgrade the gameplay, UI, map, materials, and feedback kit.",
            ("project_path", project), ("template_id", blueprint.PrimaryGenreId)));
        Editor("compile", "Compile the production runtime and Unity bridge.");
        Editor("create_game_campaign", "Build the linked campaign with maps, objectives, runtime UI, procedural assets, and feedback.",
            ("scene_path", firstScene), ("template_id", blueprint.PrimaryGenreId),
            ("seed", blueprint.Campaign.Levels[0].Seed.ToString(CultureInfo.InvariantCulture)),
            ("level_count", blueprint.Campaign.Levels.Count.ToString(CultureInfo.InvariantCulture)));
        Editor("run_tests", "Verify serialized campaign and gameplay contracts.", ("test_mode", "edit"));
        Editor("run_tests", "Exercise runtime input, combat, menus, and progression.", ("test_mode", "play"));
        for (var index = 1; index <= blueprint.Campaign.Levels.Count; index++)
            Editor("verify_gameplay", $"Verify movement, objectives, and completion in level {index}.",
                ("scene_path", $"Assets/Scenes/Productions/{name}-Level{index:00}.unity"), ("duration_seconds", "5"));
        Editor("capture_scene_preview", "Capture the campaign's rendered opening view for review.",
            ("scene_path", firstScene), ("output_path", $"Previews/Productions/{name}.png"), ("duration_seconds", "5"));
        Editor("build_windows", "Build the exact ordered campaign as a Windows development executable.",
            ("output_path", output), ("development_build", "true"), ("scene_path", firstScene),
            ("level_count", blueprint.Campaign.Levels.Count.ToString(CultureInfo.InvariantCulture)));
        actions.Add(ToolCall.Create(ToolKind.UnityPlayerSmokeTest, PermissionKind.Execute, "Test startup of this exact campaign executable.",
            ("project_path", project), ("output_path", output), ("duration_seconds", "10")));
        actions.Add(ToolCall.Create(ToolKind.UnityPlayerProfileTest, PermissionKind.Execute, "Exercise and measure rendered gameplay in this exact campaign build.",
            ("project_path", project), ("output_path", output), ("duration_seconds", "10")));
        var steps = actions.Select((call, index) => new WorkQueueStepDefinition(call, index == 0 ? [] : [actions[index - 1].Id])).ToArray();
        var workflow = new WorkQueueDefinition(QueuePrefix + blueprint.PrimaryGenreId + " / " + name, steps);
        workflow.Validate();
        var pending = new List<string>
        {
            "Coherent production art, character animation, audio/VFX, and asset placement still need asset-specific implementation and evidence; vault discovery alone does not satisfy those requirements.",
            "Visual quality, controller feel, combat balance, accessibility/settings, and release packaging still require their own acceptance."
        };
        var implemented = new HashSet<string>(["core_runtime", "input_actions", "ui_hud", "audio_feedback", "save_progress",
            "locomotion_3d", "locomotion_2d", "camera_first_person", "camera_third_person", "camera_top_down", "interaction", "objectives",
            "health_damage", "combat_ranged", "combat_melee", "enemy_ai", "platforming", "collectibles", "vehicle_physics", "race_rules"], StringComparer.Ordinal);
        var unfinished = blueprint.SystemPackIds.Where(x => !implemented.Contains(x)).ToArray();
        if (unfinished.Length > 0) pending.Add("Requested systems without complete runtime acceptance: " + string.Join(", ", unfinished) + ".");
        return new(blueprint, project, firstScene, output, workflow, pending);

        void Editor(string operation, string description, params (string Key, string Value)[] args) =>
            actions.Add(ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, description,
                [("project_path", project), ("operation", operation), .. args]));
    }

    public static string Describe(WorkQueueSnapshot snapshot, GameBlueprint blueprint)
    {
        var passed = snapshot.Steps.Count(x => x.State == WorkQueueStepState.Succeeded && x.Result?.Success == true);
        var build = snapshot.Steps.FirstOrDefault(x => x.Call.Kind == ToolKind.UnityEditorAction && x.Call.Arguments.GetValueOrDefault("operation") == "build_windows");
        var output = build?.Call.Arguments.GetValueOrDefault("output_path") ?? "not yet available";
        var current = snapshot.Steps.FirstOrDefault(x => x.State is WorkQueueStepState.Failed or WorkQueueStepState.RequiresReview or WorkQueueStepState.AwaitingApproval or WorkQueueStepState.Executing or WorkQueueStepState.Pending);
        var state = snapshot.State == WorkQueueState.Completed
            ? "Playable campaign build checks finished; full game production remains unfinished."
            : $"Game production is {snapshot.State}; {passed}/{snapshot.Steps.Count} steps passed.";
        return state + $"\nGenre: {blueprint.PrimaryGenreId}. Levels: {blueprint.Campaign.Levels.Count}. Build: {output}." +
            (current is null ? "" : "\nCurrent stage: " + current.Call.Description + (current.Result?.Error is { } error ? " " + error : "")) +
            "\nRequired systems: " + string.Join(", ", blueprint.SystemPackIds) + ". Each retains its blueprint validation gates." +
            "\nProduction assets: " + string.Join(", ", blueprint.ArtSet!.Slots.Select(x => GameArtSetPlanner.RoleId(x.Role) + " x" + x.MinimumAssetCount)) +
            ". These are required art slots; catalogue searches and procedural stand-ins do not count as accepted production assets." +
            "\nThe brief's additional systems and art/animation, visual quality, play-feel, accessibility/settings, and release gates remain open until separately implemented and verified.";
    }
}
