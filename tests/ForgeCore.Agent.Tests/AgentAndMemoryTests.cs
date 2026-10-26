using ForgeCore.Agent;
using ForgeCore.Core;
using ForgeCore.Memory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeCore.Agent.Tests;
public sealed class AgentAndMemoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "forgecore-agent-tests", Guid.NewGuid().ToString("N"));
    public AgentAndMemoryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(_dir, true); }

    [Fact] public async Task DatabaseIsCreatedAndPersistsConversation()
    {
        var path = Path.Combine(_dir, "memory.db"); var store = new SqliteForgeStore(path);
        await store.InitializeAsync(); var id = await store.CreateConversationAsync("test", default);
        await store.RenameConversationAsync(id, "renamed", default);
        await store.AddMessageAsync(id, "user", "hello", default);
        var taskId = Guid.NewGuid(); await store.CreateTaskAsync(taskId, id, "persist task", ForgeTaskState.Pending, default); await store.UpdateTaskStateAsync(taskId, ForgeTaskState.Planning, default);
        await store.AuditAsync(new(DateTimeOffset.UtcNow, "Test", id.ToString(), "persisted"), default);
        Assert.True(File.Exists(path));
        await using var connection = new SqliteConnection($"Data Source={path}"); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Messages";
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        var conversation = (await store.GetRecentConversationsAsync(10, default)).Single(); Assert.Equal(id, conversation.Id); Assert.Equal("renamed", conversation.Title);
        Assert.Equal("hello", (await store.GetMessagesAsync(id, default)).Single().Content);
        Assert.Equal(ForgeTaskState.Planning, (await store.GetRecentTasksAsync(id, 10, default)).Single().State);
        Assert.Equal("persisted", (await store.GetRecentAuditEventsAsync(10, default)).Single().Detail);
    }

    [Fact] public async Task RejectedApproval_PreventsExecution()
    {
        var store = new MemoryStore(); var call = ToolCall.Create(ToolKind.WriteTextFile, PermissionKind.Write, "write", ("path", "x"));
        var executor = new CountingExecutor(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(new FixedModel(call), new FixedPolicy(SecurityDecision.ApprovalRequired), new FixedApproval(false), executor, store, global, new(), NullLogger<AgentRunner>.Instance);
        var result = await runner.RunAsync(Guid.NewGuid(), "do it", default);
        Assert.Equal(0, executor.Count); Assert.False(result.ToolResults.Single().Success); Assert.Contains("test", result.Response);
    }

    [Fact] public async Task HumanGameArtReview_BindsLiveRejectAndExecutesWhileCancellationRecordsNothing()
    {
        var hash = new string('a', 64);
        var review = ToolCall.Create(ToolKind.ReviewGameArtAsset, PermissionKind.Write, "human visual review",
            ("project_path", "Game"), ("review_kind", GameArtReviewSpec.Visual), ("asset_set_id", "frontier-watchtower"),
            ("asset_path", "Assets/ForgeCore/Generated/frontier-watchtower.glb"), ("expected_asset_sha256", hash),
            ("preview_path", "Previews/GameArt/frontier-watchtower.png"), ("expected_preview_sha256", hash),
            ("expected_preview_provenance_sha256", hash));
        var model = new ScriptedModel(new AgentPlan("Review it.", [review]), new AgentPlan("Review recorded.", []));
        var executor = new RecordingExecutor(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.ApprovalRequired),
            new DecisionApproval(ApprovalDecision.Rejected), executor, new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "review exact art", default);

        Assert.True(result.ToolResults.Single().Success); Assert.NotNull(executor.LastCall);
        Assert.Equal("rejected", executor.LastCall.Arguments["review_decision"]);
        Assert.Equal(GameArtReviewSpec.UserLiveApproval, executor.LastCall.Arguments["attestation_source"]);

        var cancelledExecutor = new RecordingExecutor();
        var cancelledRunner = new AgentRunner(new FixedModel(review), new FixedPolicy(SecurityDecision.ApprovalRequired),
            new DecisionApproval(ApprovalDecision.Cancelled), cancelledExecutor, new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);
        var cancelled = await cancelledRunner.RunAsync(Guid.NewGuid(), "close review", default);
        Assert.Equal(0, cancelledExecutor.Count); Assert.False(cancelled.ToolResults.Single().Success);
        Assert.Contains("without recording", cancelled.ToolResults.Single().Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task AgentPersistsVersionedBlueprintForGameGenerationTask()
    {
        var path = Path.Combine(_dir, "blueprint.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("game", default); using var global = new GlobalCancellation();
        var runner = new AgentRunner(new EmptyModel(), new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true),
            new CountingExecutor(), store, global, new(), NullLogger<AgentRunner>.Instance);

        await runner.RunAsync(conversation, "Create a racing game with controller support", default);

        var task = (await store.GetRecentTasksAsync(conversation, 1, default)).Single();
        var blueprint = await store.GetGameBlueprintAsync(task.Id, default);
        Assert.NotNull(blueprint); Assert.Equal(GameBlueprint.CurrentSchemaVersion, blueprint.SchemaVersion);
        Assert.Equal("racing", blueprint.PrimaryGenreId); Assert.Contains("race_rules", blueprint.SystemPackIds);
        Assert.Contains("controller_support", blueprint.SystemPackIds);
        Assert.Equal(3, blueprint.Campaign.Levels.Count); Assert.Equal(2, blueprint.Campaign.Transitions.Count);
        Assert.NotNull(blueprint.ArtSet); Assert.Contains(blueprint.ArtSet.Slots, x => x.Role == GameArtAssetRole.Vehicles);
    }

    [Fact] public async Task BlueprintSurvivesStoreReinitializationWithoutPersistingPromptText()
    {
        var path = Path.Combine(_dir, "blueprint-reopen.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("game", default); using var global = new GlobalCancellation();
        var runner = new AgentRunner(new EmptyModel(), new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true),
            new CountingExecutor(), store, global, new(), NullLogger<AgentRunner>.Instance);
        const string privateMarker = "PRIVATE_BLUEPRINT_MARKER_7429";
        await runner.RunAsync(conversation, $"Create a strategy game with resources. {privateMarker}", default);
        var task = (await store.GetRecentTasksAsync(conversation, 1, default)).Single();

        var reopened = new SqliteForgeStore(path); await reopened.InitializeAsync();
        var blueprint = await reopened.GetGameBlueprintAsync(task.Id, default);

        Assert.NotNull(blueprint); Assert.Equal("strategy", blueprint.PrimaryGenreId);
        Assert.DoesNotContain(privateMarker, System.Text.Json.JsonSerializer.Serialize(blueprint), StringComparison.Ordinal);
        Assert.Contains((await reopened.GetRecentAuditEventsAsync(20, default)), x =>
            x.EventType == "GameBlueprintCreated" && !x.Detail.Contains(privateMarker, StringComparison.Ordinal));
    }

    [Fact] public async Task StoreReadsLegacySchemaTwoBlueprintThroughDeterministicArtSetUpgrade()
    {
        var path = Path.Combine(_dir, "blueprint-legacy.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("legacy", default); var taskId = Guid.NewGuid();
        await store.CreateTaskAsync(taskId, conversation, "legacy game", ForgeTaskState.Planning, default);
        var legacy = GameBlueprint.FromPrompt("Create a realistic racing game.") with { SchemaVersion = 2, ArtSet = null };
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO GameBlueprints(TaskId,BlueprintJson,UpdatedUtc) VALUES($task,$json,$now)";
            command.Parameters.AddWithValue("$task", taskId);
            command.Parameters.AddWithValue("$json", System.Text.Json.JsonSerializer.Serialize(legacy));
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var upgraded = await store.GetGameBlueprintAsync(taskId, default);

        Assert.NotNull(upgraded); Assert.Equal(GameBlueprint.CurrentSchemaVersion, upgraded.SchemaVersion);
        Assert.NotNull(upgraded.ArtSet); Assert.Contains(upgraded.ArtSet.Slots, x => x.Role == GameArtAssetRole.Vehicles);
    }

    [Fact] public async Task AgentDoesNotPersistBlueprintForOrdinaryConversation()
    {
        var path = Path.Combine(_dir, "conversation.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("chat", default); using var global = new GlobalCancellation();
        var runner = new AgentRunner(new EmptyModel(), new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true),
            new CountingExecutor(), store, global, new(), NullLogger<AgentRunner>.Instance);

        await runner.RunAsync(conversation, "Explain dependency injection", default);

        var task = (await store.GetRecentTasksAsync(conversation, 1, default)).Single();
        Assert.Null(await store.GetGameBlueprintAsync(task.Id, default));
    }

    [Fact] public async Task ApprovalInboxPersistsBoundedPendingItemsAndResolvesOnce()
    {
        var path = Path.Combine(_dir, "approval-inbox.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var taskId = Guid.NewGuid();
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile project",
            ("project_path", "Games/ForgeCore"), ("operation", "compile"));
        var request = new ApprovalRequest(call, "Compile the reviewed Unity project.", taskId);

        var approvalId = await store.EnqueueApprovalAsync(taskId, request, DateTimeOffset.UtcNow.AddHours(12), default);
        var reopened = new SqliteForgeStore(path); await reopened.InitializeAsync();
        var pending = await reopened.GetPendingApprovalsAsync(10, default);

        var item = Assert.Single(pending); Assert.Equal(approvalId, item.Id); Assert.Equal(taskId, item.TaskId);
        Assert.Equal(call.Kind, item.Call.Kind); Assert.Equal(call.Arguments, item.Call.Arguments); Assert.Equal(ApprovalInboxStatus.Pending, item.Status);
        Assert.True(await reopened.ResolveApprovalAsync(approvalId, ApprovalInboxStatus.Approved, default));
        Assert.False(await reopened.ResolveApprovalAsync(approvalId, ApprovalInboxStatus.Rejected, default));
        Assert.Empty(await reopened.GetPendingApprovalsAsync(10, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            reopened.EnqueueApprovalAsync(taskId, request, DateTimeOffset.UtcNow.AddHours(25), default));
    }

    [Fact] public async Task OrphanedApprovalRecovery_ClaimsRevalidatesExecutesAndCompletesExactlyOnce()
    {
        var path = Path.Combine(_dir, "approval-recovery.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("recover", default); var taskId = Guid.NewGuid();
        await store.CreateTaskAsync(taskId, conversation, "Compile after restart", ForgeTaskState.AwaitingApproval, default);
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile exact project",
            ("project_path", "Games/ForgeCore"), ("operation", "compile"));
        await store.RecordToolCallAsync(taskId, call, "Proposed", null, default);
        var approvalId = await store.EnqueueApprovalAsync(taskId, new(call, "Current policy requires approval.", taskId), DateTimeOffset.UtcNow.AddHours(12), default);

        var reopened = new SqliteForgeStore(path); await reopened.InitializeAsync(); var executor = new CountingExecutor(); using var global = new GlobalCancellation();
        var recovery = new ApprovalRecoveryService(new FixedPolicy(SecurityDecision.ApprovalRequired), executor, reopened, reopened, global,
            NullLogger<ApprovalRecoveryService>.Instance);
        var result = await recovery.ExecuteAsync(approvalId, default);

        Assert.True(result.Claimed, result.Message); Assert.True(result.Success, result.Message); Assert.Equal(1, executor.Count);
        Assert.Equal(ForgeTaskState.Completed, (await reopened.GetTaskAsync(taskId, default))!.State);
        Assert.Contains((await reopened.GetMessagesAsync(conversation, default)), x => x.Role == "assistant" && x.Content.Contains("completed after restart", StringComparison.Ordinal));
        Assert.Empty(await reopened.GetPendingApprovalsAsync(10, default));
        var repeated = await recovery.ExecuteAsync(approvalId, default);
        Assert.False(repeated.Claimed); Assert.Equal(1, executor.Count);
    }

    [Fact] public async Task OrphanedArtAttestation_RemainsLiveOnlyAndNeverExecutes()
    {
        var path = Path.Combine(_dir, "art-recovery.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("art", default); var taskId = Guid.NewGuid();
        await store.CreateTaskAsync(taskId, conversation, "Review exact art", ForgeTaskState.AwaitingApproval, default);
        var call = ToolCall.Create(ToolKind.ReviewGameArtAsset, PermissionKind.Write, "Human art review",
            ("project_path", "Games/ForgeCore"));
        var approvalId = await store.EnqueueApprovalAsync(taskId, new(call, "Human review required.", taskId), DateTimeOffset.UtcNow.AddHours(12), default);
        var executor = new CountingExecutor(); using var global = new GlobalCancellation();
        var recovery = new ApprovalRecoveryService(new FixedPolicy(SecurityDecision.ApprovalRequired), executor, store, store, global,
            NullLogger<ApprovalRecoveryService>.Instance);

        var result = await recovery.ExecuteAsync(approvalId, default);

        Assert.True(result.Claimed, result.Message); Assert.False(result.Success); Assert.Contains("live-dialog-only", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, executor.Count); Assert.Equal(ForgeTaskState.Failed, (await store.GetTaskAsync(taskId, default))!.State);
    }

    [Fact] public async Task OrphanedApprovalRecovery_CurrentPolicyDenialBlocksExecution()
    {
        var path = Path.Combine(_dir, "denied-recovery.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("denied", default); var taskId = Guid.NewGuid();
        await store.CreateTaskAsync(taskId, conversation, "Old approved write", ForgeTaskState.AwaitingApproval, default);
        var call = ToolCall.Create(ToolKind.WriteTextFile, PermissionKind.Write, "Write exact file", ("path", "obsolete.txt"), ("content", "old"));
        var approvalId = await store.EnqueueApprovalAsync(taskId, new(call, "Approval was requested before restart.", taskId), DateTimeOffset.UtcNow.AddHours(12), default);
        var executor = new CountingExecutor(); using var global = new GlobalCancellation();
        var recovery = new ApprovalRecoveryService(new FixedPolicy(SecurityDecision.Denied), executor, store, store, global,
            NullLogger<ApprovalRecoveryService>.Instance);

        var result = await recovery.ExecuteAsync(approvalId, default);

        Assert.True(result.Claimed, result.Message); Assert.False(result.Success);
        Assert.Contains("Current policy denied", result.Message, StringComparison.Ordinal); Assert.Equal(0, executor.Count);
        Assert.Equal(ForgeTaskState.Failed, (await store.GetTaskAsync(taskId, default))!.State);
    }

    [Fact] public async Task PersistentWorkQueue_ResumesDependencyGraphAcrossStoreAndServiceRestart()
    {
        var path = Path.Combine(_dir, "work-queue-restart.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("queue", default); var order = new List<Guid>();
        var first = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect first", ("path", "first"));
        var second = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Inspect second", ("path", "second"));
        var third = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect third", ("path", "third"));
        var final = ToolCall.Create(ToolKind.DotnetTest, PermissionKind.Execute, "Verify after both branches", ("project_path", "Game"), ("configuration", "Release"));
        var definition = new WorkQueueDefinition("Restartable offline acceptance",
        [
            new(first, []), new(second, [first.Id]), new(third, [first.Id]), new(final, [second.Id, third.Id])
        ]);
        using var global = new GlobalCancellation();
        var service = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), new OrderedExecutor(order), store, store, store, global,
            NullLogger<WorkQueueService>.Instance);
        var queueId = await service.CreateAsync(conversation, definition, default);

        var firstRun = await service.RunAsync(queueId, 1, default);

        Assert.Equal(WorkQueueState.Running, firstRun.State); Assert.Equal([first.Id], order);
        var reopened = new SqliteForgeStore(path); await reopened.InitializeAsync();
        var restarted = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), new OrderedExecutor(order), reopened, reopened, reopened, global,
            NullLogger<WorkQueueService>.Instance);
        var resumed = await restarted.ResumeReadyAsync(4, WorkQueueDefinition.MaximumSteps, default);

        Assert.Equal(WorkQueueState.Completed, Assert.Single(resumed).State);
        Assert.Equal([first.Id, second.Id, third.Id, final.Id], order);
        var snapshot = await reopened.GetWorkQueueAsync(queueId, default); Assert.NotNull(snapshot);
        Assert.All(snapshot.Steps, x => Assert.Equal(WorkQueueStepState.Succeeded, x.State));
        Assert.Equal(ForgeTaskState.Completed, (await reopened.GetTaskAsync(queueId, default))!.State);
    }

    [Fact] public async Task PersistentWorkQueue_ApprovalRecoveryReleasesOnlyDependencyReadySuccessors()
    {
        var path = Path.Combine(_dir, "work-queue-approval.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("queue approval", default); var order = new List<Guid>();
        var inspect = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect project", ("path", "Game"));
        var write = ToolCall.Create(ToolKind.WriteTextFile, PermissionKind.Write, "Write reviewed file", ("path", "Game/queued.txt"), ("content", "safe"));
        var verify = ToolCall.Create(ToolKind.DotnetTest, PermissionKind.Execute, "Verify reviewed write", ("project_path", "Game"), ("configuration", "Release"));
        var definition = new WorkQueueDefinition("Approval-gated dependency chain",
            [new(inspect, []), new(write, [inspect.Id]), new(verify, [write.Id])]);
        using var global = new GlobalCancellation(); var policy = new WriteApprovalPolicy(); var executor = new OrderedExecutor(order);
        var service = new WorkQueueService(policy, executor, store, store, store, global, NullLogger<WorkQueueService>.Instance);
        var queueId = await service.CreateAsync(conversation, definition, default);

        var paused = await service.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, default);

        Assert.Equal(WorkQueueState.AwaitingApproval, paused.State); Assert.Equal([inspect.Id], order);
        Assert.Equal(ForgeTaskState.AwaitingApproval, (await store.GetTaskAsync(queueId, default))!.State);
        var approval = Assert.Single(await store.GetPendingApprovalsAsync(10, default)); Assert.Equal(write.Id, approval.Call.Id);
        var recovery = new ApprovalRecoveryService(policy, executor, store, store, global, NullLogger<ApprovalRecoveryService>.Instance, store, service);

        var recovered = await recovery.ExecuteAsync(approval.Id, default);

        Assert.True(recovered.Success, recovered.Message); Assert.Equal([inspect.Id, write.Id, verify.Id], order);
        Assert.Equal(WorkQueueState.Completed, (await store.GetWorkQueueAsync(queueId, default))!.State);
        Assert.Equal(ForgeTaskState.Completed, (await store.GetTaskAsync(queueId, default))!.State);
        Assert.Empty(await store.GetPendingApprovalsAsync(10, default));
    }

    [Fact] public async Task PersistentWorkQueue_ForeignExecutingStepRequiresReviewAndIsNeverReplayed()
    {
        var path = Path.Combine(_dir, "work-queue-interrupted.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("interrupted queue", default);
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile exact project",
            ("project_path", "Games/ForgeCore"), ("operation", "compile"));
        using var global = new GlobalCancellation(); var executor = new CountingExecutor();
        var creator = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), executor, store, store, store, global,
            NullLogger<WorkQueueService>.Instance);
        var queueId = await creator.CreateAsync(conversation, new("Interrupted exact action", [new(call, [])]), default);
        await store.UpdateTaskStateAsync(queueId, ForgeTaskState.Planning, default); await store.UpdateTaskStateAsync(queueId, ForgeTaskState.Running, default);
        Assert.NotNull(await store.TryClaimNextWorkQueueStepAsync(queueId, Guid.NewGuid().ToString(), default));
        var restarted = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), executor, store, store, store, global,
            NullLogger<WorkQueueService>.Instance);

        var result = await restarted.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, default);

        Assert.Equal(WorkQueueState.RequiresReview, result.State); Assert.Equal(0, executor.Count);
        Assert.Equal(WorkQueueStepState.RequiresReview, (await store.GetWorkQueueAsync(queueId, default))!.Steps.Single().State);
        Assert.Equal(ForgeTaskState.Failed, (await store.GetTaskAsync(queueId, default))!.State);
    }

    [Fact] public async Task PersistentWorkQueue_PolicyDenialFailsClosedAndBlocksDependents()
    {
        var path = Path.Combine(_dir, "work-queue-denied.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("denied queue", default); var executor = new CountingExecutor();
        var denied = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "Forbidden command", ("command", "unsafe"));
        var dependent = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Must not run", ("path", "Game"));
        using var global = new GlobalCancellation();
        var service = new WorkQueueService(new KindPolicy(), executor, store, store, store, global, NullLogger<WorkQueueService>.Instance);
        var queueId = await service.CreateAsync(conversation, new("Denied dependency", [new(denied, []), new(dependent, [denied.Id])]), default);

        var result = await service.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, default);

        Assert.Equal(WorkQueueState.Failed, result.State); Assert.Equal(0, executor.Count);
        var steps = (await store.GetWorkQueueAsync(queueId, default))!.Steps;
        Assert.Equal(WorkQueueStepState.Failed, steps[0].State); Assert.Equal(WorkQueueStepState.Blocked, steps[1].State);
        Assert.Equal(ForgeTaskState.Failed, (await store.GetTaskAsync(queueId, default))!.State);
    }

    [Fact] public async Task PersistentWorkQueue_ConcurrentClaimsExecuteAtMostOneExactStep()
    {
        var path = Path.Combine(_dir, "work-queue-claim.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("claim queue", default); var queueId = Guid.NewGuid();
        var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Exact claim", ("path", "Game"));
        await store.CreateWorkQueueAsync(queueId, conversation, new("Atomic claim", [new(call, [])]), default);

        var claims = await Task.WhenAll(
            store.TryClaimNextWorkQueueStepAsync(queueId, Guid.NewGuid().ToString(), default),
            store.TryClaimNextWorkQueueStepAsync(queueId, Guid.NewGuid().ToString(), default));

        Assert.Equal(1, claims.Count(x => x is not null));
        Assert.Equal(WorkQueueStepState.Executing, (await store.GetWorkQueueAsync(queueId, default))!.Steps.Single().State);
    }

    [Fact] public async Task PersistentWorkQueue_CancellationQuarantinesInFlightStepForReview()
    {
        var path = Path.Combine(_dir, "work-queue-cancel.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("cancel queue", default);
        var call = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Cancellable compile",
            ("project_path", "Games/ForgeCore"), ("operation", "compile"));
        using var global = new GlobalCancellation();
        var service = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), new CancellingExecutor(), store, store, store, global,
            NullLogger<WorkQueueService>.Instance);
        var queueId = await service.CreateAsync(conversation, new("Cancellation", [new(call, [])]), default);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, default));

        var snapshot = await store.GetWorkQueueAsync(queueId, default); Assert.NotNull(snapshot);
        Assert.Equal(WorkQueueState.RequiresReview, snapshot.State); Assert.Equal(WorkQueueStepState.RequiresReview, snapshot.Steps.Single().State);
        Assert.Equal(ForgeTaskState.Cancelled, (await store.GetTaskAsync(queueId, default))!.State);
    }

    [Fact] public async Task AgentExplicitUnattendedIntentPersistsMultiActionPlanBeforeExecution()
    {
        var path = Path.Combine(_dir, "agent-work-queue.db"); var store = new SqliteForgeStore(path); await store.InitializeAsync();
        var conversation = await store.CreateConversationAsync("overnight", default); var order = new List<Guid>();
        var inspect = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect", ("path", "Game"));
        var verify = ToolCall.Create(ToolKind.DotnetTest, PermissionKind.Execute, "Verify", ("project_path", "Game"), ("configuration", "Release"));
        var model = new ScriptedModel(new AgentPlan("Do bounded work.", [inspect, verify]), new AgentPlan("Done.", []));
        using var global = new GlobalCancellation(); var executor = new OrderedExecutor(order);
        var queueService = new WorkQueueService(new FixedPolicy(SecurityDecision.Allowed), executor, store, store, store, global,
            NullLogger<WorkQueueService>.Instance);
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor, store, global,
            new(), NullLogger<AgentRunner>.Instance, queueService, store);

        var result = await runner.RunAsync(conversation, "Please work overnight and run unattended on this exact plan.", default);

        Assert.Equal([inspect.Id, verify.Id], order); Assert.All(result.ToolResults, x => Assert.True(x.Success));
        var tasks = await store.GetRecentTasksAsync(conversation, 10, default);
        Assert.Equal(2, tasks.Count); Assert.All(tasks, x => Assert.Equal(ForgeTaskState.Completed, x.State));
        Assert.Contains(tasks, x => x.Request.StartsWith("Queued workflow:", StringComparison.Ordinal));
        Assert.Contains(await store.GetRecentAuditEventsAsync(50, default), x => x.EventType == "PersistentPlanQueue");
    }

    [Fact] public async Task CancellationStopsBeforeExecution()
    {
        var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "read", ("path", "."));
        using var cts = new CancellationTokenSource(); cts.Cancel(); using var global = new GlobalCancellation(); var executor = new CountingExecutor();
        var runner = new AgentRunner(new FixedModel(call), new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor, new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(Guid.NewGuid(), "cancel", cts.Token)); Assert.Equal(0, executor.Count);
    }

    [Fact] public async Task ConversationalPlanWithoutTools_ReturnsModelSummary()
    {
        using var global = new GlobalCancellation(); var executor = new CountingExecutor();
        var runner = new AgentRunner(new EmptyModel(), new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor, new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);
        var result = await runner.RunAsync(Guid.NewGuid(), "hello", default);
        Assert.Equal("Local conversational response", result.Response); Assert.Empty(result.ToolResults); Assert.Equal(0, executor.Count);
    }

    [Fact] public async Task AgentPassesOnlyBoundedRecentConversationHistoryToModel()
    {
        var messages = new[]
        {
            new StoredMessage(1, Guid.Empty, "user", "old", DateTimeOffset.UtcNow),
            new StoredMessage(2, Guid.Empty, "assistant", "middle", DateTimeOffset.UtcNow),
            new StoredMessage(3, Guid.Empty, "user", "recent", DateTimeOffset.UtcNow)
        };
        var store = new MemoryStore { Messages = messages }; var model = new CapturingModel(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), new CountingExecutor(), store, global,
            new AgentOptions { MaxHistoryMessages = 2 }, NullLogger<AgentRunner>.Instance);
        await runner.RunAsync(Guid.NewGuid(), "current", default);
        Assert.Equal("current", model.Request!.UserMessage);
        Assert.Equal(["middle", "recent"], model.Request.History.Select(x => x.Content));
    }

    [Fact] public async Task MultiPass_UsesCompilerFailureToPlanAndVerifyRepair()
    {
        var read = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Inspect source", ("path", "Program.cs"));
        var firstPatch = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "Apply initial change",
            ("path", "Program.cs"), ("old_text", "old"), ("new_text", "broken"));
        var firstBuild = ToolCall.Create(ToolKind.DotnetBuild, PermissionKind.Execute, "Build initial change",
            ("project_path", "App.csproj"), ("configuration", "Release"));
        var repair = ToolCall.Create(ToolKind.ApplyTextPatch, PermissionKind.Write, "Repair compiler error",
            ("path", "Program.cs"), ("old_text", "broken"), ("new_text", "fixed"));
        var verification = ToolCall.Create(ToolKind.DotnetBuild, PermissionKind.Execute, "Verify repair",
            ("project_path", "App.csproj"), ("configuration", "Release"));
        var model = new ScriptedModel(
            new AgentPlan("Inspecting.", [read]),
            new AgentPlan("Applying and building.", [firstPatch, firstBuild]),
            new AgentPlan("Repairing the compiler error.", [repair, verification]),
            new AgentPlan("The requested change now builds successfully.", []));
        var executor = new SequencedExecutor(
            new ToolResult(read.Id, true, "old"),
            new ToolResult(firstPatch.Id, true, "patched"),
            new ToolResult(firstBuild.Id, false, "", "CS1002: ; expected"),
            new ToolResult(repair.Id, true, "repaired"),
            new ToolResult(verification.Id, true, "Build succeeded."));
        using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "change and build the app", default);

        Assert.Equal(5, executor.Count);
        Assert.Equal(4, model.Requests.Count);
        Assert.Contains(model.Requests[2].Observations, x => !x.Success && x.Detail.Contains("CS1002", StringComparison.Ordinal));
        Assert.Contains(model.Requests[3].Observations, x => x.Kind == ToolKind.DotnetBuild && x.Success);
        Assert.Contains("builds successfully", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task DuplicateAction_IsSuppressedAndNotExecutedTwice()
    {
        var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect", ("path", "."));
        var repeated = call with { Id = Guid.NewGuid() };
        var model = new ScriptedModel(
            new AgentPlan("Inspecting.", [call]),
            new AgentPlan("Inspecting again.", [repeated]),
            new AgentPlan("Done without repeating the inspection.", []));
        using var global = new GlobalCancellation();
        var executor = new CountingExecutor();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "inspect once", default);

        Assert.Equal(1, executor.Count);
        Assert.Single(result.ToolResults);
        Assert.Contains("without repeating", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(model.Requests[2].Observations, x => x.Status == "DuplicateSuppressed");
    }

    [Fact] public async Task AggregateActionLimit_AppliesAcrossPlanningPasses()
    {
        var model = new UniqueActionModel();
        using var global = new GlobalCancellation();
        var executor = new CountingExecutor();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new AgentOptions { MaxActions = 2, MaxPlanningIterations = 4 }, NullLogger<AgentRunner>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Guid.NewGuid(), "keep inspecting", default));

        Assert.Contains("aggregate 2 action limit", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, executor.Count);
        Assert.Equal(0, model.Requests[2].RemainingActions);
    }

    [Fact] public async Task CancellationBetweenPasses_PreventsFurtherExecution()
    {
        using var source = new CancellationTokenSource();
        var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect", ("path", "."));
        var model = new CancelOnSecondPassModel(source, call);
        using var global = new GlobalCancellation();
        var executor = new CountingExecutor();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(Guid.NewGuid(), "cancel between passes", source.Token));

        Assert.Equal(1, executor.Count);
        Assert.Equal(2, model.Count);
    }

    [Fact] public async Task PlanningLimit_AfterSuccessfulDotnetVerification_CompletesWithBlockedExtrasReported()
    {
        var build = ToolCall.Create(ToolKind.DotnetBuild, PermissionKind.Execute, "Build",
            ("project_path", "App.csproj"), ("configuration", "Release"));
        var extra = ToolCall.Create(ToolKind.RunCommand, PermissionKind.Execute, "Unnecessary extra check", ("command", "not allowed"));
        var model = new ScriptedModel(new AgentPlan("Building.", [build]), new AgentPlan("Checking again.", [extra]));
        using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new KindPolicy(), new FixedApproval(true), new CountingExecutor(),
            new MemoryStore(), global, new AgentOptions { MaxPlanningIterations = 2 }, NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "build it", default);

        Assert.Contains("verification succeeded", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not allowed", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.ToolResults.Count);
    }

    [Fact] public async Task PlanningLimit_AfterVerificationThenExecutionFailure_DoesNotClaimCompletion()
    {
        var build = ToolCall.Create(ToolKind.DotnetBuild, PermissionKind.Execute, "Build",
            ("project_path", "App.csproj"), ("configuration", "Release"));
        var read = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Inspect another file", ("path", "Other.cs"));
        var model = new ScriptedModel(new AgentPlan("Building.", [build]), new AgentPlan("Inspecting.", [read]));
        var executor = new SequencedExecutor(new ToolResult(build.Id, true, "Build succeeded."),
            new ToolResult(read.Id, false, "", "Could not read required file."));
        using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new AgentOptions { MaxPlanningIterations = 2 }, NullLogger<AgentRunner>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Guid.NewGuid(), "build and inspect", default));

        Assert.Contains("planning-pass limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task MultiPass_CanIndexSearchThenReadCurrentProjectFile()
    {
        var index = ToolCall.Create(ToolKind.IndexProject, PermissionKind.Read, "Index project", ("path", "Game"));
        var search = ToolCall.Create(ToolKind.SearchProject, PermissionKind.Read, "Find player code",
            ("path", "Game"), ("query", "PlayerController"), ("max_results", "5"));
        var read = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Read current player code", ("path", "Game/PlayerController.cs"));
        var model = new ScriptedModel(
            new AgentPlan("Indexing.", [index]), new AgentPlan("Searching.", [search]),
            new AgentPlan("Reading the current file.", [read]), new AgentPlan("I found the player controller.", []));
        var executor = new SequencedExecutor(
            new ToolResult(index.Id, true, "Indexed project Game: 3 files."),
            new ToolResult(search.Id, true, "1. Game/PlayerController.cs symbols: PlayerController"),
            new ToolResult(read.Id, true, "public class PlayerController { }"));
        using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.Allowed), new FixedApproval(true), executor,
            new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "where is player control implemented?", default);

        Assert.Equal(3, executor.Count); Assert.Equal(4, model.Requests.Count);
        Assert.Contains(model.Requests[2].Observations, x => x.Kind == ToolKind.SearchProject && x.Detail.Contains("PlayerController.cs", StringComparison.Ordinal));
        Assert.Contains("found", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task AssetCatalogueResult_IsAvailableForPlanningButOmittedFromConversationTranscript()
    {
        var search = ToolCall.Create(ToolKind.SearchAssetCatalog, PermissionKind.Network, "Find forest art",
            ("query", "forest"), ("media_type", "image"), ("license_filter", "cc0"), ("max_results", "4"));
        var model = new ScriptedModel(
            new AgentPlan("Searching after approval.", [search]),
            new AgentPlan("I found a suitable CC0 forest image and included its source link.", []));
        var executor = new SequencedExecutor(new ToolResult(search.Id, true, "RAW REMOTE RESULT https://catalog.example/forest"));
        var store = new MemoryStore(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.ApprovalRequired), new FixedApproval(true), executor,
            store, global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "find forest art", default);

        Assert.Contains("RAW REMOTE RESULT", model.Requests[1].Observations.Single().Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW REMOTE RESULT", result.Response, StringComparison.Ordinal);
        Assert.Contains("raw result set was omitted", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAW REMOTE RESULT", store.AddedMessages.Last().Content, StringComparison.Ordinal);
    }

    [Fact] public async Task AssetFlow_StagesInspectsAndSeparatelyApprovesHashBoundImport()
    {
        var hash = new string('a', 64);
        var stage = ToolCall.Create(ToolKind.StageWebAsset, PermissionKind.Network, "Stage selected asset",
            ("project_path", "Game"), ("file_name", "forest.png"), ("url", "https://download.example/forest.png"),
            ("source_page", "https://source.example/forest"), ("license", "CC0-1.0"));
        var inspect = ToolCall.Create(ToolKind.InspectStagedAsset, PermissionKind.Read, "Inspect quarantined asset",
            ("project_path", "Game"), ("file_name", "forest.png"));
        var import = ToolCall.Create(ToolKind.ImportStagedAsset, PermissionKind.Write, "Import inspected asset",
            ("project_path", "Game"), ("file_name", "forest.png"), ("destination_path", "Assets/Art/forest.png"),
            ("expected_sha256", hash));
        var model = new ScriptedModel(
            new AgentPlan("Staging requires network approval.", [stage]),
            new AgentPlan("Inspecting the quarantined file without changing the project.", [inspect]),
            new AgentPlan("The inspection passed; import now requires separate write approval.", [import]),
            new AgentPlan("The verified asset was imported with provenance.", []));
        var executor = new SequencedExecutor(
            new ToolResult(stage.Id, true, $"Staged forest.png; SHA-256 {hash}."),
            new ToolResult(inspect.Id, true, $"PNG image; 1024x1024 pixels; SHA-256 {hash}; CC0-1.0."),
            new ToolResult(import.Id, true, "Imported to Game/Assets/Art/forest.png with provenance sidecar."));
        var approvals = new RecordingApproval(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new AssetFlowPolicy(), approvals, executor,
            new MemoryStore(), global, new(), NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "find and import a forest texture", default);

        Assert.Equal(3, executor.Count);
        Assert.Equal([ToolKind.StageWebAsset, ToolKind.ImportStagedAsset], approvals.Requests.Select(x => x.Call.Kind));
        Assert.Contains(model.Requests[2].Observations, x => x.Kind == ToolKind.InspectStagedAsset && x.Detail.Contains(hash, StringComparison.Ordinal));
        Assert.Contains(model.Requests[3].Observations, x => x.Kind == ToolKind.ImportStagedAsset && x.Success);
        Assert.Contains("provenance", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task UnityFlow_InstallsHandshakesBuildsSceneCompilesTestsAndBuilds()
    {
        var install = ToolCall.Create(ToolKind.InstallUnityBridge, PermissionKind.Write, "Install bridge", ("project_path", "Games/ForestRun"));
        var capabilities = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Check capabilities",
            ("project_path", "Games/ForestRun"), ("operation", "capabilities"));
        var scene = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Create scene",
            ("project_path", "Games/ForestRun"), ("operation", "create_scene"), ("scene_path", "Assets/Scenes/Main.unity"), ("scene_template", "basic_3d"));
        var compile = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Compile scripts",
            ("project_path", "Games/ForestRun"), ("operation", "compile"));
        var tests = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Run tests",
            ("project_path", "Games/ForestRun"), ("operation", "run_tests"), ("test_mode", "edit"));
        var build = ToolCall.Create(ToolKind.UnityEditorAction, PermissionKind.Execute, "Build Windows prototype",
            ("project_path", "Games/ForestRun"), ("operation", "build_windows"),
            ("output_path", "Builds/Windows/ForestRun.exe"), ("development_build", "true"));
        var model = new ScriptedModel(
            new AgentPlan("Installing the deterministic project bridge.", [install]),
            new AgentPlan("Checking the exact editor capabilities.", [capabilities]),
            new AgentPlan("Creating the approved basic scene.", [scene]),
            new AgentPlan("Compiling the current project.", [compile]),
            new AgentPlan("Running EditMode tests.", [tests]),
            new AgentPlan("Producing the supervised Windows development build.", [build]),
            new AgentPlan("The Unity prototype compiled, passed tests, and built successfully.", []));
        var executor = new SequencedExecutor(
            new ToolResult(install.Id, true, "Bridge 0.9.1 installed."),
            new ToolResult(capabilities.Id, true, "Editor 6000.0.50f1; bridge ready."),
            new ToolResult(scene.Id, true, "Created Assets/Scenes/Main.unity."),
            new ToolResult(compile.Id, true, "Compilation succeeded."),
            new ToolResult(tests.Id, true, "3 EditMode tests passed."),
            new ToolResult(build.Id, true, "Built Builds/Windows/ForestRun.exe."));
        var approvals = new RecordingApproval(); using var global = new GlobalCancellation();
        var runner = new AgentRunner(model, new FixedPolicy(SecurityDecision.ApprovalRequired), approvals, executor,
            new MemoryStore(), global, new AgentOptions { MaxPlanningIterations = 8, MaxActions = 12 }, NullLogger<AgentRunner>.Instance);

        var result = await runner.RunAsync(Guid.NewGuid(), "create and verify a Unity prototype", default);

        Assert.Equal(6, executor.Count); Assert.Equal(6, approvals.Requests.Count);
        Assert.Contains(model.Requests[4].Observations, x => x.Kind == ToolKind.UnityEditorAction && x.Arguments["operation"] == "compile" && x.Success);
        Assert.Contains(model.Requests[6].Observations, x => x.Kind == ToolKind.UnityEditorAction && x.Arguments["operation"] == "build_windows" && x.Success);
        Assert.Contains("built successfully", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Sqlite_RedactsRawAssetCatalogueResultPayload()
    {
        var path = Path.Combine(_dir, "catalog-redaction.db"); var store = new SqliteForgeStore(path);
        await store.InitializeAsync(); var conversation = await store.CreateConversationAsync("catalog", default);
        var task = Guid.NewGuid(); await store.CreateTaskAsync(task, conversation, "find forest art", ForgeTaskState.Running, default);
        var call = ToolCall.Create(ToolKind.SearchAssetCatalog, PermissionKind.Network, "Find forest art",
            ("query", "forest"), ("media_type", "image"), ("license_filter", "cc0"), ("max_results", "4"));
        await store.RecordToolCallAsync(task, call, "Executed", new ToolResult(call.Id, true, "RAW REMOTE RESULT"), default);

        await using var connection = new SqliteConnection($"Data Source={path}"); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT ResultJson FROM ToolCalls WHERE Id=$id";
        command.Parameters.AddWithValue("$id", call.Id);
        var persisted = (string)(await command.ExecuteScalarAsync())!;
        Assert.DoesNotContain("RAW REMOTE RESULT", persisted, StringComparison.Ordinal);
        Assert.Contains("Ephemeral online catalogue result omitted", persisted, StringComparison.Ordinal);
    }

    private sealed class FixedModel(ToolCall call) : ILocalModel
    {
        private int _count;
        public ModelStatus Status => new("test", true, "ok");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) =>
            Task.FromResult(_count++ == 0 ? new AgentPlan("test", [call]) : new AgentPlan("test", []));
    }
    private sealed class EmptyModel : ILocalModel { public ModelStatus Status => new("test", true, "ok"); public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) => Task.FromResult(new AgentPlan("Local conversational response", [])); }
    private sealed class CapturingModel : ILocalModel
    {
        public ModelRequest? Request { get; private set; }
        public ModelStatus Status => new("test", true, "ok");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token) { Request = request; return Task.FromResult(new AgentPlan("ok", [])); }
    }
    private sealed class ScriptedModel(params AgentPlan[] plans) : ILocalModel
    {
        public List<ModelRequest> Requests { get; } = [];
        public ModelStatus Status => new("test", true, "ok");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(plans[Requests.Count - 1]);
        }
    }
    private sealed class UniqueActionModel : ILocalModel
    {
        public List<ModelRequest> Requests { get; } = [];
        public ModelStatus Status => new("test", true, "ok");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token)
        {
            Requests.Add(request);
            var call = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Inspect", ("path", $"folder-{Requests.Count}"));
            return Task.FromResult(new AgentPlan("continue", [call]));
        }
    }
    private sealed class CancelOnSecondPassModel(CancellationTokenSource source, ToolCall call) : ILocalModel
    {
        public int Count { get; private set; }
        public ModelStatus Status => new("test", true, "ok");
        public Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken token)
        {
            Count++;
            if (Count == 2) source.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentPlan("continue", [call]));
        }
    }
    private sealed class FixedPolicy(SecurityDecision decision) : ISecurityPolicy { public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken token) => ValueTask.FromResult(new PolicyResult(decision, "test")); }
    private sealed class KindPolicy : ISecurityPolicy
    {
        public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken token) => ValueTask.FromResult(
            call.Kind == ToolKind.RunCommand ? new PolicyResult(SecurityDecision.Denied, "Command is not allowed.") : new PolicyResult(SecurityDecision.Allowed, "Allowed."));
    }
    private sealed class WriteApprovalPolicy : ISecurityPolicy
    {
        public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken token) => ValueTask.FromResult(
            call.RequiredPermission == PermissionKind.Write
                ? new PolicyResult(SecurityDecision.ApprovalRequired, "The exact queued write requires approval.")
                : new PolicyResult(SecurityDecision.Allowed, "Offline queued action allowed."));
    }
    private sealed class AssetFlowPolicy : ISecurityPolicy
    {
        public ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken token) => ValueTask.FromResult(
            call.Kind == ToolKind.InspectStagedAsset
                ? new PolicyResult(SecurityDecision.Allowed, "Read-only quarantine inspection.")
                : new PolicyResult(SecurityDecision.ApprovalRequired, "External download or project write requires approval."));
    }
    private sealed class FixedApproval(bool value) : IApprovalService { public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken token) => Task.FromResult(value ? ApprovalDecision.Approved : ApprovalDecision.Rejected); }
    private sealed class DecisionApproval(ApprovalDecision decision) : IApprovalService { public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken token) => Task.FromResult(decision); }
    private sealed class RecordingApproval : IApprovalService
    {
        public List<ApprovalRequest> Requests { get; } = [];
        public Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken token) { Requests.Add(request); return Task.FromResult(ApprovalDecision.Approved); }
    }
    private sealed class CountingExecutor : IToolExecutor { public int Count { get; private set; } public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token) { Count++; return Task.FromResult(new ToolResult(call.Id, true, "ok")); } }
    private sealed class OrderedExecutor(List<Guid> order) : IToolExecutor
    {
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); order.Add(call.Id); return Task.FromResult(new ToolResult(call.Id, true, "ok"));
        }
    }
    private sealed class CancellingExecutor : IToolExecutor
    {
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token) => throw new OperationCanceledException("simulated interruption", token);
    }
    private sealed class RecordingExecutor : IToolExecutor
    {
        public int Count { get; private set; }
        public ToolCall? LastCall { get; private set; }
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token)
        {
            Count++; LastCall = call;
            return Task.FromResult(new ToolResult(call.Id, true, "review recorded"));
        }
    }
    private sealed class SequencedExecutor(params ToolResult[] results) : IToolExecutor
    {
        public int Count { get; private set; }
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var configured = results[Count++];
            return Task.FromResult(configured with { ToolCallId = call.Id });
        }
    }
    private sealed class MemoryStore : IForgeStore
    {
        public IReadOnlyList<StoredMessage> Messages { get; init; } = [];
        public List<(string Role, string Content)> AddedMessages { get; } = [];
        public Task InitializeAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task<Guid> CreateConversationAsync(string title, CancellationToken token) => Task.FromResult(Guid.NewGuid());
        public Task RenameConversationAsync(Guid id, string title, CancellationToken token) => Task.CompletedTask;
        public Task AddMessageAsync(Guid id, string role, string content, CancellationToken token) { AddedMessages.Add((role, content)); return Task.CompletedTask; }
        public Task CreateTaskAsync(Guid task, Guid conversation, string request, ForgeTaskState state, CancellationToken token) => Task.CompletedTask;
        public Task UpdateTaskStateAsync(Guid task, ForgeTaskState state, CancellationToken token) => Task.CompletedTask;
        public Task<TaskSummary?> GetTaskAsync(Guid task, CancellationToken token) => Task.FromResult<TaskSummary?>(null);
        public Task RecordToolCallAsync(Guid task, ToolCall call, string status, ToolResult? result, CancellationToken token) => Task.CompletedTask;
        public Task AuditAsync(AuditEntry entry, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<ConversationSummary>> GetRecentConversationsAsync(int limit, CancellationToken token) => Task.FromResult<IReadOnlyList<ConversationSummary>>([]);
        public Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid id, CancellationToken token) => Task.FromResult(Messages);
        public Task<IReadOnlyList<TaskSummary>> GetRecentTasksAsync(Guid id, int limit, CancellationToken token) => Task.FromResult<IReadOnlyList<TaskSummary>>([]);
        public Task<IReadOnlyList<AuditEvent>> GetRecentAuditEventsAsync(int limit, CancellationToken token) => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    }
}
