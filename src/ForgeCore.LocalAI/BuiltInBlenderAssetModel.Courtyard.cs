using ForgeCore.Core;
namespace ForgeCore.LocalAI;
public sealed partial class BuiltInBlenderAssetModel
{
    private AgentPlan CourtyardDeliveryPlan(ModelRequest request,string name,string hash,bool retroStyle=false,bool connectedLevel=false)
    {
        var scene=$"Assets/Scenes/CharacterAnimations/{name}-{hash[..12]}.unity";
        var preview=$"Previews/Courtyard/{name}-{hash[..12]}-runtime.png";
        var exe=$"Builds/Courtyard/{name}-{hash[..12]}/{name}.exe";
        AgentPlan? Step(string operation,string description,params(string Key,string Value)[] extra)
        {
            var fields=new[]{("project_path",projectPath),("operation",operation),("scene_path",scene)}.Concat(extra).ToArray();
            var expected=fields.ToDictionary(p=>p.Item1,p=>p.Item2);
            var existing=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.UnityEditorAction&&o.Arguments.Count==expected.Count&&expected.All(p=>o.Arguments.TryGetValue(p.Key,out var v)&&v==p.Value));
            if(existing!=null)return existing.Success?null:new("Courtyard delivery stopped: "+existing.Detail+" Existing scene/artifacts are retained; no automatic retry or quality approval.",[]);
            var call=ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,description,fields);_=UnityEditorActionSpec.From(call);
            return new(description+" Normal scoped approval applies. This is a bounded technical candidate, not final art acceptance.",[call]);
        }
        var pending=Step("capture_scene_preview","Capture the generated courtyard in actual play mode; keep the separate static three-view comparison.",("output_path",preview),("duration_seconds","4"))
            ?? Step("profile_scene","Measure ten seconds of the generated courtyard in Unity; this startup sample is not representative gameplay performance acceptance.",("duration_seconds","10"))
            ?? Step("build_windows","Export this exact generated courtyard scene into a fresh standalone Windows EXE folder; preserve prior games.",("output_path",exe),("development_build","true"));
        if(pending!=null)return pending;
        var smokeFields=new[]{("project_path",projectPath),("output_path",exe),("duration_seconds","5")};
        var smoke=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.UnityPlayerSmokeTest&&o.Arguments.Count==3&&smokeFields.All(p=>o.Arguments.TryGetValue(p.Item1,out var v)&&v==p.Item2));
        if(smoke==null)
        {
            var call=ToolCall.Create(ToolKind.UnityPlayerSmokeTest,PermissionKind.Execute,"Run a five-second hash-bound startup check on this exact exported EXE. This is headless startup evidence, not a rendered gameplay/performance test.",smokeFields);
            _=UnityPlayerSmokeTestSpec.From(call);return new(call.Description+" Normal scoped approval applies.",[call]);
        }
        if(!smoke.Success)return new("Courtyard delivery stopped at standalone startup: "+smoke.Detail+" The exported folder is retained for diagnosis; no automatic replay or quality approval.",[]);
        return new("Industrial pipeline finished its technical candidate: "+(connectedLevel?"three accessible single-floor zones, two stage-gated doors, bounded 64m navigation and separate checkpoint profile":retroStyle?"original low-poly N64-era-inspired rig/weapon derivative, small original textures, corrected cover placement and simplified lighting":"original Blender guard and reusable detailed courtyard recipe")+", before/after sheets (original courtyard prepass only for connected layout), actual final runtime screenshot, short Editor profile, standalone Windows EXE folder and five-second hash-bound startup check at "+exe+". Startup is headless and not a gameplay/rendering benchmark. Human visual/playthrough review, approved-source prop placement, further map scale and representative performance remain open. This does not certify indie/AA graphics, hardware emulation, a licensed remake or arbitrary-genre game generation.",[]);
    }
}
