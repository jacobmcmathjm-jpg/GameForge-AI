using System.IO;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Desktop;

/// <summary>Packaged-app planner smoke test; deliberately never starts the host, database, model or editor.</summary>
internal static class CourtyardPipelineProbe
{
    public static async Task RunAsync(string output,bool retro=false,bool connected=false)
    {
        if(!Path.IsPathFullyQualified(output)||File.Exists(output)||Directory.Exists(output))throw new IOException("A fresh absolute output path is required.");
        await using var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");
        var observations=new List<ToolObservation>();var calls=new List<ToolCall>();string response="";
        for(var turn=0;turn<12;turn++)
        {
            var plan=await model.CreatePlanAsync(new(connected?"Create a connected industrial level named connected-yard.":retro?"Create a retro industrial courtyard named retro-yard.":"Create a detailed industrial courtyard named graphics-yard.",[],observations,turn,12-turn),default);
            if(plan.Actions.Count==0){response=plan.Response;break;}
            if(plan.Actions.Count!=1)throw new InvalidDataException("Bounded route must propose exactly one action.");
            var call=plan.Actions[0];calls.Add(call);
            if(call.Kind==ToolKind.UnityEditorAction)_=UnityEditorActionSpec.From(call);
            else if(call.Kind==ToolKind.CreateBlenderAsset)_=BlenderAssetSpec.From(call);
            else if(call.Kind==ToolKind.ImportBlenderAsset)_=BlenderAssetImportSpec.From(call);
            else if(call.Kind==ToolKind.UnityPlayerSmokeTest)_=UnityPlayerSmokeTestSpec.From(call);
            else if(call.Kind is not ToolKind.InstallUnityBridge and not ToolKind.ScaffoldUnityGameTemplate)throw new InvalidDataException("Unexpected route action.");
            var detail=call.Kind==ToolKind.CreateBlenderAsset?JsonSerializer.Serialize(new{bundleSha256=new string('a',64),assetSha256=new string('b',64)}):"Synthetic planner observation; no tool execution or quality approval.";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        }
        if(calls.Count!=11||calls[^1].Kind!=ToolKind.UnityPlayerSmokeTest||!response.Contains("Windows EXE",StringComparison.Ordinal)||
            !calls.Where(c=>c.Kind==ToolKind.UnityEditorAction).Select(c=>c.Arguments["operation"]).SequenceEqual(new[]{"compile","inspect_character_rig","create_industrial_mission","capture_scene_preview","profile_scene","build_windows"}))
            throw new InvalidDataException("Packaged courtyard route did not finish its exact eleven-step sequence.");
        var mission=calls.Single(c=>c.Kind==ToolKind.UnityEditorAction&&c.Arguments["operation"]=="create_industrial_mission");
        if(UnityEditorActionSpec.From(mission).StyleFamily!=(retro?RetroCourtyardProfile.Id:null)||calls.Where(c=>c.Kind==ToolKind.UnityEditorAction&&c.Id!=mission.Id).Any(c=>UnityEditorActionSpec.From(c).StyleFamily!=null))throw new InvalidDataException("Packaged profile must be explicit and mission-only.");
        if(UnityEditorActionSpec.From(mission).LayoutProfile!=(connected?ConnectedIndustrialProfile.Id:null)||calls.Where(c=>c.Kind==ToolKind.UnityEditorAction&&c.Id!=mission.Id).Any(c=>UnityEditorActionSpec.From(c).LayoutProfile!=null))throw new InvalidDataException("Packaged layout must be exact and mission-only.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using var stream=new FileStream(output,FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,kind="packaged_app_courtyard_planner_probe",selectedStyle=retro?RetroCourtyardProfile.Id:"default",selectedLayout=connected?ConnectedIndustrialProfile.Id:"original_courtyard",actionCount=calls.Count,calls,response,
            realToolExecution=false,providerFallback=false,databaseOpened=false,humanArtAccepted=false,
            qualification="Actual packaged desktop assembly executing its deterministic planner with synthetic observations. Not a normal UI conversation, Unity run or gameplay proof."},new JsonSerializerOptions{WriteIndented=true});
    }
    private sealed class NoInference:ILocalModel
    {
        public ModelStatus Status=>new("offline-probe",true,"No model/provider access");
        public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken token)=>throw new InvalidOperationException("Courtyard route must not use inference/provider fallback.");
    }
}
