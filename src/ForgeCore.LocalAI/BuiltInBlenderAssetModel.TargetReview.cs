using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;
namespace ForgeCore.LocalAI;
public sealed partial class BuiltInBlenderAssetModel
{
    private static readonly Regex TargetReviewPrompt=new(@"^\s*review\s+imported\s+industrial\s+asset\s+(?<name>[a-z][a-z0-9_-]{1,47})\s+(?<kind>visually|rights)(?:\s+revision\s+(?<revision>[a-z][a-z0-9_-]{1,19}))?[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex TargetPreviewHashes=new(@"four views, [0-9]+ bytes, SHA-256 (?<preview>[0-9a-f]{64})\); provenance .+? \(SHA-256 (?<provenance>[0-9a-f]{64})\)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private AgentPlan? IndustrialTargetReviewRoute(ModelRequest request)
    {
        var match=TargetReviewPrompt.Match(request.UserMessage);
        if(!match.Success)return Regex.IsMatch(request.UserMessage,@"\breview\s+imported\s+industrial\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)?
            new("Use: Review imported industrial asset <bounded-name> visually. Or replace visually with rights. Optional: revision review2. This is an exact target-only review, never placement or source approval; unsupported syntax does not reach inference.",[]):null;
        var name=match.Groups["name"].Value.ToLowerInvariant();var kind=match.Groups["kind"].Value.Equals("rights",StringComparison.OrdinalIgnoreCase)?"rights":"visual";
        var revision=match.Groups["revision"].Success?match.Groups["revision"].Value.ToLowerInvariant():"review1";
        var asset=$"Assets/ForgeCore/Generated/Blender/{name}/{name}.glb";var sidecar=projectPath+"/"+asset+".asset.json";
        ToolObservation? Read(string path)=>request.Observations.LastOrDefault(o=>o.Kind==ToolKind.ReadTextFile&&o.Arguments.TryGetValue("path",out var p)&&p==path);
        AgentPlan ReadStep(string path,string message)=>new(message,[ToolCall.Create(ToolKind.ReadTextFile,PermissionKind.Read,"Read only the exact generated target's passive JSON metadata",("path",path))]);
        var imported=Read(sidecar);if(imported is null)return ReadStep(sidecar,"I will identify this exact already-imported industrial asset. No creation, download, approval or placement is implied.");
        if(!imported.Success)return new("The exact local import sidecar could not be read. No target review, download, re-import or placement will run.",[]);
        try {
            if(imported.Detail.Length>16384)throw new JsonException();using var source=JsonDocument.Parse(imported.Detail);var s=source.RootElement;
            const string prefix="forgecore://local-blender/";var page=s.GetProperty("sourcePage").GetString()??"";
            if(!page.StartsWith(prefix,StringComparison.Ordinal)||page.Length!=prefix.Length+64||s.GetProperty("destinationPath").GetString()!=asset)throw new JsonException();
            var bundle=page[prefix.Length..];var hash=s.GetProperty("sha256").GetString()!;
            if(!ExactTargetHash(bundle)||!ExactTargetHash(hash))throw new JsonException();
            var local=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.InspectLocalBlenderAsset&&o.Arguments.TryGetValue("bundle_sha256",out var b)&&b==bundle);
            if(local is null)return new("I will re-verify the exact immutable original source packet before a separate target review.",
                [ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Verify target's original industrial source identity, no source decision",("bundle_sha256",bundle))]);
            if(!local.Success)return new("Exact source verification failed; no target approval or follow-up write is proposed.",[]);
            if(local.Detail.Length>32768)throw new JsonException();using var packet=JsonDocument.Parse(local.Detail);var p=packet.RootElement;
            if(p.GetProperty("kind").GetString()!="local_blender_source_review_packet"||p.GetProperty("schemaVersion").GetInt32()!=1||
                p.GetProperty("targetProjectApproved").GetBoolean()||!IndustrialPropRecipes.Contains(p.GetProperty("recipe").GetString()!)||
                p.GetProperty("identity").GetProperty("bundleSha256").GetString()!=bundle||p.GetProperty("identity").GetProperty("assetSha256").GetString()!=hash)throw new JsonException();
            // A new preview revision is not another asset. Keep the target catalogue identity stable.
            var id="industrial-"+name;var preview=$"Previews/IndustrialTarget/{id}-{hash[..12]}-{revision}.png";
            var metadataPath=projectPath+"/"+preview+".forgecore-art-preview.json";var metadata=Read(metadataPath);
            if(metadata is null)return ReadStep(metadataPath,"I will check for this exact revision's existing four-view target preview. Missing previews require separately scoped Unity work; occupied outputs are never replaced.");
            ToolObservation? Action(string operation)=>request.Observations.LastOrDefault(o=>o.Kind==ToolKind.UnityEditorAction&&o.Arguments.TryGetValue("project_path",out var project)&&project==projectPath&&
                o.Arguments.TryGetValue("operation",out var op)&&op==operation&&o.Arguments.TryGetValue("asset_set_id",out var set)&&set==id&&
                o.Arguments.TryGetValue("asset_path",out var path)&&path==asset&&o.Arguments.TryGetValue("expected_sha256",out var expected)&&expected==hash);
            AgentPlan EditorStep(string operation,params(string Key,string Value)[] extra) {
                var fields=new[]{("project_path",projectPath),("operation",operation),("asset_path",asset),("expected_sha256",hash),("asset_set_id",id)}.Concat(extra).ToArray();
                var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"Separate exact imported industrial target inspection/preview, not human acceptance",fields);_=UnityEditorActionSpec.From(call);
                return new("Unity can prepare this exact target review evidence without changing a gameplay scene or accepting the asset.",[call]);
            }
            string previewHash,metadataHash;
            if(metadata.Success) {
                if(metadata.Detail.Length>16384)throw new JsonException();using var document=JsonDocument.Parse(metadata.Detail);var m=document.RootElement;
                if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("assetSetId").GetString()!=id||m.GetProperty("assetPath").GetString()!=asset||m.GetProperty("assetSha256").GetString()!=hash||
                    m.GetProperty("previewPath").GetString()!=preview||m.GetProperty("width").GetInt32()!=1280||m.GetProperty("height").GetInt32()!=720||m.GetProperty("viewCount").GetInt32()!=4||
                    m.GetProperty("visualReviewed").GetBoolean()||m.GetProperty("rightsReviewed").GetBoolean())throw new JsonException();
                previewHash=m.GetProperty("previewSha256").GetString()!;metadataHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata.Detail))).ToLowerInvariant();
            }else {
                var inspected=Action("inspect_game_art_asset");if(inspected is null)return EditorStep("inspect_game_art_asset",("asset_role","environment_props"),("style_family","realistic_pbr"));
                if(!inspected.Success||!inspected.Detail.Contains("structural_ready=true",StringComparison.Ordinal))return new("Target structural inspection did not pass; no human review or placement will run.",[]);
                var captured=Action("capture_game_art_preview");if(captured is null)return EditorStep("capture_game_art_preview",("output_path",preview));
                if(!captured.Success)return new("Target preview capture stopped. Preserve occupied or failed evidence; use an explicitly fresh revision after inspecting the cause. No approval was inferred.",[]);
                if(captured.Arguments.GetValueOrDefault("output_path")!=preview||captured.Detail.Length>32768)throw new JsonException();var hashes=TargetPreviewHashes.Match(captured.Detail);if(!hashes.Success)throw new JsonException();
                previewHash=hashes.Groups["preview"].Value.ToLowerInvariant();metadataHash=hashes.Groups["provenance"].Value.ToLowerInvariant();
            }
            var proposal=ToolCall.Create(ToolKind.ReviewGameArtAsset,PermissionKind.Write,"Exact live human imported-industrial target "+kind+" review only",
                ("project_path",projectPath),("review_kind",kind),("asset_set_id",id),("asset_path",asset),("expected_asset_sha256",hash),
                ("preview_path",preview),("expected_preview_sha256",previewHash),("expected_preview_provenance_sha256",metadataHash));var exact=GameArtReviewSpec.FromProposal(proposal);
            var decision=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.ReviewGameArtAsset&&o.Arguments.GetValueOrDefault("project_path")==projectPath&&
                o.Arguments.GetValueOrDefault("asset_set_id")==id&&o.Arguments.GetValueOrDefault("review_kind")==kind);
            if(decision is null)return new("The exact four-view target preview is ready. Only your verified preview dialog may record this target-only "+kind+" decision. Source promotion, placement and gameplay acceptance remain separate.",[proposal]);
            if(!decision.Success)return new("No successful target decision was recorded. Cancellation/failure grants no acceptance, placement or follow-up import.",[]);
            var recorded=GameArtReviewSpec.FromExecution(new(decision.ToolCallId,decision.Kind,decision.Arguments,PermissionKind.Write,decision.Description));
            if((recorded with{ReviewDecision=null,AttestationSource=null})!=exact||!decision.Detail.StartsWith($"Game-art {kind} review recorded: decision={recorded.ReviewDecision}; asset_set_id={id}; record=",StringComparison.Ordinal))throw new JsonException();
            return new("Your exact imported-target "+kind+" decision was recorded as "+recorded.ReviewDecision+". No source-library approval, promotion, scene placement, automatic next review or whole-game acceptance was performed.",[]);
        }catch(Exception error)when(error is JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException or FormatException or OverflowException) {
            return new("Imported industrial target evidence is invalid or stale. No approval, overwrite, re-import, source promotion or placement is inferred. Reinspect the exact files or choose a fresh bounded review revision.",[]);
        }
    }
    private static bool ExactTargetHash(string? hash)=>hash is {Length:64}&&hash.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
