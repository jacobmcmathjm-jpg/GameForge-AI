using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndieWeaponTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new{schemaVersion=1,profile=IndieWeaponEvidence.Profile,source="original_code_authored_shared_meshes",weaponCount=4,sharedMeshCount=2,materialCount=3,trianglesPerWeapon=4000,addedRenderers=8,addedColliders=0,disabledLegacyRenderers=80,socketsUnchanged=true,magazineTransformsUnchanged=true,collisionUnchanged=true,sourceHandsChanged=false,animationsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,stageComplete=false})!.AsObject();
    [Fact]public void SharedModelIsNotHandAnatomyOrArtAcceptance(){using var d=JsonDocument.Parse(Report().ToJsonString());IndieWeaponEvidence.Validate(d.RootElement);}
    [Theory][InlineData("weaponCount","3")][InlineData("sharedMeshCount","8")][InlineData("trianglesPerWeapon","12001")][InlineData("addedRenderers","9")][InlineData("addedColliders","1")][InlineData("disabledLegacyRenderers","0")][InlineData("socketsUnchanged","false")][InlineData("magazineTransformsUnchanged","false")][InlineData("collisionUnchanged","false")][InlineData("sourceHandsChanged","true")][InlineData("animationsChanged","true")][InlineData("audioChanged","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")]
    public void ResourceRigAndOverclaimChangesFail(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var d=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>IndieWeaponEvidence.Validate(d.RootElement));}
}
