using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("")][InlineData("hash")][InlineData("loop")][InlineData("stretch")][InlineData("stance")][InlineData("missing")][InlineData("meta")][InlineData("occupied")][InlineData("kit")]
    public async Task AnimationPreparationRetainsOnlyHashBoundNonOverwritingDiagnosticEvidence(string failure)
    {
        await CreateBareUnityProjectAsync();var guard=new WorkspacePathGuard(_root);
        using(var installer=new ToolExecutor(guard,[]))
        {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"install",("project_path","Game")),default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        }
        var exe=Path.Combine(_root,"Unity.exe");await File.WriteAllBytesAsync(exe,[0]);var (asset,hash,meta)=await CreateImportedGeneratedArtAssetAsync();
        var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"S03 diagnostic fixture",
            ("project_path","Game"),("operation","prepare_character_animation"),("asset_path","Assets/ForgeCore/Generated/frontier-watchtower.glb"),
            ("expected_sha256",hash),("asset_set_id","test-motion"),("scene_path","Assets/Scenes/Motion.unity"),("output_path","Previews/Motion.png"));
        var folder=Path.Combine(_root,"Game","Assets","ForgeCore","CharacterAnimations","test-motion");
        if(failure=="occupied")Directory.CreateDirectory(folder);
        var policy=new SecurityPolicy(guard,[],unityEditorPath:exe,unityEditorVersion:"6000.0.50f1");
        Assert.Equal(failure=="occupied"?SecurityDecision.Denied:SecurityDecision.ApprovalRequired,(await policy.ValidateAsync(call,default)).Decision);
        var calls=0;
        UnityProcessRunner runner=async(_,args,_,_,token)=>
        {
            calls++;using var request=JsonDocument.Parse(await File.ReadAllBytesAsync(ValueAfter(args,"-forgecoreRequest"),token));var r=request.RootElement;
            Directory.CreateDirectory(folder);var names=new[]{"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"};
            foreach(var file in names.Select(n=>n+".anim").Concat(new[]{"Mannequin.controller","animation-report.json"}))
            {if(failure=="missing"&&file=="Run.anim")continue;await File.WriteAllTextAsync(Path.Combine(folder,file),"synthetic fixture, not Unity acceptance",token);await File.WriteAllTextAsync(Path.Combine(folder,file+".meta"),"synthetic metadata",token);}
            await File.WriteAllTextAsync(Path.Combine(folder,"animation-report.json"),JsonSerializer.Serialize(new{schemaVersion=1,recipe="generic_mannequin_motion_v3",
                assetSha256=failure=="hash"?new string('0',64):hash,clipCount=8,rootMotionPolicy="runtime_translation_in_place_clips",finite=true,clips=names,
                maximumLoopError=failure=="loop"?.01:0,maximumEdgeStretch=failure=="stretch"?3.6:2.5,maximumStanceSlip=failure=="stance"?.02:.0001}),token);
            var scene=Path.Combine(_root,"Game","Assets","Scenes","Motion.unity");Directory.CreateDirectory(Path.GetDirectoryName(scene)!);await File.WriteAllTextAsync(scene,"synthetic scene",token);
            var png=Path.Combine(_root,"Game","Previews","Motion.png");Directory.CreateDirectory(Path.GetDirectoryName(png)!);await File.WriteAllBytesAsync(png,MinimalPng(1920,1040),token);
            if(failure=="meta")await File.WriteAllTextAsync(asset+".meta","mutated",token);
            if(failure=="kit")await File.WriteAllTextAsync(Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates","Runtime","PrototypeSkinnedAnimator.cs"),"modified after execution began",token);
            await File.WriteAllTextAsync(ValueAfter(args,"-forgecoreResult"),JsonSerializer.Serialize(new{protocolVersion="1.0",bridgeVersion="0.17.22",editorVersion="6000.0.50f1",
                requestId=r.GetProperty("requestId").GetString(),operation="prepare_character_animation",success=true,summary="Synthetic animation receipt"}),token);return new(0,"","");
        };
        using var tools=new ToolExecutor(guard,[],[],1024*1024,unityEditorPath:exe,unityEditorVersion:"6000.0.50f1",unityProcessRunner:runner);
        var result=await tools.ExecuteAsync(call,default);Assert.Equal(failure=="",result.Success);Assert.Equal(failure=="occupied"?0:1,calls);
        var evidence=Path.Combine(_root,"Game",".forgecore","unity","character-animation-evidence");
        if(failure=="")
        {
            using var e=JsonDocument.Parse(await File.ReadAllBytesAsync(Directory.GetFiles(evidence,"*.json").Single()));
            Assert.False(e.RootElement.GetProperty("stageComplete").GetBoolean());Assert.False(e.RootElement.GetProperty("visualReviewed").GetBoolean());Assert.False(e.RootElement.GetProperty("rightsReviewed").GetBoolean());
            Assert.Equal(20,e.RootElement.GetProperty("artifacts").GetArrayLength());Assert.Equal(SecurityDecision.Denied,(await policy.ValidateAsync(call,default)).Decision);Assert.False((await tools.ExecuteAsync(call,default)).Success);
        }
        else Assert.False(Directory.Exists(evidence)&&Directory.GetFiles(evidence,"*.json").Length>0);
        Assert.Equal(meta,await File.ReadAllTextAsync(asset+".meta"));
    }
}
