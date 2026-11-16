using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Theory]
    [InlineData("")][InlineData("folder")][InlineData("scene")][InlineData("preview")][InlineData("permission")]
    public async Task CombatLabPolicyRequiresFreshScopeAndKeepsApprovalExplicit(string occupied)
    {
        await CreateBareUnityProjectAsync();var guard=new WorkspacePathGuard(_root);
        using(var installer=new ToolExecutor(guard,[])) {
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"install",("project_path","Game")),default)).Success);
            Assert.True((await installer.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        }
        var exe=Path.Combine(_root,"Unity.exe");await File.WriteAllBytesAsync(exe,[0]);
        var (_,hash,_)=await CreateImportedGeneratedArtAssetAsync();
        var call=ToolCall.Create(ToolKind.UnityEditorAction,occupied=="permission"?PermissionKind.Read:PermissionKind.Execute,"combat lab",
            ("project_path","Game"),("operation","create_security_combat_lab"),("asset_path","Assets/ForgeCore/Generated/frontier-watchtower.glb"),
            ("expected_sha256",hash),("asset_set_id","combat-fixture"),("scene_path","Assets/Scenes/Combat.unity"),("output_path","Previews/Combat.png"));
        if(occupied=="folder")Directory.CreateDirectory(Path.Combine(_root,"Game","Assets","ForgeCore","CharacterAnimations","combat-fixture"));
        if(occupied=="scene"){var p=Path.Combine(_root,"Game","Assets","Scenes","Combat.unity");Directory.CreateDirectory(Path.GetDirectoryName(p)!);await File.WriteAllTextAsync(p,"retain");}
        if(occupied=="preview"){var p=Path.Combine(_root,"Game","Previews","Combat.png");Directory.CreateDirectory(Path.GetDirectoryName(p)!);await File.WriteAllBytesAsync(p,MinimalPng(1280,720));}
        var result=await new SecurityPolicy(guard,[],unityEditorPath:exe,unityEditorVersion:"6000.0.50f1").ValidateAsync(call,default);
        Assert.Equal(occupied==""?SecurityDecision.ApprovalRequired:SecurityDecision.Denied,result.Decision);
        if(occupied=="") {Assert.Contains("skinned",result.Reason);Assert.Contains("No downloads",result.Reason);}
    }
}
