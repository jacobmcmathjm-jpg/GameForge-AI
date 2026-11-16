using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
using ForgeCore.Security;
using ForgeCore.Tools;

// User-requested demonstration of EXISTING production builders, not a new game generator or human approval service.
const string workspace=@"D:\ForgeCore\Workspace",project="Games/ForgeCore",label="Showcase20261028";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe";
const string blender=@"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
var output=Path.Combine(Environment.CurrentDirectory,"outputs","ForgeCore-CapabilityShowcase-20261028");
var guard=new WorkspacePathGuard(workspace);var projectRoot=guard.Resolve(project,false);
var json=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",blenderPath:blender);
using var executor=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,
    enableUnityProjectPackages:true,blenderPath:blender,blenderTimeoutSeconds:600);
try
{
    RequireIdle();
    if(args.Length==1&&args[0]=="--baseline")
    {
        if(Directory.Exists(output))throw new IOException("Showcase destination exists; do not replay baseline.");
        Directory.CreateDirectory(output);var entries=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var relative in new[]{"Assets/Scenes","Assets/ForgeCore/Generated/Blender","Assets/ForgeCoreGameTemplates","Assets/ForgeCoreMapDemo"})
        {
            var root=guard.Resolve(project+"/"+relative,true);if(!Directory.Exists(root))continue;
            foreach(var file in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))
                entries.Add(Path.GetRelativePath(workspace,guard.Resolve(file,false)),await HashFile(file));
        }
        foreach(var file in new[]{"Packages/manifest.json","Packages/packages-lock.json","Assets/ForgeCore/Editor/ForgeCoreBridge.cs"})
            entries.Add(project+"/"+file,await HashFile(guard.Resolve(project+"/"+file,false)));
        var objects=guard.Resolve(".forgecore/blender-library/objects",false);
        foreach(var file in Directory.EnumerateFiles(objects,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))
            entries.Add(Path.GetRelativePath(workspace,guard.Resolve(file,false)),await HashFile(file));
        await SaveNew(Path.Combine(output,"baseline.json"),new{schemaVersion=1,createdUtc=DateTimeOffset.UtcNow,entries,
            qualification="Existing scenes/source/kit/map/packages protected; EditorBuildSettings may gain new scene registrations with ForgeCore backups. No art/rights decisions."});
        Console.WriteLine($"BASELINE {entries.Count} existing files protected");return;
    }
    if(!File.Exists(Path.Combine(output,"baseline.json")))throw new IOException("Capture a fresh baseline first.");
    await VerifyBaseline();
    if(args.Length==2&&args[0]=="--template")await Template(args[1]);
    else if(args.Length==1&&args[0]=="--fps-normal-profile-retry")await Template("first_person_action",true);
    else if(args.Length==1&&args[0]=="--other-templates")
    {
        foreach(var template in new[]{"platformer","top_down_rpg","racing"}){await Template(template);await VerifyBaseline();}
    }
    else if(args.Length==2&&args[0]=="--asset")await Asset(args[1]);
    else if(args.Length==2&&args[0]=="--asset-source")await AssetSource(args[1]);
    else if(args.Length==1&&args[0]=="--asset-sources")
    {
        foreach(var kind in new[]{"barrel","mannequin","security"}){await AssetSource(kind);await VerifyBaseline();}
    }
    else if(args.Length==1&&args[0]=="--existing-labs")
    {
        foreach(var item in new[]{("SecurityCombat","Assets/Scenes/S06-SecurityCombat-v10.unity"),("AnimationLab","Assets/Scenes/S03-AnimationLab-v3.unity")})
        {
            await SaveNew(Path.Combine(output,"Evidence",item.Item1+"-source.json"),new{scene=item.Item2,reusedExistingGeneratedScene=true,
                sceneSha256=await HashFile(guard.Resolve(project+"/"+item.Item2,false)),qualification="Fresh runtime preview/build of an existing ForgeCore-generated lab; not newly generated character art."});
            await Preview(item.Item1,item.Item2);await Build(item.Item1,item.Item2);
        }
    }
    else if(args.Length==1&&args[0]=="--map")await Map();
    else if(args.Length==1&&args[0]=="--finish")await ShowcaseReport.WriteAsync(output,projectRoot);
    else if(args.Length==1&&args[0]=="--verify")Console.WriteLine("Existing source/scene/package baseline preserved.");
    else throw new ArgumentException("Use --baseline, --template <supported id>, --asset barrel|mannequin|security, --map or --verify.");
    await VerifyBaseline();Console.WriteLine("SHOWCASE_ITEM_COMPLETE");
}
catch(Exception error){Console.Error.WriteLine("SHOWCASE_STOPPED "+error.GetType().Name+": "+string.Join(' ',error.Message.Split('\n').Take(3)));Environment.ExitCode=1;}

async Task<string> Run(string step,ToolCall call)
{
    var receipt=Path.Combine(output,"Evidence",step+".json");if(File.Exists(receipt))throw new IOException("A step receipt exists; do not blindly replay the operation: "+step);
    if(call.Kind is ToolKind.ReviewGameArtAsset or ToolKind.PromoteLocalBlenderAsset or ToolKind.StoreReviewedAsset or ToolKind.Generate3DAsset)
        throw new InvalidOperationException("This demonstration cannot attest, promote or call a provider.");
    var p=await policy.ValidateAsync(call,default);if(p.Decision==SecurityDecision.Denied)throw new InvalidOperationException(p.Reason);
    Console.WriteLine("START "+step+" "+call.Kind);var timer=Stopwatch.StartNew();
    var result=await executor.ExecuteAsync(call,default);
    await SaveNew(receipt,new{schemaVersion=1,createdUtc=DateTimeOffset.UtcNow,call,policy=p.Decision.ToString(),
        executionAuthority="Current user's explicit request to generate a bounded showcase via production components; not a persisted GUI approval or human art/rights decision.",
        result.Success,result.Output,result.Error,seconds=timer.Elapsed.TotalSeconds});
    if(!result.Success)throw new InvalidOperationException(result.Error);
    Console.WriteLine("DONE "+step+" "+timer.Elapsed.TotalSeconds.ToString("0.0")+"s");return result.Output;
}
ToolCall Action(string op,params(string Key,string Value)[] fields)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,
    "Showcase: "+op,new[]{("project_path",project),("operation",op)}.Concat(fields).ToArray());
async Task Template(string template,bool normalProfileRetry=false)
{
    if(!UnityGameTemplateSpec.SupportedTemplates.Contains(template))throw new ArgumentException("Unsupported game family.");
    var id=template switch{"first_person_action"=>"FPS","platformer"=>"Platformer","top_down_rpg"=>"TopDownRPG",_=>"Racing"};
    var scene=$"Assets/Scenes/{label}/{id}"+(id=="FPS"?"-Level01":"")+".unity";
    var create=id=="FPS"?Action("create_game_campaign",("scene_path",scene),("template_id",template),("seed","281028"),("level_count","3")):
        Action("create_game_template",("scene_path",scene),("template_id",template),("seed","281028"));
    if(normalProfileRetry)
    {
        // Explicitly scoped recovery of the recorded pre-scene licensing failure, never generic replay or lock removal.
        using var failed=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"Evidence","FPS-create.json")));
        if(id!="FPS"||failed.RootElement.GetProperty("success").GetBoolean()||!failed.RootElement.GetProperty("error").GetString()!.Contains("exit 198",StringComparison.Ordinal))
            throw new InvalidOperationException("Only the recorded pre-scene licensing failure may use this retry.");
        foreach(var path in UnityEditorActionSpec.From(create).GetCampaignScenePaths())
            if(File.Exists(guard.Resolve(project+"/"+path,false)))throw new IOException("A campaign target exists; no retry.");
    }
    await Run(id+(normalProfileRetry?"-create-normal-profile-retry":"-create"),create);
    await Preview(id,scene);
    for(var i=1;i<=(id=="FPS"?3:1);i++)await Run(id+"-verify-"+i,Action("verify_gameplay",("scene_path",id=="FPS"?scene.Replace("Level01","Level"+i.ToString("00")):scene),("duration_seconds","5")));
    await Build(id,scene,id=="FPS"?3:0);
}
async Task Asset(string kind)
{
    var name="showcase-"+kind+"-20261028";
    var prompt=kind switch{"barrel"=>$"Create a weathered barrel in Blender. Name it {name}.",
        "mannequin"=>$"Create an animated rigged mannequin in Blender. Name it {name}.",
        "security"=>$"Create a security combat lab in Blender. Name it {name}.",_=>throw new ArgumentException("Unsupported source recipe.")};
    await using var model=new BuiltInBlenderAssetModel(new NoFallback(),project);var observations=new List<ToolObservation>();
    for(var turn=0;turn<14;turn++)
    {
        var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn+1,14-turn),default);
        if(plan.Actions.Count==0)
        {
            if(observations.Count==0||!observations.All(x=>x.Success)||!observations.Any(x=>x.Kind==ToolKind.ImportBlenderAsset))throw new InvalidOperationException("Incomplete source workflow.");
            await SaveNew(Path.Combine(output,"Evidence",kind+"-workflow.json"),new{schemaVersion=1,prompt,observations,plan.Response,
                qualification="Actual deterministic production wrapper/policy/tool route, not full persisted desktop workflow or human art acceptance."});
            break;
        }
        var call=plan.Actions.Single();var result=await Run(kind+"-step-"+(turn+1).ToString("00"),call);
        observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,result));
        if(turn==13)throw new InvalidOperationException("Source workflow exceeded its bound.");
    }
    var created=observations.Single(x=>x.Kind==ToolKind.CreateBlenderAsset);using var receipt=JsonDocument.Parse(created.Detail);
    var hash=receipt.RootElement.GetProperty("bundleSha256").GetString()!;var source=guard.Resolve($".forgecore/blender-library/objects/{hash}",false);
    foreach(var file in Directory.GetFiles(source))await CopyExact(file,Path.Combine(output,"Assets",kind,Path.GetFileName(file)));
    if(kind!="barrel")
    {
        var asset=BlenderAssetImportSpec.From(new(observations.First(x=>x.Kind==ToolKind.ImportBlenderAsset).ToolCallId,ToolKind.ImportBlenderAsset,
            observations.First(x=>x.Kind==ToolKind.ImportBlenderAsset).Arguments,PermissionKind.Write,"Exact import"));
        var scene=$"Assets/Scenes/CharacterAnimations/{name}-{asset.ExpectedAssetSha256[..12]}.unity";
        var id=kind=="security"?"SecurityCombat":"AnimationLab";await Preview(id,scene);await Build(id,scene);
    }
    else
    {
        var png=guard.Resolve(project+$"/Previews/Blender/{name}-{receipt.RootElement.GetProperty("assetSha256").GetString()![..12]}.png",false);
        await CopyExact(png,Path.Combine(output,"Previews","Barrel-Unity.png"));
    }
}
async Task AssetSource(string kind)
{
    var name="showcase-"+kind+"-20261028";
    var prompt=kind switch{"barrel"=>$"Create a weathered barrel in Blender. Name it {name}.",
        "mannequin"=>$"Create a rigged mannequin in Blender. Name it {name}.",
        "security"=>$"Create an industrial security character in Blender. Name it {name}.",_=>throw new ArgumentException("Unsupported source recipe.")};
    await using var model=new BuiltInBlenderAssetModel(new NoFallback(),project);
    var call=(await model.CreatePlanAsync(new(prompt,[]),default)).Actions.Single();
    if(call.Kind!=ToolKind.CreateBlenderAsset)throw new InvalidOperationException("Only original source creation is requested in this demonstration step.");
    var result=await Run(kind+"-source-create",call);using var receipt=JsonDocument.Parse(result);var hash=receipt.RootElement.GetProperty("bundleSha256").GetString()!;
    var source=guard.Resolve($".forgecore/blender-library/objects/{hash}",false);
    foreach(var file in Directory.GetFiles(source))await CopyExact(file,Path.Combine(output,"Assets",kind,Path.GetFileName(file)));
    await SaveNew(Path.Combine(output,"Evidence",kind+"-source-route.json"),new{schemaVersion=1,prompt,call,bundleSha256=hash,
        reused=receipt.RootElement.GetProperty("reused").GetBoolean(),sourceOnly=true,imported=false,visualReviewed=false,rightsReviewed=false,
        qualification="Actual production wrapper creation step and installed Blender execution. Stopped before import; Blender source preview is not target Unity or human acceptance."});
}
async Task Map()
{
    // The existing map builder owns a fixed Generated directory. Preserve that map; demonstrate exact existing-scene reuse instead of destructive regeneration.
    const string scene="Assets/Scenes/FrontierValley.unity";
    if(!File.Exists(guard.Resolve(project+"/"+scene,false)))throw new IOException("Existing Frontier Valley map is absent; no fixed generated folder will be overwritten.");
    await SaveNew(Path.Combine(output,"Evidence","FrontierValley-source.json"),new{scene,reusedExistingGeneratedMap=true,sceneSha256=await HashFile(guard.Resolve(project+"/"+scene,false))});
    await Preview("FrontierValley",scene);await Build("FrontierValley",scene);
}
async Task Preview(string id,string scene)
{
    var relative=$"Previews/{label}/{id}.png";
    await Run(id+"-preview",Action("capture_scene_preview",("scene_path",scene),("output_path",relative),("duration_seconds","5")));
    foreach(var suffix in new[]{"",".forgecore-preview.json"})await CopyExact(guard.Resolve(project+"/"+relative+suffix,false),Path.Combine(output,"Previews",id+".png"+suffix));
}
async Task Build(string id,string scene,int count=0)
{
    var relative=$"Builds/{label}/{id}/{id}.exe";
    var fields=new List<(string Key,string Value)>{("scene_path",scene),("output_path",relative),("development_build","true")};
    if(count>0)fields.Add(("level_count",count.ToString()));await Run(id+"-build",Action("build_windows",fields.ToArray()));
    var root=Path.GetDirectoryName(guard.Resolve(project+"/"+relative,false))!;
    foreach(var file in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))
        await CopyExact(file,Path.Combine(output,"Games",id,Path.GetRelativePath(root,file)));
    await Run(id+"-startup",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Headless bounded startup smoke only; not interactive play acceptance",
        ("project_path",project),("output_path",relative),("duration_seconds","5")));
}
async Task VerifyBaseline()
{
    using var baseline=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,"baseline.json")));
    foreach(var entry in baseline.RootElement.GetProperty("entries").EnumerateObject())
        if(await HashFile(guard.Resolve(entry.Name,false))!=entry.Value.GetString())throw new InvalidDataException("Protected old content changed: "+entry.Name);
}
async Task CopyExact(string source,string target)
{
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target,false);
    if(await HashFile(source)!=await HashFile(target))throw new InvalidDataException("Showcase copy integrity failed.");
}
async Task SaveNew(string path,object data)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);await using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
    await JsonSerializer.SerializeAsync(stream,data,json);
}
static async Task<string> HashFile(string path){await using var stream=File.OpenRead(path);return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();}
static void RequireIdle(){foreach(var name in new[]{"Unity","blender"})foreach(var p in Process.GetProcessesByName(name)){using(p)throw new InvalidOperationException("Close "+name+" before serialized showcase work; nothing is terminated automatically.");}}
sealed class NoFallback:ILocalModel
{
    public ModelStatus Status=>new("offline",true,"Production fixed recipes only");
    public Task<AgentPlan>CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new InvalidOperationException("No inference/provider fallback in showcase.");
}
