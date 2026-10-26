using ForgeCore.Core;

namespace ForgeCore.Core.Tests;

public sealed class GameProductionPlanTests
{
    [Theory]
    [InlineData("Create a first person shooter game with 12 levels", "first_person_action")]
    [InlineData("Make a platformer game with 12 levels", "platformer")]
    [InlineData("Build a top down RPG game with 12 levels", "top_down_rpg")]
    [InlineData("Generate a racing game with 12 levels", "racing")]
    public void ProductionPreservesAllLevelsAndEvidenceGatesInBoundedPersistentPlan(string prompt, string genre)
    {
        var plan = GameProductionPlan.Create(prompt, "Games/ForgeCore", Guid.NewGuid());
        plan.Workflow.Validate(); plan.Blueprint.Validate();
        Assert.Equal(genre, plan.Blueprint.PrimaryGenreId);
        var calls = plan.Workflow.Steps.Select(x => x.Call).ToArray();
        Assert.InRange(calls.Length, 20, WorkQueueDefinition.MaximumSteps);
        Assert.Equal(12, calls.Count(x => x.Arguments.GetValueOrDefault("operation") == "verify_gameplay"));
        Assert.Equal(plan.Blueprint.ArtSet!.Slots.Count, calls.Count(x => x.Kind == ToolKind.SearchAssetVault));
        foreach (var call in calls.Where(x => x.Kind == ToolKind.SearchAssetVault))
        {
            var spec = AssetVaultSearchSpec.From(call);
            Assert.Equal(spec.AssetRole, spec.Query); Assert.Equal(plan.Blueprint.ArtSet.StyleFamily, spec.StyleFamily);
        }
        var create = calls.Single(x => x.Arguments.GetValueOrDefault("operation") == "create_game_campaign");
        var build = calls.Single(x => x.Arguments.GetValueOrDefault("operation") == "build_windows");
        Assert.Equal("12", create.Arguments["level_count"]); Assert.Equal("12", build.Arguments["level_count"]);
        Assert.Equal(UnityEditorActionSpec.From(create).GetCampaignScenePaths(), UnityEditorActionSpec.From(build).GetCampaignScenePaths());
        var seed = int.Parse(create.Arguments["seed"]);
        Assert.All(plan.Blueprint.Campaign.Levels, level => Assert.Equal(((seed - 1 + level.Index - 1) % 999999) + 1, level.Seed));
        Assert.Equal(ToolKind.UnityPlayerSmokeTest, calls[^2].Kind);
        Assert.Equal(ToolKind.UnityPlayerProfileTest, calls[^1].Kind);
        Assert.Equal(plan.OutputPath, calls[^1].Arguments["output_path"]);
        for (var i = 1; i < plan.Workflow.Steps.Count; i++)
            Assert.Equal(plan.Workflow.Steps[i - 1].Call.Id, Assert.Single(plan.Workflow.Steps[i].DependsOn));
        Assert.NotEmpty(plan.OutstandingRequirements);
    }

    [Theory]
    [InlineData("Create a first person game", true)]
    [InlineData("Can you make me a racing game?", true)]
    [InlineData("I want you to build a platformer game", true)]
    [InlineData("What can ForgeCore do to create a game?", false)]
    [InlineData("Explain how to build a game", false)]
    [InlineData("Create a text file named gameplay.txt", false)]
    [InlineData("Generate a game-ready GLB watchtower asset", false)]
    [InlineData("Create a menu for my racing game", false)]
    [InlineData("Create a game with a map, assets and menus", true)]
    public void ProductionOnlyStartsForExplicitGameCreation(string prompt, bool expected) =>
        Assert.Equal(expected, GameProductionPlan.IsCreationRequest(prompt));

    [Fact] public void ProductionNeverSubstitutesAnotherGenreOrReusesOutputPaths()
    {
        Assert.Throws<NotSupportedException>(() => GameProductionPlan.Create("Create a strategy game", "Games/ForgeCore", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => GameProductionPlan.Create("Create a racing game", "../Game", Guid.NewGuid()));
        var first = GameProductionPlan.Create("Create a racing game", "Games/ForgeCore", Guid.NewGuid());
        var second = GameProductionPlan.Create("Create a racing game", "Games/ForgeCore", Guid.NewGuid());
        Assert.NotEqual(first.OutputPath, second.OutputPath); Assert.NotEqual(first.FirstScenePath, second.FirstScenePath);
    }

    [Fact] public void ProductionReportsUnacceptedAssetsAfterBuildChecksAndPreservesAdditionalSystems()
    {
        var plan = GameProductionPlan.Create("Create a multiplayer FPS game with inventory", "Games/ForgeCore", Guid.NewGuid());
        Assert.Contains(plan.OutstandingRequirements, x => x.Contains("networking") && x.Contains("inventory"));
        var steps = plan.Workflow.Steps.Select((step, i) => new WorkQueueStep(step.Call.Id, Guid.Empty, i, step.Call,
            step.DependsOn, WorkQueueStepState.Succeeded, new(step.Call.Id, true, "passed"), "test", DateTimeOffset.UtcNow)).ToArray();
        var snapshot = new WorkQueueSnapshot(Guid.NewGuid(), Guid.NewGuid(), plan.Workflow.Name, WorkQueueState.Completed,
            steps, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var report = GameProductionPlan.Describe(snapshot, plan.Blueprint);
        Assert.Contains("full game production remains unfinished", report);
        Assert.Contains("required art slots", report);
        Assert.Contains(plan.OutputPath, report);
    }
}
