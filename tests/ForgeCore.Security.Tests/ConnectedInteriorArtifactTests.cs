using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    private async Task<string> InteriorFixture()
    {
        const string relative="Game/Assets/ConnectedReview";var folder=Path.Combine(_root,relative);Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder,"connected-interior-report.json"),JsonSerializer.Serialize(new{schemaVersion=1,profile=ConnectedInteriorEvidence.Id,source="original_code_authored_geometry_and_screen_pattern",screenWidth=256,screenHeight=128,partsBeforeMaterialBatch=105,addedTriangles=4620,addedColliders=0,additionalMaterialBatches=1,screenTextureBytes=102639,collisionUnchanged=true,visualReviewed=false,rightsReviewed=false,stageComplete=false,animationsChanged=false,audioChanged=false}));
        await File.WriteAllTextAsync(Path.Combine(folder,"connected-interior-review.json"),JsonSerializer.Serialize(new{schemaVersion=1,profile=ConnectedInteriorEvidence.Id,scope=ConnectedInteriorEvidence.ComparisonScope,baseline=ConnectedInteriorEvidence.Baseline,width=1536,height=1296,viewCount=3,magentaPixels=0,changedPixels=1000,protectedPoseTransforms=99,combatAndObjectivePosesUnchanged=true,humanArtAccepted=false,stageComplete=false,views=new[]{"warehouse_entry","warehouse_reverse","control_room_workstations"}}));
        await File.WriteAllBytesAsync(Path.Combine(folder,"ConnectedScreen.png"),InteriorFixturePng(256,128));
        await File.WriteAllBytesAsync(Path.Combine(folder,"connected-interior-review.png"),InteriorFixturePng(1536,1296));return relative;
    }
    [Fact]public async Task InteriorArtifactReaderUsesExactOwnedReportAndPngContracts(){var relative=await InteriorFixture();var result=await ConnectedInteriorArtifactValidator.ValidateAsync(_root,relative);Assert.False(result.Detail.GetProperty("visualReviewed").GetBoolean());Assert.False(result.Comparison.GetProperty("humanArtAccepted").GetBoolean());}
    [Fact]public async Task InteriorArtifactReaderRejectsActualPngDimensionMismatch(){var relative=await InteriorFixture();await File.WriteAllBytesAsync(Path.Combine(_root,relative,"ConnectedScreen.png"),InteriorFixturePng(128,128));await Assert.ThrowsAsync<InvalidDataException>(()=>ConnectedInteriorArtifactValidator.ValidateAsync(_root,relative));}
    [Fact]public async Task InteriorArtifactReaderRejectsClaimedHumanApproval(){var relative=await InteriorFixture();var path=Path.Combine(_root,relative,"connected-interior-review.json");var json=JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();json["humanArtAccepted"]=true;await File.WriteAllTextAsync(path,json.ToJsonString());await Assert.ThrowsAsync<InvalidDataException>(()=>ConnectedInteriorArtifactValidator.ValidateAsync(_root,relative));}
    [Fact]public async Task InteriorArtifactReaderRejectsOversizedJson(){var relative=await InteriorFixture();await File.WriteAllTextAsync(Path.Combine(_root,relative,"connected-interior-report.json"),new string(' ',32769));await Assert.ThrowsAsync<InvalidDataException>(()=>ConnectedInteriorArtifactValidator.ValidateAsync(_root,relative));}
    [Fact]public async Task InteriorArtifactReaderCannotReadOutsideAuthorizedProject(){Directory.CreateDirectory(_root);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>ConnectedInteriorArtifactValidator.ValidateAsync(_root,"../outside"));}
    private static byte[] InteriorFixturePng(int width,int height)
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string name,byte[] data){var type=Encoding.ASCII.GetBytes(name);var length=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(length,(uint)data.Length);output.Write(length);output.Write(type);output.Write(data);uint crc=uint.MaxValue;foreach(var value in type.Concat(data)){crc^=value;for(var bit=0;bit<8;bit++)crc=(crc&1)!=0?0xedb88320u^(crc>>1):crc>>1;}BinaryPrimitives.WriteUInt32BigEndian(length,~crc);output.Write(length);}
        var header=new byte[13];BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0,4),(uint)width);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4,4),(uint)height);header[8]=8;header[9]=6;Chunk("IHDR",header);
        using var packed=new MemoryStream();using(var zip=new ZLibStream(packed,CompressionLevel.Fastest,true)){var row=new byte[width*4+1];for(var x=0;x<width;x++){row[1+x*4]=20;row[2+x*4]=40;row[3+x*4]=60;row[4+x*4]=255;}for(var y=0;y<height;y++)zip.Write(row);}
        Chunk("IDAT",packed.ToArray());Chunk("IEND",[]);return output.ToArray();
    }
}
