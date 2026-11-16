using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
using ForgeCore.Security;
using ForgeCore.Tools;

// Actual production components, read-only against the local source library. No UI/store/editor/provider.
const string workspace=@"D:\ForgeCore\Workspace";
var guard=new WorkspacePathGuard(workspace);var policy=new SecurityPolicy(guard,[]);
using var executor=new ToolExecutor(guard,[]);
await using var model=new BuiltInBlenderAssetModel(new NoFallback(),"Games/ForgeCore");
var objects=Path.Combine(workspace,".forgecore","blender-library","objects");
var before=Snapshot();
if(args.Length==1&&args[0]=="--reuse-two-projects")
{
    await ReuseTwoProjects();return;
}
if(args.Length==1&&args[0]=="--source-review-packet")
{
    await VerifySourceReviewPacket();return;
}
if(args.Length==1&&args[0]=="--source-compatibility")
{
    await VerifySourceCompatibility();return;
}
if(args.Length!=0)throw new ArgumentException("Use default read-only search, --reuse-two-projects, --source-review-packet or --source-compatibility.");
var records=new List<object>();
foreach(var query in new[]{"barrel","industrial security","rigged mannequin"})
{
    var prompt=$"Search local Blender assets for {query}.";
    var action=(await model.CreatePlanAsync(new(prompt,[]),default)).Actions.Single();
    if(action.Kind!=ToolKind.SearchLocalBlenderAssets || (await policy.ValidateAsync(action,default)).Decision!=SecurityDecision.Allowed)
        throw new InvalidOperationException("Read-only library route differed.");
    var result=await executor.ExecuteAsync(action,default);if(!result.Success)throw new InvalidOperationException(result.Error);
    using var parsed=JsonDocument.Parse(result.Output);
    var observation=new ToolObservation(action.Id,action.Kind,action.Description,action.Arguments,"Succeeded",true,result.Output);
    var done=await model.CreatePlanAsync(new(prompt,[],[observation],2,4),default);
    if(done.Actions.Count!=0)throw new InvalidOperationException("Search must not trigger imports/reviews.");
    records.Add(new{prompt,tool=action.Kind.ToString(),output=parsed.RootElement.Clone(),response=done.Response});
}
var after=Snapshot();if(before.Count!=after.Count||before.Any(x=>!after.TryGetValue(x.Key,out var h)||h!=x.Value))
    throw new InvalidOperationException("Local source library changed during read-only validation.");
var evidence=new{schemaVersion=1,kind="real_local_library_readonly_component_route",createdUtc=DateTimeOffset.UtcNow,
    kitVersion="0.16.49_unchanged",libraryFilesUnchanged=before.Count,records,
    qualification="Actual local model-wrapper/policy/tool route. No inference/provider fallback, editor launch, import, review approval or persisted GUI/store/restart acceptance."};
var destination=Path.Combine(Environment.CurrentDirectory,"work","s07-library-verification","live-readonly-receipt.json");
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
await using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
    await JsonSerializer.SerializeAsync(output,evidence,new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine(JsonSerializer.Serialize(new{receipt=destination,libraryFilesUnchanged=before.Count,queries=records.Count}));
Dictionary<string,string> Snapshot()
{
    var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
    if(!Directory.Exists(objects))return hashes;
    foreach(var path in Directory.EnumerateFiles(objects,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,MaxRecursionDepth=3}))
    {
        var resolved=guard.Resolve(path,false);
        hashes.Add(Path.GetRelativePath(objects,resolved),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(resolved))).ToLowerInvariant());
    }
    return hashes;
}
async Task VerifySourceCompatibility()
{
    var destination=Path.Combine(Environment.CurrentDirectory,"work","s07-promotion-verification","live-source-compatibility.json");
    if(File.Exists(destination))throw new IOException("Compatibility evidence already exists; do not overwrite it.");
    var statesBefore=StateSnapshot();var records=new List<object>();
    foreach(var query in new[]{"barrel","industrial security","rigged mannequin"})
    {
        var found=await executor.ExecuteAsync(ToolCall.Create(ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Read actual local candidates",("query",query),("max_results","1")),default);
        if(!found.Success)throw new InvalidOperationException(found.Error);using var results=JsonDocument.Parse(found.Output);
        var hash=results.RootElement.GetProperty("results").EnumerateArray().Single().GetProperty("bundleSha256").GetString()!;
        var prompt=$"Check local Blender asset {hash}.";var call=(await model.CreatePlanAsync(new(prompt,[]),default)).Actions.Single();
        if(call.Kind!=ToolKind.CheckLocalBlenderCompatibility||(await policy.ValidateAsync(call,default)).Decision!=SecurityDecision.Allowed)throw new InvalidOperationException("Compatibility must remain read-only.");
        var result=await executor.ExecuteAsync(call,default);if(!result.Success)throw new InvalidOperationException(result.Error);
        using var json=JsonDocument.Parse(result.Output);var r=json.RootElement;
        if(r.GetProperty("eligibleForSourcePromotion").GetBoolean()||r.GetProperty("targetProjectApproved").GetBoolean()||
            r.GetProperty("sourceVisualReview").GetProperty("state").GetString()!="pending"||r.GetProperty("sourceRightsReview").GetProperty("state").GetString()!="pending")
            throw new InvalidOperationException("This negative admission probe expects unreviewed real sources; no approval or write is executed.");
        var observation=new ToolObservation(call.Id,call.Kind,call.Description,call.Arguments,"Succeeded",true,result.Output);
        var checkedPlan=await model.CreatePlanAsync(new(prompt,[],[observation],2,5),default);
        var blockedPlan=await model.CreatePlanAsync(new($"Promote local Blender asset {hash}.",[],[observation],2,5),default);
        if(checkedPlan.Actions.Count!=0||blockedPlan.Actions.Count!=0)throw new InvalidOperationException("No follow-up, human attestation or promotion may run for pending reviews.");
        var constrained=ToolCall.Create(ToolKind.CheckLocalBlenderCompatibility,PermissionKind.Read,"Intentionally unsuitable triangle budget",("bundle_sha256",hash),("max_triangles","1"));
        var mismatch=await executor.ExecuteAsync(constrained,default);if(!mismatch.Success)throw new InvalidOperationException(mismatch.Error);
        using var incompatible=JsonDocument.Parse(mismatch.Output);
        if(incompatible.RootElement.GetProperty("matchesSourceConstraints").GetBoolean())throw new InvalidOperationException("Measured source exceeds this intentionally unsuitable budget.");
        records.Add(new{query,output=r.Clone(),checkResponse=checkedPlan.Response,promotionResponse=blockedPlan.Response,
            rejectedConstraints=incompatible.RootElement.GetProperty("incompatibilities").Clone(),promotionExecuted=false});
    }
    var after=Snapshot();var statesAfter=StateSnapshot();
    if(before.Count!=after.Count||before.Any(x=>!after.TryGetValue(x.Key,out var h)||h!=x.Value)||
        statesBefore.Count!=statesAfter.Count||statesBefore.Any(x=>!statesAfter.TryGetValue(x.Key,out var h)||h!=x.Value))throw new InvalidOperationException("Source or decision/promotion state changed.");
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    await using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,kind="actual_local_source_compatibility_and_negative_promotion_admission",createdUtc=DateTimeOffset.UtcNow,
            sourceFilesUnchanged=before.Count,reviewAndPromotionFilesUnchanged=statesAfter.Count,records,
            qualification="Actual offline wrapper/policy/tool read-only route. Pending real human reviews block promotion. No real attestation, source promotion, editor launch, import, model/provider, target game modification or persisted WPF/restart acceptance."},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine(JsonSerializer.Serialize(new{receipt=destination,sourceFilesUnchanged=before.Count,reviewAndPromotionFilesUnchanged=statesAfter.Count,queries=records.Count}));
    Dictionary<string,string> StateSnapshot()
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var name in new[]{"reviews","approved"})
        {
            var root=guard.Resolve($".forgecore/blender-library/{name}",true);if(!Directory.Exists(root))continue;
            foreach(var file in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,MaxRecursionDepth=4}))
            {var path=guard.Resolve(file,false);result.Add(Path.GetRelativePath(workspace,path),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());}
        }
        return result;
    }
}
async Task ReuseTwoProjects()
{
    var receiptPath=Path.Combine(Environment.CurrentDirectory,"work","s07-library-verification","two-project-reuse-receipt.json");
    if(File.Exists(receiptPath))throw new InvalidOperationException("Receipt already exists; do not replay an ambiguous import.");
    var projects=new[]{"Games/S07-LibraryReuse-A-20261028","Games/S07-LibraryReuse-B-20261028"};
    foreach(var project in projects)if(Directory.Exists(guard.Resolve(project,true))||File.Exists(guard.Resolve(project,true)))
        throw new InvalidOperationException("A target project exists; neither target will be replayed.");
    var search=ToolCall.Create(ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Find exact existing local barrel",("query","barrel"),("recipe",BlenderAssetSpec.SupportedRecipe),("max_results","1"));
    var found=await executor.ExecuteAsync(search,default);if(!found.Success)throw new InvalidOperationException(found.Error);
    using var result=JsonDocument.Parse(found.Output);var candidate=result.RootElement.GetProperty("results").EnumerateArray().Single();
    var bundle=candidate.GetProperty("bundleSha256").GetString()!;var expected=candidate.GetProperty("assetSha256").GetString()!;
    var actions=new List<object>();
    foreach(var project in projects)
    {
        await Execute(ToolCall.Create(ToolKind.ScaffoldProject,PermissionKind.Write,"Fresh isolated local reuse test project",
            ("path",project),("template","unity-basic"),("name",Path.GetFileName(project)),("unity_version","6000.5.5f1")));
        var import=ToolCall.Create(ToolKind.ImportBlenderAsset,PermissionKind.Write,"Exact candidate reuse; no review or placement approval",
            ("project_path",project),("asset_name","s07-reused-barrel"),("bundle_sha256",bundle),("expected_asset_sha256",expected));
        await Execute(import);
        var file=guard.Resolve(Path.Combine(project,BlenderAssetImportSpec.From(import).DestinationPath),false);
        if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant()!=expected)
            throw new InvalidOperationException("Fresh target differs from exact source.");
        if((await policy.ValidateAsync(import,default)).Decision!=SecurityDecision.Denied || (await executor.ExecuteAsync(import,default)).Success)
            throw new InvalidOperationException("Occupied destination must be denied and left intact.");
        Console.WriteLine("VERIFIED_EXACT_CANDIDATE_IMPORT "+project);
    }
    var after=Snapshot();if(before.Count!=after.Count||before.Any(x=>!after.TryGetValue(x.Key,out var h)||h!=x.Value))throw new InvalidOperationException("Immutable source library changed.");
    Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
    await using(var stream=new FileStream(receiptPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,createdUtc=DateTimeOffset.UtcNow,projects,bundleSha256=bundle,assetSha256=expected,
            sourceFilesUnchanged=before.Count,actions,occupiedImportsRejected=true,reviewState="unreviewed_local_recipe",visualReviewed=false,rightsReviewed=false,
            targetUnityInspectionComplete=false,qualification="Actual policy/tool fresh-project and exact-copy verification under the calling development request. No persistent app approval, source promotion, Unity/Blender launch, package install or target-game acceptance."},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine("REUSE_RECEIPT "+receiptPath);
    async Task Execute(ToolCall call)
    {
        var validation=await policy.ValidateAsync(call,default);if(validation.Decision==SecurityDecision.Denied)throw new InvalidOperationException(validation.Reason);
        var executed=await executor.ExecuteAsync(call,default);if(!executed.Success)throw new InvalidOperationException(executed.Error);
        actions.Add(new{tool=call.Kind.ToString(),call.Arguments,policy=validation.Decision.ToString(),executed.Output});
    }
}
async Task VerifySourceReviewPacket()
{
    var destination=Path.Combine(Environment.CurrentDirectory,"work","s07-review-verification","live-source-packet.json");
    if(File.Exists(destination))throw new IOException("Review-packet evidence already exists; do not overwrite it.");
    var libraryReviewRoot=Path.Combine(workspace,".forgecore","blender-library","reviews");
    var reviewFilesBefore=Directory.Exists(libraryReviewRoot)?Directory.GetFiles(libraryReviewRoot,"*",SearchOption.AllDirectories).Length:0;
    var search=await executor.ExecuteAsync(ToolCall.Create(ToolKind.SearchLocalBlenderAssets,PermissionKind.Read,"Find source candidate",("query","barrel"),("max_results","1")),default);
    if(!search.Success)throw new InvalidOperationException(search.Error);
    using var found=JsonDocument.Parse(search.Output);var hash=found.RootElement.GetProperty("results").EnumerateArray().Single().GetProperty("bundleSha256").GetString()!;
    var records=new List<object>();
    foreach(var suffix in new[]{"visually","rights"})
    {
        var prompt=$"Review local Blender asset {hash} {suffix}.";
        var inspect=(await model.CreatePlanAsync(new(prompt,[]),default)).Actions.Single();
        if(inspect.Kind!=ToolKind.InspectLocalBlenderAsset||(await policy.ValidateAsync(inspect,default)).Decision!=SecurityDecision.Allowed)throw new InvalidOperationException("Inspection must remain read-only.");
        var output=await executor.ExecuteAsync(inspect,default);if(!output.Success)throw new InvalidOperationException(output.Error);
        var observation=new ToolObservation(inspect.Id,inspect.Kind,inspect.Description,inspect.Arguments,"Succeeded",true,output.Output);
        var proposal=(await model.CreatePlanAsync(new(prompt,[],[observation],2,5),default)).Actions.Single();
        if(!LocalBlenderReviewSpec.IsLocal(proposal)||(await policy.ValidateAsync(proposal,default)).Decision!=SecurityDecision.ApprovalRequired)throw new InvalidOperationException("Only a live human review may follow.");
        var grant=new TaskAuthorizationSession();grant.Activate("Games/ForgeCore",TimeSpan.FromMinutes(1),1);
        if(grant.TryConsume(new(proposal,"Human source review")))throw new InvalidOperationException("Automatic grants must never approve source reviews.");
        using var packet=JsonDocument.Parse(output.Output);records.Add(new{prompt,packet=packet.RootElement.Clone(),proposal=proposal.Arguments,policy="ApprovalRequired",executed=false});
    }
    var after=Snapshot();if(before.Count!=after.Count||before.Any(x=>!after.TryGetValue(x.Key,out var h)||h!=x.Value))throw new InvalidOperationException("Source library changed.");
    var reviewFilesAfter=Directory.Exists(libraryReviewRoot)?Directory.GetFiles(libraryReviewRoot,"*",SearchOption.AllDirectories).Length:0;
    if(reviewFilesBefore!=reviewFilesAfter)throw new InvalidOperationException("Read-only preparation may not create source review records.");
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);await using(var stream=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,createdUtc=DateTimeOffset.UtcNow,sourceFilesUnchanged=before.Count,reviewFilesBefore,reviewFilesAfter,records,
            qualification="Actual read-only component preparation stops at live human review; no approval, rejection, import, promotion, database, editor or provider operation."},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine("SOURCE_REVIEW_PACKET_RECEIPT "+destination);
}
sealed class NoFallback:ILocalModel
{
    public ModelStatus Status=>new("test",true,"read-only deterministic wrapper; no inference");
    public Task<AgentPlan>CreatePlanAsync(ModelRequest request,CancellationToken token)=>throw new InvalidOperationException("No inference or provider fallback permitted.");
}
