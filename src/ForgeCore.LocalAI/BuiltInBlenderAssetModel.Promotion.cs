using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed partial class BuiltInBlenderAssetModel
{
    private static AgentPlan? LocalPromotionRoute(ModelRequest request)
    {
        var match=Regex.Match(request.UserMessage,@"^\s*(?<action>check|promote)\s+local\s+blender\s+asset\s+(?<hash>[a-f0-9]{64})[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!match.Success)return Regex.IsMatch(request.UserMessage,@"\b(?:check|promote)\s+local\s+blender\s+asset\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
            ?new("Use: Check local Blender asset <exact 64-character bundle hash>. Or: Promote local Blender asset <exact hash>. Promotion needs both current live human source reviews; unsupported syntax is not sent to inference.",[]):null;
        var hash=match.Groups["hash"].Value.ToLowerInvariant();var promote=match.Groups["action"].Value.Equals("promote",StringComparison.OrdinalIgnoreCase);
        var check=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.CheckLocalBlenderCompatibility&&o.Arguments.TryGetValue("bundle_sha256",out var h)&&h==hash);
        if(check is null)return new("First I will verify this exact source version, source constraints and current human review records. No editor, import or approval is implied.",
            [ToolCall.Create(ToolKind.CheckLocalBlenderCompatibility,PermissionKind.Read,"Check exact local source compatibility and promotion eligibility",("bundle_sha256",hash))]);
        if(!check.Success)return new("Source compatibility inspection failed; no promotion or import will run.",[]);
        try
        {
            using var document=JsonDocument.Parse(check.Detail);var r=document.RootElement;
            if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("kind").GetString()!="local_blender_source_compatibility"||
                r.GetProperty("targetProjectApproved").GetBoolean()||!r.GetProperty("targetUnityInspectionRequired").GetBoolean()||
                r.GetProperty("animationCompatibility").GetString()!="not_measured_in_target")throw new JsonException();
            var id=r.GetProperty("identity");
            var identity=new LocalBlenderReviewIdentity(id.GetProperty("bundleSha256").GetString()!,id.GetProperty("assetSha256").GetString()!,
                id.GetProperty("sourceSha256").GetString()!,id.GetProperty("previewSha256").GetString()!,id.GetProperty("metricsSha256").GetString()!);
            _=LocalBlenderReviewSpec.FromProposal(LocalBlenderReviewSpec.Proposal(identity,"visual"));if(identity.BundleSha256!=hash)throw new JsonException();
            var eligible=r.GetProperty("eligibleForSourcePromotion").GetBoolean();
            if(!promote)return new("Exact source compatibility checked. Source promotion "+(eligible?"may be requested separately":"is not eligible")+". Target Unity, animation compatibility, performance and placement remain unverified. No files were changed.",[]);
            if(!eligible||!r.GetProperty("matchesSourceConstraints").GetBoolean()||r.GetProperty("sourceVisualReview").GetProperty("state").GetString()!="approved"||
                r.GetProperty("sourceRightsReview").GetProperty("state").GetString()!="approved")
                return new("Source promotion is blocked: both current exact human source reviews and source constraints must pass. No automatic review, import or placement will run.",[]);
            var spec=new LocalBlenderPromotionSpec(identity,r.GetProperty("sourceVisualReview").GetProperty("recordSha256").GetString()!,
                r.GetProperty("sourceRightsReview").GetProperty("recordSha256").GetString()!);
            var proposal=spec.Proposal();_=LocalBlenderPromotionSpec.From(proposal);
            var result=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.PromoteLocalBlenderAsset&&o.Arguments.TryGetValue("bundle_sha256",out var h)&&h==hash);
            if(result is null)return new("Both exact current human source reviews are recorded. I will request the separate immutable source-library write; this does not import or approve the asset for any game.",[proposal]);
            if(!result.Success)return new("Source promotion stopped or was cancelled. No import or placement will follow.",[]);
            if(LocalBlenderPromotionSpec.From(new(result.ToolCallId,result.Kind,result.Arguments,PermissionKind.Write,result.Description))!=spec)throw new JsonException();
            using var output=JsonDocument.Parse(result.Detail);var p=output.RootElement;
            var recorded=p.GetProperty("identity");
            if(p.GetProperty("schemaVersion").GetInt32()!=1||p.GetProperty("kind").GetString()!="local_blender_source_promotion"||
                p.GetProperty("tier").GetString()!="human_reviewed_local_source"||p.GetProperty("targetProjectApproved").GetBoolean()||
                !p.GetProperty("targetUnityInspectionRequired").GetBoolean()||p.GetProperty("visualReviewSha256").GetString()!=spec.VisualReviewSha256||
                p.GetProperty("rightsReviewSha256").GetString()!=spec.RightsReviewSha256||recorded.GetRawText()!=id.GetRawText())throw new JsonException();
            return new("The exact human-reviewed source version was promoted without changing its editable source or any game. Fresh target Unity inspection, rig/animation compatibility, collision, LOD, performance and placement review remain required.",[]);
        }
        catch(Exception error)when(error is JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {return new("Source compatibility or promotion evidence is invalid or changed; no follow-up action or game approval is inferred.",[]);}
    }
}
