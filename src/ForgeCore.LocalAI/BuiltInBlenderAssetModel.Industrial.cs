using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;
namespace ForgeCore.LocalAI;
public sealed partial class BuiltInBlenderAssetModel
{
    private AgentPlan? IndustrialAssetRoute(ModelRequest request)
    {
        var match=Regex.Match(request.UserMessage,@"^\s*(?<mode>plan|import\s+reviewed)\s+(?<refined>refined\s+)?industrial\s+assets\s+for\s+(?<id>[a-z][a-z0-9_-]{1,19})[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!match.Success)return Regex.IsMatch(request.UserMessage,@"\b(?:plan|import)\b.*\bindustrial\s+assets\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)?
            new("Use: Plan industrial assets for yard-mission. Or: Import reviewed industrial assets for yard-mission. Add refined before industrial for the explicit new recipe/layout set. Only this fixed profile is supported; unsupported syntax does not reach inference.",[]):null;
        var id=match.Groups["id"].Value.ToLowerInvariant();
        var recipeSet=match.Groups["refined"].Success?"refined_v1":"legacy_v1";
        bool Relevant(ToolObservation o)=>o.Arguments.TryGetValue("project_path",out var project)&&project==projectPath&&o.Arguments.TryGetValue("level_id",out var level)&&level==id&&
            (o.Arguments.TryGetValue("recipe_set",out var set)?set:"legacy_v1")==recipeSet;
        ToolCall Scoped(ToolKind kind,PermissionKind permission,string description,string? hash=null) {
            var fields=new List<(string Key,string Value)>{("project_path",projectPath),("level_id",id)};
            if(recipeSet!="legacy_v1")fields.Add(("recipe_set",recipeSet));if(hash!=null)fields.Add(("plan_sha256",hash));
            return ToolCall.Create(kind,permission,description,fields.ToArray());
        }
        var plan=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.PlanIndustrialAssets&&Relevant(o));
        if(plan is null)return new("I will create a read-only exact industrial asset manifest. Missing source reviews/promotions remain visible.",
            [Scoped(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Plan exact versioned industrial mission asset requirements")]);
        if(!plan.Success)return new("Industrial asset planning stopped; no import or placement will run.",[]);
        try {
            if(plan.Detail.Length>1_048_576)throw new JsonException();
            using var json=JsonDocument.Parse(plan.Detail);var r=json.RootElement;var manifest=r.GetProperty("manifest");
            if(r.GetProperty("kind").GetString()!="industrial_asset_manifest"||manifest.GetProperty("levelId").GetString()!=id||
                manifest.GetProperty("projectPath").GetString()!=projectPath||manifest.GetProperty("profile").GetString()!=IndustrialAssetPlan.MissionProfile||
                manifest.GetProperty("readyForAutomaticPlacement").GetBoolean()||
                (manifest.TryGetProperty("recipeSet",out var selectedSet)?selectedSet.GetString():"legacy_v1")!=recipeSet||
                !manifest.GetProperty("slots").EnumerateArray().Select(s=>s.GetProperty("recipe").GetString()).SequenceEqual(IndustrialAssetPlan.SlotsFor(recipeSet).Select(s=>s.Recipe)))
                throw new JsonException();
            var audit=ValidateIndustrialObservation(r,recipeSet);
            var geometry=audit.NominalLayoutClear?"Nominal layout/three-route planning checks are clear; this is not Unity runtime navigation proof.":
                $"Nominal layout has {audit.Conflicts.Count} overlap/bounds conflicts and requires correction before placement.";
            var ready=manifest.GetProperty("readyForTargetPreparation").GetBoolean();var unmet=manifest.GetProperty("slots").EnumerateArray().Count(s=>s.GetProperty("state").GetString()!="exact_current_human_reviewed_source");
            if(!ready||unmet!=0)return new($"Industrial manifest has {unmet}/5 unmet source slots. Generate supported sources and complete exact live visual/rights reviews and promotion. {geometry} Nothing was imported; target Unity review and placement remain separate.",[]);
            if(match.Groups["mode"].Value.Equals("plan",StringComparison.OrdinalIgnoreCase))
                return new("Five exact human-reviewed/promoted source versions selected. "+geometry+" Target inspection/review/collision/LOD/navigation/playthrough gates are still open; no imports or placement occurred.",[]);
            var imported=request.Observations.LastOrDefault(o=>o.Kind==ToolKind.ImportIndustrialAssetSet&&Relevant(o));
            if(imported is not null)return new(imported.Success?"Exact reviewed source set imported with a no-replay journal. Target Unity inspection, human target reviews and suitable placement are still required; this is not automatic level-population acceptance.":"Industrial import stopped; inspect its journal before any retry. No automatic replay.",[]);
            var hash=r.GetProperty("planSha256").GetString()!;
            var call=Scoped(ToolKind.ImportIndustrialAssetSet,PermissionKind.Write,"Import this exact currently reviewed versioned industrial source set; never place/approve it implicitly",hash);
            _=IndustrialAssetPlanSpec.From(call);return new("The source-only set is ready for separately scoped import. Current reviews/plan identity are rechecked before writes; target placement is not approved.",[call]);
        }catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {return new("Industrial manifest evidence was invalid; no follow-up import or placement.",[]);}
    }
}
