using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;

public sealed class CourtyardGraphicsTests
{
    private const string Valid="""
    {"schemaVersion":1,"profile":"industrial_courtyard_detail_v2","source":"original_bounded_procedural_template","renderScope":"static_editor_comparison_not_runtime_animation",
    "textureSize":1024,"textureCount":9,"viewCount":3,"comparisonWidth":1920,"comparisonHeight":1620,"collisionUnchanged":true,"decorativeColliderCount":0,
    "addedRenderers":3,"addedTriangles":12000,"ownedTextureMemoryBytes":50331648,"magentaPixels":0,"minimumLuminanceRange":0.6,
    "visualReviewed":false,"rightsReviewed":false,"stageComplete":false,"approvedLibraryAssetsPlaced":false}
    """;
    [Fact] public void TechnicalEvidenceDoesNotGrantArtAcceptance(){using var json=JsonDocument.Parse(Valid);CourtyardGraphicsEvidence.Validate(json.RootElement);}
    [Theory]
    [InlineData("schemaVersion","2")][InlineData("profile","\"anything\"")][InlineData("source","\"reviewed_library\"")]
    [InlineData("renderScope","\"runtime_accepted\"")][InlineData("textureSize","512")][InlineData("textureCount","8")]
    [InlineData("viewCount","2")][InlineData("comparisonWidth","960")][InlineData("comparisonHeight","1080")]
    [InlineData("collisionUnchanged","false")][InlineData("decorativeColliderCount","1")][InlineData("addedRenderers","7")][InlineData("addedRenderers","0")]
    [InlineData("addedTriangles","25001")][InlineData("addedTriangles","0")][InlineData("ownedTextureMemoryBytes","67108865")][InlineData("ownedTextureMemoryBytes","0")]
    [InlineData("magentaPixels","1")][InlineData("minimumLuminanceRange","0.07")][InlineData("minimumLuminanceRange","1.1")]
    [InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")][InlineData("approvedLibraryAssetsPlaced","true")]
    public void InvalidOrOverclaimedEvidenceIsBlocked(string field,string value)
    {var obj=JsonNode.Parse(Valid)!;obj[field]=JsonNode.Parse(value);using var json=JsonDocument.Parse(obj.ToJsonString());Assert.Throws<InvalidDataException>(()=>CourtyardGraphicsEvidence.Validate(json.RootElement));}
    private sealed class NoInference:ILocalModel
    {public ModelStatus Status=>new("offline",true,"no fallback");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken t)=>throw new Exception("Unexpected inference");}
    [Theory][InlineData(-1)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)]
    public async Task CourtyardBuildsThroughExistingTypedToolsAndStopsAtEveryDeliveryFailure(int failAt)
    {
        var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");var observations=new List<ToolObservation>();
        for(var turn=0;turn<12;turn++)
        {
            var plan=await model.CreatePlanAsync(new("Create a detailed industrial courtyard named graphics-yard.",[],observations,turn,12-turn),default);
            if(plan.Actions.Count==0)
            {Assert.Equal(failAt<0?11:failAt+1,observations.Count);Assert.Contains(failAt<0?"Windows EXE":"stopped",plan.Response);return;}
            var c=Assert.Single(plan.Actions);Assert.DoesNotContain(c.Kind,new[]{ToolKind.Generate3DAsset,ToolKind.ReviewGameArtAsset});
            if(turn>=7&&turn<10)
            {
                var spec=UnityEditorActionSpec.From(c);Assert.Equal(new[]{"capture_scene_preview","profile_scene","build_windows"}[turn-7],spec.Operation);
                Assert.Equal("Assets/Scenes/CharacterAnimations/graphics-yard-bbbbbbbbbbbb.unity",spec.ScenePath);
            }
            if(turn==10){Assert.Equal(ToolKind.UnityPlayerSmokeTest,c.Kind);Assert.Equal(5,UnityPlayerSmokeTestSpec.From(c).DurationSeconds);}
            var success=turn!=failAt;var detail=c.Kind==ToolKind.CreateBlenderAsset?"{\"bundleSha256\":\""+new string('a',64)+"\",\"assetSha256\":\""+new string('b',64)+"\"}":"planner fixture only, not actual runtime evidence";
            observations.Add(new(c.Id,c.Kind,c.Description,c.Arguments,success?"Succeeded":"Failed",success,detail));
        }
        Assert.Fail("Courtyard pipeline failed to terminate within the bounded action budget.");
    }
    [Theory][InlineData("Create a detailed industrial courtyard named graphics-yard.","graphics-yard")]
    [InlineData("Make an industrial courtyard.","detailed-courtyard")][InlineData("Please generate me a detailed industrial courtyard called demo-yard!","demo-yard")]
    public async Task OrdinaryCourtyardPromptUsesExistingOfflineMissionPipeline(string prompt,string name)
    {
        Assert.False(GameProductionPlan.IsCreationRequest(prompt)); // Desktop must route this to the bounded asset/mission planner, not the separate generic game workflow.
        var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");var plan=await model.CreatePlanAsync(new(prompt,[]),default);
        var call=Assert.Single(plan.Actions);Assert.Equal(ToolKind.CreateBlenderAsset,call.Kind);Assert.Equal(name,call.Arguments["asset_name"]);
        Assert.Equal("industrial_security_v1",call.Arguments["recipe"]);Assert.Equal(PermissionKind.Execute,call.RequiredPermission);
        var failed=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Failed",false,"cancelled");
        var stopped=await model.CreatePlanAsync(new(prompt,[],[failed],2,7),default);Assert.Empty(stopped.Actions);Assert.Contains("stopped",stopped.Response);
    }
    [Theory][InlineData("Make an industrial courtyard named ../../escape.")][InlineData("Create an industrial courtyard with an entire city.")]
    public async Task UnsupportedCourtyardDoesNotFallThroughToInference(string prompt)
    {var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");Assert.Empty((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);}
}
