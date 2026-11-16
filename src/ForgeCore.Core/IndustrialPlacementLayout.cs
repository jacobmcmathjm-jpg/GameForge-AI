namespace ForgeCore.Core;

/// <summary>Exact planning footprints for the existing authored greybox, not executed placements or navigation proof.</summary>
public sealed record IndustrialPlacementFootprint(string PlacementId,string SlotId,string Recipe,
    double X,double Y,double Z,double YawDegrees,double Width,double Height,double Depth)
{
    public double UniformScale=>1;
    public string Pivot=>"bottom_center";
}

public static class IndustrialPlacementLayout
{
    public const string Version="industrial_infiltration_layout_v1";
    public static IReadOnlyList<IndustrialPlacementFootprint> Placements {get;}=Array.AsReadOnly(new[]{
        Slot("fuel-1","fuel","industrial_barrel_v2",-12,-5),
        Slot("supplies-1","supplies","wooden_crate_v1",-6,-3),
        Slot("supplies-2","supplies","wooden_crate_v1",8,-1),
        Slot("barriers-1","barriers","concrete_barrier_v1",-5,-4),
        Slot("barriers-2","barriers","concrete_barrier_v1",9,7),
        Slot("fences-1","fences","industrial_fence_v1",-14,4),
        Slot("fences-2","fences","industrial_fence_v1",14,4),
        Slot("fences-3","fences","industrial_fence_v1",0,14),
        Slot("containers-1","containers","shipping_container_v1",3,0),
        Slot("containers-2","containers","shipping_container_v1",9,0)
    });
    public static bool FitsSource(string recipe,int heightMillimetres,int radiusMillimetres)
    {
        var definition=IndustrialPropRecipes.Find(recipe);
        // Search by height alone is insufficient: a wider approved barrel could
        // silently exceed the authored cover/navigation footprint.
        return definition is not null&&heightMillimetres==definition.DefaultHeight&&
            radiusMillimetres==(definition.IsBarrel?340:0);
    }
    public static string VersionFor(string recipeSet)=>recipeSet switch {
        "legacy_v1"=>Version,"refined_v1"=>"industrial_infiltration_refined_layout_v2",_=>throw new ArgumentException("Unsupported layout recipe set.")};
    public static IReadOnlyList<IndustrialPlacementFootprint> For(string recipeSet)
    {
        _=VersionFor(recipeSet);if(recipeSet=="legacy_v1")return Placements;
        // A separate unexecuted plan, never a mutation of an existing greybox scene.
        return Array.AsReadOnly(Placements.Select(p=>p with {
            Recipe=IndustrialPropRecipes.Refined.Single(r=>r.FamilyId==p.Recipe).Id,
            Z=p.PlacementId=="supplies-2"?-5:p.PlacementId=="containers-1"?-2:p.Z}).ToArray());
    }
    private static IndustrialPlacementFootprint Slot(string id,string slot,string recipe,double x,double z)
    {
        var r=IndustrialPropRecipes.Find(recipe)??throw new InvalidOperationException("Unsupported authored placement recipe.");
        var size=r.Dimensions(r.DefaultHeight,r.IsBarrel?340:0);
        return new(id,slot,recipe,x,0,z,0,size.Width,size.Height,size.Depth);
    }
}
