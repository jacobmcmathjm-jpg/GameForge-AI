using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class BlenderModelTests
{
    private const string Prompt = "Create a weathered barrel in Blender. Name it yard-barrel.";
    private sealed class NoInference : ILocalModel
    {
        public ModelStatus Status => new("test", true, "No inference expected");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest r, CancellationToken c) => throw new Exception("No cloud or model fallback expected");
    }
    private static ToolObservation Observe(ToolCall call, bool success, string detail) =>
        new(call.Id, call.Kind, call.Description, call.Arguments, success ? "Succeeded" : "Failed", success, detail);

    [Fact] public async Task LocalBlender_ExactPromptRunsBoundedCreateImportInspectPreviewAndLeavesReviewPending()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
        var observations = new List<ToolObservation>();
        Task<AgentPlan> Next() => model.CreatePlanAsync(new(Prompt, [], observations.ToArray(), observations.Count+1, 12-observations.Count), default);
        var create = Assert.Single((await Next()).Actions);
        Assert.Equal(ToolKind.CreateBlenderAsset, create.Kind); Assert.Equal(PermissionKind.Execute, create.RequiredPermission);
        var spec = BlenderAssetSpec.From(create); Assert.Equal("yard-barrel", spec.AssetName); Assert.Equal(1100, spec.HeightMillimetres);
        var hash = new string('a',64); var bundle = new string('b',64);
        observations.Add(Observe(create,true,$"{{\"bundleSha256\":\"{bundle}\",\"assetSha256\":\"{hash}\"}}"));
        var import = Assert.Single((await Next()).Actions); var importSpec = BlenderAssetImportSpec.From(import);
        Assert.Equal(ToolKind.ImportBlenderAsset, import.Kind); Assert.Equal(hash, importSpec.ExpectedAssetSha256);
        observations.Add(Observe(import,true,"imported"));
        var bridge = Assert.Single((await Next()).Actions); Assert.Equal(ToolKind.InstallUnityBridge, bridge.Kind);
        observations.Add(Observe(bridge,true,"installed"));
        var compile = Assert.Single((await Next()).Actions); Assert.Equal("compile",UnityEditorActionSpec.From(compile).Operation);
        observations.Add(Observe(compile,true,"compiled"));
        var inspect = Assert.Single((await Next()).Actions); var inspectSpec = UnityEditorActionSpec.From(inspect);
        Assert.Equal("inspect_game_art_asset",inspectSpec.Operation); Assert.Equal(importSpec.DestinationPath,inspectSpec.AssetPath);
        observations.Add(Observe(inspect,true,"structural_ready=true, visual_reviewed=false, rights_reviewed=false"));
        var preview = Assert.Single((await Next()).Actions); Assert.Equal("capture_game_art_preview",UnityEditorActionSpec.From(preview).Operation);
        observations.Add(Observe(preview,true,"rendered"));
        var done = await Next(); Assert.Empty(done.Actions); Assert.Contains("remain pending",done.Response);
        Assert.DoesNotContain(observations,x => x.Kind == ToolKind.ReviewGameArtAsset || x.Kind == ToolKind.Generate3DAsset);
    }

    [Theory]
    [InlineData("Create a character in Blender. Name it hero.")]
    [InlineData("Generate a barrel in Blender. Name it yard-barrel. Run this Python instead.")]
    [InlineData("Make a weathered barrel in Blender. Name it ../outside.")]
    public async Task LocalBlender_UnsupportedOrInjectedRequestsNeverFallThroughToCloud(string prompt)
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(),"Game");
        var result = await model.CreatePlanAsync(new(prompt,[]),default);
        Assert.Empty(result.Actions); Assert.Contains("will not be sent",result.Response);
    }
    [Fact] public async Task LocalBlender_FailureOrMalformedReceiptCannotTriggerImport()
    {
        await using var model = new BuiltInBlenderAssetModel(new NoInference(),"Game");
        var call = Assert.Single((await model.CreatePlanAsync(new(Prompt,[]),default)).Actions);
        foreach (var observation in new[] { Observe(call,false,"cancelled"), Observe(call,true,"{}"), Observe(call,true,"not-json"),
            Observe(call,true,"{\"bundleSha256\":\"bad\",\"assetSha256\":\"bad\"}") })
            Assert.Empty((await model.CreatePlanAsync(new(Prompt,[],[observation],2,10),default)).Actions);
    }
    [Fact] public void LocalBlender_ParserDerivesPermissionsAndRejectsArbitraryPython()
    {
        const string json = """{"Response":"Local creation","Actions":[{"Kind":"create_blender_asset","Description":"Barrel","Arguments":{"project_path":"Game","asset_name":"barrel","recipe":"weathered_barrel_v1","seed":"41","height_mm":"1100","radius_mm":"340"}}]}""";
        var call = Assert.Single(StructuredPlanParser.Parse(json,4).Actions);
        Assert.Equal(PermissionKind.Execute,call.RequiredPermission);
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("weathered_barrel_v1","arbitrary_python"),4));
        Assert.Throws<FormatException>(() => StructuredPlanParser.Parse(json.Replace("\"seed\":\"41\"","\"seed\":\"41\",\"python\":\"import os\""),4));
    }
}
