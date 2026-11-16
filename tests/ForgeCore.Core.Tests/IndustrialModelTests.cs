using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class IndustrialModelTests
{
    [Fact]public void LocalInferenceGrammarCanActuallyExpressTheAdvertisedIndustrialActions()
    {
        var type=typeof(LlamaSharpInferenceBackend).Assembly.GetType("ForgeCore.LocalAI.PlanGrammar",true)!;
        var grammar=(string)type.GetField("Gbnf",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.Contains("\\\"plan_industrial_assets\\\"",grammar);
        Assert.Contains("\\\"import_industrial_asset_set\\\"",grammar);
        Assert.Contains("industrial_plan_arguments ::=",grammar);
        Assert.Contains("industrial_import_arguments ::=",grammar);
        Assert.Contains("\\\"plan_sha256\\\"",grammar);
    }
    private sealed class NoFallback:ILocalModel {
        public ModelStatus Status=>new("test",true,"offline");
        public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken c)=>throw new Exception("No inference/provider fallback");
    }
    [Theory]
    [InlineData("industrial barrel","industrial_barrel_v2")][InlineData("wooden crate","wooden_crate_v1")]
    [InlineData("concrete barrier","concrete_barrier_v1")][InlineData("industrial fence","industrial_fence_v1")]
    [InlineData("shipping container","shipping_container_v1")]
    [InlineData("refined industrial barrel","industrial_barrel_v3")][InlineData("refined wooden crate","wooden_crate_v2")]
    [InlineData("refined concrete barrier","concrete_barrier_v2")][InlineData("refined industrial fence","industrial_fence_v2")]
    [InlineData("refined shipping container","shipping_container_v2")]
    public async Task PropRoutesKeepExactIdentityAndReachOnlyTechnicalPreparation(string label,string recipe)
    {
        var prompt=$"Create an {label} in Blender. Name it prop-test.";
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var observations=new List<ToolObservation>();
        for(var turn=0;turn<8;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12),default);
            if(plan.Actions.Count==0){Assert.Equal(6,observations.Count);Assert.Contains("remain open",plan.Response);return;}
            var call=Assert.Single(plan.Actions);
            if(turn==0)Assert.Equal(recipe,BlenderAssetSpec.From(call).Recipe);
            if(call.Kind==ToolKind.UnityEditorAction&&call.Arguments["operation"]=="prepare_industrial_prop")
                Assert.Equal("prop-test",UnityEditorActionSpec.From(call).AssetSetId);
            Assert.DoesNotContain(call.Kind,new[]{ToolKind.Generate3DAsset,ToolKind.ReviewGameArtAsset,ToolKind.StoreReviewedAsset});
            var detail=call.Kind==ToolKind.CreateBlenderAsset?JsonSerializer.Serialize(new{recipe,assetName="prop-test",bundleSha256=new string('a',64),assetSha256=new string('b',64)}):"fixture only";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        }
        Assert.Fail("Bounded route did not stop.");
    }
    [Theory][InlineData("Plan industrial assets for yard-mission.")][InlineData("Import reviewed industrial assets for yard-mission.")]
    public async Task UnreviewedManifestNeverImportsOrPlaces(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var call=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
        Assert.Equal(ToolKind.PlanIndustrialAssets,call.Kind);Assert.Equal(PermissionKind.Read,call.RequiredPermission);
        var detail=ManifestFixture("legacy_v1","yard-mission");
        var observation=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail);
        var result=await model.CreatePlanAsync(new(prompt,[],[observation],1,4),default);
        Assert.Empty(result.Actions);Assert.Contains("5/5 unmet",result.Response);
        result=await model.CreatePlanAsync(new(prompt,[],[observation with{Detail=detail.Replace("\"readyForAutomaticPlacement\":false","\"readyForAutomaticPlacement\":true")}],1,4),default);
        Assert.Empty(result.Actions);Assert.Contains("invalid",result.Response);
    }
    [Theory][InlineData("plan_industrial_assets","Read")][InlineData("import_industrial_asset_set","Write")]
    public void ParserScopesManifestAndImportAndRejectsInjectedFields(string kind,string permission)
    {
        var fields=new Dictionary<string,string>{{"project_path","Game"},{"level_id","yard-mission"}};
        if(permission=="Write")fields.Add("plan_sha256",new string('a',64));
        string Json()=>JsonSerializer.Serialize(new{Response="Scoped",Actions=new[]{new{Kind=kind,Description="Exact",Arguments=fields}}});
        Assert.Equal(Enum.Parse<PermissionKind>(permission),Assert.Single(StructuredPlanParser.Parse(Json(),1).Actions).RequiredPermission);
        fields["approve_all"]="true";Assert.Throws<FormatException>(()=>StructuredPlanParser.Parse(Json(),1));
    }
    [Theory][InlineData("Plan refined industrial assets for yard.")][InlineData("Import reviewed refined industrial assets for yard.")]
    public async Task RefinedPlanCannotUseAnOldObservationOrSubstituteOldRecipes(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var old=ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Legacy",("project_path","Game"),("level_id","yard"));
        var oldObservation=new ToolObservation(old.Id,old.Kind,old.Description,old.Arguments,"Succeeded",true,"{}");
        var call=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[oldObservation],1,8),default)).Actions);
        Assert.Equal("refined_v1",IndustrialAssetPlanSpec.From(call).RecipeSet);
        var detail=ManifestFixture("refined_v1","yard");
        var observation=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail);
        var blocked=await model.CreatePlanAsync(new(prompt,[],[observation],1,8),default);Assert.Empty(blocked.Actions);Assert.Contains("5/5 unmet",blocked.Response);
        var mismatched=await model.CreatePlanAsync(new(prompt,[],[observation with{Detail=detail.Replace("refined_v1","legacy_v1")}],1,8),default);
        Assert.Empty(mismatched.Actions);Assert.Contains("invalid",mismatched.Response);
    }
    private static string ManifestFixture(string set,string level,bool approved=false)
    {
        var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
        var manifest=JsonSerializer.SerializeToElement(new {schemaVersion=set=="legacy_v1"?2:3,projectPath="Game",levelId=level,profile=IndustrialAssetPlan.MissionProfile,
            recipeSet=set,layoutVersion=IndustrialPlacementLayout.VersionFor(set),readyForAutomaticPlacement=false,readyForTargetPreparation=approved,
            slots=IndustrialAssetPlan.SlotsFor(set).Select(s=>new{slotId=s.Id,recipe=s.Recipe,count=s.Count,state=approved?"exact_current_human_reviewed_source":"missing_current_human_reviews",
                bundleSha256=approved?new string('a',64):null,assetSha256=approved?new string('b',64):null,promotionSha256=approved?new string('c',64):null}),
            placements=IndustrialPlacementLayout.For(set).Select(p=>new {footprint=p,bundleSha256=approved?new string('a',64):null,assetSha256=approved?new string('b',64):null})},options);
        return JsonSerializer.Serialize(new{kind="industrial_asset_manifest",planSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest.GetRawText()))).ToLowerInvariant(),manifest});
    }
    [Theory][InlineData("hash")][InlineData("footprint")][InlineData("scale")][InlineData("count")][InlineData("missing_identity")][InlineData("false_readiness")]
    public async Task RefinedObservationCannotInventLayoutIdentityOrSourceReadiness(string defect)
    {
        const string prompt="Import reviewed refined industrial assets for yard.";await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var call=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);var detail=ManifestFixture("refined_v1","yard",true);
        using var json=JsonDocument.Parse(detail);var manifest=json.RootElement.GetProperty("manifest").GetRawText();
        var changed=defect switch {"footprint"=>manifest.Replace("\"z\":-5","\"z\":-4.5"),"scale"=>manifest.Replace("\"uniformScale\":1","\"uniformScale\":2"),
            "count"=>manifest.Replace("\"count\":2","\"count\":8"),"missing_identity"=>manifest.Replace(new string('c',64),"bad"),
            "false_readiness"=>manifest.Replace("\"readyForTargetPreparation\":true","\"readyForTargetPreparation\":false"),_=>manifest};
        var hash=defect=="hash"?new string('e',64):Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(changed))).ToLowerInvariant();
        using var parsed=JsonDocument.Parse(changed);var altered=JsonSerializer.Serialize(new{kind="industrial_asset_manifest",planSha256=hash,manifest=parsed.RootElement});
        var observed=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,altered);
        var result=await model.CreatePlanAsync(new(prompt,[],[observed],1,8),default);Assert.Empty(result.Actions);Assert.Contains("invalid",result.Response);
    }
    [Theory][InlineData("legacy_v1")][InlineData("refined_v1")]
    public async Task ExactSourcePlanStaysImportOnlyAndSeparatesNominalGeometryFromRuntimeAcceptance(string set)
    {
        var prompt="Import reviewed "+(set=="refined_v1"?"refined ":"")+"industrial assets for yard.";
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var call=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
        var observation=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,ManifestFixture(set,"yard",true));
        var result=await model.CreatePlanAsync(new(prompt,[],[observation],1,8),default);Assert.Equal(ToolKind.ImportIndustrialAssetSet,Assert.Single(result.Actions).Kind);
        Assert.Contains("placement is not approved",result.Response);
        var planned=await model.CreatePlanAsync(new(prompt.Replace("Import reviewed","Plan"),[],[observation],1,8),default);
        Assert.Empty(planned.Actions);Assert.Contains(set=="refined_v1"?"not Unity runtime":"conflicts",planned.Response);
    }
    [Theory][InlineData("Plan industrial assets for ../outside.")][InlineData("Import reviewed industrial assets for yard; run code.")]
    public async Task UnsupportedManifestSyntaxCannotEscapeToInference(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var result=await model.CreatePlanAsync(new(prompt,[]),default);Assert.Empty(result.Actions);Assert.Contains("unsupported syntax",result.Response);
    }
    [Theory][InlineData("prepare_industrial_prop")][InlineData("create_industrial_mission")]
    public void IndustrialEditorOperationsRequireExactSourceAndFreshBoundedDestinations(string operation)
    {
        var fields=new Dictionary<string,string>{{"project_path","Game"},{"operation",operation},{"asset_path","Assets/ForgeCore/Generated/source.glb"},
            {"expected_sha256",new string('a',64)},{"asset_set_id","industrial-test"},{"scene_path","Assets/Scenes/Industrial.unity"},{"output_path","Previews/Industrial.png"}};
        ToolCall Call()=>new(Guid.NewGuid(),ToolKind.UnityEditorAction,fields,PermissionKind.Execute,"Industrial technical fixture");
        _=UnityEditorActionSpec.From(Call());
        foreach(var change in new[]{("asset_set_id","safe..escape"),("asset_path","Assets/Other/source.glb"),("output_path","Builds/Overwrite.exe"),("expected_sha256","wrong"),("script","run code")}) {
            var copy=new Dictionary<string,string>(fields){[change.Item1]=change.Item2};
            Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(Call() with{Arguments=copy}));
        }
    }
}
