namespace ForgeCore.Core;

/// <summary>Source constraints only; never an assertion of target-engine or animation compatibility.</summary>
public sealed record LocalBlenderCompatibilitySpec(string BundleSha256,LocalBlenderSearchSpec Constraints)
{
    public static LocalBlenderCompatibilitySpec From(ToolCall call)
    {
        if(call.Kind!=ToolKind.CheckLocalBlenderCompatibility)throw new ArgumentException("Not local Blender compatibility inspection.");
        StagedAssetInspectionSpec.RequireOnly(call,"bundle_sha256","asset_role","recipe","rig_required","height_min_mm","height_max_mm","max_triangles");
        var hash=ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,"bundle_sha256"),"bundle_sha256");
        var arguments=call.Arguments.Where(x=>x.Key!="bundle_sha256").ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        arguments.Add("query","compatibility");
        var filter=LocalBlenderSearchSpec.From(new(call.Id,ToolKind.SearchLocalBlenderAssets,arguments,PermissionKind.Read,call.Description));
        return new(hash,filter);
    }
}

/// <summary>Promotes only an exact source version with both current human review records, not a Unity-ready asset.</summary>
public sealed record LocalBlenderPromotionSpec(LocalBlenderReviewIdentity Identity,string VisualReviewSha256,string RightsReviewSha256)
{
    public static LocalBlenderPromotionSpec From(ToolCall call)
    {
        if(call.Kind!=ToolKind.PromoteLocalBlenderAsset)throw new ArgumentException("Not local Blender source promotion.");
        StagedAssetInspectionSpec.RequireOnly(call,"bundle_sha256","expected_asset_sha256","expected_source_sha256",
            "expected_preview_sha256","expected_metrics_sha256","expected_visual_review_sha256","expected_rights_review_sha256");
        string Hash(string name)=>ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,name),name);
        return new(new(Hash("bundle_sha256"),Hash("expected_asset_sha256"),Hash("expected_source_sha256"),Hash("expected_preview_sha256"),Hash("expected_metrics_sha256")),
            Hash("expected_visual_review_sha256"),Hash("expected_rights_review_sha256"));
    }
    public ToolCall Proposal()=>ToolCall.Create(ToolKind.PromoteLocalBlenderAsset,PermissionKind.Write,
        "Promote this exact human-reviewed local source version; no import or target-game acceptance",
        ("bundle_sha256",Identity.BundleSha256),("expected_asset_sha256",Identity.AssetSha256),("expected_source_sha256",Identity.SourceSha256),
        ("expected_preview_sha256",Identity.PreviewSha256),("expected_metrics_sha256",Identity.MetricsSha256),
        ("expected_visual_review_sha256",VisualReviewSha256),("expected_rights_review_sha256",RightsReviewSha256));
}
