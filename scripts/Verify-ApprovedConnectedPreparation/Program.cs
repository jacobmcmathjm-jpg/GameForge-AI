using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
// Exactly one preparation explicitly approved by the user in this chat; no stored app approval is forged.
const string project="Games/ForgeCore",id="connected-host-proof-v1",scene="Assets/Scenes/Indie/Industrial-Connected-HostProof-v1.unity",preview="Previews/Indie/Industrial-Connected-HostProof-v1.png";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
var guard=new WorkspacePathGuard(@"D:\ForgeCore\Workspace");var root=guard.Resolve(project,false);
var receipt=Path.Combine(Environment.CurrentDirectory,"work","connected-host-validation","approved-preparation-v1.json");
if(args.Length>1||args.Length==1&&args[0]!="--verify-existing")throw new ArgumentException("Only the read-only --verify-existing follow-through is supported.");
if(args.Length==1){await VerifyPrepared();return;}
if(File.Exists(receipt))throw new IOException("One-shot receipt exists; no replay.");
foreach(var relative in new[]{scene,preview,preview+".forgecore-rig-preview.json","Assets/ForgeCore/CharacterAnimations/"+id})if(File.Exists(Path.Combine(root,relative))||Directory.Exists(Path.Combine(root,relative)))throw new IOException("Fresh approved destination required.");
var before=new Dictionary<string,string>();
foreach(var relative in new[]{"Assets/Scenes/Indie/Industrial-Connected-v8.unity","Assets/ForgeCoreGameTemplates/forgecore-game-templates.manifest.json","Previews/Indie/Runtime-Connected-v8.png"})before[relative]=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(root,relative)))).ToLowerInvariant();
var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"One fresh connected preparation explicitly approved in chat; no v8 overwrite or paid/provider operation",("project_path",project),("operation","create_industrial_mission"),("asset_path","Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb"),("expected_sha256","ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0"),("asset_set_id",id),("layout_profile",ConnectedIndustrialProfile.Id),("scene_path",scene),("output_path",preview));
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1");var decision=await policy.ValidateAsync(call,default);if(decision.Decision==SecurityDecision.Denied)throw new UnauthorizedAccessException(decision.Reason);
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true);
Console.WriteLine("START explicitly approved one-shot preparation through current ToolExecutor");
var result=await executor.ExecuteAsync(call,default);
var unchanged=before.All(p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,p.Key)))).ToLowerInvariant()==p.Value);
Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
await using(var output=new FileStream(receipt,FileMode.CreateNew))await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,explicitChatApproval=true,scope="one_fresh_connected_preparation_no_v8_overwrite",requestId=call.Id,result.Success,result.Output,result.Error,v8SceneKitRuntimePreviewUnchanged=unchanged,providerOrModelStarted=false,normalAppStarted=false,liveDatabaseOpened=false,humanArtAccepted=false,stageComplete=false},new JsonSerializerOptions{WriteIndented=true});
if(!result.Success||!unchanged)throw new IOException("Approved preparation failed or protected v8 inputs changed; receipt retained.");
await VerifyPrepared();
async Task VerifyPrepared(){
    using var completed=JsonDocument.Parse(await File.ReadAllBytesAsync(receipt));
    if(!completed.RootElement.GetProperty("Success").GetBoolean()||!completed.RootElement.GetProperty("v8SceneKitRuntimePreviewUnchanged").GetBoolean())throw new IOException("Successful original one-shot result required; never replay.");
    var records=new List<(string Path,JsonElement Value)>();
    foreach(var path in Directory.EnumerateFiles(Path.Combine(root,".forgecore","unity","character-animation-evidence"),"*.json")){
        if(new FileInfo(path).Length>2*1024*1024)continue;using var json=JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        if(json.RootElement.GetProperty("scenePath").GetString()==scene)records.Add((path,json.RootElement.Clone()));
    }
    if(records.Count!=1)throw new IOException("One exact new preparation evidence record required.");var e=records[0].Value;
    ConnectedInteriorEvidence.ValidateDetail(e.GetProperty("connectedInteriorReport"));ConnectedInteriorEvidence.ValidateComparison(e.GetProperty("connectedInteriorComparisonReport"));
    if(e.GetProperty("templateKitVersion").GetString()!="0.16.107"||e.GetProperty("selectedLayout").GetString()!=ConnectedIndustrialProfile.Id||e.GetProperty("stageComplete").GetBoolean())throw new IOException("Prepared evidence identity differs.");
    foreach(var artifact in e.GetProperty("artifacts").EnumerateArray())if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(root,artifact.GetProperty("path").GetString()!)))).ToLowerInvariant()!=artifact.GetProperty("sha256").GetString())throw new IOException("New preparation artifact changed.");
    var expected=new Dictionary<string,string>{{"Assets/Scenes/Indie/Industrial-Connected-v8.unity","7a9398059a6af4c941013100922fde813f1ce0531672f62a77038792d4efba41"},{"Assets/ForgeCoreGameTemplates/forgecore-game-templates.manifest.json","696e943ed5f2f6649cd72b37d32d91983278401ada5447f5ee6b6b1abd691668"},{"Previews/Indie/Runtime-Connected-v8.png","1336b86a1a21365ca4971330500da0c6ad734fdef4e67ac20b351ac54dfc5268"}};
    foreach(var pair in expected)if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(root,pair.Key)))).ToLowerInvariant()!=pair.Value)throw new IOException("Protected v8 input differs.");
    var verification=Path.Combine(Path.GetDirectoryName(receipt)!,"approved-preparation-v1-verified.json");
    if(File.Exists(verification))throw new IOException("Verification receipt exists; no overwrite.");
    await using(var output=new FileStream(verification,FileMode.CreateNew))await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,originalToolCallId=completed.RootElement.GetProperty("requestId").GetString(),unityRequestId=e.GetProperty("requestId").GetString(),evidencePath=records[0].Path,artifactsVerified=e.GetProperty("artifacts").GetArrayLength(),validatedInteriorReportRecorded=true,validatedComparisonReportRecorded=true,v8SceneKitRuntimePreviewUnchanged=true,readOnlyFollowThrough=true,preparationReplayed=false,humanArtAccepted=false,stageComplete=false},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine("APPROVED_PREPARATION_PASS current writer recorded validated interior/comparison reports; v8/kit/runtime preview unchanged. Evidence "+e.GetProperty("requestId").GetString());
}
