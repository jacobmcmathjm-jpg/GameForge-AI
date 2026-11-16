using System.Text.Json;
namespace ForgeCore.Core;

/// <summary>Original N64-era-inspired presentation budgets, not hardware emulation or a licensed remake.</summary>
public static class RetroCourtyardProfile
{
    public const string Id="retro_64";
    public const string Recipe="original_retro_industrial_v1";
    public const string Layout="retro_industrial_layout_v1";
    public static IReadOnlyList<IndustrialPlacementFootprint> Placements {get;}=Array.AsReadOnly(IndustrialPlacementLayout.Placements.Select(p=>p with {
        Z=p.PlacementId=="supplies-2"?-5:p.PlacementId=="containers-1"?-2:p.Z
    }).ToArray());
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Id||r.GetProperty("recipe").GetString()!=Recipe||r.GetProperty("layout").GetString()!=Layout||
            r.GetProperty("source").GetString()!="original_template_derivative_no_game_assets"||r.GetProperty("textureCount").GetInt32()!=8||r.GetProperty("maximumTextureSize").GetInt32()!=128||
            r.GetProperty("paletteLevels").GetInt32()!=32||r.GetProperty("guardCount").GetInt32()!=3||r.GetProperty("guardTriangles").GetInt32() is <200 or >2000||
            r.GetProperty("armsTriangles").GetInt32() is <48 or >500||r.GetProperty("weaponTriangles").GetInt32() is <48 or >400||
            r.GetProperty("propLodGroups").GetInt32()!=10||r.GetProperty("activePropTriangles").GetInt32() is <1000 or >6000||
            r.GetProperty("ownedTextureBytes").GetInt64() is <=0 or >1024*1024||r.GetProperty("remainingActiveReflections").GetInt32()!=0||
            r.GetProperty("addedColliders").GetInt32()!=0||!r.GetProperty("socketsUnchanged").GetBoolean()||!r.GetProperty("routesReachable").GetBoolean()||
            r.GetProperty("nominalPlacementConflicts").GetInt32()!=0||r.GetProperty("animationClipsChanged").GetBoolean()||r.GetProperty("audioChanged").GetBoolean()||
            r.GetProperty("visualReviewed").GetBoolean()||r.GetProperty("rightsReviewed").GetBoolean()||r.GetProperty("approvedLibraryAssetsPlaced").GetBoolean()||r.GetProperty("hardwareEmulated").GetBoolean())
            throw new InvalidDataException("Retro courtyard profile identity, geometry/resource, route or acceptance boundary failed.");
    }
}
