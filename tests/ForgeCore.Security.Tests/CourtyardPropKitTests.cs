using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Fact]public async Task ShapedPropsAreReusableTemplateCodeNotAnApprovedSourceReplacement()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var source=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreCourtyardProps.cs"));
        Assert.Contains(CourtyardPropEvidence.Profile,source);Assert.Contains("original_code_authored_template_not_blender_or_vault",source);
        Assert.Contains("approvedLibraryAssetsPlaced=false",source);Assert.Contains("visualReviewed=false,rightsReviewed=false",source);
        Assert.Contains("renderer.enabled=false",source);Assert.Contains("before.Count==after.Count&&before.All",source);
        Assert.DoesNotContain("AddComponent<BoxCollider>",source);Assert.DoesNotContain("AddComponent<MeshCollider>",source);
        Assert.DoesNotContain("AddComponent<IndustrialPropIdentity>",source);Assert.DoesNotContain("FileMode.Create,",source);
        Assert.Contains("new LOD[3]",source);Assert.Contains("RecalculateTangents",source);Assert.Contains("8*1024*1024",source);
        Assert.Contains("TextureImporterFormat.BC5",source);Assert.Contains("TextureImporterFormat.BC7",source);
        var presentation=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreCourtyardPresentation.cs"));
        Assert.Contains("ForgeCoreCourtyardProps.Apply(root,folder)",presentation);Assert.DoesNotContain("Proxy cargo door hinge",presentation);
        var tests=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","CourtyardPropTests.cs"));
        Assert.Contains("TenUnitScaleLodPropsReplaceOnlyRenderingInsideExistingCollisionFootprints",tests);
        Assert.Contains("BarrelIsRoundBarrierNarrowsAtTopAndFenceHasRealOpenMesh",tests);
    }
    [Fact]public async Task HeroFinishKeepsOriginalSocketsMotionAudioAndReviewBoundaries()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var source=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreVisualPolish.cs"));
        Assert.Contains(CourtyardHeroEvidence.Profile,source);Assert.Contains("socketsUnchanged",source);Assert.Contains("animationsChanged=false,audioChanged=false",source);
        Assert.Contains("approvedLibraryAssetsPlaced=false",source);Assert.Contains("ReflectionProbeMode.Baked",source);Assert.Contains("Lightmapping.BakeReflectionProbe",source);
        Assert.DoesNotContain("AddComponent<BoxCollider>",source);Assert.DoesNotContain("SetCurve(",source);Assert.DoesNotContain("AddComponent<Light>",source);
        var mission=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreIndustrialMissionBuilder.cs"));Assert.Contains("ForgeCoreVisualPolish.Apply(root,folder)",mission);
        var isolation=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","PlayModeBackgroundIsolation.cs"));
        Assert.Contains("[SetUpFixture]",isolation);Assert.Contains("root.SetActive(false)",isolation);Assert.DoesNotContain("SaveScene",isolation);Assert.DoesNotContain("DestroyImmediate",isolation);
    }
}
