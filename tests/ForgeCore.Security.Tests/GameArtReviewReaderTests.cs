using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    private async Task<(ToolCall Call,string Asset,string Preview,string Metadata)> TargetReviewFixture(string? defect=null)
    {
        await CreateBareUnityProjectAsync();var(asset,assetHash,_)=await CreateImportedGeneratedArtAssetAsync();
        var preview=Path.Combine(_root,"Game","Previews","GameArt","frontier-watchtower.png");Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
        await File.WriteAllBytesAsync(preview,MinimalPng(defect=="wrong_png_size"?512u:1280u,720));
        var metadata=preview+".forgecore-art-preview.json";
        var record=new Dictionary<string,object?> {
            ["schemaVersion"]=1,["assetSetId"]="frontier-watchtower",["assetPath"]="Assets/ForgeCore/Generated/frontier-watchtower.glb",
            ["assetSha256"]=assetHash,["provenanceSha256"]=await HashAsync(asset+".asset.json"),["metaSha256"]=await HashAsync(asset+".meta"),
            ["previewPath"]="Previews/GameArt/frontier-watchtower.png",["previewSha256"]=await HashAsync(preview),
            ["editorVersion"]="6000.0.50f1",["bridgeVersion"]="0.17.23",["width"]=1280,["height"]=720,["viewCount"]=4,
            ["rightsReviewed"]=false,["visualReviewed"]=false,["createdUtc"]=DateTimeOffset.UtcNow};
        switch(defect){
            case "wrong_editor":record["editorVersion"]="6000.5.5f1";break;
            case "wrong_bridge":record["bridgeVersion"]="0.0.0";break;
            case "wrong_meta":record["metaSha256"]=new string('a',64);break;
            case "wrong_source":record["provenanceSha256"]=new string('b',64);break;
            case "wrong_views":record["viewCount"]=1;break;
            case "fabricated_visual":record["visualReviewed"]=true;break;
            case "fabricated_rights":record["rightsReviewed"]=true;break;
            case "extra_field":record["approval"]=true;break;
            case "missing_field":record.Remove("metaSha256");break;
            case "wrong_path":record["previewPath"]="Previews/GameArt/other.png";break;
            case "null_path":record["assetPath"]=null;break;
            case "null_preview":record["previewPath"]=null;break;
        }
        await File.WriteAllTextAsync(metadata,JsonSerializer.Serialize(record));
        var call=ReviewGameArtCall("visual",assetHash,await HashAsync(preview),await HashAsync(metadata));
        return(call,asset,preview,metadata);
    }
    [Fact]public async Task TargetReviewReaderReturnsExactFourViewBytesWithoutRecordingAnyDecision()
    {
        var f=await TargetReviewFixture();var reader=new GameArtReviewReader(new WorkspacePathGuard(_root),"6000.0.50f1");
        var before=Directory.GetFiles(_root,"*",SearchOption.AllDirectories).Order().ToArray();
        var packet=await reader.ReadAsync(f.Call,default);
        Assert.Equal(GameArtReviewSpec.FromProposal(f.Call),packet.Proposal);Assert.Equal(await File.ReadAllBytesAsync(f.Preview),packet.PreviewPng);
        Assert.Equal(await HashAsync(f.Asset+".asset.json"),packet.SourceProvenanceSha256);Assert.Contains("not a guarantee of rights",packet.Summary);
        Assert.Equal(before,Directory.GetFiles(_root,"*",SearchOption.AllDirectories).Order().ToArray());
        Assert.False(Directory.Exists(Path.Combine(_root,"Game",".forgecore","unity","art-reviews")));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reader.ReadAsync(f.Call,cancelled.Token));
        await Assert.ThrowsAsync<ArgumentException>(()=>reader.ReadAsync(GameArtReviewSpec.BindUserDecision(f.Call,ApprovalDecision.Approved),default));
    }
    [Theory][InlineData("wrong_png_size")][InlineData("wrong_editor")][InlineData("wrong_bridge")][InlineData("wrong_meta")]
    [InlineData("wrong_source")][InlineData("wrong_views")][InlineData("fabricated_visual")][InlineData("fabricated_rights")]
    [InlineData("extra_field")][InlineData("missing_field")][InlineData("wrong_path")]
    [InlineData("null_path")][InlineData("null_preview")]
    public async Task TargetReviewReaderFailsClosedOnUntrustedMetadata(string defect)
    {
        var f=await TargetReviewFixture(defect);var reader=new GameArtReviewReader(new WorkspacePathGuard(_root),"6000.0.50f1");
        await Assert.ThrowsAsync<InvalidDataException>(()=>reader.ReadAsync(f.Call,default));
    }
    [Theory][InlineData("asset")][InlineData("preview")][InlineData("metadata")][InlineData("unity_meta")][InlineData("source")]
    public async Task TargetReviewReaderRejectsChangedExactInputs(string changed)
    {
        var f=await TargetReviewFixture();var path=changed switch {"asset"=>f.Asset,"preview"=>f.Preview,"metadata"=>f.Metadata,"unity_meta"=>f.Asset+".meta",_=>f.Asset+".asset.json"};
        await File.AppendAllTextAsync(path,"changed");
        await Assert.ThrowsAnyAsync<Exception>(()=>new GameArtReviewReader(new WorkspacePathGuard(_root),"6000.0.50f1").ReadAsync(f.Call,default));
    }
    [Fact]public async Task TargetReviewReaderRejectsOversizedSourceSidecarBeforeParsing()
    {
        var f=await TargetReviewFixture();await File.WriteAllBytesAsync(f.Asset+".asset.json",new byte[1024*1024+1]);
        await Assert.ThrowsAsync<InvalidDataException>(()=>new GameArtReviewReader(new WorkspacePathGuard(_root)).ReadAsync(f.Call,default));
    }
    [Theory][InlineData("license")][InlineData("size")][InlineData("unknown")][InlineData("null_source")]
    public async Task TargetReviewReaderRejectsImportProvenanceEvenWhenPreviewHashesAreRebound(string defect)
    {
        var f=await TargetReviewFixture();var source=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(await File.ReadAllTextAsync(f.Asset+".asset.json"))!;
        source[defect=="license"?"license":defect=="size"?"bytes":defect=="unknown"?"approval":"destinationPath"]=JsonSerializer.SerializeToElement<object?>(defect=="license"?"Line1\nLine2":defect=="size"?987654321:defect=="unknown"?true:null);
        await File.WriteAllTextAsync(f.Asset+".asset.json",JsonSerializer.Serialize(source));
        var metadata=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(await File.ReadAllTextAsync(f.Metadata))!;
        metadata["provenanceSha256"]=JsonSerializer.SerializeToElement(await HashAsync(f.Asset+".asset.json"));
        await File.WriteAllTextAsync(f.Metadata,JsonSerializer.Serialize(metadata));
        var call=f.Call with{Arguments=new Dictionary<string,string>(f.Call.Arguments){["expected_preview_provenance_sha256"]=await HashAsync(f.Metadata)}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>new GameArtReviewReader(new WorkspacePathGuard(_root)).ReadAsync(call,default));
    }
}
