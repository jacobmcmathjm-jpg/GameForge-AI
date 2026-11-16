using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class IndieCharacterTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new{schemaVersion=1,profile=IndieCharacterEvidence.Profile,
        source="original_code_authored_derivative_of_hash_bound_blender_skin",fingerMotion="precurled_rigid_to_existing_wrist",guardCount=3,skinCount=4,
        sharedMeshCount=2,gloveDigitsPerHand=5,guardTriangles=30000,armsTriangles=5000,addedColliders=0,addedRenderers=0,
        skeletonUnchanged=true,bindposesUnchanged=true,socketsUnchanged=true,collisionUnchanged=true,sourceMeshPreserved=true,weightsNormalized=true,
        clipsChanged=false,audioChanged=false,individualFingerAnimation=false,visualReviewed=false,rightsReviewed=false,stageComplete=false})!.AsObject();
    [Fact]public void NewGloveGeometryDoesNotClaimFingerAnimationOrHumanAcceptance(){using var d=JsonDocument.Parse(Report().ToJsonString());IndieCharacterEvidence.Validate(d.RootElement);}
    [Theory]
    [InlineData("guardCount","2")][InlineData("skinCount","5")][InlineData("sharedMeshCount","4")][InlineData("gloveDigitsPerHand","4")]
    [InlineData("guardTriangles","45001")][InlineData("armsTriangles","12001")][InlineData("addedColliders","1")][InlineData("addedRenderers","1")]
    [InlineData("skeletonUnchanged","false")][InlineData("bindposesUnchanged","false")][InlineData("socketsUnchanged","false")][InlineData("collisionUnchanged","false")]
    [InlineData("sourceMeshPreserved","false")][InlineData("weightsNormalized","false")][InlineData("clipsChanged","true")][InlineData("audioChanged","true")]
    [InlineData("individualFingerAnimation","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("stageComplete","true")]
    public void RigResourceAndOverclaimFailuresAreRejected(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var d=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>IndieCharacterEvidence.Validate(d.RootElement));}
}
