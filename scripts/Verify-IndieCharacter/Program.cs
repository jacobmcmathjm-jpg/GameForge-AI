using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
// Explicit production policy/executor experiment. Fresh generated scene only; no provider/API, review decision, normal saves or SQLite.
const string project="Games/ForgeCore", scene="Assets/Scenes/Indie/Industrial-Indie-Character-v1.unity";
const string player="Builds/Indie-Character-20261116/ForgeCore-IndieCharacter.exe";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
var guard=new WorkspacePathGuard(@"D:\ForgeCore\Workspace");
var evidence=Path.Combine(Environment.CurrentDirectory,"work","indie-character");
Directory.CreateDirectory(evidence);
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1");
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true);
try {
    if(args.Length!=1)throw new ArgumentException("One bounded mode required.");
    switch(args[0]) {
        case "audit-before":await Audit(false);break;
        case "install":
            if(!File.Exists(Path.Combine(evidence,"preservation-before.json")))throw new IOException("Audit first.");
            await Run("install-kit-v99",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Exact protected .98 to .99 repair of PlayMode test assembly reference; preserve prior failed receipt and all old scenes, clips, audio and approvals",("project_path",project),("template_id","first_person_action")));break;
        case "compile":await Run("compile-v99",Action("compile"));break;
        case "generate":await Run("generate-character-v1",Action("create_industrial_mission",("asset_path","Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb"),("expected_sha256","ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0"),("asset_set_id","indie-character-v1"),("scene_path",scene),("output_path","Previews/Indie/Industrial-Indie-Character-v1.png")));break;
        case "capture":await Run("capture-character-v1",Action("capture_scene_preview",("scene_path",scene),("output_path","Previews/Indie/Runtime-Character-v1.png"),("duration_seconds","2")));break;
        case "capture-v2":await Run("capture-hero-v2",Action("capture_scene_preview",("scene_path",scene),("output_path","Previews/Indie/Runtime-Character-v2.png"),("duration_seconds","2")));break;
        case "capture-quiet":await Run("capture-hero-quiet-v1",Action("capture_scene_preview",("scene_path",scene),("output_path","Previews/Indie/Runtime-Character-Quiet-v1.png"),("duration_seconds","2")));break;
        case "build":
            if(!File.Exists(Path.Combine(evidence,"generate-character-v1.json")))throw new IOException("Generate first.");
            if(Directory.Exists(guard.Resolve(project+"/Builds/Indie-Character-20261116",false)))throw new IOException("Fresh build destination required.");
            await Run("build-v99",Action("build_windows",("scene_path",scene),("output_path",player),("development_build","true")));break;
        case "edit-tests":await Run("edit-tests-v99",Action("run_tests",("test_mode","edit")));break;
        case "edit-tests-quiet":await Run("edit-tests-v98-quiet-v1",Action("run_tests",("test_mode","edit")));break;
        case "audit-after":await Audit(true);break;
        case "audit-quiet":await Audit(true,"preservation-quiet-v1");break;
        default:throw new ArgumentException("Unknown mode.");
    }
}catch(Exception ex){Console.Error.WriteLine("CURRENT_TIMING_STOPPED "+ex.GetType().Name+": "+ex.Message.Split('\n')[0]);Environment.ExitCode=1;}
ToolCall Action(string operation,params(string Key,string Value)[] fields)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,operation,new[]{("project_path",project),("operation",operation)}.Concat(fields).ToArray());
async Task<string> Hash(string relative)=>Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(guard.Resolve(project+"/"+relative,false)))).ToLowerInvariant();
async Task Write(string name,object value){await using var s=new FileStream(Path.Combine(evidence,name+".json"),FileMode.CreateNew);await JsonSerializer.SerializeAsync(s,value,new JsonSerializerOptions{WriteIndented=true});}
async Task Run(string name,ToolCall call){
    var path=Path.Combine(evidence,name+".json");
    if(File.Exists(path)){using var prior=JsonDocument.Parse(await File.ReadAllBytesAsync(path));if(prior.RootElement.GetProperty("Success").GetBoolean()){Console.WriteLine("Already succeeded; no replay "+name);return;}throw new IOException("Failed receipt retained; inspect before a fresh attempt.");}
    var check=await policy.ValidateAsync(call,default);if(check.Decision==SecurityDecision.Denied)throw new IOException(check.Reason);
    Console.WriteLine("START "+name);var result=await executor.ExecuteAsync(call,default);
    await Write(name,new{call.Kind,call.Arguments,result.Success,result.Output,result.Error});
    Console.WriteLine(result.Success?result.Output:result.Error?.Split('\n')[0]);if(!result.Success)throw new IOException("Production tool failed: "+name);
}
async Task Audit(bool after,string? resultName=null){
    var files=new SortedDictionary<string,string>(StringComparer.Ordinal);
    foreach(var id in new[]{"1b32758cf81b442dbee891d372b95752","c9eda7a1c44c4ea29e5b5d12c442b425","51e77f866d8d4337af0102a2a3c4233b","e54ee23f4fa94872abe31a0eae862e9b"}){
        var path=".forgecore/unity/character-animation-evidence/"+id+".json";
        using var doc=JsonDocument.Parse(await File.ReadAllBytesAsync(guard.Resolve(project+"/"+path,false)));
        files[path]=await Hash(path);var r=doc.RootElement;
        foreach(var a in r.GetProperty("artifacts").EnumerateArray()){var rel=a.GetProperty("path").GetString()!;var hash=await Hash(rel);if(hash!=a.GetProperty("sha256").GetString())throw new IOException("Historical artifact changed: "+rel);files[rel]=hash;}
        foreach(var pair in new[]{("scenePath","sceneSha256"),("assetPath","assetSha256"),("previewPath","previewSha256")}){
            var rel=r.GetProperty(pair.Item1).GetString()!;var hash=await Hash(rel);if(hash!=r.GetProperty(pair.Item2).GetString())throw new IOException("Historical evidence target changed: "+rel);files[rel]=hash;}
    }
    files["Assets/ForgeCoreGameTemplates/Runtime/Resources/ForgeCoreSoftWarmFeedback.shader"]=await Hash("Assets/ForgeCoreGameTemplates/Runtime/Resources/ForgeCoreSoftWarmFeedback.shader");
    // Review/approval records are hashed, never parsed or changed. Databases are never opened.
    var privateRoot=guard.Resolve(project+"/.forgecore",false);
    foreach(var f in Directory.EnumerateFiles(privateRoot,"*.json",SearchOption.AllDirectories).Where(f=>f.Contains("review",StringComparison.OrdinalIgnoreCase)||f.Contains("approval",StringComparison.OrdinalIgnoreCase))){
        var rel=Path.GetRelativePath(guard.Resolve(project,false),f).Replace('\\','/');files[rel]=await Hash(rel);
    }
    if(after){using var prior=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"preservation-before.json")));var old=JsonSerializer.Deserialize<SortedDictionary<string,string>>(prior.RootElement.GetProperty("files").GetRawText())!;
        if(old.Count!=files.Count||old.Any(pair=>!files.TryGetValue(pair.Key,out var hash)||hash!=pair.Value))throw new IOException("Protected artifact or review set differs from before.");}
    await Write(resultName??(after?"preservation-after":"preservation-before"),new{schemaVersion=1,files,artAccepted=false,freezeFixed=false});
    Console.WriteLine("PRESERVATION_PASS "+files.Count+" exact hashes");
}
