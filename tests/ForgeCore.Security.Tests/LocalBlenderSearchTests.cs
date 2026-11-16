using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Security.Tests;

public sealed partial class SecurityTests
{
    private static ToolCall FindLocal(params (string Key,string Value)[] filters) => ToolCall.Create(
        ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Find local candidates",
        new[]{("query","barrel"),("max_results","10")}.Concat(filters).ToArray());
    [Fact]public async Task BlenderSearchEmptyLibraryIsReadOnlyAndNeedsNoConfiguredEditors()
    {
        var guard=new WorkspacePathGuard(_root);var policy=new SecurityPolicy(guard,[]);
        var call=FindLocal();Assert.Equal(SecurityDecision.Allowed,(await policy.ValidateAsync(call,default)).Decision);
        Assert.Equal(SecurityDecision.Denied,(await policy.ValidateAsync(call with {RequiredPermission=PermissionKind.Execute},default)).Decision);
        using var executor=new ToolExecutor(guard,[]);var result=await executor.ExecuteAsync(call,default);
        Assert.True(result.Success,result.Error);using var json=JsonDocument.Parse(result.Output);
        Assert.Empty(json.RootElement.GetProperty("results").EnumerateArray());Assert.False(json.RootElement.GetProperty("truncated").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore")));
    }
    [Fact]public async Task BlenderSearchVerifiesCompleteSourceAndNeverPromotesOrImportsCandidates()
    {
        var exe=await BlenderFixtureExecutable();var launches=0;
        using var create=BlenderExecutor(exe,async(e,a,w,t,c)=>{launches++;return await BlenderFixtureRunner(e,a,w,t,c);});
        var made=await create.ExecuteAsync(BarrelCall(),default);Assert.True(made.Success,made.Error);
        using var original=JsonDocument.Parse(made.Output);var bundle=original.RootElement.GetProperty("bundleSha256").GetString()!;
        var library=Path.Combine(_root,".forgecore","blender-library","objects",bundle);
        var before=Directory.GetFiles(library).ToDictionary(p=>Path.GetFileName(p)!,p=>SHA256.HashData(File.ReadAllBytes(p)));
        using var search=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var call=FindLocal(("asset_role","environment_props"),("height_min_mm","1000"),("height_max_mm","1200"),("max_triangles","100"));
        var result=await search.ExecuteAsync(call,default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);var entry=Assert.Single(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal(bundle,entry.GetProperty("bundleSha256").GetString());Assert.Equal("barrel-test",entry.GetProperty("assetName").GetString());
        Assert.True(entry.GetProperty("editableSource").GetBoolean());Assert.False(entry.GetProperty("rigged").GetBoolean());
        Assert.Equal(0,entry.GetProperty("animationCount").GetInt32());Assert.Equal("unreviewed_local_recipe",entry.GetProperty("reviewState").GetString());
        foreach(var flag in new[]{"visualReviewed","rightsReviewed","placementApproved"})Assert.False(entry.GetProperty(flag).GetBoolean());
        Assert.Equal(1,launches);Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","asset-vault")));
        Assert.False(Directory.Exists(Path.Combine(_root,"Game","Assets","ForgeCore")));
        foreach(var file in before)Assert.Equal(file.Value,SHA256.HashData(File.ReadAllBytes(Path.Combine(library,file.Key!))));
    }
    [Theory]
    [InlineData("asset_role","characters")][InlineData("rig_required","true")]
    [InlineData("recipe","industrial_security_v1")][InlineData("height_min_mm","1200")]
    [InlineData("height_max_mm","1000")][InlineData("max_triangles","79")]
    public async Task BlenderSearchRejectsUnsuitableCandidateRatherThanSubstituting(string field,string value)
    {
        var exe=await BlenderFixtureExecutable();using var executor=BlenderExecutor(exe,BlenderFixtureRunner);
        Assert.True((await executor.ExecuteAsync(BarrelCall(),default)).Success);
        var result=await executor.ExecuteAsync(FindLocal((field,value)),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);Assert.Empty(json.RootElement.GetProperty("results").EnumerateArray());
    }
    [Theory]
    [InlineData("asset.glb")][InlineData("model.blend")][InlineData("entry.json")]
    [InlineData("recipe.py")][InlineData("request.json")][InlineData("preview.png")]
    public async Task BlenderSearchRejectsAnyChangedBundleSource(string file)
    {
        var exe=await BlenderFixtureExecutable();using var executor=BlenderExecutor(exe,BlenderFixtureRunner);
        var made=await executor.ExecuteAsync(BarrelCall(),default);Assert.True(made.Success,made.Error);
        using var source=JsonDocument.Parse(made.Output);var hash=source.RootElement.GetProperty("bundleSha256").GetString();
        await File.AppendAllTextAsync(Path.Combine(_root,".forgecore","blender-library","objects",hash!,file),"changed");
        var result=await executor.ExecuteAsync(FindLocal(),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);Assert.Empty(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal(1,json.RootElement.GetProperty("rejectedEntries").GetInt32());
    }
    [Fact]public async Task BlenderSearchBoundsDirectoryEnumerationAndHonoursCancellation()
    {
        var objects=Path.Combine(_root,".forgecore","blender-library","objects");Directory.CreateDirectory(objects);
        for(var i=0;i<129;i++)Directory.CreateDirectory(Path.Combine(objects,i.ToString("x64")));
        using var executor=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var result=await executor.ExecuteAsync(FindLocal(),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);Assert.Equal(128,json.RootElement.GetProperty("scannedEntries").GetInt32());
        Assert.True(json.RootElement.GetProperty("truncated").GetBoolean());
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(()=>executor.ExecuteAsync(FindLocal(),cancelled.Token));
    }
    [Fact]public async Task BlenderSearchDeclaresVerificationBudgetSkipInsteadOfClaimingExhaustiveSearch()
    {
        var exe=await BlenderFixtureExecutable();using var executor=BlenderExecutor(exe,BlenderFixtureRunner);
        var made=await executor.ExecuteAsync(BarrelCall(),default);using var original=JsonDocument.Parse(made.Output);
        var hash=original.RootElement.GetProperty("bundleSha256").GetString()!;
        var objects=Path.Combine(_root,".forgecore","blender-library","objects");
        var metadata=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(objects,hash,"entry.json")))!;
        foreach(var file in metadata["files"]!.AsArray())file!["bytes"]=20*1024*1024;
        var bytes=JsonSerializer.SerializeToUtf8Bytes(metadata);var changed=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var folder=Path.Combine(objects,changed);Directory.CreateDirectory(folder);await File.WriteAllBytesAsync(Path.Combine(folder,"entry.json"),bytes);
        var result=await executor.ExecuteAsync(FindLocal(),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);Assert.True(json.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Single(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.InRange(json.RootElement.GetProperty("verificationBytesCharged").GetInt64(),1,512L*1024*1024);
    }
}
