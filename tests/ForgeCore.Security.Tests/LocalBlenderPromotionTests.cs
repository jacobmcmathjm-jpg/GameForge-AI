using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    private static ToolCall CompatibilityCall(LocalBlenderReviewIdentity id,params (string Key,string Value)[] constraints)=>
        ToolCall.Create(ToolKind.CheckLocalBlenderCompatibility,PermissionKind.Read,"Source-only fixture check",new[]{("bundle_sha256",id.BundleSha256)}.Concat(constraints).ToArray());
    // These are synthetic fixture attestations, never decisions about real workstation assets.
    private async Task<LocalBlenderPromotionSpec> SyntheticSourceApprovals(ToolExecutor executor,LocalBlenderReviewIdentity id)
    {
        async Task<string> Approve(string kind)
        {
            var result=await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(id,kind),ApprovalDecision.Approved),default);
            Assert.True(result.Success,result.Error);using var json=JsonDocument.Parse(result.Output);return json.RootElement.GetProperty("recordSha256").GetString()!;
        }
        return new(id,await Approve("visual"),await Approve("rights"));
    }
    private async Task<JsonDocument> SourceCompatibility(ToolExecutor executor,LocalBlenderReviewIdentity id,params (string Key,string Value)[] constraints)
    {
        var result=await executor.ExecuteAsync(CompatibilityCall(id,constraints),default);Assert.True(result.Success,result.Error);return JsonDocument.Parse(result.Output);
    }
    private string ApprovedSourceRoot=>Path.Combine(_root,".forgecore","blender-library","approved");
    [Fact]public async Task SourceCompatibilityIsReadOnlyAndPendingCannotPromote()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;
        var policy=new SecurityPolicy(new(_root),[]);var call=CompatibilityCall(id);
        Assert.Equal(SecurityDecision.Allowed,(await policy.ValidateAsync(call,default)).Decision);
        using var check=await SourceCompatibility(executor,id);var r=check.RootElement;
        Assert.True(r.GetProperty("matchesSourceConstraints").GetBoolean());Assert.False(r.GetProperty("eligibleForSourcePromotion").GetBoolean());
        Assert.Equal("pending",r.GetProperty("sourceVisualReview").GetProperty("state").GetString());
        Assert.Equal("not_promoted",r.GetProperty("sourcePromotionState").GetString());
        var proposal=new LocalBlenderPromotionSpec(id,new string('a',64),new string('b',64)).Proposal();
        Assert.Equal(SecurityDecision.ApprovalRequired,(await policy.ValidateAsync(proposal,default)).Decision);
        var grant=new TaskAuthorizationSession();grant.Activate("Game",TimeSpan.FromMinutes(5),10);Assert.False(grant.TryConsume(new(proposal,"Source write")));
        Assert.False((await executor.ExecuteAsync(proposal,default)).Success);Assert.False(Directory.Exists(ApprovedSourceRoot));
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","reviews")));
    }
    [Fact]public async Task ExactSourcePromotionIsImmutableIdempotentDiscoverableAndNotGameApproval()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;
        var before=Directory.GetFiles(Path.Combine(_root,".forgecore","blender-library","objects",id.BundleSha256)).ToDictionary(x=>x,x=>SHA256.HashData(File.ReadAllBytes(x)));
        var spec=await SyntheticSourceApprovals(executor,id);var first=await executor.ExecuteAsync(spec.Proposal(),default);Assert.True(first.Success,first.Error);
        using var receipt=JsonDocument.Parse(first.Output);var r=receipt.RootElement;
        Assert.False(r.GetProperty("alreadyPresent").GetBoolean());Assert.False(r.GetProperty("targetProjectApproved").GetBoolean());Assert.True(r.GetProperty("targetUnityInspectionRequired").GetBoolean());
        var path=Path.Combine(_root,r.GetProperty("promotionPath").GetString()!);
        Assert.Equal(r.GetProperty("promotionSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        var repeated=await executor.ExecuteAsync(spec.Proposal(),default);Assert.True(repeated.Success,repeated.Error);
        using var repeat=JsonDocument.Parse(repeated.Output);Assert.True(repeat.RootElement.GetProperty("alreadyPresent").GetBoolean());
        Assert.Single(Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories));
        using var check=await SourceCompatibility(executor,id);Assert.Equal("current",check.RootElement.GetProperty("sourcePromotionState").GetString());
        Assert.Equal("not_measured_in_target",check.RootElement.GetProperty("animationCompatibility").GetString());
        var found=await executor.ExecuteAsync(FindLocal(),default);using var search=JsonDocument.Parse(found.Output);
        var candidate=Assert.Single(search.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal("current",candidate.GetProperty("sourcePromotionState").GetString());
        foreach(var flag in new[]{"visualReviewed","rightsReviewed","placementApproved"})Assert.False(candidate.GetProperty(flag).GetBoolean());
        foreach(var file in before)Assert.Equal(file.Value,SHA256.HashData(File.ReadAllBytes(file.Key)));
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","asset-vault")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*.glb",SearchOption.AllDirectories));
    }
    [Theory][InlineData("visual")][InlineData("rights")]
    public async Task NewRejectionRevokesPromotionAndOldReviewHashesCannotReplay(string kind)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;
        var spec=await SyntheticSourceApprovals(executor,id);Assert.True((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
        Assert.True((await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(id,kind),ApprovalDecision.Rejected),default)).Success);
        using var check=await SourceCompatibility(executor,id);Assert.False(check.RootElement.GetProperty("eligibleForSourcePromotion").GetBoolean());
        Assert.Equal("stale_or_revoked",check.RootElement.GetProperty("sourcePromotionState").GetString());
        Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);Assert.Single(Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories));
    }
    [Fact]public async Task NewApprovedReviewRequiresFreshPromotionVersionAndRetainsHistory()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;
        var old=await SyntheticSourceApprovals(executor,id);Assert.True((await executor.ExecuteAsync(old.Proposal(),default)).Success);
        var next=await SyntheticSourceApprovals(executor,id);Assert.NotEqual(old,next);
        using var stale=await SourceCompatibility(executor,id);Assert.Equal("stale_or_revoked",stale.RootElement.GetProperty("sourcePromotionState").GetString());
        Assert.False((await executor.ExecuteAsync(old.Proposal(),default)).Success);Assert.True((await executor.ExecuteAsync(next.Proposal(),default)).Success);
        Assert.Equal(2,Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories).Length);
    }
    [Theory][InlineData("expected_asset_sha256")][InlineData("expected_source_sha256")][InlineData("expected_preview_sha256")][InlineData("expected_metrics_sha256")]
    [InlineData("expected_visual_review_sha256")][InlineData("expected_rights_review_sha256")]
    public async Task PromotionBindsEverySourceAndReviewHash(string field)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var spec=await SyntheticSourceApprovals(executor,fixture.Packet.Identity);
        var call=spec.Proposal();var args=call.Arguments.ToDictionary(x=>x.Key,x=>x.Value);args[field]=new string('f',64);
        Assert.False((await executor.ExecuteAsync(call with{Arguments=args},default)).Success);Assert.False(Directory.Exists(ApprovedSourceRoot));
    }
    [Theory][InlineData("asset.glb")][InlineData("model.blend")][InlineData("preview.png")][InlineData("metrics.json")][InlineData("recipe.py")][InlineData("entry.json")]
    public async Task ChangedSourceCannotCheckOrPromote(string file)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;
        var spec=await SyntheticSourceApprovals(executor,id);
        await File.AppendAllTextAsync(Path.Combine(_root,".forgecore","blender-library","objects",id.BundleSha256,file),"tamper");
        Assert.False((await executor.ExecuteAsync(CompatibilityCall(id),default)).Success);
        Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);Assert.False(Directory.Exists(ApprovedSourceRoot));
    }
    [Theory][InlineData("asset_role","characters","asset_role_mismatch")][InlineData("rig_required","true","rig_required")]
    [InlineData("height_min_mm","1500","source_height_out_of_range")][InlineData("height_max_mm","1000","source_height_out_of_range")]
    [InlineData("max_triangles","1","triangle_budget_exceeded")][InlineData("recipe","rigged_humanoid_v1","recipe_mismatch")]
    public async Task SourceConstraintsRejectUnsuitableCandidatesEvenWithReviews(string field,string value,string reason)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;_=await SyntheticSourceApprovals(executor,id);
        using var check=await SourceCompatibility(executor,id,(field,value));var r=check.RootElement;
        Assert.False(r.GetProperty("matchesSourceConstraints").GetBoolean());Assert.False(r.GetProperty("eligibleForSourcePromotion").GetBoolean());
        Assert.Contains(reason,r.GetProperty("incompatibilities").EnumerateArray().Select(x=>x.GetString()));Assert.False(r.GetProperty("targetProjectApproved").GetBoolean());
    }
    [Theory][InlineData("append")][InlineData("missing_digest")][InlineData("missing_record")]
    public async Task DamagedReviewHistoryCannotPromote(string damage)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;var spec=await SyntheticSourceApprovals(executor,id);
        var file=Directory.GetFiles(Path.Combine(_root,".forgecore","blender-library","reviews",id.BundleSha256),"*.json")[0];
        if(damage=="append")await File.AppendAllTextAsync(file,"tamper");else File.Delete(damage=="missing_digest"?file+".sha256":file);
        using var check=await SourceCompatibility(executor,id);Assert.False(check.RootElement.GetProperty("eligibleForSourcePromotion").GetBoolean());
        Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);Assert.False(Directory.Exists(ApprovedSourceRoot));
    }
    [Fact]public async Task DamagedPromotionIsNotCurrentAndIsNeverOverwritten()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;var spec=await SyntheticSourceApprovals(executor,id);
        Assert.True((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
        var file=Assert.Single(Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories));await File.AppendAllTextAsync(file,"tamper");
        using var check=await SourceCompatibility(executor,id);Assert.Equal("invalid_promotion",check.RootElement.GetProperty("sourcePromotionState").GetString());
        Assert.False(check.RootElement.GetProperty("eligibleForSourcePromotion").GetBoolean());Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
        Assert.EndsWith("tamper",await File.ReadAllTextAsync(file));
    }
    [Fact]public async Task PromotionCancellationAndOccupiedLeaseNeverPublishOrDeleteOtherLease()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;var spec=await SyntheticSourceApprovals(executor,id);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>executor.ExecuteAsync(spec.Proposal(),cancellation.Token));
        var lease=Path.Combine(_root,".forgecore","blender-library","source-decisions.lease");await File.WriteAllTextAsync(lease,"owned by another request");
        Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);Assert.Equal("owned by another request",await File.ReadAllTextAsync(lease));
        Assert.False(Directory.Exists(ApprovedSourceRoot));
    }
    [Fact]public async Task ConcurrentIdenticalPromotionsPublishExactlyOneVersion()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var spec=await SyntheticSourceApprovals(executor,fixture.Packet.Identity);
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>executor.ExecuteAsync(spec.Proposal(),default)));
        Assert.All(results,r=>Assert.True(r.Success,r.Error));Assert.Single(Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories));
        Assert.Equal(3,results.Count(r=>{using var j=JsonDocument.Parse(r.Output);return j.RootElement.GetProperty("alreadyPresent").GetBoolean();}));
    }
    [Fact]public async Task PromotionEnumerationLimitIsExplicitAndFailClosed()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;var spec=await SyntheticSourceApprovals(executor,id);
        Assert.True((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
        var source=Assert.Single(Directory.GetFiles(ApprovedSourceRoot,"entry.json",SearchOption.AllDirectories));var parent=Path.GetDirectoryName(Path.GetDirectoryName(source))!;
        // Synthetic historical versions with distinct bound record hashes; no real approvals or source objects are touched.
        for(var i=0;i<128;i++)
        {
            var entry=JsonNode.Parse(await File.ReadAllTextAsync(source))!;entry["visualReviewSha256"]=i.ToString("x64");
            var bytes=JsonSerializer.SerializeToUtf8Bytes(entry);var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var d=Path.Combine(parent,hash);Directory.CreateDirectory(d);await File.WriteAllBytesAsync(Path.Combine(d,"entry.json"),bytes);
        }
        using var check=await SourceCompatibility(executor,id);Assert.False(check.RootElement.GetProperty("eligibleForSourcePromotion").GetBoolean());
        Assert.Equal("promotion_limit_reached",check.RootElement.GetProperty("sourcePromotionState").GetString());
        Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
    }
    [Theory][InlineData("visual")][InlineData("rights")]
    public async Task EqualTimestampRejectionWinsRegardlessOfFileEnumeration(string kind)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var id=fixture.Packet.Identity;var spec=await SyntheticSourceApprovals(executor,id);
        Assert.True((await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(id,kind),ApprovalDecision.Rejected),default)).Success);
        foreach(var file in Directory.GetFiles(Path.Combine(_root,".forgecore","blender-library","reviews",id.BundleSha256),"*.json"))
        {
            var entry=JsonNode.Parse(await File.ReadAllTextAsync(file))!;entry["createdUtc"]="2026-10-28T00:00:00+00:00";
            var bytes=JsonSerializer.SerializeToUtf8Bytes(entry);await File.WriteAllBytesAsync(file,bytes);
            await File.WriteAllTextAsync(file+".sha256",Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        using var check=await SourceCompatibility(executor,id);
        Assert.Equal("rejected",check.RootElement.GetProperty(kind=="visual"?"sourceVisualReview":"sourceRightsReview").GetProperty("state").GetString());
        Assert.False(check.RootElement.GetProperty("eligibleForSourcePromotion").GetBoolean());Assert.False((await executor.ExecuteAsync(spec.Proposal(),default)).Success);
    }
}
