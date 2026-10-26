using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    private static ToolCall BarrelCall(string project = "Game") => ToolCall.Create(ToolKind.CreateBlenderAsset, PermissionKind.Execute,
        "Fixed local recipe", ("project_path", project), ("asset_name", "barrel-test"), ("recipe", "weathered_barrel_v1"),
        ("seed", "41"), ("height_mm", "1100"), ("radius_mm", "340"));

    private async Task<string> BlenderFixtureExecutable()
    {
        await CreateBareUnityProjectAsync();
        var exe = Path.Combine(_root, "blender.exe");
        await File.WriteAllTextAsync(exe, "test executable identity; injected runner never launches it");
        return exe;
    }

    // Synthetic format envelopes for policy/hash tests only, not evidence of modelling or rendering quality.
    private static async Task<UnityProcessResult> BlenderFixtureRunner(string exe, IReadOnlyList<string> arguments,
        string root, TimeSpan timeout, CancellationToken token)
    {
        Assert.Contains("--offline-mode", arguments); Assert.Contains("--disable-autoexec", arguments);
        Assert.Contains("--factory-startup", arguments); Assert.Contains("--python-exit-code", arguments);
        Assert.Equal(Path.Combine(root, "request.json"), arguments[^1]);
        var output = Path.Combine(root, "output"); Directory.CreateDirectory(output);
        await File.WriteAllBytesAsync(Path.Combine(output, "asset.glb"), MinimalGlb(), token);
        await File.WriteAllBytesAsync(Path.Combine(output, "model.blend"), Encoding.ASCII.GetBytes("BLENDER-v502 synthetic unit fixture"), token);
        foreach (var name in new[] { "preview.png", "basecolor.png", "roughness.png", "metallic.png" })
            await File.WriteAllBytesAsync(Path.Combine(output, name), MinimalPng(512, 512), token);
        await File.WriteAllTextAsync(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(new
        {
            recipe = "weathered_barrel_v1", blenderVersion = "5.2.1 LTS", vertices = 100, triangles = 80, materials = 2,
            objects = 1, boundsMin = new[] { -.3485, -.3485, 0 }, boundsMax = new[] { .3485, .3485, 1.1 },
            pivot = "bottom_center", units = "metres", textureResolution = 512, animationCount = 0
        }), token);
        return new(0, "FORGECORE_BLENDER_RECIPE_COMPLETE", "");
    }
    private ToolExecutor BlenderExecutor(string exe, UnityProcessRunner runner) =>
        new(new WorkspacePathGuard(_root), [], [], 100*1024*1024, blenderPath: exe, blenderProcessRunner: runner);
    private static ToolCall ImportBarrel(string receipt, string name = "barrel-import", string? hash = null)
    {
        using var doc = JsonDocument.Parse(receipt);
        return ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write, "Import local barrel",
            ("project_path", "Game"), ("asset_name", name),
            ("bundle_sha256", doc.RootElement.GetProperty("bundleSha256").GetString()!),
            ("expected_asset_sha256", hash ?? doc.RootElement.GetProperty("assetSha256").GetString()!));
    }

    [Theory]
    [InlineData("recipe", "arbitrary_python")]
    [InlineData("seed", "-1")]
    [InlineData("seed", "1000000")]
    [InlineData("height_mm", "999")]
    [InlineData("radius_mm", "501")]
    [InlineData("asset_name", "../escape")]
    [InlineData("asset_name", "A bad name")]
    [InlineData("python", "import os")]
    public void Blender_SpecRejectsUnsupportedOrUnboundedInput(string key, string value)
    {
        var call = BarrelCall(); var args = new Dictionary<string, string>(call.Arguments) { [key] = value };
        Assert.Throws<ArgumentException>(() => BlenderAssetSpec.From(call with { Arguments = args }));
    }

    [Fact] public async Task Blender_PolicyRequiresConfiguredExecutableProjectAndExecutePermission()
    {
        var exe = await BlenderFixtureExecutable(); var guard = new WorkspacePathGuard(_root);
        var policy = new SecurityPolicy(guard, [], blenderPath: exe);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(BarrelCall(), default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(BarrelCall() with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(BarrelCall("../outside"), default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await new SecurityPolicy(guard, []).ValidateAsync(BarrelCall(), default)).Decision);
        using var executor = new ToolExecutor(guard, []);
        Assert.False((await executor.ExecuteAsync(BarrelCall(), default)).Success);
    }

    [Fact] public async Task Blender_BundleIsReusedWithoutRerunAndImportsExactSourceIntoFreshFolders()
    {
        var exe = await BlenderFixtureExecutable(); var calls = 0;
        using var executor = BlenderExecutor(exe, async (e,a,w,t,c) => { calls++; return await BlenderFixtureRunner(e,a,w,t,c); });
        var first = await executor.ExecuteAsync(BarrelCall(), default); Assert.True(first.Success, first.Error);
        var again = await executor.ExecuteAsync(BarrelCall(), default); Assert.True(again.Success, again.Error);
        using var reuse = JsonDocument.Parse(again.Output); Assert.True(reuse.RootElement.GetProperty("reused").GetBoolean());
        Assert.Equal(1, calls);
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), [], blenderPath: exe);
        var import = ImportBarrel(first.Output);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(import, default)).Decision);
        var imported = await executor.ExecuteAsync(import, default); Assert.True(imported.Success, imported.Error);
        var duplicate = await executor.ExecuteAsync(import, default); Assert.False(duplicate.Success);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(import, default)).Decision);
        var second = await executor.ExecuteAsync(ImportBarrel(first.Output, "barrel-second-game"), default); Assert.True(second.Success, second.Error);
        var wrongHash = await executor.ExecuteAsync(ImportBarrel(first.Output, "barrel-wrong", new string('a', 64)), default); Assert.False(wrongHash.Success);
        var spec = BlenderAssetImportSpec.From(import);
        var sidecar = await File.ReadAllTextAsync(Path.Combine(_root, spec.ProjectPath, spec.DestinationPath + ".asset.json"));
        Assert.Contains("forgecore://local-blender/", sidecar); Assert.Contains("GENERATED_REVIEW_REQUIRED", sidecar);
        Assert.False(Directory.Exists(Path.Combine(_root, ".forgecore", "asset-vault")));
        Assert.False(Directory.Exists(Path.Combine(_root, "Game", ".forgecore", "unity", "art-reviews")));
    }

    [Theory]
    [InlineData("model.blend")]
    [InlineData("asset.glb")]
    [InlineData("recipe.py")]
    [InlineData("request.json")]
    [InlineData("entry.json")]
    public async Task Blender_TamperedBundleIsNeitherReusedNorImported(string file)
    {
        var exe = await BlenderFixtureExecutable(); using var executor = BlenderExecutor(exe, BlenderFixtureRunner);
        var first = await executor.ExecuteAsync(BarrelCall(), default); Assert.True(first.Success, first.Error);
        using var doc = JsonDocument.Parse(first.Output); var hash = doc.RootElement.GetProperty("bundleSha256").GetString();
        await File.AppendAllTextAsync(Path.Combine(_root, ".forgecore", "blender-library", "objects", hash!, file), "tampered");
        Assert.False((await executor.ExecuteAsync(BarrelCall(), default)).Success);
        Assert.False((await executor.ExecuteAsync(ImportBarrel(first.Output), default)).Success);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("truncated")]
    [InlineData("bounds")]
    [InlineData("extra_file")]
    [InlineData("modified_recipe")]
    public async Task Blender_InvalidRunDoesNotPublishBundle(string failure)
    {
        var exe = await BlenderFixtureExecutable();
        using var executor = BlenderExecutor(exe, async (e,a,w,t,c) =>
        {
            var r = await BlenderFixtureRunner(e,a,w,t,c);
            if (failure == "failed") return r with { ExitCode = 1 };
            if (failure == "truncated") return r with { Truncated = true };
            if (failure == "bounds")
            {
                var file = Path.Combine(w, "output", "metrics.json");
                await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file,c)).Replace("1.1", "9.1"), c);
            }
            if (failure == "extra_file") await File.WriteAllTextAsync(Path.Combine(w,"output","extra.exe"), "no",c);
            if (failure == "modified_recipe") await File.AppendAllTextAsync(Path.Combine(w,"recipe.py"), "# changed",c);
            return r;
        });
        Assert.False((await executor.ExecuteAsync(BarrelCall(), default)).Success);
        var objects = Path.Combine(_root, ".forgecore", "blender-library", "objects");
        Assert.False(Directory.Exists(objects));
    }

    [Fact] public async Task Blender_CancelledRunReleasesLeaseAndCannotImport()
    {
        var exe = await BlenderFixtureExecutable(); using var cancellation = new CancellationTokenSource();
        using var executor = BlenderExecutor(exe, (_,_,_,_,token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); throw new Exception(); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(BarrelCall(), cancellation.Token));
        Assert.False(File.Exists(Path.Combine(_root,".forgecore","blender-library","execution.lease")));
        using var recovery = BlenderExecutor(exe, BlenderFixtureRunner);
        Assert.True((await recovery.ExecuteAsync(BarrelCall(), default)).Success);
    }

    [Fact] public async Task Blender_StaleCrossProcessLeaseStopsExecution()
    {
        var exe = await BlenderFixtureExecutable(); var directory = Path.Combine(_root,".forgecore","blender-library"); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"execution.lease"), "another owner or interrupted run");
        using var executor = BlenderExecutor(exe, (_,_,_,_,_) => throw new Exception("Must not run"));
        Assert.False((await executor.ExecuteAsync(BarrelCall(), default)).Success);
        Assert.True(File.Exists(Path.Combine(directory,"execution.lease")));
    }

    [Fact] public void Blender_TrustedGrantIncludesOnlyTheExactProjectAndNeverHumanAssetReview()
    {
        var grant = new TaskAuthorizationSession(); grant.Activate("Game",TimeSpan.FromMinutes(10),10);
        Assert.True(grant.TryConsume(new ApprovalRequest(BarrelCall(),"bounded local recipe")));
        Assert.False(grant.TryConsume(new ApprovalRequest(BarrelCall("OtherGame"),"other project")));
        var review = ToolCall.Create(ToolKind.ReviewGameArtAsset,PermissionKind.Write,"Human review",("project_path","Game"));
        Assert.False(grant.TryConsume(new ApprovalRequest(review,"must remain a live human decision")));
    }

    [Fact] public async Task Blender_ImportRejectsTraversalAndOccupiedUnityMetadataWithoutWriting()
    {
        var exe = await BlenderFixtureExecutable(); using var executor = BlenderExecutor(exe, BlenderFixtureRunner);
        var first = await executor.ExecuteAsync(BarrelCall(),default); Assert.True(first.Success,first.Error);
        var import = ImportBarrel(first.Output); var spec = BlenderAssetImportSpec.From(import);
        var folder = Path.GetDirectoryName(Path.Combine(_root,spec.ProjectPath,spec.DestinationPath))!;
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        await File.WriteAllTextAsync(folder+".meta","pre-existing Unity identity");
        Assert.False((await executor.ExecuteAsync(import,default)).Success);
        Assert.False(Directory.Exists(folder));
        var args = new Dictionary<string,string>(import.Arguments) { ["asset_name"]="../../escape" };
        Assert.False((await executor.ExecuteAsync(import with { Arguments=args },default)).Success);
    }
}
