using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class CourtyardHeroEvidenceTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new {
        schemaVersion=1,profile=CourtyardHeroEvidence.Profile,source="original_template_geometry_and_procedural_pbr",lighting="one_prebaked_custom_box_projected_probe_no_added_realtime_lights",
        weaponCount=4,guardCount=3,guardAttachments=36,addedRenderers=44,addedTriangles=12000,addedColliders=0,textureCount=6,textureSize=1024,reflectionResolution=128,
        ownedTextureMemoryBytes=17000000,collisionUnchanged=true,socketsUnchanged=true,opaqueWetOverlayDisabled=true,animationsChanged=false,audioChanged=false,
        visualReviewed=false,rightsReviewed=false,stageComplete=false,approvedLibraryAssetsPlaced=false
    })!.AsObject();
    [Fact]public void AcceptsOnlyTechnicalPresentationNotArtOrNewMotion(){using var json=JsonDocument.Parse(Report().ToJsonString());CourtyardHeroEvidence.Validate(json.RootElement);}
    [Theory][InlineData("schemaVersion","2")][InlineData("profile","\"fake\"")][InlineData("source","\"approved_blender\"")][InlineData("lighting","\"realtime\"")]
    [InlineData("weaponCount","3")][InlineData("guardCount","2")][InlineData("guardAttachments","35")][InlineData("addedRenderers","49")][InlineData("addedTriangles","18001")]
    [InlineData("addedColliders","1")][InlineData("textureCount","5")][InlineData("textureSize","2048")][InlineData("reflectionResolution","256")][InlineData("ownedTextureMemoryBytes","20971521")]
    [InlineData("collisionUnchanged","false")][InlineData("socketsUnchanged","false")][InlineData("opaqueWetOverlayDisabled","false")][InlineData("animationsChanged","true")]
    [InlineData("audioChanged","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")][InlineData("approvedLibraryAssetsPlaced","true")]
    public void RejectsMutatedBoundary(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var json=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>CourtyardHeroEvidence.Validate(json.RootElement));}
}
