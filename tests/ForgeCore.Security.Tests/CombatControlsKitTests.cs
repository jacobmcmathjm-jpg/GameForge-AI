using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Fact]public async Task PhysicalPowerBoxUsesCabinetReachPersistsAfterUseAndHasNoFloatingPowerLabel()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var power=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","IndustrialPowerBox.cs"));
        Assert.Contains("solidCollider.ClosestPoint(playerPosition)",power);Assert.Contains("statusLamp.sharedMaterial",power);
        Assert.Contains("POWER / ONLINE",power);Assert.Contains("POWER / OFFLINE",power);
        var mission=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","IndustrialMission.cs"));
        Assert.Contains("powerBox.WithinReach",mission);Assert.Contains("i==0&&powerBox!=null",mission);
        var builder=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreCourtyardPresentation.cs"));
        Assert.DoesNotContain("Sign(p,\"POWER\"",builder);Assert.Contains("Cabinet-mounted power status plate",builder);
        var tests=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","IndustrialMissionTests.cs"));
        Assert.Contains("PowerBoxReplacesSphereStaysVisible",tests);Assert.Contains("must not walk through the power box",tests);
    }
    [Fact]public async Task CourtyardRecipePreservesCollisionAndDoesNotBypassLibraryReviews()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        var source=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreCourtyardPresentation.cs"));
        Assert.Contains("before.Count==after.Count&&before.All",source);Assert.Contains("decorativeColliderCount!=0",source);
        Assert.DoesNotContain("GetInstanceID",source);Assert.Contains("box.size.ToString",source);
        Assert.Contains("approvedLibraryAssetsPlaced=false",source);Assert.Contains("visualReviewed=false,rightsReviewed=false,stageComplete=false",source);
        Assert.Contains("mesh.CombineMeshes",source);Assert.Contains("shadowCastingMode",source);Assert.Contains("32*1024*1024",source);
        Assert.DoesNotContain("IndustrialPropIdentity",source);Assert.DoesNotContain("AddComponent<BoxCollider>",source);
        var mission=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreIndustrialMissionBuilder.cs"));
        Assert.Contains("ForgeCoreCourtyardPresentation.Apply(root,folder)",mission);
        var probe=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","CombatRenderProbe.cs"));Assert.Contains("-forgecore-courtyard-probe",probe);
        var tests=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","CourtyardPresentationTests.cs"));
        Assert.Contains("PresentationHasNoNewCollisionAndNoFalselyApprovedLibrarySources",tests);Assert.Contains("Assert.Ignore",tests);
    }
    [Fact]public async Task StandaloneRenderProbeIsDevelopmentOnlyOptInAndUsesActualFiringMarker()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var source=await File.ReadAllTextAsync(Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates","Runtime","CombatRenderProbe.cs"));
        Assert.Contains("#if DEVELOPMENT_BUILD && !UNITY_EDITOR",source);
        Assert.Contains("-forgecore-render-probe",source);Assert.Contains("FileMode.CreateNew",source);
        Assert.Contains("player.Simulate(new PrototypeInputFrame(fireHeld:true)",source);
        Assert.Contains("player.motion.ShotEmitted+=OnShot",source);Assert.Contains("camera=player.view.GetComponent<Camera>()",source);
        Assert.Contains("No actual firing marker",source);Assert.Contains("GraphicsDeviceType.Null",source);
        Assert.Contains("session.mission.TryInteract()",source);Assert.DoesNotContain("gameObject.SetActive(true)",source);
        Assert.Contains("-forgecore-corpse-probe",source);Assert.Contains("torsoMinY-s.floorY>.015f",source);
    }
    [Fact]public async Task CorpseSupportCorrectionIsDeathOnlyAndRequiresTorsoContactNotJustLowestVertex()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var runtime=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates","Runtime");
        var source=await File.ReadAllTextAsync(Path.Combine(runtime,"SecurityEnemyFootwork.cs"));
        Assert.Contains("if(dead){SettleCorpsePose(actionTime);GroundCorpse(actionTime);return;}",source);
        Assert.Contains("Quaternion.FromToRotation(axis.normalized,forward)",source);
        Assert.Contains("Vector3.up*.18f",source);
        Assert.Contains("RelaxHandSupport(weight)",source);Assert.Contains("torso-min+.012f",source);
        Assert.Contains("if(lift<=0)continue;",source);
        var tests=await File.ReadAllTextAsync(Path.Combine(runtime,"..","Tests","PlayMode","SecurityCombatTests.cs"));
        Assert.Contains("CorpseTorsoRestsOnFloorAt30_60_120Hz",tests);Assert.Contains("AutomaticDeathKeepsBodySupportedAcrossFramesAndPause",tests);
    }
    [Fact]public async Task FlashOwnsSupportedMaterialsAndFiringDoesNotUseUnassignedParticleBursts()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates","Runtime");
        var flash=await File.ReadAllTextAsync(Path.Combine(folder,"PrototypeMuzzleFlash.cs"));
        Assert.Contains("Resources.Load<Shader>(\"ForgeCoreSoftWarmFeedback\")",flash);
        Assert.DoesNotContain("Shader.Find(\"Standard\")",flash);Assert.Contains("renderer.sharedMaterials=effect.owned",flash);
        var shader=await File.ReadAllTextAsync(Path.Combine(folder,"Resources","ForgeCoreSoftWarmFeedback.shader"));
        Assert.Contains("Blend SrcAlpha One",shader);Assert.Contains("ZWrite Off",shader);Assert.Contains("Fallback Off",shader);
        Assert.DoesNotContain("shader_feature",shader);Assert.DoesNotContain("multi_compile",shader);
        var bullet=await File.ReadAllTextAsync(Path.Combine(folder,"SecurityCombatBullet.cs"));
        Assert.Contains("PrototypeMuzzleFlash.CreateFeedbackMaterial",bullet);Assert.DoesNotContain("Shader.Find(\"Standard\")",bullet);
        Assert.Contains("Destroy(material)",flash);Assert.Contains("Destroy(mesh)",flash);Assert.Contains("Lifetime=.055f",flash);
        var feedback=await File.ReadAllTextAsync(Path.Combine(folder,"PrototypeFeedback.cs"));
        Assert.Contains("PrototypeMuzzleFlash.Spawn(position,direction)",feedback);
        Assert.Contains("AssignParticleMaterial(value,color)",feedback);
        var fire=feedback[feedback.IndexOf("public static void Fire(Vector3 position,Vector3 direction)",StringComparison.Ordinal)..feedback.IndexOf("public static void Hit",StringComparison.Ordinal)];
        Assert.DoesNotContain("Burst(",fire);
    }
    [Fact]public async Task MotionProfileShipsTheExactHostTestedSourceAndKeepsReloadTargetExplicit()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var root=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        using var stream=typeof(ToolExecutor).Assembly.GetManifestResourceStream("ForgeCore.Tools.GuardMotionProfile.cs")!;
        using var reader=new StreamReader(stream);var exact=await reader.ReadToEndAsync();
        Assert.Equal(exact,await File.ReadAllTextAsync(Path.Combine(root,"Runtime","GuardMotionProfile.cs")));
        Assert.Contains("generic_mannequin_motion_v3",exact);
        var builder=await File.ReadAllTextAsync(Path.Combine(root,"Editor","ForgeCoreCharacterAnimationBuilder.cs"));
        Assert.Contains("GuardMotionProfile.Sample",builder);Assert.Contains("profile.KneeFold",builder);
        var combatBuilder=await File.ReadAllTextAsync(Path.Combine(root,"Editor","ForgeCoreSecurityCombatBuilder.cs"));
        Assert.Contains("enemy.leftGrip.SetParent(enemy.weapon,false)",combatBuilder);
        Assert.DoesNotContain("enemy.leftGrip.SetParent(magazine,false)",combatBuilder);
        var enemy=await File.ReadAllTextAsync(Path.Combine(root,"Runtime","SecurityCombatEnemy.cs"));
        Assert.Contains("SupportHandTarget",enemy);Assert.Contains("ContactPoint(leftUpper",enemy);
        Assert.DoesNotContain("private void LateUpdate()",enemy);
        var runtimeTests=await File.ReadAllTextAsync(Path.Combine(root,"Tests","PlayMode","SecurityCombatTests.cs"));
        Assert.Contains("magazineContactFrames",runtimeTests);
        Assert.Contains("Support hand must reach the actual magazine",runtimeTests);
        Assert.DoesNotContain("Left hand during reload",runtimeTests);
    }
    [Fact]public async Task InstalledBridgeRuntimeVersionAndManifestCannotDrift()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"install",("project_path","Game")),default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCore","Editor");
        var source=await File.ReadAllTextAsync(Path.Combine(folder,"ForgeCoreBridge.cs"));
        var manifest=Directory.GetFiles(folder,"*.json").Single();
        using var document=JsonDocument.Parse(await File.ReadAllBytesAsync(manifest));
        Assert.Contains("private const string BridgeVersion = \""+document.RootElement.GetProperty("bridgeVersion").GetString()+"\";",source);
        Assert.DoesNotContain("__FORGECORE_BRIDGE_VERSION__",source);
    }
    [Fact]public async Task CombatKitKeepsProfileVersionBoundAndIsolatesGenreInputBindings()
    {
        await CreateBareUnityProjectAsync();
        using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var call=ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"kit",("project_path","Game"),("template_id","first_person_action"));
        Assert.True((await tools.ExecuteAsync(call,default)).Success);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        using var manifest=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"forgecore-game-templates.manifest.json")));
        var version=manifest.RootElement.GetProperty("version").GetString();
        var game=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","PrototypeGame.cs"));
        Assert.Contains("PlayerProfileTemplateKitVersion = \""+version+"\"",game);
        var input=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","PrototypeTypes.cs"));
        Assert.Contains("CombatBindings(Read(),Input.GetMouseButtonDown(0),Input.GetMouseButton(0))",input);
        Assert.Contains("Input.GetKey(KeyCode.Space)",input); // Other genres retain their original action.
        var player=await File.ReadAllTextAsync(Path.Combine(folder,"Runtime","SecurityCombatPlayer.cs"));
        Assert.Contains("PrototypeInput.ReadSecurityCombat()",player);
        Assert.Contains("controller.isGrounded",player);
        Assert.Contains("adsPosition",player);
        Assert.Contains("sightLocal",player);
        Assert.Contains("input.SprintHeld",player);
        var regression=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","SecurityCombatTests.cs"));
        Assert.Contains("GraphicsDeviceType.Null",regression);
        Assert.Contains("Assert.Ignore",regression);
        var missionBuilder=await File.ReadAllTextAsync(Path.Combine(folder,"Editor","ForgeCoreIndustrialMissionBuilder.cs"));
        Assert.Contains("var navigation=new SecurityGridPathfinder",missionBuilder);
        Assert.DoesNotContain("session.Navigation",missionBuilder); // Awake is not run by editor-time construction.
        var runtime=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","PlayMode","IndustrialMissionTests.cs"));
        Assert.Contains("ActualPlacedPropBlocksARealControllerWithoutMutatingItsPrefab",runtime);
        var campaigns=await File.ReadAllTextAsync(Path.Combine(folder,"Tests","EditMode","PrototypeCampaignTests.cs"));
        Assert.Contains("Cache path classification, never objects/assertion results",campaigns);
        Assert.Contains("Filter campaign filenames before opening",campaigns);
    }
}
