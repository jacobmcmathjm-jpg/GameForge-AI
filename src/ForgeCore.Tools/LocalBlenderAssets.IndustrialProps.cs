using System.Buffers.Binary;
using ForgeCore.Core;
namespace ForgeCore.Tools;
internal sealed partial class LocalBlenderAssets
{
    public async Task<Dictionary<string,string>> VerifyIndustrialImportAsync(string project,string assetPath,string expected,CancellationToken token)
    {
        RequireProject(project);
        var full=paths.Resolve(Path.Combine(project,assetPath),false);
        using var provenance=System.Text.Json.JsonDocument.Parse(await ReadSourceStateBytes(full+".asset.json",16*1024,token));
        var page=provenance.RootElement.GetProperty("sourcePage").GetString()??"";
        const string prefix="forgecore://local-blender/";
        if(!page.StartsWith(prefix,StringComparison.Ordinal)||page.Length!=prefix.Length+64||
            provenance.RootElement.GetProperty("destinationPath").GetString()!=assetPath.Replace('\\','/')||
            provenance.RootElement.GetProperty("sha256").GetString()!=expected)throw new InvalidDataException("Industrial import provenance identity failed.");
        var bundle=await VerifyBundle(page[prefix.Length..],token);
        if(!IndustrialPropRecipes.Contains(bundle.Request.Recipe)||bundle.Files.Single(f=>f.Name=="asset.glb").Sha256!=expected)
            throw new InvalidDataException("Industrial source recipe/main GLB identity failed.");
        var stem=Path.Combine(Path.GetDirectoryName(full)!,Path.GetFileNameWithoutExtension(full));
        var map=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var name in new[]{"asset.glb","lod1.glb","lod2.glb","prop.json"})
        {
            var path=name=="asset.glb"?full:name=="prop.json"?stem+".prop.json":stem+"-"+name;
            paths.Resolve(path,false);
            var hash=bundle.Files.Single(f=>f.Name==name).Sha256;
            if(await HashFile(path,MaxFileBytes,token)!=hash)throw new InvalidDataException("Industrial companion differs from exact source: "+name);
            map.Add(path,hash);
            var meta=paths.Resolve(path+".meta",false);map.Add(meta,await HashFile(meta,1024*1024,token));
        }
        return map;
    }
    private sealed record PropMetadata(int SchemaVersion,string Recipe,string CollisionConvention,double[] CollisionCentre,
        double[] CollisionSize,int[] LodTriangles,bool UvFinite,bool RootIdentity,string CoordinateSystem);
    private sealed record RefinedSurfaceMetadata(int SchemaVersion,string Recipe,string Revision,int Resolution,
        double RoughnessMinimum,double RoughnessMaximum,double NormalMaximumXY,double NormalMeanZ,double PaintedMetallicMaximum,
        bool ProceduralOriginal,bool HumanVisualAccepted);
    private async Task<RefinedSurfaceMetadata> ReadRefinedSurface(string root,string recipe,CancellationToken token)
    {
        var s=await ReadJson<RefinedSurfaceMetadata>(Path.Combine(root,"surface.json"),4096,token);
        if(s.SchemaVersion!=1||s.Recipe!=recipe||s.Revision!="refined_industrial_finish_v1"||s.Resolution!=1024||
            !s.ProceduralOriginal||s.HumanVisualAccepted||
            new[]{s.RoughnessMinimum,s.RoughnessMaximum,s.NormalMaximumXY,s.NormalMeanZ,s.PaintedMetallicMaximum}.Any(x=>!double.IsFinite(x))||
            s.RoughnessMinimum<.25||s.RoughnessMaximum>.95||s.RoughnessMinimum>s.RoughnessMaximum||
            s.NormalMaximumXY is <0 or >.181||s.NormalMeanZ is <.96 or >1||s.PaintedMetallicMaximum!=0)
            throw new InvalidDataException("Refined surface technical bounds/identity failed; not a human appearance approval.");
        return s;
    }
    private async Task ValidateIndustrialProp(string root,Request req,Metrics m,IndustrialPropRecipe recipe,CancellationToken token)
    {
        var dimensions=recipe.Dimensions(req.HeightMillimetres,req.RadiusMillimetres);
        if(m.Recipe!=req.Recipe||m.BlenderVersion is null||!m.BlenderVersion.StartsWith("5.2.",StringComparison.Ordinal)||
            m.Vertices is <32 or >20000||m.Triangles is <32 or >16000||m.Materials is <1 or >4||m.Objects!=1||
            m.Pivot!="bottom_center"||m.Units!="metres"||m.TextureResolution!=1024||m.AnimationCount!=0||
            m.BoundsMin is not {Length:3}||m.BoundsMax is not {Length:3}||m.BoundsMin.Concat(m.BoundsMax).Any(v=>!double.IsFinite(v)))
            throw new InvalidDataException("Industrial prop metrics failed.");
        var size=new[]{dimensions.Width,dimensions.Depth,dimensions.Height};
        var low=new[]{-size[0]/2,-size[1]/2,0d};var high=new[]{size[0]/2,size[1]/2,size[2]};
        for(var i=0;i<3;i++)if(Math.Abs(m.BoundsMin[i]-low[i])>.006||Math.Abs(m.BoundsMax[i]-high[i])>.006)
            throw new InvalidDataException("Industrial prop measured bounds/pivot differ from fixed recipe dimensions.");
        foreach(var name in new[]{"basecolor.png","roughness.png","metallic.png","normal.png"})
        {
            _=await StagedAssetInspector.InspectPngAsync(Path.Combine(root,name),token);
            await using var input=File.OpenRead(Path.Combine(root,name));var header=new byte[24];await input.ReadExactlyAsync(header,token);
            if(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=1024||BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=1024)
                throw new InvalidDataException("Industrial PBR maps must be 1024 square.");
        }
        foreach(var name in new[]{"lod1.glb","lod2.glb"})_=await StagedAssetInspector.InspectGlbAsync(Path.Combine(root,name),token);
        var p=await ReadJson<PropMetadata>(Path.Combine(root,"prop.json"),4096,token);
        if(p.SchemaVersion!=1||p.Recipe!=req.Recipe||p.CollisionConvention!="solid_box_no_walkable_interior"||p.CoordinateSystem!="Blender_Z_up_metres"||
            !p.UvFinite||!p.RootIdentity||p.LodTriangles is not {Length:3}||p.LodTriangles[0]!=m.Triangles||
            p.LodTriangles.Any(n=>n is <12 or >16000)||p.LodTriangles[1]>=p.LodTriangles[0]||p.LodTriangles[2]>=p.LodTriangles[1]||
            p.CollisionSize is not {Length:3}||p.CollisionCentre is not {Length:3}||
            p.CollisionSize.Concat(p.CollisionCentre).Any(v=>!double.IsFinite(v)))
            throw new InvalidDataException("Industrial collision/UV/LOD source convention failed.");
        for(var i=0;i<3;i++)if(Math.Abs(p.CollisionSize[i]-size[i])>.006||Math.Abs(p.CollisionCentre[i]-(i==2?size[2]/2:0))>.006)
            throw new InvalidDataException("Industrial collision box differs from its measured solid bounds.");
        if(recipe.IsRefined)_=await ReadRefinedSurface(root,req.Recipe,token);
    }
}
