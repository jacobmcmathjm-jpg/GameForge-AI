using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Fact]public async Task DemoKitEmbedsExactTestedSharedRulesAndDoesNotReplaceSourceReviews()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        Assert.True((await tools.ExecuteAsync(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"demo foundation",("project_path","Game"),("template_id","first_person_action")),default)).Success);
        var kit=Path.Combine(_root,"Game","Assets","ForgeCoreGameTemplates");
        foreach(var name in new[]{"DemoState","DemoAudioSynthesis","DemoFrameWindow","RecordedFootstepBank","DemoWorkJournal","DemoCheckpointQueue"}) {
            using var embedded=typeof(ToolExecutor).Assembly.GetManifestResourceStream("ForgeCore.Tools."+name+".cs");Assert.NotNull(embedded);
            using var reader=new StreamReader(embedded);Assert.Equal(await reader.ReadToEndAsync(),await File.ReadAllTextAsync(Path.Combine(kit,"Runtime",name+".cs")));
        }
        var options=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","SecurityDemoOptions.cs"));
        Assert.Contains("FileMode.CreateNew",options);Assert.Contains("File.Replace(temporary,path,backup)",options);Assert.Contains("FileAttributes.ReparsePoint",options);
        Assert.Contains("CheckpointValid(data)",options);Assert.Contains("Physics.CheckCapsule",options);Assert.Contains("ForgeCoreDemoProbe-",options);
        var probe=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","DemoAcceptanceProbe.cs"));
        Assert.Contains("#if DEVELOPMENT_BUILD && !UNITY_EDITOR",probe);Assert.Contains("-forgecore-demo-probe",probe);Assert.Contains("240s bounded probe deadline",probe);
        Assert.DoesNotContain(".Damage(",probe);Assert.DoesNotContain("transform.position=",probe);Assert.Contains("checkpointRoundtrip",probe);
        Assert.Contains("session.options.GetComponent<DemoRuntimeTelemetry>()",probe);
        var telemetry=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","DemoRuntimeTelemetry.cs"));Assert.Contains("GetComponentInChildren<SecurityCombatSession>()",telemetry);
        var builder=await File.ReadAllTextAsync(Path.Combine(kit,"Editor","ForgeCoreIndustrialMissionBuilder.cs"));
        Assert.Contains("objectivePlaceholderSpheres=0",builder);Assert.Contains("approvedAssetsPlaced=false",builder);
        var audio=await File.ReadAllTextAsync(Path.Combine(kit,"Editor","ForgeCoreDemoAudioBuilder.cs"));Assert.Contains("humanListeningAccepted=false",audio);
        Assert.Contains("AudioCompressionFormat.PCM",audio);Assert.Contains("FileMode.CreateNew",audio);
        Assert.Contains("DemoAudioSynthesis.ClipKeys",audio);Assert.Contains("target.variationClips",audio);
        var playback=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","DemoAudioController.cs"));Assert.Contains("DemoFootstepClock",playback);
        Assert.Contains("guardControllers[i].isGrounded",playback);Assert.Contains("DemoAudioSurface.Under",playback);Assert.Contains("AudioRolloffMode.Logarithmic",playback);
        var player=await File.ReadAllTextAsync(Path.Combine(kit,"Runtime","SecurityCombatPlayer.cs"));Assert.Contains("PrototypeFeedback.Fire(muzzle,forward,false)",player);
    }
}
