using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    public static IEnumerable<object[]> IndustrialRecipes()=>IndustrialPropRecipes.Supported.Select(r=>new object[]{r.Id});
    private static ToolCall IndustrialCall(string recipe,string? name=null)
    {
        var r=IndustrialPropRecipes.Find(recipe)!;
        var fields=new List<(string Key,string Value)>{("project_path","Game"),("asset_name",name??recipe),("recipe",recipe),("seed","83"),("height_mm",r.DefaultHeight.ToString())};
        if(r.IsBarrel)fields.Add(("radius_mm","340"));
        return ToolCall.Create(ToolKind.CreateBlenderAsset,PermissionKind.Execute,"Synthetic industrial recipe fixture",fields.ToArray());
    }
    // Format/identity-only temporary test envelopes: never real art or workstation review attestations.
    private static async Task<UnityProcessResult> IndustrialFixtureRunner(string exe,IReadOnlyList<string> arguments,string directory,TimeSpan timeout,CancellationToken token)
    {
        using var request=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory,"request.json"),token));
        var req=request.RootElement;var recipe=IndustrialPropRecipes.Find(req.GetProperty("recipe").GetString()!)!;
        var (w,d,h)=recipe.Dimensions(req.GetProperty("heightMillimetres").GetInt32(),req.GetProperty("radiusMillimetres").GetInt32());
        var output=Path.Combine(directory,"output");Directory.CreateDirectory(output);
        foreach(var name in new[]{"asset.glb","lod1.glb","lod2.glb"})await File.WriteAllBytesAsync(Path.Combine(output,name),MinimalGlb(),token);
        await File.WriteAllBytesAsync(Path.Combine(output,"model.blend"),Encoding.ASCII.GetBytes("BLENDER-v502 synthetic industrial test, not art"),token);
        foreach(var name in new[]{"preview.png","basecolor.png","metallic.png","roughness.png","normal.png"})
            await File.WriteAllBytesAsync(Path.Combine(output,name),MinimalPng(1024,1024),token);
        await File.WriteAllTextAsync(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(new{recipe=recipe.Id,blenderVersion="5.2.1 LTS",
            vertices=100,triangles=120,materials=1,objects=1,boundsMin=new[]{-w/2,-d/2,0d},boundsMax=new[]{w/2,d/2,h},
            pivot="bottom_center",units="metres",textureResolution=1024,animationCount=0}),token);
        await File.WriteAllTextAsync(Path.Combine(output,"prop.json"),JsonSerializer.Serialize(new{schemaVersion=1,recipe=recipe.Id,
            collisionConvention="solid_box_no_walkable_interior",collisionCentre=new[]{0d,0,h/2},collisionSize=new[]{w,d,h},
            lodTriangles=new[]{120,80,40},uvFinite=true,rootIdentity=true,coordinateSystem="Blender_Z_up_metres"}),token);
        if(recipe.IsRefined)await File.WriteAllTextAsync(Path.Combine(output,"surface.json"),JsonSerializer.Serialize(new{
            schemaVersion=1,recipe=recipe.Id,revision="refined_industrial_finish_v1",resolution=1024,roughnessMinimum=.4,roughnessMaximum=.85,
            normalMaximumXY=.08,normalMeanZ=.99,paintedMetallicMaximum=0,proceduralOriginal=true,humanVisualAccepted=false}),token);
        return new(0,"FORGECORE_BLENDER_RECIPE_COMPLETE","");
    }
    [Theory][MemberData(nameof(IndustrialRecipes))]
    public async Task IndustrialRecipeRetainsLodsAndImmutableReuseAndImportsEveryExactCompanion(string recipe)
    {
        var exe=await BlenderFixtureExecutable();var runs=0;
        using var executor=BlenderExecutor(exe,async(e,a,w,t,c)=>{runs++;return await IndustrialFixtureRunner(e,a,w,t,c);});
        var first=await executor.ExecuteAsync(IndustrialCall(recipe),default);Assert.True(first.Success,first.Error);
        Assert.True((await executor.ExecuteAsync(IndustrialCall(recipe),default)).Success);Assert.Equal(1,runs);
        var imported=await executor.ExecuteAsync(ImportBarrel(first.Output,"industrial-one"),default);Assert.True(imported.Success,imported.Error);
        var folder=Path.Combine(_root,"Game","Assets","ForgeCore","Generated","Blender","industrial-one");
        Assert.Equal(5,Directory.GetFiles(folder).Length);
        foreach(var extra in new[]{"industrial-one-lod1.glb","industrial-one-lod2.glb","industrial-one.prop.json"})Assert.True(File.Exists(Path.Combine(folder,extra)));
        Assert.False((await executor.ExecuteAsync(ImportBarrel(first.Output,"industrial-one"),default)).Success);
        Assert.True((await executor.ExecuteAsync(ImportBarrel(first.Output,"industrial-two"),default)).Success);
        Directory.CreateDirectory(Path.Combine(_root,"Game","Assets","ForgeCore","Generated","Blender","industrial-meta.meta"));
        var occupiedMeta=ImportBarrel(first.Output,"industrial-meta");
        Assert.False((await executor.ExecuteAsync(occupiedMeta,default)).Success);
        Assert.Equal(SecurityDecision.Denied,(await new SecurityPolicy(new WorkspacePathGuard(_root),[]).ValidateAsync(occupiedMeta,default)).Decision);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","reviews")));
    }
    [Theory][MemberData(nameof(IndustrialRecipes))]
    public void IndustrialRecipeRejectsUnboundedHeightAndInjectedCodeAndWrongRadiusConvention(string recipe)
    {
        var call=IndustrialCall(recipe);Assert.Equal(recipe,BlenderAssetSpec.From(call).Recipe);
        foreach(var item in new[]{("height_mm","99999"),("seed","-1"),("python","print('injected')"),("asset_name","../escape")}) {
            var args=new Dictionary<string,string>(call.Arguments){[item.Item1]=item.Item2};
            Assert.Throws<ArgumentException>(()=>BlenderAssetSpec.From(call with{Arguments=args}));
        }
        if(!IndustrialPropRecipes.Find(recipe)!.IsBarrel)
            Assert.Throws<ArgumentException>(()=>BlenderAssetSpec.From(call with{Arguments=new Dictionary<string,string>(call.Arguments){["radius_mm"]="340"}}));
    }
    [Theory][MemberData(nameof(IndustrialRecipes))]
    public async Task IndustrialRecipeChangedCompanionAndCancelledRunsCannotPublishOrImport(string recipe)
    {
        var exe=await BlenderFixtureExecutable();
        using var cancellation=new CancellationTokenSource();
        using(var cancelled=BlenderExecutor(exe,(_,_,_,_,token)=>{cancellation.Cancel();token.ThrowIfCancellationRequested();throw new Exception();}))
            await Assert.ThrowsAsync<OperationCanceledException>(()=>cancelled.ExecuteAsync(IndustrialCall(recipe),cancellation.Token));
        Assert.False(File.Exists(Path.Combine(_root,".forgecore","blender-library","execution.lease")));
        using var executor=BlenderExecutor(exe,IndustrialFixtureRunner);
        var result=await executor.ExecuteAsync(IndustrialCall(recipe),default);Assert.True(result.Success,result.Error);
        using var receipt=JsonDocument.Parse(result.Output);var hash=receipt.RootElement.GetProperty("bundleSha256").GetString()!;
        await File.AppendAllTextAsync(Path.Combine(_root,".forgecore","blender-library","objects",hash,"lod1.glb"),"changed");
        Assert.False((await executor.ExecuteAsync(IndustrialCall(recipe),default)).Success);
        Assert.False((await executor.ExecuteAsync(ImportBarrel(result.Output,"invalid-import"),default)).Success);
    }
    [Theory][InlineData("lod_order")][InlineData("collision")][InlineData("normal_size")][InlineData("unexpected")]
    public async Task IndustrialSourceBadLodCollisionMapsOrOutputsFailClosed(string defect)
    {
        var exe=await BlenderFixtureExecutable();
        using var executor=BlenderExecutor(exe,async(e,a,w,t,c)=>{
            var result=await IndustrialFixtureRunner(e,a,w,t,c);var output=Path.Combine(w,"output");
            var path=Path.Combine(output,"prop.json");
            if(defect=="lod_order")await File.WriteAllTextAsync(path,(await File.ReadAllTextAsync(path,c)).Replace("[120,80,40]","[120,120,40]"),c);
            if(defect=="collision")await File.WriteAllTextAsync(path,(await File.ReadAllTextAsync(path,c)).Replace("solid_box_no_walkable_interior","walkable_arbitrary_mesh"),c);
            if(defect=="normal_size")await File.WriteAllBytesAsync(Path.Combine(output,"normal.png"),MinimalPng(512,512),c);
            if(defect=="unexpected")await File.WriteAllTextAsync(Path.Combine(output,"execute.exe"),"blocked",c);
            return result;
        });
        Assert.False((await executor.ExecuteAsync(IndustrialCall("wooden_crate_v1"),default)).Success);
        Assert.False(Directory.Exists(Path.Combine(_root,".forgecore","blender-library","objects")));
    }
    private async Task<(ToolExecutor Executor,List<LocalBlenderReviewIdentity> Identities)> IndustrialPlanFixture(bool approve,int barrelRadius=340,string recipeSet="legacy_v1")
    {
        var exe=await BlenderFixtureExecutable();var executor=BlenderExecutor(exe,IndustrialFixtureRunner);
        var identities=new List<LocalBlenderReviewIdentity>();
        foreach(var recipe in IndustrialAssetPlan.SlotsFor(recipeSet).Select(slot=>IndustrialPropRecipes.Find(slot.Recipe)!)) {
            var call=IndustrialCall(recipe.Id);
            if(recipe.IsBarrel)call=call with{Arguments=new Dictionary<string,string>(call.Arguments){["radius_mm"]=barrelRadius.ToString()}};
            var result=await executor.ExecuteAsync(call,default);Assert.True(result.Success,result.Error);
            using var json=JsonDocument.Parse(result.Output);var hash=json.RootElement.GetProperty("bundleSha256").GetString()!;
            var packet=await new LocalBlenderReviewReader(new(_root)).ReadAsync(hash,default);identities.Add(packet.Identity);
            if(approve){var promotion=await SyntheticSourceApprovals(executor,packet.Identity);Assert.True((await executor.ExecuteAsync(promotion.Proposal(),default)).Success);}
        }
        return(executor,identities);
    }
    private static ToolCall IndustrialPlanCall()=>ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Read-only fixture manifest",("project_path","Game"),("level_id","yard-mission"));
    private static ToolCall IndustrialSetImport(string hash)=>ToolCall.Create(ToolKind.ImportIndustrialAssetSet,PermissionKind.Write,"Exact synthetic source set",("project_path","Game"),("level_id","yard-mission"),("plan_sha256",hash));
    [Fact]public async Task IndustrialManifestListsUnmetReviewsWithoutImplicitImportsOrSubstitution()
    {
        var fixture=await IndustrialPlanFixture(false);using var executor=fixture.Executor;
        var result=await executor.ExecuteAsync(IndustrialPlanCall(),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);var r=json.RootElement;
        Assert.False(r.GetProperty("manifest").GetProperty("readyForTargetPreparation").GetBoolean());
        Assert.False(r.GetProperty("manifest").GetProperty("readyForAutomaticPlacement").GetBoolean());
        Assert.Equal(5,r.GetProperty("manifest").GetProperty("slots").GetArrayLength());
        Assert.Equal(2,r.GetProperty("manifest").GetProperty("schemaVersion").GetInt32());
        Assert.Equal(10,r.GetProperty("manifest").GetProperty("placements").GetArrayLength());
        Assert.Equal(IndustrialPlacementLayout.Version,r.GetProperty("manifest").GetProperty("layoutVersion").GetString());
        var reviewSteps=r.GetProperty("sourceReviewPreflight").EnumerateArray().ToArray();Assert.Equal(5,reviewSteps.Length);
        foreach(var step in reviewSteps) {
            Assert.False(step.GetProperty("executed").GetBoolean());Assert.True(step.GetProperty("humanDecisionsRequired").GetBoolean());
            var candidate=Assert.Single(step.GetProperty("candidates").EnumerateArray());
            Assert.Equal("pending",candidate.GetProperty("currentVisualReview").GetString());
            Assert.False(candidate.GetProperty("visualReviewProposal").GetProperty("arguments").TryGetProperty("review_decision",out _));
        }
        Assert.False((await executor.ExecuteAsync(IndustrialSetImport(r.GetProperty("planSha256").GetString()!),default)).Success);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*.glb",SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(_root,"Game",".forgecore","unity","industrial-imports")));
    }
    [Fact]public async Task IndustrialImportRevalidatesPromotionsAndRetainsJournalWithoutTargetApprovalOrReplay()
    {
        var fixture=await IndustrialPlanFixture(true);using var executor=fixture.Executor;
        var result=await executor.ExecuteAsync(IndustrialPlanCall(),default);using var json=JsonDocument.Parse(result.Output);
        Assert.True(json.RootElement.GetProperty("manifest").GetProperty("readyForTargetPreparation").GetBoolean());
        var call=IndustrialSetImport(json.RootElement.GetProperty("planSha256").GetString()!);
        var imported=await executor.ExecuteAsync(call,default);Assert.True(imported.Success,imported.Error);
        using var receipt=JsonDocument.Parse(imported.Output);Assert.Equal(5,receipt.RootElement.GetProperty("importedAssets").GetArrayLength());
        Assert.False(receipt.RootElement.GetProperty("placementApproved").GetBoolean());
        Assert.False((await executor.ExecuteAsync(call,default)).Success);
        Assert.Equal(5,Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*-lod1.glb",SearchOption.AllDirectories).Length);
    }
    [Theory][InlineData("visual")][InlineData("rights")]
    public async Task IndustrialImportRejectsAStalePlanAfterAnySourceReviewRevocation(string kind)
    {
        var fixture=await IndustrialPlanFixture(true);using var executor=fixture.Executor;
        var result=await executor.ExecuteAsync(IndustrialPlanCall(),default);using var json=JsonDocument.Parse(result.Output);
        Assert.True((await executor.ExecuteAsync(GameArtReviewSpec.BindUserDecision(LocalBlenderReviewSpec.Proposal(fixture.Identities[0],kind),ApprovalDecision.Rejected),default)).Success);
        Assert.False((await executor.ExecuteAsync(IndustrialSetImport(json.RootElement.GetProperty("planSha256").GetString()!),default)).Success);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*.glb",SearchOption.AllDirectories));
    }
    [Fact]public async Task EvenReviewedPromotedBarrelCannotExceedTheFixedMissionFootprint()
    {
        var fixture=await IndustrialPlanFixture(true,500);using var executor=fixture.Executor;
        var result=await executor.ExecuteAsync(IndustrialPlanCall(),default);Assert.True(result.Success,result.Error);
        using var json=JsonDocument.Parse(result.Output);var manifest=json.RootElement.GetProperty("manifest");
        Assert.False(manifest.GetProperty("readyForTargetPreparation").GetBoolean());
        Assert.False(manifest.GetProperty("readyForAutomaticPlacement").GetBoolean());
        var fuel=manifest.GetProperty("slots").EnumerateArray().Single(s=>s.GetProperty("slotId").GetString()=="fuel");
        Assert.Equal("source_geometry_or_review_required",fuel.GetProperty("state").GetString());
        Assert.Contains(fuel.GetProperty("unmetRequirements").EnumerateArray(),p=>p.GetString()=="exact_authored_footprint_source");
        Assert.False((await executor.ExecuteAsync(IndustrialSetImport(json.RootElement.GetProperty("planSha256").GetString()!),default)).Success);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root,"Game","Assets"),"*.glb",SearchOption.AllDirectories));
    }
    [Fact]public async Task TargetPreflightIsReadOnlyAndOccupiedDestinationDoesNotChangeSourcePlanIdentity()
    {
        var fixture=await IndustrialPlanFixture(true);using var executor=fixture.Executor;
        var first=await executor.ExecuteAsync(IndustrialPlanCall(),default);Assert.True(first.Success,first.Error);
        using var a=JsonDocument.Parse(first.Output);var root=a.RootElement;Assert.Equal(5,root.GetProperty("targetPreparationPreflight").GetArrayLength());
        var step=root.GetProperty("manifest").GetProperty("targetPreparations")[0];
        var path=Path.Combine(_root,"Game",step.GetProperty("scenePath").GetString()!);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,"occupied user fixture scene");
        var next=await executor.ExecuteAsync(IndustrialPlanCall(),default);Assert.True(next.Success,next.Error);using var b=JsonDocument.Parse(next.Output);
        Assert.Equal(root.GetProperty("planSha256").GetString(),b.RootElement.GetProperty("planSha256").GetString());
        Assert.False(b.RootElement.GetProperty("targetPreparationPreflight")[0].GetProperty("destinationsFresh").GetBoolean());
        Assert.False(b.RootElement.GetProperty("targetPreparationPreflight")[0].GetProperty("automaticPlacementApproved").GetBoolean());
        Assert.Equal("occupied user fixture scene",await File.ReadAllTextAsync(path));
        Assert.False(Directory.Exists(Path.Combine(_root,"Game",".forgecore","unity","industrial-imports")));
    }
}
