namespace ForgeCore.Core;
public sealed record IndustrialAssetPlanSpec(string ProjectPath,string LevelId,string RecipeSet="legacy_v1")
{
    public static IndustrialAssetPlanSpec From(ToolCall call)
    {
        if(call.Kind is not (ToolKind.PlanIndustrialAssets or ToolKind.ImportIndustrialAssetSet))throw new ArgumentException("Not an industrial asset plan/import.");
        StagedAssetInspectionSpec.RequireOnly(call,call.Kind==ToolKind.PlanIndustrialAssets?["project_path","level_id","recipe_set"]:["project_path","level_id","plan_sha256","recipe_set"]);
        var id=StagedAssetInspectionSpec.Get(call,"level_id");
        if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-z][a-z0-9_-]{1,19}$",System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("level_id must be 2-20 bounded lowercase identifier characters.");
        if(call.Kind==ToolKind.ImportIndustrialAssetSet)_=ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,"plan_sha256"),"plan_sha256");
        var set=call.Arguments.TryGetValue("recipe_set",out var value)?value:"legacy_v1";
        if(set is not ("legacy_v1" or "refined_v1"))throw new ArgumentException("recipe_set must be legacy_v1 or refined_v1.");
        return new(StagedAssetInspectionSpec.Get(call,"project_path"),id,set);
    }
}
public sealed record IndustrialAssetSlot(string Id,string Recipe,int Count,string Purpose);
public static class IndustrialAssetPlan
{
    public const string MissionProfile="industrial_infiltration_v1";
    public static IReadOnlyList<IndustrialAssetSlot> Slots {get;}=[
        new("fuel","industrial_barrel_v2",1,"Original fuel prop beside the service route; not explosive."),
        new("supplies","wooden_crate_v1",2,"Storage silhouettes and conservative solid cover."),
        new("barriers","concrete_barrier_v1",2,"Low cover at the two encounter approaches."),
        new("fences","industrial_fence_v1",3,"Visible boundary panels; solid-blocking collision despite open visual mesh."),
        new("containers","shipping_container_v1",2,"Sealed cargo cover; no walkable interior.")
    ];
    private static readonly IReadOnlyList<IndustrialAssetSlot> RefinedSlots=Slots.Select(slot=>slot with {
        Recipe=IndustrialPropRecipes.Refined.Single(r=>r.FamilyId==slot.Recipe).Id}).ToArray();
    public static IReadOnlyList<IndustrialAssetSlot> SlotsFor(string recipeSet)=>recipeSet switch {
        "legacy_v1"=>Slots,"refined_v1"=>RefinedSlots,_=>throw new ArgumentException("Unsupported industrial recipe set.")};
}
