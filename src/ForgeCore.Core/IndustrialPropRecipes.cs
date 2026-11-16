namespace ForgeCore.Core;

/// <summary>Fixed original S08 recipes. Dimensions are metres, Blender Z-up; not arbitrary modelling.</summary>
public sealed record IndustrialPropRecipe(string Id,string Label,int DefaultHeight,int MinHeight,int MaxHeight,
    double Width,double Depth,double Height,string Tags)
{
    public string FamilyId=>Id switch {
        "industrial_barrel_v3"=>"industrial_barrel_v2","wooden_crate_v2"=>"wooden_crate_v1",
        "concrete_barrier_v2"=>"concrete_barrier_v1","industrial_fence_v2"=>"industrial_fence_v1",
        "shipping_container_v2"=>"shipping_container_v1",_=>Id};
    public bool IsRefined=>FamilyId!=Id;
    public bool IsBarrel=>FamilyId=="industrial_barrel_v2";
    public (double Width,double Depth,double Height) Dimensions(int heightMillimetres,int radiusMillimetres=0)
    {
        if(heightMillimetres<MinHeight||heightMillimetres>MaxHeight)throw new ArgumentException("Prop height is outside its fixed recipe range.");
        var h=heightMillimetres/1000d;
        if(IsBarrel) {
            if(radiusMillimetres is <250 or >500)throw new ArgumentException("Barrel radius must be 250-500 mm.");
            return (radiusMillimetres/1000d*2,radiusMillimetres/1000d*2,h);
        }
        if(radiusMillimetres!=0)throw new ArgumentException("This recipe does not accept a radius.");
        return(Width*h/Height,Depth*h/Height,h);
    }
}
public static class IndustrialPropRecipes
{
    public static IReadOnlyList<IndustrialPropRecipe> All {get;}=[
        new("industrial_barrel_v2","Weathered industrial steel barrel",1100,1000,1500,.68,.68,1.1,"industrial steel metal weathered barrel prop"),
        new("wooden_crate_v1","Braced wooden shipping crate",800,600,1200,1,.8,.8,"industrial wooden wood shipping crate prop"),
        new("concrete_barrier_v1","Concrete safety barrier",1000,800,1200,2.4,.6,1,"industrial concrete safety jersey barrier cover prop"),
        new("industrial_fence_v1","Solid-blocking welded fence panel",2200,1800,2600,3,.16,2.2,"industrial metal welded fence panel prop"),
        new("shipping_container_v1","Closed ribbed shipping container",2590,2200,3000,2.44,6.06,2.59,"industrial steel cargo shipping container prop")
    ];
    // Legacy IDs remain supported and keep their original embedded recipe byte-for-byte.
    public static IReadOnlyList<IndustrialPropRecipe> Refined {get;}=[
        new("industrial_barrel_v3","Refined weathered industrial steel barrel",1100,1000,1500,.68,.68,1.1,"refined industrial steel metal weathered barrel prop"),
        new("wooden_crate_v2","Refined braced wooden shipping crate",800,600,1200,1,.8,.8,"refined industrial wooden wood shipping crate prop"),
        new("concrete_barrier_v2","Refined concrete safety barrier",1000,800,1200,2.4,.6,1,"refined industrial concrete safety jersey barrier cover prop"),
        new("industrial_fence_v2","Refined welded fence panel",2200,1800,2600,3,.16,2.2,"refined industrial metal welded fence panel prop"),
        new("shipping_container_v2","Refined closed ribbed shipping container",2590,2200,3000,2.44,6.06,2.59,"refined industrial steel cargo shipping container prop")
    ];
    public static IReadOnlyList<IndustrialPropRecipe> Supported {get;}=All.Concat(Refined).ToArray();
    public static IndustrialPropRecipe? Find(string id)=>Supported.FirstOrDefault(r=>r.Id==id);
    public static bool Contains(string id)=>Find(id)is not null;
}
