using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
using ForgeCore.Memory;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "forgecore-tests", Guid.NewGuid().ToString("N"));
    public SecurityTests() => Directory.CreateDirectory(_root);
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }

    [Fact] public void RelativePath_ResolvesInsideWorkspace()
    {
        var guard = new WorkspacePathGuard(_root); var resolved = guard.Resolve("notes/test.txt", true);
        Assert.StartsWith(Path.GetFullPath(_root), resolved, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\..\\escape.txt")]
    [InlineData("\\\\server\\share\\file.txt")]
    public void TraversalAndUnc_AreBlocked(string path) => Assert.Throws<UnauthorizedAccessException>(() => new WorkspacePathGuard(_root).Resolve(path, true));

    [Fact] public async Task ReadsAreAllowed_WritesRequireApproval_AdminDenied()
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        var read = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "read", ("path", "."));
        var write = ToolCall.Create(ToolKind.WriteTextFile, PermissionKind.Write, "write", ("path", "a.txt"), ("content", "x"));
        var admin = ToolCall.Create(ToolKind.CreateDirectory, PermissionKind.Administrator, "admin", ("path", "a"));
        Assert.Equal(SecurityDecision.Allowed, (await policy.ValidateAsync(read, default)).Decision);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(write, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(admin, default)).Decision);
    }

    [Fact] public async Task CommandMustExactlyMatchAllowList()
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), ["git --version"]);
        var allowed = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "git", ("command", "git --version"));
        var injected = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "bad", ("command", "git --version && whoami"));
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(allowed, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(injected, default)).Decision);
    }

    [Fact] public async Task PermissionCannotBeDowngradedByModel()
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), ["git --version"]);
        var disguised = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Read, "disguised", ("command", "git --version"));
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(disguised, default)).Decision);
    }

    [Fact] public async Task ExecutorHonoursCancellation()
    {
        var executor = new ToolExecutor(new WorkspacePathGuard(_root), []);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "list", ("path", "."));
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(call, cts.Token));
    }

    [Fact] public async Task ProjectScaffold_RequiresWriteApproval_AndRejectsUnknownTemplates()
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        var valid = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "create app",
            ("path", "apps/HelloGame"), ("template", "dotnet-console"), ("name", "HelloGame"));
        var invalid = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "unknown",
            ("path", "apps/Bad"), ("template", "arbitrary-script"), ("name", "Bad"));
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(valid, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(invalid, default)).Decision);
    }

    [Fact] public async Task ProjectScaffold_CreatesKnownTemplate_AndRefusesOverwrite()
    {
        var executor = new ToolExecutor(new WorkspacePathGuard(_root), []);
        var call = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "create app",
            ("path", "apps/HelloGame"), ("template", "dotnet-console"), ("name", "HelloGame"));
        var first = await executor.ExecuteAsync(call, default);
        Assert.True(first.Success, first.Error);
        Assert.True(File.Exists(Path.Combine(_root, "apps", "HelloGame", "HelloGame.csproj")));
        Assert.True(File.Exists(Path.Combine(_root, "apps", "HelloGame", "Program.cs")));
        var second = await executor.ExecuteAsync(call, default);
        Assert.False(second.Success); Assert.Contains("already exists", second.Error, StringComparison.Ordinal);
    }

    [Fact] public async Task UnityScaffold_RequiresExplicitValidEditorVersion()
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        var missing = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "create Unity game",
            ("path", "games/Test"), ("template", "unity-basic"), ("name", "TestGame"));
        var valid = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "create Unity game",
            ("path", "games/Test"), ("template", "unity-basic"), ("name", "TestGame"), ("unity_version", "6000.0.50f1"));
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(missing, default)).Decision);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(valid, default)).Decision);
    }

    [Fact] public async Task WebAsset_RequiresNetworkPermissionApprovedHostsAndPassiveType()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Game"));
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), [], ["assets.example.com", "catalog.example.com"]);
        var valid = AssetCall("https://assets.example.com/tree.glb", "tree.glb");
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(valid, default)).Decision);
        var wrongHost = AssetCall("https://untrusted.example/tree.glb", "tree.glb");
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(wrongHost, default)).Decision);
        var executable = AssetCall("https://assets.example.com/tool.exe", "tool.exe");
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(executable, default)).Decision);
        var downgraded = valid with { RequiredPermission = PermissionKind.Write };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(downgraded, default)).Decision);
    }

    [Fact] public async Task WebAsset_StagesBytesAndLicenseManifestWithoutExternalNetwork()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Game"));
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        using var http = new HttpClient(new StaticResponseHandler(payload));
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], ["assets.example.com", "catalog.example.com"], 1024, http);
        var result = await executor.ExecuteAsync(AssetCall("https://assets.example.com/tree.glb", "tree.glb"), default);
        Assert.True(result.Success, result.Error);
        var staged = Path.Combine(_root, "Game", ".forgecore", "staging", "tree.glb");
        Assert.Equal(payload, await File.ReadAllBytesAsync(staged));
        var manifest = await File.ReadAllTextAsync(staged + ".asset.json");
        Assert.Contains("CC0-1.0", manifest, StringComparison.Ordinal);
        Assert.Contains("sha256", manifest, StringComparison.Ordinal);
    }

    [Fact] public async Task WebAsset_ChecksumMismatchLeavesNoAssetOrPartialFile()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Game"));
        using var http = new HttpClient(new StaticResponseHandler([1, 2, 3]));
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], ["assets.example.com", "catalog.example.com"], 1024, http);
        var call = AssetCall("https://assets.example.com/tree.glb", "tree.glb") with
        {
            Arguments = new Dictionary<string, string>(AssetCall("https://assets.example.com/tree.glb", "tree.glb").Arguments)
            {
                ["sha256"] = new string('0', 64)
            }
        };
        var result = await executor.ExecuteAsync(call, default);
        Assert.False(result.Success); Assert.Contains("SHA-256", result.Error, StringComparison.Ordinal);
        var staging = Path.Combine(_root, "Game", ".forgecore", "staging");
        Assert.Empty(Directory.EnumerateFiles(staging));
    }

    [Fact] public async Task AssetCatalog_RequiresFeatureFlagNetworkPermissionAndApproval()
    {
        var call = CatalogCall();
        var disabled = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        Assert.Equal(SecurityDecision.Denied, (await disabled.ValidateAsync(call, default)).Decision);
        var enabled = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableOnlineCatalogSearch: true);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await enabled.ValidateAsync(call, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(call with { RequiredPermission = PermissionKind.Read }, default)).Decision);
    }

    [Fact] public async Task AssetCatalog_FiltersMetadataAndUsesOnlyProcessMemoryCache()
    {
        var payload = """
            {"results":[
              {"title":"Forest Path","creator":"Example Artist","license":"cc0","license_version":"1.0","license_url":"https://creativecommons.org/publicdomain/zero/1.0/","foreign_landing_url":"https://catalog.example/forest","url":"https://media.example/forest.jpg","thumbnail":"https://api.openverse.org/thumb/forest","attribution":"Forest Path by Example Artist","mature":false,"width":2048,"height":1024,"filetype":"jpg"},
              {"title":"Blocked NC Asset","creator":"Other","license":"by-nc","foreign_landing_url":"https://catalog.example/nc","url":"https://media.example/nc.jpg","mature":false}
            ]}
            """;
        var handler = new CatalogResponseHandler(payload);
        using var http = new HttpClient(handler);
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, http,
            enableOnlineCatalogSearch: true, maxCatalogResults: 8, maxCatalogResponseBytes: 16_384, onlineResultCacheMinutes: 10);
        var first = await executor.ExecuteAsync(CatalogCall(), default);
        var second = await executor.ExecuteAsync(CatalogCall(), default);
        Assert.True(first.Success, first.Error); Assert.True(second.Success, second.Error);
        Assert.Contains("UNTRUSTED REMOTE CATALOGUE METADATA", first.Output, StringComparison.Ordinal);
        Assert.Contains("Forest Path", first.Output, StringComparison.Ordinal);
        Assert.Contains("https://catalog.example/forest", first.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocked NC Asset", first.Output, StringComparison.Ordinal);
        Assert.Contains("mature=false", handler.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("api.openverse.org", handler.RequestUri.Host);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact] public async Task AssetCatalog_EnforcesResponseLimitAndCancellation()
    {
        using var oversizedHttp = new HttpClient(new CatalogResponseHandler(new string('x', 20_000)));
        using var oversized = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, oversizedHttp,
            enableOnlineCatalogSearch: true, maxCatalogResponseBytes: 16_384);
        var failed = await oversized.ExecuteAsync(CatalogCall(), default);
        Assert.False(failed.Success); Assert.Contains("response exceeds", failed.Error, StringComparison.OrdinalIgnoreCase);

        using var malformedHttp = new HttpClient(new CatalogResponseHandler("not-json"));
        using var malformed = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, malformedHttp,
            enableOnlineCatalogSearch: true, maxCatalogResponseBytes: 16_384);
        Assert.False((await malformed.ExecuteAsync(CatalogCall(), default)).Success);

        using var blockedHttp = new HttpClient(new CancellableCatalogHandler());
        using var blocked = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, blockedHttp,
            enableOnlineCatalogSearch: true, maxCatalogResponseBytes: 16_384);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.ExecuteAsync(CatalogCall(), source.Token));
    }

    [Fact] public async Task Generated3DAsset_RequiresUnityProjectFeatureKeyNetworkPermissionAndApproval()
    {
        await CreateBareUnityProjectAsync();
        var call = GeneratedAssetCall();
        var variable = "FORGECORE_TEST_SEELE_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        try
        {
            var disabled = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableSeeleGeneration: false,
                seeleApiKeyEnvironmentVariable: variable);
            Assert.Equal(SecurityDecision.Denied, (await disabled.ValidateAsync(call, default)).Decision);
            var missing = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableSeeleGeneration: true,
                seeleApiKeyEnvironmentVariable: variable);
            Assert.Equal(SecurityDecision.Denied, (await missing.ValidateAsync(call, default)).Decision);
            Environment.SetEnvironmentVariable(variable, "c4a_sk_test_1234567890");
            var enabled = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableSeeleGeneration: true,
                seeleApiKeyEnvironmentVariable: variable);
            Assert.Equal(SecurityDecision.ApprovalRequired, (await enabled.ValidateAsync(call, default)).Decision);
            Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(call with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact] public async Task Generated3DAsset_StagesInspectedGlbAndRedactsSignedUrlQuery()
    {
        await CreateBareUnityProjectAsync();
        var payload = MinimalGlb();
        using var http = new HttpClient(new StaticResponseHandler(payload));
        var generated = new Generated3DResult("job_test", new Uri("https://www.seeles.ai/workspace/job_test"), 42,
            [new Generated3DArtifact("Watchtower", new Uri("https://downloads.example/watchtower.glb?X-Amz-Signature=never-store-this"))]);
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024 * 1024, http,
            enableSeeleGeneration: true, generationProvider: new Fixed3DProvider(generated));

        var result = await executor.ExecuteAsync(GeneratedAssetCall(), default);
        Assert.True(result.Success, result.Error); Assert.Contains("job_test", result.Output, StringComparison.Ordinal);
        Assert.Contains("SHA-256", result.Output, StringComparison.Ordinal);
        var staged = Path.Combine(_root, "Game", ".forgecore", "staging", "frontier-watchtower.glb");
        Assert.True(File.Exists(staged));
        var manifest = await File.ReadAllTextAsync(staged + ".asset.json");
        Assert.Contains("SEELE_GENERATED_REVIEW_REQUIRED", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("never-store-this", manifest, StringComparison.Ordinal);
        Assert.Contains("https://downloads.example/watchtower.glb", manifest, StringComparison.Ordinal);
    }

    [Fact] public async Task Generated3DAsset_RejectsArchivesAndHonoursCancellation()
    {
        await CreateBareUnityProjectAsync();
        using var http = new HttpClient(new StaticResponseHandler([1, 2, 3]));
        var archive = new Generated3DResult("job_zip", new Uri("https://www.seeles.ai/workspace/job_zip"), null,
            [new Generated3DArtifact("Unity package", new Uri("https://downloads.example/project.zip"))]);
        using (var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, http,
                   enableSeeleGeneration: true, generationProvider: new Fixed3DProvider(archive)))
        {
            var result = await executor.ExecuteAsync(GeneratedAssetCall(), default);
            Assert.True(result.Success, result.Error); Assert.Contains("No supported", result.Output, StringComparison.Ordinal);
            var staging = Path.Combine(_root, "Game", ".forgecore", "staging");
            Assert.Empty(Directory.EnumerateFiles(staging));
        }
        using (var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024, http,
                   enableSeeleGeneration: true, generationProvider: new Blocking3DProvider()))
        using (var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(GeneratedAssetCall(), source.Token));
    }

    [Fact] public async Task SeeleProvider_UsesVersionedApiBearerAuthUnityEngineAndBoundedResultIdentity()
    {
        var variable = "FORGECORE_TEST_SEELE_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(variable, "c4a_sk_test_1234567890");
        try
        {
            var handler = new SeeleApiHandler(); using var http = new HttpClient(handler);
            var provider = new Seele3DGenerationProvider(http, variable, timeoutSeconds: 60, pollSeconds: 2);
            var result = await provider.GenerateAsync(Generated3DAssetSpec.From(GeneratedAssetCall()), default);
            Assert.Equal("job_123", result.JobId); Assert.Single(result.Artifacts); Assert.Equal(77, result.TotalCredits);
            Assert.True(handler.SawBearer); Assert.Contains("\"engine\":\"unity\"", handler.CreateBody, StringComparison.Ordinal);
            Assert.Contains("standalone textured GLB", handler.CreateBody, StringComparison.Ordinal);
            Assert.DoesNotContain("c4a_sk_test", handler.CreateBody, StringComparison.Ordinal);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact] public async Task MeshyGenerated3DAsset_RequiresFeatureKeyNetworkPermissionAndApproval()
    {
        await CreateBareUnityProjectAsync();
        var call = MeshyGeneratedAssetCall();
        var variable = "FORGECORE_TEST_MESHY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        try
        {
            var disabled = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableMeshyGeneration: false,
                meshyApiKeyEnvironmentVariable: variable);
            Assert.Equal(SecurityDecision.Denied, (await disabled.ValidateAsync(call, default)).Decision);
            var missing = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableMeshyGeneration: true,
                meshyApiKeyEnvironmentVariable: variable);
            Assert.Equal(SecurityDecision.Denied, (await missing.ValidateAsync(call, default)).Decision);
            Environment.SetEnvironmentVariable(variable, "msy_test_12345678901234567890");
            var enabled = new SecurityPolicy(new WorkspacePathGuard(_root), [], enableMeshyGeneration: true,
                meshyApiKeyEnvironmentVariable: variable);
            var approval = await enabled.ValidateAsync(call, default);
            Assert.Equal(SecurityDecision.ApprovalRequired, approval.Decision);
            Assert.Contains("credits", approval.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(SecurityDecision.Denied,
                (await enabled.ValidateAsync(call with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact] public async Task MeshyProvider_UsesPreviewThenPbrRefineBearerAuthAndBoundedTaskIdentity()
    {
        var variable = "FORGECORE_TEST_MESHY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(variable, "msy_test_12345678901234567890");
        try
        {
            var handler = new MeshyApiHandler(); using var http = new HttpClient(handler);
            var provider = new Meshy3DGenerationProvider(http, variable, timeoutSeconds: 60, pollSeconds: 2);
            var result = await provider.GenerateAsync(Generated3DAssetSpec.From(MeshyGeneratedAssetCall()), default);

            Assert.Equal("preview_123/refine_456", result.JobId);
            Assert.Equal(30, result.TotalCredits);
            Assert.Equal("https://downloads.meshy.example/watchtower.glb?signature=do-not-store", Assert.Single(result.Artifacts).DownloadUri.AbsoluteUri);
            Assert.True(handler.AllRequestsUsedBearer);
            Assert.Equal(2, handler.PostBodies.Count);
            Assert.Contains("\"mode\":\"preview\"", handler.PostBodies[0], StringComparison.Ordinal);
            Assert.Contains("\"ai_model\":\"meshy-6\"", handler.PostBodies[0], StringComparison.Ordinal);
            Assert.Contains("one original game-ready", handler.PostBodies[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"mode\":\"refine\"", handler.PostBodies[1], StringComparison.Ordinal);
            Assert.Contains("\"preview_task_id\":\"preview_123\"", handler.PostBodies[1], StringComparison.Ordinal);
            Assert.Contains("\"enable_pbr\":true", handler.PostBodies[1], StringComparison.Ordinal);
            Assert.All(handler.PostBodies, body => Assert.DoesNotContain("msy_test_", body, StringComparison.Ordinal));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact] public async Task MeshyProvider_HonoursCancellationWhilePolling()
    {
        var variable = "FORGECORE_TEST_MESHY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(variable, "msy_test_12345678901234567890");
        try
        {
            using var http = new HttpClient(new MeshyApiHandler(alwaysPending: true));
            var provider = new Meshy3DGenerationProvider(http, variable, timeoutSeconds: 60, pollSeconds: 2);
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                provider.GenerateAsync(Generated3DAssetSpec.From(MeshyGeneratedAssetCall()), source.Token));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact] public async Task MeshyGenerated3DAsset_StagesInspectedGlbAndRedactsSignedUrlQuery()
    {
        await CreateBareUnityProjectAsync();
        using var http = new HttpClient(new StaticResponseHandler(MinimalGlb()));
        var generated = new Generated3DResult("preview_123/refine_456", null, 30,
            [new Generated3DArtifact("Meshy refined PBR GLB", new Uri("https://downloads.meshy.example/watchtower.glb?signature=never-store-this"))]);
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024 * 1024, http,
            enableMeshyGeneration: true, meshyGenerationProvider: new FixedMeshy3DProvider(generated));

        var result = await executor.ExecuteAsync(MeshyGeneratedAssetCall(), default);
        Assert.True(result.Success, result.Error);
        Assert.Contains("preview_123/refine_456", result.Output, StringComparison.Ordinal);
        var staged = Path.Combine(_root, "Game", ".forgecore", "staging", "frontier-watchtower.glb");
        Assert.True(File.Exists(staged));
        var manifest = await File.ReadAllTextAsync(staged + ".asset.json");
        Assert.Contains("MESHY_GENERATED_REVIEW_REQUIRED", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("never-store-this", manifest, StringComparison.Ordinal);
        Assert.Contains("https://downloads.meshy.example/watchtower.glb", manifest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.4", false)]
    [InlineData("172.16.0.4", false)]
    [InlineData("192.168.1.4", false)]
    [InlineData("169.254.1.4", false)]
    [InlineData("100.64.0.4", false)]
    [InlineData("::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void WebAsset_PublicAddressFilter_BlocksLocalAndPrivateNetworks(string value, bool expected) =>
        Assert.Equal(expected, ToolExecutor.IsPublicAddress(IPAddress.Parse(value)));

    [Fact] public async Task TextPatch_RequiresApproval_CreatesBackup_AndAppliesUniqueReplacement()
    {
        var path = Path.Combine(_root, "Program.cs"); await File.WriteAllTextAsync(path, "Console.WriteLine(\"old\");\n");
        var call = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "change greeting",
            ("path", "Program.cs"), ("old_text", "old"), ("new_text", "new"));
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), []);
        var result = await executor.ExecuteAsync(call, default);
        Assert.True(result.Success, result.Error); Assert.Equal("Console.WriteLine(\"new\");\n", await File.ReadAllTextAsync(path));
        var backup = Directory.EnumerateFiles(Path.Combine(_root, ".forgecore", "backups"), "*.bak", SearchOption.AllDirectories).Single();
        Assert.Equal("Console.WriteLine(\"old\");\n", await File.ReadAllTextAsync(backup));
        var currentHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant();
        var restore = ToolCall.Create(ToolKind.RestoreTextSnapshot, PermissionKind.Write, "undo",
            ("path", "Program.cs"), ("snapshot_path", Path.GetRelativePath(_root, backup)), ("expected_current_sha256", currentHash));
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(restore, default)).Decision);
        Assert.True((await executor.ExecuteAsync(restore, default)).Success);
        Assert.Equal("Console.WriteLine(\"old\");\n", await File.ReadAllTextAsync(path));
    }

    [Fact] public async Task TextPatch_RejectsAmbiguousStaleAndInternalEdits()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "repeat.txt"), "same same");
        var guard = new WorkspacePathGuard(_root); using var executor = new ToolExecutor(guard, []);
        var ambiguous = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "bad",
            ("path", "repeat.txt"), ("old_text", "same"), ("new_text", "changed"));
        Assert.False((await executor.ExecuteAsync(ambiguous, default)).Success);
        var stale = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "stale",
            ("path", "repeat.txt"), ("old_text", "same same"), ("new_text", "changed"), ("expected_sha256", new string('0', 64)));
        Assert.False((await executor.ExecuteAsync(stale, default)).Success);
        Directory.CreateDirectory(Path.Combine(_root, ".forgecore")); await File.WriteAllTextAsync(Path.Combine(_root, ".forgecore", "internal.txt"), "x");
        var internalCall = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "internal",
            ("path", ".forgecore/internal.txt"), ("old_text", "x"), ("new_text", "y"));
        Assert.Equal(SecurityDecision.Denied, (await new SecurityPolicy(guard, []).ValidateAsync(internalCall, default)).Decision);
        Directory.CreateDirectory(Path.Combine(_root, "Game", ".forgecore"));
        var nestedInternal = ToolCall.Create(ToolKind.WriteTextFile, PermissionKind.Write, "nested internal",
            ("path", "Game/.forgecore/metadata.txt"), ("content", "blocked"));
        Assert.Equal(SecurityDecision.Denied, (await new SecurityPolicy(guard, []).ValidateAsync(nestedInternal, default)).Decision);
        Assert.False((await executor.ExecuteAsync(nestedInternal, default)).Success);
    }

    [Fact] public async Task SnapshotRestore_AllowsHashBoundSameProjectUnityBackupButNotCrossProject()
    {
        var guard = new WorkspacePathGuard(_root); var policy = new SecurityPolicy(guard, []); using var executor = new ToolExecutor(guard, []);
        var target = Path.Combine(_root, "Game", "ProjectSettings", "EditorBuildSettings.asset");
        var backup = Path.Combine(_root, "Game", ".forgecore", "unity", "backups", "approved", "EditorBuildSettings.asset.bak");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        await File.WriteAllTextAsync(target, "campaign settings"); await File.WriteAllTextAsync(backup, "prior settings");
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(target))).ToLowerInvariant();
        var restore = ToolCall.Create(ToolKind.RestoreTextSnapshot, PermissionKind.Write, "restore Unity setting",
            ("path", Path.GetRelativePath(_root, target)), ("snapshot_path", Path.GetRelativePath(_root, backup)),
            ("expected_current_sha256", hash));

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(restore, default)).Decision);
        var result = await executor.ExecuteAsync(restore, default);
        Assert.True(result.Success, result.Error); Assert.Equal("prior settings", await File.ReadAllTextAsync(target));
        Assert.Contains("Pre-restore backup", result.Output, StringComparison.Ordinal);

        var other = Path.Combine(_root, "Other", "ProjectSettings", "EditorBuildSettings.asset");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!); await File.WriteAllTextAsync(other, "other project");
        var crossProject = restore with { Arguments = new Dictionary<string, string>(restore.Arguments) { ["path"] = Path.GetRelativePath(_root, other) } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(crossProject, default)).Decision);
    }

    [Fact] public async Task DedicatedDotnetBuild_IsApprovedAndBuildsScaffoldOffline()
    {
        var guard = new WorkspacePathGuard(_root); using var executor = new ToolExecutor(guard, [], [], 1024,
            dotnetPath: FindDotnetHost(), dotnetTimeoutSeconds: 60);
        var scaffold = ToolCall.Create(ToolKind.ScaffoldProject, PermissionKind.Write, "create",
            ("path", "BuildProbe"), ("template", "dotnet-console"), ("name", "BuildProbe"));
        Assert.True((await executor.ExecuteAsync(scaffold, default)).Success);
        var build = ToolCall.Create(ToolKind.DotnetBuild, PermissionKind.Execute, "build",
            ("project_path", "BuildProbe/BuildProbe.csproj"), ("configuration", "Release"));
        var policy = new SecurityPolicy(guard, []);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(build, default)).Decision);
        var result = await executor.ExecuteAsync(build, default);
        Assert.True(result.Success, result.Error); Assert.Contains("dotnet build succeeded", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_root, "BuildProbe", "bin", "Release", "net10.0", "BuildProbe.dll")));
    }

    [Fact] public async Task RunningAllowListedProcess_IsKilledByCancellation()
    {
        const string command = "ping 127.0.0.1 -n 20";
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), [command]);
        var call = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "wait", ("command", command));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var started = DateTime.UtcNow;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(call, source.Token));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact] public async Task ProjectIndex_IsWorkspaceScopedMetadataOnlySearchableAndPersistent()
    {
        var project = Path.Combine(_root, "Game"); Directory.CreateDirectory(Path.Combine(project, "Source"));
        await File.WriteAllTextAsync(Path.Combine(project, "Source", "PlayerController.cs"),
            "namespace Demo; public sealed class PlayerController { private string apiKey = \"DO_NOT_STORE_VALUE\"; public void MovePlayer(float movementSpeed) { } }");
        await File.WriteAllTextAsync(Path.Combine(project, "appsettings.json"), "{\"password\":\"DO_NOT_STORE_PASSWORD\"}");
        Directory.CreateDirectory(Path.Combine(project, "bin"));
        await File.WriteAllTextAsync(Path.Combine(project, "bin", "Generated.cs"), "class GeneratedSecret { }");
        var database = Path.Combine(_root, "index.db"); var store = new SqliteForgeStore(database); await store.InitializeAsync();
        var guard = new WorkspacePathGuard(_root); using var executor = new ToolExecutor(guard, [], [], 1024,
            projectIndex: store, maxIndexFiles: 50, maxIndexFileBytes: 4096, maxIndexTotalBytes: 16384);
        var index = ToolCall.Create(ToolKind.IndexProject, PermissionKind.Read, "index", ("path", "Game"));
        var policy = new SecurityPolicy(guard, []);

        Assert.Equal(SecurityDecision.Allowed, (await policy.ValidateAsync(index, default)).Decision);
        var indexed = await executor.ExecuteAsync(index, default);
        Assert.True(indexed.Success, indexed.Error); Assert.Contains("1 files", indexed.Output, StringComparison.Ordinal);
        var search = ToolCall.Create(ToolKind.SearchProject, PermissionKind.Read, "find player code",
            ("path", "Game"), ("query", "PlayerController MovePlayer"), ("max_results", "5"));
        var found = await executor.ExecuteAsync(search, default);
        Assert.True(found.Success, found.Error); Assert.Contains("Game/Source/PlayerController.cs", found.Output, StringComparison.Ordinal);
        Assert.Contains("PlayerController", found.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("DO_NOT_STORE_VALUE", found.Output, StringComparison.Ordinal);

        var reopened = new SqliteForgeStore(database);
        var summary = await reopened.GetProjectIndexSummaryAsync("Game", default);
        var documents = await reopened.GetProjectIndexDocumentsAsync("Game", default);
        Assert.Equal(1, summary!.FileCount); Assert.Single(documents);
        Assert.DoesNotContain("DO_NOT_STORE_VALUE", documents[0].SearchTerms, StringComparison.Ordinal);
        Assert.DoesNotContain("DO_NOT_STORE_PASSWORD", documents[0].SearchTerms, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedSecret", documents[0].SearchTerms, StringComparison.Ordinal);
    }

    [Fact] public async Task ProjectIndex_EnforcesLimitsAndPreservesPriorIndexOnCancellation()
    {
        var project = Path.Combine(_root, "Large"); Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(Path.Combine(project, "One.cs"), "class One { }");
        await File.WriteAllTextAsync(Path.Combine(project, "Two.cs"), "class Two { }");
        var store = new CapturingIndexStore(); using var limited = new ToolExecutor(new WorkspacePathGuard(_root), [], [], 1024,
            projectIndex: store, maxIndexFiles: 1, maxIndexFileBytes: 1024, maxIndexTotalBytes: 1024);
        var call = ToolCall.Create(ToolKind.IndexProject, PermissionKind.Read, "index", ("path", "Large"));
        var result = await limited.ExecuteAsync(call, default);
        Assert.True(result.Success, result.Error); Assert.True(store.Summary!.IsTruncated); Assert.Single(store.Documents);

        store.BlockReplacement = true; using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limited.ExecuteAsync(call with { Id = Guid.NewGuid() }, source.Token));
        Assert.Single(store.Documents);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("\\\\server\\share")]
    public async Task ProjectRetrieval_BlocksTraversalAndUnc(string path)
    {
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        var index = ToolCall.Create(ToolKind.IndexProject, PermissionKind.Read, "bad index", ("path", path));
        var search = ToolCall.Create(ToolKind.SearchProject, PermissionKind.Read, "bad search",
            ("path", path), ("query", "player"), ("max_results", "5"));
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(index, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(search, default)).Decision);
    }

    [Fact] public async Task StagedAsset_InspectionAndImport_AreSeparatedAndNeverOverwrite()
    {
        var (hash, staged) = await CreateStagedAssetAsync("forest.png", MinimalPng(2, 3));
        var guard = new WorkspacePathGuard(_root); var policy = new SecurityPolicy(guard, []);
        using var executor = new ToolExecutor(guard, []);
        var inspect = InspectAssetCall("forest.png");
        var import = ImportAssetCall("forest.png", "Assets/Art/forest.png", hash);

        Assert.Equal(SecurityDecision.Allowed, (await policy.ValidateAsync(inspect, default)).Decision);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(import, default)).Decision);
        var inspected = await executor.ExecuteAsync(inspect, default);
        Assert.True(inspected.Success, inspected.Error);
        Assert.Contains("2x3 pixels", inspected.Output, StringComparison.Ordinal);
        Assert.Contains(hash, inspected.Output, StringComparison.Ordinal);

        var imported = await executor.ExecuteAsync(import, default);
        Assert.True(imported.Success, imported.Error);
        var target = Path.Combine(_root, "Game", "Assets", "Art", "forest.png");
        Assert.Equal(await File.ReadAllBytesAsync(staged), await File.ReadAllBytesAsync(target));
        var provenance = await File.ReadAllTextAsync(target + ".asset.json");
        Assert.Contains("CC0-1.0", provenance, StringComparison.Ordinal);
        Assert.Contains("source.example", provenance, StringComparison.Ordinal);
        Assert.Contains("importedUtc", provenance, StringComparison.Ordinal);
        Assert.True(File.Exists(staged));

        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(import, default)).Decision);
        var duplicate = await executor.ExecuteAsync(import, default);
        Assert.False(duplicate.Success); Assert.Contains("never overwrites", duplicate.Error, StringComparison.Ordinal);
    }

    [Fact] public async Task StagedAsset_TamperingStaleHashAndMalformedManifest_AreRejected()
    {
        var (hash, staged) = await CreateStagedAssetAsync("tampered.png", MinimalPng(4, 5));
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), []);
        var changed = await File.ReadAllBytesAsync(staged); changed[^1] ^= 1; await File.WriteAllBytesAsync(staged, changed);
        var tampered = await executor.ExecuteAsync(InspectAssetCall("tampered.png"), default);
        Assert.False(tampered.Success); Assert.Contains("no longer matches", tampered.Error, StringComparison.Ordinal);
        var target = Path.Combine(_root, "Game", "Assets", "tampered.png");
        Assert.False((await executor.ExecuteAsync(ImportAssetCall("tampered.png", "Assets/tampered.png", hash), default)).Success);
        Assert.False(File.Exists(target));

        var (freshHash, _) = await CreateStagedAssetAsync("stale.png", MinimalPng(6, 7));
        var stale = await executor.ExecuteAsync(ImportAssetCall("stale.png", "Assets/stale.png", new string('0', 64)), default);
        Assert.False(stale.Success); Assert.Contains("approved import hash", stale.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, "Game", "Assets", "stale.png")));
        Assert.NotEqual(new string('0', 64), freshHash);

        var (_, malformedPath) = await CreateStagedAssetAsync("extra.png", MinimalPng(8, 9));
        var manifestPath = malformedPath + ".asset.json";
        var json = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, json.TrimEnd('}', '\r', '\n') + ",\"unexpected\":true}");
        var malformed = await executor.ExecuteAsync(InspectAssetCall("extra.png"), default);
        Assert.False(malformed.Success); Assert.Contains("unexpected fields", malformed.Error, StringComparison.Ordinal);

        var (_, missingPath) = await CreateStagedAssetAsync("missing.png", MinimalPng(10, 11));
        var missingManifest = JsonSerializer.Deserialize<Dictionary<string, object?>>(await File.ReadAllTextAsync(missingPath + ".asset.json"))!;
        missingManifest.Remove("stagedUtc"); await File.WriteAllTextAsync(missingPath + ".asset.json", JsonSerializer.Serialize(missingManifest));
        var missing = await executor.ExecuteAsync(InspectAssetCall("missing.png"), default);
        Assert.False(missing.Success); Assert.Contains("malformed", missing.Error, StringComparison.Ordinal);
    }

    [Fact] public async Task StagedAsset_ProjectEscapesInternalTargetsAndPermissionDowngrades_AreDenied()
    {
        var (hash, _) = await CreateStagedAssetAsync("forest.png", MinimalPng(2, 2));
        var policy = new SecurityPolicy(new WorkspacePathGuard(_root), []);
        var escape = ImportAssetCall("forest.png", "../forest.png", hash);
        var internalTarget = ImportAssetCall("forest.png", ".forgecore/imported/forest.png", hash);
        var downgradedImport = ImportAssetCall("forest.png", "Assets/forest.png", hash) with { RequiredPermission = PermissionKind.Read };
        var downgradedInspect = InspectAssetCall("forest.png") with { RequiredPermission = PermissionKind.Write };

        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(escape, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(internalTarget, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(downgradedImport, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(downgradedInspect, default)).Decision);
    }

    [Fact] public async Task StagedAsset_RejectsExternalObjReferencesMalformedHeadersAndCancellation()
    {
        await CreateStagedAssetAsync("mesh.obj", Encoding.UTF8.GetBytes("mtllib material.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"));
        await CreateStagedAssetAsync("simple.obj", Encoding.UTF8.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"));
        await CreateStagedAssetAsync("bad.png", [1, 2, 3, 4]);
        await CreateStagedAssetAsync("external.glb", GlbWithExternalUri());
        using var executor = new ToolExecutor(new WorkspacePathGuard(_root), []);

        var external = await executor.ExecuteAsync(InspectAssetCall("mesh.obj"), default);
        Assert.False(external.Success); Assert.Contains("external material library", external.Error, StringComparison.Ordinal);
        var simple = await executor.ExecuteAsync(InspectAssetCall("simple.obj"), default);
        Assert.True(simple.Success, simple.Error); Assert.Contains("3 vertices; 1 faces", simple.Output, StringComparison.Ordinal);
        var malformed = await executor.ExecuteAsync(InspectAssetCall("bad.png"), default);
        Assert.False(malformed.Success); Assert.Contains("structure header", malformed.Error, StringComparison.Ordinal);
        var glb = await executor.ExecuteAsync(InspectAssetCall("external.glb"), default);
        Assert.False(glb.Success); Assert.Contains("URI references", glb.Error, StringComparison.Ordinal);

        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(InspectAssetCall("simple.obj"), source.Token));
    }

    [Fact] public async Task UnityBridge_InstallationIsApprovedVersionedAndNeverOverwrites()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var call = ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install bridge", ("project_path", "Game"));
        var policy = new SecurityPolicy(guard, []); using var executor = new ToolExecutor(guard, []);

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        var installed = await executor.ExecuteAsync(call, default);
        Assert.True(installed.Success, installed.Error); Assert.Contains("SHA-256", installed.Output, StringComparison.Ordinal);
        var bridge = Path.Combine(_root, "Game", "Assets", "ForgeCore", "Editor", "ForgeCoreBridge.cs");
        var manifest = bridge.Replace(".cs", ".version.json", StringComparison.Ordinal);
        Assert.True(File.Exists(bridge)); Assert.True(File.Exists(manifest));
        var bridgeSource = await File.ReadAllTextAsync(bridge);
        Assert.Contains("ForgeCoreBridge", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("Visual QA detected a likely unsupported-shader magenta frame", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("Visual QA detected a blank or near-uniform frame", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("bridgeVersion", await File.ReadAllTextAsync(manifest), StringComparison.Ordinal);
        Assert.True((await executor.ExecuteAsync(call, default)).Success);

        await File.AppendAllTextAsync(bridge, "// tampered");
        var changed = await File.ReadAllTextAsync(bridge);
        var refused = await executor.ExecuteAsync(call, default);
        Assert.False(refused.Success); Assert.Contains("neither the current release nor a hash-verified supported upgrade source", refused.Error, StringComparison.Ordinal);
        Assert.Equal(changed, await File.ReadAllTextAsync(bridge));
    }

    [Fact] public async Task UnityMapDemo_IsApprovalGatedHashVerifiedAndNeverOverwrites()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var call = ToolCall.Create(ToolKind.ScaffoldUnityMapDemo, PermissionKind.Write, "install map kit",
            ("project_path", "Game"), ("map_template", "frontier_valley"));
        var policy = new SecurityPolicy(guard, []); using var executor = new ToolExecutor(guard, []);

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(call with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        var invalid = call with { Arguments = new Dictionary<string, string> { ["project_path"] = "Game", ["map_template"] = "arbitrary_map" } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(invalid, default)).Decision);

        var installed = await executor.ExecuteAsync(call, default);
        Assert.True(installed.Success, installed.Error); Assert.Contains("hash-verified", installed.Output, StringComparison.Ordinal);
        var root = Path.Combine(_root, "Game", "Assets", "ForgeCoreMapDemo");
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "FrontierPlayer.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Editor", "FrontierValleyBuilder.cs")));
        Assert.True(File.Exists(Path.Combine(root, "forgecore-map-demo.manifest.json")));
        Assert.True((await executor.ExecuteAsync(call, default)).Success);

        var player = Path.Combine(root, "Runtime", "FrontierPlayer.cs");
        await File.AppendAllTextAsync(player, "// tampered"); var changed = await File.ReadAllTextAsync(player);
        var refused = await executor.ExecuteAsync(call, default);
        Assert.False(refused.Success); Assert.Contains("does not exactly match", refused.Error, StringComparison.Ordinal);
        Assert.Equal(changed, await File.ReadAllTextAsync(player));
    }

    [Theory]
    [InlineData("first_person_action")]
    [InlineData("platformer")]
    [InlineData("top_down_rpg")]
    [InlineData("racing")]
    public async Task UnityGameTemplateKit_IsApprovalGatedHashVerifiedAndNeverOverwrites(string templateId)
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var call = ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "install game template",
            ("project_path", "Game"), ("template_id", templateId));
        var policy = new SecurityPolicy(guard, []); using var executor = new ToolExecutor(guard, []);

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(call with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        var invalid = call with { Arguments = new Dictionary<string, string> { ["project_path"] = "Game", ["template_id"] = "arbitrary_template" } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(invalid, default)).Decision);

        var installed = await executor.ExecuteAsync(call, default);
        Assert.True(installed.Success, installed.Error); Assert.Contains("hash-verified", installed.Output, StringComparison.Ordinal);
        var root = Path.Combine(_root, "Game", "Assets", "ForgeCoreGameTemplates");
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "PrototypeGame.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "PrototypeActor.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "PrototypeObjective.cs")));
        Assert.True(File.Exists(Path.Combine(root, "Runtime", "PrototypeCamera.cs")));
        var typesSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeTypes.cs"));
        Assert.Contains("PrototypeInputFrame", typesSource, StringComparison.Ordinal);
        Assert.Contains("SetScriptedFrame", typesSource, StringComparison.Ordinal);
        var cameraSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeCamera.cs"));
        Assert.Contains("ConfigureProjection", cameraSource, StringComparison.Ordinal);
        Assert.Contains("orthographicSize", cameraSource, StringComparison.Ordinal);
        var gameSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeGame.cs"));
        Assert.Contains("ApplyVisualTheme", gameSource, StringComparison.Ordinal);
        Assert.Contains("Universal Render Pipeline/Lit", gameSource, StringComparison.Ordinal);
        Assert.Contains("RunAutomatedGameplayCheck", gameSource, StringComparison.Ordinal);
        Assert.Contains("FrameTimingManager.CaptureFrameTimings", gameSource, StringComparison.Ordinal);
        Assert.Contains("scripted_gameplay_v1", gameSource, StringComparison.Ordinal);
        Assert.Contains("CreatePlayerProfileInput", gameSource, StringComparison.Ordinal);
        Assert.Contains("playerProfileTravelMetres", gameSource, StringComparison.Ordinal);
        Assert.Contains("-forgecoreProfileRequestSha256", gameSource, StringComparison.Ordinal);
        Assert.Contains("FixedTimeEqualsHex", gameSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Input.Get", gameSource, StringComparison.Ordinal);
        Assert.Contains("PrototypeSceneNavigation.LoadPath", gameSource, StringComparison.Ordinal);
        Assert.Contains("art.Apply(); return;", gameSource, StringComparison.Ordinal);
        var artSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeArtDirection.cs"));
        Assert.Contains("parts[0] != \"Yard\"", artSource);
        Assert.Contains("out Finish finish)) continue", artSource);
        Assert.Contains("AmbientMode.Trilight", artSource, StringComparison.Ordinal);
        Assert.Contains("renderer.sharedMaterial = MaterialFor(finish)", artSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var texture in textures)", artSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityWebRequest", artSource, StringComparison.Ordinal);
        var yardSource = await File.ReadAllTextAsync(Path.Combine(root, "Editor", "ForgeCoreIndustrialYard.cs"));
        Assert.Contains("TankTower", yardSource, StringComparison.Ordinal);
        Assert.Contains("Cargo corrugation", yardSource, StringComparison.Ordinal);
        Assert.Contains("parent.GetComponent<Renderer>().enabled = false", yardSource, StringComparison.Ordinal);
        Assert.Contains("PrototypeRangedEnemy", yardSource, StringComparison.Ordinal);
        var projectileSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeProjectile.cs"));
        Assert.Contains("Physics.RaycastAll", projectileSource, StringComparison.Ordinal);
        Assert.Contains("MaximumActive = 96", projectileSource, StringComparison.Ordinal);
        Assert.Contains("spent = true", projectileSource, StringComparison.Ordinal);
        var animationSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeCombatAnimation.cs"));
        Assert.Contains("legacy = true", animationSource, StringComparison.Ordinal);
        Assert.Contains("LeftHip/Knee", animationSource, StringComparison.Ordinal);
        var combatTests = await File.ReadAllTextAsync(Path.Combine(root, "Tests", "PlayMode", "PrototypeCombatTests.cs"));
        Assert.Contains("VisibleBullets_TravelBeforeDamageAndCannotCrossCover", combatTests, StringComparison.Ordinal);
        var progressSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeCampaignProgress.cs"));
        Assert.Contains("public static class PrototypeSceneNavigation", progressSource, StringComparison.Ordinal);
        Assert.Contains("BeginCapture", progressSource, StringComparison.Ordinal);
        var actorSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeActor.cs"));
        Assert.Contains("ActionEvents", actorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Input.Get", actorSource, StringComparison.Ordinal);
        var feedbackSource = await File.ReadAllTextAsync(Path.Combine(root, "Runtime", "PrototypeFeedback.cs"));
        Assert.Contains("ParticleSystemStopBehavior.StopEmittingAndClear", feedbackSource, StringComparison.Ordinal);
        var builder = Path.Combine(root, "Editor", "ForgeCorePrototypeBuilder.cs");
        Assert.True(File.Exists(builder));
        var builderSource = await File.ReadAllTextAsync(builder);
        Assert.Contains("ValidateGameplayContract", builderSource, StringComparison.Ordinal);
        Assert.Contains("Racing checkpoints accepted an out-of-order completion", builderSource, StringComparison.Ordinal);
        Assert.Contains("CheckpointGate", builderSource, StringComparison.Ordinal);
        var campaignTests = Path.Combine(root, "Tests", "EditMode", "PrototypeCampaignTests.cs");
        Assert.True(File.Exists(campaignTests));
        var campaignTestSource = await File.ReadAllTextAsync(campaignTests);
        Assert.Contains("SavedCampaigns_HaveContiguousLevelsAndExistingTransitions", campaignTestSource, StringComparison.Ordinal);
        Assert.Contains("SerializedCampaignObjectiveRulesRemainReachableAndOrdered", campaignTestSource, StringComparison.Ordinal);
        Assert.Contains("ScriptedControlFrame_ClampsAnalogueAxes", campaignTestSource, StringComparison.Ordinal);
        var playModeTests = Path.Combine(root, "Tests", "PlayMode", "PrototypeRuntimeInputTests.cs");
        Assert.True(File.Exists(playModeTests));
        var playModeSource = await File.ReadAllTextAsync(playModeTests);
        Assert.Contains("ScriptedControlFrames_DriveMovementPrimaryActionPauseAndRespawnAcrossEveryGenre", playModeSource, StringComparison.Ordinal);
        Assert.Contains("CampaignCompletion_PersistsMonotonicUnlockAndRequestsExactNextScene", playModeSource, StringComparison.Ordinal);
        Assert.Contains("TopDownTarget_ChasesDealsContactDamageAndFallsToRealAttackFrames", playModeSource, StringComparison.Ordinal);
        Assert.Contains("yield return null", playModeSource, StringComparison.Ordinal);
        Assert.Contains("WaitForSecondsRealtime(0.02f)", playModeSource, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "Tests", "PlayMode", "ForgeCoreGameTemplates.Tests.PlayMode.asmdef")));
        Assert.True(File.Exists(Path.Combine(root, "forgecore-game-templates.manifest.json")));
        Assert.True((await executor.ExecuteAsync(call, default)).Success);

        var runtime = Path.Combine(root, "Runtime", "PrototypeActor.cs");
        await File.AppendAllTextAsync(runtime, "// tampered"); var changed = await File.ReadAllTextAsync(runtime);
        var refused = await executor.ExecuteAsync(call, default);
        Assert.False(refused.Success); Assert.Contains("does not exactly match", refused.Error, StringComparison.Ordinal);
        Assert.Equal(changed, await File.ReadAllTextAsync(runtime));
    }

    [Fact] public async Task UnityGameTemplateScene_IsTypedHashVerifiedAndBacksUpProjectSettings()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "template",
                ("project_path", "Game"), ("template_id", "racing")), default)).Success);
        }
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var settings = Path.Combine(_root, "Game", "ProjectSettings", "EditorBuildSettings.asset"); await File.WriteAllTextAsync(settings, "original settings");
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("create_game_template", root.GetProperty("operation").GetString());
            Assert.Equal("racing", root.GetProperty("templateId").GetString()); Assert.Equal(90210, root.GetProperty("seed").GetInt32());
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "create_game_template", success = true, summary = "Racing prototype created."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "create racing scene",
            ("project_path", "Game"), ("operation", "create_game_template"), ("scene_path", "Assets/Scenes/Racing.unity"),
            ("template_id", "racing"), ("seed", "90210"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        var result = await executor.ExecuteAsync(call, default);
        Assert.True(result.Success, result.Error); Assert.Contains("Racing prototype created", result.Output, StringComparison.Ordinal);
        var backup = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "backups"),
            "EditorBuildSettings.asset.bak", SearchOption.AllDirectories).Single();
        Assert.Equal("original settings", await File.ReadAllTextAsync(backup));
    }

    [Fact] public async Task UnityGameCampaign_IsBoundedLinkedBackedUpAndRefusesAnyExistingLevel()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "template",
                ("project_path", "Game"), ("template_id", "first_person_action")), default)).Success);
        }
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var buildSettings = Path.Combine(_root, "Game", "ProjectSettings", "EditorBuildSettings.asset");
        var graphicsSettings = Path.Combine(_root, "Game", "ProjectSettings", "GraphicsSettings.asset");
        var qualitySettings = Path.Combine(_root, "Game", "ProjectSettings", "QualitySettings.asset");
        await File.WriteAllTextAsync(buildSettings, "original build settings");
        await File.WriteAllTextAsync(graphicsSettings, "original graphics settings");
        await File.WriteAllTextAsync(qualitySettings, "original quality settings");
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            var operation = root.GetProperty("operation").GetString();
            Assert.True(operation is "create_game_campaign" or "build_windows");
            Assert.Equal("Assets/Scenes/Campaigns/ForgeCampaign-Level01.unity", root.GetProperty("scenePath").GetString());
            Assert.Equal(3, root.GetProperty("levelCount").GetInt32());
            if (operation == "create_game_campaign")
            {
                Assert.Equal("first_person_action", root.GetProperty("templateId").GetString());
                Assert.Equal(84001, root.GetProperty("seed").GetInt32());
            }
            else
            {
                var executable = Path.Combine(_root, "Game", root.GetProperty("outputPath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
                var data = Path.Combine(Path.GetDirectoryName(executable)!, Path.GetFileNameWithoutExtension(executable) + "_Data");
                Directory.CreateDirectory(data); await File.WriteAllBytesAsync(executable, [1, 2, 3], token);
                await File.WriteAllBytesAsync(Path.Combine(data, "level0"), [4, 5, 6], token);
            }
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation, success = true,
                summary = operation == "create_game_campaign" ? "Created three linked first-person levels." : "Built three linked first-person levels."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "create linked campaign",
            ("project_path", "Game"), ("operation", "create_game_campaign"),
            ("scene_path", "Assets/Scenes/Campaigns/ForgeCampaign-Level01.unity"),
            ("template_id", "first_person_action"), ("seed", "84001"), ("level_count", "3"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        var result = await executor.ExecuteAsync(call, default);
        Assert.True(result.Success, result.Error); Assert.Contains("three linked", result.Output, StringComparison.Ordinal);
        var backups = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "backups"), "*.bak", SearchOption.AllDirectories).ToArray();
        Assert.Equal(3, backups.Length);
        Assert.Contains(backups, path => File.ReadAllText(path) == "original build settings");
        Assert.Contains(backups, path => File.ReadAllText(path) == "original graphics settings");
        Assert.Contains(backups, path => File.ReadAllText(path) == "original quality settings");

        var campaignScenes = UnityEditorActionSpec.From(call).GetCampaignScenePaths();
        foreach (var relative in campaignScenes)
        {
            var target = Path.Combine(_root, "Game", relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); await File.WriteAllTextAsync(target, "existing level");
        }
        var campaignBuild = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "build linked campaign",
            ("project_path", "Game"), ("operation", "build_windows"),
            ("output_path", "Builds/Campaign/Campaign.exe"), ("development_build", "true"),
            ("scene_path", campaignScenes[0]), ("level_count", "3"));
        var campaignBuildPolicy = await policy.ValidateAsync(campaignBuild, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, campaignBuildPolicy.Decision);
        Assert.Contains("exactly 3 linked campaign scenes", campaignBuildPolicy.Reason, StringComparison.Ordinal);
        var built = await executor.ExecuteAsync(campaignBuild, default);
        Assert.True(built.Success, built.Error);
        var provenancePath = Path.Combine(_root, "Game", "Builds", "Campaign", "Campaign.exe.forgecore-build.json");
        using (var provenance = JsonDocument.Parse(await File.ReadAllBytesAsync(provenancePath)))
        {
            Assert.Equal(3, provenance.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(3, provenance.RootElement.GetProperty("sceneCount").GetInt32());
            var scenes = provenance.RootElement.GetProperty("scenes").EnumerateArray().ToArray();
            Assert.Equal(campaignScenes, scenes.Select(scene => scene.GetProperty("path").GetString()));
            Assert.All(scenes, scene => Assert.Equal(64, scene.GetProperty("sha256").GetString()!.Length));
        }

        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(call, default)).Decision);
        var refused = await executor.ExecuteAsync(call, default);
        Assert.False(refused.Success); Assert.Contains("already exists", refused.Error, StringComparison.OrdinalIgnoreCase);
        Assert.All(campaignScenes, relative => Assert.Equal("existing level", File.ReadAllText(Path.Combine(_root, "Game", relative.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [Fact] public async Task UnityPolicy_FailsClosedWithoutEditorBridgeOrValidPaths()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using var installer = new ToolExecutor(guard, []);
        var install = ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game"));
        Assert.True((await installer.ExecuteAsync(install, default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var capability = UnityCall("capabilities");

        Assert.Equal(SecurityDecision.Denied, (await new SecurityPolicy(guard, []).ValidateAsync(capability, default)).Decision);
        var configured = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        Assert.Equal(SecurityDecision.ApprovalRequired, (await configured.ValidateAsync(capability, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.65f1").ValidateAsync(capability, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await configured.ValidateAsync(capability with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        var escape = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "bad scene",
            ("project_path", "Game"), ("operation", "create_scene"), ("scene_path", "../Outside.unity"), ("scene_template", "empty"));
        Assert.Equal(SecurityDecision.Denied, (await configured.ValidateAsync(escape, default)).Decision);
        var missingScene = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "missing scene",
            ("project_path", "Game"), ("operation", "create_primitive"), ("scene_path", "Assets/Missing.unity"),
            ("object_name", "Cube"), ("primitive", "cube"), ("position_x", "0"), ("position_y", "0"), ("position_z", "0"));
        Assert.Equal(SecurityDecision.Denied, (await configured.ValidateAsync(missingScene, default)).Decision);
        Directory.CreateDirectory(Path.Combine(_root, "Game", "Builds", "Windows"));
        await File.WriteAllBytesAsync(Path.Combine(_root, "Game", "Builds", "Windows", "Game.exe"), [1]);
        var overwriteBuild = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "overwrite build",
            ("project_path", "Game"), ("operation", "build_windows"), ("output_path", "Builds/Windows/Game.exe"), ("development_build", "true"));
        Assert.Equal(SecurityDecision.Denied, (await configured.ValidateAsync(overwriteBuild, default)).Decision);
    }

    [Fact] public async Task UnityProjectLease_SerializesConcurrentActionsForTheSameProject()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var active = 0; var maximum = 0; var sync = new object();
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            lock (sync) { active++; maximum = Math.Max(maximum, active); }
            try
            {
                await Task.Delay(100, token);
                var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
                using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
                await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
                {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                    requestId = root.GetProperty("requestId").GetString(), operation = "compile", success = true, summary = "Compiled."
                }), token);
                return new UnityProcessResult(0, string.Empty, string.Empty);
            }
            finally { lock (sync) active--; }
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);
        var first = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "compile one", ("project_path", "Game"), ("operation", "compile"));
        var second = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "compile two", ("project_path", "Game"), ("operation", "compile"));

        var results = await Task.WhenAll(executor.ExecuteAsync(first, default), executor.ExecuteAsync(second, default));

        Assert.All(results, result => Assert.True(result.Success, result.Error));
        Assert.Equal(1, maximum);
    }

    [Fact] public async Task UnityExecutor_UsesHashBoundNoPackageBatchProtocolAndParsesResult()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        IReadOnlyList<string>? captured = null;
        UnityProcessRunner runner = async (_, arguments, _, timeout, token) =>
        {
            token.ThrowIfCancellationRequested(); captured = arguments.ToArray(); Assert.InRange(timeout.TotalSeconds, 30, 600);
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            var approvedHash = ValueAfter(arguments, "-forgecoreRequestSha256"); var bytes = await File.ReadAllBytesAsync(requestPath, token);
            Assert.Equal(approvedHash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var request = JsonDocument.Parse(bytes); var id = request.RootElement.GetProperty("requestId").GetString()!;
            var operation = request.RootElement.GetProperty("operation").GetString()!;
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = id, operation, success = true, summary = "Fake capability handshake passed."
            }), token);
            await File.WriteAllTextAsync(ValueAfter(arguments, "-logFile"), "bounded fake Unity log", token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(UnityCall("capabilities"), default);

        Assert.True(result.Success, result.Error); Assert.Contains("6000.0.50f1", result.Output, StringComparison.Ordinal);
        Assert.Contains("-batchmode", captured!); Assert.Contains("-quit", captured!); Assert.Contains("-noUpm", captured!);
        Assert.DoesNotContain(captured!, x => x.Contains("powershell", StringComparison.OrdinalIgnoreCase) || x.Contains("cmd", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "requests")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "results")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "logs")));

        var buildSettingsPath = Path.Combine(_root, "Game", "ProjectSettings", "EditorBuildSettings.asset");
        await File.WriteAllTextAsync(buildSettingsPath, "original build settings yaml");
        var createScene = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "create scene",
            ("project_path", "Game"), ("operation", "create_scene"), ("scene_path", "Assets/New.unity"), ("scene_template", "basic_3d"));
        Assert.True((await executor.ExecuteAsync(createScene, default)).Success);
        var settingsBackup = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "backups"),
            "EditorBuildSettings.asset.bak", SearchOption.AllDirectories).Single();
        Assert.Equal("original build settings yaml", await File.ReadAllTextAsync(settingsBackup));
        Assert.True(File.Exists(settingsBackup + ".json"));

        var scenePath = Path.Combine(_root, "Game", "Assets", "Main.unity"); await File.WriteAllTextAsync(scenePath, "original scene yaml");
        var primitive = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "add cube",
            ("project_path", "Game"), ("operation", "create_primitive"), ("scene_path", "Assets/Main.unity"),
            ("object_name", "Cube"), ("primitive", "cube"), ("position_x", "0"), ("position_y", "1"), ("position_z", "0"));
        Assert.True((await executor.ExecuteAsync(primitive, default)).Success);
        var backup = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "backups"), "Main.unity.bak", SearchOption.AllDirectories).Single();
        Assert.Equal("original scene yaml", await File.ReadAllTextAsync(backup));
        Assert.True(File.Exists(backup + ".json"));

        UnityProcessRunner mismatched = async (_, arguments, _, _, token) =>
        {
            var resultPath = ValueAfter(arguments, "-forgecoreResult"); var requestPath = ValueAfter(arguments, "-forgecoreRequest");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token));
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = "wrong", operation = request.RootElement.GetProperty("operation").GetString(), success = true, summary = "untrusted"
            }), token);
            return new UnityProcessResult(0, "", "");
        };
        using var mismatchExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: mismatched);
        var mismatch = await mismatchExecutor.ExecuteAsync(UnityCall("capabilities"), default);
        Assert.False(mismatch.Success); Assert.Contains("identity", mismatch.Error, StringComparison.Ordinal);
    }

    [Fact] public async Task UnityPlayerSmokeTest_RequiresHashBoundForgeCoreBuildAndRejectsTamperingOrCrashMarkers()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var output = "Builds/Smoke/Smoke.exe";
        var smoke = ToolCall.Create(ToolKind.UnityPlayerSmokeTest, PermissionKind.Execute, "smoke test player",
            ("project_path", "Game"), ("output_path", output), ("duration_seconds", "10"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(smoke, default)).Decision);

        UnityProcessRunner buildRunner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            var relative = root.GetProperty("outputPath").GetString()!;
            var executable = Path.Combine(_root, "Game", relative.Replace('/', Path.DirectorySeparatorChar));
            var data = Path.Combine(Path.GetDirectoryName(executable)!, Path.GetFileNameWithoutExtension(executable) + "_Data");
            Directory.CreateDirectory(data); await File.WriteAllBytesAsync(executable, [1, 2, 3, 4], token);
            await File.WriteAllBytesAsync(Path.Combine(data, "level0"), [5, 6, 7, 8], token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "build_windows", success = true, summary = "Fake build succeeded."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        var cleanInvoked = false;
        UnityPlayerSmokeRunner cleanRunner = async (executable, workingDirectory, logPath, duration, token) =>
        {
            cleanInvoked = true; Assert.EndsWith("Smoke.exe", executable, StringComparison.Ordinal);
            Assert.Equal(Path.GetDirectoryName(executable), workingDirectory); Assert.Equal(TimeSpan.FromSeconds(10), duration);
            await File.WriteAllTextAsync(logPath, "Player initialized and remained healthy.", token);
            return new UnityPlayerSmokeResult(true, null);
        };
        using (var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityProcessRunner: buildRunner, unityPlayerSmokeRunner: cleanRunner))
        {
            var build = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "build",
                ("project_path", "Game"), ("operation", "build_windows"), ("output_path", output), ("development_build", "true"));
            var built = await executor.ExecuteAsync(build, default);
            Assert.True(built.Success, built.Error); Assert.Contains("Build provenance:", built.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(_root, "Game", output.Replace('/', Path.DirectorySeparatorChar)) + ".forgecore-build.json"));
            Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(smoke, default)).Decision);
            var passed = await executor.ExecuteAsync(smoke, default);
            Assert.True(passed.Success, passed.Error); Assert.True(cleanInvoked); Assert.Contains("verified build provenance", passed.Output, StringComparison.Ordinal);
        }

        var level0 = Path.Combine(_root, "Game", "Builds", "Smoke", "Smoke_Data", "level0");
        await File.AppendAllTextAsync(level0, "tampered"); cleanInvoked = false;
        using (var tamperExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerSmokeRunner: cleanRunner))
        {
            var denied = await tamperExecutor.ExecuteAsync(smoke, default);
            Assert.False(denied.Success); Assert.False(cleanInvoked); Assert.Contains("hashes", denied.Error, StringComparison.OrdinalIgnoreCase);
        }

        await File.WriteAllBytesAsync(level0, [5, 6, 7, 8]);
        UnityPlayerSmokeRunner crashRunner = async (_, _, logPath, _, token) =>
        {
            await File.WriteAllTextAsync(logPath, "The file level0 is corrupted!\nCrash!!!", token);
            return new UnityPlayerSmokeResult(true, null);
        };
        using var crashExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityPlayerSmokeRunner: crashRunner);
        var crashed = await crashExecutor.ExecuteAsync(smoke, default);
        Assert.False(crashed.Success); Assert.Contains("failure marker", crashed.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task UnityPlayerProfileTest_IsRenderedHashBoundBudgetedAndRejectsTamperingMismatchAndTimeout()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install",
                ("project_path", "Game")), default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "install templates",
                ("project_path", "Game"), ("template_id", "first_person_action")), default)).Success);
        }
        var sceneRelative = "Assets/Scenes/Profile.unity";
        var scene = Path.Combine(_root, "Game", sceneRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scene)!); await File.WriteAllTextAsync(scene, "profile scene");
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var output = "Builds/Profile/Profile.exe";
        var profile = ToolCall.Create(ToolKind.UnityPlayerProfileTest, PermissionKind.Execute, "profile rendered player",
            ("project_path", "Game"), ("output_path", output), ("duration_seconds", "10"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(profile, default)).Decision);

        UnityProcessRunner buildRunner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            var relative = root.GetProperty("outputPath").GetString()!;
            var executable = Path.Combine(_root, "Game", relative.Replace('/', Path.DirectorySeparatorChar));
            var data = Path.Combine(Path.GetDirectoryName(executable)!, Path.GetFileNameWithoutExtension(executable) + "_Data");
            Directory.CreateDirectory(data); await File.WriteAllBytesAsync(executable, [1, 2, 3, 4], token);
            await File.WriteAllBytesAsync(Path.Combine(data, "level0"), [5, 6, 7, 8], token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "build_windows", success = true, summary = "Fake profile build succeeded."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        var build = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "build profiled player",
            ("project_path", "Game"), ("operation", "build_windows"), ("output_path", output),
            ("development_build", "true"), ("scene_path", sceneRelative));
        using (var builder = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityProcessRunner: buildRunner))
        {
            var built = await builder.ExecuteAsync(build, default);
            Assert.True(built.Success, built.Error);
        }
        var executablePath = Path.Combine(_root, "Game", output.Replace('/', Path.DirectorySeparatorChar));
        var provenancePath = executablePath + ".forgecore-build.json";
        using (var provenance = JsonDocument.Parse(await File.ReadAllBytesAsync(provenancePath)))
        {
            Assert.Equal(3, provenance.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("0.16.33", provenance.RootElement.GetProperty("templateKitVersion").GetString());
            Assert.Equal(64, provenance.RootElement.GetProperty("templateKitManifestSha256").GetString()!.Length);
        }
        var policyResult = await policy.ValidateAsync(profile, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, policyResult.Decision);
        Assert.Contains("visible rendering", policyResult.Reason, StringComparison.Ordinal);
        Assert.Contains("1280x720", policyResult.Reason, StringComparison.Ordinal);

        static async Task WriteProfileResultAsync(string requestPath, string resultPath, string requestSha256,
            string logPath, double p95Frame, string? requestIdOverride, CancellationToken token,
            double playerTravelMetres = 17.5, int actionEvents = 24)
        {
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("scripted_gameplay_v1", root.GetProperty("profileScenario").GetString());
            await File.WriteAllTextAsync(logPath, "Player initialized. ForgeCore standalone player profile completed.", token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2, requestId = requestIdOverride ?? root.GetProperty("requestId").GetString(), requestSha256,
                templateKitVersion = "0.16.33", environment = "standalone_player",
                executableSha256 = root.GetProperty("executableSha256").GetString(), level0Sha256 = root.GetProperty("level0Sha256").GetString(),
                buildProvenanceSha256 = root.GetProperty("buildProvenanceSha256").GetString(), scenePath = root.GetProperty("scenePath").GetString(),
                sceneSha256 = root.GetProperty("sceneSha256").GetString(), durationSeconds = root.GetProperty("durationSeconds").GetInt32(),
                width = 1280, height = 720, sceneBuildIndex = 0, sceneName = "Profile", sampledFrames = 600,
                averageFrameMilliseconds = 12.5, p95FrameMilliseconds = p95Frame, maximumFrameMilliseconds = Math.Max(25, p95Frame + 5),
                gpuTimingAvailable = false, sampledGpuFrames = 0, averageGpuMilliseconds = 0, p95GpuMilliseconds = 0, maximumGpuMilliseconds = 0,
                peakAllocatedMemoryBytes = 536870912L, rendererCount = 20, meshCount = 8, vertexCount = 25000L, triangleCount = 12000L,
                cameraCount = 1, graphicsDeviceName = "Test GPU", graphicsDeviceType = "Direct3D12", graphicsDeviceVersion = "Direct3D 12",
                qualityLevel = 2, vSyncCount = 0, targetFrameRate = -1, profileScenario = "scripted_gameplay_v1",
                templateId = "first_person_action", gameplayFrames = 600, playerTravelMetres, actionEvents,
                completedObjectives = 1, requiredObjectives = 6, minimumHealth = 78, completedUtc = DateTimeOffset.UtcNow
            }), token);
        }

        var profileInvoked = false;
        UnityPlayerProfileRunner passingRunner = async (executable, workingDirectory, logPath, requestPath, resultPath, requestSha256, duration, token) =>
        {
            profileInvoked = true; Assert.Equal(executablePath, executable); Assert.Equal(Path.GetDirectoryName(executable), workingDirectory);
            Assert.Equal(TimeSpan.FromSeconds(10), duration); Assert.False(File.Exists(resultPath)); Assert.Equal(await HashAsync(requestPath), requestSha256);
            await WriteProfileResultAsync(requestPath, resultPath, requestSha256, logPath, 20, null, token);
            return new UnityPlayerProfileRunResult(false, 0);
        };
        using (var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: passingRunner))
        {
            var passed = await executor.ExecuteAsync(profile, default);
            Assert.True(passed.Success, passed.Error); Assert.True(profileInvoked);
            Assert.Contains("rendered frames", passed.Output, StringComparison.Ordinal);
        }
        var profileRoot = Path.Combine(_root, "Game", ".forgecore", "unity", "player-profile");
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(profileRoot, "requests")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(profileRoot, "results")));
        var firstEvidence = Directory.EnumerateFiles(Path.Combine(profileRoot, "evidence"), "*.json").Single();
        using (var evidence = JsonDocument.Parse(await File.ReadAllBytesAsync(firstEvidence)))
        {
            Assert.True(evidence.RootElement.GetProperty("workstationBaselineBudgetPassed").GetBoolean());
            Assert.False(evidence.RootElement.GetProperty("gpuTimingAvailable").GetBoolean());
            Assert.False(evidence.RootElement.GetProperty("gpuBudgetPassed").GetBoolean());
            Assert.Equal(2, evidence.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("0.16.33", evidence.RootElement.GetProperty("templateKitVersion").GetString());
            Assert.Equal("scripted_gameplay_v1", evidence.RootElement.GetProperty("profileScenario").GetString());
            Assert.Equal("first_person_action", evidence.RootElement.GetProperty("templateId").GetString());
            Assert.Equal(17.5, evidence.RootElement.GetProperty("playerTravelMetres").GetDouble());
            Assert.Equal(24, evidence.RootElement.GetProperty("actionEvents").GetInt32());
        }

        await File.AppendAllTextAsync(executablePath, "tampered"); profileInvoked = false;
        using (var tamperExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: passingRunner))
        {
            var tampered = await tamperExecutor.ExecuteAsync(profile, default);
            Assert.False(tampered.Success); Assert.False(profileInvoked); Assert.Contains("hashes", tampered.Error, StringComparison.OrdinalIgnoreCase);
        }
        await File.WriteAllBytesAsync(executablePath, [1, 2, 3, 4]);

        UnityPlayerProfileRunner slowRunner = async (_, _, logPath, requestPath, resultPath, requestSha256, _, token) =>
        {
            await WriteProfileResultAsync(requestPath, resultPath, requestSha256, logPath, 40, null, token);
            return new UnityPlayerProfileRunResult(false, 0);
        };
        using (var slowExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: slowRunner))
        {
            var slow = await slowExecutor.ExecuteAsync(profile, default);
            Assert.False(slow.Success); Assert.Contains("budget failed", slow.Error, StringComparison.OrdinalIgnoreCase);
        }
        var evidenceFiles = Directory.EnumerateFiles(Path.Combine(profileRoot, "evidence"), "*.json").ToArray();
        Assert.Equal(2, evidenceFiles.Length);
        Assert.Contains(evidenceFiles, path =>
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return !document.RootElement.GetProperty("workstationBaselineBudgetPassed").GetBoolean();
        });

        UnityPlayerProfileRunner mismatchRunner = async (_, _, logPath, requestPath, resultPath, requestSha256, _, token) =>
        {
            await WriteProfileResultAsync(requestPath, resultPath, requestSha256, logPath, 20, new string('0', 32), token);
            return new UnityPlayerProfileRunResult(false, 0);
        };
        using (var mismatchExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: mismatchRunner))
        {
            var mismatch = await mismatchExecutor.ExecuteAsync(profile, default);
            Assert.False(mismatch.Success); Assert.Contains("identity", mismatch.Error, StringComparison.OrdinalIgnoreCase);
        }

        UnityPlayerProfileRunner idleRunner = async (_, _, logPath, requestPath, resultPath, requestSha256, _, token) =>
        {
            await WriteProfileResultAsync(requestPath, resultPath, requestSha256, logPath, 20, null, token,
                playerTravelMetres: 0, actionEvents: 0);
            return new UnityPlayerProfileRunResult(false, 0);
        };
        using (var idleExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
                   unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: idleRunner))
        {
            var idle = await idleExecutor.ExecuteAsync(profile, default);
            Assert.False(idle.Success); Assert.Contains("evidence contract", idle.Error, StringComparison.OrdinalIgnoreCase);
        }

        UnityPlayerProfileRunner timeoutRunner = async (_, _, logPath, _, _, _, _, token) =>
        {
            await File.WriteAllTextAsync(logPath, "Profile process remained responsive but did not complete.", token);
            return new UnityPlayerProfileRunResult(true, null);
        };
        using var timeoutExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityPlayerProfileRunner: timeoutRunner);
        var timedOut = await timeoutExecutor.ExecuteAsync(profile, default);
        Assert.False(timedOut.Success); Assert.Contains("bounded runtime", timedOut.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task UnityScenePreview_IsApprovalGatedNonOverwritingAndPngVerified()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var scene = Path.Combine(_root, "Game", "Assets", "Scenes", "Main.unity"); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
        await File.WriteAllTextAsync(scene, "scene yaml");
        var preview = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "capture runtime preview",
            ("project_path", "Game"), ("operation", "capture_scene_preview"), ("scene_path", "Assets/Scenes/Main.unity"),
            ("output_path", "Previews/Main.png"), ("duration_seconds", "5"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        var policyResult = await policy.ValidateAsync(preview, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, policyResult.Decision);
        Assert.Contains("stays local", policyResult.Reason, StringComparison.Ordinal);
        IReadOnlyList<string>? captured = null;
        UnityProcessRunner runner = async (_, arguments, _, timeout, token) =>
        {
            captured = arguments.ToArray(); Assert.InRange(timeout.TotalSeconds, 120, 155);
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            var output = Path.Combine(_root, "Game", root.GetProperty("outputPath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllBytesAsync(output,
                [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 5, 0, 0, 0, 2, 208], token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "capture_scene_preview", success = true, summary = "Preview captured."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(preview, default);

        Assert.True(result.Success, result.Error); Assert.Contains("1280x720", result.Output, StringComparison.Ordinal);
        Assert.Contains("SHA-256", result.Output, StringComparison.Ordinal); Assert.DoesNotContain("-quit", captured!);
        var provenancePath = Path.Combine(_root, "Game", "Previews", "Main.png.forgecore-preview.json");
        Assert.True(File.Exists(provenancePath));
        using (var provenance = JsonDocument.Parse(await File.ReadAllTextAsync(provenancePath)))
        {
            Assert.Equal(1, provenance.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Assets/Scenes/Main.unity", provenance.RootElement.GetProperty("scenePath").GetString());
            Assert.Equal("0.17.15", provenance.RootElement.GetProperty("bridgeVersion").GetString());
            Assert.Equal(1280, provenance.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(720, provenance.RootElement.GetProperty("height").GetInt32());
            Assert.Equal(64, provenance.RootElement.GetProperty("sceneSha256").GetString()!.Length);
            Assert.Equal(64, provenance.RootElement.GetProperty("pngSha256").GetString()!.Length);
        }
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(preview, default)).Decision);
        var retry = await executor.ExecuteAsync(preview, default);
        Assert.False(retry.Success); Assert.Contains("never overwrites previews", retry.Error, StringComparison.Ordinal);
    }

    [Fact] public async Task UnityGameplayVerification_IsTypedApprovalGatedAndUsesPlayModeLifecycle()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "template",
                ("project_path", "Game"), ("template_id", "platformer")), default)).Success);
        }
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var scene = Path.Combine(_root, "Game", "Assets", "Scenes", "Platformer.unity"); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
        await File.WriteAllTextAsync(scene, "scene yaml");
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "verify runtime gameplay",
            ("project_path", "Game"), ("operation", "verify_gameplay"), ("scene_path", "Assets/Scenes/Platformer.unity"), ("duration_seconds", "5"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        var decision = await policy.ValidateAsync(call, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, decision.Decision);
        Assert.Contains("movement, respawn, ordered objectives, and completion", decision.Reason, StringComparison.Ordinal);
        IReadOnlyList<string>? captured = null;
        UnityProcessRunner runner = async (_, arguments, _, timeout, token) =>
        {
            captured = arguments.ToArray(); Assert.InRange(timeout.TotalSeconds, 120, 155);
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("verify_gameplay", root.GetProperty("operation").GetString());
            Assert.Equal("Assets/Scenes/Platformer.unity", root.GetProperty("scenePath").GetString());
            Assert.Equal(5, root.GetProperty("durationSeconds").GetInt32());
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "verify_gameplay", success = true,
                summary = "Deterministic runtime gameplay check passed for platformer: movement=2.50m, respawn=passed, objectives=5/5."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(call, default);

        Assert.True(result.Success, result.Error);
        Assert.Contains("movement=2.50m", result.Output, StringComparison.Ordinal);
        Assert.Contains("Gameplay evidence:", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("-quit", captured!);
        var evidencePath = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "gameplay-evidence"), "*.json").Single();
        using (var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath)))
        {
            Assert.Equal(1, evidence.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Assets/Scenes/Platformer.unity", evidence.RootElement.GetProperty("scenePath").GetString());
            Assert.Equal("platformer", evidence.RootElement.GetProperty("templateId").GetString());
            Assert.Equal("0.17.15", evidence.RootElement.GetProperty("bridgeVersion").GetString());
            Assert.Equal("0.16.33", evidence.RootElement.GetProperty("templateKitVersion").GetString());
            Assert.Equal(2.5, evidence.RootElement.GetProperty("movementMetres").GetDouble());
            Assert.Equal(5, evidence.RootElement.GetProperty("completedObjectives").GetInt32());
            Assert.Equal(64, evidence.RootElement.GetProperty("sceneSha256").GetString()!.Length);
            Assert.Equal(64, evidence.RootElement.GetProperty("templateKitManifestSha256").GetString()!.Length);
        }
        var tooShort = call with { Arguments = new Dictionary<string, string>(call.Arguments) { ["duration_seconds"] = "1" } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(tooShort, default)).Decision);
    }

    [Fact] public async Task UnitySceneProfile_IsTypedApprovalGatedAndWritesHashBoundBudgetEvidence()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "template",
                ("project_path", "Game"), ("template_id", "first_person_action")), default)).Success);
        }
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var scene = Path.Combine(_root, "Game", "Assets", "Scenes", "Campaign-Level02.unity"); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
        await File.WriteAllTextAsync(scene, "scene yaml");
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "profile runtime scene",
            ("project_path", "Game"), ("operation", "profile_scene"), ("scene_path", "Assets/Scenes/Campaign-Level02.unity"), ("duration_seconds", "10"));
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        var decision = await policy.ValidateAsync(call, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, decision.Decision);
        Assert.Contains("not rendered-frame or target-hardware certification", decision.Reason, StringComparison.Ordinal);
        IReadOnlyList<string>? captured = null;
        UnityProcessRunner passingRunner = async (_, arguments, _, timeout, token) =>
        {
            captured = arguments.ToArray(); Assert.InRange(timeout.TotalSeconds, 120, 160);
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("profile_scene", root.GetProperty("operation").GetString());
            Assert.Equal("Assets/Scenes/Campaign-Level02.unity", root.GetProperty("scenePath").GetString());
            Assert.Equal(10, root.GetProperty("durationSeconds").GetInt32());
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "profile_scene", success = true,
                summary = "Repeatable editor baseline.",
                performance = new
                {
                    environment = "editor_play_mode", sampledUpdates = 540, averageUpdateMilliseconds = 15.2,
                    p95UpdateMilliseconds = 22.4, maximumUpdateMilliseconds = 31.8, maximumDrawCalls = 740,
                    maximumSetPassCalls = 320, maximumTriangles = 1800000, maximumVertices = 2400000,
                    peakAllocatedMemoryBytes = 1200000000L
                }
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: passingRunner);

        var result = await executor.ExecuteAsync(call, default);

        Assert.True(result.Success, result.Error);
        Assert.Contains("Performance evidence:", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("-quit", captured!);
        var evidenceDirectory = Path.Combine(_root, "Game", ".forgecore", "unity", "performance-evidence");
        var passedEvidencePath = Directory.EnumerateFiles(evidenceDirectory, "*.json").Single();
        using (var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(passedEvidencePath)))
        {
            Assert.Equal(2, evidence.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Assets/Scenes/Campaign-Level02.unity", evidence.RootElement.GetProperty("scenePath").GetString());
            Assert.Equal("editor_play_mode", evidence.RootElement.GetProperty("environment").GetString());
            Assert.Equal(22.4, evidence.RootElement.GetProperty("p95UpdateMilliseconds").GetDouble());
            Assert.True(evidence.RootElement.GetProperty("renderMetricsAvailable").GetBoolean());
            Assert.True(evidence.RootElement.GetProperty("renderBudgetPassed").GetBoolean());
            Assert.True(evidence.RootElement.GetProperty("editorBaselineBudgetPassed").GetBoolean());
            Assert.Contains("target hardware", evidence.RootElement.GetProperty("qualification").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(64, evidence.RootElement.GetProperty("sceneSha256").GetString()!.Length);
            Assert.Equal(64, evidence.RootElement.GetProperty("templateKitManifestSha256").GetString()!.Length);
        }

        UnityProcessRunner failingRunner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "profile_scene", success = true,
                summary = "Repeatable editor baseline.",
                performance = new
                {
                    environment = "editor_play_mode", sampledUpdates = 500, averageUpdateMilliseconds = 20.0,
                    p95UpdateMilliseconds = 45.0, maximumUpdateMilliseconds = 60.0, maximumDrawCalls = 740,
                    maximumSetPassCalls = 320, maximumTriangles = 1800000, maximumVertices = 2400000,
                    peakAllocatedMemoryBytes = 1200000000L
                }
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var failingExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: failingRunner);
        var failed = await failingExecutor.ExecuteAsync(call with { Id = Guid.NewGuid() }, default);
        Assert.False(failed.Success); Assert.Contains("editor performance budget failed", failed.Error, StringComparison.Ordinal);
        var evidencePaths = Directory.EnumerateFiles(evidenceDirectory, "*.json").ToArray(); Assert.Equal(2, evidencePaths.Length);
        Assert.Contains(evidencePaths, path =>
        {
            using var evidence = JsonDocument.Parse(File.ReadAllText(path));
            return !evidence.RootElement.GetProperty("editorBaselineBudgetPassed").GetBoolean();
        });
        var tooShort = call with { Arguments = new Dictionary<string, string>(call.Arguments) { ["duration_seconds"] = "4" } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(tooShort, default)).Decision);
    }

    [Fact] public async Task UnityGameplayEvidenceCatalog_DistinguishesCurrentStaleAndInvalidRecords()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var scene = Path.Combine(_root, "Game", "Assets", "Scenes", "Platformer.unity"); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
        await File.WriteAllTextAsync(scene, "scene-v1");
        var manifest = Path.Combine(_root, "Game", "Assets", "ForgeCoreGameTemplates", "forgecore-game-templates.manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!); await File.WriteAllTextAsync(manifest, "manifest-v1");
        var directory = Path.Combine(_root, "Game", ".forgecore", "unity", "gameplay-evidence"); Directory.CreateDirectory(directory);
        static async Task<string> HashAsync(string path) => Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant();
        await File.WriteAllTextAsync(Path.Combine(directory, "valid.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, scenePath = "Assets/Scenes/Platformer.unity", editorVersion = "6000.0.50f1",
            bridgeVersion = "0.17.15", templateKitVersion = "0.16.33", sceneSha256 = await HashAsync(scene),
            templateKitManifestSha256 = await HashAsync(manifest), durationSeconds = 5, templateId = "platformer",
            movementMetres = 2.5, completedObjectives = 5, requiredObjectives = 5,
            summary = "Deterministic runtime gameplay check passed for platformer: movement=2.50m, respawn=passed, objectives=5/5.",
            createdUtc = DateTimeOffset.Parse("2026-07-31T12:00:00Z")
        }));
        await File.WriteAllTextAsync(Path.Combine(directory, "superseded.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, scenePath = "Assets/Scenes/Platformer.unity", editorVersion = "6000.0.50f1",
            bridgeVersion = "0.17.15", templateKitVersion = "0.16.12", sceneSha256 = new string('0', 64),
            templateKitManifestSha256 = new string('0', 64), durationSeconds = 5, templateId = "platformer",
            movementMetres = 2.5, completedObjectives = 5, requiredObjectives = 5,
            summary = "Deterministic runtime gameplay check passed for platformer: movement=2.50m, respawn=passed, objectives=5/5.",
            createdUtc = DateTimeOffset.Parse("2026-07-31T11:00:00Z")
        }));
        var catalog = new UnityGameplayEvidenceCatalog(guard, "Game");

        var current = await catalog.ReadAsync(default);

        Assert.Equal(new UnityGameplayEvidenceSnapshot(1, 1, 0, 0, DateTimeOffset.Parse("2026-07-31T12:00:00Z")), current);
        await File.WriteAllTextAsync(scene, "scene-v2");
        await File.WriteAllTextAsync(Path.Combine(directory, "malformed.json"), "{not-json");
        var changed = await catalog.ReadAsync(default);
        Assert.Equal(2, changed.TotalRecords); Assert.Equal(0, changed.CurrentRecords);
        Assert.Equal(1, changed.StaleRecords); Assert.Equal(1, changed.InvalidRecords);
    }

    [Fact] public async Task GeneratedAssetPreparation_IsHashBoundBackedUpAndTrustedProjectEligible()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var scene = Path.Combine(_root, "Game", "Assets", "Scenes", "FrontierValley.unity"); Directory.CreateDirectory(Path.GetDirectoryName(scene)!);
        await File.WriteAllTextAsync(scene, "original frontier scene");
        var asset = Path.Combine(_root, "Game", "Assets", "ForgeCore", "Generated", "frontier-watchtower.glb"); Directory.CreateDirectory(Path.GetDirectoryName(asset)!);
        await File.WriteAllBytesAsync(asset, MinimalGlb());
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(asset))).ToLowerInvariant();
        await File.WriteAllTextAsync(asset + ".asset.json", JsonSerializer.Serialize(new
        {
            file = "frontier-watchtower.glb", destinationPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb",
            sourcePage = "https://www.meshy.ai/", downloadUrl = "https://assets.meshy.example/model.glb",
            license = "MESHY_GENERATED_REVIEW_REQUIRED", sha256 = hash, bytes = new FileInfo(asset).Length,
            format = "glTF 2.0 binary model", inspection = "bounded", stagedUtc = DateTimeOffset.UtcNow.AddMinutes(-1), importedUtc = DateTimeOffset.UtcNow
        }));
        var call = PrepareGeneratedAssetCall(hash);
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(call, default)).Decision);
        var trusted = new TaskAuthorizationSession(); trusted.Activate("Game", TimeSpan.FromMinutes(30), 2);
        Assert.True(trusted.TryConsume(new ApprovalRequest(call, "prepare exact asset")));

        var invoked = false;
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            invoked = true; var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("prepare_generated_asset", root.GetProperty("operation").GetString());
            Assert.Equal(hash, root.GetProperty("assetSha256").GetString()); Assert.Equal("mesh", root.GetProperty("colliderMode").GetString());
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "prepare_generated_asset", success = true, summary = "Prepared generated asset."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024 * 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);
        var result = await executor.ExecuteAsync(call, default);
        Assert.True(result.Success, result.Error); Assert.True(invoked);
        var backup = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "backups"), "FrontierValley.unity.bak", SearchOption.AllDirectories).Single();
        Assert.Equal("original frontier scene", await File.ReadAllTextAsync(backup));

        var tampered = call with { Arguments = new Dictionary<string, string>(call.Arguments) { ["expected_sha256"] = new string('f', 64) } };
        invoked = false; var denied = await executor.ExecuteAsync(tampered, default);
        Assert.False(denied.Success); Assert.False(invoked); Assert.Contains("provenance", denied.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task GameArtInspection_IsTypedHashBoundAndPersistsOnlyUnreviewedStructuralEvidence()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var (asset, hash, _) = await CreateImportedGeneratedArtAssetAsync();
        var call = InspectGameArtCall(hash);
        var spec = UnityEditorActionSpec.From(call);
        Assert.Equal("frontier-watchtower", spec.AssetSetId); Assert.Equal("environment_props", spec.AssetRole);
        Assert.Equal("realistic_pbr", spec.StyleFamily);
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        var decision = await policy.ValidateAsync(call, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, decision.Decision);
        Assert.Contains("rights/visual review remain incomplete", decision.Reason, StringComparison.OrdinalIgnoreCase);
        var unknownRole = call with { Arguments = new Dictionary<string, string>(call.Arguments) { ["asset_role"] = "arbitrary_role" } };
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(unknownRole));

        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("inspect_game_art_asset", root.GetProperty("operation").GetString());
            Assert.Equal(hash, root.GetProperty("assetSha256").GetString());
            Assert.Equal("frontier-watchtower", root.GetProperty("assetSetId").GetString());
            Assert.Equal("environment_props", root.GetProperty("assetRole").GetString());
            Assert.Equal("realistic_pbr", root.GetProperty("styleFamily").GetString());
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "inspect_game_art_asset", success = true,
                summary = "Inspected one imported game-art asset without saving project content.",
                artInspection = new
                {
                    assetPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb", assetSha256 = hash,
                    assetSetId = "frontier-watchtower", assetRole = "environment_props", styleFamily = "realistic_pbr",
                    rendererCount = 2, meshCount = 2, skinnedMeshRendererCount = 0, animatorCount = 0, avatarCount = 0,
                    materialCount = 2, pbrMaterialCount = 2, textureCount = 4, texturePixelCount = 4_194_304,
                    estimatedTextureMemoryBytes = 16_777_216, maximumTextureWidth = 1024, maximumTextureHeight = 1024,
                    missingMaterialCount = 0, missingShaderCount = 0,
                    vertexCount = 2400, triangleCount = 1200, animationClipCount = 0, lodGroupCount = 1, lodLevelCount = 2,
                    colliderCount = 1, boundsWidth = 5.0, boundsHeight = 16.0, boundsDepth = 5.0,
                    pivotBottomOffsetNormalized = 0.01, pivotHorizontalOffsetNormalized = 0.02,
                    rootScaleX = 1.0, rootScaleY = 1.0, rootScaleZ = 1.0, rootRotationDegrees = 0.0,
                    hasPbrMaterials = true, isRigged = false, hasLods = true, hasCollision = true,
                    hasGroundedPivot = true, hasUniformRootScale = true, hasCanonicalRootRotation = true
                }
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024 * 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(call, default);

        Assert.True(result.Success, result.Error); Assert.Contains("Game-art evidence", result.Output, StringComparison.Ordinal);
        var evidencePath = Directory.EnumerateFiles(Path.Combine(_root, "Game", ".forgecore", "unity", "art-evidence"), "*.json").Single();
        using (var evidence = JsonDocument.Parse(await File.ReadAllBytesAsync(evidencePath)))
        {
            var root = evidence.RootElement;
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(hash, root.GetProperty("assetSha256").GetString());
            Assert.Equal(await HashAsync(asset + ".asset.json"), root.GetProperty("provenanceSha256").GetString());
            Assert.Equal(await HashAsync(asset + ".meta"), root.GetProperty("metaSha256").GetString());
            Assert.True(root.GetProperty("structuralReady").GetBoolean());
            Assert.False(root.GetProperty("rightsReviewed").GetBoolean());
            Assert.False(root.GetProperty("visualReviewed").GetBoolean());
        }
        var plan = GameBlueprint.FromPrompt("Create a realistic FPS game with weapons and enemies.").ArtSet!;
        var catalog = new UnityGameArtEvidenceCatalog(guard, "Game");
        var snapshot = await catalog.ReadAsync(plan, default);
        Assert.Equal(1, snapshot.TotalRecords); Assert.Equal(1, snapshot.CurrentRecords);
        Assert.False(snapshot.Assessment.Ready); Assert.Single(snapshot.CurrentEvidence);
        Assert.True(snapshot.CurrentEvidence[0].PerformanceMeasured);
        Assert.Equal(2400, snapshot.CurrentEvidence[0].VertexCount);
        Assert.Equal(1200, snapshot.CurrentEvidence[0].TriangleCount);
        Assert.Equal(2, snapshot.CurrentEvidence[0].MaterialCount);
        Assert.Equal(4, snapshot.CurrentEvidence[0].TextureCount);
        Assert.Equal(4_194_304, snapshot.CurrentEvidence[0].TexturePixelCount);
        Assert.Equal(16_777_216, snapshot.CurrentEvidence[0].EstimatedTextureMemoryBytes);
        Assert.True(snapshot.CurrentEvidence[0].ConventionsMeasured);
        Assert.True(snapshot.CurrentEvidence[0].HasGroundedPivot);
        Assert.True(snapshot.CurrentEvidence[0].HasUniformRootScale);
        Assert.True(snapshot.CurrentEvidence[0].HasCanonicalRootRotation);
        Assert.Contains(snapshot.Assessment.Issues, x => x.Contains("rights review", StringComparison.OrdinalIgnoreCase));
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(evidencePath)!, "legacy-schema-1.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, assetSetId = "frontier-watchtower", assetPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb",
            assetSha256 = hash, provenanceSha256 = await HashAsync(asset + ".asset.json"), metaSha256 = await HashAsync(asset + ".meta"),
            assetRole = "environment_props", styleFamily = "realistic_pbr", editorVersion = "6000.0.50f1", bridgeVersion = "0.17.10",
            assetCount = 1, rendererCount = 2, meshCount = 2, skinnedMeshRendererCount = 0, animatorCount = 0, avatarCount = 0,
            materialCount = 2, pbrMaterialCount = 2, textureCount = 4, missingMaterialCount = 0, missingShaderCount = 0,
            vertexCount = 2400, triangleCount = 1200, animationClipCount = 0, lodGroupCount = 1, lodLevelCount = 2,
            colliderCount = 1, boundsWidth = 5.0, boundsHeight = 16.0, boundsDepth = 5.0,
            hasPbrMaterials = true, isRigged = false, hasLods = true, hasCollision = true, structuralReady = true,
            rightsReviewed = false, visualReviewed = false, createdUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        var withLegacy = await catalog.ReadAsync(plan, default);
        Assert.Equal(1, withLegacy.TotalRecords); Assert.Equal(1, withLegacy.CurrentRecords); Assert.Equal(0, withLegacy.InvalidRecords);
        await File.AppendAllTextAsync(asset + ".meta", "# changed");
        var stale = await catalog.ReadAsync(plan, default);
        Assert.Equal(0, stale.CurrentRecords); Assert.Equal(1, stale.StaleRecords);
    }

    [Fact] public async Task GameArtInspection_RestoresChangedUnityMetadataAndRejectsEvidence()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var (asset, hash, originalMeta) = await CreateImportedGeneratedArtAssetAsync();
        UnityProcessRunner runner = async (_, arguments, _, _, token) =>
        {
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            await File.WriteAllTextAsync(asset + ".meta", "mutated by fake Unity", token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "inspect_game_art_asset", success = true,
                summary = "This result must be rejected."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024 * 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(InspectGameArtCall(hash), default);

        Assert.False(result.Success); Assert.Contains("restored", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(originalMeta, await File.ReadAllTextAsync(asset + ".meta"));
        var evidenceDirectory = Path.Combine(_root, "Game", ".forgecore", "unity", "art-evidence");
        Assert.False(Directory.Exists(evidenceDirectory) && Directory.EnumerateFiles(evidenceDirectory, "*.json").Any());
    }

    [Fact] public async Task GameArtPreview_IsHashBoundNonOverwritingAndCannotGrantReview()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        var (asset, hash, _) = await CreateImportedGeneratedArtAssetAsync();
        var call = CaptureGameArtPreviewCall(hash);
        var policy = new SecurityPolicy(guard, [], unityEditorPath: unity, unityEditorVersion: "6000.0.50f1");
        var decision = await policy.ValidateAsync(call, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, decision.Decision);
        Assert.Contains("four-view preview", decision.Reason, StringComparison.OrdinalIgnoreCase);
        UnityProcessRunner runner = async (_, arguments, projectRoot, _, token) =>
        {
            Assert.Contains("-quit", arguments);
            var requestPath = ValueAfter(arguments, "-forgecoreRequest"); var resultPath = ValueAfter(arguments, "-forgecoreResult");
            using var request = JsonDocument.Parse(await File.ReadAllBytesAsync(requestPath, token)); var root = request.RootElement;
            Assert.Equal("capture_game_art_preview", root.GetProperty("operation").GetString());
            Assert.Equal(hash, root.GetProperty("assetSha256").GetString());
            Assert.Equal("frontier-watchtower", root.GetProperty("assetSetId").GetString());
            var output = Path.Combine(projectRoot, "Previews", "GameArt", "frontier-watchtower.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); await File.WriteAllBytesAsync(output, MinimalPng(1280, 720), token);
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(new
            {
                protocolVersion = "1.0", bridgeVersion = "0.17.15", editorVersion = "6000.0.50f1",
                requestId = root.GetProperty("requestId").GetString(), operation = "capture_game_art_preview", success = true,
                summary = "Captured non-overwriting four-view game-art preview; the preview remains unreviewed."
            }), token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024 * 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: runner);

        var result = await executor.ExecuteAsync(call, default);

        Assert.True(result.Success, result.Error); Assert.Contains("Game-art preview evidence", result.Output, StringComparison.Ordinal);
        var preview = Path.Combine(_root, "Game", "Previews", "GameArt", "frontier-watchtower.png");
        var provenancePath = preview + ".forgecore-art-preview.json";
        using var provenance = JsonDocument.Parse(await File.ReadAllBytesAsync(provenancePath)); var root = provenance.RootElement;
        Assert.Equal("frontier-watchtower", root.GetProperty("assetSetId").GetString());
        Assert.Equal(hash, root.GetProperty("assetSha256").GetString());
        Assert.Equal(await HashAsync(asset + ".asset.json"), root.GetProperty("provenanceSha256").GetString());
        Assert.Equal(await HashAsync(asset + ".meta"), root.GetProperty("metaSha256").GetString());
        Assert.Equal(await HashAsync(preview), root.GetProperty("previewSha256").GetString());
        Assert.Equal(4, root.GetProperty("viewCount").GetInt32()); Assert.False(root.GetProperty("rightsReviewed").GetBoolean());
        Assert.False(root.GetProperty("visualReviewed").GetBoolean());
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(call, default)).Decision);
        var unsafeOutput = call with { Arguments = new Dictionary<string, string>(call.Arguments) { ["output_path"] = "Builds/not-a-preview.png" } };
        Assert.Throws<ArgumentException>(() => UnityEditorActionSpec.From(unsafeOutput));
    }

    [Fact] public async Task GameArtReview_IsLiveUserBoundSeparateRejectableHashVerifiedAndNonOverwriting()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var (asset, assetHash, _) = await CreateImportedGeneratedArtAssetAsync();
        var sourceProvenanceHash = await HashAsync(asset + ".asset.json"); var metaHash = await HashAsync(asset + ".meta");
        var createdUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        var evidenceDirectory = Path.Combine(_root, "Game", ".forgecore", "unity", "art-evidence");
        Directory.CreateDirectory(evidenceDirectory);
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "structural.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 2, assetSetId = "frontier-watchtower", assetPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb",
            assetSha256 = assetHash, provenanceSha256 = sourceProvenanceHash, metaSha256 = metaHash,
            assetRole = "environment_props", styleFamily = "realistic_pbr", editorVersion = "6000.0.50f1", bridgeVersion = "0.17.15",
            assetCount = 1, rendererCount = 2, meshCount = 2, skinnedMeshRendererCount = 0, animatorCount = 0, avatarCount = 0,
            materialCount = 2, pbrMaterialCount = 2, textureCount = 4, texturePixelCount = 4_194_304,
            estimatedTextureMemoryBytes = 16_777_216, maximumTextureWidth = 1024, maximumTextureHeight = 1024,
            missingMaterialCount = 0, missingShaderCount = 0, vertexCount = 2400, triangleCount = 1200,
            animationClipCount = 0, lodGroupCount = 1, lodLevelCount = 2, colliderCount = 1,
            boundsWidth = 5.0, boundsHeight = 16.0, boundsDepth = 5.0,
            pivotBottomOffsetNormalized = 0.01, pivotHorizontalOffsetNormalized = 0.02,
            rootScaleX = 1.0, rootScaleY = 1.0, rootScaleZ = 1.0, rootRotationDegrees = 0.0,
            hasPbrMaterials = true, isRigged = false, hasLods = true, hasCollision = true,
            hasGroundedPivot = true, hasUniformRootScale = true, hasCanonicalRootRotation = true,
            structuralReady = true, rightsReviewed = false, visualReviewed = false, createdUtc
        }));
        var preview = Path.Combine(_root, "Game", "Previews", "GameArt", "frontier-watchtower.png");
        Directory.CreateDirectory(Path.GetDirectoryName(preview)!); var previewBytes = MinimalPng(1280, 720);
        await File.WriteAllBytesAsync(preview, previewBytes); var previewHash = await HashAsync(preview);
        var previewProvenance = preview + ".forgecore-art-preview.json";
        await File.WriteAllTextAsync(previewProvenance, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, assetSetId = "frontier-watchtower", assetPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb",
            assetSha256 = assetHash, provenanceSha256 = sourceProvenanceHash, metaSha256 = metaHash,
            previewPath = "Previews/GameArt/frontier-watchtower.png", previewSha256 = previewHash,
            editorVersion = "6000.0.50f1", bridgeVersion = "0.17.15",
            width = 1280, height = 720, viewCount = 4, rightsReviewed = false, visualReviewed = false,
            createdUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        var previewProvenanceHash = await HashAsync(previewProvenance);
        var visualProposal = ReviewGameArtCall(GameArtReviewSpec.Visual, assetHash, previewHash, previewProvenanceHash);
        var rightsProposal = ReviewGameArtCall(GameArtReviewSpec.Rights, assetHash, previewHash, previewProvenanceHash);
        _ = GameArtReviewSpec.FromProposal(visualProposal);
        Assert.Throws<ArgumentException>(() => GameArtReviewSpec.FromExecution(visualProposal));
        var policy = new SecurityPolicy(guard, []);
        var policyResult = await policy.ValidateAsync(visualProposal, default);
        Assert.Equal(SecurityDecision.ApprovalRequired, policyResult.Decision);
        Assert.Contains("HUMAN ATTESTATION ONLY", policyResult.Reason, StringComparison.Ordinal);
        var forgedDecision = visualProposal with { Arguments = new Dictionary<string, string>(visualProposal.Arguments)
        {
            ["review_decision"] = "approved", ["attestation_source"] = GameArtReviewSpec.UserLiveApproval
        } };
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(forgedDecision, default)).Decision);
        var trusted = new TaskAuthorizationSession(); trusted.Activate("Game", TimeSpan.FromHours(1), 10);
        Assert.False(trusted.TryConsume(new ApprovalRequest(visualProposal, "human review cannot be pre-authorized")));

        using var executor = new ToolExecutor(guard, [], unityEditorVersion: "6000.0.50f1", allowedAssetHosts: [], maxAssetBytes: 1024 * 1024);
        await File.AppendAllTextAsync(preview, "tampered after approval request");
        var tampered = await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(visualProposal, ApprovalDecision.Approved), default);
        Assert.False(tampered.Success); Assert.Contains("changed", tampered.Error, StringComparison.OrdinalIgnoreCase);
        await File.WriteAllBytesAsync(preview, previewBytes);

        var approvedVisualCall = GameArtReviewSpec.BindUserDecision(visualProposal, ApprovalDecision.Approved);
        var approvedVisual = await executor.ExecuteAsync(approvedVisualCall, default);
        Assert.True(approvedVisual.Success, approvedVisual.Error); Assert.Contains("decision=approved", approvedVisual.Output, StringComparison.Ordinal);
        var replay = await executor.ExecuteAsync(approvedVisualCall, default);
        Assert.False(replay.Success); Assert.Contains("not overwritten", replay.Error, StringComparison.OrdinalIgnoreCase);
        var rejectedRights = await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(rightsProposal, ApprovalDecision.Rejected), default);
        Assert.True(rejectedRights.Success, rejectedRights.Error); Assert.Contains("decision=rejected", rejectedRights.Output, StringComparison.Ordinal);

        var plan = GameBlueprint.FromPrompt("Create a realistic FPS game with weapons and enemies.").ArtSet!;
        var catalog = new UnityGameArtEvidenceCatalog(guard, "Game");
        var rejectedSnapshot = await catalog.ReadAsync(plan, default);
        Assert.Equal(2, rejectedSnapshot.TotalReviewRecords); Assert.Equal(2, rejectedSnapshot.CurrentReviewRecords);
        Assert.True(rejectedSnapshot.CurrentEvidence.Single().VisualReviewed);
        Assert.False(rejectedSnapshot.CurrentEvidence.Single().RightsReviewed);

        var storeCall = ToolCall.Create(ToolKind.StoreReviewedAsset, PermissionKind.Write, "archive exact reviewed asset",
            ("project_path", "Game"), ("asset_set_id", "frontier-watchtower"), ("expected_asset_sha256", assetHash),
            ("tags", "watchtower,frontier,defensive_structure"));
        var prematureStore = await executor.ExecuteAsync(storeCall, default);
        Assert.False(prematureStore.Success); Assert.Contains("current structural, visual, rights", prematureStore.Error, StringComparison.OrdinalIgnoreCase);

        await Task.Delay(2);
        var secondRightsProposal = ReviewGameArtCall(GameArtReviewSpec.Rights, assetHash, previewHash, previewProvenanceHash);
        var approvedRights = await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(secondRightsProposal, ApprovalDecision.Approved), default);
        Assert.True(approvedRights.Success, approvedRights.Error);
        var approvedSnapshot = await catalog.ReadAsync(plan, default);
        Assert.Equal(2, approvedSnapshot.CurrentReviewRecords);
        Assert.True(approvedSnapshot.CurrentEvidence.Single().VisualReviewed);
        Assert.True(approvedSnapshot.CurrentEvidence.Single().RightsReviewed);

        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(storeCall, default)).Decision);
        var stored = await executor.ExecuteAsync(storeCall, default);
        Assert.True(stored.Success, stored.Error); Assert.Contains("immutable local vault", stored.Output, StringComparison.Ordinal);
        var storedAgain = await executor.ExecuteAsync(storeCall with { Id = Guid.NewGuid() }, default);
        Assert.True(storedAgain.Success, storedAgain.Error); Assert.Contains("already_present=true", storedAgain.Output, StringComparison.Ordinal);

        var searchCall = ToolCall.Create(ToolKind.SearchAssetVault, PermissionKind.Read, "find reusable watchtower",
            ("query", "frontier watchtower"), ("asset_role", "environment_props"), ("style_family", "realistic_pbr"), ("max_results", "5"));
        Assert.Equal(SecurityDecision.Allowed, (await policy.ValidateAsync(searchCall, default)).Decision);
        var search = await executor.ExecuteAsync(searchCall, default);
        Assert.True(search.Success, search.Error);
        using var searchJson = JsonDocument.Parse(search.Output); var resultEntry = searchJson.RootElement.GetProperty("results").EnumerateArray().Single();
        var bundleHash = resultEntry.GetProperty("bundleSha256").GetString()!;
        Assert.Equal(assetHash, resultEntry.GetProperty("assetSha256").GetString());

        Directory.CreateDirectory(Path.Combine(_root, "Target", "Assets"));
        Directory.CreateDirectory(Path.Combine(_root, "Target", "ProjectSettings"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Target", "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.50f1\n");
        var importCall = ToolCall.Create(ToolKind.ImportVaultAsset, PermissionKind.Write, "import exact reusable watchtower",
            ("project_path", "Target"), ("bundle_sha256", bundleHash), ("expected_asset_sha256", assetHash),
            ("destination_directory", "Assets/ForgeCore/Generated/Vault/frontier-watchtower"));
        Assert.Equal(SecurityDecision.ApprovalRequired, (await policy.ValidateAsync(importCall, default)).Decision);
        var imported = await executor.ExecuteAsync(importCall, default);
        Assert.True(imported.Success, imported.Error); Assert.Contains("Fresh target-project Unity inspection is required", imported.Output, StringComparison.Ordinal);
        var importedAsset = Path.Combine(_root, "Target", "Assets", "ForgeCore", "Generated", "Vault", "frontier-watchtower", "frontier-watchtower.glb");
        Assert.Equal(assetHash, await HashAsync(importedAsset)); Assert.True(File.Exists(importedAsset + ".asset.json"));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(importedAsset)!, "forgecore-vault-import.json")));
        Assert.Equal(SecurityDecision.Denied, (await policy.ValidateAsync(importCall with { RequiredPermission = PermissionKind.Read }, default)).Decision);
        var replayImport = await executor.ExecuteAsync(importCall with { Id = Guid.NewGuid() }, default);
        Assert.False(replayImport.Success); Assert.Contains("already exists", replayImport.Error, StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(Path.Combine(_root, "Target2", "Assets"));
        Directory.CreateDirectory(Path.Combine(_root, "Target2", "ProjectSettings"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Target2", "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.50f1\n");
        var vaultAsset = Path.Combine(_root, ".forgecore", "asset-vault", "objects", bundleHash, "asset.glb");
        await File.AppendAllTextAsync(vaultAsset, "tampered immutable object");
        var tamperedImportCall = importCall with { Id = Guid.NewGuid(), Arguments = new Dictionary<string, string>(importCall.Arguments)
        {
            ["project_path"] = "Target2", ["destination_directory"] = "Assets/ForgeCore/Generated/Vault/frontier-watchtower"
        } };
        var tamperedImport = await executor.ExecuteAsync(tamperedImportCall, default);
        Assert.False(tamperedImport.Success); Assert.Contains("changed", tamperedImport.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(_root, "Target2", "Assets", "ForgeCore", "Generated", "Vault", "frontier-watchtower")));

        var reviewDirectory = Path.Combine(_root, "Game", ".forgecore", "unity", "art-reviews");
        await File.WriteAllTextAsync(Path.Combine(reviewDirectory, "malformed.json"), "{}");
        var malformed = await catalog.ReadAsync(plan, default);
        Assert.Equal(1, malformed.InvalidReviewRecords);
        await File.AppendAllTextAsync(preview, "stale");
        var stale = await catalog.ReadAsync(plan, default);
        Assert.Equal(0, stale.CurrentReviewRecords); Assert.Equal(2, stale.StaleReviewRecords);
        Assert.False(stale.CurrentEvidence.Single().VisualReviewed); Assert.False(stale.CurrentEvidence.Single().RightsReviewed);
    }

    [Fact] public async Task UnityTestsAreParsedAndEditorActionCancellationPropagates()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        UnityProcessRunner passing = async (_, arguments, _, _, token) =>
        {
            await File.WriteAllTextAsync(ValueAfter(arguments, "-testResults"), "<test-run total=\"3\" passed=\"3\" failed=\"0\" skipped=\"0\" inconclusive=\"0\" />", token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using (var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: passing))
        {
            var tests = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "tests",
                ("project_path", "Game"), ("operation", "run_tests"), ("test_mode", "edit"));
            var result = await executor.ExecuteAsync(tests, default);
            Assert.True(result.Success, result.Error); Assert.Contains("total=3, passed=3", result.Output, StringComparison.Ordinal);
        }

        UnityProcessRunner failing = async (_, arguments, _, _, token) =>
        {
            await File.WriteAllTextAsync(ValueAfter(arguments, "-testResults"),
                "<test-run total=\"1\" passed=\"0\" failed=\"1\" skipped=\"0\" inconclusive=\"0\"><test-suite><test-case name=\"Runtime input\" fullname=\"ForgeCore.Tests.RuntimeInput\" result=\"Failed\"><failure><message>Player moved while paused.</message><stack-trace>at RuntimeInput.cs:42</stack-trace></failure></test-case></test-suite></test-run>", token);
            return new UnityProcessResult(2, string.Empty, string.Empty);
        };
        using (var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: failing))
        {
            var tests = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "tests",
                ("project_path", "Game"), ("operation", "run_tests"), ("test_mode", "play"));
            var result = await executor.ExecuteAsync(tests, default);
            Assert.False(result.Success); Assert.Contains("ForgeCore.Tests.RuntimeInput", result.Error, StringComparison.Ordinal);
            Assert.Contains("Player moved while paused", result.Error, StringComparison.Ordinal);
            Assert.Contains("RuntimeInput.cs:42", result.Error, StringComparison.Ordinal);
        }

        UnityProcessRunner malformedXml = async (_, arguments, _, _, token) =>
        {
            await File.WriteAllTextAsync(ValueAfter(arguments, "-testResults"), "<!DOCTYPE test-run [<!ENTITY xxe SYSTEM 'file:///windows/win.ini'>]><test-run total=\"0\" />", token);
            return new UnityProcessResult(0, "", "");
        };
        using (var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: malformedXml))
        {
            var tests = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "tests",
                ("project_path", "Game"), ("operation", "run_tests"), ("test_mode", "edit"));
            var result = await executor.ExecuteAsync(tests, default);
            Assert.False(result.Success); Assert.Contains("DTD", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        UnityProcessRunner blocking = async (_, _, _, _, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new UnityProcessResult(0, "", ""); };
        using var cancelledExecutor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity, unityEditorVersion: "6000.0.50f1", unityProcessRunner: blocking);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledExecutor.ExecuteAsync(UnityCall("compile"), source.Token));
    }

    [Fact] public async Task UnityTests_UseAndProtectAlreadyLockedPackagesWhenEnabled()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        using (var installer = new ToolExecutor(guard, []))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "install", ("project_path", "Game")), default)).Success);
        var packages = Path.Combine(_root, "Game", "Packages"); Directory.CreateDirectory(packages);
        var manifest = Path.Combine(packages, "manifest.json"); var locked = Path.Combine(packages, "packages-lock.json");
        await File.WriteAllTextAsync(manifest, "{\"dependencies\":{}}"); await File.WriteAllTextAsync(locked, "{\"dependencies\":{}}");
        Directory.CreateDirectory(Path.Combine(_root, "Game", "Library", "PackageCache"));
        var unity = Path.Combine(_root, "Unity.exe"); await File.WriteAllBytesAsync(unity, [0]);
        UnityProcessRunner passing = async (_, arguments, _, _, token) =>
        {
            Assert.DoesNotContain("-noUpm", arguments);
            await File.WriteAllTextAsync(ValueAfter(arguments, "-testResults"),
                "<test-run total=\"2\" passed=\"2\" failed=\"0\" skipped=\"0\" inconclusive=\"0\" />", token);
            return new UnityProcessResult(0, string.Empty, string.Empty);
        };
        using var executor = new ToolExecutor(guard, [], [], 1024, unityEditorPath: unity,
            unityEditorVersion: "6000.0.50f1", unityProcessRunner: passing, enableUnityProjectPackages: true);
        var tests = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "tests",
            ("project_path", "Game"), ("operation", "run_tests"), ("test_mode", "edit"));

        var result = await executor.ExecuteAsync(tests, default);

        Assert.True(result.Success, result.Error); Assert.Contains("total=2, passed=2", result.Output, StringComparison.Ordinal);
        Assert.Equal("{\"dependencies\":{}}", await File.ReadAllTextAsync(manifest));
        Assert.Equal("{\"dependencies\":{}}", await File.ReadAllTextAsync(locked));
    }

    [Fact] public async Task UnityMcpPolicy_IsDisabledByDefaultAndRequiresPackageRelayApprovalAndExactAllowList()
    {
        await CreateBareUnityProjectAsync(); var guard = new WorkspacePathGuard(_root);
        var relay = Path.Combine(_root, "relay_win.exe"); await File.WriteAllBytesAsync(relay, [0]);
        var discover = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, "discover Unity MCP tools",
            ("project_path", "Game"), ("operation", "discover"));

        var disabled = new SecurityPolicy(guard, [], unityEditorVersion: "6000.0.50f1",
            enableUnityMcp: false, unityMcpRelayPath: relay);
        Assert.Equal(SecurityDecision.Denied, (await disabled.ValidateAsync(discover, default)).Decision);

        var enabled = new SecurityPolicy(guard, [], unityEditorVersion: "6000.0.50f1",
            enableUnityMcp: true, unityMcpRelayPath: relay, allowedUnityMcpTools: ["scene_polish"]);
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(discover, default)).Decision);

        Directory.CreateDirectory(Path.Combine(_root, "Game", "Packages"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Game", "Packages", "manifest.json"),
            "{\"dependencies\":{\"com.unity.ai.assistant\":\"2.17.0\"}}");
        Assert.Equal(SecurityDecision.ApprovalRequired, (await enabled.ValidateAsync(discover, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(discover with { RequiredPermission = PermissionKind.Read }, default)).Decision);

        var blocked = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, "unlisted tool",
            ("project_path", "Game"), ("operation", "call"), ("tool_name", "delete_everything"), ("arguments_json", "{}"));
        var allowed = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, "approved polish",
            ("project_path", "Game"), ("operation", "call"), ("tool_name", "scene_polish"), ("arguments_json", "{\"strength\":0.4}"));
        var traversal = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, "escaping path",
            ("project_path", "Game"), ("operation", "call"), ("tool_name", "scene_polish"), ("arguments_json", "{\"asset_path\":\"../Outside.asset\"}"));
        var secret = ToolCall.Create(ToolKind.UnityMcpAction, PermissionKind.Execute, "credential field",
            ("project_path", "Game"), ("operation", "call"), ("tool_name", "scene_polish"), ("arguments_json", "{\"api_token\":\"never\"}"));
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(blocked, default)).Decision);
        Assert.Equal(SecurityDecision.ApprovalRequired, (await enabled.ValidateAsync(allowed, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(traversal, default)).Decision);
        Assert.Equal(SecurityDecision.Denied, (await enabled.ValidateAsync(secret, default)).Decision);
    }

    [Fact] public void TaskAuthorizationSession_AllowsOnlyScopedUnityActionAndConsumesBudget()
    {
        var session = new TaskAuthorizationSession();
        session.Activate("Game", TimeSpan.FromMinutes(30), 2);
        var approved = new ApprovalRequest(UnityCall("build_windows"), "build the approved project");

        Assert.True(session.TryConsume(approved));
        Assert.True(session.Snapshot.Active);
        Assert.Equal(1, session.Snapshot.RemainingActions);
        Assert.True(session.TryConsume(approved));
        Assert.False(session.Snapshot.Active);
        Assert.False(session.TryConsume(approved));
    }

    [Fact] public void TaskAuthorizationSession_BlocksCommandsWrongProjectsAndMalformedCalls()
    {
        var session = new TaskAuthorizationSession();
        session.Activate("Game", TimeSpan.FromMinutes(30), 5);
        var command = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "command", ("command", "git --version"));
        var wrongProject = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "wrong project", ("project_path", "OtherGame"), ("operation", "compile"));
        var malformed = ToolCall.Create(ToolKind.StageWebAsset, PermissionKind.Network, "missing arguments", ("project_path", "Game"));
        var cloudGeneration = GeneratedAssetCall();

        Assert.False(session.TryConsume(new ApprovalRequest(command, "blocked")));
        Assert.False(session.TryConsume(new ApprovalRequest(wrongProject, "blocked")));
        Assert.False(session.TryConsume(new ApprovalRequest(malformed, "blocked")));
        Assert.False(session.TryConsume(new ApprovalRequest(cloudGeneration, "cloud credits always need explicit approval")));
        Assert.Equal(5, session.Snapshot.RemainingActions);
    }

    [Fact] public void TaskAuthorizationSession_AllowsNarrowCc0AssetFlowOnly()
    {
        var session = new TaskAuthorizationSession();
        session.Activate("Game", TimeSpan.FromMinutes(30), 4);
        var cc0Search = ToolCall.Create(ToolKind.SearchAssetCatalog, PermissionKind.Network, "find CC0 assets",
            ("query", "forest"), ("media_type", "image"), ("license_filter", "cc0"), ("max_results", "8"));
        var commercialSearch = cc0Search with { Arguments = new Dictionary<string, string>(cc0Search.Arguments) { ["license_filter"] = "commercial" } };
        var cc0Asset = ToolCall.Create(ToolKind.StageWebAsset, PermissionKind.Network, "stage CC0 asset",
            ("project_path", "Game"), ("file_name", "tree.png"), ("url", "https://assets.example.com/tree.png"),
            ("source_page", "https://assets.example.com/tree"), ("license", "CC0"));
        var attributionAsset = cc0Asset with { Arguments = new Dictionary<string, string>(cc0Asset.Arguments) { ["license"] = "CC BY 4.0" } };

        Assert.True(session.TryConsume(new ApprovalRequest(cc0Search, "search")));
        Assert.False(session.TryConsume(new ApprovalRequest(commercialSearch, "blocked")));
        Assert.True(session.TryConsume(new ApprovalRequest(cc0Asset, "stage")));
        Assert.False(session.TryConsume(new ApprovalRequest(attributionAsset, "blocked")));
        Assert.Equal(2, session.Snapshot.RemainingActions);
    }

    [Fact] public void TaskAuthorizationSession_RevokeEndsGrantImmediately()
    {
        var session = new TaskAuthorizationSession();
        session.Activate("Game", TimeSpan.FromMinutes(30), 3);
        session.Revoke();
        Assert.Equal(TaskAuthorizationSnapshot.Inactive, session.Snapshot);
        Assert.False(session.TryConsume(new ApprovalRequest(UnityCall("compile"), "compile")));
    }

    [Fact] public void TaskAuthorizationSession_OvernightModeIsBoundedAndIncludesReviewedGameTemplates()
    {
        var session = new TaskAuthorizationSession();
        session.Activate("Game", TimeSpan.FromHours(10), 120);
        var kit = ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate, PermissionKind.Write, "install reviewed kit",
            ("project_path", "Game"), ("template_id", "platformer"));
        var scene = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "create reviewed scene",
            ("project_path", "Game"), ("operation", "create_game_template"),
            ("scene_path", "Assets/Scenes/Platformer.unity"), ("template_id", "platformer"), ("seed", "73118"));
        var gameplay = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "verify reviewed gameplay",
            ("project_path", "Game"), ("operation", "verify_gameplay"),
            ("scene_path", "Assets/Scenes/Platformer.unity"), ("duration_seconds", "5"));

        Assert.True(session.TryConsume(new ApprovalRequest(kit, "exact hash-verified kit")));
        Assert.True(session.TryConsume(new ApprovalRequest(scene, "typed non-overwriting scene")));
        Assert.True(session.TryConsume(new ApprovalRequest(gameplay, "deterministic runtime evidence")));
        Assert.Equal(117, session.Snapshot.RemainingActions);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Activate("Game", TimeSpan.FromHours(12.1), 120));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Activate("Game", TimeSpan.FromHours(10), 151));
    }

    private static ToolCall AssetCall(string url, string fileName) => ToolCall.Create(ToolKind.StageWebAsset, PermissionKind.Network, "stage asset",
        ("project_path", "Game"), ("file_name", fileName), ("url", url),
        ("source_page", "https://catalog.example.com/assets/tree"), ("license", "CC0-1.0"));

    private static ToolCall CatalogCall() => ToolCall.Create(ToolKind.SearchAssetCatalog, PermissionKind.Network, "search catalogue",
        ("query", "forest path"), ("media_type", "image"), ("license_filter", "commercial"), ("max_results", "5"));

    private static ToolCall GeneratedAssetCall() => ToolCall.Create(ToolKind.Generate3DAsset, PermissionKind.Network, "generate original 3D asset",
        ("project_path", "Game"), ("provider", "seele"), ("asset_name", "frontier-watchtower"),
        ("prompt", "An original weathered timber ranger watchtower with efficient topology and realistic PBR materials."),
        ("model", "Seele02-flash"));

    private static ToolCall MeshyGeneratedAssetCall() => ToolCall.Create(ToolKind.Generate3DAsset, PermissionKind.Network, "generate original 3D asset",
        ("project_path", "Game"), ("provider", "meshy"), ("asset_name", "frontier-watchtower"),
        ("prompt", "An original weathered timber ranger watchtower with efficient topology and realistic PBR materials."),
        ("model", "meshy-6"));

    private static ToolCall InspectAssetCall(string fileName) => ToolCall.Create(ToolKind.InspectStagedAsset, PermissionKind.Read,
        "inspect quarantined asset", ("project_path", "Game"), ("file_name", fileName));

    private static ToolCall ImportAssetCall(string fileName, string destination, string hash) => ToolCall.Create(ToolKind.ImportStagedAsset,
        PermissionKind.Write, "import inspected asset", ("project_path", "Game"), ("file_name", fileName),
        ("destination_path", destination), ("expected_sha256", hash));

    private static ToolCall UnityCall(string operation) => ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
        "Unity " + operation, ("project_path", "Game"), ("operation", operation));

    private static ToolCall PrepareGeneratedAssetCall(string hash) => ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
        "prepare generated asset", ("project_path", "Game"), ("operation", "prepare_generated_asset"),
        ("scene_path", "Assets/Scenes/FrontierValley.unity"), ("asset_path", "Assets/ForgeCore/Generated/frontier-watchtower.glb"),
        ("expected_sha256", hash), ("parent_object_name", "Forest Watchtower"), ("object_name", "Generated Frontier Watchtower"),
        ("target_height", "16"), ("collider_mode", "mesh"), ("replace_placeholder", "true"));

    private static ToolCall InspectGameArtCall(string hash) => ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
        "inspect generated game art", ("project_path", "Game"), ("operation", "inspect_game_art_asset"),
        ("asset_path", "Assets/ForgeCore/Generated/frontier-watchtower.glb"), ("expected_sha256", hash),
        ("asset_set_id", "frontier-watchtower"), ("asset_role", "environment_props"), ("style_family", "realistic_pbr"));

    private static ToolCall CaptureGameArtPreviewCall(string hash) => ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute,
        "capture generated game-art preview", ("project_path", "Game"), ("operation", "capture_game_art_preview"),
        ("asset_path", "Assets/ForgeCore/Generated/frontier-watchtower.glb"), ("expected_sha256", hash),
        ("asset_set_id", "frontier-watchtower"), ("output_path", "Previews/GameArt/frontier-watchtower.png"));

    private static ToolCall ReviewGameArtCall(string reviewKind, string assetHash, string previewHash, string previewProvenanceHash) =>
        ToolCall.Create(ToolKind.ReviewGameArtAsset, PermissionKind.Write, "record exact human game-art review",
            ("project_path", "Game"), ("review_kind", reviewKind), ("asset_set_id", "frontier-watchtower"),
            ("asset_path", "Assets/ForgeCore/Generated/frontier-watchtower.glb"), ("expected_asset_sha256", assetHash),
            ("preview_path", "Previews/GameArt/frontier-watchtower.png"), ("expected_preview_sha256", previewHash),
            ("expected_preview_provenance_sha256", previewProvenanceHash));

    private async Task<(string AssetPath, string Hash, string Meta)> CreateImportedGeneratedArtAssetAsync()
    {
        var asset = Path.Combine(_root, "Game", "Assets", "ForgeCore", "Generated", "frontier-watchtower.glb");
        Directory.CreateDirectory(Path.GetDirectoryName(asset)!); await File.WriteAllBytesAsync(asset, MinimalGlb());
        var hash = await HashAsync(asset);
        await File.WriteAllTextAsync(asset + ".asset.json", JsonSerializer.Serialize(new
        {
            file = "frontier-watchtower.glb", destinationPath = "Assets/ForgeCore/Generated/frontier-watchtower.glb",
            sourcePage = "https://www.meshy.ai/", downloadUrl = "https://assets.meshy.example/model.glb",
            license = "MESHY_GENERATED_REVIEW_REQUIRED", sha256 = hash, bytes = new FileInfo(asset).Length,
            format = "glTF 2.0 binary model", inspection = "bounded", stagedUtc = DateTimeOffset.UtcNow.AddMinutes(-1), importedUtc = DateTimeOffset.UtcNow
        }));
        const string meta = "fileFormatVersion: 2\nguid: 1234567890abcdef1234567890abcdef\n";
        await File.WriteAllTextAsync(asset + ".meta", meta);
        return (asset, hash, meta);
    }

    private static async Task<string> HashAsync(string path) =>
        Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant();

    private async Task CreateBareUnityProjectAsync()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Game", "Assets"));
        Directory.CreateDirectory(Path.Combine(_root, "Game", "ProjectSettings"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Game", "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.50f1\n");
    }

    private static string ValueAfter(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index + 1 < arguments.Count; index++)
            if (arguments[index] == name) return arguments[index + 1];
        throw new InvalidOperationException($"Argument {name} was not supplied.");
    }

    private async Task<(string Hash, string Path)> CreateStagedAssetAsync(string fileName, byte[] payload)
    {
        var staging = Path.Combine(_root, "Game", ".forgecore", "staging"); Directory.CreateDirectory(staging);
        var path = Path.Combine(staging, fileName); await File.WriteAllBytesAsync(path, payload);
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var manifest = new Dictionary<string, object?>
        {
            ["file"] = fileName,
            ["sourcePage"] = "https://source.example/assets/item",
            ["downloadUrl"] = "https://download.example/assets/" + fileName,
            ["license"] = "CC0-1.0",
            ["sha256"] = hash,
            ["bytes"] = payload.LongLength,
            ["stagedUtc"] = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(path + ".asset.json", JsonSerializer.Serialize(manifest));
        return (hash, path);
    }

    private static byte[] MinimalPng(uint width, uint height)
    {
        var result = new byte[58];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8, 4), 13); Encoding.ASCII.GetBytes("IHDR").CopyTo(result, 12);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(16, 4), width); BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(20, 4), height);
        result[24] = 8; result[25] = 2;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(33, 4), 1); Encoding.ASCII.GetBytes("IDAT").CopyTo(result, 37); result[41] = 0;
        Encoding.ASCII.GetBytes("IEND").CopyTo(result, 50);
        return result;
    }

    private static byte[] GlbWithExternalUri()
    {
        var text = "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":0,\"uri\":\"https://evil.example/x.bin\"}]}";
        while (Encoding.UTF8.GetByteCount(text) % 4 != 0) text += " ";
        var json = Encoding.UTF8.GetBytes(text); var result = new byte[20 + json.Length];
        Encoding.ASCII.GetBytes("glTF").CopyTo(result, 0); BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)json.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A); json.CopyTo(result, 20);
        return result;
    }

    private static byte[] MinimalGlb()
    {
        var text = "{\"asset\":{\"version\":\"2.0\"},\"nodes\":[],\"meshes\":[]}";
        while (Encoding.UTF8.GetByteCount(text) % 4 != 0) text += " ";
        var json = Encoding.UTF8.GetBytes(text); var result = new byte[20 + json.Length];
        Encoding.ASCII.GetBytes("glTF").CopyTo(result, 0); BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)json.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4E4F534A); json.CopyTo(result, 20);
        return result;
    }

    private sealed class Fixed3DProvider(Generated3DResult result) : I3DGenerationProvider
    {
        public string Name => "seele";
        public bool IsConfigured => true;
        public Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FixedMeshy3DProvider(Generated3DResult result) : I3DGenerationProvider
    {
        public string Name => "meshy";
        public bool IsConfigured => true;
        public Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class Blocking3DProvider : I3DGenerationProvider
    {
        public string Name => "seele";
        public bool IsConfigured => true;
        public async Task<Generated3DResult> GenerateAsync(Generated3DAssetSpec request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class SeeleApiHandler : HttpMessageHandler
    {
        public bool SawBearer { get; private set; }
        public string CreateBody { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SawBearer |= request.Headers.Authorization?.Scheme == "Bearer" && !string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter);
            string payload;
            if (request.Method == HttpMethod.Post)
            {
                CreateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                payload = "{\"ok\":true,\"data\":{\"job_id\":\"job_123\"}}";
            }
            else
            {
                payload = "{\"ok\":true,\"data\":{\"job_id\":\"job_123\",\"status\":\"finished\",\"total_koin\":77,\"artifacts\":[{\"name\":\"Watchtower\",\"urls\":[\"https://downloads.example/watchtower.glb\"]}],\"meta\":{\"platform_url\":\"https://www.seeles.ai/workspace/job_123\"}}}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class MeshyApiHandler(bool alwaysPending = false) : HttpMessageHandler
    {
        private int _postCount;
        public bool AllRequestsUsedBearer { get; private set; } = true;
        public List<string> PostBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AllRequestsUsedBearer &= request.Headers.Authorization?.Scheme == "Bearer" &&
                !string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter);
            string payload;
            if (request.Method == HttpMethod.Post)
            {
                PostBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                payload = ++_postCount == 1 ? "{\"result\":\"preview_123\"}" : "{\"result\":\"refine_456\"}";
            }
            else if (request.RequestUri!.AbsolutePath.EndsWith("/preview_123", StringComparison.Ordinal))
            {
                payload = alwaysPending
                    ? "{\"id\":\"preview_123\",\"status\":\"IN_PROGRESS\",\"consumed_credits\":0}"
                    : "{\"id\":\"preview_123\",\"status\":\"SUCCEEDED\",\"consumed_credits\":20,\"model_urls\":{\"glb\":\"https://downloads.meshy.example/preview.glb\"}}";
            }
            else
            {
                payload = "{\"id\":\"refine_456\",\"status\":\"SUCCEEDED\",\"consumed_credits\":10,\"model_urls\":{\"glb\":\"https://downloads.meshy.example/watchtower.glb?signature=do-not-store\"}}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StaticResponseHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
    }

    private sealed class CatalogResponseHandler(string payload) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++; RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CancellableCatalogHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class CapturingIndexStore : IProjectIndexStore
    {
        public ProjectIndexSummary? Summary { get; private set; }
        public IReadOnlyList<ProjectIndexDocument> Documents { get; private set; } = [];
        public bool BlockReplacement { get; set; }
        public async Task ReplaceProjectIndexAsync(ProjectIndexSummary summary, IReadOnlyList<ProjectIndexDocument> documents, CancellationToken token)
        {
            if (BlockReplacement) await Task.Delay(Timeout.InfiniteTimeSpan, token);
            Summary = summary; Documents = documents.ToArray();
        }
        public Task<ProjectIndexSummary?> GetProjectIndexSummaryAsync(string projectPath, CancellationToken token) => Task.FromResult(Summary);
        public Task<IReadOnlyList<ProjectIndexDocument>> GetProjectIndexDocumentsAsync(string projectPath, CancellationToken token) => Task.FromResult(Documents);
    }

    private static string FindDotnetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var candidate = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        return File.Exists(candidate) ? candidate : "dotnet";
    }
}
