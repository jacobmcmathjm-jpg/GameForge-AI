using ForgeCore.Agent;
using ForgeCore.Core;
using ForgeCore.Memory;
using ForgeCore.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeCore.Agent.Tests;

public sealed class GameProductionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "forgecore-production-tests", Guid.NewGuid().ToString("N"));
    public GameProductionTests() => Directory.CreateDirectory(root);
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }

    [Fact] public async Task PlainGamePromptPersistsWholePipelineAndBlueprintWithoutCallingLocalModel()
    {
        var store = await Store(); var conversation = await store.CreateConversationAsync("Production", default);
        using var stop = new GlobalCancellation(); var executor = new Executor();
        var queue = Queue(store, executor, stop, new Policy());
        var production = new GameProductionService(queue, store, store, new());
        var runner = new AgentRunner(new UnusedModel(), new Policy(), new UnusedApproval(), executor, store, stop, new(),
            NullLogger<AgentRunner>.Instance, queue, store, production);
        var result = await runner.RunAsync(conversation, "Create a first person shooter game with 3 levels", default);
        var task = Assert.Single(await store.GetRecentTasksAsync(conversation, 20, default));
        var snapshot = await store.GetWorkQueueAsync(task.Id, default); Assert.NotNull(snapshot);
        Assert.Equal(WorkQueueState.Completed, snapshot.State); Assert.Equal(snapshot.Steps.Count, executor.Calls.Count);
        Assert.Equal(task.Id, result.WorkQueueId);
        Assert.True(snapshot.Steps.Count > 12); // The reviewed workflow has its own 32-step bound, not the chat loop's 12 actions.
        var blueprint = await store.GetGameBlueprintAsync(task.Id, default); Assert.NotNull(blueprint);
        Assert.Equal(3, blueprint.Campaign.Levels.Count);
        Assert.Equal(3, executor.Calls.Count(x => x.Arguments.GetValueOrDefault("operation") == "verify_gameplay"));
        Assert.Contains("full game production remains unfinished", result.Response);
    }

    [Fact] public async Task ProductionApprovalAfterRestartResumesExactSavedPipelineAndKeepsArtGatesOpen()
    {
        var store = await Store(); var conversation = await store.CreateConversationAsync("Restart", default);
        using var stop = new GlobalCancellation(); var executor = new Executor(); var policy = new Policy(requireApproval: true);
        var production = new GameProductionService(Queue(store, executor, stop, policy), store, store, new());
        var paused = await production.RunAsync(conversation, "Create a racing game with 2 levels", default);
        Assert.Contains("AwaitingApproval", paused.Response);
        Assert.Equal(ForgeTaskState.AwaitingApproval, paused.State); Assert.NotNull(paused.WorkQueueId);
        var pending = Assert.Single(await store.GetPendingApprovalsAsync(20, default));
        Assert.Equal(ToolKind.InstallUnityBridge, pending.Call.Kind);
        var reopened = await Store();
        var grant = new TaskAuthorizationSession(); grant.Activate("Games/ForgeCore", TimeSpan.FromHours(1), 100);
        var queue = Queue(reopened, executor, stop, policy, grant);
        var recovery = new ApprovalRecoveryService(policy, executor, reopened, reopened, stop,
            NullLogger<ApprovalRecoveryService>.Instance, reopened, queue);
        var recovered = await recovery.ExecuteAsync(pending.Id, default);
        Assert.True(recovered.Success); Assert.Contains("full game production remains unfinished", recovered.Message);
        var snapshot = await reopened.GetWorkQueueAsync(pending.TaskId, default); Assert.NotNull(snapshot);
        Assert.Equal(WorkQueueState.Completed, snapshot.State);
        Assert.Equal(snapshot.Steps.Count, executor.Calls.Count);
        Assert.Equal(executor.Calls.Count, executor.Calls.Select(x => x.Id).Distinct().Count());
        Assert.False((await recovery.ExecuteAsync(pending.Id, default)).Claimed);
    }

    [Fact] public async Task FailureBlocksDependentGameBuildAndPreservesPartialEvidence()
    {
        var store = await Store(); var conversation = await store.CreateConversationAsync("Failure", default);
        using var stop = new GlobalCancellation(); var executor = new Executor("create_game_campaign");
        var production = new GameProductionService(Queue(store, executor, stop, new Policy()), store, store, new());
        var result = await production.RunAsync(conversation, "Create a platformer game", default);
        var task = Assert.Single(await store.GetRecentTasksAsync(conversation, 20, default));
        var snapshot = await store.GetWorkQueueAsync(task.Id, default); Assert.NotNull(snapshot);
        Assert.Equal(WorkQueueState.Failed, snapshot.State);
        Assert.DoesNotContain(executor.Calls, x => x.Arguments.GetValueOrDefault("operation") == "build_windows");
        Assert.Contains(snapshot.Steps, x => x.State == WorkQueueStepState.Blocked);
        Assert.Contains("Failed", result.Response); Assert.DoesNotContain("build checks finished", result.Response);
        Assert.Equal(ForgeTaskState.Failed, result.State);
    }

    [Fact] public async Task DeniedPolicyNeverConsumesTrustedProjectGrantOrExecutesMutation()
    {
        var store = await Store(); var conversation = await store.CreateConversationAsync("Denied", default);
        using var stop = new GlobalCancellation(); var executor = new Executor();
        var grant = new TaskAuthorizationSession(); grant.Activate("Games/ForgeCore", TimeSpan.FromHours(1), 100);
        var production = new GameProductionService(Queue(store, executor, stop, new Policy(denyWrites: true), grant), store, store, new());
        await production.RunAsync(conversation, "Create a racing game", default);
        Assert.All(executor.Calls, x => Assert.Equal(PermissionKind.Read, x.RequiredPermission));
        Assert.Equal(100, grant.Snapshot.RemainingActions);
    }

    [Fact] public async Task UnsupportedGenreProducesNoQueueAndNoMutations()
    {
        var store = await Store(); var conversation = await store.CreateConversationAsync("Strategy", default);
        using var stop = new GlobalCancellation(); var executor = new Executor();
        var production = new GameProductionService(Queue(store, executor, stop, new Policy()), store, store, new());
        var result = await production.RunAsync(conversation, "Create a strategy game", default);
        Assert.Empty(executor.Calls); Assert.Empty(await store.GetRecentTasksAsync(conversation, 20, default));
        Assert.Contains("has not been substituted", result.Response);
        Assert.Equal(ForgeTaskState.Failed, result.State); Assert.Null(result.WorkQueueId);
    }

    private async Task<SqliteForgeStore> Store()
    {
        var store = new SqliteForgeStore(Path.Combine(root, "production.db")); await store.InitializeAsync(); return store;
    }
    private static WorkQueueService Queue(SqliteForgeStore store, IToolExecutor tools, IGlobalCancellation stop,
        ISecurityPolicy policy, IAutomaticApprovalGrant? grant = null) =>
        new(policy, tools, store, store, store, stop, NullLogger<WorkQueueService>.Instance, grant);
    private sealed class Policy(bool requireApproval = false, bool denyWrites = false) : ISecurityPolicy
    {
        public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken token) => ValueTask.FromResult(new PolicyResult(
            call.RequiredPermission == PermissionKind.Read ? SecurityDecision.Allowed : denyWrites ? SecurityDecision.Denied :
            requireApproval ? SecurityDecision.ApprovalRequired : SecurityDecision.Allowed, "Production test policy"));
    }
    private sealed class Executor(string? failOperation = null) : IToolExecutor
    {
        public List<ToolCall> Calls { get; } = [];
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token)
        {
            Calls.Add(call); var fail = failOperation is not null && call.Arguments.GetValueOrDefault("operation") == failOperation;
            return Task.FromResult(new ToolResult(call.Id, !fail, fail ? "" : "Verified test execution", fail ? "Injected build-stage failure" : null));
        }
    }
    private sealed class UnusedModel : ILocalModel
    {
        public ModelStatus Status => new("unused", true, "Must not be called for a production workflow");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) => throw new InvalidOperationException("Local model was called");
    }
    private sealed class UnusedApproval : IApprovalService
    {
        public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken token) => throw new InvalidOperationException("Chat approval path was called");
    }
}
