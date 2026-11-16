using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    private static readonly string[] CharacterBones=["Root","Hips","Spine","Chest","Neck","Head",
        "Clavicle.L","UpperArm.L","LowerArm.L","Hand.L","UpperLeg.L","LowerLeg.L","Foot.L","Toe.L",
        "Clavicle.R","UpperArm.R","LowerArm.R","Hand.R","UpperLeg.R","LowerLeg.R","Foot.R","Toe.R"];
    private static ToolCall CharacterCall()=>ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Original technical mannequin",
        ("project_path","Game"),("asset_name","training-rig"),("recipe",BlenderAssetSpec.CharacterRecipe),("seed","41"),("height_mm","1800"));
    // Synthetic dense skin data proves parser/hash handling only, not real modelling/rendering quality.
    private static byte[] CharacterGlb(string defect)
    {
        using var buffer=new MemoryStream();using(var writer=new BinaryWriter(buffer,Encoding.UTF8,true))
        {
            for(var i=0;i<100;i++){writer.Write(0f);writer.Write(defect=="position"&&i==0?float.NaN:i*.018f);writer.Write(0f);}
            for(var i=0;i<100;i++){writer.Write(defect=="weights"?.5f:1f);writer.Write(0f);writer.Write(0f);writer.Write(0f);}
            for(var i=0;i<100;i++){writer.Write((byte)(defect=="index"?90:1));writer.Write((byte)0);writer.Write((byte)0);writer.Write((byte)0);}
            for(var j=0;j<22;j++)for(var c=0;c<16;c++)writer.Write(defect=="bind"&&j==0&&c==0?float.NaN:c%5==0?1f:0f);
            for(var i=0;i<300;i++)writer.Write((ushort)(i%100));
        }
        var data=buffer.ToArray();
        var nodes=new List<Dictionary<string,object>>();
        for(var i=0;i<22;i++)nodes.Add(new(){{"name",defect=="name"&&i==5?"NoHead":CharacterBones[i]}});
        // This fixture's simple chain is only a GLB structural parser test, not Unity rig acceptance.
        for(var i=0;i<21;i++)nodes[i]["children"]=new[]{i+1};
        if(defect=="cycle")nodes[21]["children"]=new[]{0};
        nodes.Add(new(){{"name","Skin"},{"mesh",0},{"skin",0}});
        var attributes=new Dictionary<string,int>{{"POSITION",0},{"WEIGHTS_0",1},{"JOINTS_0",2}};
        if(defect=="missing")attributes.Remove("WEIGHTS_0");
        var root=new
        {
            asset=new{version="2.0"},scene=0,scenes=new[]{new{nodes=new[]{0,22}}},nodes,
            buffers=new[]{new{byteLength=data.Length}},
            bufferViews=new[]{new{buffer=0,byteOffset=0,byteLength=1200},new{buffer=0,byteOffset=1200,byteLength=1600},
                new{buffer=0,byteOffset=2800,byteLength=400},new{buffer=0,byteOffset=3200,byteLength=1408},new{buffer=0,byteOffset=4608,byteLength=600}},
            accessors=new object[]{new{bufferView=0,componentType=5126,count=100,type="VEC3",min=new[]{0,0,0},max=new[]{0,1.8,0}},
                new{bufferView=1,componentType=5126,count=100,type="VEC4"},new{bufferView=2,componentType=5121,count=100,type="VEC4"},
                new{bufferView=3,componentType=5126,count=22,type="MAT4"},new{bufferView=4,componentType=5123,count=300,type="SCALAR"}},
            skins=new[]{new{inverseBindMatrices=3,joints=Enumerable.Range(0,22).ToArray()}},
            meshes=new[]{new{primitives=new[]{new{attributes,indices=4}}}}
        };
        var json=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(root));var padding=(4-json.Length%4)%4;
        using var output=new MemoryStream();using var glb=new BinaryWriter(output);
        glb.Write(0x46546c67u);glb.Write(2u);glb.Write((uint)(12+8+json.Length+padding+8+data.Length));
        glb.Write((uint)(json.Length+padding));glb.Write(0x4e4f534au);glb.Write(json);for(var i=0;i<padding;i++)glb.Write((byte)32);
        glb.Write((uint)data.Length);glb.Write(0x004e4942u);glb.Write(data);return output.ToArray();
    }
    private static async Task<UnityProcessResult> CharacterRunner(string exe,IReadOnlyList<string> args,string directory,TimeSpan timeout,CancellationToken token,string defect="")
    {
        await BlenderFixtureRunner(exe,args,directory,timeout,token);
        var output=Path.Combine(directory,"output");
        await File.WriteAllBytesAsync(Path.Combine(output,"asset.glb"),CharacterGlb(defect),token);
        await File.WriteAllTextAsync(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(new{
            recipe=BlenderAssetSpec.CharacterRecipe,blenderVersion="5.2.1 LTS",vertices=100,triangles=100,materials=1,objects=2,
            boundsMin=new[]{-.88,-.24,0},boundsMax=new[]{.88,.14,1.8},pivot="bottom_center",units="metres",textureResolution=512,animationCount=0}),token);
        await File.WriteAllTextAsync(Path.Combine(output,"rig.json"),JsonSerializer.Serialize(new{
            schemaVersion=1,rigConvention="generic_named_bones_v1",restPose="T",sourceForward="-Y",sourceUp="+Z",unityForward="+Z",
            rootMotion="in_place_no_motion_clips",boneNames=CharacterBones,maximumInfluences=4,unweightedVertices=0,maximumWeightSumError=0}),token);
        return new(0,"FORGECORE_BLENDER_RECIPE_COMPLETE","");
    }
    [Theory]
    [InlineData("weights")][InlineData("index")][InlineData("bind")][InlineData("name")][InlineData("missing")][InlineData("cycle")][InlineData("position")]
    public async Task Character_InvalidActualSkinCannotPublish(string defect)
    {
        var exe=await BlenderFixtureExecutable();using var tools=BlenderExecutor(exe,(e,a,w,t,c)=>CharacterRunner(e,a,w,t,c,defect));
        var result=await tools.ExecuteAsync(CharacterCall(),default);Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","objects")));
    }
    [Fact] public async Task Character_SourceRigManifestIsRetainedReverifiedAndSeparateFromHumanReview()
    {
        var exe=await BlenderFixtureExecutable();var launches=0;
        using var tools=BlenderExecutor(exe,(e,a,w,t,c)=>{launches++;return CharacterRunner(e,a,w,t,c);});
        var first=await tools.ExecuteAsync(CharacterCall(),default);Assert.True(first.Success,first.Error);
        var repeat=await tools.ExecuteAsync(CharacterCall(),default);Assert.True(repeat.Success,repeat.Error);Assert.Equal(1,launches);
        var import=ImportBarrel(first.Output,"character-import");Assert.True((await tools.ExecuteAsync(import,default)).Success);
        Assert.False((await tools.ExecuteAsync(import,default)).Success);
        using var receipt=JsonDocument.Parse(first.Output);var bundle=receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        var rigPath=Path.Combine(_root,".forgecore","blender-library","objects",bundle,"rig.json");
        await File.AppendAllTextAsync(rigPath," ");
        Assert.False((await tools.ExecuteAsync(CharacterCall(),default)).Success);
        Assert.False((await tools.ExecuteAsync(ImportBarrel(first.Output,"second-rig"),default)).Success);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","asset-vault")));
    }
}
