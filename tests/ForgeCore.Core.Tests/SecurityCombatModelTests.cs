using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class SecurityCombatModelTests
{
    private sealed class NoFallback:ILocalModel
    {
        public ModelStatus Status=>new("test",true,"offline");
        public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken c)=>throw new Exception("No provider fallback");
    }
    [Theory]
    [InlineData("Create an industrial security character in Blender. Name it security-guard.","prepare_character_animation")]
    [InlineData("Create a security combat lab in Blender. Name it security-combat.","create_security_combat_lab")]
    [InlineData("Create an industrial infiltration mission in Blender. Name it yard-mission.","create_industrial_mission")]
    public async Task BoundedSecurityPromptKeepsOriginalSourceAndScopedExecution(string prompt,string finalOperation)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var observations=new List<ToolObservation>();var finished=false;
        for(var turn=0;turn<9;turn++)
        {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12),default);
            if(plan.Actions.Count==0){Assert.Equal(7,observations.Count);Assert.Contains(finalOperation=="create_industrial_mission"?"acceptance":"pending",plan.Response);finished=true;break;}
            var call=Assert.Single(plan.Actions);
            if(turn==0)Assert.Equal(BlenderAssetSpec.SecurityCharacterRecipe,BlenderAssetSpec.From(call).Recipe);
            if(turn==6)Assert.Equal(finalOperation,UnityEditorActionSpec.From(call).Operation);
            Assert.DoesNotContain(call.Kind,new[]{ToolKind.Generate3DAsset,ToolKind.ReviewGameArtAsset,ToolKind.StoreReviewedAsset});
            var detail=call.Kind==ToolKind.CreateBlenderAsset?"{\"bundleSha256\":\""+new string('a',64)+"\",\"assetSha256\":\""+new string('b',64)+"\"}":"fixture only";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        }
        Assert.True(finished);
    }
    [Theory]
    [InlineData("asset_set_id","safe..escape")][InlineData("asset_path","Assets/Other/source.glb")]
    [InlineData("output_path","Builds/Overwrite.exe")][InlineData("script","arbitrary")]
    public void CombatLabCannotBroadenItsExactSourceOrFreshDestinations(string key,string value)
    {
        var fields=new Dictionary<string,string>{{"project_path","Game"},{"operation","create_security_combat_lab"},
            {"asset_path","Assets/ForgeCore/Generated/guard.glb"},{"expected_sha256",new string('a',64)},
            {"asset_set_id","combat-lab"},{"scene_path","Assets/Scenes/Combat.unity"},{"output_path","Previews/Combat.png"}};
        _=UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,fields,PermissionKind.Execute,"combat"));
        fields[key]=value;
        Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,fields,PermissionKind.Execute,"combat")));
    }
}
