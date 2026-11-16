using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    private async Task<(ToolExecutor Executor,LocalBlenderReviewPacket Packet)> LocalReviewFixture()
    {
        var exe=await BlenderFixtureExecutable();var executor=BlenderExecutor(exe,BlenderFixtureRunner);
        var created=await executor.ExecuteAsync(BarrelCall(),default);Assert.True(created.Success,created.Error);
        using var json=JsonDocument.Parse(created.Output);var hash=json.RootElement.GetProperty("bundleSha256").GetString()!;
        return(executor,await new LocalBlenderReviewReader(new(_root)).ReadAsync(hash,default));
    }
    [Theory][InlineData("visual")][InlineData("rights")]
    public async Task LocalReviewPolicyRequiresLiveDecisionAndPreservesSource(string kind)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var packet=fixture.Packet;
        var proposal=LocalBlenderReviewSpec.Proposal(packet.Identity,kind);var guard=new WorkspacePathGuard(_root);
        var policy=new SecurityPolicy(guard,[]);
        Assert.Equal(SecurityDecision.ApprovalRequired,(await policy.ValidateAsync(proposal,default)).Decision);
        var grant=new TaskAuthorizationSession();grant.Activate("Game",TimeSpan.FromMinutes(5),10);
        Assert.False(grant.TryConsume(new(proposal,"Human decision required")));
        Assert.False((await executor.ExecuteAsync(proposal,default)).Success);
        var folder=Path.Combine(_root,".forgecore","blender-library","objects",packet.Identity.BundleSha256);
        var before=Directory.GetFiles(folder).ToDictionary(p=>Path.GetFileName(p)!,p=>SHA256.HashData(File.ReadAllBytes(p)));
        // Synthetic test decisions only: these are not human approval of any workstation asset.
        var accepted=GameArtReviewSpec.BindUserDecision(proposal,ApprovalDecision.Approved);
        Assert.Equal(SecurityDecision.Denied,(await policy.ValidateAsync(accepted,default)).Decision);
        var result=await executor.ExecuteAsync(accepted,default);Assert.True(result.Success,result.Error);
        Assert.False((await executor.ExecuteAsync(accepted,default)).Success,"A live call ID is never overwritten/replayed.");
        using var output=JsonDocument.Parse(result.Output);Assert.False(output.RootElement.GetProperty("targetProjectApproved").GetBoolean());
        var inspect=await executor.ExecuteAsync(ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Read source state",("bundle_sha256",packet.Identity.BundleSha256)),default);
        Assert.True(inspect.Success,inspect.Error);using var state=JsonDocument.Parse(inspect.Output);
        Assert.Equal("approved",state.RootElement.GetProperty(kind=="visual"?"sourceVisualReview":"sourceRightsReview").GetString());
        Assert.Equal("pending",state.RootElement.GetProperty(kind=="visual"?"sourceRightsReview":"sourceVisualReview").GetString());
        var rejected=GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(packet.Identity,kind),ApprovalDecision.Rejected);
        Assert.True((await executor.ExecuteAsync(rejected,default)).Success);
        var next=await new LocalBlenderReviewReader(guard).ReadAsync(packet.Identity.BundleSha256,default);
        Assert.Contains("review: rejected",next.Summary);
        foreach(var file in before)Assert.Equal(file.Value,SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,file.Key))));
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","asset-vault")));
    }
    [Theory]
    [InlineData("asset.glb")][InlineData("model.blend")][InlineData("preview.png")][InlineData("metrics.json")][InlineData("recipe.py")][InlineData("entry.json")]
    public async Task LocalReviewChangedSourceCannotRecordDecision(string file)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var identity=fixture.Packet.Identity;
        var call=GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(identity,"visual"),ApprovalDecision.Approved);
        await File.AppendAllTextAsync(Path.Combine(_root,".forgecore","blender-library","objects",identity.BundleSha256,file),"changed");
        Assert.False((await executor.ExecuteAsync(call,default)).Success);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","reviews")));
    }
    [Fact]public async Task LocalReviewWrongExpectedHashAndCancellationWriteNoAttestation()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var identity=fixture.Packet.Identity;
        var call=GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(identity with{PreviewSha256=new string('a',64)},"visual"),ApprovalDecision.Approved);
        Assert.Equal(SecurityDecision.Denied,(await new SecurityPolicy(new(_root),[]).ValidateAsync(LocalBlenderReviewSpec.Proposal(identity with{SourceSha256=new string('a',64)},"rights"),default)).Decision);
        Assert.False((await executor.ExecuteAsync(call,default)).Success);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(identity,"visual"),ApprovalDecision.Rejected),cancelled.Token));
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","reviews")));
    }
    [Fact]public async Task LocalReviewAlteredRecordFailsClosedAndDoesNotRetainApproval()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;var identity=fixture.Packet.Identity;
        var result=await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(identity,"visual"),ApprovalDecision.Approved),default);
        Assert.True(result.Success,result.Error);using var json=JsonDocument.Parse(result.Output);
        await File.AppendAllTextAsync(Path.Combine(_root,json.RootElement.GetProperty("recordPath").GetString()!),"changed");
        var packet=await new LocalBlenderReviewReader(new(_root)).ReadAsync(identity.BundleSha256,default);
        Assert.Contains("visual review: invalid_review",packet.Summary);
    }
    [Fact]public async Task LocalReviewSearchSeparatesSourceApprovalFromTargetProjectAcceptance()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;
        foreach(var kind in new[]{"visual","rights"})Assert.True((await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(fixture.Packet.Identity,kind),ApprovalDecision.Approved),default)).Success);
        var found=await executor.ExecuteAsync(FindLocal(),default);Assert.True(found.Success,found.Error);using var json=JsonDocument.Parse(found.Output);
        var candidate=Assert.Single(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal("approved",candidate.GetProperty("sourceVisualReview").GetString());Assert.Equal("approved",candidate.GetProperty("sourceRightsReview").GetString());
        Assert.False(candidate.GetProperty("visualReviewed").GetBoolean());Assert.False(candidate.GetProperty("rightsReviewed").GetBoolean());Assert.False(candidate.GetProperty("placementApproved").GetBoolean());
    }
    [Fact]public async Task LocalReviewHistoryLimitCannotMasqueradeAsCurrentApproval()
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;
        var accepted=await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(fixture.Packet.Identity,"visual"),ApprovalDecision.Approved),default);
        Assert.True(accepted.Success,accepted.Error);using var json=JsonDocument.Parse(accepted.Output);
        var source=Path.Combine(_root,json.RootElement.GetProperty("recordPath").GetString()!);var folder=Path.GetDirectoryName(source)!;
        for(var i=0;i<128;i++) {var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");File.Copy(source,path);File.Copy(source+".sha256",path+".sha256");}
        var packet=await new LocalBlenderReviewReader(new(_root)).ReadAsync(fixture.Packet.Identity.BundleSha256,default);
        Assert.Contains("visual review: review_limit_reached",packet.Summary);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task LocalReviewMissingRecordOrDigestFailsClosed(bool removeRecord)
    {
        var fixture=await LocalReviewFixture();using var executor=fixture.Executor;
        var result=await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(fixture.Packet.Identity,"visual"),ApprovalDecision.Approved),default);
        Assert.True(result.Success,result.Error);using var json=JsonDocument.Parse(result.Output);
        var file=Path.Combine(_root,json.RootElement.GetProperty("recordPath").GetString()!);
        File.Delete(removeRecord?file:file+".sha256");
        var packet=await new LocalBlenderReviewReader(new(_root)).ReadAsync(fixture.Packet.Identity.BundleSha256,default);
        Assert.Contains("visual review: invalid_review",packet.Summary);
    }
}
