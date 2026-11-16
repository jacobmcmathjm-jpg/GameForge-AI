using System.Text.Json;
namespace ForgeCore.Core;
public static class IndieWeaponEvidence
{
    public const string Profile="original_indie_rifle_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Profile||r.GetProperty("source").GetString()!="original_code_authored_shared_meshes"||r.GetProperty("weaponCount").GetInt32()!=4||r.GetProperty("sharedMeshCount").GetInt32()!=2||r.GetProperty("materialCount").GetInt32()!=3||r.GetProperty("trianglesPerWeapon").GetInt32() is <100 or >12000||r.GetProperty("addedRenderers").GetInt32()!=8||r.GetProperty("addedColliders").GetInt32()!=0||r.GetProperty("disabledLegacyRenderers").GetInt32()<40||
            new[]{"socketsUnchanged","magazineTransformsUnchanged","collisionUnchanged"}.Any(f=>!r.GetProperty(f).GetBoolean())||new[]{"sourceHandsChanged","animationsChanged","audioChanged","visualReviewed","rightsReviewed","stageComplete"}.Any(f=>r.GetProperty(f).GetBoolean()))throw new InvalidDataException("Indie rifle identity, rig, resource or acceptance gate failed.");
    }
}
