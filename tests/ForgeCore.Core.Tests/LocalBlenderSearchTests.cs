using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;

public sealed class LocalBlenderSearchTests
{
    private static ToolCall Call(string field,string value)=>ToolCall.Create(ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Search",("query","barrel"),(field,value));
    [Theory]
    [InlineData("query","../outside")][InlineData("query","barrel; run code")][InlineData("query","a")]
    [InlineData("path","outside")][InlineData("asset_role","people")][InlineData("recipe","arbitrary")]
    [InlineData("rig_required","yes")][InlineData("max_triangles","60001")][InlineData("height_min_mm","-1")]
    [InlineData("height_max_mm","0")][InlineData("max_results","21")][InlineData("visual_approved","true")]
    public void SearchRejectsUnboundedOrApprovalArguments(string field,string value)
    {
        var args=new Dictionary<string,string>{{"query","barrel"}};args[field]=value;
        Assert.Throws<ArgumentException>(()=>LocalBlenderSearchSpec.From(Call("max_results","10") with{Arguments=args}));
    }
    [Fact]public void SearchParserDerivesReadPermissionAndValidatesEveryFilter()
    {
        const string json="""{"Response":"Find candidates","Actions":[{"Kind":"search_local_blender_assets","Description":"Local only","Arguments":{"query":"industrial security","asset_role":"characters","rig_required":"true","height_min_mm":"1700","height_max_mm":"1900","max_triangles":"40000","max_results":"5"}}]}""";
        var call=Assert.Single(StructuredPlanParser.Parse(json,1).Actions);Assert.Equal(PermissionKind.Read,call.RequiredPermission);
        var spec=LocalBlenderSearchSpec.From(call);Assert.True(spec.RigRequired);Assert.Equal(40000,spec.MaximumTriangles);
        Assert.Throws<FormatException>(()=>StructuredPlanParser.Parse(json.Replace("1900","1600"),1));
        Assert.Throws<FormatException>(()=>StructuredPlanParser.Parse(json.Replace("40000","999999"),1));
    }
    private sealed class NoFallback:ILocalModel
    {
        public ModelStatus Status=>new("test",true,"no inference");
        public Task<AgentPlan>CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new Exception("No inference/provider fallback allowed.");
    }
    [Fact]public async Task LibraryPromptSearchesOnceWithoutImportGenerationOrReview()
    {
        const string prompt="Search local Blender assets for industrial security.";
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var call=Assert.Single((await model.CreatePlanAsync(new(prompt,[]),default)).Actions);
        Assert.Equal(ToolKind.SearchLocalBlenderAssets,call.Kind);Assert.Equal("industrial security",call.Arguments["query"]);
        var data=JsonSerializer.Serialize(new{schemaVersion=1,library="local_blender_candidates",truncated=true,results=new[]{new{reviewState="unreviewed_local_recipe",visualReviewed=false,rightsReviewed=false,placementApproved=false}}});
        var observation=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,data);
        var done=await model.CreatePlanAsync(new(prompt,[],[observation],2,4),default);
        Assert.Empty(done.Actions);Assert.Contains("not an exhaustive",done.Response);Assert.Contains("human visual/rights",done.Response);
        foreach(var invalid in new[]{"{}","not-json",data.Replace("\"visualReviewed\":false","\"visualReviewed\":true")})
            Assert.Contains("invalid evidence",(await model.CreatePlanAsync(new(prompt,[],[observation with{Detail=invalid}],2,4),default)).Response);
    }
    [Theory]
    [InlineData("Search local Blender assets for ../outside.")]
    [InlineData("Search local Blender assets for barrel; run a script.")]
    [InlineData("Find local Blender assets for a.")]
    public async Task UnsupportedLibrarySearchNeverFallsThroughToInference(string prompt)
    {
        await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Game");
        var plan=await model.CreatePlanAsync(new(prompt,[]),default);Assert.Empty(plan.Actions);Assert.Contains("will not be forwarded",plan.Response);
    }
}
