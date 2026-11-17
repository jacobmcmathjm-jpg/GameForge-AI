using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
// Explicit production policy/executor experiment. Fresh generated scene only; no provider/API, review decision, normal saves or SQLite.
const string project="Games/ForgeCore", scene="Assets/Scenes/Indie/Industrial-Connected-Encounters-v1.unity";
const string player="Builds/Connected-Encounters-20261117-V1/ForgeCore-Connected.exe";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
var guard=new WorkspacePathGuard(@"D:\ForgeCore\Workspace");
var evidence=Path.Combine(Environment.CurrentDirectory,"work","connected-encounters-v1");
Directory.CreateDirectory(evidence);
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1");
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true);
try {
    if(args.Length!=1)throw new ArgumentException("One bounded mode required.");
    switch(args[0]) {
        case "audit-before":await Audit(false);break;
        case "install-bridge":await Run("install-bridge-v23",ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Exact hash-verified protected bridge .22 to .23 layout-profile protocol addition",("project_path",project)));break;
        case "install":
            if(!File.Exists(Path.Combine(evidence,"preservation-before.json")))throw new IOException("Audit first.");
            await Run("install-kit-v108",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Exact protected .107 to .108 opt-in indoor encounter diagnostics and regression tests; gameplay, audio, flash and v8 unchanged",("project_path",project),("template_id","first_person_action")));break;
        case "compile":await Run("compile-v108",Action("compile"));break;
        case "generate":await Run("generate-encounters-v1",Action("create_industrial_mission",("asset_path","Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb"),("expected_sha256","ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0"),("asset_set_id","connected-encounters-v1"),("layout_profile",ConnectedIndustrialProfile.Id),("scene_path",scene),("output_path","Previews/Indie/Industrial-Connected-Encounters-v1.png")));break;
        case "capture":await Run("capture-encounters-v1",Action("capture_scene_preview",("scene_path",scene),("output_path","Previews/Indie/Runtime-Connected-Encounters-v1.png"),("duration_seconds","2")));break;
        case "build":
            if(!File.Exists(Path.Combine(evidence,"generate-encounters-v1.json")))throw new IOException("Generate first.");
            if(Directory.Exists(guard.Resolve(project+"/Builds/Connected-Encounters-20261117-V1",false)))throw new IOException("Fresh build destination required.");
            await Run("build-v108",Action("build_windows",("scene_path",scene),("output_path",player),("development_build","true")));break;
        case "edit-tests":await Run("edit-tests-v108",Action("run_tests",("test_mode","edit")));break;
        case "verify-generation":await VerifyGeneration();break;
        case "audit-after":await Audit(true,"preservation-encounters-after");break;
        case "audit-final":await Audit(true,"preservation-encounters-final");break;
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
    foreach(var id in new[]{"1b32758cf81b442dbee891d372b95752","c9eda7a1c44c4ea29e5b5d12c442b425","51e77f866d8d4337af0102a2a3c4233b","e54ee23f4fa94872abe31a0eae862e9b","4df0dd6362d7483da7f403fd222025bb","6afce20456b9485b885c1c787379884e","e391533b504e4092aa5c8a6992e66e15","7acef7c69bd24e10859a60e368cc60ea","9dd9ec55c81849d58908efbc4d4d01be","206538edb1b74947b1941a2bea066816"}){
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
async Task VerifyGeneration(){
    var records=new List<(string Path,JsonElement Value)>();
    foreach(var file in Directory.EnumerateFiles(guard.Resolve(project+"/.forgecore/unity/character-animation-evidence",false),"*.json")){
        if(new FileInfo(file).Length>2*1024*1024)continue;
        using var doc=JsonDocument.Parse(await File.ReadAllBytesAsync(file));
        if(doc.RootElement.GetProperty("scenePath").GetString()==scene)records.Add((file,doc.RootElement.Clone()));
    }
    if(records.Count!=1)throw new IOException("One exact fresh scene evidence record required.");var current=records[0].Value;
    if(current.GetProperty("templateKitVersion").GetString()!="0.16.108"||current.GetProperty("selectedLayout").GetString()!=ConnectedIndustrialProfile.Id||current.GetProperty("stageComplete").GetBoolean())throw new IOException("Wrong generation identity or acceptance claim.");
    ConnectedInteriorEvidence.ValidateDetail(current.GetProperty("connectedInteriorReport"));ConnectedInteriorEvidence.ValidateComparison(current.GetProperty("connectedInteriorComparisonReport"));
    var artifacts=current.GetProperty("artifacts").EnumerateArray().ToArray();if(artifacts.Length!=306)throw new IOException("Unexpected artifact count.");
    foreach(var artifact in artifacts){var rel=artifact.GetProperty("path").GetString()!;if(await Hash(rel)!=artifact.GetProperty("sha256").GetString()||new FileInfo(guard.Resolve(project+"/"+rel,false)).Length!=artifact.GetProperty("bytes").GetInt64())throw new IOException("Prepared artifact changed: "+rel);}
    using var old=JsonDocument.Parse(await File.ReadAllBytesAsync(guard.Resolve(project+"/.forgecore/unity/character-animation-evidence/9dd9ec55c81849d58908efbc4d4d01be.json",false)));
    var previous=old.RootElement.GetProperty("artifacts").EnumerateArray().ToDictionary(a=>Path.GetFileName(a.GetProperty("path").GetString()!),a=>a.GetProperty("sha256").GetString()!,StringComparer.Ordinal);
    var inherited=artifacts.Where(a=>{var name=Path.GetFileName(a.GetProperty("path").GetString()!);return name.EndsWith(".anim",StringComparison.Ordinal)||name.EndsWith(".wav",StringComparison.Ordinal)||name=="FirstPersonArms.asset";}).ToArray();
    if(inherited.Length!=43||inherited.Any(a=>!previous.TryGetValue(Path.GetFileName(a.GetProperty("path").GetString()!),out var hash)||hash!=a.GetProperty("sha256").GetString()))throw new IOException("Inherited audio, motion or original-arm payload differs from v8.");
    await Write("generation-verified",new{schemaVersion=1,evidencePath=records[0].Path,artifactCount=artifacts.Length,everyArtifactHashAndLengthVerified=true,inheritedAudioClipOriginalArmFiles=inherited.Length,matchingV8=true,humanArtAccepted=false,humanBalanceAccepted=false,stageComplete=false});
    Console.WriteLine("GENERATION_PASS 306 exact artifacts; 43 inherited payloads match v8");
}
