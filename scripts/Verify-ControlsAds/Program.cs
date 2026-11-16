using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

// Bounded regression/build harness. Uses the production kit/tool/policy route, not new provider calls.
const string workspace=@"D:\ForgeCore\Workspace", project="Games/ForgeCore";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
const string scene="Assets/Scenes/S04-S05-ControlsAds-v2.unity", id="s04-s05-controls-ads-v2";
const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
var evidence=Path.Combine(Environment.CurrentDirectory,"work","controls-ads-verification");
Directory.CreateDirectory(evidence);
var guard=new WorkspacePathGuard(workspace);
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1");
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true);
try {
    if(args.Length!=1)throw new ArgumentException("Choose --install, --create, --tests, --build or --startup.");
    if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Close Unity Editor before running the isolated batch operation.");
    if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard asset changed.");
    if(await Hash("Assets/Scenes/S06-SecurityCombat-v10.unity")!="c5ae2da5972d4b97adb8a28e40edaa9ecad097372967b96963483090c07865c7")throw new IOException("Previous scene changed.");
    switch(args[0]) {
        case "--install":
        case "--install-final":
        case "--install-renderguard":
            var suffix=args[0]=="--install-renderguard"?"-renderguard":args[0]=="--install-final"?"-final":"";
            await Run("install"+suffix,ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install controls/ADS kit with exact protected prior-version backup",("project_path",project),("template_id","first_person_action")));
            await Run("compile"+suffix,Action("compile"));break;
        case "--create":
        case "--create-v2":
            if(File.Exists(guard.Resolve(project+"/"+scene,true)))throw new IOException("Fresh scene already exists; do not replay creation.");
            await Run("create-v2",Action("create_security_combat_lab",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id",id),("scene_path",scene),("output_path","Previews/S04-S05-ControlsAds-v2.png")));break;
        case "--tests":
        case "--tests-v2":
            await Run("edit-tests-v2",Action("run_tests",("test_mode","edit")));
            await Run("play-tests-v2",Action("run_tests",("test_mode","play")));break;
        case "--build":
            await Run("build",Action("build_windows",("scene_path",scene),("output_path","Builds/S04-S05-ControlsAds-v2/S04-S05-ControlsAds-v2.exe"),("development_build","true")));break;
        case "--smoke":
            await Run("smoke",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Bounded headless player startup with verified provenance, not an interactive playtest",("project_path",project),("output_path","Builds/S04-S05-ControlsAds-v2/S04-S05-ControlsAds-v2.exe"),("duration_seconds","5")));break;
        case "--render-tests":
        case "--render-sight-only":
            // Supplement the production -nographics suite with a real graphics-device run.
            // Never describe Null-device captures as visual evidence.
            var renderStep=args[0]=="--render-sight-only"?"rendered-sight":"rendered-playmode";
            var xml=Path.Combine(evidence,renderStep+".xml");var renderLog=Path.Combine(evidence,renderStep+".log");
            if(File.Exists(xml)||File.Exists(renderLog))throw new IOException("Rendered test evidence exists; do not replay.");
            var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=guard.Resolve(project,false)};
            foreach(var argument in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",renderLog})start.ArgumentList.Add(argument);
            if(args[0]=="--render-sight-only"){start.ArgumentList.Add("-testFilter");start.ArgumentList.Add("ForgeCoreGameTemplates.Tests.SecurityCombatTests.CaptureActualHipAndDownSightCameraEvidence");}
            using(var rendering=Process.Start(start)) {
                if(rendering==null)throw new IOException("Could not start rendered test runner.");
                using var bound=new CancellationTokenSource(TimeSpan.FromMinutes(10));
                await rendering.WaitForExitAsync(bound.Token);
                var document=XDocument.Load(xml);var testRoot=document.Root!;
                await Save(renderStep,new{exitCode=rendering.ExitCode,attributes=testRoot.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
                if(rendering.ExitCode!=0||testRoot.Attribute("result")?.Value!="Passed"||testRoot.Attribute("failed")?.Value!="0")throw new IOException("Rendered PlayMode suite failed; inspect preserved XML/log.");
            }
            Console.WriteLine("Rendered PlayMode suite passed: "+xml);break;
        case "--audit":
            using(var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(guard.Resolve(project+"/.forgecore/unity/character-animation-evidence/6dd7c29164e04351988b1be3a6643676.json",false)))) {
                var r=receipt.RootElement;var count=0;
                foreach(var entry in r.GetProperty("artifacts").EnumerateArray()) {
                    if(await Hash(entry.GetProperty("path").GetString()!)!=entry.GetProperty("sha256").GetString())throw new IOException("Generated artifact changed: "+entry.GetProperty("path").GetString());
                    count++;
                }
                if(await Hash(scene)!=r.GetProperty("sceneSha256").GetString())throw new IOException("Fresh scene changed after generation.");
                if(await Hash("Assets/ForgeCoreGameTemplates/forgecore-game-templates.manifest.json")!=r.GetProperty("templateKitManifestSha256").GetString())throw new IOException("Kit manifest changed.");
                using var baseline=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(Environment.CurrentDirectory,"outputs","ForgeCore-CapabilityShowcase-20261028","baseline.json")));
                var preserved=0;
                foreach(var entry in baseline.RootElement.GetProperty("entries").EnumerateObject()) {
                    var relative=entry.Name.Replace('\\','/');
                    if(relative.StartsWith(project+"/Assets/ForgeCoreGameTemplates/",StringComparison.Ordinal))continue;
                    var bytes=await File.ReadAllBytesAsync(guard.Resolve(relative,false));
                    if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=entry.Value.GetString())throw new IOException("Historical baseline changed: "+relative);
                    preserved++;
                }
                await Save("audit",new{generatedArtifacts=count,historicalFilesPreserved=preserved,sceneSha256=await Hash(scene),templateKitManifestSha256=r.GetProperty("templateKitManifestSha256").GetString()});
                Console.WriteLine("AUDIT "+count+" generated artifacts and "+preserved+" historical files preserved.");
            }
            break;
        case "--startup":
            var executable=guard.Resolve(project+"/Builds/S04-S05-ControlsAds-v2/S04-S05-ControlsAds-v2.exe",false);
            var log=Path.Combine(evidence,"startup.log");if(File.Exists(log))throw new IOException("Startup evidence already exists.");
            using(var process=Process.Start(new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,Arguments="-batchmode -nographics -quit -logFile \""+log+"\""})) {
                if(process==null||!process.WaitForExit(60000))throw new IOException("Startup did not exit within its bound; inspect the owned process before continuing.");
                if(process.ExitCode!=0)throw new IOException("Player startup failed: "+process.ExitCode);
            }
            var content=File.ReadAllText(log);
            if(content.Contains("Exception:")||content.Contains("NullReferenceException")||content.Contains("error CS"))throw new IOException("Player log reports an exception.");
            await Save("startup",new{success=true,qualification="Headless startup/exit only; not a human playtest.",scene,sceneSha256=await Hash(scene)});
            break;
        default:throw new ArgumentException("Unknown mode.");
    }
    Console.WriteLine("CONTROLS_ADS_STEP_COMPLETE "+args[0]);
}catch(Exception error){Console.Error.WriteLine("CONTROLS_ADS_STOPPED "+error.GetType().Name+": "+string.Join(' ',error.Message.Split('\n').Take(4)));Environment.ExitCode=1;}
ToolCall Action(string op,params(string Key,string Value)[] fields)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,op,new[]{("project_path",project),("operation",op)}.Concat(fields).ToArray());
async Task<string> Hash(string path)=>Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(guard.Resolve(project+"/"+path,false)))).ToLowerInvariant();
async Task Save(string step,object value){
    await using var stream=new FileStream(Path.Combine(evidence,step+".json"),FileMode.CreateNew);
    await JsonSerializer.SerializeAsync(stream,value,new JsonSerializerOptions{WriteIndented=true});
}
async Task Run(string step,ToolCall call){
    if(File.Exists(Path.Combine(evidence,step+".json")))throw new IOException("Receipt exists; do not replay "+step);
    var decision=await policy.ValidateAsync(call,default);if(decision.Decision==SecurityDecision.Denied)throw new InvalidOperationException(decision.Reason);
    Console.WriteLine("START "+step);var result=await executor.ExecuteAsync(call,default);
    await Save(step,new{call.Kind,call.Arguments,result.Success,result.Output,result.Error});
    Console.WriteLine(result.Success?result.Output:result.Error);
    if(!result.Success)throw new IOException("Production tool failed at "+step);
}
