using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Linq;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
const string project="Games/ForgeCore";
const string editor=@"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe",blender=@"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
var guard=new WorkspacePathGuard(@"D:\ForgeCore\Workspace");
var evidence=Path.Combine(Environment.CurrentDirectory,"work","s08-s10-verification");Directory.CreateDirectory(evidence);
const string missionScene="Assets/Scenes/S10-IndustrialInfiltration-v2.unity";
const string missionExe="Builds/S10-IndustrialInfiltration-v2/S10-IndustrialInfiltration-v2.exe";
var policy=new SecurityPolicy(guard,[],unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",blenderPath:blender);
using var tools=new ToolExecutor(guard,[],[],100*1024*1024,unityEditorPath:editor,unityEditorVersion:"6000.5.5f1",unityTimeoutSeconds:1200,enableUnityProjectPackages:true,blenderPath:blender,blenderTimeoutSeconds:600);
try {
    if(args.Length!=1)throw new ArgumentException("One bounded mode required.");
    if(args[0] is "--retro64-v1" or "--retro64-v2") {
        var revision=args[0].EndsWith("v2",StringComparison.Ordinal)?"v2":"v1";
        await Run("retro64-"+revision+"-install-bridge",ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Exact protected bridge upgrade for opt-in original retro profile",("project_path",project)));
        await Run("retro64-"+revision+"-install-kit",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Original retro profile; preserve modern default, exact prior kit and games",("project_path",project),("template_id","first_person_action")));
        await Run("retro64-"+revision+"-compile",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("retro64-"+revision+"-mission",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","retro-yard-"+revision),
            ("scene_path","Assets/Scenes/Retro/Industrial-Retro64-"+revision+".unity"),("output_path","Previews/Retro/Industrial-Retro64-"+revision+"-poses.png"),("style_family","retro_64")));
    }else if(args[0] is "--retro64-v1-delivery" or "--retro64-v2-delivery" or "--retro64-v2-build-only") {
        var revision=args[0].Contains("-v2-",StringComparison.Ordinal)?"v2":"v1";
        await Run("retro64-"+revision+"-runtime",Action("capture_scene_preview",("scene_path","Assets/Scenes/Retro/Industrial-Retro64-"+revision+".unity"),("output_path","Previews/Retro/Industrial-Retro64-"+revision+"-runtime.png"),("duration_seconds","4")));
        await Run("retro64-"+revision+"-build",Action("build_windows",("scene_path","Assets/Scenes/Retro/Industrial-Retro64-"+revision+".unity"),("output_path","Builds/Retro/retro-yard-"+revision+"/retro-yard.exe"),("development_build","true")));
        await Run("retro64-"+revision+"-startup",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Exact retro player startup, not visual acceptance",("project_path",project),("output_path","Builds/Retro/retro-yard-"+revision+"/retro-yard.exe"),("duration_seconds","5")));
        if(!args[0].EndsWith("-build-only",StringComparison.Ordinal))await Run("retro64-"+revision+"-edit-tests",Action("run_tests",("test_mode","edit")));
    }else if(args[0] is "--visual-polish-v1" or "--visual-polish-v2") {
        var revision=args[0].EndsWith("v2",StringComparison.Ordinal)?"v2":"v1";
        await Run("visual-polish-"+revision+"-install-kit",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Original courtyard surface/hero/lighting finish; protect exact prior sources and games",("project_path",project),("template_id","first_person_action")));
        await Run("visual-polish-"+revision+"-compile",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("visual-polish-"+revision+"-mission",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","polished-yard-"+revision),
            ("scene_path","Assets/Scenes/S13-PolishedCourtyard-"+revision+".unity"),("output_path","Previews/Graphics/S13-PolishedCourtyard-"+revision+"-poses.png")));
    }else if(args[0] is "--visual-polish-v1-delivery" or "--visual-polish-v2-delivery") {
        var revision=args[0].Contains("-v2-",StringComparison.Ordinal)?"v2":"v1";
        await Run("visual-polish-"+revision+"-runtime",Action("capture_scene_preview",("scene_path","Assets/Scenes/S13-PolishedCourtyard-"+revision+".unity"),("output_path","Previews/Graphics/S13-PolishedCourtyard-"+revision+"-runtime.png"),("duration_seconds","4")));
        await Run("visual-polish-"+revision+"-build",Action("build_windows",("scene_path","Assets/Scenes/S13-PolishedCourtyard-"+revision+".unity"),("output_path","Builds/Courtyard/polished-yard-"+revision+"/polished-yard.exe"),("development_build","true")));
        await Run("visual-polish-"+revision+"-startup",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Exact final prototype startup, not visual acceptance",("project_path",project),("output_path","Builds/Courtyard/polished-yard-"+revision+"/polished-yard.exe"),("duration_seconds","5")));
        await Run("visual-polish-"+revision+"-edit-tests",Action("run_tests",("test_mode","edit")));
    }else if(args[0] is "--shaped-props-v1" or "--shaped-props-v1-resume") {
        await Run("shaped-props-v1-install-kit",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Reusable original shaped template props, preserve exact previous kit and existing games",("project_path",project),("template_id","first_person_action")));
        await Run(args[0]=="--shaped-props-v1-resume"?"shaped-props-v1-compile-desktop":"shaped-props-v1-compile",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("shaped-props-v1-mission",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","shaped-yard-v1"),
            ("scene_path","Assets/Scenes/S13-ShapedCourtyard-v1.unity"),("output_path","Previews/Graphics/S13-ShapedCourtyard-v1-poses.png")));
    }else if(args[0]=="--shaped-props-v1-delivery") {
        await Run("shaped-props-v1-runtime",Action("capture_scene_preview",("scene_path","Assets/Scenes/S13-ShapedCourtyard-v1.unity"),("output_path","Previews/Graphics/S13-ShapedCourtyard-v1-runtime.png"),("duration_seconds","4")));
        await Run("shaped-props-v1-build",Action("build_windows",("scene_path","Assets/Scenes/S13-ShapedCourtyard-v1.unity"),("output_path","Builds/Courtyard/shaped-yard-v1/shaped-yard.exe"),("development_build","true")));
        await Run("shaped-props-v1-startup",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Exact new prototype startup, not visual acceptance",("project_path",project),("output_path","Builds/Courtyard/shaped-yard-v1/shaped-yard.exe"),("duration_seconds","5")));
    }else if(args[0]=="--shaped-props-v1-edit-tests") {
        await Run("shaped-props-v1-edit-tests",Action("run_tests",("test_mode","edit")));
    }else if(args[0] is "--graphics-v1" or "--graphics-v2" or "--graphics-v3") {
        var revision=args[0]=="--graphics-v3"?"v3":args[0]=="--graphics-v2"?"v2":"v1";
        await Run("graphics-"+revision+"-install-kit",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Reusable bounded courtyard graphics benchmark; exact prior kit backup, no existing game overwrite",("project_path",project),("template_id","first_person_action")));
        await Run("graphics-"+revision+"-compile",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("graphics-"+revision+"-mission",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","graphics-yard-"+revision),
            ("scene_path","Assets/Scenes/S13-GraphicsBenchmark-"+revision+".unity"),("output_path","Previews/Graphics/S13-GraphicsBenchmark-"+revision+"-poses.png")));
    }else if(args[0] is "--graphics-natural-v1" or "--graphics-natural-v2-delivery") {
        const string prompt="Create a detailed industrial courtyard named nightly-yard.";
        await using var model=new ForgeCore.LocalAI.BuiltInBlenderAssetModel(new NoProviderFallback(),project);
        var observations=new List<ToolObservation>();var finished=false;
        for(var turn=0;turn<12;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12-turn),default);
            if(plan.Actions.Count==0) {
                if(observations.Count!=11||!observations.All(o=>o.Success)||!plan.Response.Contains("Windows EXE",StringComparison.Ordinal))throw new IOException("Ordinary courtyard route stopped unfinished: "+plan.Response);
                using var stream=new FileStream(Path.Combine(evidence,args[0]=="--graphics-natural-v2-delivery"?"graphics-natural-v2-complete.json":"graphics-natural-v1-complete.json"),FileMode.CreateNew);
                await JsonSerializer.SerializeAsync(stream,new{prompt,steps=observations.Count,plan.Response,providerFallbackUsed=false,humanArtAccepted=false,
                    qualification="Real deterministic app planner with production policy/tools and scoped execution in this harness, not a normal UI conversation or human acceptance."},new JsonSerializerOptions{WriteIndented=true});
                finished=true;break;
            }
            if(plan.Actions.Count!=1)throw new IOException("Expected one scoped graphics action at a time.");
            var call=plan.Actions[0];var step="graphics-natural-v1-"+turn.ToString("00");await Run(step,call);
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,step+".json")));
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,receipt.RootElement.GetProperty("Output").GetString()!));
        }
        if(!finished)throw new IOException("Ordinary courtyard route exceeded its bounded budget.");
    }else if(args[0]=="--graphics-v1-build") {
        await Run("graphics-v1-build",Action("build_windows",("scene_path","Assets/Scenes/S13-GraphicsBenchmark-v1.unity"),("output_path","Builds/S13-GraphicsBenchmark-v1/S13-GraphicsBenchmark-v1.exe"),("development_build","true")));
    }else if(args[0]=="--graphics-v1-tests") {
        await Run("graphics-v1-edit-tests",Action("run_tests",("test_mode","edit")));
    }else if(args[0]=="--graphics-v1-runtime") {
        await Run("graphics-v1-runtime-capture",Action("capture_scene_preview",("scene_path","Assets/Scenes/S13-GraphicsBenchmark-v1.unity"),("output_path","Previews/Graphics/S13-GraphicsBenchmark-v1-runtime.png"),("duration_seconds","4")));
        await Run("graphics-v1-runtime-profile",Action("profile_scene",("scene_path","Assets/Scenes/S13-GraphicsBenchmark-v1.unity"),("duration_seconds","10")));
    }else if(args[0] is "--refined-props-v1" or "--refined-crate-v2" or "--refined-props-v2" or "--refined-crate-v3" or "--refined-props-v3" or "--refined-props-v4" or "--refined-props-v5") {
        var revision=args[0].EndsWith("v5",StringComparison.Ordinal)?"refined5":args[0].EndsWith("v4",StringComparison.Ordinal)?"refined4":args[0].EndsWith("v3",StringComparison.Ordinal)?"refined3":args[0]=="--refined-props-v1"?"refined1":"refined2";
        foreach(var recipe in IndustrialPropRecipes.Refined.Where(r=>!args[0].StartsWith("--refined-crate",StringComparison.Ordinal)||r.Id=="wooden_crate_v2")) {
            var fields=new List<(string Key,string Value)>{("project_path",project),("asset_name",revision+"-"+recipe.Id.Replace('_','-')),("recipe",recipe.Id),("seed","83"),("height_mm",recipe.DefaultHeight.ToString())};
            if(recipe.IsBarrel)fields.Add(("radius_mm","340"));
            await Run(revision+"-create-"+recipe.Id,ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Original versioned refined prop candidate, no human acceptance or scene placement",fields.ToArray()));
        }
    }else if(args[0] is "--refined4-technical-import" or "--refined5-technical-import") {
        var revision=args[0].StartsWith("--refined5",StringComparison.Ordinal)?"refined5":"refined4";
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,revision+"-create-"+recipe.Id+".json")));
            using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
            await Run(revision+"-import-"+recipe.Id,ToolCall.Create(ToolKind.ImportBlenderAsset,PermissionKind.Write,"Exact candidate import for separate technical Unity review, not human approval or gameplay placement",
                ("project_path",project),("asset_name",body.RootElement.GetProperty("assetName").GetString()!),
                ("bundle_sha256",body.RootElement.GetProperty("bundleSha256").GetString()!),("expected_asset_sha256",body.RootElement.GetProperty("assetSha256").GetString()!)));
        }
    }else if(args[0] is "--refined4-unity-review" or "--refined5-unity-review") {
        var revision=args[0].StartsWith("--refined5",StringComparison.Ordinal)?"refined5":"refined4";
        await Run(revision+"-compile",Action("compile"));
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,revision+"-import-"+recipe.Id+".json")));
            using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
            var id=revision+"-"+recipe.Id.Replace('_','-');var asset=body.RootElement.GetProperty("assetPath").GetString()!;var hash=body.RootElement.GetProperty("assetSha256").GetString()!;
            await Run(revision+"-prepare-"+recipe.Id,Action("prepare_industrial_prop",("asset_path",asset),("expected_sha256",hash),("asset_set_id",id),
                ("scene_path","Assets/Scenes/IndustrialProps/"+id+".unity"),("output_path","Previews/IndustrialProps/"+id+".png")));
        }
    }else if(args[0] is "--refined5-agent-cancel-check" or "--refined5-agent-stable-cancel") {
        var prefix=args[0]=="--refined5-agent-stable-cancel"?"refined5-stable-target":"refined5-natural-target";
        const string prompt="Review imported industrial asset refined5-wooden-crate-v2 visually.";
        using var proposalDocument=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,prefix+"-proposal.json")));
        var proposal=JsonSerializer.Deserialize<ToolCall>(proposalDocument.RootElement.GetRawText())!;var expected=GameArtReviewSpec.FromProposal(proposal);
        var reader=new GameArtReviewReader(guard,"6000.5.5f1");var cancellation=new CancelOnlyVerifiedTarget(reader,expected);var memory=new ProbeMemoryStore();
        var executor=new ReadOnlyProbeExecutor(tools);using var global=new GlobalCancellation();
        await using var model=new ForgeCore.LocalAI.BuiltInBlenderAssetModel(new NoProviderFallback(),project);
        var runner=new ForgeCore.Agent.AgentRunner(model,policy,cancellation,executor,memory,global,new ForgeCore.Agent.AgentOptions{MaxActions=8,MaxPlanningIterations=8,MaxRetries=0},Microsoft.Extensions.Logging.Abstractions.NullLogger<ForgeCore.Agent.AgentRunner>.Instance);
        var result=await runner.RunAsync(Guid.NewGuid(),prompt,default);
        if(executor.Executed!=3||cancellation.Requests!=1||!memory.States.Contains(ForgeTaskState.AwaitingApproval)||!result.Response.Contains("Cancellation/failure grants no acceptance",StringComparison.Ordinal)||result.ToolResults.Count!=4)
            throw new IOException("Actual AgentRunner did not stop cleanly after synthetic target cancellation.");
        using(var stream=new FileStream(Path.Combine(evidence,args[0]=="--refined5-agent-stable-cancel"?"refined5-agent-stable-cancel.json":"refined5-agent-cancel-check.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,readOnlyToolsExecuted=executor.Executed,verifiedTargetBoundaryRequests=cancellation.Requests,
            states=memory.States.Select(s=>s.ToString()).ToArray(),syntheticDecision="cancelled",humanDecisionRecorded=false,liveSqliteOpened=false,providerFallbackUsed=false,normalWpfDialogShown=false,
            qualification="Actual AgentRunner/model/policy/production read tools, verified current PNG, ephemeral in-memory store and cancellation-only test approval service. No review/write tool can execute through this probe; not a live user interaction test."},new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("ACTUAL_AGENT_READONLY_TO_VERIFIED_CANCEL_BOUNDARY_PASS");
    }else if(args[0] is "--refined5-natural-target-review" or "--refined5-stable-target-review") {
        var prefix=args[0]=="--refined5-stable-target-review"?"refined5-stable-target":"refined5-natural-target";
        const string prompt="Review imported industrial asset refined5-wooden-crate-v2 visually.";
        await using var model=new ForgeCore.LocalAI.BuiltInBlenderAssetModel(new NoProviderFallback(),project);var observations=new List<ToolObservation>();var stopped=false;
        for(var turn=0;turn<9;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,9),default);
            if(plan.Actions.Count!=1)throw new IOException("Natural target route stopped before an exact verified human proposal: "+plan.Response);
            var call=plan.Actions[0];
            if(call.Kind==ToolKind.ReviewGameArtAsset) {
                var packet=await new GameArtReviewReader(guard,"6000.5.5f1").ReadAsync(call,default);
                using(var stream=new FileStream(Path.Combine(evidence,prefix+"-proposal.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,call);
                using(var stream=new FileStream(Path.Combine(evidence,prefix+"-stop.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{prompt,technicalSteps=observations.Count,plan.Response,packet.Proposal,anyReviewExecuted=false,providerFallbackUsed=false,qualification="Actual built-in prompt to verified target proposal; deliberately stops BEFORE live human review and records no decision."},new JsonSerializerOptions{WriteIndented=true});
                Console.WriteLine("STOPPED_BEFORE_LIVE_HUMAN_TARGET_REVIEW "+packet.Proposal.ExpectedPreviewSha256);stopped=true;break;
            }
            if(call.Kind is not (ToolKind.ReadTextFile or ToolKind.InspectLocalBlenderAsset or ToolKind.UnityEditorAction))throw new IOException("Target review escaped its fixed preparation tool set.");
            var step=prefix+"-"+turn.ToString("00");var path=Path.Combine(evidence,step+".json");
            if(File.Exists(path))throw new IOException("Target route probe receipt exists; choose a fresh attempt rather than replay writes.");
            var check=await policy.ValidateAsync(call,default);if(check.Decision==SecurityDecision.Denied)throw new IOException(check.Reason);
            Console.WriteLine("START "+step);var result=await tools.ExecuteAsync(call,default);
            using(var stream=new FileStream(path,FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{call.Kind,call.Arguments,result.Success,result.Output,result.Error},new JsonSerializerOptions{WriteIndented=true});
            // A missing metadata read is an expected discovery result, not authority to overwrite an occupied preview.
            if(!result.Success&&call.Kind!=ToolKind.ReadTextFile)throw new IOException(result.Error);
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,result.Success?"Succeeded":"Failed",result.Success,result.Success?result.Output:result.Error??"Missing"));
        }
        if(!stopped)throw new IOException("Target review probe exhausted its bounded planner steps.");
    }else if(args[0]=="--refined5-natural-prompt") {
        const string prompt="Create a refined wooden crate in Blender. Name it refined-session-crate.";
        await using var model=new ForgeCore.LocalAI.BuiltInBlenderAssetModel(new NoProviderFallback(),project);
        var observations=new List<ToolObservation>();var finished=false;
        for(var turn=0;turn<9;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,9),default);
            if(plan.Actions.Count==0) {
                if(observations.Count!=6||!plan.Response.Contains("remain open",StringComparison.Ordinal))throw new IOException("Natural prompt route stopped without all bounded technical steps.");
                using(var stream=new FileStream(Path.Combine(evidence,"refined5-natural-prompt-complete.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{prompt,toolSteps=observations.Count,plan.Response,providerFallbackUsed=false,humanReviewRecorded=false,gameplaySceneChanged=false,qualification="Real bounded offline built-in planner to production Blender/import/bridge/kit/Unity technical fixture. Not unrestricted neural generation, a normal app/SQLite session or human art acceptance."},new JsonSerializerOptions{WriteIndented=true});
                Console.WriteLine(plan.Response);finished=true;break;
            }
            if(plan.Actions.Count!=1||plan.Actions[0].Kind is not (ToolKind.CreateBlenderAsset or ToolKind.ImportBlenderAsset or ToolKind.InstallUnityBridge or ToolKind.ScaffoldUnityGameTemplate or ToolKind.UnityEditorAction))throw new IOException("Natural asset probe escaped its fixed local tool set.");
            var call=plan.Actions[0];var step="refined5-natural-prompt-"+turn.ToString("00");await Run(step,call);
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,step+".json")));
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,receipt.RootElement.GetProperty("Output").GetString()!));
        }
        if(!finished)throw new IOException("Natural prompt exhausted its bounded planner steps.");
    }else if(args[0]=="--refined5-model-plan-check") {
        using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-layout-source-plan.json")));
        var arguments=JsonSerializer.Deserialize<Dictionary<string,string>>(receipt.RootElement.GetProperty("Arguments").GetRawText())!;
        var call=new ToolCall(Guid.NewGuid(),ToolKind.PlanIndustrialAssets,arguments,PermissionKind.Read,"Actual production refined source plan");
        await using var model=new ForgeCore.LocalAI.BuiltInBlenderAssetModel(new NoProviderFallback(),project);
        var plan=await model.CreatePlanAsync(new("Plan refined industrial assets for refined-yard.",[],[new(call.Id,call.Kind,call.Description,arguments,"Succeeded",true,receipt.RootElement.GetProperty("Output").GetString()!)],1,8),default);
        if(plan.Actions.Count!=0||!plan.Response.Contains("5/5 unmet",StringComparison.Ordinal)||!plan.Response.Contains("not Unity runtime",StringComparison.Ordinal))throw new IOException("Production plan observation did not survive exact identity/footprint validation.");
        using(var stream=new FileStream(Path.Combine(evidence,"refined5-model-plan-check.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{plan.Response,followUpActions=plan.Actions.Count,providerFallbackUsed=false,actualProductionReceipt=true});
        Console.WriteLine(plan.Response);
    }else if(args[0]=="--refined5-dimension-variants") {
        foreach(var variant in new[]{(Recipe:"industrial_barrel_v3",Height:1500,Radius:500),(Recipe:"wooden_crate_v2",Height:1200,Radius:0),(Recipe:"shipping_container_v2",Height:2200,Radius:0)}) {
            var fields=new List<(string Key,string Value)>{("project_path",project),("asset_name","refined5-variant-"+variant.Recipe.Replace('_','-')),("recipe",variant.Recipe),("seed","97"),("height_mm",variant.Height.ToString())};if(variant.Radius!=0)fields.Add(("radius_mm",variant.Radius.ToString()));
            await Run("refined5-dimension-variant-"+variant.Recipe,ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Exercise actual bounded recipe dimensions/seed, retained as unreviewed variants outside the authored layout",fields.ToArray()));
            if(IndustrialPlacementLayout.FitsSource(variant.Recipe,variant.Height,variant.Radius))throw new IOException("Non-authored variant must not fit fixed placement footprint.");
        }
    }else if(args[0]=="--refined5-regression-tests") {
        await Run("refined5-edit-regression",Action("run_tests",("test_mode","edit")));
        await Run("refined5-play-regression",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--refined5-restore-check") {
        var exported=Path.Combine(Environment.CurrentDirectory,"outputs","ForgeCore-IndustrialReview-20261030-RefinedV1");
        var restored=Path.Combine(Environment.CurrentDirectory,"work","three-hour-assets","refined5-restored-workspace");
        if(Directory.Exists(restored)||File.Exists(restored))throw new IOException("Restore probe destination exists; no overwrite.");
        using var manifest=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(exported,"review-manifest.json")));
        var exportGuard=new WorkspacePathGuard(exported);Directory.CreateDirectory(restored);var restoredGuard=new WorkspacePathGuard(restored);var copied=0;
        foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray()) {
            var relative=file.GetProperty("path").GetString()!;var source=exportGuard.Resolve(relative,false);var bytes=await File.ReadAllBytesAsync(source);
            if(bytes.LongLength!=file.GetProperty("bytes").GetInt64()||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=file.GetProperty("sha256").GetString())throw new IOException("Export manifest readback failed.");
            if(!relative.StartsWith(".forgecore/blender-library/",StringComparison.Ordinal))continue;
            var destination=restoredGuard.Resolve(relative,true);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using(var stream=new FileStream(destination,FileMode.CreateNew))await stream.WriteAsync(bytes);copied++;
        }
        const string reuseProject="Games/ReuseProbe";
        Directory.CreateDirectory(restoredGuard.Resolve(reuseProject+"/Assets",true));Directory.CreateDirectory(restoredGuard.Resolve(reuseProject+"/ProjectSettings",true));
        using(var version=new FileStream(restoredGuard.Resolve(reuseProject+"/ProjectSettings/ProjectVersion.txt",true),FileMode.CreateNew))await version.WriteAsync(System.Text.Encoding.UTF8.GetBytes("m_EditorVersion: 6000.5.5f1\n"));
        using var restoredTools=new ToolExecutor(restoredGuard,[],[],100*1024*1024,blenderPath:blender,blenderTimeoutSeconds:600);
        var verified=new List<object>();
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            var name="refined5-"+recipe.Id.Replace('_','-');
            var fields=new List<(string Key,string Value)>{("project_path",reuseProject),("asset_name",name),("recipe",recipe.Id),("seed","83"),("height_mm",recipe.DefaultHeight.ToString())};if(recipe.IsBarrel)fields.Add(("radius_mm","340"));
            var creation=await restoredTools.ExecuteAsync(ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Verify exact restored current source cache in a separate disposable test project",fields.ToArray()),default);
            if(!creation.Success)throw new IOException(creation.Error);using var source=JsonDocument.Parse(creation.Output);
            if(!source.RootElement.GetProperty("reused").GetBoolean())throw new IOException("Restored source was regenerated rather than reused.");
            var bundle=source.RootElement.GetProperty("bundleSha256").GetString()!;var asset=source.RootElement.GetProperty("assetSha256").GetString()!;
            var packet=await new LocalBlenderReviewReader(restoredGuard).ReadAsync(bundle,default);
            if(packet.Identity.AssetSha256!=asset)throw new IOException("Restored source review identity changed.");
            var import=await restoredTools.ExecuteAsync(ToolCall.Create(ToolKind.ImportBlenderAsset,PermissionKind.Write,"Separate restored-source technical import test, not art acceptance",("project_path",reuseProject),("asset_name","reuse-probe-"+recipe.Id.Replace('_','-')),("bundle_sha256",bundle),("expected_asset_sha256",asset)),default);
            if(!import.Success)throw new IOException(import.Error);
            verified.Add(new{recipe=recipe.Id,bundleSha256=bundle,assetSha256=asset,reusedWithoutBlenderLaunch=true,freshTechnicalImport=true,humanAccepted=false});
        }
        if(Directory.Exists(restoredGuard.Resolve(".forgecore/blender-library/reviews",true))||Directory.Exists(restoredGuard.Resolve(".forgecore/blender-library/promotions",true)))throw new IOException("Restore probe must not create source decisions/promotions.");
        using(var stream=new FileStream(Path.Combine(evidence,"refined5-source-restore-check.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,exportManifestSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(exported,"review-manifest.json")))).ToLowerInvariant(),copiedLibraryFiles=copied,verified,qualification="Restored generated binary source/cache readback and fresh technical imports in a separate minimal test project. Not a restored source-code build, Unity project run, human review, promotion or gameplay placement."},new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("RESTORED_LIBRARY_REUSED_AND_IMPORTED_FIVE_EXACT_SOURCES copied="+copied);
    }else if(args[0]=="--refined5-cache-check") {
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            var fields=new List<(string Key,string Value)>{("project_path",project),("asset_name","refined5-"+recipe.Id.Replace('_','-')),("recipe",recipe.Id),("seed","83"),("height_mm",recipe.DefaultHeight.ToString())};
            if(recipe.IsBarrel)fields.Add(("radius_mm","340"));
            await Run("refined5-cache-reuse-"+recipe.Id,ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Verify exact current cached source reuse without accepting art",fields.ToArray()));
            using var old=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-create-"+recipe.Id+".json")));
            using var reuse=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-cache-reuse-"+recipe.Id+".json")));
            using var a=JsonDocument.Parse(old.RootElement.GetProperty("Output").GetString()!);using var b=JsonDocument.Parse(reuse.RootElement.GetProperty("Output").GetString()!);
            if(a.RootElement.GetProperty("bundleSha256").GetString()!=b.RootElement.GetProperty("bundleSha256").GetString()||!b.RootElement.GetProperty("reused").GetBoolean())throw new IOException("Exact source cache reuse failed.");
        }
    }else if(args[0]=="--refined5-review-packets") {
        var folder=Path.Combine(evidence,"refined5-review-proposals");if(Directory.Exists(folder))throw new IOException("Review packets already exist; no overwrite.");
        Directory.CreateDirectory(folder);
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-import-"+recipe.Id+".json")));
            using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
            var id="refined5-"+recipe.Id.Replace('_','-');var asset=body.RootElement.GetProperty("assetPath").GetString()!;
            var hash=body.RootElement.GetProperty("assetSha256").GetString()!;var preview="Previews/RefinedReview/"+id+".png";
            var proposal=ToolCall.Create(ToolKind.ReviewGameArtAsset,PermissionKind.Write,"Exact live target review proposal only, no decision",
                ("project_path",project),("review_kind","visual"),("asset_set_id",id),("asset_path",asset),("expected_asset_sha256",hash),
                ("preview_path",preview),("expected_preview_sha256",await Hash(preview)),("expected_preview_provenance_sha256",await Hash(preview+".forgecore-art-preview.json")));
            var packet=await new GameArtReviewReader(guard,"6000.5.5f1").ReadAsync(proposal,default);
            using(var stream=new FileStream(Path.Combine(folder,recipe.Id+"-target-proposal.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,proposal);
            await Run("refined5-source-inspect-"+recipe.Id,ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Exact current refined source packet, no approval",("bundle_sha256",body.RootElement.GetProperty("bundleSha256").GetString()!)));
            Console.WriteLine("VERIFIED_REVIEW_PACKET "+recipe.Id+" "+packet.Proposal.ExpectedPreviewSha256);
        }
    }else if(args[0]=="--refined5-four-view") {
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-import-"+recipe.Id+".json")));
            using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
            var id="refined5-"+recipe.Id.Replace('_','-');var asset=body.RootElement.GetProperty("assetPath").GetString()!;var hash=body.RootElement.GetProperty("assetSha256").GetString()!;
            await Run("refined5-art-inspect-"+recipe.Id,Action("inspect_game_art_asset",("asset_path",asset),("expected_sha256",hash),("asset_set_id",id),("asset_role","environment_props"),("style_family","realistic_pbr")));
            await Run("refined5-art-preview-"+recipe.Id,Action("capture_game_art_preview",("asset_path",asset),("expected_sha256",hash),("asset_set_id",id),("output_path","Previews/RefinedReview/"+id+".png")));
        }
    }else if(args[0]=="--refined5-layout-gate") {
        using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined5-layout-source-plan.json")));
        using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
        if(!body.RootElement.GetProperty("placementAudit").GetProperty("nominalLayoutClear").GetBoolean()||body.RootElement.GetProperty("manifest").GetProperty("readyForAutomaticPlacement").GetBoolean())throw new IOException("Nominal audit must not grant placement.");
        var result=await tools.ExecuteAsync(ToolCall.Create(ToolKind.ImportIndustrialAssetSet,PermissionKind.Write,"Negative real-review gate; no approval is fabricated",("project_path",project),("level_id","refined-yard"),("recipe_set","refined_v1"),("plan_sha256",body.RootElement.GetProperty("planSha256").GetString()!)),default);
        if(result.Success||!result.Error!.Contains("Nothing imported",StringComparison.Ordinal))throw new IOException("Missing real source reviews did not block the refined set import.");
        var occupied=Directory.GetDirectories(guard.Resolve(project+"/Assets/ForgeCore/Generated/Blender",false),"refined-yard-*").Length;
        if(occupied!=0)throw new IOException("Negative refined gate wrote an import.");
        using(var stream=new FileStream(Path.Combine(evidence,"refined5-layout-review-gate.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{expectedBlocked=true,result.Error,importedFolders=occupied,nominalClearanceDoesNotGrantApproval=true});
        Console.WriteLine("REFINED_REAL_REVIEW_GATE_BLOCKED_ALL_IMPORTS");
    }else if(args[0] is "--refined4-plan" or "--refined5-plan" or "--refined5-layout-plan") {
        var revision=args[0]=="--refined5-layout-plan"?"refined5-layout":args[0].StartsWith("--refined5",StringComparison.Ordinal)?"refined5":"refined4";
        await Run(revision+"-source-plan",ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Exact refined recipe-set plan; retain all unmet live human reviews and do not import or place",
            ("project_path",project),("level_id","refined-yard"),("recipe_set","refined_v1")));
    }else if(args[0]=="--refined-props-v1-inspect") {
        foreach(var recipe in IndustrialPropRecipes.Refined) {
            using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"refined1-create-"+recipe.Id+".json")));
            using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
            await Run("refined1-inspect-"+recipe.Id,ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Hash-bound source review packet only; no human decision",
                ("bundle_sha256",body.RootElement.GetProperty("bundleSha256").GetString()!)));
        }
    }else if(args[0]=="--fetch-recorded-footsteps") {
        var intake=Path.Combine(Environment.CurrentDirectory,"work","recorded-footsteps-intake");Directory.CreateDirectory(intake);
        var intakeGuard=new WorkspacePathGuard(intake);var intakePolicy=new SecurityPolicy(intakeGuard,[],["opengameart.org"]);
        using var intakeTools=new ToolExecutor(intakeGuard,[],["opengameart.org"],128*1024);
        for(var i=1;i<=6;i++) {
            var remote=i==1?"01-footstep_0.ogg":$"{i:00}-footstep.ogg";var file=$"gbox-hardfloor-{i:00}.ogg";
            var stage=ToolCall.Create(ToolKind.StageWebAsset,PermissionKind.Network,"User-authorized passive CC0 recorded-footstep source, quarantine only",
                ("project_path","."),("file_name",file),("url","https://opengameart.org/sites/default/files/"+remote),
                ("source_page","https://opengameart.org/content/footsteps-0"),("license","CC0-1.0; GboxMikeFozzy; recorded subway walk; source checked 2026-10-30; human listening pending"));
            var decision=await intakePolicy.ValidateAsync(stage,default);if(decision.Decision!=SecurityDecision.ApprovalRequired)throw new IOException(decision.Reason);
            // Exact source selected and download authorized in this chat; no app policy or live host settings are changed.
            var staged=await intakeTools.ExecuteAsync(stage,default);if(!staged.Success)throw new IOException(staged.Error);Console.WriteLine(staged.Output);
            var inspected=await intakeTools.ExecuteAsync(ToolCall.Create(ToolKind.InspectStagedAsset,PermissionKind.Read,"Inspect quarantined passive OGG, not listening acceptance",("project_path","."),("file_name",file)),default);
            if(!inspected.Success)throw new IOException(inspected.Error);Console.WriteLine(inspected.Output);
        }
    }else if(args[0]=="--import-recorded-footsteps") {
        var intake=Path.Combine(Environment.CurrentDirectory,"work","recorded-footsteps-intake");
        var intakeGuard=new WorkspacePathGuard(intake);var intakePolicy=new SecurityPolicy(intakeGuard,[]);
        using var intakeTools=new ToolExecutor(intakeGuard,[]);
        var hashes=new[]{"33c9bef5e8aeb1069455699a34a0c5e1ef1787fd3f61594b0859d7e6bb9f9dec","f05396c807eb8eaecffb2a1ead611ec971c5a7914d1d081c91d7796e852692a2","9817cc93897ed1de5da87675b26be4196bfda3daa6f1de9dc4398a68d250cdb9","b373ff981e44d6f7ba2291225a4803078771029615ed81606136fdb9599906d5","f1b4893dd3cebe23438567a0dcaf047848a52b594e7eb03e5c212d99e83d2298","39147cc1b98abb4b5378ad07b15d50d18f2aa0f0fd2f5514f078c1e574d14773"};
        for(var i=1;i<=6;i++) {
            var file=$"gbox-hardfloor-{i:00}.ogg";var call=ToolCall.Create(ToolKind.ImportStagedAsset,PermissionKind.Write,"User-authorized exact-source footstep candidate, not human mix acceptance",
                ("project_path","."),("file_name",file),("destination_path","imported-recordings/"+file),("expected_sha256",hashes[i-1]));
            var decision=await intakePolicy.ValidateAsync(call,default);if(decision.Decision!=SecurityDecision.ApprovalRequired)throw new IOException(decision.Reason);
            var result=await intakeTools.ExecuteAsync(call,default);if(!result.Success)throw new IOException(result.Error);Console.WriteLine(result.Output);
        }
    }else if(args[0]=="--pack-recorded-footsteps") {
        var folder=Path.Combine(Environment.CurrentDirectory,"src","ForgeCore.Tools","Audio");Directory.CreateDirectory(folder);
        var entries=new List<object>();
        for(var i=0;i<6;i++){
            var file=$"gbox-hardfloor-{i+1:00}.ogg";var path=Path.Combine(Environment.CurrentDirectory,"work","recorded-footsteps-intake","imported-recordings",file);
            var bytes=await File.ReadAllBytesAsync(path);if(!ForgeCore.Demo.RecordedFootstepBank.MatchesSource(i,bytes))throw new IOException("Source hash changed before packing.");
            using var provenance=JsonDocument.Parse(await File.ReadAllBytesAsync(path+".asset.json"));
            if(provenance.RootElement.GetProperty("sha256").GetString()!=ForgeCore.Demo.RecordedFootstepBank.SourceHashes[i])throw new IOException("Imported provenance mismatch.");
            entries.Add(new{file=ForgeCore.Demo.RecordedFootstepBank.SourceFile(i),sha256=ForgeCore.Demo.RecordedFootstepBank.SourceHashes[i],frames=ForgeCore.Demo.RecordedFootstepBank.SourceFrames[i],base64=Convert.ToBase64String(bytes)});
        }
        await using(var stream=new FileStream(Path.Combine(folder,"RecordedFootsteps.json"),FileMode.CreateNew))
            await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,id=ForgeCore.Demo.RecordedFootstepBank.Id,creator=ForgeCore.Demo.RecordedFootstepBank.Creator,
                license=ForgeCore.Demo.RecordedFootstepBank.License,sourcePage=ForgeCore.Demo.RecordedFootstepBank.SourcePage,sourceCheckedDate="2026-10-30",
                sourceDescription="Six individual takes from the creator's recorded subway walk. Hard-floor candidate; not recordings of every surface.",
                sourceRightsEvidence="Creator upload labels CC0 and permits reuse. Source checked; not a guarantee of ownership or human sound-quality acceptance.",
                humanListeningAccepted=false,processingRecipe=ForgeCore.Demo.RecordedFootstepBank.ProcessingRecipe,files=entries},new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("Mechanically packed six hash-bound original passive recordings with source/rights evidence; no human acceptance inferred.");
    }else if(args[0]=="--recorded-v26-audition") {
        var folder=Path.Combine(Environment.CurrentDirectory,"outputs","ForgeCore-RecordedFootstepReview-20261030-v26");
        if(Directory.Exists(folder))throw new IOException("Audition exists; no overwrite.");
        var source=guard.Resolve(project+"/Assets/ForgeCore/CharacterAnimations/s10-recorded-footsteps-v26",false);
        using var report=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(source,"demo-audio-report.json")));
        if(report.RootElement.GetProperty("recordedBankId").GetString()!=ForgeCore.Demo.RecordedFootstepBank.Id)throw new IOException("Wrong source bank.");
        var clips=new short[6][];var auditionEvidence=new List<object>();Directory.CreateDirectory(folder);
        for(var i=0;i<6;i++){
            var file="Demo-"+ForgeCore.Demo.RecordedFootstepBank.Keys[i]+".wav";var bytes=await File.ReadAllBytesAsync(Path.Combine(source,file));
            var entry=report.RootElement.GetProperty("clips").EnumerateArray().Single(e=>e.GetProperty("file").GetString()==file);
            var sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();if(sha!=entry.GetProperty("sha256").GetString()||bytes.Length!=44+ForgeCore.Demo.RecordedFootstepBank.SourceFrames[i]*2)throw new IOException("Derivative changed.");
            clips[i]=new short[(bytes.Length-44)/2];Buffer.BlockCopy(bytes,44,clips[i],0,bytes.Length-44);File.Copy(Path.Combine(source,file),Path.Combine(folder,file),false);
            auditionEvidence.Add(new{file,sha256=sha,sourceSha256=ForgeCore.Demo.RecordedFootstepBank.SourceHashes[i]});
        }
        var sequence=new short[(int)(48000*(12*.43+.4))];
        for(var step=0;step<12;step++){var key=ForgeCore.Demo.RecordedFootstepBank.Key(ForgeCore.Demo.DemoFootstepSurface.Asphalt,step);var take=Array.IndexOf(ForgeCore.Demo.RecordedFootstepBank.Keys,key);Array.Copy(clips[take],0,sequence,(int)(step*.43*48000),clips[take].Length);}
        using(var stream=new FileStream(Path.Combine(folder,"Recorded-HardFloor-Walk-v1.wav"),FileMode.CreateNew))using(var w=new BinaryWriter(stream)){
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+sequence.Length*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
            w.Write(48000);w.Write(96000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(sequence.Length*2);foreach(var sample in sequence)w.Write(sample);
        }
        await using(var stream=new FileStream(Path.Combine(folder,"audio-review.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{bankId=ForgeCore.Demo.RecordedFootstepBank.Id,
            creator=ForgeCore.Demo.RecordedFootstepBank.Creator,license=ForgeCore.Demo.RecordedFootstepBank.License,sourcePage=ForgeCore.Demo.RecordedFootstepBank.SourcePage,
            processingRecipe=ForgeCore.Demo.RecordedFootstepBank.ProcessingRecipe,humanListeningAccepted=false,files=auditionEvidence},new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("Saved twelve-step recorded audition without playback: "+folder);
    }else if(args[0]=="--next-v28") {
        await Run("install-kit-next-v28",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Serialized durable automatic checkpoints off main thread; preserve exact profiled prior kit",("project_path",project),("template_id","first_person_action")));
        await Run("compile-next-v28",Action("compile"));
        await Run("build-next-v28",Action("build_windows",("scene_path","Assets/Scenes/S10-RecordedFootsteps-v26.unity"),("output_path","Builds/S10-NextSteps-v28/S10-NextSteps-v28.exe"),("development_build","true")));
    }else if(args[0]=="--next-v27") {
        await Run("install-kit-next-v27",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Bounded work/hitch timing and independent audio mix controls; exact previous kit backup",("project_path",project),("template_id","first_person_action")));
        await Run("compile-next-v27",Action("compile"));
        await Run("build-next-v27",Action("build_windows",("scene_path","Assets/Scenes/S10-RecordedFootsteps-v26.unity"),("output_path","Builds/S10-NextSteps-v27/S10-NextSteps-v27.exe"),("development_build","true")));
    }else if(args[0]=="--recorded-v26") {
        await Run("install-kit-recorded-v26",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install reusable exact-source CC0 recording bank; preserve verified prior kit and accepted rifle",("project_path",project),("template_id","first_person_action")));
        await Run("compile-recorded-v26",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-recorded-v26",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-recorded-footsteps-v26"),
            ("scene_path","Assets/Scenes/S10-RecordedFootsteps-v26.unity"),("output_path","Previews/S10-RecordedFootsteps-v26-poses.png")));
        await Run("build-recorded-v26",Action("build_windows",("scene_path","Assets/Scenes/S10-RecordedFootsteps-v26.unity"),("output_path","Builds/S10-RecordedFootsteps-v26/S10-RecordedFootsteps-v26.exe"),("development_build","true")));
    }else if(args[0]=="--audio-v25") {
        await Run("install-kit-audio-v25",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Reusable layered gunshots and grounded surface-aware footsteps, not an individual WAV replacement",("project_path",project),("template_id","first_person_action")));
        await Run("compile-audio-v25",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-audio-v25",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-audio-v25"),
            ("scene_path","Assets/Scenes/S10-Audio-v25.unity"),("output_path","Previews/S10-Audio-v25-poses.png")));
        await Run("build-audio-v25",Action("build_windows",("scene_path","Assets/Scenes/S10-Audio-v25.unity"),("output_path","Builds/S10-Audio-v25/S10-Audio-v25.exe"),("development_build","true")));
    }else if(args[0]=="--audio-v25-audition") {
        var folder=Path.Combine(Environment.CurrentDirectory,"outputs","ForgeCore-AudioReview-20261029-v25");
        if(Directory.Exists(folder))throw new IOException("Audition exists; no overwrite.");Directory.CreateDirectory(folder);
        var sources=new List<object>();
        void Audition(string file,ForgeCore.Demo.DemoFootstepSurface? surface) {
            var count=surface.HasValue?8:4;var spacing=surface.HasValue?.44:.95;
            var data=new float[(int)((count*spacing+.4)*ForgeCore.Demo.DemoAudioSynthesis.SampleRate)];
            for(var i=0;i<count;i++){var key=ForgeCore.Demo.DemoAudioSynthesis.Key(surface.HasValue?"footstep":"shot",surface??ForgeCore.Demo.DemoFootstepSurface.Asphalt,new[]{0,2,1,3}[i%4]);
                var clip=ForgeCore.Demo.DemoAudioSynthesis.Generate(key);var offset=(int)(i*spacing*ForgeCore.Demo.DemoAudioSynthesis.SampleRate);Array.Copy(clip,0,data,offset,clip.Length);}
            var path=Path.Combine(folder,file);using(var stream=new FileStream(path,FileMode.CreateNew))using(var w=new BinaryWriter(stream)){
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+data.Length*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
                w.Write(48000);w.Write(96000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(data.Length*2);foreach(var v in data)w.Write((short)Math.Round(v*32767));}
            sources.Add(new{file,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),seconds=data.Length/48000.0,surface=surface?.ToString()});
        }
        Audition("Rifle-v2.wav",null);foreach(ForgeCore.Demo.DemoFootstepSurface surface in Enum.GetValues(typeof(ForgeCore.Demo.DemoFootstepSurface)))Audition("Footsteps-"+surface+"-v2.wav",surface);
        await using(var stream=new FileStream(Path.Combine(folder,"audio-review.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{recipe=ForgeCore.Demo.DemoAudioSynthesis.Recipe,sampleRate=48000,humanListeningAccepted=false,source="Original local synthesis; not recordings or a realism certification",files=sources},new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("Original procedural auditions saved without playback: "+folder);
    }else if(args[0]=="--five-plan-s09") {
        await Run("five-plan-s09",ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Read source gates and exact target-review preparation proposals; no approval/import/placement",("project_path",project),("level_id","yard-mission")));
    }else if(args[0]=="--five-v22") {
        await Run("install-kit-five-v22",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Fresh mission terminal/options/checkpoint/original audio/preset technical candidate; protect exact prior kit",("project_path",project),("template_id","first_person_action")));
        await Run("compile-five-v22",Action("compile"));
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-five-v22",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-five-step-v22"),
            ("scene_path","Assets/Scenes/S10-Demo-v22.unity"),("output_path","Previews/S10-Demo-v22-poses.png")));
        await Run("build-five-v22",Action("build_windows",("scene_path","Assets/Scenes/S10-Demo-v22.unity"),("output_path","Builds/S10-Demo-v22/S10-Demo-v22.exe"),("development_build","true")));
    }else if(args[0] is "--five-v23" or "--five-v24") {
        var label=args[0]=="--five-v24"?"v24":"v23";
        await Run("install-kit-five-"+label,ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Correct owned diagnostic hierarchy/routing; preserve exact prior kit and all earlier builds",("project_path",project),("template_id","first_person_action")));
        await Run("build-five-"+label,Action("build_windows",("scene_path","Assets/Scenes/S10-Demo-v22.unity"),("output_path","Builds/S10-Demo-"+label+"/S10-Demo-"+label+".exe"),("development_build","true")));
    }else if(args[0] is "--five-v22-tests" or "--five-v23-tests" or "--five-v24-tests" or "--audio-v25-tests" or "--recorded-v26-tests" or "--next-v27-tests" or "--next-v28-tests") {
        var label=args[0]=="--next-v28-tests"?"next-v28":args[0]=="--next-v27-tests"?"next-v27":args[0]=="--recorded-v26-tests"?"recorded-v26":args[0]=="--audio-v25-tests"?"audio-v25":args[0]=="--five-v24-tests"?"v24":args[0]=="--five-v23-tests"?"v23":"v22";
        await Run("edit-tests-five-"+label,Action("run_tests",("test_mode","edit")));
        await Run("play-tests-five-"+label,Action("run_tests",("test_mode","play")));
    }else if(args[0] is "--render-five-v22-tests" or "--render-five-v23-tests" or "--render-five-v24-tests" or "--render-audio-v25-tests" or "--render-recorded-v26-tests" or "--render-next-v27-tests" or "--render-next-v28-tests" or "--render-refined5-tests" or "--render-graphics-v1-tests") {
        if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle.");
        var stem=args[0].Replace("--render-","rendered-").Replace("-tests","-playmode");var xml=Path.Combine(evidence,stem+".xml");var log=Path.Combine(evidence,stem+".log");
        if(File.Exists(xml)||File.Exists(log))throw new IOException("Rendered evidence exists; no overwrite.");
        var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var field in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",log})start.ArgumentList.Add(field);
        using var process=Process.Start(start)??throw new IOException("No rendered runner.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await process.WaitForExitAsync(timeout.Token);
        var result=XDocument.Load(xml).Root!;
        await using(var stream=new FileStream(Path.Combine(evidence,stem+".json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{exitCode=process.ExitCode,attributes=result.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
        if(process.ExitCode!=0||result.Attribute("result")?.Value!="Passed"||result.Attribute("failed")?.Value!="0"||result.Attribute("skipped")?.Value!="0")throw new IOException("Rendered complete-demo tests failed or skipped; retain evidence.");
        Console.WriteLine("Rendered complete-demo PlayMode passed: "+xml);
    }else if(args[0]=="--props")foreach(var recipe in IndustrialPropRecipes.All) {
        var fields=new List<(string Key,string Value)>{("project_path",project),("asset_name","s08-"+recipe.Id.Replace('_','-')),("recipe",recipe.Id),("seed","83"),("height_mm",recipe.DefaultHeight.ToString())};
        if(recipe.IsBarrel)fields.Add(("radius_mm","340"));
        await Run("create-"+recipe.Id,ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Create exact original industrial prop source",fields.ToArray()));
    }else if(args[0] is "--import-props" or "--import-props-authorized")foreach(var recipe in IndustrialPropRecipes.All) {
        using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"create-"+recipe.Id+".json")));
        using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
        await Run("import-"+recipe.Id+(args[0]=="--import-props-authorized"?"-authorized":""),ToolCall.Create(ToolKind.ImportBlenderAsset,PermissionKind.Write,"Import exact industrial source for a technical Unity review, not acceptance",
            ("project_path",project),("asset_name","s08-"+recipe.Id.Replace('_','-')),("bundle_sha256",body.RootElement.GetProperty("bundleSha256").GetString()!),("expected_asset_sha256",body.RootElement.GetProperty("assetSha256").GetString()!)));
    }else if(args[0]=="--install") {
        await Run("install-bridge",ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Install exact verified bridge with protected prior-version backup",("project_path",project)));
        await Run("install-kit",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install exact verified industrial kit with protected prior-version backup",("project_path",project),("template_id","first_person_action")));
        await Run("compile",Action("compile"));
    }else if(args[0]=="--install-runtime-tests") {
        await Run("install-kit-runtime-tests",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Upgrade exact kit to include five actual placed-prop controller collision regressions",("project_path",project),("template_id","first_person_action")));
        await Run("compile-runtime-tests",Action("compile"));
    }else if(args[0]=="--install-mission-fix") {
        await Run("install-bridge-mission-fix",ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Preserve inner builder diagnostic stacks through the trusted bridge",("project_path",project)));
        await Run("install-kit-mission-fix",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Fix editor-time mission route checks without invoking play-mode lifecycle",("project_path",project),("template_id","first_person_action")));
        await Run("compile-mission-fix",Action("compile"));
    }else if(args[0]=="--install-bridge-version-fix") {
        await Run("install-bridge-version-fix",ToolCall.Create(ToolKind.InstallUnityBridge,PermissionKind.Write,"Bind embedded bridge version to one source of truth; retain exact prior backup",("project_path",project)));
        await Run("compile-bridge-version-fix",Action("compile"));
    }else if(args[0]=="--install-campaign-discovery-fix") {
        await Run("install-kit-campaign-discovery",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Avoid repeatedly loading unrelated lab scenes during campaign test discovery; keep all assertions",("project_path",project),("template_id","first_person_action")));
        await Run("compile-campaign-discovery",Action("compile"));
    }else if(args[0]=="--install-route-test-fix") {
        await Run("install-kit-route-test",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Correct route test encounter order without weakening controller travel checks",("project_path",project),("template_id","first_person_action")));
        await Run("compile-route-test",Action("compile"));
    }else if(args[0]=="--install-render-probe") {
        await Run("install-kit-render-probe-v7",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Add opt-in development-player actual gameplay render diagnostics; preserve previous kit",("project_path",project),("template_id","first_person_action")));
        await Run("compile-render-probe-v7",Action("compile"));
    }else if(args[0]=="--powerbox-v20") {
        await Run("install-kit-powerbox-v20",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Original bounded courtyard presentation with protected prior-kit backup; no library review bypass",("project_path",project),("template_id","first_person_action")));
        var asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        await Run("mission-powerbox-v20",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-powerbox-v20"),
            ("scene_path","Assets/Scenes/S10-PowerBox-v20.unity"),("output_path","Previews/S10-PowerBox-v20-pose.png")));
        await Run("preview-powerbox-v20",Action("capture_scene_preview",("scene_path","Assets/Scenes/S10-PowerBox-v20.unity"),("output_path","Previews/S10-PowerBox-v20-live.png"),("duration_seconds","3")));
        await Run("build-powerbox-v20",Action("build_windows",("scene_path","Assets/Scenes/S10-PowerBox-v20.unity"),("output_path","Builds/S10-PowerBox-v20/S10-PowerBox-v20.exe"),("development_build","true")));
    }else if(args[0]=="--powerbox-v20-tests") {
        await Run("edit-tests-powerbox-v20",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-powerbox-v20",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--render-powerbox-v20-tests") {
        if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle.");
        var stem="rendered-powerbox-v20-playmode";var xml=Path.Combine(evidence,stem+".xml");var log=Path.Combine(evidence,stem+".log");
        if(File.Exists(xml)||File.Exists(log))throw new IOException("Rendered evidence exists; no overwrite.");
        var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var field in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",log})start.ArgumentList.Add(field);
        using var process=Process.Start(start)??throw new IOException("No rendered runner.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await process.WaitForExitAsync(timeout.Token);
        var result=XDocument.Load(xml).Root!;
        await using(var stream=new FileStream(Path.Combine(evidence,stem+".json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{exitCode=process.ExitCode,attributes=result.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
        if(process.ExitCode!=0||result.Attribute("result")?.Value!="Passed"||result.Attribute("failed")?.Value!="0"||result.Attribute("skipped")?.Value!="0")throw new IOException("Rendered courtyard tests failed or skipped; inspect retained evidence.");
        Console.WriteLine("Rendered courtyard PlayMode passed: "+xml);
    }else if(args[0]=="--powerbox-v21") {
        await Run("install-kit-powerbox-v21",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Correct generator outcome wording to disclose new cabinet collision; preserve exact prior kit",("project_path",project),("template_id","first_person_action")));
        await Run("build-powerbox-v21",Action("build_windows",("scene_path","Assets/Scenes/S10-PowerBox-v20.unity"),("output_path","Builds/S10-PowerBox-v21/S10-PowerBox-v21.exe"),("development_build","true")));
    }else if(args[0]=="--powerbox-v21-tests") {
        await Run("edit-tests-powerbox-v21",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-powerbox-v21",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--render-powerbox-v21-tests") {
        if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle.");
        var stem="rendered-powerbox-v21-playmode";var xml=Path.Combine(evidence,stem+".xml");var log=Path.Combine(evidence,stem+".log");
        if(File.Exists(xml)||File.Exists(log))throw new IOException("Rendered evidence exists; no overwrite.");
        var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var field in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",log})start.ArgumentList.Add(field);
        using var process=Process.Start(start)??throw new IOException("No rendered runner.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await process.WaitForExitAsync(timeout.Token);
        var result=XDocument.Load(xml).Root!;
        await using(var stream=new FileStream(Path.Combine(evidence,stem+".json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{exitCode=process.ExitCode,attributes=result.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
        if(process.ExitCode!=0||result.Attribute("result")?.Value!="Passed"||result.Attribute("failed")?.Value!="0"||result.Attribute("skipped")?.Value!="0")throw new IOException("Rendered courtyard tests failed or skipped; inspect retained evidence.");
        Console.WriteLine("Rendered courtyard PlayMode passed: "+xml);
    }else if(args[0]=="--courtyard-v19") {
        await Run("install-kit-courtyard-v19",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Original bounded courtyard presentation with protected prior-kit backup; no library review bypass",("project_path",project),("template_id","first_person_action")));
        var asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        await Run("mission-courtyard-v19",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-courtyard-v19"),
            ("scene_path","Assets/Scenes/S10-Courtyard-v19.unity"),("output_path","Previews/S10-Courtyard-v19-pose.png")));
        await Run("preview-courtyard-v19",Action("capture_scene_preview",("scene_path","Assets/Scenes/S10-Courtyard-v19.unity"),("output_path","Previews/S10-Courtyard-v19-live.png"),("duration_seconds","3")));
        await Run("build-courtyard-v19",Action("build_windows",("scene_path","Assets/Scenes/S10-Courtyard-v19.unity"),("output_path","Builds/S10-Courtyard-v19/S10-Courtyard-v19.exe"),("development_build","true")));
    }else if(args[0]=="--courtyard-v19-tests") {
        await Run("edit-tests-courtyard-v19",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-courtyard-v19",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--render-courtyard-v19-tests") {
        if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle.");
        var stem="rendered-courtyard-v19-playmode";var xml=Path.Combine(evidence,stem+".xml");var log=Path.Combine(evidence,stem+".log");
        if(File.Exists(xml)||File.Exists(log))throw new IOException("Rendered evidence exists; no overwrite.");
        var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var field in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",log})start.ArgumentList.Add(field);
        using var process=Process.Start(start)??throw new IOException("No rendered runner.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await process.WaitForExitAsync(timeout.Token);
        var result=XDocument.Load(xml).Root!;
        await using(var stream=new FileStream(Path.Combine(evidence,stem+".json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{exitCode=process.ExitCode,attributes=result.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
        if(process.ExitCode!=0||result.Attribute("result")?.Value!="Passed"||result.Attribute("failed")?.Value!="0"||result.Attribute("skipped")?.Value!="0")throw new IOException("Rendered courtyard tests failed or skipped; inspect retained evidence.");
        Console.WriteLine("Rendered courtyard PlayMode passed: "+xml);
    }else if(args[0]=="--install-corpse-ground-v16") {
        await Run("install-kit-corpse-ground-v16",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Final independent hand-support loop correction; retain verified 5mm torso contact and living motion",("project_path",project),("template_id","first_person_action")));
        await Run("build-corpse-ground-v16",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-CorpseGround-v16/S10-CorpseGround-v16.exe"),("development_build","true")));
    }else if(args[0]=="--corpse-ground-v16-tests") {
        await Run("edit-tests-corpse-ground-v16",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-corpse-ground-v16",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-corpse-ground-v15") {
        await Run("install-kit-corpse-ground-v15",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Measure torso/hand support and relax hands to allow core-body floor contact without penetration",("project_path",project),("template_id","first_person_action")));
        await Run("build-corpse-ground-v15",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-CorpseGround-v15/S10-CorpseGround-v15.exe"),("development_build","true")));
    }else if(args[0]=="--corpse-ground-v15-tests") {
        await Run("edit-tests-corpse-ground-v15",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-corpse-ground-v15",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-corpse-ground-v14") {
        await Run("install-kit-corpse-ground-v14",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Relax death-only arm brace and require torso within 15mm of floor; preserve living motion",("project_path",project),("template_id","first_person_action")));
        await Run("build-corpse-ground-v14",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-CorpseGround-v14/S10-CorpseGround-v14.exe"),("development_build","true")));
    }else if(args[0]=="--corpse-ground-v14-tests") {
        await Run("edit-tests-corpse-ground-v14",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-corpse-ground-v14",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-corpse-ground-v13") {
        await Run("install-kit-corpse-ground-v13",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Settle death-only torso/leg support onto floor without changing living curves",("project_path",project),("template_id","first_person_action")));
        await Run("build-corpse-ground-v13",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-CorpseGround-v13/S10-CorpseGround-v13.exe"),("development_build","true")));
    }else if(args[0]=="--corpse-ground-v13-tests") {
        await Run("edit-tests-corpse-ground-v13",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-corpse-ground-v13",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-corpse-probe") {
        await Run("install-kit-corpse-probe-v12",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Add opt-in independent animated corpse/floor measurement and side-camera evidence",("project_path",project),("template_id","first_person_action")));
        await Run("build-corpse-probe-v12",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-CorpseDiagnostic-v12/S10-CorpseDiagnostic-v12.exe"),("development_build","true")));
    }else if(args[0]=="--install-soft-flash-v11") {
        await Run("install-kit-soft-flash-v11",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Exercise authored mission power gate before guard render check; no motion changes",("project_path",project),("template_id","first_person_action")));
        await Run("build-soft-flash-v11",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-SoftFlash-v11/S10-SoftFlash-v11.exe"),("development_build","true")));
    }else if(args[0]=="--soft-flash-v11-tests") {
        await Run("edit-tests-soft-flash-v11",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-soft-flash-v11",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-soft-flash-v10") {
        await Run("install-kit-soft-flash-v10",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Refine soft axial flash and exercise guard's normal AI firing in standalone diagnostic",("project_path",project),("template_id","first_person_action")));
        await Run("build-soft-flash-v10",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-SoftFlash-v10/S10-SoftFlash-v10.exe"),("development_build","true")));
    }else if(args[0]=="--soft-flash-v10-tests") {
        await Run("edit-tests-soft-flash-v10",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-soft-flash-v10",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-soft-flash") {
        await Run("install-kit-soft-flash-v9",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Explicitly bundle soft feedback shader and replace solid triangle flash; preserve living animation",("project_path",project),("template_id","first_person_action")));
        await Run("build-soft-flash-v9",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-SoftFlash-v9/S10-SoftFlash-v9.exe"),("development_build","true")));
    }else if(args[0]=="--soft-flash-tests") {
        await Run("edit-tests-soft-flash-v9",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-soft-flash-v9",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--install-render-probe-correction") {
        await Run("install-kit-render-probe-v8",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Correct diagnostic input constructor; no gameplay changes",("project_path",project),("template_id","first_person_action")));
        await Run("build-render-probe-v8",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-FlashDiagnostic-v8/S10-FlashDiagnostic-v8.exe"),("development_build","true")));
    }else if(args[0]=="--compile-render-probe-authorized") {
        await Run("compile-render-probe-v7-normal-profile",Action("compile"));
    }else if(args[0]=="--render-probe-build") {
        await Run("build-render-probe-v7",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-FlashDiagnostic-v7/S10-FlashDiagnostic-v7.exe"),("development_build","true")));
    }else if(args[0]=="--install-death-arm-fix") {
        await Run("install-kit-flash-death-v6",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Keep accepted living clips; settle death hands close to torso and orient all muzzle flashes",("project_path",project),("template_id","first_person_action")));
        await Run("compile-flash-death-v6",Action("compile"));
    }else if(args[0]=="--flash-death-v6-mission") {
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-flash-death-v6",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-industrial-flash-death-v6"),
            ("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Previews/S10-IndustrialFlashDeath-v6-poses.png")));
    }else if(args[0]=="--flash-death-v6-tests") {
        await Run("edit-tests-flash-death-v6",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-flash-death-v6",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--flash-death-v6-build") {
        await Run("build-flash-death-v6",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v6.unity"),("output_path","Builds/S10-IndustrialFlashDeath-v6/S10-IndustrialFlashDeath-v6.exe"),("development_build","true")));
    }else if(args[0]=="--flash-death-v6-smoke") {
        await Run("smoke-flash-death-v6",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Five-second exact corrected flash/death candidate startup",("project_path",project),("output_path","Builds/S10-IndustrialFlashDeath-v6/S10-IndustrialFlashDeath-v6.exe"),("duration_seconds","5")));
    }else if(args[0]=="--install-flash-death") {
        await Run("install-kit-flash-death-v5",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Preserve accepted living movement; add owned warm muzzle effects and death-only refinement",("project_path",project),("template_id","first_person_action")));
        await Run("compile-flash-death-v5",Action("compile"));
    }else if(args[0]=="--flash-death-mission") {
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-flash-death-v5",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-industrial-flash-death-v5"),
            ("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v5.unity"),("output_path","Previews/S10-IndustrialFlashDeath-v5-poses.png")));
    }else if(args[0]=="--flash-death-tests") {
        await Run("edit-tests-flash-death-v5",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-flash-death-v5",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--flash-death-build") {
        await Run("build-flash-death-v5",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialFlashDeath-v5.unity"),("output_path","Builds/S10-IndustrialFlashDeath-v5/S10-IndustrialFlashDeath-v5.exe"),("development_build","true")));
    }else if(args[0]=="--flash-death-smoke") {
        await Run("smoke-flash-death-v5",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Five-second exact flash/death candidate startup",("project_path",project),("output_path","Builds/S10-IndustrialFlashDeath-v5/S10-IndustrialFlashDeath-v5.exe"),("duration_seconds","5")));
    }else if(args[0]=="--install-motion-refinement") {
        await Run("install-kit-motion-v2",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Install exact motion v2 candidate with protected 0.16.57 backup",("project_path",project),("template_id","first_person_action")));
        await Run("compile-motion-v2",Action("compile"));
    }else if(args[0]=="--install-motion-grip-fix") {
        await Run("install-kit-motion-grip-v3",ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"Separate stationary firing foregrip from moving reload magazine; preserve exact old kit",("project_path",project),("template_id","first_person_action")));
        await Run("compile-motion-grip-v3",Action("compile"));
    }else if(args[0]=="--motion-grip-tests") {
        await Run("edit-tests-motion-grip-v3",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-motion-grip-v3",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--motion-grip-mission") {
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-motion-grip-v4",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-industrial-motion-v4"),
            ("scene_path","Assets/Scenes/S10-IndustrialMotion-v4.unity"),("output_path","Previews/S10-IndustrialMotion-v4-poses.png")));
    }else if(args[0]=="--motion-refinement-tests") {
        await Run("edit-tests-motion-v2",Action("run_tests",("test_mode","edit")));
        await Run("play-tests-motion-v2",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--motion-refinement-mission") {
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-motion-v3",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-industrial-motion-v3"),
            ("scene_path","Assets/Scenes/S10-IndustrialMotion-v3.unity"),("output_path","Previews/S10-IndustrialMotion-v3-poses.png")));
    }else if(args[0]=="--motion-grip-build") {
        await Run("build-motion-grip-v4",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialMotion-v4.unity"),("output_path","Builds/S10-IndustrialMotion-v4/S10-IndustrialMotion-v4.exe"),("development_build","true")));
    }else if(args[0]=="--motion-grip-smoke") {
        await Run("smoke-motion-grip-v4",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Five-second exact v4 player startup, not human motion approval",("project_path",project),("output_path","Builds/S10-IndustrialMotion-v4/S10-IndustrialMotion-v4.exe"),("duration_seconds","5")));
    }else if(args[0]=="--motion-refinement-build") {
        await Run("build-motion-v3",Action("build_windows",("scene_path","Assets/Scenes/S10-IndustrialMotion-v3.unity"),("output_path","Builds/S10-IndustrialMotion-v3/S10-IndustrialMotion-v3.exe"),("development_build","true")));
    }else if(args[0]=="--motion-refinement-smoke") {
        await Run("smoke-motion-v3",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Five-second hash-bound motion candidate startup, not visual acceptance",("project_path",project),("output_path","Builds/S10-IndustrialMotion-v3/S10-IndustrialMotion-v3.exe"),("duration_seconds","5")));
    }else if(args[0] is "--prepare-props" or "--prepare-props-retry")foreach(var recipe in IndustrialPropRecipes.All) {
        var sourceId="s08-"+recipe.Id.Replace('_','-');var id=sourceId+(args[0]=="--prepare-props-retry"&&recipe.IsBarrel?"-attempt2":"");var asset=$"Assets/ForgeCore/Generated/Blender/{sourceId}/{sourceId}.glb";
        await Run("prepare-"+recipe.Id+(args[0]=="--prepare-props-retry"&&recipe.IsBarrel?"-attempt2":""),Action("prepare_industrial_prop",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id",id),
            ("scene_path","Assets/Scenes/S08-"+id+".unity"),("output_path","Previews/S08-"+id+".png")));
    }else if(args[0]=="--mission") {
        const string asset="Assets/ForgeCore/Generated/Blender/s04-industrial-security-v1/s04-industrial-security-v1.glb";
        if(await Hash(asset)!="ccb702f8dad9a98645692cccbadb137d53554c7b1093d65c210f134abfaaabf0")throw new IOException("Original guard changed.");
        await Run("mission-v2",Action("create_industrial_mission",("asset_path",asset),("expected_sha256",await Hash(asset)),("asset_set_id","s10-industrial-infiltration-v2"),
            ("scene_path",missionScene),("output_path","Previews/S10-IndustrialInfiltration-v2-poses.png")));
    }else if(args[0]=="--plan-layout-v2") {
        await Run("manifest-layout-v2",ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Bind ten authored positions/dimensions to exact eligible source identities without placing or approving assets",("project_path",project),("level_id","yard-mission")));
    }else if(args[0]=="--plan") {
        await Run("manifest",ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Read exact current industrial kit requirements and review gates",("project_path",project),("level_id","yard-mission")));
    }else if(args[0]=="--review-gate") {
        using var receipt=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(evidence,"manifest.json")));
        using var body=JsonDocument.Parse(receipt.RootElement.GetProperty("Output").GetString()!);
        var manifest=body.RootElement.GetProperty("manifest");
        if(manifest.GetProperty("readyForTargetPreparation").GetBoolean()||manifest.GetProperty("readyForAutomaticPlacement").GetBoolean())throw new IOException("Expected current unreviewed sources, not automatic approval.");
        var call=ToolCall.Create(ToolKind.ImportIndustrialAssetSet,PermissionKind.Write,"Negative test: real missing human reviews must block all imports",("project_path",project),("level_id","yard-mission"),("plan_sha256",body.RootElement.GetProperty("planSha256").GetString()!));
        var result=await tools.ExecuteAsync(call,default);
        if(result.Success||!result.Error!.Contains("Nothing imported",StringComparison.Ordinal))throw new IOException("Missing-review gate did not fail closed.");
        var imports=guard.Resolve(project+"/Assets/ForgeCore/Generated/Blender",false);
        if(Directory.GetDirectories(imports,"yard-mission-*").Length!=0)throw new IOException("Negative gate wrote an import.");
        await using(var stream=new FileStream(Path.Combine(evidence,"review-gate.json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{expectedBlocked=true,result.Error,importedFolders=0,qualification="Real source reviews/promotions were not fabricated."});
        Console.WriteLine("REAL_REVIEW_GATE_BLOCKED_ALL_IMPORTS");
    }else if(args[0] is "--tests" or "--tests-discovery-fixed" or "--tests-final") {
        await Run(args[0]=="--tests-final"?"edit-tests-final":args[0]=="--tests-discovery-fixed"?"edit-tests-discovery-fixed":"edit-tests",Action("run_tests",("test_mode","edit")));
        await Run(args[0]=="--tests-final"?"play-tests-final":"play-tests",Action("run_tests",("test_mode","play")));
    }else if(args[0]=="--preview") {
        await Run("mission-preview",Action("capture_scene_preview",("scene_path",missionScene),("output_path","Previews/S10-IndustrialInfiltration-v2-live.png"),("duration_seconds","5")));
    }else if(args[0]=="--build") {
        await Run("build",Action("build_windows",("scene_path",missionScene),("output_path",missionExe),("development_build","true")));
    }else if(args[0]=="--smoke") {
        await Run("smoke",ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Five-second hash-bound player startup, not full playthrough",("project_path",project),("output_path",missionExe),("duration_seconds","5")));
    }else if(args[0] is "--render-tests" or "--render-motion-tests" or "--render-flash-death-tests" or "--render-flash-death-v6-tests" or "--render-soft-flash-tests" or "--render-soft-flash-v10-tests" or "--render-soft-flash-v11-tests" or "--render-corpse-ground-v13-tests" or "--render-corpse-ground-v14-tests" or "--render-corpse-ground-v15-tests" or "--render-corpse-ground-v16-tests") {
        if(Process.GetProcessesByName("Unity").Length!=0)throw new IOException("Unity must be idle.");
        var stem=args[0]=="--render-corpse-ground-v16-tests"?"rendered-corpse-ground-v16-playmode":args[0]=="--render-corpse-ground-v15-tests"?"rendered-corpse-ground-v15-playmode":args[0]=="--render-corpse-ground-v14-tests"?"rendered-corpse-ground-v14-playmode":args[0]=="--render-corpse-ground-v13-tests"?"rendered-corpse-ground-v13-playmode":args[0]=="--render-soft-flash-v11-tests"?"rendered-soft-flash-v11-playmode":args[0]=="--render-soft-flash-v10-tests"?"rendered-soft-flash-v10-playmode":args[0]=="--render-soft-flash-tests"?"rendered-soft-flash-v9-playmode":args[0]=="--render-flash-death-v6-tests"?"rendered-flash-death-v6-playmode":args[0]=="--render-flash-death-tests"?"rendered-flash-death-playmode":args[0]=="--render-motion-tests"?"rendered-motion-playmode":"rendered-playmode";
        var xml=Path.Combine(evidence,stem+".xml");var log=Path.Combine(evidence,stem+".log");
        if(File.Exists(xml)||File.Exists(log))throw new IOException("Rendered test evidence exists; no replay.");
        var start=new ProcessStartInfo(editor){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var field in new[]{"-batchmode","-projectPath",guard.Resolve(project,false),"-runTests","-testPlatform","PlayMode","-testResults",xml,"-logFile",log})start.ArgumentList.Add(field);
        using var process=Process.Start(start)??throw new IOException("No rendered runner.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await process.WaitForExitAsync(timeout.Token);
        var root=XDocument.Load(xml).Root!;
        await using(var stream=new FileStream(Path.Combine(evidence,stem+".json"),FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{exitCode=process.ExitCode,attributes=root.Attributes().ToDictionary(a=>a.Name.LocalName,a=>a.Value),graphicsDeviceRequired=true});
        if(process.ExitCode!=0||root.Attribute("result")?.Value!="Passed"||root.Attribute("failed")?.Value!="0")throw new IOException("Rendered PlayMode failed; inspect retained XML/log.");
        Console.WriteLine("Rendered PlayMode passed: "+xml);
    }else throw new ArgumentException("Unknown bounded mode.");
}catch(Exception e){Console.Error.WriteLine("S08_S10_STOPPED "+e.GetType().Name+": "+string.Join(' ',e.Message.Split('\n').Take(5)));Environment.ExitCode=1;}
ToolCall Action(string operation,params(string Key,string Value)[] fields)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,operation,new[]{("project_path",project),("operation",operation)}.Concat(fields).ToArray());
async Task<string> Hash(string relative)=>Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(guard.Resolve(project+"/"+relative,false)))).ToLowerInvariant();
async Task Run(string step,ToolCall call) {
    var path=Path.Combine(evidence,step+".json");if(File.Exists(path)) {
        using var old=JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        if(old.RootElement.GetProperty("Success").GetBoolean()){Console.WriteLine("EXISTING RECEIPT, no replay "+step);return;}
        throw new IOException("Failed receipt retained; explicitly choose a fresh diagnostic attempt before retry.");
    }
    var check=await policy.ValidateAsync(call,default);if(check.Decision==SecurityDecision.Denied)throw new InvalidOperationException(check.Reason);
    Console.WriteLine("START "+step);var result=await tools.ExecuteAsync(call,default);
    await using(var stream=new FileStream(path,FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{call.Kind,call.Arguments,result.Success,result.Output,result.Error},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine(result.Success?result.Output:result.Error?.Split('\n')[0]);
    if(!result.Success)throw new IOException("Production tool failed: "+step);
}
sealed class NoProviderFallback:ILocalModel
{
    public ModelStatus Status=>new("bounded-verification",true,"No inference/provider allowed");
    public Task<AgentPlan> CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new InvalidOperationException("This probe must not invoke any inference, model service or provider fallback.");
}
sealed class ReadOnlyProbeExecutor(IToolExecutor inner):IToolExecutor
{
    public int Executed{get;private set;}
    public async Task<ToolResult> ExecuteAsync(ToolCall call,CancellationToken token) {
        if(call.Kind is not (ToolKind.ReadTextFile or ToolKind.InspectLocalBlenderAsset))throw new InvalidOperationException("Read-only cancellation probe forbids every write/editor/review operation.");
        Executed++;return await inner.ExecuteAsync(call,token);
    }
}
sealed class CancelOnlyVerifiedTarget(IGameArtReviewReader reader,GameArtReviewSpec expected):IApprovalService
{
    public int Requests{get;private set;}
    public async Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request,CancellationToken token) {
        if(request.Call.Kind!=ToolKind.ReviewGameArtAsset||GameArtReviewSpec.FromProposal(request.Call)!=expected)throw new InvalidOperationException("Only the exact target boundary is permitted in this synthetic probe.");
        var packet=await reader.ReadAsync(request.Call,token);if(packet.Proposal!=expected)throw new IOException("Probe target changed.");
        Requests++;return ApprovalDecision.Cancelled;
    }
}
sealed class ProbeMemoryStore:IForgeStore
{
    public List<ForgeTaskState> States{get;}=[];
    public Task InitializeAsync(CancellationToken token=default)=>Task.CompletedTask;
    public Task<Guid>CreateConversationAsync(string title,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
    public Task RenameConversationAsync(Guid id,string title,CancellationToken token)=>Task.CompletedTask;
    public Task AddMessageAsync(Guid id,string role,string content,CancellationToken token)=>Task.CompletedTask;
    public Task CreateTaskAsync(Guid id,Guid conversation,string request,ForgeTaskState state,CancellationToken token){States.Add(state);return Task.CompletedTask;}
    public Task UpdateTaskStateAsync(Guid id,ForgeTaskState state,CancellationToken token){States.Add(state);return Task.CompletedTask;}
    public Task<TaskSummary?>GetTaskAsync(Guid id,CancellationToken token)=>Task.FromResult<TaskSummary?>(null);
    public Task RecordToolCallAsync(Guid id,ToolCall call,string status,ToolResult? result,CancellationToken token)=>Task.CompletedTask;
    public Task AuditAsync(AuditEntry entry,CancellationToken token)=>Task.CompletedTask;
    public Task<IReadOnlyList<ConversationSummary>>GetRecentConversationsAsync(int limit,CancellationToken token)=>Task.FromResult<IReadOnlyList<ConversationSummary>>([]);
    public Task<IReadOnlyList<StoredMessage>>GetMessagesAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<StoredMessage>>([]);
    public Task<IReadOnlyList<TaskSummary>>GetRecentTasksAsync(Guid id,int limit,CancellationToken token)=>Task.FromResult<IReadOnlyList<TaskSummary>>([]);
    public Task<IReadOnlyList<AuditEvent>>GetRecentAuditEventsAsync(int limit,CancellationToken token)=>Task.FromResult<IReadOnlyList<AuditEvent>>([]);
}
