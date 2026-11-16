using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.Demo;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class ConnectedIndustrialTests
{
    private static Dictionary<string,string> Fields()=>new(){{"project_path","Game"},{"operation","create_industrial_mission"},{"asset_path","Assets/ForgeCore/Generated/character.glb"},{"expected_sha256",new string('a',64)},{"asset_set_id","connected"},{"scene_path","Assets/Scenes/Connected.unity"},{"output_path","Previews/Connected.png"},{"layout_profile",ConnectedIndustrialProfile.Id}};
    [Fact]public void ExactConnectedProfileIsTypedAndNotAnArbitraryLayout(){
        Assert.Equal(ConnectedIndustrialProfile.Id,UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,Fields(),PermissionKind.Execute,"test")).LayoutProfile);
        foreach(var bad in new[]{"","../escape","huge_world","industrial_connected_v2"}){var f=Fields();f["layout_profile"]=bad;Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,f,PermissionKind.Execute,"test")));}
        var retro=Fields();retro["style_family"]="retro_64";Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,retro,PermissionKind.Execute,"test")));
        var wrong=Fields();wrong["operation"]="compile";Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,wrong,PermissionKind.Execute,"test")));
    }
    [Theory][InlineData(-15,0)][InlineData(38,41)][InlineData(47,47)]
    public void LargerCoordinatesRequireExplicitMatchingCheckpointProfile(float x,float z){
        var c=new DemoCheckpoint{schemaVersion=1,profile=DemoStateRules.ConnectedProfile,stage=2,defeatedMask=3,health=83,reserve=40,magazine=4,x=x,y=.02f,z=z};
        Assert.True(DemoStateRules.CheckpointValid(c,DemoStateRules.ConnectedProfile));Assert.False(DemoStateRules.CheckpointValid(c));
        c.profile=DemoStateRules.Profile;Assert.False(DemoStateRules.CheckpointValid(c,DemoStateRules.ConnectedProfile));
    }
    [Theory][InlineData(-16f)][InlineData(48f)][InlineData(float.NaN)][InlineData(float.PositiveInfinity)]
    public void ConnectedSaveStillRejectsOutOfWorldOrNonfiniteLocations(float x){var c=new DemoCheckpoint{schemaVersion=1,profile=DemoStateRules.ConnectedProfile,health=100,reserve=60,magazine=6,x=x};Assert.False(DemoStateRules.CheckpointValid(c,DemoStateRules.ConnectedProfile));}
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new{schemaVersion=1,profile=ConnectedIndustrialProfile.Id,source="original_template_extension_with_final_physics_audit",gridWidth=64,gridHeight=64,zones=3,doors=2,guardCount=3,renderers=150,colliders=70,routeMetres=150,spawnSafe=true,stagedRouteReachable=true,doorGatingVerified=true,socketsUnchanged=true,baseCombatAssetsUnchanged=true,separateSaveProfile=true,stairsSupported=false,approvedLibraryPlacement=false,visualReviewed=false,rightsReviewed=false,stageComplete=false,freezeFixed=false})!.AsObject();
    [Fact]public void FinalPhysicsEvidenceIsNotHumanQualityAcceptance(){using var d=JsonDocument.Parse(Report().ToJsonString());ConnectedIndustrialProfile.Validate(d.RootElement);}
    [Theory][InlineData("gridWidth","65")][InlineData("zones","2")][InlineData("doors","1")][InlineData("guardCount","8")][InlineData("renderers","301")][InlineData("colliders","101")][InlineData("routeMetres","45")][InlineData("spawnSafe","false")][InlineData("stagedRouteReachable","false")][InlineData("doorGatingVerified","false")][InlineData("socketsUnchanged","false")][InlineData("baseCombatAssetsUnchanged","false")][InlineData("separateSaveProfile","false")][InlineData("stairsSupported","true")][InlineData("approvedLibraryPlacement","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")][InlineData("freezeFixed","true")]
    public void ResourceGateAndOverclaimChangesFail(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var d=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>ConnectedIndustrialProfile.Validate(d.RootElement));}
    private sealed class NoInference:ILocalModel{public ModelStatus Status=>new("offline",true,"test");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken t)=>throw new Exception("Unexpected inference");}
    [Fact]public async Task ConnectedPromptSelectsExactProfileAndCompletesBoundedPlannerRoute(){
        await using var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");var observations=new List<ToolObservation>();var calls=new List<ToolCall>();
        for(var i=0;i<12;i++){var p=await model.CreatePlanAsync(new("Create a connected industrial level named connected-test.",[],observations,i,12-i),default);if(p.Actions.Count==0){Assert.Contains("three accessible single-floor zones",p.Response);break;}var call=Assert.Single(p.Actions);calls.Add(call);if(call.Kind==ToolKind.UnityEditorAction)_=UnityEditorActionSpec.From(call);
            var detail=call.Kind==ToolKind.CreateBlenderAsset?JsonSerializer.Serialize(new{bundleSha256=new string('a',64),assetSha256=new string('b',64)}):"Synthetic observation, no tool execution or quality approval.";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));}
        Assert.Equal(11,calls.Count);var mission=Assert.Single(calls,c=>c.Kind==ToolKind.UnityEditorAction&&c.Arguments["operation"]=="create_industrial_mission");Assert.Equal(ConnectedIndustrialProfile.Id,UnityEditorActionSpec.From(mission).LayoutProfile);
        Assert.All(calls.Where(c=>c.Id!=mission.Id),c=>Assert.False(c.Arguments.ContainsKey("layout_profile")));
    }
}

