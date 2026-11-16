using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class ExperienceGuidedModelTests
{
    private const string Prompt = "Create an experience-guided wooden crate in Blender. Name it next-crate.";
    private static string Hash(char c) => new(c, 64);
    private sealed class NoInference : ILocalModel
    {
        public ModelStatus Status => new("offline", true, "no inference");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) => throw new Exception("Unexpected provider fallback");
    }
    private static ToolObservation Observe(ToolCall c, string data) => new(c.Id, c.Kind, c.Description, c.Arguments, "Succeeded", true, data);
    private static string Search(string visual = "pending", bool empty = false, bool truncated = false) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, library = "local_blender_candidates", truncated, rejectedEntries = 0,
        results = empty ? Array.Empty<object>() : [new
        {
            bundleSha256 = Hash('a'), assetSha256 = Hash('b'), recipe = "wooden_crate_v2", heightMillimetres = 800,
            reviewState = "unreviewed_local_recipe", placementApproved = false, sourceVisualReview = visual, sourceRightsReview = "pending"
        }]
    });
    private static string Inspection(string visual = "pending", bool approval = false) => JsonSerializer.Serialize(new
    {
        identity = new { bundleSha256 = Hash('a'), assetSha256 = Hash('b') }, recipe = "wooden_crate_v2",
        learningPlan = new { policyVersion = AssetImprovementModel.Version, bundleSha256 = Hash('a'), grantsApproval = approval,
            neuralWeightsTrained = false, sourceVisualReview = visual, sourceRightsReview = "pending" }
    });

    [Fact] public async Task GuidedCreationConsultsHistoryAndProposesExactReuseWithoutNewGeneration()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var find = Assert.Single((await model.CreatePlanAsync(new(Prompt, []), default)).Actions);
        Assert.Equal(ToolKind.SearchLocalBlenderAssets, find.Kind);
        var observations = new List<ToolObservation> { Observe(find, Search()) };
        var inspect = Assert.Single((await model.CreatePlanAsync(new(Prompt, [], observations, 2, 10), default)).Actions);
        Assert.Equal(ToolKind.InspectLocalBlenderAsset, inspect.Kind); observations.Add(Observe(inspect, Inspection()));
        var import = Assert.Single((await model.CreatePlanAsync(new(Prompt, [], observations, 3, 9), default)).Actions);
        Assert.Equal(ToolKind.ImportBlenderAsset, import.Kind);
        var spec = BlenderAssetImportSpec.From(import); Assert.Equal(Hash('a'), spec.BundleSha256); Assert.Equal("next-crate", spec.AssetName);
        Assert.DoesNotContain(observations, o => o.Kind == ToolKind.CreateBlenderAsset);
    }

    [Theory]
    [InlineData("search_rejected")][InlineData("new_rejection")][InlineData("forged_approval")][InlineData("malformed")][InlineData("partial_empty")]
    public async Task GuidedCreationCannotRegenerateRejectedModelsOrTreatMemoryAsAuthority(string defect)
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var find = Assert.Single((await model.CreatePlanAsync(new(Prompt, []), default)).Actions);
        var data = defect == "malformed" ? "{}" : Search(defect == "search_rejected" ? "rejected" : "pending", defect == "partial_empty", defect == "partial_empty");
        var observations = new List<ToolObservation> { Observe(find, data) };
        var result = await model.CreatePlanAsync(new(Prompt, [], observations, 2, 10), default);
        if (result.Actions.Count != 0)
        {
            var inspect = Assert.Single(result.Actions); Assert.Equal(ToolKind.InspectLocalBlenderAsset, inspect.Kind);
            observations.Add(Observe(inspect, Inspection(defect == "new_rejection" ? "rejected" : "pending", defect == "forged_approval")));
            result = await model.CreatePlanAsync(new(Prompt, [], observations, 3, 9), default);
        }
        Assert.Empty(result.Actions);
    }

    [Fact] public async Task NoHistoryUsesExistingBoundedRecipeAndStillRequiresExecuteApproval()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var find = Assert.Single((await model.CreatePlanAsync(new(Prompt, []), default)).Actions);
        var call = Assert.Single((await model.CreatePlanAsync(new(Prompt, [], [Observe(find, Search(empty: true))], 2, 10), default)).Actions);
        Assert.Equal(ToolKind.CreateBlenderAsset, call.Kind); Assert.Equal(PermissionKind.Execute, call.RequiredPermission);
        Assert.Equal("wooden_crate_v2", BlenderAssetSpec.From(call).Recipe);
    }

    [Fact] public async Task CurrentHumanReviewsGuidePreferenceWithoutBecomingTargetApproval()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var find = Assert.Single((await model.CreatePlanAsync(new(Prompt, []), default)).Actions);
        var node = JsonNode.Parse(Search())!; var reviewed = node["results"]![0]!.DeepClone();
        reviewed["bundleSha256"] = Hash('c'); reviewed["sourceVisualReview"] = "approved"; reviewed["sourceRightsReview"] = "approved";
        node["results"]!.AsArray().Add(reviewed);
        var inspection = Assert.Single((await model.CreatePlanAsync(new(Prompt, [], [Observe(find, node.ToJsonString())], 2, 10), default)).Actions);
        Assert.Equal(ToolKind.InspectLocalBlenderAsset, inspection.Kind);
        Assert.Equal(Hash('c'), inspection.Arguments["bundle_sha256"]);
        Assert.DoesNotContain(inspection.Kind, new[] { ToolKind.ReviewGameArtAsset, ToolKind.ImportBlenderAsset });
    }
}
