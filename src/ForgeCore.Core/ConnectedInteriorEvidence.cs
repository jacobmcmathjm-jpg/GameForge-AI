using System.Text.Json;
namespace ForgeCore.Core;
/// <summary>Technical preparation contract only; never grants human art/rights or whole-stage acceptance.</summary>
public static class ConnectedInteriorEvidence
{
    public const string Id="industrial_connected_interior_v1";
    public const string ComparisonScope="static_editor_comparison_left_before_right_after_not_runtime_animation";
    public const string Baseline="fresh_same_output_before_optional_interior_dressing_not_previous_v6_binary";
    public static void ValidateDetail(JsonElement r)=>Schema(()=>{
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Id||
            r.GetProperty("source").GetString()!="original_code_authored_geometry_and_screen_pattern"||
            r.GetProperty("screenWidth").GetInt32()!=256||r.GetProperty("screenHeight").GetInt32()!=128||
            r.GetProperty("partsBeforeMaterialBatch").GetInt32() is <1 or >180||
            r.GetProperty("addedTriangles").GetInt32() is <1 or >20000||
            r.GetProperty("addedColliders").GetInt32()!=0||r.GetProperty("additionalMaterialBatches").GetInt32()!=1||
            r.GetProperty("screenTextureBytes").GetInt64() is <1 or >131072||!r.GetProperty("collisionUnchanged").GetBoolean()||
            new[]{"visualReviewed","rightsReviewed","stageComplete","animationsChanged","audioChanged"}.Any(k=>r.GetProperty(k).GetBoolean()))
            throw new InvalidDataException("Connected interior detail exceeds its original visual-only preparation contract.");
    });
    public static void ValidateComparison(JsonElement r)=>Schema(()=>{
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Id||
            r.GetProperty("scope").GetString()!=ComparisonScope||r.GetProperty("baseline").GetString()!=Baseline||
            r.GetProperty("width").GetInt32()!=1536||r.GetProperty("height").GetInt32()!=1296||r.GetProperty("viewCount").GetInt32()!=3||
            r.GetProperty("magentaPixels").GetInt32()!=0||r.GetProperty("changedPixels").GetInt32() is <1000 or >995328||
            r.GetProperty("protectedPoseTransforms").GetInt32() is <10 or >1024||!r.GetProperty("combatAndObjectivePosesUnchanged").GetBoolean()||
            r.GetProperty("humanArtAccepted").GetBoolean()||r.GetProperty("stageComplete").GetBoolean()||
            !r.GetProperty("views").EnumerateArray().Select(v=>v.GetString()).SequenceEqual(new[]{"warehouse_entry","warehouse_reverse","control_room_workstations"},StringComparer.Ordinal))
            throw new InvalidDataException("Connected interior comparison is not an exact unaccepted static same-pose review.");
    });
    private static void Schema(Action check)
    {
        try{check();}
        catch(Exception e)when(e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {throw new InvalidDataException("Connected interior evidence has missing or incorrectly typed contract fields.",e);}
    }
}
