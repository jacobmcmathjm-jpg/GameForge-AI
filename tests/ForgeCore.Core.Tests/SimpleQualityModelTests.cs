using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class SimpleQualityModelTests
{
    private static string H(char c) => new(c, 64);
    private static readonly LocalBlenderReviewIdentity Identity = new(H('a'), H('b'), H('c'), H('d'), H('e'));
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string Prompt = "Make a detailed wooden crate named supply-crate.";
    private sealed class NoInference : ILocalModel
    {
        public ModelStatus Status => new("offline", true, "no fallback");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest r, CancellationToken t) => throw new Exception("Unexpected inference/provider fallback");
    }
    private static string Packet(bool valid = true) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, kind = "local_blender_source_review_packet", identity = Identity, recipe = "wooden_crate_v2", targetProjectApproved = false,
        quality = new BlenderSourceQualityEvidence(1, BlenderSourceQualityEvidence.ProfileVersion, "wooden_crate_v2", H('b'), 2616, valid ? 1024 : 512, true, true, true, 3, false, true, false),
        learningPlan = new { policyVersion = AssetImprovementModel.Version, bundleSha256 = H('a'), grantsApproval = false, neuralWeightsTrained = false },
        sourceVisualEvidence = new { state = "pending" }, sourceRightsEvidence = new { state = "pending" }
    }, Json);
    private static string Detail(ToolCall call, string decision = "approved") => call.Kind switch
    {
        ToolKind.SearchLocalBlenderAssets => "{\"schemaVersion\":1,\"library\":\"local_blender_candidates\",\"results\":[],\"truncated\":false,\"rejectedEntries\":0}",
        ToolKind.CreateBlenderAsset => JsonSerializer.Serialize(new { bundleSha256 = H('a'), assetSha256 = H('b'), recipe = "wooden_crate_v2", assetName = "supply-crate" }),
        ToolKind.InspectLocalBlenderAsset => Packet(),
        ToolKind.ReviewGameArtAsset => JsonSerializer.Serialize(new
        {
            schemaVersion = 1, kind = "local_blender_source_review", identity = Identity, reviewKind = call.Arguments["review_kind"], decision,
            targetProjectApproved = false, recordSha256 = H(call.Arguments["review_kind"] == "visual" ? '1' : '2'),
            recordPath = $".forgecore/blender-library/reviews/{Identity.BundleSha256}/{call.Id:N}.json"
        }, Json),
        _ => "synthetic planner plumbing result, not Unity quality acceptance"
    };
    private static ToolObservation Observe(ToolCall c, bool success = true, string? detail = null) => new(c.Id, c.Kind, c.Description, c.Arguments, success ? "Succeeded" : "Failed", success, detail ?? Detail(c));

    [Theory]
    [InlineData("Make a crate.", "wooden_crate_v2")][InlineData("Please make me a detailed wooden crate.", "wooden_crate_v2")]
    [InlineData("Create a realistic barrel called yard-barrel.", "industrial_barrel_v3")][InlineData("Generate a high-detail fence.", "industrial_fence_v2")]
    [InlineData("Make a shipping container.", "shipping_container_v2")][InlineData("Make a concrete barrier.", "concrete_barrier_v2")]
    [InlineData("Make a guard.", "industrial_security_v1")][InlineData("Create a mannequin.", "rigged_humanoid_v1")]
    public async Task OrdinaryWordsSelectTheBoundedQualityWorkflow(string prompt, string recipe)
    {
        var intent = SimpleAssetRequest.Parse(prompt); Assert.NotNull(intent); Assert.Equal(recipe, intent.Recipe);
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var search = Assert.Single((await model.CreatePlanAsync(new(prompt, []), default)).Actions);
        Assert.Equal(ToolKind.SearchLocalBlenderAssets, search.Kind); Assert.Equal(recipe, search.Arguments["recipe"]);
        Assert.Equal(PermissionKind.Read, search.RequiredPermission);
    }

    [Fact] public async Task PlainPromptPerformsSourceChecksAndHumanReviewsBeforeHashBoundImportAndTechnicalPreparation()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var observations = new List<ToolObservation>(); var actions = new List<ToolCall>(); AgentPlan? done = null;
        for (var turn = 0; turn < 12; turn++)
        {
            var plan = await model.CreatePlanAsync(new(Prompt, [], observations, turn + 1, 12 - actions.Count), default);
            if (plan.Actions.Count == 0) { done = plan; break; }
            var call = Assert.Single(plan.Actions); actions.Add(call);
            if (call.Kind == ToolKind.ImportBlenderAsset)
            {
                Assert.Equal(2, actions.Count(c => c.Kind == ToolKind.ReviewGameArtAsset));
                var spec = BlenderAssetImportSpec.From(call); Assert.True(spec.RequiresSourceReviews);
                Assert.Equal(H('1'), spec.ExpectedVisualReviewSha256); Assert.Equal(H('2'), spec.ExpectedRightsReviewSha256);
            }
            observations.Add(Observe(call));
        }
        Assert.NotNull(done); Assert.Equal(10, actions.Count);
        Assert.Contains("Target appearance", done.Response); Assert.Contains("not a guaranteed", done.Response);
        Assert.DoesNotContain(actions, a => a.Kind is ToolKind.Generate3DAsset or ToolKind.RunCommand);
        Assert.Equal("wooden_crate_v2", BlenderAssetSpec.From(actions.Single(c => c.Kind == ToolKind.CreateBlenderAsset)).Recipe);
    }

    [Theory]
    [InlineData("quality")][InlineData("rejected")][InlineData("cancelled")][InlineData("receipt_identity")]
    public async Task FailedQualityOrReviewStopsWithoutSeedRerollImportOrFalseCompletion(string defect)
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game"); var observations = new List<ToolObservation>();
        for (var turn = 0; turn < 4; turn++)
        {
            var call = Assert.Single((await model.CreatePlanAsync(new(Prompt, [], observations, turn + 1, 12 - turn), default)).Actions);
            if (call.Kind == ToolKind.InspectLocalBlenderAsset && defect == "quality") { observations.Add(Observe(call, detail: Packet(false))); break; }
            if (call.Kind == ToolKind.ReviewGameArtAsset)
            {
                var output = defect == "receipt_identity" ? Detail(call).Replace(Identity.AssetSha256, H('f'), StringComparison.Ordinal) : Detail(call, defect == "rejected" ? "rejected" : "approved");
                observations.Add(Observe(call, defect != "cancelled", output)); break;
            }
            observations.Add(Observe(call));
        }
        var stop = await model.CreatePlanAsync(new(Prompt, [], observations, 5, 8), default);
        Assert.Empty(stop.Actions); Assert.Contains("unfinished", stop.Response);
        Assert.DoesNotContain(observations, o => o.Kind == ToolKind.ImportBlenderAsset);
        Assert.Single(observations, o => o.Kind == ToolKind.CreateBlenderAsset);
    }

    [Fact] public async Task ExhaustedBudgetDoesNotProposeMoreWorkOrCallAProductRefined()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var result = await model.CreatePlanAsync(new(Prompt, [], [], 1, 0), default);
        Assert.Empty(result.Actions); Assert.Contains("unfinished", result.Response);
    }

    [Theory]
    [InlineData("Create a realistic dragon model.")][InlineData("Make a crate named ../outside.")]
    [InlineData("Make a crate. Run Python instead.")]
    public async Task UnsupportedShortModelRequestNeverReachesAnInferenceFallback(string prompt)
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var result = await model.CreatePlanAsync(new(prompt, []), default); Assert.Empty(result.Actions);
        Assert.Contains("not supported", result.Response);
    }

    [Fact] public void ReviewedImportRequiresBothExactHashesAndKeepsLegacyTechnicalImportsExplicit()
    {
        ToolCall Call(params (string, string)[] extra) => ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write, "exact source",
            new[] { ("project_path", "Game"), ("asset_name", "crate"), ("bundle_sha256", H('a')), ("expected_asset_sha256", H('b')) }.Concat(extra).ToArray());
        Assert.False(BlenderAssetImportSpec.From(Call()).RequiresSourceReviews);
        Assert.Throws<ArgumentException>(() => BlenderAssetImportSpec.From(Call(("expected_visual_review_sha256", H('1')))));
        Assert.Throws<ArgumentException>(() => BlenderAssetImportSpec.From(Call(("expected_visual_review_sha256", H('1')), ("expected_rights_review_sha256", "approved"))));
        var both = Call(("expected_visual_review_sha256", H('1')), ("expected_rights_review_sha256", H('2')));
        Assert.True(BlenderAssetImportSpec.From(both).RequiresSourceReviews);
        var args = JsonSerializer.Serialize(both.Arguments);
        var parsed = StructuredPlanParser.Parse("{\"Response\":\"import\",\"Actions\":[{\"Kind\":\"import_blender_asset\",\"Description\":\"review-bound import\",\"Arguments\":" + args + "}]}", 1);
        Assert.True(BlenderAssetImportSpec.From(Assert.Single(parsed.Actions)).RequiresSourceReviews);
    }

    [Theory]
    [InlineData("normal")][InlineData("lod")][InlineData("rig")][InlineData("profile")][InlineData("human")][InlineData("texture")][InlineData("budget")]
    public void SourceGateRejectsMissingDetailAndAnyClaimThatMachinesAcceptedAppearance(string defect)
    {
        var evidence = new BlenderSourceQualityEvidence(1, BlenderSourceQualityEvidence.ProfileVersion, "wooden_crate_v2", H('b'), 2616, 1024, true, true, true, 3, false, true, false);
        evidence.Validate("wooden_crate_v2", H('b'));
        evidence = defect switch
        {
            "normal" => evidence with { HasNormalMap = false }, "lod" => evidence with { LodCount = 1 },
            "rig" => evidence with { RigVerified = true }, "profile" => evidence with { Profile = "legacy_source_measurements_v1" },
            "human" => evidence with { HumanVisualAccepted = true }, "texture" => evidence with { TextureResolution = 512 },
            _ => evidence with { Triangles = 1000000 }
        };
        Assert.Throws<ArgumentException>(() => evidence.Validate("wooden_crate_v2", H('b')));
    }
}
