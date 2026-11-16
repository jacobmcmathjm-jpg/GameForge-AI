using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed partial class BuiltInBlenderAssetModel
{
    private static AgentPlan LocalReviewPlan(ModelRequest request,string hash,string kind)
    {
        var inspected=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.InspectLocalBlenderAsset&&o.Arguments.TryGetValue("bundle_sha256",out var h)&&h==hash);
        if(inspected is null)return new("First I will verify the exact local source bundle and preview without changing it.",
            [ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Load exact local source review evidence",("bundle_sha256",hash))]);
        if(!inspected.Success)return new("Exact source inspection failed; no review or import will run.",[]);
        try
        {
            using var json=JsonDocument.Parse(inspected.Detail);var root=json.RootElement;
            if(root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("kind").GetString()!="local_blender_source_review_packet"||root.GetProperty("targetProjectApproved").GetBoolean())throw new JsonException();
            var id=root.GetProperty("identity");
            var identity=new LocalBlenderReviewIdentity(id.GetProperty("bundleSha256").GetString()!,id.GetProperty("assetSha256").GetString()!,
                id.GetProperty("sourceSha256").GetString()!,id.GetProperty("previewSha256").GetString()!,id.GetProperty("metricsSha256").GetString()!);
            var proposal=LocalBlenderReviewSpec.Proposal(identity,kind);_=LocalBlenderReviewSpec.FromProposal(proposal);
            if(identity.BundleSha256!=hash)throw new JsonException();
            var decision=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.ReviewGameArtAsset&&o.Arguments.TryGetValue("bundle_sha256",out var h)&&h==hash&&o.Arguments.TryGetValue("review_kind",out var k)&&k==kind);
            if(decision is null)return new("The exact source preview and measurements are ready. Only you may accept or reject this source-only "+kind+" review in the full dialog. It does not approve a Unity project or placement.",[proposal]);
            if(!decision.Success)return new("No successful source decision was recorded; cancellation or failure does not approve the asset. No import or promotion will run.",[]);
            using var recorded=JsonDocument.Parse(decision.Detail);var r=recorded.RootElement;var value=r.GetProperty("decision").GetString();
            if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("kind").GetString()!="local_blender_source_review"||r.GetProperty("reviewKind").GetString()!=kind||r.GetProperty("targetProjectApproved").GetBoolean()||value is not ("approved" or "rejected"))throw new JsonException();
            var recordedId=r.GetProperty("identity");
            if(recordedId.GetProperty("bundleSha256").GetString()!=identity.BundleSha256||recordedId.GetProperty("assetSha256").GetString()!=identity.AssetSha256||
                recordedId.GetProperty("sourceSha256").GetString()!=identity.SourceSha256||recordedId.GetProperty("previewSha256").GetString()!=identity.PreviewSha256||
                recordedId.GetProperty("metricsSha256").GetString()!=identity.MetricsSha256)throw new JsonException();
            return new("Your exact source-library "+kind+" decision was recorded as "+value+". No source object was changed, imported or promoted. The other human review, target Unity inspection and placement/performance acceptance remain separate.",[]);
        }
        catch(Exception error) when(error is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {return new("Local source review evidence is invalid or changed; no human approval or follow-up action is inferred.",[]);}
    }
}
