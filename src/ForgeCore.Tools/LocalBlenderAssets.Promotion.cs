using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

internal sealed partial class LocalBlenderAssets
{
    private static readonly SemaphoreSlim SourceDecisionGate=new(1,1);
    private sealed record SourcePromotion(int SchemaVersion,string Tier,LocalBlenderReviewIdentity Identity,
        string VisualReviewSha256,string RightsReviewSha256,string Recipe,string RecipeSha256,string BlenderVersion,
        string AssetRole,int HeightMillimetres,int Triangles,bool Rigged,string Qualification);
    private const string SourceTier="human_reviewed_local_source";
    private const string SourceQualification="Source only. Fresh target Unity inspection, materials, scale, collision, LOD, rig/animation compatibility, performance and placement acceptance are required.";

    private static async Task<byte[]> ReadSourceStateBytes(string path,int maximum,CancellationToken token)
    {
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length is < 1||stream.Length>maximum)throw new InvalidDataException("Source state exceeds bounded metadata size.");
        var bytes=new byte[(int)stream.Length];await stream.ReadExactlyAsync(bytes,token);
        if(stream.ReadByte()!=-1)throw new InvalidDataException("Source state changed during read.");
        return bytes;
    }

    private async Task<string> WithSourceDecisionLease(Func<Task<string>> action,CancellationToken token)
    {
        await SourceDecisionGate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(paths.Resolve(Library,true));
            // Shared by source decisions and promotion across app instances. Never erase a stale lease or replay a write.
            using var lease=new FileStream(paths.Resolve(Library+"/source-decisions.lease",true),FileMode.CreateNew,
                FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose);
            return await action();
        }
        finally{SourceDecisionGate.Release();}
    }
    private static LocalBlenderReviewIdentity IdentityOf(string hash,Bundle bundle)
    {
        string FileHash(string name)=>bundle.Files.Single(f=>f.Name==name).Sha256;
        return new(hash,FileHash("asset.glb"),FileHash("model.blend"),FileHash("preview.png"),FileHash("metrics.json"));
    }
    private static SourcePromotion PromotionFor(Bundle bundle,LocalBlenderReviewIdentity identity,SourceReviewStatus visual,SourceReviewStatus rights)=>
        new(1,SourceTier,identity,visual.RecordSha256!,rights.RecordSha256!,bundle.Request.Recipe,bundle.Request.RecipeSha256,bundle.Request.BlenderVersion,
            BlenderAssetSpec.IsCharacter(bundle.Request.Recipe)?"characters":"environment_props",bundle.Request.HeightMillimetres,bundle.Metrics.Triangles,
            BlenderAssetSpec.IsCharacter(bundle.Request.Recipe),SourceQualification);

    public async Task<string> CheckCompatibilityAsync(LocalBlenderCompatibilitySpec spec,CancellationToken token)
    {
        var bundle=await VerifyBundle(spec.BundleSha256,token);var identity=IdentityOf(spec.BundleSha256,bundle);
        var visual=await LatestReviewInfoAsync(identity,"visual",token);var rights=await LatestReviewInfoAsync(identity,"rights",token);
        var role=BlenderAssetSpec.IsCharacter(bundle.Request.Recipe)?"characters":"environment_props";var c=spec.Constraints;
        var reasons=new List<string>();
        if(c.AssetRole is not null&&c.AssetRole!=role)reasons.Add("asset_role_mismatch");
        if(c.Recipe is not null&&c.Recipe!=bundle.Request.Recipe)reasons.Add("recipe_mismatch");
        if(c.RigRequired&&!BlenderAssetSpec.IsCharacter(bundle.Request.Recipe))reasons.Add("rig_required");
        if(bundle.Request.HeightMillimetres<c.MinimumHeightMillimetres||bundle.Request.HeightMillimetres>c.MaximumHeightMillimetres)reasons.Add("source_height_out_of_range");
        if(bundle.Metrics.Triangles>c.MaximumTriangles)reasons.Add("triangle_budget_exceeded");
        var current=visual.State=="approved"&&rights.State=="approved";
        var promotionState=await PromotionStateAsync(bundle,identity,visual,rights,token);
        return JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_compatibility",identity,
            assetRole=role,recipe=bundle.Request.Recipe,heightMillimetres=bundle.Request.HeightMillimetres,bundle.Metrics.Triangles,
            boundsMin=bundle.Metrics.BoundsMin,boundsMax=bundle.Metrics.BoundsMax,bundle.Metrics.Materials,bundle.Metrics.TextureResolution,
            coordinateSystem="Blender Z-up metres; target Unity measurements still required",bundle.Metrics.Pivot,
            rigged=BlenderAssetSpec.IsCharacter(bundle.Request.Recipe),sourceAnimationCount=bundle.Metrics.AnimationCount,
            matchesSourceConstraints=reasons.Count==0,incompatibilities=reasons,sourceVisualReview=visual,sourceRightsReview=rights,
            eligibleForSourcePromotion=current&&reasons.Count==0&&promotionState is not ("invalid_promotion" or "promotion_limit_reached"),sourcePromotionState=promotionState,
            targetProjectApproved=false,targetUnityInspectionRequired=true,animationCompatibility="not_measured_in_target",
            qualification=SourceQualification},Json);
    }
    public Task<string> PromoteAsync(LocalBlenderPromotionSpec spec,CancellationToken token)=>WithSourceDecisionLease(async()=>
    {
        var bundle=await VerifyBundle(spec.Identity.BundleSha256,token);var identity=IdentityOf(spec.Identity.BundleSha256,bundle);
        if(identity!=spec.Identity)throw new InvalidDataException("Exact source identity differs from the promotion proposal.");
        var visual=await LatestReviewInfoAsync(identity,"visual",token);var rights=await LatestReviewInfoAsync(identity,"rights",token);
        if(visual.State!="approved"||rights.State!="approved"||visual.RecordSha256!=spec.VisualReviewSha256||rights.RecordSha256!=spec.RightsReviewSha256)
            throw new InvalidDataException("Both exact current live human source reviews must be approved and match the proposed review hashes.");
        var state=await PromotionStateAsync(bundle,identity,visual,rights,token);
        if(state is "invalid_promotion" or "promotion_limit_reached")throw new InvalidDataException("Source promotion history failed bounded integrity verification.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(PromotionFor(bundle,identity,visual,rights),Json);var hash=Hash(bytes);
        var relative=$"{Library}/approved/{identity.BundleSha256}/{hash}";var target=paths.Resolve(relative,true);
        if(state!="current")
        {
            if(Directory.Exists(target)||File.Exists(target))throw new IOException("Promotion destination occupied; no overwrite allowed.");
            var staging=paths.Resolve($"{Library}/promotion-staging/{Guid.NewGuid():N}",true);Directory.CreateDirectory(staging);
            await WriteNew(paths.Resolve(Path.Combine(staging,"entry.json"),true),bytes,token);
            // Validate the source and review snapshot again immediately before atomic publication.
            if(IdentityOf(identity.BundleSha256,await VerifyBundle(identity.BundleSha256,token))!=identity||
                await LatestReviewInfoAsync(identity,"visual",token)!=visual||await LatestReviewInfoAsync(identity,"rights",token)!=rights)
                throw new InvalidDataException("Source or current reviews changed before promotion publication.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);paths.Resolve(target,true);token.ThrowIfCancellationRequested();
            Directory.Move(staging,target);
        }
        return JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_promotion",identity,
            promotionSha256=hash,promotionPath=relative+"/entry.json",tier=SourceTier,alreadyPresent=state=="current",
            visualReviewSha256=visual.RecordSha256,rightsReviewSha256=rights.RecordSha256,
            targetProjectApproved=false,targetUnityInspectionRequired=true,qualification=SourceQualification},Json);
    },token);

    private async Task<string> PromotionStateAsync(Bundle bundle,LocalBlenderReviewIdentity identity,SourceReviewStatus visual,SourceReviewStatus rights,CancellationToken token)
    {
        var root=paths.Resolve($"{Library}/approved/{identity.BundleSha256}",true);
        if(!Directory.Exists(root))return "not_promoted";
        var count=0;var found=false;
        var expected=visual.State=="approved"&&rights.State=="approved"?Hash(JsonSerializer.SerializeToUtf8Bytes(PromotionFor(bundle,identity,visual,rights),Json)):null;
        foreach(var directory in Directory.EnumerateFileSystemEntries(root))
        {
            token.ThrowIfCancellationRequested();if(++count>128)return "promotion_limit_reached";
            try
            {
                var hash=Path.GetFileName(directory);
                if(hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigit(c))||!Directory.Exists(directory))return "invalid_promotion";
                paths.Resolve(directory,false);var file=paths.Resolve(Path.Combine(directory,"entry.json"),false);
                var bytes=await ReadSourceStateBytes(file,16*1024,token);
                if(Hash(bytes)!=hash)return "invalid_promotion";
                var entry=JsonSerializer.Deserialize<SourcePromotion>(bytes,Json);
                if(entry is null||entry.Identity!=identity||entry.VisualReviewSha256 is null||entry.RightsReviewSha256 is null||
                    entry.VisualReviewSha256.Length!=64||entry.VisualReviewSha256.Any(c=>!char.IsAsciiHexDigit(c))||
                    entry.RightsReviewSha256.Length!=64||entry.RightsReviewSha256.Any(c=>!char.IsAsciiHexDigit(c))||
                    entry!=PromotionFor(bundle,identity,new("approved",entry.VisualReviewSha256),new("approved",entry.RightsReviewSha256)))
                    return "invalid_promotion";
                if(hash==expected)found=true;
            }
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            {return "invalid_promotion";}
        }
        return found?"current":count==0?"not_promoted":"stale_or_revoked";
    }
}
