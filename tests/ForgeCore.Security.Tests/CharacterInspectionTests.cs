using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("")][InlineData("pose")][InlineData("hash")][InlineData("missing")]
    [InlineData("meta")][InlineData("scene")][InlineData("png")]
    public async Task CharacterInspection_IsHashBoundNonOverwritingAndCannotGrantReview(string failure)
    {
        await CreateBareUnityProjectAsync();var guard=new WorkspacePathGuard(_root);
        using(var installer=new ToolExecutor(guard,[]))
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"install",("project_path","Game")),default)).Success);
        var exe=Path.Combine(_root,"Unity.exe");await File.WriteAllBytesAsync(exe,[0]);
        var (asset,hash,meta)=await CreateImportedGeneratedArtAssetAsync();
        var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"Inspect generic character rig",
            ("project_path","Game"),("operation","inspect_character_rig"),("asset_path","Assets/ForgeCore/Generated/frontier-watchtower.glb"),
            ("expected_sha256",hash),("scene_path","Assets/Scenes/RigReview.unity"),("output_path","Previews/RigReview.png"));
        var policy=new SecurityPolicy(guard,[],unityEditorPath:exe,unityEditorVersion:"6000.0.50f1");
        Assert.Equal(SecurityDecision.ApprovalRequired,(await policy.ValidateAsync(call,default)).Decision);
        UnityProcessRunner runner=async (_,args,_,_,token)=>
        {
            using var request=JsonDocument.Parse(await File.ReadAllBytesAsync(ValueAfter(args,"-forgecoreRequest"),token));var root=request.RootElement;
            var scene=Path.Combine(_root,"Game",root.GetProperty("scenePath").GetString()!);
            var png=Path.Combine(_root,"Game",root.GetProperty("outputPath").GetString()!);
            Directory.CreateDirectory(Path.GetDirectoryName(scene)!);Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            if(failure!="scene")await File.WriteAllTextAsync(scene,"synthetic scene fixture, not real engine acceptance",token);
            if(failure!="png")await File.WriteAllBytesAsync(png,MinimalPng(1280,1440),token);
            if(failure=="meta")await File.WriteAllTextAsync(asset+".meta","mutated",token);
            var rig=new CharacterRigEvidence(1,failure=="hash"?new string('0',64):hash,"generic_named_bones_v1",22,1000,0,0,0,0,1.8,0,
                [new("arms",300,.4,failure=="pose"?5.72:1.5,true),new("legs",400,.5,2,true),new("combined",800,.6,2.2,true)]);
            await File.WriteAllTextAsync(ValueAfter(args,"-forgecoreResult"),JsonSerializer.Serialize(new{
                protocolVersion="1.0",bridgeVersion="0.17.22",editorVersion="6000.0.50f1",
                requestId=root.GetProperty("requestId").GetString(),operation="inspect_character_rig",success=true,summary="Synthetic rig result",
                characterRig=failure=="missing"?null:rig}),token);
            return new(0,"","");
        };
        using var tools=new ToolExecutor(guard,[],[],1024*1024,unityEditorPath:exe,unityEditorVersion:"6000.0.50f1",unityProcessRunner:runner);
        var result=await tools.ExecuteAsync(call,default);
        Assert.Equal(failure=="",result.Success);
        var directory=Path.Combine(_root,"Game",".forgecore","unity","character-rig-evidence");
        if(failure=="")
        {
            var path=Directory.EnumerateFiles(directory,"*.json").Single();using var e=JsonDocument.Parse(await File.ReadAllBytesAsync(path));
            Assert.True(e.RootElement.GetProperty("technicalReady").GetBoolean());
            Assert.False(e.RootElement.GetProperty("visualReviewed").GetBoolean());Assert.False(e.RootElement.GetProperty("rightsReviewed").GetBoolean());
            Assert.Equal(hash,e.RootElement.GetProperty("assetSha256").GetString());
            Assert.Equal(SecurityDecision.Denied,(await policy.ValidateAsync(call,default)).Decision);
            Assert.False((await tools.ExecuteAsync(call,default)).Success);
        }
        else Assert.False(Directory.Exists(directory)&&Directory.EnumerateFiles(directory,"*.json").Any());
        Assert.Equal(meta,await File.ReadAllTextAsync(asset+".meta"));
        Assert.False(Directory.Exists(Path.Combine(_root,"Game",".forgecore","unity","art-reviews")));
    }
}
