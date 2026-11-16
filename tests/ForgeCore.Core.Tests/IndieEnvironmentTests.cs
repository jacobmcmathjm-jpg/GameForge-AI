using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndieEnvironmentTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new{schemaVersion=1,profile=IndieEnvironmentEvidence.Profile,source="original_template_geometry_and_procedural_sky",skyWidth=1024,skyHeight=512,addedRenderers=2,addedTriangles=8000,ownedTextureBytes=1000000,addedColliders=0,addedLights=0,collisionUnchanged=true,socketsUnchanged=true,animationsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,stageComplete=false})!.AsObject();
    [Fact]public void BoundedOriginalEnvironmentIsNotArtAcceptance(){using var doc=JsonDocument.Parse(Report().ToJsonString());IndieEnvironmentEvidence.Validate(doc.RootElement);}
    [Theory][InlineData("profile","\"retro_64\"")][InlineData("skyWidth","4096")][InlineData("addedRenderers","3")][InlineData("addedTriangles","12001")][InlineData("ownedTextureBytes","4194305")][InlineData("addedColliders","1")][InlineData("addedLights","1")][InlineData("collisionUnchanged","false")][InlineData("socketsUnchanged","false")][InlineData("animationsChanged","true")][InlineData("audioChanged","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")]
    public void WrongBudgetsChangesAndOverclaimsFail(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var doc=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>IndieEnvironmentEvidence.Validate(doc.RootElement));}
}
