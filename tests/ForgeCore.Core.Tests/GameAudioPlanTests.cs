using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class GameAudioPlanTests
{
    [Fact]public void EveryRegisteredGenreHasARequirementsPlanWithoutFabricatedRuntimeAcceptance()
    {
        foreach(var genre in GameGenreRegistry.GenrePacks){var gates=GameGenreRegistry.SystemPacks.Where(p=>genre.SystemPackIds.Contains(p.Id)).SelectMany(p=>p.ValidationGates).Distinct().ToArray();
            var selection=new GameGenreSelection(genre.Id,[],genre.Dimension,genre.Perspective,genre.Certification,genre.SystemPackIds,gates,1,false,[]);
            var plan=GameAudioPlanner.Create(GameBlueprint.FromSelection(selection));Assert.Equal(genre.Id,plan.GenreId);Assert.True(plan.RequirementsOnly);Assert.NotEmpty(plan.OpenGates);
            Assert.Equal(plan.Cues.Count,plan.Cues.Select(c=>c.Id).Distinct().Count());}
    }
    [Theory][InlineData("Create a racing game","vehicle_engine")][InlineData("Create a rhythm game","synchronized_music_transport")]
    [InlineData("Create a strategy game","unit_order_confirmation")][InlineData("Create a puzzle game","puzzle_state_change")]
    [InlineData("Create a first person shooter game","weapon_shot")]
    public void DifferentGenresKeepDifferentAudioRequirements(string prompt,string expected)
    {
        var blueprint=GameBlueprint.FromPrompt(prompt);var audio=GameAudioPlanner.Create(blueprint);Assert.Contains(audio.Cues,c=>c.Id==expected);
        if(blueprint.PrimaryGenreId!="first_person_action")Assert.DoesNotContain(audio.Cues,c=>c.Id=="weapon_shot");
    }
    [Fact]public void GenericProductionExposesItsAudioGapRatherThanClaimingTheDemoRecipeCompletesAllGenres()
    {
        var production=GameProductionPlan.Create("Create a racing game with 1 level", "Games/ForgeCore",Guid.NewGuid());
        Assert.True(production.AudioPlan.RequirementsOnly);Assert.Contains(production.OutstandingRequirements,s=>s.Contains("vehicle_engine"));
        Assert.Throws<NotSupportedException>(()=>GameProductionPlan.Create("Create a strategy game with 1 level","Games/ForgeCore",Guid.NewGuid()));
    }
}
