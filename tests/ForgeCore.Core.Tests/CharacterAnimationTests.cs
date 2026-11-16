using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class CharacterAnimationTests
{
    private static Dictionary<string,string> Fields()=>new(){{"project_path","Game"},{"operation","prepare_character_animation"},
        {"asset_path","Assets/ForgeCore/Generated/character.glb"},{"expected_sha256",new string('a',64)},
        {"asset_set_id","enemy-motion"},{"scene_path","Assets/Scenes/Motion.unity"},{"output_path","Previews/Motion.png"}};
    [Theory]
    [InlineData("asset_set_id","../escape")][InlineData("asset_set_id","safe..escape")][InlineData("scene_path","../scene.unity")]
    [InlineData("output_path","Builds/Overwrite.exe")][InlineData("asset_path","Assets/Other/character.glb")]
    [InlineData("script","arbitrary")][InlineData("expected_sha256","wrong")]
    public void AnimationPreparationIsExactAndBounded(string key,string value)
    {var f=Fields();f[key]=value;Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,f,PermissionKind.Execute,"animation")));}
    [Fact]public void AnimationPreparationRequiresAllSixInputs()
    {
        Assert.Equal("prepare_character_animation",UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,Fields(),PermissionKind.Execute,"animation")).Operation);
        foreach(var key in Fields().Keys){var f=Fields();f.Remove(key);Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,f,PermissionKind.Execute,"animation")));}
    }
    private sealed class NoFallback:ILocalModel
    {public ModelStatus Status=>new("test",true,"offline");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken c)=>throw new Exception("Unexpected provider use");}
    [Fact]public async Task AnimatedPromptUsesBlenderBaseAndUnityMotionWithSeparateScopedSteps()
    {
        var observations=new List<ToolObservation>();await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        const string prompt="Create an animated rigged mannequin in Blender. Name it training-motion.";
        var operations=new List<string>();
        for(var turn=0;turn<9;turn++)
        {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12),default);
            if(plan.Actions.Count==0){Assert.Equal(7,observations.Count);Assert.Contains("remains pending",plan.Response);break;}
            var call=Assert.Single(plan.Actions);if(call.Kind==ToolKind.UnityEditorAction)operations.Add(UnityEditorActionSpec.From(call).Operation);
            Assert.DoesNotContain(call.Kind,new[]{ToolKind.Generate3DAsset,ToolKind.ReviewGameArtAsset,ToolKind.StoreReviewedAsset});
            var detail=call.Kind==ToolKind.CreateBlenderAsset?"{\"bundleSha256\":\""+new string('a',64)+"\",\"assetSha256\":\""+new string('b',64)+"\"}":"fixture success";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        }
        Assert.Equal(new[]{"compile","inspect_character_rig","prepare_character_animation"},operations);
        Assert.Single(observations,o=>o.Kind==ToolKind.ScaffoldUnityGameTemplate);
    }
}
