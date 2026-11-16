using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RefinedPlanBindsExactNewRecipesAndKeepsSourceAndTargetReviewsSeparate(bool approve)
    {
        var fixture=await IndustrialPlanFixture(approve,recipeSet:"refined_v1");using var executor=fixture.Executor;
        var planCall=IndustrialPlanCall() with{Arguments=new Dictionary<string,string>(IndustrialPlanCall().Arguments){["recipe_set"]="refined_v1"}};
        var result=await executor.ExecuteAsync(planCall,default);Assert.True(result.Success,result.Error);using var json=JsonDocument.Parse(result.Output);
        var manifest=json.RootElement.GetProperty("manifest");Assert.Equal(3,manifest.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("refined_v1",manifest.GetProperty("recipeSet").GetString());Assert.Equal(approve,manifest.GetProperty("readyForTargetPreparation").GetBoolean());
        Assert.Equal("industrial_infiltration_refined_layout_v2",manifest.GetProperty("layoutVersion").GetString());
        var audit=json.RootElement.GetProperty("placementAudit");Assert.True(audit.GetProperty("nominalLayoutClear").GetBoolean());
        Assert.False(audit.GetProperty("runtimeNavigationVerified").GetBoolean());Assert.False(audit.GetProperty("placementApproved").GetBoolean());
        Assert.False(manifest.GetProperty("readyForAutomaticPlacement").GetBoolean());
        var slots=manifest.GetProperty("slots").EnumerateArray().ToArray();Assert.Equal(5,slots.Length);
        foreach(var slot in slots)Assert.True(IndustrialPropRecipes.Find(slot.GetProperty("recipe").GetString()!)!.IsRefined);
        foreach(var placement in manifest.GetProperty("placements").EnumerateArray())
            Assert.Equal(slots.Single(s=>s.GetProperty("slotId").GetString()==placement.GetProperty("footprint").GetProperty("slotId").GetString()).GetProperty("recipe").GetString(),
                placement.GetProperty("footprint").GetProperty("recipe").GetString());
        var hash=json.RootElement.GetProperty("planSha256").GetString()!;
        var wrongSet=await executor.ExecuteAsync(IndustrialSetImport(hash),default);Assert.False(wrongSet.Success);
        var exactImport=IndustrialSetImport(hash) with{Arguments=new Dictionary<string,string>(IndustrialSetImport(hash).Arguments){["recipe_set"]="refined_v1"}};
        var imported=await executor.ExecuteAsync(exactImport,default);Assert.Equal(approve,imported.Success);
        if(approve){using var body=JsonDocument.Parse(imported.Output);Assert.False(body.RootElement.GetProperty("placementApproved").GetBoolean());}
        else Assert.Empty(Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*.glb",SearchOption.AllDirectories));
    }
    [Fact]public void RefinedScriptDoesNotSilentlyInvalidateTheExistingIndustrialSourceLibrary()
    {
        var assembly=typeof(ToolExecutor).Assembly;
        using var legacy=assembly.GetManifestResourceStream("ForgeCore.Tools.Blender.IndustrialProps.py");Assert.NotNull(legacy);
        Assert.Equal("83e4d0c59b9d728e8c684349e831ebb012a1702ef864b2035d98b4a4668b05d0",Convert.ToHexString(SHA256.HashData(legacy)).ToLowerInvariant());
        using var refined=assembly.GetManifestResourceStream("ForgeCore.Tools.Blender.IndustrialPropsV2.py");Assert.NotNull(refined);
        using var reader=new StreamReader(refined);var text=reader.ReadToEnd();
        Assert.Contains("refined_industrial_finish_v1",text);Assert.Contains("humanVisualAccepted=False",text);
        Assert.DoesNotContain("requests.",text);Assert.DoesNotContain("subprocess",text);
        Assert.Equal(5,IndustrialPropRecipes.All.Count);Assert.Equal(5,IndustrialPropRecipes.Refined.Count);Assert.Equal(10,IndustrialPropRecipes.Supported.Count);
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            var old=IndustrialPropRecipes.Find(recipe.FamilyId)!;Assert.False(old.IsRefined);Assert.True(recipe.IsRefined);
            Assert.Equal(old.Dimensions(old.DefaultHeight,old.IsBarrel?340:0),recipe.Dimensions(recipe.DefaultHeight,recipe.IsBarrel?340:0));
        }
    }
    [Theory][InlineData("missing")][InlineData("exaggerated_normal")][InlineData("metallic_paint")][InlineData("fabricated_acceptance")][InlineData("wrong_recipe")]
    public async Task RefinedSurfaceMetricsAreBoundedAndCannotAssertHumanAppearanceApproval(string defect)
    {
        var exe=await BlenderFixtureExecutable();using var executor=BlenderExecutor(exe,async(e,a,w,t,c)=>{
            var result=await IndustrialFixtureRunner(e,a,w,t,c);var path=Path.Combine(w,"output","surface.json");
            if(defect=="missing"){File.Move(path,path+".retained");return result;}
            var json=await File.ReadAllTextAsync(path,c);
            json=defect switch {
                "exaggerated_normal"=>json.Replace("\"normalMaximumXY\":0.08","\"normalMaximumXY\":0.9"),
                "metallic_paint"=>json.Replace("\"paintedMetallicMaximum\":0","\"paintedMetallicMaximum\":1"),
                "fabricated_acceptance"=>json.Replace("\"humanVisualAccepted\":false","\"humanVisualAccepted\":true"),
                _=>json.Replace("\"recipe\":\"wooden_crate_v2\"","\"recipe\":\"wooden_crate_v1\"")};
            await File.WriteAllTextAsync(path,json,c);return result;
        });
        var result=await executor.ExecuteAsync(IndustrialCall("wooden_crate_v2"),default);Assert.False(result.Success);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","objects")));
    }
}
