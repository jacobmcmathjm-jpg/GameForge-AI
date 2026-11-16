using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("wrong_hash")][InlineData("revoked")][InlineData("tampered_review")]
    public async Task QualityGatedImportChecksCurrentExactReviewsBeforeWriting(string scenario)
    {
        var fixture = await LocalReviewFixture(); using var tools = fixture.Executor; var id = fixture.Packet.Identity;
        var hashes = new Dictionary<string, string>(); string? visualPath = null;
        if (scenario != "missing")
        {
            foreach (var kind in new[] { "visual", "rights" })
            {
                // Synthetic decisions inside a disposable fixture, never workstation asset acceptance.
                var recorded = await tools.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(id, kind), ApprovalDecision.Approved), default);
                Assert.True(recorded.Success, recorded.Error); using var r = JsonDocument.Parse(recorded.Output);
                hashes[kind] = r.RootElement.GetProperty("recordSha256").GetString()!;
                if (kind == "visual") visualPath = r.RootElement.GetProperty("recordPath").GetString();
            }
        }
        if (scenario == "revoked") Assert.True((await tools.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(id, "visual"), ApprovalDecision.Rejected), default)).Success);
        if (scenario == "tampered_review") await File.AppendAllTextAsync(Path.Combine(_root, visualPath!), "changed");
        var call = ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write, "Review-bound import fixture",
            ("project_path", "Game"), ("asset_name", "review-bound"), ("bundle_sha256", id.BundleSha256), ("expected_asset_sha256", id.AssetSha256),
            ("expected_visual_review_sha256", scenario is "missing" or "wrong_hash" ? new string('a', 64) : hashes["visual"]),
            ("expected_rights_review_sha256", scenario == "missing" ? new string('b', 64) : hashes["rights"]));
        var result = await tools.ExecuteAsync(call, default); Assert.Equal(scenario == "valid", result.Success);
        var destination = Path.Combine(_root, "Game", "Assets", "ForgeCore", "Generated", "Blender", "review-bound");
        Assert.Equal(scenario == "valid", Directory.Exists(destination));
        if (scenario != "valid") Assert.Contains("quality-gated import stopped", result.Error);
        Assert.False(File.Exists(Path.Combine(_root, ".forgecore", "blender-library", "source-decisions.lease")));
    }
}
