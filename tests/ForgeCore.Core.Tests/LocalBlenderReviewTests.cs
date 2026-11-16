using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class LocalBlenderReviewTests
{
    private static LocalBlenderReviewIdentity Identity=>new(new string('a',64),new string('b',64),new string('c',64),new string('d',64),new string('e',64));
    [Theory][InlineData("review_decision","approved")][InlineData("attestation_source","user_live_approval")][InlineData("project_path","Game")][InlineData("preview_path","../outside")]
    public void SourceProposalCannotInjectAttestationOrPaths(string field,string value)
    {
        var call=LocalBlenderReviewSpec.Proposal(Identity,"visual");var args=new Dictionary<string,string>(call.Arguments){[field]=value};
        Assert.Throws<ArgumentException>(()=>LocalBlenderReviewSpec.FromProposal(call with{Arguments=args}));
    }
    [Fact]public void SourceReviewBindingsAreExactAndCancellationIsNotARejection()
    {
        var call=LocalBlenderReviewSpec.Proposal(Identity,"rights");
        Assert.Throws<ArgumentException>(()=>GameArtReviewSpec.BindUserDecision(call,ApprovalDecision.Cancelled));
        foreach(var choice in new[]{ApprovalDecision.Approved,ApprovalDecision.Rejected})
        {
            var bound=GameArtReviewSpec.BindUserDecision(call,choice);var spec=LocalBlenderReviewSpec.FromExecution(bound);
            Assert.Equal(Identity,spec.Identity);Assert.Equal(GameArtReviewSpec.UserLiveApproval,spec.AttestationSource);
            Assert.Equal(choice==ApprovalDecision.Approved?"approved":"rejected",spec.Decision);
            Assert.Throws<ArgumentException>(()=>LocalBlenderReviewSpec.FromProposal(bound));
        }
        Assert.Throws<ArgumentException>(()=>LocalBlenderReviewSpec.FromProposal(LocalBlenderReviewSpec.Proposal(Identity,"everything")));
    }
    private sealed class NoFallback:ILocalModel
    {
        public ModelStatus Status=>new("test",true,"No inference");
        public Task<AgentPlan>CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new Exception("No inference fallback permitted.");
    }
    [Theory][InlineData("visually","visual")][InlineData("rights","rights")]
    public async Task SourceReviewPromptInspectsThenRequestsOneLiveReviewWithoutImport(string suffix,string kind)
    {
        var prompt=$"Review local Blender asset {Identity.BundleSha256} {suffix}.";
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var inspect=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);Assert.Equal(ToolKind.InspectLocalBlenderAsset,inspect.Kind);
        var data=JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_review_packet",identity=Identity,targetProjectApproved=false},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var observation=new ToolObservation(inspect.Id,inspect.Kind,inspect.Description,inspect.Arguments,"Succeeded",true,data);
        var review=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[observation],2,5),default)).Actions);
        Assert.Equal(Identity,LocalBlenderReviewSpec.FromProposal(review).Identity);Assert.Equal(kind,review.Arguments["review_kind"]);
        var cancelled=new ToolObservation(review.Id,review.Kind,review.Description,review.Arguments,"Cancelled",false,"closed");
        Assert.Empty((await model.CreatePlanAsync(new(prompt,[],[observation,cancelled],3,4),default)).Actions);
        var malformed=observation with{Detail=data.Replace(Identity.BundleSha256,new string('f',64))};
        Assert.Empty((await model.CreatePlanAsync(new(prompt,[],[malformed],2,5),default)).Actions);
        var recorded=JsonSerializer.Serialize(new{schemaVersion=1,kind="local_blender_source_review",identity=Identity,reviewKind=kind,decision="rejected",targetProjectApproved=false},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var result=new ToolObservation(review.Id,review.Kind,review.Description,review.Arguments,"Succeeded",true,recorded);
        var finished=await model.CreatePlanAsync(new(prompt,[],[observation,result],3,4),default);
        Assert.Empty(finished.Actions);Assert.Contains("recorded as rejected",finished.Response);
        var wrongKind=result with{Detail=recorded.Replace("\"reviewKind\":\""+kind+"\"","\"reviewKind\":\"other\"")};
        Assert.Contains("invalid",(await model.CreatePlanAsync(new(prompt,[],[observation,wrongKind],3,4),default)).Response);
    }
    [Theory][InlineData("Review local Blender asset ../outside visually.")][InlineData("Review local Blender asset all automatically.")]
    public async Task UnsupportedReviewPromptDoesNotFallThroughToInference(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");Assert.Empty((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
    }
    [Fact]public void SourceInspectionParserRejectsExtraPathsAndDerivesReadPermission()
    {
        var json=JsonSerializer.Serialize(new{Response="Inspect exact source",Actions=new[]{new{Kind="inspect_local_blender_asset",Description="Read",Arguments=new Dictionary<string,string>{{"bundle_sha256",Identity.BundleSha256}}}}});
        var call=Assert.Single(StructuredPlanParser.Parse(json,1).Actions);Assert.Equal(PermissionKind.Read,call.RequiredPermission);
        Assert.Equal(Identity.BundleSha256,LocalBlenderInspectSpec.From(call).BundleSha256);
        Assert.Throws<FormatException>(()=>StructuredPlanParser.Parse(json.Replace(Identity.BundleSha256,"../outside"),1));
    }
}
