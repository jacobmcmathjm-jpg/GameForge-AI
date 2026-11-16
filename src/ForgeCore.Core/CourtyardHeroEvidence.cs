using System.Text.Json;
namespace ForgeCore.Core;
/// <summary>Presentation-only original hero finish; deliberately not an animation/art acceptance score.</summary>
public static class CourtyardHeroEvidence
{
    public const string Profile="industrial_hero_finish_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Profile||r.GetProperty("source").GetString()!="original_template_geometry_and_procedural_pbr"||
            r.GetProperty("lighting").GetString()!="one_prebaked_custom_box_projected_probe_no_added_realtime_lights"||
            r.GetProperty("weaponCount").GetInt32()!=4||r.GetProperty("guardCount").GetInt32()!=3||r.GetProperty("guardAttachments").GetInt32()!=36||
            r.GetProperty("addedRenderers").GetInt32() is <40 or >48||r.GetProperty("addedTriangles").GetInt32() is <1000 or >18000||r.GetProperty("addedColliders").GetInt32()!=0||
            r.GetProperty("textureCount").GetInt32()!=6||r.GetProperty("textureSize").GetInt32()!=1024||r.GetProperty("reflectionResolution").GetInt32()!=128||
            r.GetProperty("ownedTextureMemoryBytes").GetInt64() is <=0 or >20*1024*1024||
            !r.GetProperty("collisionUnchanged").GetBoolean()||!r.GetProperty("socketsUnchanged").GetBoolean()||!r.GetProperty("opaqueWetOverlayDisabled").GetBoolean()||
            r.GetProperty("animationsChanged").GetBoolean()||r.GetProperty("audioChanged").GetBoolean()||r.GetProperty("visualReviewed").GetBoolean()||
            r.GetProperty("rightsReviewed").GetBoolean()||r.GetProperty("stageComplete").GetBoolean()||r.GetProperty("approvedLibraryAssetsPlaced").GetBoolean())
            throw new InvalidDataException("Hero finish identity, collision/socket, resource or acceptance gate failed.");
    }
}
