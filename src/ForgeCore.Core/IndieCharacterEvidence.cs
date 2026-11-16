using System.Text.Json;
namespace ForgeCore.Core;
public static class IndieCharacterEvidence
{
    public const string Profile="original_indie_character_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Profile||
            r.GetProperty("source").GetString()!="original_code_authored_derivative_of_hash_bound_blender_skin"||
            r.GetProperty("fingerMotion").GetString()!="precurled_rigid_to_existing_wrist"||
            r.GetProperty("guardCount").GetInt32()!=3||r.GetProperty("skinCount").GetInt32()!=4||
            r.GetProperty("sharedMeshCount").GetInt32()!=2||r.GetProperty("gloveDigitsPerHand").GetInt32()!=5||
            r.GetProperty("guardTriangles").GetInt32() is <10000 or >45000||r.GetProperty("armsTriangles").GetInt32() is <1000 or >12000||
            r.GetProperty("addedColliders").GetInt32()!=0||r.GetProperty("addedRenderers").GetInt32()!=0||
            new[]{"skeletonUnchanged","bindposesUnchanged","socketsUnchanged","collisionUnchanged","sourceMeshPreserved","weightsNormalized"}.Any(f=>!r.GetProperty(f).GetBoolean())||
            new[]{"clipsChanged","audioChanged","individualFingerAnimation","visualReviewed","rightsReviewed","stageComplete"}.Any(f=>r.GetProperty(f).GetBoolean()))
            throw new InvalidDataException("Indie character geometry, rig, preservation or acceptance gate failed.");
    }
}
