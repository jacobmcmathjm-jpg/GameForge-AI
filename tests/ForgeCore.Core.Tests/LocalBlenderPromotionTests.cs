using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class LocalBlenderPromotionTests
{
    private static LocalBlenderReviewIdentity Identity=>new(new string('a',64),new string('b',64),new string('c',64),new string('d',64),new string('e',64));
    private static LocalBlenderPromotionSpec Promotion=>new(Identity,new string('1',64),new string('2',64));
    private sealed class NoFallback:ILocalModel
    {
        public ModelStatus Status=>new("fixture",true,"no inference");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new InvalidOperationException("Offline source routes must not call inference.");
    }
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    private static string CheckEvidence(bool eligible)=>JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_compatibility",identity=Identity,
        eligibleForSourcePromotion=eligible,matchesSourceConstraints=true,sourcePromotionState="not_promoted",
        sourceVisualReview=new{state=eligible?"approved":"pending",recordSha256=eligible?Promotion.VisualReviewSha256:null},
        sourceRightsReview=new{state=eligible?"approved":"pending",recordSha256=eligible?Promotion.RightsReviewSha256:null},
        targetProjectApproved=false,targetUnityInspectionRequired=true,animationCompatibility="not_measured_in_target"},Json);
    private static ToolObservation Observation(ToolCall call,string output,bool success=true)=>new(call.Id,call.Kind,call.Description,call.Arguments,success?"Succeeded":"Cancelled",success,output);
    [Theory][InlineData("review_decision","approved")][InlineData("attestation_source","user_live_approval")][InlineData("project_path","Game")][InlineData("output_path","../outside")]
    public void PromotionRejectsInjectedDecisionsAndPaths(string field,string value)
    {
        var call=Promotion.Proposal();var args=call.Arguments.ToDictionary(x=>x.Key,x=>x.Value);args.Add(field,value);
        Assert.Throws<ArgumentException>(()=>LocalBlenderPromotionSpec.From(call with{Arguments=args}));
    }
    [Theory][InlineData("bundle_sha256","../outside")][InlineData("height_min_mm","20001")][InlineData("rig_required","yes")]
    [InlineData("max_triangles","0")][InlineData("asset_role","weapons")][InlineData("project_path","Game")][InlineData("review_decision","approved")]
    public void CompatibilityRejectsUnsafeOrUnsupportedArguments(string field,string value)
    {
        var args=new Dictionary<string,string>{{"bundle_sha256",Identity.BundleSha256}};args[field]=value;
        Assert.Throws<ArgumentException>(()=>LocalBlenderCompatibilitySpec.From(new(Guid.NewGuid(),ToolKind.CheckLocalBlenderCompatibility,args,PermissionKind.Read,"fixture")));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ParserDerivesSourceReadVersusPromotionWritePermission(bool promote)
    {
        var args=promote?Promotion.Proposal().Arguments:new Dictionary<string,string>{{"bundle_sha256",Identity.BundleSha256},{"asset_role","environment_props"},{"rig_required","false"},{"max_triangles","12000"}};
        var payload=JsonSerializer.Serialize(new{Response="Source only",Actions=new[]{new{Kind=promote?"promote_local_blender_asset":"check_local_blender_compatibility",Description="fixture",Arguments=args}}});
        var call=Assert.Single(StructuredPlanParser.Parse(payload,1).Actions);Assert.Equal(promote?PermissionKind.Write:PermissionKind.Read,call.RequiredPermission);
        if(promote)Assert.Equal(Promotion,LocalBlenderPromotionSpec.From(call));else Assert.Equal(12000,LocalBlenderCompatibilitySpec.From(call).Constraints.MaximumTriangles);
        Assert.Throws<FormatException>(()=>StructuredPlanParser.Parse(payload.Replace(Identity.BundleSha256,"invalid"),1));
    }
    [Theory][InlineData("Promote local Blender asset all automatically.")][InlineData("Check local Blender asset ../outside.")]
    public async Task UnsupportedSourcePromptsNeverCallInference(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");Assert.Empty((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
    }
    [Theory][InlineData("Check")][InlineData("Promote")]
    public async Task PendingSourceChecksCannotManufactureReviewsOrImports(string action)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var prompt=$"{action} local Blender asset {Identity.BundleSha256}.";
        var read=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);Assert.Equal(ToolKind.CheckLocalBlenderCompatibility,read.Kind);
        var stopped=await model.CreatePlanAsync(new(prompt,[],[Observation(read,CheckEvidence(false))],2,5),default);Assert.Empty(stopped.Actions);
        Assert.Contains(action=="Check"?"not eligible":"blocked",stopped.Response);
    }
    [Fact]public async Task ExactPromotionRouteRequiresSeparateWriteAndStopsAfterReceipt()
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var prompt=$"Promote local Blender asset {Identity.BundleSha256}.";
        var read=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);var checkedSource=Observation(read,CheckEvidence(true));
        var write=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[checkedSource],2,5),default)).Actions);
        Assert.Equal(Promotion,LocalBlenderPromotionSpec.From(write));Assert.Equal(PermissionKind.Write,write.RequiredPermission);
        Assert.Empty((await model.CreatePlanAsync(new(prompt,[],[checkedSource,Observation(write,"cancelled",false)],3,4),default)).Actions);
        var receipt=JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_promotion",identity=Identity,tier="human_reviewed_local_source",
            visualReviewSha256=Promotion.VisualReviewSha256,rightsReviewSha256=Promotion.RightsReviewSha256,targetProjectApproved=false,targetUnityInspectionRequired=true},Json);
        var done=await model.CreatePlanAsync(new(prompt,[],[checkedSource,Observation(write,receipt)],3,4),default);
        Assert.Empty(done.Actions);Assert.Contains("promoted without changing",done.Response);
        var wrong=receipt.Replace(Promotion.RightsReviewSha256,new string('3',64));
        Assert.Contains("invalid",(await model.CreatePlanAsync(new(prompt,[],[checkedSource,Observation(write,wrong)],3,4),default)).Response);
    }
    [Theory][InlineData("\"targetProjectApproved\":false","\"targetProjectApproved\":true")]
    [InlineData("not_measured_in_target","approved")][InlineData("\"state\":\"approved\"","\"state\":\"pending\"")]
    public async Task PromotionCannotTrustInflatedOrInconsistentCheckEvidence(string before,string after)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var prompt=$"Promote local Blender asset {Identity.BundleSha256}.";
        var read=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
        var result=await model.CreatePlanAsync(new(prompt,[],[Observation(read,CheckEvidence(true).Replace(before,after))],2,5),default);Assert.Empty(result.Actions);
    }
}
