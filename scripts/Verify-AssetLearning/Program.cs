using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
using ForgeCore.Security;
using ForgeCore.Tools;

// Fresh isolated repository-owned workspace. No live app config/store, D-drive write,
// Unity launch, human decisions, provider or neural model. Real Blender is serialized.
if (args.Length > 1 || args.Length == 1 && args[0] != "--guided-reuse") throw new ArgumentException("Only the fixed --guided-reuse follow-up is supported.");
const string blender = @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
// bin/<configuration>/<framework>/ is three levels below this script project.
if (!File.Exists(Path.Combine(repository, "ForgeCore.sln"))) throw new IOException("Cannot identify repository root.");
var root = Path.Combine(repository, "work", "asset-learning", "production-v1");
if (args.Length == 1)
{
    if (!File.Exists(Path.Combine(root, "verification.json"))) throw new IOException("Run the fresh generation proof first.");
    var guidedGuard = new WorkspacePathGuard(root);
    using var guidedTools = new ToolExecutor(guidedGuard, []);
    var guidedPolicy = new SecurityPolicy(guidedGuard, []);
    await using var guidedModel = new BuiltInBlenderAssetModel(new NoInference(), "Game");
    const string guidedPrompt = "Create an experience-guided wooden crate in Blender. Name it learning-picked-crate.";
    var observations = new List<ToolObservation>(); var completed = false;
    for (var step = 0; step < 4; step++)
    {
        var plan = await guidedModel.CreatePlanAsync(new(guidedPrompt, [], observations, step + 1, 8 - step), default);
        var call = plan.Actions.Single();
        if (call.Kind is not (ToolKind.SearchLocalBlenderAssets or ToolKind.InspectLocalBlenderAsset or ToolKind.ImportBlenderAsset))
            throw new IOException("Guided reuse escaped its bounded read/import route.");
        var check = await guidedPolicy.ValidateAsync(call, default);
        if (check.Decision == SecurityDecision.Denied) throw new IOException(check.Reason);
        var result = await guidedTools.ExecuteAsync(call, default);
        if (!result.Success) throw new IOException(result.Error);
        observations.Add(new(call.Id, call.Kind, call.Description, call.Arguments, "Succeeded", true, result.Output));
        if (call.Kind == ToolKind.ImportBlenderAsset)
        {
            await WriteNew(Path.Combine(root, "guided-reuse-verification.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, steps = observations, additionalSourceGeneration = false, humanDecisionRecorded = false,
                providerUsed = false, unityLaunched = false, qualification = "Production built-in planner reads prior sources/experience and imports exact compatible source into isolated scratch project. Stops before Unity; no live-game or art acceptance claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            completed = true; break;
        }
    }
    if (!completed) throw new IOException("Guided reuse did not reach its exact technical import boundary.");
    Console.WriteLine("EXPERIENCE_GUIDED_EXACT_SOURCE_REUSE_PASS");
    return;
}
if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Owned verification folder exists; no overwrite/replay.");
Directory.CreateDirectory(Path.Combine(root, "Game", "Assets"));
Directory.CreateDirectory(Path.Combine(root, "Game", "ProjectSettings"));
await WriteNew(Path.Combine(root, "Game", "ProjectSettings", "ProjectVersion.txt"), Encoding.UTF8.GetBytes("m_EditorVersion: 6000.5.5f1\n"));
var guard = new WorkspacePathGuard(root);
var policy = new SecurityPolicy(guard, [], blenderPath: blender);
using var tools = new ToolExecutor(guard, [], [], 100 * 1024 * 1024, blenderPath: blender, blenderTimeoutSeconds: 600);
var results = new List<object>(); string? bundle = null;
foreach (var variant in new[] { (Name: "learning-crate-a", Seed: "41"), (Name: "learning-crate-a", Seed: "41"), (Name: "learning-crate-b", Seed: "97") })
{
    var call = ToolCall.Create(ToolKind.CreateBlenderAsset, PermissionKind.Execute, "Approved isolated real production-executor learning test; no art acceptance",
        ("project_path", "Game"), ("asset_name", variant.Name), ("recipe", "wooden_crate_v2"), ("seed", variant.Seed), ("height_mm", "800"));
    var check = await policy.ValidateAsync(call, default);
    if (check.Decision != SecurityDecision.ApprovalRequired) throw new IOException("Unexpected generation policy result.");
    Console.WriteLine("START " + variant.Name + " seed=" + variant.Seed);
    var result = await tools.ExecuteAsync(call, default);
    if (!result.Success || result.LearningStatus != "recorded_advisory_only") throw new IOException(result.Error ?? result.LearningStatus);
    using var receipt = JsonDocument.Parse(result.Output); bundle = receipt.RootElement.GetProperty("bundleSha256").GetString()!;
    results.Add(new { call.Id, call.Arguments, result.Success, result.LearningStatus, result.Output });
    Console.WriteLine("COMPLETE reused=" + receipt.RootElement.GetProperty("reused").GetBoolean());
}
await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
var prompt = "Plan improvements for local Blender asset " + bundle + ".";
var read = (await model.CreatePlanAsync(new(prompt, []), default)).Actions.Single();
if (read.Kind != ToolKind.InspectLocalBlenderAsset || (await policy.ValidateAsync(read, default)).Decision != SecurityDecision.Allowed)
    throw new IOException("Learning plan escaped a read-only source boundary.");
var inspected = await tools.ExecuteAsync(read, default);
if (!inspected.Success) throw new IOException(inspected.Error);
using (var packet = JsonDocument.Parse(inspected.Output))
{
    var plan = packet.RootElement.GetProperty("learningPlan"); var source = plan.GetProperty("stages")[0];
    if (source.GetProperty("recordedAttempts").GetInt32() != 3 || source.GetProperty("distinctSuccessfulCandidates").GetInt32() != 2 ||
        source.GetProperty("cacheReuses").GetInt32() != 1 || plan.GetProperty("grantsApproval").GetBoolean())
        throw new IOException("Actual generation experience was not retained/distinguished correctly.");
}
var done = await model.CreatePlanAsync(new(prompt, [], [new(read.Id, read.Kind, read.Description, read.Arguments, "Succeeded", true, inspected.Output)], 2, 8), default);
if (done.Actions.Count != 0 || !done.Response.Contains("source: 3", StringComparison.Ordinal)) throw new IOException("Read-only offline learning route did not finish correctly.");
await WriteNew(Path.Combine(root, "verification.json"), JsonSerializer.SerializeToUtf8Bytes(new
{
    schemaVersion = 1, realBlender = true, sourceGenerationCalls = 3, exactCacheReuses = 1, distinctGeneratedCandidates = 2,
    results, learningPacket = JsonDocument.Parse(inspected.Output).RootElement.Clone(), response = done.Response,
    neuralTraining = false, humanDecisionRecorded = false, unityLaunched = false, liveGameChanged = false,
    qualification = "Actual isolated production generation/cache/history/prompt proof. Existing frozen recipe produced both candidates; no learned geometry rewrite or visual improvement claim. Motion recording is separately covered by synthetic host plumbing tests, not new real Unity tests."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("REAL_ASSET_EXPERIENCE_PASS " + root);

static async Task WriteNew(string path, byte[] bytes)
{
    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await file.WriteAsync(bytes);
}
sealed class NoInference : ILocalModel
{
    public ModelStatus Status => new("offline_probe", true, "No inference expected");
    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) => throw new InvalidOperationException("Unexpected provider/inference fallback");
}
