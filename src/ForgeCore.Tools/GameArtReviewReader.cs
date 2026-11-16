using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeCore.Core;
using ForgeCore.Security;
namespace ForgeCore.Tools;

/// <summary>Verifies the exact bounded source, metadata and four-view PNG before display. No store/editor/model access.</summary>
public sealed class GameArtReviewReader(WorkspacePathGuard paths,string? editorVersion=null):IGameArtReviewReader
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true};
    private sealed record Preview(int SchemaVersion,string AssetSetId,string AssetPath,string AssetSha256,string ProvenanceSha256,
        string MetaSha256,string PreviewPath,string PreviewSha256,string EditorVersion,string BridgeVersion,
        int Width,int Height,int ViewCount,bool RightsReviewed,bool VisualReviewed,DateTimeOffset CreatedUtc);
    private sealed record Imported(string File,string DestinationPath,string SourcePage,string DownloadUrl,string License,string Sha256,
        long Bytes,string Format,string Inspection,DateTimeOffset StagedUtc,DateTimeOffset ImportedUtc);
    public async Task<GameArtReviewPacket> ReadAsync(ToolCall proposal,CancellationToken token)
    {
        var spec=GameArtReviewSpec.FromProposal(proposal);
        var asset=Resolve(spec,spec.AssetPath);var provenance=Resolve(spec,spec.AssetPath+".asset.json");var meta=Resolve(spec,spec.AssetPath+".meta");
        var preview=Resolve(spec,spec.PreviewPath);var previewMetadata=Resolve(spec,spec.PreviewPath+".forgecore-art-preview.json");
        var assetHash=await HashFile(asset,100*1024*1024,token);if(assetHash!=spec.ExpectedAssetSha256)throw new InvalidDataException("Target source changed before review.");
        var sourceBytes=await Read(provenance,1024*1024,token);var metaHash=await HashFile(meta,1024*1024,token);
        var png=await Read(preview,8*1024*1024,token);var previewBytes=await Read(previewMetadata,1024*1024,token);
        var sourceHash=Hash(sourceBytes);
        if(Hash(png)!=spec.ExpectedPreviewSha256||Hash(previewBytes)!=spec.ExpectedPreviewProvenanceSha256)
            throw new InvalidDataException("Target preview/provenance changed before review.");
        Preview p;Imported imported;
        try {
            p=JsonSerializer.Deserialize<Preview>(previewBytes,Json)??throw new InvalidDataException("Empty target preview provenance.");
            imported=JsonSerializer.Deserialize<Imported>(sourceBytes,Json)??throw new InvalidDataException("Empty target import provenance.");
        }catch(JsonException error){throw new InvalidDataException("Target preview/import provenance is malformed or contains unexpected fields.",error);}
        if(p.SchemaVersion!=1||p.AssetSetId!=spec.AssetSetId||p.AssetPath?.Replace('\\','/')!=spec.AssetPath||p.AssetSha256!=assetHash||
            p.ProvenanceSha256!=sourceHash||p.MetaSha256!=metaHash||p.PreviewPath?.Replace('\\','/')!=spec.PreviewPath||p.PreviewSha256!=Hash(png)||
            p.BridgeVersion!=UnityBridgeTemplate.BridgeVersion||string.IsNullOrWhiteSpace(p.EditorVersion)||p.EditorVersion.Length>40||
            editorVersion!=null&&p.EditorVersion!=editorVersion||p.Width!=1280||p.Height!=720||p.ViewCount!=4||
            p.VisualReviewed||p.RightsReviewed||p.CreatedUtc==default)
            throw new InvalidDataException("Target review identity/version/four-view contract failed.");
        if(png.Length<24||!png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||
            BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16,4))!=1280||BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20,4))!=720)
            throw new InvalidDataException("Target review requires a bounded 1280x720 PNG.");
        if(imported.DestinationPath?.Replace('\\','/')!=spec.AssetPath||imported.Sha256!=assetHash||
            imported.File!=Path.GetFileName(asset)||imported.Bytes!=new FileInfo(asset).Length||
            imported.StagedUtc==default||imported.ImportedUtc==default)
            throw new InvalidDataException("Target import provenance does not match the reviewed GLB.");
        var license=imported.License;var page=imported.SourcePage;
        if(string.IsNullOrWhiteSpace(license)||license.Length>200||string.IsNullOrWhiteSpace(page)||page.Length>2000||license.Any(char.IsControl)||page.Any(char.IsControl))
            throw new InvalidDataException("Target rights metadata is missing or unbounded.");
        var summary=$"Project: {spec.ProjectPath}\nAsset set: {spec.AssetSetId}\nImported GLB: {spec.AssetPath}\nUnity: {p.EditorVersion}; bridge: {p.BridgeVersion}\n"+
            $"Verified target preview: {p.Width}x{p.Height}, {p.ViewCount} views\nReported source: {page}\nReported licence: {license}\n"+
            "Reported provenance is evidence to inspect, not a guarantee of rights. This is an imported-asset review, not whole-level appearance, collision/navigation, performance or stage acceptance.";
        return new(spec,sourceHash,metaHash,summary,png);
    }
    private string Resolve(GameArtReviewSpec spec,string relative)
    {
        var root=paths.Resolve(spec.ProjectPath,false);var full=paths.Resolve(Path.Combine(spec.ProjectPath,relative),false);
        if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Target review path escaped its project.");
        return full;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task<byte[]> Read(string path,int maximum,CancellationToken token)
    {
        await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length is <1||file.Length>maximum)throw new InvalidDataException("Target review resource exceeds its size bound.");
        var data=new byte[(int)file.Length];await file.ReadExactlyAsync(data,token);if(file.ReadByte()!=-1)throw new InvalidDataException("Target review resource changed while reading.");return data;
    }
    private static async Task<string> HashFile(string path,int maximum,CancellationToken token)
    {
        await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length is <1||file.Length>maximum)throw new InvalidDataException("Target review source exceeds its size bound.");
        return Convert.ToHexString(await SHA256.HashDataAsync(file,token)).ToLowerInvariant();
    }
}
