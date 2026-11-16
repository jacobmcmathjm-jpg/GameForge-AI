using System.Buffers.Binary;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
namespace ForgeCore.Tools;
/// <summary>Read-only technical validation inside an already authorized Unity project. No approval or evidence writes.</summary>
public static class ConnectedInteriorArtifactValidator
{
    public static async Task<(JsonElement Detail,JsonElement Comparison)> ValidateAsync(string projectRoot,string relativeFolder,CancellationToken token=default)
    {
        if(!Path.IsPathFullyQualified(projectRoot)||!Directory.Exists(projectRoot))throw new DirectoryNotFoundException("An existing authorized Unity project is required; validation never creates one.");
        if(Path.IsPathRooted(relativeFolder))throw new UnauthorizedAccessException("Interior artifact folders must be relative to the authorized project.");
        var guard=new WorkspacePathGuard(projectRoot);
        string Owned(string name)=>guard.Resolve(Path.Combine(relativeFolder,name),false);
        async Task<JsonElement> Read(string name)
        {
            await using var stream=new FileStream(Owned(name),FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length is <2 or >32768)throw new InvalidDataException("Connected interior JSON is missing or exceeds 32 KiB.");
            using var json=await JsonDocument.ParseAsync(stream,new JsonDocumentOptions{MaxDepth=32},token);
            return json.RootElement.Clone();
        }
        var detail=await Read("connected-interior-report.json");ConnectedInteriorEvidence.ValidateDetail(detail);
        var comparison=await Read("connected-interior-review.json");ConnectedInteriorEvidence.ValidateComparison(comparison);
        foreach(var item in new[]{(Name:"ConnectedScreen.png",Width:256u,Height:128u),(Name:"connected-interior-review.png",Width:1536u,Height:1296u)})
        {
            var path=Owned(item.Name);
            if(new FileInfo(path).Length is <24 or >30*1024*1024)throw new InvalidDataException("Connected interior PNG is missing or oversized.");
            _=await StagedAssetInspector.InspectPngAsync(path,token);
            await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            var header=new byte[24];await stream.ReadExactlyAsync(header,token);
            if(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=item.Width||BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=item.Height)
                throw new InvalidDataException("Connected interior PNG dimensions differ from the exact report contract.");
        }
        return(detail,comparison);
    }
}
