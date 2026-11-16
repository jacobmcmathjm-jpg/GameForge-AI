using System.Text.Json;
namespace ForgeCore.Core;
public static class IndieEnvironmentEvidence
{
    public const string Profile="grounded_indie_environment_v1";
    public static void Validate(JsonElement r)
    {
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("profile").GetString()!=Profile||r.GetProperty("source").GetString()!="original_template_geometry_and_procedural_sky"||
            r.GetProperty("skyWidth").GetInt32()!=1024||r.GetProperty("skyHeight").GetInt32()!=512||r.GetProperty("addedRenderers").GetInt32() is <1 or >2||
            r.GetProperty("addedTriangles").GetInt32() is <100 or >12000||r.GetProperty("ownedTextureBytes").GetInt64() is <=0 or >4*1024*1024||
            r.GetProperty("addedColliders").GetInt32()!=0||r.GetProperty("addedLights").GetInt32()!=0||!r.GetProperty("collisionUnchanged").GetBoolean()||!r.GetProperty("socketsUnchanged").GetBoolean()||
            new[]{"animationsChanged","audioChanged","visualReviewed","rightsReviewed","stageComplete"}.Any(f=>r.GetProperty(f).GetBoolean()))throw new InvalidDataException("Indie environment identity, budget or preservation gate failed.");
    }
}
