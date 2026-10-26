using ForgeCore.Core;

namespace ForgeCore.Core.Tests;

public sealed class GameGenreRegistryTests
{
    [Fact]
    public void Catalog_IsInternallyConsistentAndDependencyOrdered()
    {
        GameGenreRegistry.ValidateCatalog();

        Assert.True(GameGenreRegistry.GenrePacks.Count >= 12);
        Assert.True(GameGenreRegistry.SystemPacks.Count >= 30);
        Assert.Equal(GameGenreRegistry.GenrePacks.Count, GameGenreRegistry.GenrePacks.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(GameGenreRegistry.SystemPacks.Count, GameGenreRegistry.SystemPacks.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());

        var selection = GameGenreRegistry.Analyze("Create a third person action adventure game with melee combat.");
        foreach (var packId in selection.SystemPackIds)
        {
            var pack = GameGenreRegistry.SystemPacks.Single(x => x.Id == packId);
            foreach (var dependency in pack.Dependencies)
                Assert.True(selection.SystemPackIds.IndexOf(dependency) < selection.SystemPackIds.IndexOf(packId),
                    $"Dependency {dependency} must precede {packId}.");
        }
    }

    [Theory]
    [InlineData("Build a fast kart racing game with three laps.", "racing", "vehicle_physics")]
    [InlineData("Create an isometric action RPG dungeon crawler.", "top_down_rpg", "dialogue_quests")]
    [InlineData("Make a real-time strategy game with squads and resources.", "strategy", "strategy_units")]
    [InlineData("Create a side-scrolling platformer game with checkpoints.", "platformer", "platforming")]
    [InlineData("Build a first person shooter game with hostile robots.", "first_person_action", "combat_ranged")]
    [InlineData("Make a music rhythm game driven by the beat.", "rhythm", "rhythm_timing")]
    public void Analyze_ClassifiesDistinctGenreFamilies(string prompt, string expectedGenre, string expectedSystem)
    {
        var selection = GameGenreRegistry.Analyze(prompt);

        Assert.Equal(expectedGenre, selection.PrimaryGenreId);
        Assert.Contains(expectedSystem, selection.SystemPackIds);
        Assert.False(selection.UsesFallback);
        Assert.InRange(selection.Confidence, 0.35, 0.99);
        Assert.NotEmpty(selection.ValidationGates);
    }

    [Fact]
    public void Analyze_ComposesHybridAndExplicitFeaturePacks()
    {
        var selection = GameGenreRegistry.Analyze(
            "Create a top down survival crafting RPG game with quests, inventory, stealth, procedural maps, controller support and online co-op.");

        Assert.Equal("survival_crafting", selection.PrimaryGenreId);
        Assert.Contains("top_down_rpg", selection.SecondaryGenreIds);
        Assert.Contains("crafting", selection.SystemPackIds);
        Assert.Contains("dialogue_quests", selection.SystemPackIds);
        Assert.Contains("stealth", selection.SystemPackIds);
        Assert.Contains("procedural_generation", selection.SystemPackIds);
        Assert.Contains("controller_support", selection.SystemPackIds);
        Assert.Contains("networking", selection.SystemPackIds);
        Assert.Equal(GenreCertification.Prototype, selection.Certification);
    }

    [Fact]
    public void Analyze_UsesSafeFallbackForUnfamiliarGenre()
    {
        var selection = GameGenreRegistry.Analyze("Create a playable game about arranging magical clouds by scent.");

        Assert.Equal("sandbox_prototype", selection.PrimaryGenreId);
        Assert.True(selection.UsesFallback);
        Assert.Contains("interaction", selection.SystemPackIds);
        Assert.Contains("procedural_generation", selection.SystemPackIds);
        Assert.Contains(selection.Rationale, x => x.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanningBrief_IsBoundedAdvisoryContextOnlyForGameRequests()
    {
        var gameBrief = GameGenreRegistry.CreatePlanningBrief("Create a realistic third person adventure game with melee combat.");
        var ordinaryBrief = GameGenreRegistry.CreatePlanningBrief("Replace the greeting in notes.txt.");

        Assert.Contains("third_person_action", gameBrief, StringComparison.Ordinal);
        Assert.Contains("combat_melee", gameBrief, StringComparison.Ordinal);
        Assert.Contains("realistic_pbr", gameBrief, StringComparison.Ordinal);
        Assert.Contains("coherent art-set slots", gameBrief, StringComparison.Ordinal);
        Assert.Contains("grants no tool permissions", gameBrief, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("(not a game-generation request)", ordinaryBrief);
        Assert.DoesNotContain("third person adventure", gameBrief, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class GenreTestListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
            if (string.Equals(values[i], value, StringComparison.Ordinal)) return i;
        return -1;
    }
}
