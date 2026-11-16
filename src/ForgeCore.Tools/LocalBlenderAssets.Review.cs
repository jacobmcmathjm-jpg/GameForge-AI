using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;

namespace ForgeCore.Tools;

/// <summary>Read-only exact local source/preview loader for the human dialog; never writes attestations.</summary>
public sealed class LocalBlenderReviewReader(WorkspacePathGuard paths):ILocalBlenderReviewReader
{
    private readonly LocalBlenderAssets _assets=new(paths,null,"5.2",600);
    public Task<LocalBlenderReviewPacket> ReadAsync(string bundleSha256,CancellationToken token)=>_assets.ReadReviewPacketAsync(bundleSha256,token);
}

internal sealed partial class LocalBlenderAssets
{
    public async Task<LocalBlenderReviewPacket> ReadReviewPacketAsync(string hash,CancellationToken token)
    {
        if(hash is null||hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigit(c)))throw new ArgumentException("bundle_sha256 must be an exact SHA-256.");
        hash=hash.ToLowerInvariant();
        var bundle=await VerifyBundle(hash,token);
        var preview=paths.Resolve($"{Library}/objects/{hash}/preview.png",false);
        if(new FileInfo(preview).Length>8*1024*1024)throw new InvalidDataException("Review preview exceeds its 8 MiB display bound.");
        var png=await File.ReadAllBytesAsync(preview,token);
        var width=BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20,4));
        if(width is < 1 or > 4096||height is < 1 or > 4096||(long)width*height>8_388_608)
            throw new InvalidDataException("Preview decoded dimensions exceed display limits.");
        string HashOf(string name)=>bundle.Files.Single(f=>f.Name==name).Sha256;
        if(Hash(png)!=HashOf("preview.png"))throw new InvalidDataException("Review preview changed during loading.");
        var identity=new LocalBlenderReviewIdentity(hash,HashOf("asset.glb"),HashOf("model.blend"),HashOf("preview.png"),HashOf("metrics.json"));
        var m=bundle.Metrics;
        string Vector(double[] v)=>string.Join(", ",v.Select(n=>n.ToString("0.###",CultureInfo.InvariantCulture)));
        var summary=$"Name: {bundle.Request.AssetName}\nRecipe: {bundle.Request.Recipe}\n"+
            $"Source height: {bundle.Request.HeightMillimetres} mm\nBounds: [{Vector(m.BoundsMin)}] to [{Vector(m.BoundsMax)}]\n"+
            $"Coordinates: Blender Z-up, metres; pivot: {m.Pivot}\nVertices: {m.Vertices}; triangles: {m.Triangles}; materials: {m.Materials}\n"+
            $"Maps: {m.TextureResolution}px source maps; source motion clips: {m.AnimationCount}\nRigged: {BlenderAssetSpec.IsCharacter(bundle.Request.Recipe)}\n"+
            $"Recipe SHA-256: {bundle.Request.RecipeSha256}\nBlender source version: {m.BlenderVersion}\n"+
            "Provenance: original fixed local recipe; generated-source usage rights still require your independent review.\n"+
            "This is a source preview, not a Unity/runtime benchmark. Approval does not approve target-game collision, scale, LOD, animation compatibility, performance, placement or stage completion.";
        summary+="\nCurrent source visual review: "+await LatestReviewAsync(identity,"visual",token)+"\nCurrent source rights review: "+await LatestReviewAsync(identity,"rights",token);
        if(IndustrialPropRecipes.Find(bundle.Request.Recipe)?.IsRefined==true) {
            var surface=await ReadRefinedSurface(paths.Resolve($"{Library}/objects/{hash}",false),bundle.Request.Recipe,token);
            summary+=$"\nRefined surface recipe: {surface.Revision}; roughness {surface.RoughnessMinimum:0.###}–{surface.RoughnessMaximum:0.###}; maximum normal XY {surface.NormalMaximumXY:0.###}. Technical measurements are not appearance acceptance.";
        }
        return new(identity,bundle.Request.AssetName,bundle.Request.Recipe,summary,
            $"{Library}/objects/{hash}/model.blend",$"{Library}/objects/{hash}/recipe.py",png);
    }
    public async Task<string> InspectReviewAsync(LocalBlenderInspectSpec spec,CancellationToken token)
    {
        var packet=await ReadReviewPacketAsync(spec.BundleSha256,token);
        var bundle=await VerifyBundle(spec.BundleSha256,token);
        var profile=BlenderAssetSpec.IsCharacter(bundle.Request.Recipe)||IndustrialPropRecipes.Find(bundle.Request.Recipe)?.IsRefined==true
            ? BlenderSourceQualityEvidence.ProfileVersion : "legacy_source_measurements_v1";
        var quality=new BlenderSourceQualityEvidence(1,profile,bundle.Request.Recipe,
            packet.Identity.AssetSha256,bundle.Metrics.Triangles,bundle.Metrics.TextureResolution,true,true,
            bundle.Files.Any(f=>f.Name=="normal.png"),IndustrialPropRecipes.Contains(bundle.Request.Recipe)?3:1,
            BlenderAssetSpec.IsCharacter(bundle.Request.Recipe),true,false);
        return JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_review_packet",identity=packet.Identity,
            packet.AssetName,packet.Recipe,packet.Summary,packet.SourcePath,packet.RecipePath,
            previewPath=$"{Library}/objects/{spec.BundleSha256}/preview.png",sourceVisualReview=await LatestReviewAsync(packet.Identity,"visual",token),
            sourceRightsReview=await LatestReviewAsync(packet.Identity,"rights",token),targetProjectApproved=false,
            learningPlan=await ReadLearningPlanAsync(spec.BundleSha256,token),quality,
            sourceVisualEvidence=await LatestReviewInfoAsync(packet.Identity,"visual",token),
            sourceRightsEvidence=await LatestReviewInfoAsync(packet.Identity,"rights",token)},Json);
    }
    public Task<string> RecordReviewAsync(ToolCall call,CancellationToken token)=>WithSourceDecisionLease(async()=>
    {
        var spec=LocalBlenderReviewSpec.FromExecution(call);
        var current=await ReadReviewPacketAsync(spec.Identity.BundleSha256,token);
        if(current.Identity!=spec.Identity)throw new InvalidDataException("Exact local source/preview/metrics changed after review was requested; no decision recorded.");
        var directory=paths.Resolve($"{Library}/reviews/{spec.Identity.BundleSha256}",true);
        Directory.CreateDirectory(directory);
        // Each live call owns one create-only record. Source objects and all prior decisions remain immutable.
        var target=paths.Resolve(Path.Combine(directory,call.Id.ToString("N")+".json"),true);
        var record=new SourceReview(1,spec.Identity,spec.ReviewKind,spec.Decision!,spec.AttestationSource!,DateTimeOffset.UtcNow,
            "Source-only human decision; target-project Unity and placement acceptance remain separate.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(record,Json);
        token.ThrowIfCancellationRequested();
        await WriteNew(target,bytes,token);
        await WriteNew(paths.Resolve(target+".sha256",true),Encoding.ASCII.GetBytes(Hash(bytes)),token);
        return JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_review",identity=spec.Identity,
            reviewKind=spec.ReviewKind,decision=spec.Decision,recordPath=$"{Library}/reviews/{spec.Identity.BundleSha256}/{call.Id:N}.json",
            recordSha256=Hash(bytes),targetProjectApproved=false},Json);
    },token);
    private sealed record SourceReview(int SchemaVersion,LocalBlenderReviewIdentity Identity,string ReviewKind,string Decision,string AttestationSource,DateTimeOffset CreatedUtc,string Qualification);
    private sealed record SourceReviewStatus(string State,string? RecordSha256=null,string? RecordPath=null);
    private async Task<string> LatestReviewAsync(LocalBlenderReviewIdentity identity,string kind,CancellationToken token)=>(await LatestReviewInfoAsync(identity,kind,token)).State;
    private async Task<SourceReviewStatus> LatestReviewInfoAsync(LocalBlenderReviewIdentity identity,string kind,CancellationToken token)
    {
        var directory=paths.Resolve($"{Library}/reviews/{identity.BundleSha256}",true);
        if(!Directory.Exists(directory))return new("pending");
        SourceReview? latest=null;SourceReviewStatus status=new("pending");var count=0;
        foreach(var digest in Directory.EnumerateFiles(directory,"*.json.sha256"))
        {
            token.ThrowIfCancellationRequested();if(++count>128)return new("review_limit_reached");
            try{if(!File.Exists(paths.Resolve(digest[..^7],false)))return new("invalid_review");}
            catch(UnauthorizedAccessException){return new("invalid_review");}
        }
        count=0;
        foreach(var file in Directory.EnumerateFiles(directory,"*.json"))
        {
            token.ThrowIfCancellationRequested();if(++count>128)return new("review_limit_reached");
            try
            {
                var path=paths.Resolve(file,false);var digest=paths.Resolve(path+".sha256",false);
                var recordBytes=await ReadSourceStateBytes(path,16*1024,token);var recordHash=Hash(recordBytes);
                if(!File.Exists(digest)||new FileInfo(digest).Length!=64||await File.ReadAllTextAsync(digest,token)!=recordHash)return new("invalid_review");
                var r=JsonSerializer.Deserialize<SourceReview>(recordBytes,Json);
                if(r is null||r.SchemaVersion!=1||r.Identity is null||r.ReviewKind is not ("visual" or "rights")||
                    r.AttestationSource!=GameArtReviewSpec.UserLiveApproval||r.Decision is not ("approved" or "rejected")||r.CreatedUtc==default)
                    return new("invalid_review");
                if(r.Identity!=identity||r.ReviewKind!=kind)continue;
                // Stable tie-break: rejection wins, then exact record hash. Enumeration order cannot change the bound version.
                if(latest is null||r.CreatedUtc>latest.CreatedUtc||r.CreatedUtc==latest.CreatedUtc&&
                    (r.Decision=="rejected"&&latest.Decision!="rejected"||r.Decision==latest.Decision&&string.CompareOrdinal(recordHash,status.RecordSha256)<0))
                {latest=r;status=new(r.Decision,recordHash,$"{Library}/reviews/{identity.BundleSha256}/{Path.GetFileName(path)}");}
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {return new("invalid_review");}
        }
        return status;
    }
}
