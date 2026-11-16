using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndustrialTargetPreparationTests
{
    [Fact]public void ExactTargetPlanBindsImportAndReviewDestinationsAndNeverClaimsPlacement()
    {
        foreach(var slot in IndustrialAssetPlan.Slots) {
            var step=IndustrialTargetPreparation.Create(new("Games/ForgeCore","yard-mission"),slot,new string('a',64),new string('b',64));
            Assert.Contains("yard-mission-"+slot.Id+"-aaaaaaaa",step.ImportedAssetPath);Assert.EndsWith("-unity",step.AssetSetId);
            Assert.True(step.AssetSetId.Length<=48);Assert.StartsWith("Assets/Scenes/IndustrialReview/",step.ScenePath);
            var call=step.Proposal("Games/ForgeCore");Assert.Equal("prepare_industrial_prop",call.Arguments["operation"]);
            Assert.Equal(new string('b',64),call.Arguments["expected_sha256"]);Assert.Equal(PermissionKind.Execute,call.RequiredPermission);
        }
    }
    [Theory][InlineData("../escape")][InlineData("UPPER")][InlineData("x")]
    public void UnsafeLevelIdsCannotCreateTargetInstructions(string id)=>Assert.Throws<ArgumentException>(()=>
        IndustrialTargetPreparation.Create(new("Game",id),IndustrialAssetPlan.Slots[0],new string('a',64),new string('b',64)));
    [Fact]public void UnknownSlotsAndMalformedHashesFailClosed()
    {
        Assert.Throws<ArgumentException>(()=>IndustrialTargetPreparation.Create(new("Game","yard"),new("bad","shell",1,"untrusted"),new string('a',64),new string('b',64)));
        Assert.Throws<ArgumentException>(()=>IndustrialTargetPreparation.Create(new("Game","yard"),IndustrialAssetPlan.Slots[0],"bad",new string('b',64)));
    }
    [Fact]public void RefinedPlanIsExplicitAndCannotBorrowLegacySlotIdentity()
    {
        var spec=new IndustrialAssetPlanSpec("Game","yard","refined_v1");
        foreach(var slot in IndustrialAssetPlan.SlotsFor(spec.RecipeSet)) {
            Assert.True(IndustrialPropRecipes.Find(slot.Recipe)!.IsRefined);
            var step=IndustrialTargetPreparation.Create(spec,slot,new string('a',64),new string('b',64));Assert.Equal(slot.Recipe,step.Recipe);
        }
        Assert.Throws<ArgumentException>(()=>IndustrialTargetPreparation.Create(spec,IndustrialAssetPlan.Slots[0],new string('a',64),new string('b',64)));
        Assert.Throws<ArgumentException>(()=>IndustrialAssetPlan.SlotsFor("arbitrary"));
        var valid=ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Bounded refined plan",("project_path","Game"),("level_id","yard"),("recipe_set","refined_v1"));
        Assert.Equal("refined_v1",IndustrialAssetPlanSpec.From(valid).RecipeSet);
        Assert.Throws<ArgumentException>(()=>IndustrialAssetPlanSpec.From(valid with{Arguments=new Dictionary<string,string>(valid.Arguments){["recipe_set"]="any_recipe"}}));
    }
}
