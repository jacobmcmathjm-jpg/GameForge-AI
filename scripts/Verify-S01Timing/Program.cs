using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

// Opt-in live diagnostic fixture, never the installed production project. No approval/rights records are created.
if(args.Length is not 4 and not 5)throw new ArgumentException("mode editor.exe editor-version existing-package-cache [--allow-legacy-visual-fixture] required");
var mode=args[0];var editor=Path.GetFullPath(args[1]);var editorVersion=args[2];var cache=Path.GetFullPath(args[3]);
if(mode is "profile-first" or "profile-repeat" && (args.Length!=5||args[4]!="--allow-legacy-visual-fixture"))
{
    Console.Error.WriteLine("This PR uses the older October prototype, not the approved muzzle/audio game. No player will launch without explicit --allow-legacy-visual-fixture.");
    Environment.ExitCode=2;return;
}
var repo=Environment.CurrentDirectory;var workspace=Path.Combine(repo,"work","s01-live-v1");var project=Path.Combine(workspace,"TestGame");
var receipts=Path.Combine(repo,"work","s01-evidence");Directory.CreateDirectory(receipts);
if(mode=="prepare")
{
    if(Directory.Exists(workspace)||File.Exists(workspace))throw new IOException("Fresh isolated fixture required; never overwrite.");
    var packages=new Dictionary<string,string>();
    foreach(var name in new[]{"com.unity.test-framework","com.unity.ext.nunit"})
    {
        var candidates=Directory.GetDirectories(cache,name+"@*");if(candidates.Length!=1)throw new IOException("Exactly one existing cached test package required: "+name);
        using var metadata=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(candidates[0],"package.json")));
        if(metadata.RootElement.GetProperty("name").GetString()!=name||(new DirectoryInfo(candidates[0]).Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Unexpected package identity/link.");
        packages.Add(name,"file:"+candidates[0].Replace('\\','/'));
    }
    foreach(var module in new[]{"audio","physics","imgui","jsonserialize","ui","uielements","animation","particlesystem","imageconversion"})packages.Add("com.unity.modules."+module,"1.0.0");
    Directory.CreateDirectory(Path.Combine(project,"Assets"));Directory.CreateDirectory(Path.Combine(project,"ProjectSettings"));Directory.CreateDirectory(Path.Combine(project,"Packages"));
    await WriteNew(Path.Combine(project,"ProjectSettings","ProjectVersion.txt"),Encoding.UTF8.GetBytes("m_EditorVersion: "+editorVersion+"\n"));
    await WriteNew(Path.Combine(project,"Packages","manifest.json"),JsonSerializer.SerializeToUtf8Bytes(new{dependencies=packages},Json));
    await Record("prepare",new{kind="isolated_live_fixture",project,editorVersion,packages,productionProjectModified=false,cacheState="not controlled"});
    return;
}
if(!File.Exists(Path.Combine(project,"ProjectSettings","ProjectVersion.txt")))throw new IOException("Prepare the isolated fixture first.");
if(mode=="validate-retained")
{
    var resultRoot=Path.Combine(project,".forgecore","unity","player-profile","results");var results=Directory.GetFiles(resultRoot,"*.json");
    if(results.Length!=2)throw new IOException("Expected exactly the two retained real player results.");
    var checkedResults=new List<object>();
    foreach(var path in results)
    {
        var bytes=File.ReadAllBytes(path);if(bytes.Length>1048576)throw new IOException("Unbounded result.");
        using var json=JsonDocument.Parse(bytes);var root=json.RootElement;var timing=root.GetProperty("timingDiagnostics");
        typeof(ToolExecutor).GetMethod("ValidateTimingJsonShape",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{timing});
        var report=timing.Deserialize<ForgeCore.Diagnostics.ProfileTimingReport>(new JsonSerializerOptions{IncludeFields=true})!;
        ForgeCore.Diagnostics.ProfileTimingCollector.Validate(report,root.GetProperty("durationSeconds").GetInt32(),root.GetProperty("sampledFrames").GetInt32(),
            root.GetProperty("averageFrameMilliseconds").GetDouble(),root.GetProperty("maximumFrameMilliseconds").GetDouble());
        checkedResults.Add(new{requestId=root.GetProperty("requestId").GetString(),sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),valid=true});
    }
    await Record("retained-readback",new{kind="read_only_real_result_revalidation",checkedResults,playersLaunched=0,rawEvidenceModified=false});return;
}
if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle; no existing process will be closed.");
if(mode=="bootstrap")
{
    var log=Path.Combine(receipts,"live-bootstrap.log");if(File.Exists(log)||File.Exists(Receipt("bootstrap")))throw new IOException("Bootstrap receipt/log already exists.");
    var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
    foreach(var a in new[]{"-batchmode","-quit","-projectPath",project,"-logFile",log})start.ArgumentList.Add(a);
    using var p=Process.Start(start)??throw new IOException("Unity did not start.");
    using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(6));
    try{await p.WaitForExitAsync(deadline.Token);}catch(OperationCanceledException){if(!p.HasExited)p.Kill(true);throw new IOException("Only owned bootstrap timed out; evidence retained.");}
    var packageLock=Path.Combine(project,"Packages","packages-lock.json");
    await Record("bootstrap",new{exitCode=p.ExitCode,packageLockPresent=File.Exists(packageLock),log,qualification="Fresh project import/setup, not player startup or performance evidence."});
    if(p.ExitCode!=0||!File.Exists(packageLock))throw new IOException("Unity bootstrap failed; inspect retained log.");return;
}
var guard=new WorkspacePathGuard(workspace);var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:editorVersion);
using var tools=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:editorVersion,unityTimeoutSeconds:360,enableUnityProjectPackages:true);
if(mode is "generate" or "generate-retry1")
{
    var attempt=mode=="generate"?"":"-retry1";
    await Run("install-bridge"+attempt,ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Install bridge only in new isolated timing fixture",("project_path","TestGame")));
    await Run("install-kit"+attempt,ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install timing candidate only in new isolated fixture",("project_path","TestGame"),("template_id","first_person_action")));
    await Run("compile"+attempt,Action("compile"));
    await Run("scene",Action("create_game_template",("scene_path","Assets/Scenes/Timing.unity"),("template_id","first_person_action"),("seed","83")));
    await Run("build",Action("build_windows",("scene_path","Assets/Scenes/Timing.unity"),("output_path","Builds/Timing/Timing.exe"),("development_build","true")));return;
}
if(mode is "profile-first" or "profile-repeat")
{
    await Record(mode+"-context",new{os=RuntimeInformation.OSDescription,logicalProcessors=Environment.ProcessorCount,
        concurrentUnityEditors=Process.GetProcessesByName("Unity").Length,concurrentBlenderProcesses=Process.GetProcessesByName("blender").Length,
        cacheState="not controlled; editor/build and prior driver caches may be warm",test=mode,
        qualification="Two ordered launches of the same new build, not cold-cache reproduction or production-freeze diagnosis."});
    await Run(mode,ToolCall.Create(ToolKind.UnityPlayerProfileTest,PermissionKind.Execute,"Collect actual bounded three-phase rendered timing on the isolated new fixture",
        ("project_path","TestGame"),("output_path","Builds/Timing/Timing.exe"),("duration_seconds","10")),allowMeasuredBudgetFailure:true);return;
}
throw new ArgumentException("Unknown bounded mode.");

ToolCall Action(string operation,params(string,string)[] values)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"Isolated S01 diagnostic: "+operation,new[]{("project_path","TestGame"),("operation",operation)}.Concat(values).ToArray());
string Receipt(string name)=>Path.Combine(receipts,"live-"+name+".json");
async Task Record(string name,object value)=>await WriteNew(Receipt(name),JsonSerializer.SerializeToUtf8Bytes(value,Json));
async Task Run(string name,ToolCall call,bool allowMeasuredBudgetFailure=false)
{
    if(File.Exists(Receipt(name)))throw new IOException("Receipt exists; do not replay an ambiguous action: "+name);
    var check=await policy.ValidateAsync(call,default);if(check.Decision==SecurityDecision.Denied)throw new IOException(check.Reason);
    Console.WriteLine("START "+name);var result=await tools.ExecuteAsync(call,default);
    await Record(name,new{call.Kind,call.Arguments,result.Success,result.Output,result.Error});
    if(!result.Success)
    {
        if(allowMeasuredBudgetFailure&&result.Error?.Contains("baseline budget failed",StringComparison.Ordinal)==true){Console.WriteLine("MEASURED_BUDGET_FAILURE "+result.Error);return;}
        throw new IOException("Diagnostic step failed; retain evidence: "+name+" "+result.Error?.Split('\n')[0]);
    }
    Console.WriteLine(result.Output);
}
static async Task WriteNew(string path,byte[] bytes){using var f=new FileStream(path,FileMode.CreateNew);await f.WriteAsync(bytes);}
// Property rather than mutable global settings: no app configuration, credentials, models or SQLite are read.
partial class Program { private static JsonSerializerOptions Json => new(){WriteIndented=true}; }
