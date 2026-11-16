using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Fact] public async Task ConnectedInteriorIsOriginalNonCollidingAndProducesUnacceptedStaticComparison()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var result=await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"connected interior contract",("project_path","Game"),("template_id","first_person_action")),default);Assert.True(result.Success,result.Error);
        var kit=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var source=await File.ReadAllTextAsync(Path.Combine(kit,"Editor","ForgeCoreConnectedInterior.cs"));
        Assert.Contains("original_code_authored_geometry_and_screen_pattern",source);Assert.Contains("collisionUnchanged",source);Assert.Contains("report.addedColliders!=0",source);Assert.Contains("FileMode.CreateNew",source);
        Assert.Contains("static_editor_comparison_left_before_right_after_not_runtime_animation",source);Assert.Contains("humanArtAccepted=false",source);Assert.Contains("stageComplete=false",source);Assert.Contains("report.changedPixels<1000",source);Assert.DoesNotContain("AddComponent<BoxCollider>",source);
        var builder=await File.ReadAllTextAsync(Path.Combine(kit,"Editor","ForgeCoreConnectedLevelBuilder.cs"));Assert.Contains("using var interiorReview=ForgeCoreConnectedInterior.BeginReview(root)",builder);Assert.Contains("interiorReview.Save(folder)",builder);Assert.Contains("combatAndObjectivePosesUnchanged=poses.All",source);
        var mission=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","IndustrialMission.cs"));Assert.Contains("Clear warehouse guards first",mission);Assert.Contains("Clear control-room guard first",mission);
    }
    [Fact] public async Task ConnectedTemplateOwnsDepthTestedTextAndTrimsEveryLegacyNorthBatch()
    {
        await CreateBareUnityProjectAsync();
        using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var result=await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"connected presentation regression",("project_path","Game"),("template_id","first_person_action")),default);
        Assert.True(result.Success,result.Error);
        var kit=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var shader=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","Resources","ForgeCoreWorldSignText.shader"));
        Assert.Contains("ZTest LEqual",shader);Assert.Contains("ZWrite Off",shader);Assert.Contains("Blend SrcAlpha OneMinusSrcAlpha",shader);
        Assert.Contains("tex2D(_MainTex,i.uv).a",shader);Assert.Contains("i.color",shader);Assert.DoesNotContain("ZTest Always",shader);
        var builder=await File.ReadAllTextAsync(Path.Combine(kit,"Editor","ForgeCoreConnectedLevelBuilder.cs"));
        Assert.Contains("Courtyard detail v1 — bounded original candidate",builder);Assert.Contains("ConnectedSigns.mat",builder);
        Assert.Contains("report.worldSignsDepthTested",builder);Assert.Contains("report.legacyRoofFittingsTrimmed",builder);
        var tests=await File.ReadAllTextAsync(Path.Combine(kit,"Tests","PlayMode","ConnectedLevelTests.cs"));
        Assert.Contains("Assert.Greater(visible,30",tests);Assert.Contains("Assert.Less(hidden,visible/20",tests);
    }
}
