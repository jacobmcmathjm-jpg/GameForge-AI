using ForgeCore.Core;
using Microsoft.Extensions.Logging;

namespace ForgeCore.Agent;

public sealed class WorkQueueService(
    ISecurityPolicy security,
    IToolExecutor tools,
    IForgeStore store,
    IWorkQueueStore queues,
    IApprovalInboxStore approvals,
    IGlobalCancellation globalCancellation,
    ILogger<WorkQueueService> logger,
    IAutomaticApprovalGrant? approvalGrant = null) : IWorkQueueService
{
    private readonly string _executionOwner = Guid.NewGuid().ToString();

    public async Task<Guid> CreateAsync(Guid conversationId, WorkQueueDefinition definition, CancellationToken cancellationToken, GameBlueprint? blueprint = null)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("A work queue requires a conversation id.", nameof(conversationId));
        ArgumentNullException.ThrowIfNull(definition); definition.Validate();
        if (blueprint is not null)
        {
            blueprint.Validate();
            if (store is not IGameBlueprintStore) throw new InvalidOperationException("Blueprint persistence is required before a production workflow can run.");
        }
        var queueId = Guid.NewGuid();
        await store.CreateTaskAsync(queueId, conversationId, "Queued workflow: " + definition.Name, ForgeTaskState.Pending, cancellationToken);
        try
        {
            if (blueprint is not null)
                await ((IGameBlueprintStore)store).UpsertGameBlueprintAsync(queueId, blueprint, cancellationToken);
            await queues.CreateWorkQueueAsync(queueId, conversationId, definition, cancellationToken);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, "WorkQueueCreated", queueId.ToString(),
                $"Offline exact-call queue '{definition.Name}' contains {definition.Steps.Count} dependency-checked steps."), cancellationToken);
            return queueId;
        }
        catch
        {
            await store.UpdateTaskStateAsync(queueId, ForgeTaskState.Planning, CancellationToken.None);
            await store.UpdateTaskStateAsync(queueId, ForgeTaskState.Failed, CancellationToken.None);
            throw;
        }
    }

    public async Task<WorkQueueRunResult> RunAsync(Guid queueId, int maxSteps, CancellationToken cancellationToken)
    {
        if (queueId == Guid.Empty) throw new ArgumentException("A work queue run requires an id.", nameof(queueId));
        if (maxSteps is < 1 or > WorkQueueDefinition.MaximumSteps) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalCancellation.Token);
        var token = linked.Token; var executed = 0;
        try
        {
            var snapshot = await queues.GetWorkQueueAsync(queueId, token) ?? throw new InvalidOperationException("The work queue does not exist.");
            var task = await store.GetTaskAsync(queueId, token) ?? throw new InvalidOperationException("The work queue's owning task does not exist.");
            if (snapshot.State is WorkQueueState.Completed or WorkQueueState.Failed or WorkQueueState.Cancelled or WorkQueueState.RequiresReview)
                return Result(snapshot, executed, "The work queue is already terminal.");
            if (snapshot.State == WorkQueueState.AwaitingApproval)
                return Result(snapshot, executed, "The work queue is waiting for an ordinary exact-call approval.");
            if (snapshot.Steps.Any(x => x.State == WorkQueueStepState.Executing && x.ExecutionOwner != _executionOwner))
            {
                snapshot = await queues.MarkInterruptedWorkQueueForReviewAsync(queueId, _executionOwner, token) ?? snapshot;
                await FailTaskIfPossibleAsync(task, "WorkQueueRequiresReview", "A prior process stopped after a queued action may have begun; automatic replay was refused.");
                return Result(snapshot, executed, "A previously executing step requires manual review; ForgeCore did not replay it.");
            }
            await MoveTaskToRunningAsync(task, token);

            while (executed < maxSteps)
            {
                token.ThrowIfCancellationRequested();
                snapshot = await queues.GetWorkQueueAsync(queueId, token) ?? throw new InvalidOperationException("The work queue disappeared.");
                if (snapshot.State is WorkQueueState.Completed or WorkQueueState.Failed or WorkQueueState.Cancelled or WorkQueueState.RequiresReview)
                    return Result(snapshot, executed, "The work queue reached a terminal state.");
                if (snapshot.State == WorkQueueState.AwaitingApproval)
                    return Result(snapshot, executed, "The work queue is waiting for an ordinary exact-call approval.");

                var step = await queues.TryClaimNextWorkQueueStepAsync(queueId, _executionOwner, token);
                if (step is null)
                    return Result(snapshot, executed, "No dependency-ready step is available; no action was replayed or reordered.");

                var policy = await security.ValidateAsync(step.Call, token);
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "Queued" + policy.Decision, step.Call.Id.ToString(), policy.Reason), token);
                if (policy.Decision == SecurityDecision.Denied)
                {
                    var denied = new ToolResult(step.Call.Id, false, string.Empty, "Current policy denied the queued action: " + policy.Reason);
                    snapshot = await CompleteClaimedAsync(step, denied, token);
                    await FailTaskIfPossibleAsync(await store.GetTaskAsync(queueId, token), "WorkQueuePolicyDenied", denied.Error!);
                    return Result(snapshot, executed, denied.Error!);
                }

                if (policy.Decision == SecurityDecision.ApprovalRequired)
                {
                    if (approvalGrant?.TryConsume(new(step.Call, policy.Reason, queueId)) == true)
                    {
                        await store.AuditAsync(new(DateTimeOffset.UtcNow, "QueuedProjectGrantApplied", step.Call.Id.ToString(),
                            "Current policy passed and the active scoped project grant authorized this exact queued action."), token);
                        policy = new(SecurityDecision.Allowed, policy.Reason);
                    }
                }
                if (policy.Decision == SecurityDecision.ApprovalRequired)
                {
                    await MoveTaskToAwaitingApprovalAsync(await store.GetTaskAsync(queueId, token), token);
                    var approvalId = await approvals.EnqueueApprovalAsync(queueId, new(step.Call, policy.Reason, queueId), DateTimeOffset.UtcNow.AddHours(24), token);
                    var marked = await queues.MarkWorkQueueStepAwaitingApprovalAsync(queueId, step.Id, _executionOwner, token);
                    await store.RecordToolCallAsync(queueId, step.Call, "QueuedAwaitingApproval", null, token);
                    await store.AuditAsync(new(DateTimeOffset.UtcNow, "WorkQueueAwaitingApproval", step.Call.Id.ToString(),
                        $"Approval inbox item {approvalId} protects exact queued step {step.Ordinal + 1}; marked={marked}."), token);
                    snapshot = await queues.GetWorkQueueAsync(queueId, token) ?? snapshot;
                    return Result(snapshot, executed, "The next dependency-ready action requires approval; unrelated queues may continue.");
                }

                var result = await tools.ExecuteAsync(step.Call, token);
                executed++;
                await store.RecordToolCallAsync(queueId, step.Call, "ExecutedFromQueue", result, token);
                snapshot = await CompleteClaimedAsync(step, result, token);
                await store.AuditAsync(new(DateTimeOffset.UtcNow, result.Success ? "WorkQueueStepSucceeded" : "WorkQueueStepFailed",
                    step.Call.Id.ToString(), result.Success ? $"Step {step.Ordinal + 1} completed." : result.Error ?? "Queued step failed."), token);
                if (!result.Success)
                {
                    await FailTaskIfPossibleAsync(await store.GetTaskAsync(queueId, token), "WorkQueueFailed", result.Error ?? "Queued step failed.");
                    return Result(snapshot, executed, result.Error ?? "Queued step failed.");
                }
                if (snapshot.State == WorkQueueState.Completed)
                {
                    await CompleteTaskIfPossibleAsync(await store.GetTaskAsync(queueId, token), token);
                    var message = snapshot.Name.StartsWith(GameProductionPlan.QueuePrefix, StringComparison.Ordinal) &&
                        store is IGameBlueprintStore productionBlueprints && await productionBlueprints.GetGameBlueprintAsync(queueId, token) is { } blueprint
                        ? GameProductionPlan.Describe(snapshot, blueprint)
                        : $"Queued workflow '{snapshot.Name}' completed all {snapshot.Steps.Count} exact dependency-checked steps.";
                    await store.AddMessageAsync(snapshot.ConversationId, "assistant", message, token);
                    await store.AuditAsync(new(DateTimeOffset.UtcNow, "WorkQueueCompleted", queueId.ToString(), message), token);
                    return Result(snapshot, executed, message);
                }
            }

            snapshot = await queues.GetWorkQueueAsync(queueId, token) ?? throw new InvalidOperationException("The work queue disappeared.");
            return Result(snapshot, executed, $"This bounded run executed {executed} step(s); remaining dependency-ready work stays persisted.");
        }
        catch (OperationCanceledException)
        {
            try
            {
                await queues.MarkInterruptedWorkQueueForReviewAsync(queueId, Guid.Empty.ToString(), CancellationToken.None);
                var task = await store.GetTaskAsync(queueId, CancellationToken.None);
                if (task is not null && TaskStateMachine.CanTransition(task.State, ForgeTaskState.Cancelled))
                    await store.UpdateTaskStateAsync(queueId, ForgeTaskState.Cancelled, CancellationToken.None);
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "WorkQueueCancelled", queueId.ToString(),
                    "Cancellation was observed; any in-flight step now requires review and was not replayed."), CancellationToken.None);
            }
            catch (Exception ex) { logger.LogError(ex, "Could not persist work queue cancellation for {QueueId}", queueId); }
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkQueueRunResult>> ResumeReadyAsync(int maxQueues, int maxStepsPerQueue, CancellationToken cancellationToken)
    {
        if (maxQueues is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(maxQueues));
        if (maxStepsPerQueue is < 1 or > WorkQueueDefinition.MaximumSteps) throw new ArgumentOutOfRangeException(nameof(maxStepsPerQueue));
        var recoverable = await queues.GetRecoverableWorkQueuesAsync(maxQueues, cancellationToken);
        var results = new List<WorkQueueRunResult>(recoverable.Count);
        foreach (var queue in recoverable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (queue.State == WorkQueueState.AwaitingApproval) continue;
            results.Add(await RunAsync(queue.Id, maxStepsPerQueue, cancellationToken));
        }
        return results;
    }

    private async Task<WorkQueueSnapshot> CompleteClaimedAsync(WorkQueueStep step, ToolResult result, CancellationToken token) =>
        await queues.CompleteClaimedWorkQueueStepAsync(step.QueueId, step.Id, _executionOwner, result, token)
        ?? throw new InvalidOperationException("The exact claimed work queue step could not be completed once.");

    private async Task MoveTaskToRunningAsync(TaskSummary task, CancellationToken token)
    {
        if (task.State == ForgeTaskState.Running) return;
        if (task.State == ForgeTaskState.Pending)
        {
            TaskStateMachine.EnsureTransition(task.State, ForgeTaskState.Planning);
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Planning, token);
            task = task with { State = ForgeTaskState.Planning };
        }
        TaskStateMachine.EnsureTransition(task.State, ForgeTaskState.Running);
        await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Running, token);
    }

    private async Task MoveTaskToAwaitingApprovalAsync(TaskSummary? task, CancellationToken token)
    {
        if (task is null || task.State == ForgeTaskState.AwaitingApproval) return;
        TaskStateMachine.EnsureTransition(task.State, ForgeTaskState.AwaitingApproval);
        await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.AwaitingApproval, token);
    }

    private async Task CompleteTaskIfPossibleAsync(TaskSummary? task, CancellationToken token)
    {
        if (task is null || task.State == ForgeTaskState.Completed) return;
        TaskStateMachine.EnsureTransition(task.State, ForgeTaskState.Completed);
        await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Completed, token);
    }

    private async Task FailTaskIfPossibleAsync(TaskSummary? task, string eventType, string detail)
    {
        if (task is not null && TaskStateMachine.CanTransition(task.State, ForgeTaskState.Failed))
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Failed, CancellationToken.None);
        await store.AuditAsync(new(DateTimeOffset.UtcNow, eventType, task?.Id.ToString() ?? "missing-task", detail), CancellationToken.None);
    }

    private static WorkQueueRunResult Result(WorkQueueSnapshot queue, int executed, string message) =>
        new(queue.Id, queue.State, executed, message);
}
