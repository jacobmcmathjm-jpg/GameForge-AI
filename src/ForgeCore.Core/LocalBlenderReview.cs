namespace ForgeCore.Core;

public sealed record LocalBlenderReviewIdentity(string BundleSha256,string AssetSha256,string SourceSha256,string PreviewSha256,string MetricsSha256);
public sealed record LocalBlenderReviewPacket(LocalBlenderReviewIdentity Identity,string AssetName,string Recipe,
    string Summary,string SourcePath,string RecipePath,byte[] PreviewPng);
public interface ILocalBlenderReviewReader
{
    Task<LocalBlenderReviewPacket> ReadAsync(string bundleSha256,CancellationToken token);
}

/// <summary>Source-library attestation only, never Unity project, performance or placement approval.</summary>
public sealed record LocalBlenderReviewSpec(LocalBlenderReviewIdentity Identity,string ReviewKind,string? Decision,string? AttestationSource)
{
    public static bool IsLocal(ToolCall call)=>call.Kind==ToolKind.ReviewGameArtAsset&&call.Arguments.ContainsKey("bundle_sha256");
    public static LocalBlenderReviewSpec FromProposal(ToolCall call)=>Parse(call,false);
    public static LocalBlenderReviewSpec FromExecution(ToolCall call)=>Parse(call,true);
    public static ToolCall Proposal(LocalBlenderReviewIdentity identity,string kind)=>ToolCall.Create(ToolKind.ReviewGameArtAsset,PermissionKind.Write,
        "Human source-library "+kind+" review; not target-game approval",
        ("bundle_sha256",identity.BundleSha256),("expected_asset_sha256",identity.AssetSha256),("expected_source_sha256",identity.SourceSha256),
        ("expected_preview_sha256",identity.PreviewSha256),("expected_metrics_sha256",identity.MetricsSha256),("review_kind",kind));
    private static LocalBlenderReviewSpec Parse(ToolCall call,bool execution)
    {
        if(!IsLocal(call))throw new ArgumentException("Not a local source review.");
        var keys=new[]{"bundle_sha256","expected_asset_sha256","expected_source_sha256","expected_preview_sha256","expected_metrics_sha256","review_kind"};
        StagedAssetInspectionSpec.RequireOnly(call,execution?[..keys,"review_decision","attestation_source"]:keys);
        string Get(string name)=>StagedAssetInspectionSpec.Get(call,name);
        string Hash(string name)=>ReviewedAssetStoreSpec.NormalizeHash(Get(name),name);
        var kind=Get("review_kind");if(!GameArtReviewSpec.ReviewKinds.Contains(kind))throw new ArgumentException("review_kind must be visual or rights.");
        string? decision=null,source=null;
        if(execution)
        {
            decision=Get("review_decision");source=Get("attestation_source");
            if(decision is not ("approved" or "rejected")||source!=GameArtReviewSpec.UserLiveApproval)
                throw new ArgumentException("Local review execution requires an explicit live user decision.");
        }
        return new(new(Hash("bundle_sha256"),Hash("expected_asset_sha256"),Hash("expected_source_sha256"),Hash("expected_preview_sha256"),Hash("expected_metrics_sha256")),kind,decision,source);
    }
}

public sealed record LocalBlenderInspectSpec(string BundleSha256)
{
    public static LocalBlenderInspectSpec From(ToolCall call)
    {
        if(call.Kind!=ToolKind.InspectLocalBlenderAsset)throw new ArgumentException("Not local Blender source inspection.");
        StagedAssetInspectionSpec.RequireOnly(call,"bundle_sha256");
        return new(ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,"bundle_sha256"),"bundle_sha256"));
    }
}
