using System.Text.Json;
namespace ForgeCore.Core;

/// <summary>Original template prop sanity gates, not approval/promotion of library art.</summary>
public static class CourtyardPropEvidence
{
    public const string Profile="industrial_template_props_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Profile||
            r.GetProperty("source").GetString()!="original_code_authored_template_not_blender_or_vault"||
            r.GetProperty("collisionPolicy").GetString()!="existing_conservative_solid_boxes_unchanged"||
            r.GetProperty("propCount").GetInt32()!=10||r.GetProperty("lodGroupCount").GetInt32()!=10||r.GetProperty("lodLevels").GetInt32()!=3||
            r.GetProperty("disabledProxyRenderers").GetInt32()!=10||!r.GetProperty("collisionUnchanged").GetBoolean()||
            r.GetProperty("decorativeColliderCount").GetInt32()!=0||r.GetProperty("addedLights").GetInt32()!=0||
            r.GetProperty("highestDetailTriangles").GetInt32() is <=0 or >60000||r.GetProperty("maximumDrawSlots").GetInt32() is <10 or >40||
            r.GetProperty("textureSize").GetInt32()!=1024||r.GetProperty("textureCount").GetInt32()!=2||
            r.GetProperty("ownedTextureMemoryBytes").GetInt64() is <=0 or >8*1024*1024||
            r.GetProperty("comparisonWidth").GetInt32()!=1440||r.GetProperty("comparisonHeight").GetInt32()!=810||r.GetProperty("viewCount").GetInt32()!=2||
            r.GetProperty("magentaPixels").GetInt32()!=0||r.GetProperty("visualReviewed").GetBoolean()||r.GetProperty("rightsReviewed").GetBoolean()||
            r.GetProperty("approvedLibraryAssetsPlaced").GetBoolean()||r.GetProperty("stageComplete").GetBoolean())
            throw new InvalidDataException("Template prop identity, budget, collision or acceptance boundary failed.");
        var range=r.GetProperty("minimumLuminanceRange").GetDouble();
        if(!double.IsFinite(range)||range is <.08 or >1)throw new InvalidDataException("Template prop image sanity check failed.");
        var families=r.GetProperty("families").EnumerateArray().ToArray();
        if(!families.Select(f=>f.GetProperty("family").GetString()).SequenceEqual(new[]{"barrel","crate","barrier","fence","cargo"}))
            throw new InvalidDataException("Exact five original template families required.");
        var total=0;
        for(var i=0;i<families.Length;i++) {
            var counts=families[i].GetProperty("triangles").EnumerateArray().Select(n=>n.GetInt32()).ToArray();
            if(counts.Length!=3||counts.Any(n=>n is <12 or >16000)||counts[0]<=counts[1]||counts[1]<=counts[2])
                throw new InvalidDataException("Actual three-level template prop LOD budget/order failed.");
            total+=counts[0]*new[]{1,2,2,3,2}[i];
        }
        if(total!=r.GetProperty("highestDetailTriangles").GetInt32())throw new InvalidDataException("Template prop triangle aggregate differs from family identities.");
    }
}
