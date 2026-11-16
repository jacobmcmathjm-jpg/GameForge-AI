using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
namespace ForgeCore.Core.Tests;
public sealed class CourtyardPropEvidenceTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new {
        schemaVersion=1,profile=CourtyardPropEvidence.Profile,source="original_code_authored_template_not_blender_or_vault",collisionPolicy="existing_conservative_solid_boxes_unchanged",
        propCount=10,lodGroupCount=10,lodLevels=3,disabledProxyRenderers=10,collisionUnchanged=true,decorativeColliderCount=0,addedLights=0,
        highestDetailTriangles=10000,maximumDrawSlots=24,textureSize=1024,textureCount=2,ownedTextureMemoryBytes=5600000,
        comparisonWidth=1440,comparisonHeight=810,viewCount=2,magentaPixels=0,minimumLuminanceRange=.8,
        visualReviewed=false,rightsReviewed=false,approvedLibraryAssetsPlaced=false,stageComplete=false,
        families=new[]{"barrel","crate","barrier","fence","cargo"}.Select(f=>new{family=f,triangles=new[]{1000,500,100}}).ToArray()
    })!.AsObject();
    private static void Validate(JsonObject report){using var json=JsonDocument.Parse(report.ToJsonString());CourtyardPropEvidence.Validate(json.RootElement);}
    [Fact]public void TechnicalOriginalTemplateReportIsNotLibraryOrHumanAcceptance()=>Validate(Report());
    [Theory]
    [InlineData("schemaVersion","2")][InlineData("profile","\"other\"")][InlineData("source","\"reviewed_blender\"")]
    [InlineData("collisionPolicy","\"new_mesh_collision\"")][InlineData("propCount","9")][InlineData("lodGroupCount","9")][InlineData("lodLevels","2")]
    [InlineData("disabledProxyRenderers","9")][InlineData("collisionUnchanged","false")][InlineData("decorativeColliderCount","1")][InlineData("addedLights","1")]
    [InlineData("highestDetailTriangles","60001")][InlineData("highestDetailTriangles","9999")][InlineData("maximumDrawSlots","41")][InlineData("maximumDrawSlots","9")]
    [InlineData("textureSize","512")][InlineData("textureCount","3")][InlineData("ownedTextureMemoryBytes","8388609")][InlineData("ownedTextureMemoryBytes","0")]
    [InlineData("comparisonWidth","1280")][InlineData("comparisonHeight","720")][InlineData("viewCount","1")][InlineData("magentaPixels","1")]
    [InlineData("minimumLuminanceRange","0.079")][InlineData("minimumLuminanceRange","1.01")]
    [InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("approvedLibraryAssetsPlaced","true")][InlineData("stageComplete","true")]
    public void RejectsBoundaryDefect(string field,string value){var report=Report();report[field]=JsonNode.Parse(value);Assert.Throws<InvalidDataException>(()=>Validate(report));}
    [Theory][InlineData("reorder")][InlineData("missing")][InlineData("duplicate")][InlineData("no-reduction")][InlineData("negative")][InlineData("oversized")][InlineData("extra-lod")]
    public void RejectsWrongOrNondecreasingFamilyLods(string defect) {
        var report=Report();var families=report["families"]!.AsArray();
        if(defect=="reorder"){families[0]!["family"]="crate";families[1]!["family"]="barrel";}
        else if(defect=="missing")families.RemoveAt(4);
        else if(defect=="duplicate")families[4]!["family"]="barrel";
        else families[0]!["triangles"]=JsonSerializer.SerializeToNode(defect=="negative"?new[]{1000,500,-1}:defect=="oversized"?new[]{16001,500,100}:defect=="extra-lod"?new[]{1000,500,100,20}:new[]{1000,1000,100});
        Assert.Throws<InvalidDataException>(()=>Validate(report));
    }
}
