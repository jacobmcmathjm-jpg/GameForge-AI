using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("")][InlineData("profile")][InlineData("asset-list")][InlineData("missing")][InlineData("dimensions")][InlineData("memory")][InlineData("review")][InlineData("slots")]
    public async Task CombatVisualEvidenceBindsFreshOwnedAssetsAndNeverAcceptsArt(string defect)
    {
        await CreateBareUnityProjectAsync();var guard=new WorkspacePathGuard(_root);
        using(var installer=new ToolExecutor(guard,[])) {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"bridge",("project_path","Game")),default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        }
        var (asset,hash,meta)=await CreateImportedGeneratedArtAssetAsync();var exe=Path.Combine(_root,"Unity.exe");await File.WriteAllBytesAsync(exe,[0]);
        string[] visual={"SurfaceAlbedo.png","SurfaceNormal.png","ClothAlbedo.png","ClothNormal.png","MetalMask.png","SafetyAlbedo.png",
            "PresentationBevel.asset","Floor.mat","Wall.mat","Safety.mat","LightHousing.mat","Skybox.mat","SecurityReflection.cubemap",
            "GuardCloth.mat","GuardRubber.mat","GuardVisor.mat","GuardMetal.mat","GuardTrim.mat"};
        var folder=Path.Combine(_root,"Game","Assets","ForgeCore","CharacterAnimations","visual-check");
        UnityProcessRunner runner=async(_,args,_,_,token)=> {
            using var request=JsonDocument.Parse(await File.ReadAllBytesAsync(ValueAfter(args,"-forgecoreRequest"),token));var r=request.RootElement;
            Directory.CreateDirectory(folder);var clips=new[]{"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"};
            var files=clips.Select(n=>n+".anim").Concat(new[]{"Mannequin.controller","animation-report.json","FirstPersonArms.asset","RifleMetal.mat","RiflePolymer.mat","RifleTrim.mat","Concrete.mat","CombatLab.prefab","combat-report.json"}).Concat(visual);
            foreach(var file in files) {
                if(defect=="missing"&&file=="GuardVisor.mat")continue;
                if(file.EndsWith(".png",StringComparison.Ordinal))await File.WriteAllBytesAsync(Path.Combine(folder,file),MinimalPng(defect=="dimensions"?512u:256u,256),token);
                else await File.WriteAllTextAsync(Path.Combine(folder,file),"Synthetic fixture, not Unity or art acceptance.",token);
                await File.WriteAllTextAsync(Path.Combine(folder,file+".meta"),"synthetic metadata",token);
            }
            await File.WriteAllTextAsync(Path.Combine(folder,"animation-report.json"),JsonSerializer.Serialize(new{schemaVersion=1,recipe="generic_mannequin_motion_v3",assetSha256=hash,clipCount=8,
                rootMotionPolicy="runtime_translation_in_place_clips",finite=true,clips,maximumLoopError=0,maximumEdgeStretch=2.5,maximumStanceSlip=.0001}),token);
            await File.WriteAllTextAsync(Path.Combine(folder,"combat-report.json"),JsonSerializer.Serialize(new{schemaVersion=1,recipe="security_combat_lab_v1",assetSha256=hash,
                assetPath="Assets/ForgeCore/Generated/frontier-watchtower.glb",enemies=3,gridWidth=32,gridHeight=32,sourceTriangles=1000,armsTriangles=300,
                visualReviewed=defect=="review",rightsReviewed=false,stageComplete=false,visualProfile=defect=="profile"?"untrusted_profile":"industrial_range_finish_v1",
                visualAssets=defect=="asset-list"?visual.Reverse().ToArray():visual,textureSize=256,ownedTextureCount=7,reflectionFaceSize=64,materialSlotCount=defect=="slots"?0:5,
                ownedTextureMemoryBytes=defect=="memory"?33_000_000:2_000_000}),token);
            var scene=Path.Combine(_root,"Game","Assets","Scenes","Visual.unity");Directory.CreateDirectory(Path.GetDirectoryName(scene)!);await File.WriteAllTextAsync(scene,"synthetic scene",token);
            var png=Path.Combine(_root,"Game","Previews","Visual.png");Directory.CreateDirectory(Path.GetDirectoryName(png)!);await File.WriteAllBytesAsync(png,MinimalPng(1920,1040),token);
            await File.WriteAllTextAsync(ValueAfter(args,"-forgecoreResult"),JsonSerializer.Serialize(new{protocolVersion="1.0",bridgeVersion="0.17.22",editorVersion="6000.0.50f1",requestId=r.GetProperty("requestId").GetString(),operation="create_security_combat_lab",success=true,summary="Synthetic visual evidence"}),token);
            return new(0,"","");
        };
        var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"Fresh visual fixture",("project_path","Game"),("operation","create_security_combat_lab"),
            ("asset_path","Assets/ForgeCore/Generated/frontier-watchtower.glb"),("expected_sha256",hash),("asset_set_id","visual-check"),("scene_path","Assets/Scenes/Visual.unity"),("output_path","Previews/Visual.png"));
        using var tools=new ToolExecutor(guard,[],[],1024*1024,unityEditorPath:exe,unityEditorVersion:"6000.0.50f1",unityProcessRunner:runner);
        Assert.Equal(SecurityDecision.ApprovalRequired,(await new SecurityPolicy(guard,[],unityEditorPath:exe,unityEditorVersion:"6000.0.50f1").ValidateAsync(call,default)).Decision);
        var result=await tools.ExecuteAsync(call,default);Assert.Equal(defect=="",result.Success);
        var evidence=Path.Combine(_root,"Game",".forgecore","unity","character-animation-evidence");
        if(defect=="") {
            using var json=JsonDocument.Parse(await File.ReadAllBytesAsync(Directory.GetFiles(evidence,"*.json").Single()));
            Assert.Equal(70,json.RootElement.GetProperty("artifacts").GetArrayLength());Assert.False(json.RootElement.GetProperty("visualReviewed").GetBoolean());Assert.False(json.RootElement.GetProperty("stageComplete").GetBoolean());
            Assert.False((await tools.ExecuteAsync(call,default)).Success);
        }else Assert.False(Directory.Exists(evidence)&&Directory.GetFiles(evidence,"*.json").Length>0);
        Assert.Equal(meta,await File.ReadAllTextAsync(asset+".meta"));Assert.Equal(hash,await HashAsync(asset));
    }
}
