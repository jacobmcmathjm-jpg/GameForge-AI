using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class RetroCourtyardTests
{
    private static JsonObject Report()=>JsonSerializer.SerializeToNode(new{schemaVersion=1,profile=RetroCourtyardProfile.Id,recipe=RetroCourtyardProfile.Recipe,layout=RetroCourtyardProfile.Layout,
        source="original_template_derivative_no_game_assets",textureCount=8,maximumTextureSize=128,paletteLevels=32,guardCount=3,guardTriangles=600,armsTriangles=200,weaponTriangles=120,
        propLodGroups=10,activePropTriangles=4960,ownedTextureBytes=350000,remainingActiveReflections=0,addedColliders=0,socketsUnchanged=true,routesReachable=true,nominalPlacementConflicts=0,
        animationClipsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,approvedLibraryAssetsPlaced=false,hardwareEmulated=false})!.AsObject();
    [Fact]public void OriginalRetroProfileHasClearNominalPlacementWithoutChangingLegacyPlan() {
        var audit=IndustrialPlacementAudit.Assess(RetroCourtyardProfile.Placements);Assert.True(audit.NominalLayoutClear);Assert.Empty(audit.Conflicts);Assert.Equal(3,audit.Routes.Count);
        Assert.Equal(-1,IndustrialPlacementLayout.Placements.Single(p=>p.PlacementId=="supplies-2").Z);
        Assert.Equal(0,IndustrialPlacementLayout.Placements.Single(p=>p.PlacementId=="containers-1").Z);
        using var json=JsonDocument.Parse(Report().ToJsonString());RetroCourtyardProfile.Validate(json.RootElement);
    }
    [Theory][InlineData("profile","\"goldeneye_assets\"")][InlineData("recipe","\"other\"")][InlineData("layout","\"legacy\"")][InlineData("source","\"rom_extract\"")]
    [InlineData("textureCount","9")][InlineData("maximumTextureSize","1024")][InlineData("paletteLevels","256")][InlineData("guardCount","2")][InlineData("guardTriangles","2001")]
    [InlineData("armsTriangles","501")][InlineData("weaponTriangles","401")][InlineData("propLodGroups","9")][InlineData("activePropTriangles","6001")][InlineData("ownedTextureBytes","1048577")]
    [InlineData("remainingActiveReflections","1")][InlineData("addedColliders","1")][InlineData("socketsUnchanged","false")][InlineData("routesReachable","false")][InlineData("nominalPlacementConflicts","1")]
    [InlineData("animationClipsChanged","true")][InlineData("audioChanged","true")][InlineData("visualReviewed","true")][InlineData("rightsReviewed","true")][InlineData("approvedLibraryAssetsPlaced","true")][InlineData("hardwareEmulated","true")]
    public void RetroEvidenceRejectsOverclaimsAndWrongBudgets(string field,string value){var r=Report();r[field]=JsonNode.Parse(value);using var json=JsonDocument.Parse(r.ToJsonString());Assert.Throws<InvalidDataException>(()=>RetroCourtyardProfile.Validate(json.RootElement));}
    private static ToolCall Mission(string? style) {
        var values=new List<(string,string)>{("project_path","Games/ForgeCore"),("operation","create_industrial_mission"),("asset_path","Assets/ForgeCore/Generated/Blender/guard/guard.glb"),("expected_sha256",new string('a',64)),("asset_set_id","retro-yard"),("scene_path","Assets/Scenes/Retro.unity"),("output_path","Previews/Retro.png")};
        if(style!=null)values.Add(("style_family",style));return ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,"Original retro technical candidate",values.ToArray());
    }
    [Fact]public void RetroStyleIsExplicitAndDefaultContractRemainsSevenFields(){Assert.Null(UnityEditorActionSpec.From(Mission(null)).StyleFamily);Assert.Equal(7,Mission(null).Arguments.Count);Assert.Equal(RetroCourtyardProfile.Id,UnityEditorActionSpec.From(Mission(RetroCourtyardProfile.Id)).StyleFamily);}
    [Theory][InlineData("realistic_pbr")][InlineData("../../escape")][InlineData("goldeneye_rom")][InlineData("")]
    public void MissionStyleRejectsUnsupportedValues(string style)=>Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(Mission(style)));
    [Theory][InlineData("prepare_character_animation")][InlineData("create_security_combat_lab")][InlineData("prepare_industrial_prop")]
    public void RetroStyleDoesNotExpandOtherExistingOperations(string operation) {
        var call=Mission(RetroCourtyardProfile.Id);var values=call.Arguments.ToDictionary(x=>x.Key,x=>x.Value);values["operation"]=operation;
        Assert.Throws<ArgumentException>(()=>UnityEditorActionSpec.From(call with{Arguments=values}));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task PlannerStopsAtCrossStyleMissionObservationRatherThanExporting(bool retro) {
        await using var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");var observations=new List<ToolObservation>();
        var prompt=retro?"Create a retro industrial courtyard named retro-yard.":"Create a detailed industrial courtyard named graphics-yard.";
        for(var turn=0;turn<11;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12-turn),default);var call=Assert.Single(plan.Actions);
            var values=call.Arguments.ToDictionary(x=>x.Key,x=>x.Value);var mission=values.GetValueOrDefault("operation")=="create_industrial_mission";
            if(mission){if(retro)values.Remove("style_family");else values["style_family"]=RetroCourtyardProfile.Id;}
            var detail=call.Kind==ToolKind.CreateBlenderAsset?JsonSerializer.Serialize(new{bundleSha256=new string('a',64),assetSha256=new string('b',64)}):"Synthetic planner fixture only";
            observations.Add(new(call.Id,call.Kind,call.Description,values,"Succeeded",true,detail));
            if(mission){var stop=await model.CreatePlanAsync(new(prompt,[],observations,turn+1,11-turn),default);Assert.Empty(stop.Actions);Assert.Contains("style evidence does not match",stop.Response);return;}
        }Assert.Fail("Mission boundary not reached.");
    }
    private sealed class NoInference:ILocalModel {public ModelStatus Status=>new("fixture",true,"no fallback");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken t)=>throw new Exception("No fallback allowed.");}
    [Theory][InlineData("Create a retro industrial courtyard named retro-yard.")][InlineData("Make an n64-style industrial courtyard named retro-yard.")][InlineData("Create a GoldenEye 007-style industrial courtyard named retro-yard.")][InlineData("Create a retro industrial courtyard.")]
    public async Task RetroPromptUsesOneExplicitScopedStyleInsideExistingElevenActions(string prompt) {
        Assert.False(GameProductionPlan.IsCreationRequest(prompt));await using var model=new BuiltInBlenderAssetModel(new NoInference(),"Games/ForgeCore");var observations=new List<ToolObservation>();
        for(var turn=0;turn<12;turn++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,turn,12-turn),default);
            if(plan.Actions.Count==0){Assert.Equal(11,observations.Count);Assert.Contains("N64-era",plan.Response);return;}
            var call=Assert.Single(plan.Actions);if(prompt=="Create a retro industrial courtyard."&&call.Kind==ToolKind.CreateBlenderAsset)Assert.Equal("retro-courtyard",call.Arguments["asset_name"]);
            if(call.Kind==ToolKind.UnityEditorAction){var spec=UnityEditorActionSpec.From(call);if(spec.Operation=="create_industrial_mission")Assert.Equal("retro_64",spec.StyleFamily);else Assert.Null(spec.StyleFamily);}
            var detail=call.Kind==ToolKind.CreateBlenderAsset?JsonSerializer.Serialize(new{bundleSha256=new string('a',64),assetSha256=new string('b',64)}):"Synthetic planner fixture only";
            observations.Add(new(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,detail));
        }Assert.Fail("Retro route exceeded its bounded action budget.");
    }
}
