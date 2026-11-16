using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
using ForgeCore.LocalAI;
try { await ExecuteStageAsync(args); }
catch(Exception error) {
 Console.Error.WriteLine("Validation failed cleanly: "+error.GetType().Name);
 Console.Error.WriteLine(string.Join('\n',error.Message.Split('\n').Take(12)));
 Environment.ExitCode=1;
}
static async Task ExecuteStageAsync(string[] args) {
if(args.Length==1&&args[0]=="--self-test-failure")throw new InvalidOperationException("Synthetic harness failure: no Unity, Blender, project or store operation was run.");
const string project="Games/ForgeCore", name="s04-industrial-security-v1";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
const string blender=@"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
var guard=new WorkspacePathGuard(@"D:\ForgeCore\Workspace");
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",blenderPath:blender);
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true,blenderPath:blender,blenderTimeoutSeconds:600);
async Task<string> Run(ToolCall call){var p=await policy.ValidateAsync(call,default);if(p.Decision==SecurityDecision.Denied)throw new InvalidOperationException(p.Reason);Console.WriteLine("START "+call.Description);var r=await executor.ExecuteAsync(call,default);if(!r.Success)throw new InvalidOperationException(r.Error);Console.WriteLine(r.Output);return r.Output;}
ToolCall Action(string op,params(string Key,string Value)[] fields)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,op,new[]{("project_path",project),("operation",op)}.Concat(fields).ToArray());
if(args.Length!=1)throw new ArgumentException("mode");
if(args[0]=="--reuse") {
 var receipt=await Run(ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Reverify exact S04 source cache without Blender launch",("project_path",project),("asset_name",name),("recipe",BlenderAssetSpec.SecurityCharacterRecipe),("seed","41"),("height_mm","1800")));
 using var check=JsonDocument.Parse(receipt);if(!check.RootElement.GetProperty("reused").GetBoolean())throw new InvalidOperationException("Expected immutable exact-source reuse.");return;
}
if(args[0]=="--workflow") {
 const string prompt="Create a security combat lab in Blender. Name it s06-prompt-combat-v1.";
 await using var model=new BuiltInBlenderAssetModel(new NoFallback(),project);var observations=new List<ToolObservation>();
 for(var turn=0;turn<9;turn++){
  var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12),default);
  if(plan.Actions.Count==0){if(observations.Count!=7)throw new InvalidOperationException("Incomplete route: "+plan.Response);File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"workflow-receipt.json"),JsonSerializer.Serialize(new{prompt,observations,plan.Response},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PROMPT_ROUTE_COMPLETE "+plan.Response);return;}
  var call=plan.Actions.Single();var output=await Run(call);observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,output));
 }throw new InvalidOperationException("Workflow exceeded its bound.");
}
if(args[0]=="--create"){
 var receipt=await Run(ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Create original industrial security character",("project_path",project),("asset_name",name),("recipe",BlenderAssetSpec.SecurityCharacterRecipe),("seed","41"),("height_mm","1800")));
 File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"create-receipt.json"),receipt);using var doc=JsonDocument.Parse(receipt);
 await Run(ToolCall.Create(ToolKind.ImportBlenderAsset,PermissionKind.Write,"Import fresh S04 exact source",("project_path",project),("asset_name",name),("bundle_sha256",doc.RootElement.GetProperty("bundleSha256").GetString()!),("expected_asset_sha256",doc.RootElement.GetProperty("assetSha256").GetString()!)));return;}
if(args[0]=="--tests"){foreach(var mode in new[]{"edit","play"})await Run(Action("run_tests",("test_mode",mode)));return;}
if(args[0]=="--play-tests"){await Run(Action("run_tests",("test_mode","play")));return;}
if(args[0]=="--install"){await Run(ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Install exact bridge with protected backup",("project_path",project)));await Run(ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install exact kit with protected backup",("project_path",project),("template_id","first_person_action")));await Run(Action("compile"));return;}
var asset=$"Assets/ForgeCore/Generated/Blender/{name}/{name}.glb";
var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(guard.Resolve(project+"/"+asset,false)))).ToLowerInvariant();
if(args[0]=="--animate"){await Run(Action("prepare_character_animation",("asset_path",asset),("expected_sha256",hash),("asset_set_id",name+"-motion"),("scene_path","Assets/Scenes/S04-SecurityReview-v1.unity"),("output_path","Previews/S04-SecurityReview-v1.png")));return;}
if(args[0]=="--inspect-art"){await Run(Action("inspect_game_art_asset",("asset_path",asset),("expected_sha256",hash),("asset_set_id","s04-industrial-security"),("asset_role","characters"),("style_family","realistic_pbr")));return;}
if(args[0]=="--visual-combat"){await Run(Action("create_security_combat_lab",("asset_path",asset),("expected_sha256",hash),("asset_set_id","s06-security-combat-v10"),("scene_path","Assets/Scenes/S06-SecurityCombat-v10.unity"),("output_path","Previews/S06-SecurityCombat-v10.png")));return;}
if(args[0]=="--visual-build"){await Run(Action("build_windows",("scene_path","Assets/Scenes/S06-SecurityCombat-v10.unity"),("output_path","Builds/S06-SecurityCombat-v10/S06-SecurityCombat-v10.exe"),("development_build","true")));return;}
if(args[0]=="--visual-preview"){await Run(Action("capture_scene_preview",("scene_path","Assets/Scenes/S06-SecurityCombat-v10.unity"),("output_path","Previews/S06-SecurityCombat-v10-runtime.png"),("duration_seconds","15")));return;}
if(args[0]=="--motion-combat"){await Run(Action("create_security_combat_lab",("asset_path",asset),("expected_sha256",hash),("asset_set_id","s06-security-combat-v8"),("scene_path","Assets/Scenes/S06-SecurityCombat-v8.unity"),("output_path","Previews/S06-SecurityCombat-v8.png")));return;}
if(args[0]=="--motion-build"){await Run(Action("build_windows",("scene_path","Assets/Scenes/S06-SecurityCombat-v8.unity"),("output_path","Builds/S06-SecurityCombat-v8/S06-SecurityCombat-v8.exe"),("development_build","true")));return;}
if(args[0]=="--motion-preview"){await Run(Action("capture_scene_preview",("scene_path","Assets/Scenes/S06-SecurityCombat-v8.unity"),("output_path","Previews/S06-SecurityCombat-v8-runtime.png"),("duration_seconds","15")));return;}
if(args[0]=="--polish-combat"){await Run(Action("create_security_combat_lab",("asset_path",asset),("expected_sha256",hash),("asset_set_id","s06-security-combat-v6"),("scene_path","Assets/Scenes/S06-SecurityCombat-v6.unity"),("output_path","Previews/S06-SecurityCombat-v6.png")));return;}
if(args[0]=="--polish-build"){await Run(Action("build_windows",("scene_path","Assets/Scenes/S06-SecurityCombat-v6.unity"),("output_path","Builds/S06-SecurityCombat-v6/S06-SecurityCombat-v6.exe"),("development_build","true")));return;}
if(args[0]=="--polish-preview"){await Run(Action("capture_scene_preview",("scene_path","Assets/Scenes/S06-SecurityCombat-v6.unity"),("output_path","Previews/S06-SecurityCombat-v6-runtime.png"),("duration_seconds","5")));return;}
if(args[0]=="--combat"){await Run(Action("create_security_combat_lab",("asset_path",asset),("expected_sha256",hash),("asset_set_id","s06-security-combat-v4"),("scene_path","Assets/Scenes/S06-SecurityCombat-v4.unity"),("output_path","Previews/S06-SecurityCombat-v4.png")));return;}
if(args[0]=="--build"){await Run(Action("build_windows",("scene_path","Assets/Scenes/S06-SecurityCombat-v4.unity"),("output_path","Builds/S06-SecurityCombat-v4/S06-SecurityCombat-v4.exe"),("development_build","true")));return;}
if(args[0]=="--preview"){await Run(Action("capture_scene_preview",("scene_path","Assets/Scenes/S06-SecurityCombat-v4.unity"),("output_path","Previews/S06-SecurityCombat-v4-runtime.png"),("duration_seconds","5")));return;}
throw new ArgumentException("unknown mode");
}
sealed class NoFallback:ILocalModel {public ModelStatus Status=>new("test",true,"offline");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken c)=>throw new InvalidOperationException("No provider fallback allowed.");}
