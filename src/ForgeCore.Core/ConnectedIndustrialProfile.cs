using System.Text.Json;
namespace ForgeCore.Core;
public static class ConnectedIndustrialProfile
{
    public const string Id="industrial_connected_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Id||
            r.GetProperty("source").GetString()!="original_template_extension_with_final_physics_audit"||
            r.GetProperty("gridWidth").GetInt32()!=64||r.GetProperty("gridHeight").GetInt32()!=64||
            r.GetProperty("zones").GetInt32()!=3||r.GetProperty("doors").GetInt32()!=2||r.GetProperty("guardCount").GetInt32()!=3||
            r.GetProperty("renderers").GetInt32() is <1 or >300||r.GetProperty("colliders").GetInt32() is <10 or >100||
            !double.IsFinite(r.GetProperty("routeMetres").GetDouble())||r.GetProperty("routeMetres").GetDouble() is <110 or >350||
            new[]{"spawnSafe","stagedRouteReachable","doorGatingVerified","socketsUnchanged","baseCombatAssetsUnchanged","separateSaveProfile"}.Any(k=>!r.GetProperty(k).GetBoolean())||
            new[]{"stairsSupported","approvedLibraryPlacement","visualReviewed","rightsReviewed","stageComplete","freezeFixed"}.Any(k=>r.GetProperty(k).GetBoolean()))
            throw new InvalidDataException("Connected layout evidence failed its bounded final-physics/preservation contract.");
    }
}
