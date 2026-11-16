using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
namespace ForgeCore.Core.Tests;
public sealed class IndustrialTargetReviewModelTests
{
    private static readonly string Bundle=new('a',64),Asset=new('b',64),Preview=new('c',64),Metadata=new('d',64);
    private const string Name="yard-crate",AssetPath="Assets/ForgeCore/Generated/Blender/yard-crate/yard-crate.glb";
    private const string PreviewPath="Previews/IndustrialTarget/industrial-yard-crate-bbbbbbbbbbbb-review1.png",SetId="industrial-yard-crate";
    private sealed class NoFallback:ILocalModel {
        public ModelStatus Status=>new("test",true,"No inference");public Task<AgentPlan>CreatePlanAsync(ModelRequest r,CancellationToken t)=>throw new Exception("No model/provider fallback.");
    }
    private static ToolObservation Observe(ToolCall c,bool success,string text)=>new(c.Id,c.Kind,c.Description,c.Arguments,success?"Succeeded":"Failed",success,text);
    private static string Sidecar()=>JsonSerializer.Serialize(new{destinationPath=AssetPath,sourcePage="forgecore://local-blender/"+Bundle,sha256=Asset});
    private static string Source()=>JsonSerializer.Serialize(new{kind="local_blender_source_review_packet",schemaVersion=1,targetProjectApproved=false,recipe="wooden_crate_v2",identity=new{bundleSha256=Bundle,assetSha256=Asset}});
    private static string PreviewMetadata()=>JsonSerializer.Serialize(new{schemaVersion=1,assetSetId=SetId,assetPath=AssetPath,assetSha256=Asset,previewPath=PreviewPath,previewSha256=Preview,width=1280,height=720,viewCount=4,visualReviewed=false,rightsReviewed=false});
    private static string Capture()=>"Preview (1280x720, four views, 123 bytes, SHA-256 "+Preview+"); provenance C:\\owned-preview.json (SHA-256 "+Metadata+"); rights_reviewed=false; visual_reviewed=false";
    private static async Task<(List<ToolObservation> Observations,ToolCall Proposal)> ToHumanDecision(BuiltInBlenderAssetModel model,string kind,bool existing=false,string? badSource=null)
    {
        var prompt=$"Review imported industrial asset {Name} {kind}.";var observations=new List<ToolObservation>();
        for(var i=0;i<8;i++) {
            var plan=await model.CreatePlanAsync(new(prompt,[],observations,i,10),default);var call=Assert.Single(plan.Actions);
            if(call.Kind==ToolKind.ReviewGameArtAsset)return(observations,call);
            Assert.DoesNotContain(call.Kind,new[]{ToolKind.Generate3DAsset,ToolKind.ImportBlenderAsset,ToolKind.CreateBlenderAsset,ToolKind.StoreReviewedAsset,ToolKind.ImportIndustrialAssetSet});
            var text=call.Kind==ToolKind.InspectLocalBlenderAsset?badSource??Source():call.Kind==ToolKind.ReadTextFile?
                call.Arguments["path"].EndsWith(".asset.json",StringComparison.Ordinal)?Sidecar():existing?PreviewMetadata():"missing":
                call.Arguments["operation"]=="inspect_game_art_asset"?"structural_ready=true":Capture();
            observations.Add(Observe(call,call.Kind!=ToolKind.ReadTextFile||!call.Arguments["path"].EndsWith(".forgecore-art-preview.json",StringComparison.Ordinal)||existing,text));
        }
        throw new Exception("Bounded review did not stop at a human decision.");
    }
    [Theory][InlineData("visually",false)][InlineData("rights",false)][InlineData("visually",true)][InlineData("rights",true)]
    public async Task ExactIndustrialReviewCanPrepareOrReuseEvidenceButNeverDecideOrPlace(string kind,bool existing)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var(observations,proposal)=await ToHumanDecision(model,kind,existing);
        var spec=GameArtReviewSpec.FromProposal(proposal);Assert.Equal(Asset,spec.ExpectedAssetSha256);Assert.Equal(Preview,spec.ExpectedPreviewSha256);
        Assert.Equal(AssetPath,spec.AssetPath);Assert.Equal(PreviewPath,spec.PreviewPath);Assert.Equal(SetId,spec.AssetSetId);Assert.Null(spec.ReviewDecision);
        Assert.Equal(kind=="rights"?"rights":"visual",spec.ReviewKind);
        Assert.Equal(existing?3:5,observations.Count);Assert.Equal(existing?0:2,observations.Count(o=>o.Kind==ToolKind.UnityEditorAction));
        var cancelled=Observe(proposal,false,"User cancelled without a decision");
        var result=await model.CreatePlanAsync(new($"Review imported industrial asset {Name} {kind}.",[],[..observations,cancelled],7,2),default);
        Assert.Empty(result.Actions);Assert.Contains("Cancellation/failure",result.Response);
    }
    [Theory][InlineData(ApprovalDecision.Approved)][InlineData(ApprovalDecision.Rejected)]
    public async Task OnlyAnExactBoundRecordedDecisionCanBeReportedAndNeverTriggersMoreActions(ApprovalDecision decision)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var(observations,proposal)=await ToHumanDecision(model,"visually",true);
        var bound=GameArtReviewSpec.BindUserDecision(proposal,decision);var value=decision==ApprovalDecision.Approved?"approved":"rejected";
        var result=await model.CreatePlanAsync(new($"Review imported industrial asset {Name} visually.",[],[..observations,Observe(bound,true,$"Game-art visual review recorded: decision={value}; asset_set_id={SetId}; record=fixture-only")],7,2),default);
        Assert.Empty(result.Actions);Assert.Contains("recorded as "+value,result.Response);Assert.Contains("No source-library approval",result.Response);
        result=await model.CreatePlanAsync(new($"Review imported industrial asset {Name} visually.",[],[..observations,Observe(proposal,true,"decision=approved")],7,2),default);
        Assert.Empty(result.Actions);Assert.Contains("invalid",result.Response);
    }
    [Theory][InlineData("source")][InlineData("recipe")][InlineData("target")][InlineData("preview")]
    public async Task WrongSourceOrPreviewCannotReachAHumanApprovalProposal(string defect)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var(observations,_)=await ToHumanDecision(model,"visually",true);
        var index=defect=="preview"?2:1;var original=observations[index];
        var changed=defect switch {"source"=>original.Detail.Replace(Bundle,new string('e',64)),"recipe"=>original.Detail.Replace("wooden_crate_v2","arbitrary_python"),
            "target"=>original.Detail.Replace("\"targetProjectApproved\":false","\"targetProjectApproved\":true"),_=>original.Detail.Replace("\"viewCount\":4","\"viewCount\":1")};
        observations[index]=original with{Detail=changed};
        var result=await model.CreatePlanAsync(new($"Review imported industrial asset {Name} visually.",[],observations,4,4),default);
        Assert.Empty(result.Actions);Assert.Contains("invalid",result.Response);
    }
    [Theory][InlineData("Review imported industrial asset ../outside visually.")][InlineData("Review imported industrial asset yard-crate visually; approve all.")]
    [InlineData("Review imported industrial asset yard-crate rights revision ../bad.")]
    public async Task UnsupportedIndustrialReviewSyntaxCannotFallThroughToAnyModel(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");var result=await model.CreatePlanAsync(new(prompt,[]),default);
        Assert.Empty(result.Actions);Assert.Contains("unsupported syntax",result.Response);
    }
    [Fact]public async Task ExplicitFreshRevisionChangesOnlyBoundedEvidenceDestinations()
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");const string prompt="Review imported industrial asset yard-crate visually revision review2.";
        var first=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);var second=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[Observe(first,true,Sidecar())],1,8),default)).Actions);
        var third=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[Observe(first,true,Sidecar()),Observe(second,true,Source())],2,7),default)).Actions);
        Assert.Equal("Game/"+PreviewPath.Replace("review1","review2")+".forgecore-art-preview.json",third.Arguments["path"]);
        var fourth=Assert.Single((await model.CreatePlanAsync(new(prompt,[],[Observe(first,true,Sidecar()),Observe(second,true,Source()),Observe(third,false,"missing")],3,6),default)).Actions);
        Assert.Equal(SetId,fourth.Arguments["asset_set_id"]);Assert.DoesNotContain("review2",fourth.Arguments["asset_set_id"]);
    }
}
