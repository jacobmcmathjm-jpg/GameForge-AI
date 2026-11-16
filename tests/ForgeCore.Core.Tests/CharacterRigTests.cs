using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class CharacterRigTests
{
    private static CharacterRigEvidence Valid() => new(1,new string('a',64),"generic_named_bones_v1",22,1000,0,.00001,.00001,0,1.8,0,
        [new("arms",300,.4,1.5,true),new("legs",400,.5,2,true),new("combined",800,.6,2.2,true)]);
    [Fact] public void CharacterEvidence_RequiresActualNormalizedSkinAndBoundedRecoverableDeformation()
    {
        Valid().Validate(new string('a',64));
        foreach(var rig in new[]{Valid() with{BoneCount=0},Valid() with{UnweightedVertices=1},Valid() with{MaximumRestError=.01},
            Valid() with{MaximumRecoveryError=.001},Valid() with{Height=180},Valid() with{GroundOffset=.5},
            Valid() with{MaximumWeightSumError=double.NaN},Valid() with{Poses=[]},Valid() with{AssetSha256=new string('b',64)},
            Valid() with{Poses=[new("arms",300,.4,5.724173,true),new("legs",300,.5,1,true),new("combined",400,.5,1,true)]}})
            Assert.Throws<ArgumentException>(()=>rig.Validate(new string('a',64)));
    }
    [Theory]
    [InlineData("height_mm","1599")][InlineData("height_mm","2001")][InlineData("seed","-1")]
    [InlineData("radius_mm","340")][InlineData("python","unsafe")][InlineData("asset_name","../escape")]
    public void CharacterRecipe_RejectsUnboundedAndStaticPropOnlyArguments(string key,string value)
    {
        var args=new Dictionary<string,string>{{"project_path","Game"},{"recipe",BlenderAssetSpec.CharacterRecipe},
            {"asset_name","enemy-test"},{"seed","41"},{"height_mm","1800"}};args[key]=value;
        var call=new ToolCall(Guid.NewGuid(),ToolKind.CreateBlenderAsset,args,PermissionKind.Execute,"character");
        Assert.Throws<ArgumentException>(()=>BlenderAssetSpec.From(call));
    }
    private sealed class NoFallback:ILocalModel
    {
        public ModelStatus Status=>new("test",true,"offline");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest r,CancellationToken c)=>throw new Exception("Unexpected inference/provider fallback");
    }
    [Fact] public async Task CharacterPrompt_CreatesImportsAndRunsExplicitRigReviewWithoutHumanOrGameplayClaims()
    {
        const string prompt="Create a rigged mannequin in Blender. Name it training-enemy.";
        var observations=new List<ToolObservation>();
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        async Task<AgentPlan> Next()=>await model.CreatePlanAsync(new(prompt,[],observations,2,12),default);
        void Add(ToolCall call,string detail)=>observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        var create=Assert.Single((await Next()).Actions);
        Assert.Equal(BlenderAssetSpec.CharacterRecipe,BlenderAssetSpec.From(create).Recipe);Assert.False(create.Arguments.ContainsKey("radius_mm"));
        Add(create,"{\"bundleSha256\":\""+new string('a',64)+"\",\"assetSha256\":\""+new string('b',64)+"\"}");
        var import=Assert.Single((await Next()).Actions);Assert.Equal(ToolKind.ImportBlenderAsset,import.Kind);Add(import,"imported");
        var install=Assert.Single((await Next()).Actions);Assert.Equal(ToolKind.InstallUnityBridge,install.Kind);Add(install,"installed");
        var compile=Assert.Single((await Next()).Actions);Assert.Equal("compile",UnityEditorActionSpec.From(compile).Operation);Add(compile,"compiled");
        var inspect=Assert.Single((await Next()).Actions);var spec=UnityEditorActionSpec.From(inspect);
        Assert.Equal("inspect_character_rig",spec.Operation);Assert.StartsWith("Assets/Scenes/CharacterRigs/",spec.ScenePath);
        Assert.StartsWith("Previews/CharacterRigs/",spec.OutputPath);Add(inspect,"Character rig evidence: fixture");
        var done=await Next();Assert.Empty(done.Actions);Assert.Contains("remains pending",done.Response);
        Assert.DoesNotContain(observations,o=>o.Kind==ToolKind.Generate3DAsset || o.Kind==ToolKind.ReviewGameArtAsset);
    }
    [Theory]
    [InlineData("output_path","../out.png")][InlineData("scene_path","Assets/Scenes/../../bad.unity")]
    [InlineData("python","arbitrary")][InlineData("asset_path","Assets/Other/character.glb")]
    public void RigInspection_HasNarrowWorkspaceAndArgumentContract(string key,string value)
    {
        var args=new Dictionary<string,string>{{"project_path","Game"},{"operation","inspect_character_rig"},
            {"asset_path","Assets/ForgeCore/Generated/test.glb"},{"expected_sha256",new string('a',64)},
            {"scene_path","Assets/Scenes/Rig.unity"},{"output_path","Previews/Rig.png"}};
        args[key]=value;
        Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(new(Guid.NewGuid(),ToolKind.UnityEditorAction,args,PermissionKind.Execute,"inspect")));
    }
}
