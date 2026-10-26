using ForgeCore.Core;
using Microsoft.Extensions.Logging;

namespace ForgeCore.Agent;

public sealed class ApprovalRecoveryService(
    ISecurityPolicy security,
    IToolExecutor tools,
    IForgeStore store,
    IApprovalInboxStore inbox,
    IGlobalCancellation globalCancellation,
    ILogger<ApprovalRecoveryService> logger,
    IWorkQueueStore? workQueues = null,
    IWorkQueueService? workQueueService = null) : IApprovalRecoveryService
{
    private readonly string _executionOwner = Guid.NewGuid().ToString();

    public async Task<ApprovalRecoveryResult> ExecuteAsync(Guid approvalId, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalCancellation.Token);
        var token = linked.Token;
        var item = await inbox.TryClaimApprovalExecutionAsync(approvalId, token);
        if (item is null) return new(false, false, "The approval was already resolved, expired, or claimed.");

        try
        {
            if (item.Call.Kind == ToolKind.ReviewGameArtAsset)
                return await FailAsync(item, "Human visual and rights attestations remain live-dialog-only and cannot be recovered after restart.", token);

            var task = await store.GetTaskAsync(item.TaskId, token);
            if (task is null)
                return await FailAsync(item, "The approval's owning task no longer exists.", token);
            if (task.State != ForgeTaskState.AwaitingApproval)
                return await FailAsync(item, $"The approval's task is {task.State}, not AwaitingApproval.", token);

            var policy = await security.ValidateAsync(item.Call, token);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, "RecoveredApprovalRevalidated", item.Call.Id.ToString(), policy.Reason), token);
            if (policy.Decision == SecurityDecision.Denied)
            {
                if (workQueues is not null && await workQueues.GetWorkQueueStepByCallIdAsync(item.Call.Id, token) is not null)
                    await workQueues.CompleteApprovedWorkQueueStepAsync(item.Call.Id,
                        new(item.Call.Id, false, string.Empty, "Current policy denied the recovered queued action: " + policy.Reason), token);
                await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Failed, token);
                return await FailAsync(item, "Current policy denied the recovered action: " + policy.Reason, token);
            }

            TaskStateMachine.EnsureTransition(task.State, ForgeTaskState.Running);
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Running, token);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, "RecoveredApprovalExecuting", item.Call.Id.ToString(),
                "The exact persisted call was atomically claimed after explicit approval and revalidated against current policy."), token);

            WorkQueueStep? queuedStep = null;
            if (workQueues is not null && await workQueues.GetWorkQueueStepByCallIdAsync(item.Call.Id, token) is not null)
                queuedStep = await workQueues.MarkApprovedWorkQueueStepExecutingAsync(item.Call.Id, _executionOwner, token)
                    ?? throw new InvalidOperationException("The approved queued step could not be claimed for exact execution.");

            var result = await tools.ExecuteAsync(item.Call, token);
            await store.RecordToolCallAsync(task.Id, item.Call, "ExecutedAfterRestart", result, token);
            if (queuedStep is not null)
                return await CompleteQueuedApprovalAsync(item, task, result, token);
            var terminal = result.Success ? ForgeTaskState.Completed : ForgeTaskState.Failed;
            TaskStateMachine.EnsureTransition(ForgeTaskState.Running, terminal);
            await store.UpdateTaskStateAsync(task.Id, terminal, token);
            var outcome = result.Success ? ApprovalInboxStatus.Executed : ApprovalInboxStatus.Failed;
            if (!await inbox.CompleteApprovalExecutionAsync(item.Id, outcome, token))
                throw new InvalidOperationException("The claimed approval could not be finalized exactly once.");

            var detail = result.Success ? result.Output : result.Error ?? "Recovered action failed without an error message.";
            detail = detail.Length <= 12_000 ? detail : detail[..12_000] + " [truncated]";
            var response = result.Success
                ? "Recovered approved action completed after restart. " + detail
                : "Recovered approved action failed safely after restart. " + detail;
            await store.AddMessageAsync(task.ConversationId, "assistant", response, token);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, result.Success ? "RecoveredApprovalExecuted" : "RecoveredApprovalFailed",
                item.Call.Id.ToString(), result.Success ? "Succeeded." : detail), token);
            return new(true, result.Success, response, result);
        }
        catch (OperationCanceledException)
        {
            await TryFinalizeCancelledAsync(item);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recovered approval {ApprovalId} failed", approvalId);
            if (workQueues is not null)
            {
                try
                {
                    if (await workQueues.GetWorkQueueStepByCallIdAsync(item.Call.Id, CancellationToken.None) is { } step)
                        await workQueues.MarkInterruptedWorkQueueForReviewAsync(step.QueueId, Guid.Empty.ToString(), CancellationToken.None);
                }
                catch (Exception queueError) { logger.LogError(queueError, "Could not mark interrupted queued approval for review"); }
            }
            try { await inbox.CompleteApprovalExecutionAsync(item.Id, ApprovalInboxStatus.Failed, CancellationToken.None); } catch { }
            try
            {
                var task = await store.GetTaskAsync(item.TaskId, CancellationToken.None);
                if (task is not null && TaskStateMachine.CanTransition(task.State, ForgeTaskState.Failed))
                    await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Failed, CancellationToken.None);
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "RecoveredApprovalFailed", item.Call.Id.ToString(), ex.Message), CancellationToken.None);
            }
            catch (Exception persistenceError) { logger.LogError(persistenceError, "Could not persist recovered approval failure"); }
            return new(true, false, "Recovered action failed safely: " + ex.Message);
        }
    }

    private async Task<ApprovalRecoveryResult> FailAsync(ApprovalInboxItem item, string message, CancellationToken token)
    {
        await inbox.CompleteApprovalExecutionAsync(item.Id, ApprovalInboxStatus.Failed, token);
        var task = await store.GetTaskAsync(item.TaskId, token);
        if (task is not null && TaskStateMachine.CanTransition(task.State, ForgeTaskState.Failed))
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Failed, token);
        await store.AuditAsync(new(DateTimeOffset.UtcNow, "RecoveredApprovalBlocked", item.Call.Id.ToString(), message), token);
        return new(true, false, message);
    }

    private async Task<ApprovalRecoveryResult> CompleteQueuedApprovalAsync(
        ApprovalInboxItem item, TaskSummary task, ToolResult result, CancellationToken token)
    {
        var snapshot = await workQueues!.CompleteApprovedWorkQueueStepAsync(item.Call.Id, result, token)
            ?? throw new InvalidOperationException("The exact approved queued step could not be completed once.");
        if (!result.Success)
        {
            TaskStateMachine.EnsureTransition(ForgeTaskState.Running, ForgeTaskState.Failed);
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Failed, token);
        }
        else if (snapshot.State == WorkQueueState.Completed)
        {
            TaskStateMachine.EnsureTransition(ForgeTaskState.Running, ForgeTaskState.Completed);
            await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Completed, token);
        }

        var outcome = result.Success ? ApprovalInboxStatus.Executed : ApprovalInboxStatus.Failed;
        if (!await inbox.CompleteApprovalExecutionAsync(item.Id, outcome, token))
            throw new InvalidOperationException("The claimed queued approval could not be finalized exactly once.");
        await store.AuditAsync(new(DateTimeOffset.UtcNow, result.Success ? "RecoveredQueuedStepExecuted" : "RecoveredQueuedStepFailed",
            item.Call.Id.ToString(), result.Success ? "Exact queued step succeeded." : result.Error ?? "Queued step failed."), token);

        WorkQueueRunResult? continuation = null;
        if (result.Success && snapshot.State != WorkQueueState.Completed && workQueueService is not null)
            continuation = await workQueueService.RunAsync(snapshot.Id, WorkQueueDefinition.MaximumSteps, token);
        var message = result.Success
            ? continuation is null
                ? $"Recovered approved queue step completed; workflow state is {snapshot.State}."
                : $"Recovered approved queue step completed; dependency-ready continuation finished in state {continuation.State}: {continuation.Message}"
            : "Recovered approved queue step failed safely: " + (result.Error ?? "No error message was returned.");
        if (snapshot.Name.StartsWith(GameProductionPlan.QueuePrefix, StringComparison.Ordinal) && store is IGameBlueprintStore blueprints &&
            await blueprints.GetGameBlueprintAsync(snapshot.Id, token) is { } blueprint &&
            await workQueues.GetWorkQueueAsync(snapshot.Id, token) is { } current)
            message = GameProductionPlan.Describe(current, blueprint);
        await store.AddMessageAsync(task.ConversationId, "assistant", message, token);
        return new(true, result.Success && (continuation?.State ?? snapshot.State) != WorkQueueState.Failed, message, result);
    }

    private async Task TryFinalizeCancelledAsync(ApprovalInboxItem item)
    {
        try { await inbox.CompleteApprovalExecutionAsync(item.Id, ApprovalInboxStatus.Cancelled, CancellationToken.None); } catch { }
        try
        {
            if (workQueues is not null && await workQueues.GetWorkQueueStepByCallIdAsync(item.Call.Id, CancellationToken.None) is { } step)
                await workQueues.MarkInterruptedWorkQueueForReviewAsync(step.QueueId, Guid.Empty.ToString(), CancellationToken.None);
            var task = await store.GetTaskAsync(item.TaskId, CancellationToken.None);
            if (task is not null && TaskStateMachine.CanTransition(task.State, ForgeTaskState.Cancelled))
                await store.UpdateTaskStateAsync(task.Id, ForgeTaskState.Cancelled, CancellationToken.None);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, "RecoveredApprovalCancelled", item.Call.Id.ToString(), "Cancellation observed."), CancellationToken.None);
        }
        catch (Exception ex) { logger.LogError(ex, "Could not persist recovered approval cancellation"); }
    }
}
