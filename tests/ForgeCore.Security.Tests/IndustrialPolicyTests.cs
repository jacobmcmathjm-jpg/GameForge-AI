using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("prepare_industrial_prop","")][InlineData("prepare_industrial_prop","folder")][InlineData("prepare_industrial_prop","foldermeta")]
    [InlineData("prepare_industrial_prop","scene")][InlineData("prepare_industrial_prop","preview")][InlineData("prepare_industrial_prop","permission")]
    [InlineData("create_industrial_mission","")][InlineData("create_industrial_mission","folder")][InlineData("create_industrial_mission","foldermeta")]
    [InlineData("create_industrial_mission","scene")][InlineData("create_industrial_mission","preview")][InlineData("create_industrial_mission","permission")]
    public async Task IndustrialEditorPolicyCannotOverwriteOrApproveItsOwnEvidence(string operation,string occupied)
    {
        await CreateBareUnityProjectAsync();var guard=new WorkspacePathGuard(_root);
        using(var installer=new ToolExecutor(guard,[])) {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"install",("project_path","Game")),default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        }
        var exe=Path.Combine(_root,"Unity.exe");await File.WriteAllBytesAsync(exe,[0]);
        var (_,hash,_)=await CreateImportedGeneratedArtAssetAsync();
        var call=ToolCall.Create(ToolKind.UnityEditorAction,occupied=="permission"?PermissionKind.Read:PermissionKind.Execute,"industrial technical fixture",
            ("project_path","Game"),("operation",operation),("asset_path","Assets/ForgeCore/Generated/frontier-watchtower.glb"),
            ("expected_sha256",hash),("asset_set_id","industrial-test"),("scene_path","Assets/Scenes/Industrial.unity"),("output_path","Previews/Industrial.png"));
        var category=operation=="prepare_industrial_prop"?"IndustrialProps":"CharacterAnimations";
        if(occupied=="folder"||occupied=="foldermeta")Directory.CreateDirectory(Path.Combine(_root,"Game","Assets","ForgeCore",category,"industrial-test"+(occupied=="foldermeta"?".meta":"")));
        if(occupied=="scene"){var p=Path.Combine(_root,"Game","Assets","Scenes","Industrial.unity");Directory.CreateDirectory(Path.GetDirectoryName(p)!);await File.WriteAllTextAsync(p,"retain");}
        if(occupied=="preview"){var p=Path.Combine(_root,"Game","Previews","Industrial.png");Directory.CreateDirectory(Path.GetDirectoryName(p)!);await File.WriteAllBytesAsync(p,MinimalPng(1280,720));}
        var result=await new SecurityPolicy(guard,[],unityEditorPath:exe,unityEditorVersion:"6000.0.50f1").ValidateAsync(call,default);
        Assert.Equal(occupied==""?SecurityDecision.ApprovalRequired:SecurityDecision.Denied,result.Decision);
        if(occupied==""){Assert.Contains("fresh",result.Reason);Assert.Contains("No human",result.Reason,StringComparison.OrdinalIgnoreCase);}
    }
}
