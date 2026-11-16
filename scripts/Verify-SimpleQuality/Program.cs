using System.Text;
using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.LocalAI;
using ForgeCore.Security;
using ForgeCore.Tools;

// Actual plain request -> production tools -> verified human preview boundary.
// No live project, normal app/SQLite, human decision, Unity or provider is used.
if (args.Length > 1 || args.Length == 1 && args[0] != "--reuse") throw new ArgumentException("Only fixed optional --reuse mode is supported.");
var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (!File.Exists(Path.Combine(repository, "ForgeCore.sln"))) throw new IOException("Repository root not found.");
var root = Path.Combine(repository, "work", "simple-quality", "production-v1"); var reuse = args.Length != 0;
if (!reuse)
{
    if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fresh verification folder exists; no overwrite.");
    Directory.CreateDirectory(Path.Combine(root, "Game", "Assets")); Directory.CreateDirectory(Path.Combine(root, "Game", "ProjectSettings"));
    await WriteNew(Path.Combine(root, "Game", "ProjectSettings", "ProjectVersion.txt"), Encoding.UTF8.GetBytes("m_EditorVersion: 6000.5.5f1\n"));
}
else if (!File.Exists(Path.Combine(root, "fresh-preview-stop.json"))) throw new IOException("Run the fresh proof first.");
var guard = new WorkspacePathGuard(root); const string blender = @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";
var policy = new SecurityPolicy(guard, [], blenderPath: blender);
using var tools = new ToolExecutor(guard, [], [], 100 * 1024 * 1024, blenderPath: blender, blenderTimeoutSeconds: 600);
await using var model = new BuiltInBlenderAssetModel(new NoInference(), "Game");
var prompt = reuse ? "Make me a detailed crate named reused-quality-crate." : "Make me a detailed wooden crate named plain-quality-crate.";
var observations = new List<ToolObservation>(); var reached = false;
for (var step = 0; step < 8; step++)
{
    var plan = await model.CreatePlanAsync(new(prompt, [], observations, step + 1, 12 - observations.Count), default);
    if (plan.Actions.Count != 1) throw new IOException("Plain workflow stopped before verified review boundary: " + plan.Response);
    var call = plan.Actions.Single();
    if (call.Kind == ToolKind.ReviewGameArtAsset)
    {
        var review = LocalBlenderReviewSpec.FromProposal(call);
        var packet = await new LocalBlenderReviewReader(guard).ReadAsync(review.Identity.BundleSha256, default);
        if (packet.Identity != review.Identity || review.ReviewKind != "visual" || review.Decision is not null ||
            observations.Count(o => o.Kind == ToolKind.CreateBlenderAsset) != (reuse ? 0 : 1)) throw new IOException("Unexpected review/source identity.");
        await WriteNew(Path.Combine(root, reuse ? "reuse-preview-stop.json" : "fresh-preview-stop.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, prompt, observations, pendingProposal = call, packet.Identity, packet.SourcePath,
            actualSourceGenerated = !reuse, exactPriorSourceSelected = reuse, anyReviewExecuted = false, anyImportExecuted = false,
            providerUsed = false, unityLaunched = false, normalAppOrLiveSqliteOpened = false,
            qualification = "Actual ordinary prompt and production source/quality/preview verification. Stops BEFORE live human review. Not final appearance/rights acceptance, automatic repair or full level creation."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("VERIFIED_PLAIN_REQUEST_STOPPED_BEFORE_HUMAN_REVIEW " + review.Identity.BundleSha256); reached = true; break;
    }
    if (call.Kind is not (ToolKind.SearchLocalBlenderAssets or ToolKind.CreateBlenderAsset or ToolKind.InspectLocalBlenderAsset))
        throw new IOException("Pre-review route escaped read/source generation tools.");
    var check = await policy.ValidateAsync(call, default); if (check.Decision == SecurityDecision.Denied) throw new IOException(check.Reason);
    Console.WriteLine("START " + call.Kind); var result = await tools.ExecuteAsync(call, default);
    if (!result.Success) throw new IOException(result.Error);
    observations.Add(new(call.Id, call.Kind, call.Description, call.Arguments, "Succeeded", true, result.Output));
}
if (!reached) throw new IOException("Review boundary was not reached within bounded steps.");
static async Task WriteNew(string path, byte[] bytes)
{
    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); await file.WriteAsync(bytes);
}
sealed class NoInference : ILocalModel
{
    public ModelStatus Status => new("offline_proof", true, "No inference expected");
    public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) => throw new InvalidOperationException("Unexpected inference/provider fallback");
}
