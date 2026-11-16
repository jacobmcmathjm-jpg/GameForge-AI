using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("")][InlineData("normal")][InlineData("resolution")]
    public async Task IndustrialSecurityRetainsExactBoundedSkinAndNormalMapWithoutAcceptingArt(string defect)
    {
        var exe=await BlenderFixtureExecutable();var launches=0;
        using var tools=BlenderExecutor(exe,async(e,a,w,t,c)=>
        {
            launches++;await CharacterRunner(e,a,w,t,c);
            var output=Path.Combine(w,"output");
            var metrics=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(await File.ReadAllTextAsync(Path.Combine(output,"metrics.json"),c))!;
            metrics["recipe"]=JsonSerializer.SerializeToElement(BlenderAssetSpec.SecurityCharacterRecipe);
            metrics["materials"]=JsonSerializer.SerializeToElement(5);
            metrics["textureResolution"]=JsonSerializer.SerializeToElement(1024);
            await File.WriteAllTextAsync(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(metrics),c);
            foreach(var n in new[]{"basecolor.png","roughness.png","metallic.png","normal.png"})
                await File.WriteAllBytesAsync(Path.Combine(output,n),MinimalPng(defect=="resolution"?512u:1024u,1024),c);
            if(defect=="normal")File.Delete(Path.Combine(output,"normal.png"));
            return new ForgeCore.Tools.UnityProcessResult(0,"FORGECORE_BLENDER_RECIPE_COMPLETE","");
        });
        var args=new Dictionary<string,string>(CharacterCall().Arguments){["recipe"]=BlenderAssetSpec.SecurityCharacterRecipe,["asset_name"]="security-guard"};
        var call=CharacterCall() with { Arguments=args };
        var first=await tools.ExecuteAsync(call,default);Assert.Equal(defect=="",first.Success);
        if(defect!="")return;
        var again=await tools.ExecuteAsync(call,default);Assert.True(again.Success,again.Error);Assert.Equal(1,launches);
        using var receipt=JsonDocument.Parse(first.Output);
        Assert.Equal("unreviewed_local_recipe",receipt.RootElement.GetProperty("reviewState").GetString());
        var hash=receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        await File.AppendAllTextAsync(Path.Combine(_root,".forgecore","blender-library","objects",hash,"normal.png"),"tampered");
        Assert.False((await tools.ExecuteAsync(call,default)).Success);
        Assert.False((await tools.ExecuteAsync(ImportBarrel(first.Output,"security-import"),default)).Success);
    }
}
