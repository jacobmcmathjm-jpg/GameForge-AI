using System.Security.Cryptography;
using System.Text;
using ForgeCore.Core;
using Microsoft.Extensions.Logging;

namespace ForgeCore.Agent;

public sealed class AgentOptions
{
    public int MaxActions { get; set; } = 12;
    public int MaxRetries { get; set; } = 2;
    public int MaxHistoryMessages { get; set; } = 12;
    public int MaxPlanningIterations { get; set; } = 12;
    public int MaxRenderedToolResultCharacters { get; set; } = 12_000;
}

public sealed class AgentRunner(
    ILocalModel model, ISecurityPolicy security, IApprovalService approvals,
    IToolExecutor tools, IForgeStore store, IGlobalCancellation globalCancellation,
    AgentOptions options, ILogger<AgentRunner> logger,
    IWorkQueueService? workQueueService = null,
    IWorkQueueStore? workQueues = null,
    IGameProductionService? gameProduction = null) : IAgentRunner
{
    public async Task<AgentRunResult> RunAsync(Guid conversationId, string request, CancellationToken cancellationToken)
    {
        ValidateOptions();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, globalCancellation.Token);
        var token = linked.Token;
        if (gameProduction?.CanHandle(request) == true)
            return await gameProduction.RunAsync(conversationId, request, token);
        var taskId = Guid.NewGuid();
        var state = ForgeTaskState.Pending;
        var history = (await store.GetMessagesAsync(conversationId, token)).TakeLast(options.MaxHistoryMessages)
            .Select(message => new ConversationTurn(message.Role, message.Content)).ToArray();
        await store.AddMessageAsync(conversationId, "user", request, token);
        await store.CreateTaskAsync(taskId, conversationId, request, state, token);

        try
        {
            await TransitionAsync(ForgeTaskState.Planning, token);
            state = ForgeTaskState.Planning;
            if (store is IGameBlueprintStore blueprintStore && GameGenreRegistry.IsGameCreationPrompt(request))
            {
                var blueprint = GameBlueprint.FromPrompt(request);
                await blueprintStore.UpsertGameBlueprintAsync(taskId, blueprint, token);
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "GameBlueprintCreated", taskId.ToString(),
                    $"Schema {blueprint.SchemaVersion}; genre {blueprint.PrimaryGenreId}; {blueprint.SystemPackIds.Count} system packs; {blueprint.Campaign.Levels.Count} levels; art {blueprint.ArtSet!.StyleFamily}/{blueprint.ArtSet.Slots.Count} slots; certification {blueprint.Certification}."), token);
            }
            var results = new List<ToolResult>();
            var observations = new List<ToolObservation>();
            var proposedFingerprints = new HashSet<string>(StringComparer.Ordinal);
            var proposedActionCount = 0;
            string? finalResponse = null;
            var finished = false;
            var persistentQueueRequested = RequestsPersistentQueue(request);

            for (var iteration = 1; iteration <= options.MaxPlanningIterations; iteration++)
            {
                token.ThrowIfCancellationRequested();
                var remainingActions = options.MaxActions - proposedActionCount;
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "PlanningPass", taskId.ToString(),
                    $"Planning iteration {iteration} of {options.MaxPlanningIterations}; {remainingActions} actions remain."), token);

                var plan = await model.CreatePlanAsync(
                    new ModelRequest(request, history, observations.ToArray(), iteration, remainingActions), token);
                if (plan.Actions.Count > remainingActions)
                    throw new InvalidOperationException($"Task exceeds the aggregate {options.MaxActions} action limit.");

                proposedActionCount += plan.Actions.Count;
                finalResponse = plan.Response;
                if (plan.Actions.Count == 0)
                {
                    finished = true;
                    break;
                }

                var novelActions = new List<ToolCall>(plan.Actions.Count);
                foreach (var call in plan.Actions)
                {
                    token.ThrowIfCancellationRequested();
                    await RecordAsync(call, "Proposed", null, "Proposed", call.Description, token);
                    if (proposedFingerprints.Add(CreateFingerprint(call)))
                    {
                        novelActions.Add(call);
                        continue;
                    }

                    var duplicate = new ToolResult(call.Id, false, string.Empty,
                        "Duplicate action suppressed; the same tool kind and arguments were already proposed in this task.");
                    observations.Add(ToObservation(call, "DuplicateSuppressed", duplicate));
                    await RecordAsync(call, "DuplicateSuppressed", duplicate, "DuplicateSuppressed", duplicate.Error!, token);
                }

                if (novelActions.Count == 0)
                {
                    continue;
                }

                if (persistentQueueRequested && novelActions.Count > 1 && workQueueService is not null && workQueues is not null &&
                    TryCreateSequentialQueueDefinition(request, iteration, novelActions, out var queueDefinition))
                {
                    var queueId = await workQueueService.CreateAsync(conversationId, queueDefinition!, token);
                    var queuedRun = await workQueueService.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, token);
                    var queuedSnapshot = await workQueues.GetWorkQueueAsync(queueId, token)
                        ?? throw new InvalidOperationException("The newly created persistent work queue disappeared.");
                    foreach (var step in queuedSnapshot.Steps.Where(x => x.Result is not null))
                    {
                        results.Add(step.Result!);
                        observations.Add(ToObservation(step.Call, step.Result!.Success ? "QueuedSucceeded" : "QueuedFailed", step.Result));
                    }
                    await store.AuditAsync(new(DateTimeOffset.UtcNow, "PersistentPlanQueue", queueId.ToString(),
                        $"Planning pass {iteration} persisted {novelActions.Count} sequential exact calls; state {queuedRun.State}."), token);
                    if (queuedRun.State != WorkQueueState.Completed)
                    {
                        finalResponse = queuedRun.State == WorkQueueState.AwaitingApproval
                            ? $"ForgeCore persisted {novelActions.Count} exact offline actions as a dependency-ordered queue. It completed the safe prefix and is waiting for the next scoped approval; later steps remain blocked until that exact dependency succeeds."
                            : $"ForgeCore persisted the exact offline plan, then stopped in state {queuedRun.State}: {queuedRun.Message}";
                        finished = true;
                        break;
                    }
                    finalResponse = $"The persistent offline queue completed {novelActions.Count} exact dependency-ordered actions. ForgeCore is checking the results before planning more work.";
                    continue;
                }

                foreach (var proposedCall in novelActions)
                {
                    token.ThrowIfCancellationRequested();
                    var call = proposedCall;
                    var proposalFingerprint = CreateFingerprint(proposedCall);
                    var policy = await security.ValidateAsync(call, token);
                    await store.AuditAsync(new(DateTimeOffset.UtcNow, policy.Decision.ToString(), call.Id.ToString(), policy.Reason), token);
                    if (policy.Decision == SecurityDecision.Denied)
                    {
                        var denied = new ToolResult(call.Id, false, string.Empty, policy.Reason);
                        await store.RecordToolCallAsync(taskId, call, "Rejected", denied, token);
                        results.Add(denied);
                        observations.Add(ToObservation(call, "PolicyDenied", denied));
                        continue;
                    }

                    if (policy.Decision == SecurityDecision.ApprovalRequired)
                    {
                        if (state != ForgeTaskState.AwaitingApproval)
                        {
                            await TransitionAsync(ForgeTaskState.AwaitingApproval, token);
                            state = ForgeTaskState.AwaitingApproval;
                        }
                        var decision = await approvals.RequestApprovalAsync(new(call, policy.Reason, taskId), token);
                        await store.AuditAsync(new(DateTimeOffset.UtcNow, decision.ToString(), call.Id.ToString(), "User decision."), token);
                        if (call.Kind == ToolKind.ReviewGameArtAsset)
                        {
                            if (decision == ApprovalDecision.Cancelled)
                            {
                                var cancelled = new ToolResult(call.Id, false, string.Empty, "User closed or cancelled the review without recording a decision.");
                                await store.RecordToolCallAsync(taskId, call, "Cancelled", cancelled, token);
                                results.Add(cancelled);
                                observations.Add(ToObservation(call, "UserCancelled", cancelled));
                                continue;
                            }
                            call = GameArtReviewSpec.BindUserDecision(call, decision);
                        }
                        else if (decision != ApprovalDecision.Approved)
                        {
                            var error = decision == ApprovalDecision.Rejected
                                ? "User rejected the action."
                                : "User closed or cancelled the approval without authorizing the action.";
                            var rejected = new ToolResult(call.Id, false, string.Empty, error);
                            await store.RecordToolCallAsync(taskId, call, decision == ApprovalDecision.Rejected ? "Rejected" : "Cancelled", rejected, token);
                            results.Add(rejected);
                            observations.Add(ToObservation(call, decision == ApprovalDecision.Rejected ? "UserRejected" : "UserCancelled", rejected));
                            continue;
                        }
                    }

                    if (state != ForgeTaskState.Running)
                    {
                        await TransitionAsync(ForgeTaskState.Running, token);
                        state = ForgeTaskState.Running;
                    }

                    ToolResult result = new(call.Id, false, string.Empty, "Not executed.");
                    var retryCount = call.Kind == ToolKind.StageWebAsset ? options.MaxRetries : 0;
                    for (var attempt = 0; attempt <= retryCount; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        result = await tools.ExecuteAsync(call, token);
                        if (result.Success) break;
                    }
                    results.Add(result);
                    observations.Add(ToObservation(call, result.Success ? "Succeeded" : "Failed", result));
                    await RecordAsync(call, "Executed", result, "Executed", result.Success ? "Succeeded" : result.Error ?? "Failed", token);
                    if (!result.Success)
                        proposedFingerprints.Remove(proposalFingerprint);
                }
            }

            if (!finished)
            {
                var verificationIndex = observations.FindLastIndex(x => x.Success && IsDedicatedVerification(x));
                var onlyBlockedAfterVerification = verificationIndex >= 0 && observations.Skip(verificationIndex + 1)
                    .All(x => !x.Success && x.Status is "PolicyDenied" or "UserRejected" or "DuplicateSuppressed");
                if (!onlyBlockedAfterVerification)
                    throw new InvalidOperationException($"Task did not finish within the {options.MaxPlanningIterations} planning-pass limit.");
                finalResponse = $"A dedicated build or test verification succeeded. ForgeCore then reached the {options.MaxPlanningIterations}-pass planning limit while considering additional work. No denied or duplicate action was executed.";
                await store.AuditAsync(new(DateTimeOffset.UtcNow, "CompletedAtPlanningLimit", taskId.ToString(),
                    "A dedicated build or test succeeded; later denied or duplicate proposals were not executed."), token);
            }

            if (state != ForgeTaskState.Running)
            {
                await TransitionAsync(ForgeTaskState.Running, token);
                state = ForgeTaskState.Running;
            }
            await TransitionAsync(ForgeTaskState.Completed, token);
            var ephemeralResultIds = observations.Where(x => x.Kind == ToolKind.SearchAssetCatalog).Select(x => x.ToolCallId).ToHashSet();
            var toolOutput = RenderToolResults(results, ephemeralResultIds, options.MaxRenderedToolResultCharacters);
            var response = results.Count == 0 ? finalResponse! : $"{finalResponse}{Environment.NewLine}{Environment.NewLine}{toolOutput}";
            await store.AddMessageAsync(conversationId, "assistant", response, token);
            return new AgentRunResult(response, results);
        }
        catch (OperationCanceledException)
        {
            if (TaskStateMachine.CanTransition(state, ForgeTaskState.Cancelled))
                await store.UpdateTaskStateAsync(taskId, ForgeTaskState.Cancelled, CancellationToken.None);
            await TryAuditTerminalAsync("TaskCancelled", "Task cancellation was observed.");
            logger.LogWarning("Task {TaskId} cancelled", taskId);
            throw;
        }
        catch (Exception ex)
        {
            if (TaskStateMachine.CanTransition(state, ForgeTaskState.Failed))
                await store.UpdateTaskStateAsync(taskId, ForgeTaskState.Failed, CancellationToken.None);
            await TryAuditTerminalAsync("TaskFailed", ex.Message);
            logger.LogError(ex, "Task {TaskId} failed", taskId);
            throw;
        }

        async Task TransitionAsync(ForgeTaskState next, CancellationToken ct)
        {
            TaskStateMachine.EnsureTransition(state, next);
            await store.UpdateTaskStateAsync(taskId, next, ct);
        }

        async Task RecordAsync(ToolCall call, string status, ToolResult? result, string eventType, string detail, CancellationToken ct)
        {
            await store.RecordToolCallAsync(taskId, call, status, result, ct);
            await store.AuditAsync(new(DateTimeOffset.UtcNow, eventType, call.Id.ToString(), detail), ct);
        }

        async Task TryAuditTerminalAsync(string eventType, string detail)
        {
            try { await store.AuditAsync(new(DateTimeOffset.UtcNow, eventType, taskId.ToString(), detail), CancellationToken.None); }
            catch (Exception auditError) { logger.LogError(auditError, "Could not persist terminal audit event {EventType} for task {TaskId}", eventType, taskId); }
        }
    }

    private static bool IsDedicatedVerification(ToolObservation observation) =>
        observation.Kind is ToolKind.DotnetBuild or ToolKind.DotnetTest or ToolKind.UnityPlayerSmokeTest or ToolKind.UnityPlayerProfileTest ||
        observation.Kind == ToolKind.UnityEditorAction && observation.Arguments.TryGetValue("operation", out var operation) &&
        operation is "compile" or "run_tests" or "verify_gameplay" or "capture_scene_preview" or "build_windows";

    private void ValidateOptions()
    {
        if (options.MaxActions < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxActions));
        if (options.MaxRetries < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxRetries));
        if (options.MaxHistoryMessages < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxHistoryMessages));
        if (options.MaxPlanningIterations < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxPlanningIterations));
        if (options.MaxRenderedToolResultCharacters < 0) throw new ArgumentOutOfRangeException(nameof(options.MaxRenderedToolResultCharacters));
    }

    private static ToolObservation ToObservation(ToolCall call, string status, ToolResult result) =>
        new(call.Id, call.Kind, call.Description, call.Arguments, status, result.Success,
            result.Success ? result.Output : result.Error ?? "Failed without an error message.");

    private static string CreateFingerprint(ToolCall call)
    {
        var value = new StringBuilder().Append((int)call.Kind).Append('|');
        foreach (var argument in call.Arguments.OrderBy(x => x.Key, StringComparer.Ordinal))
            value.Append(argument.Key.Length).Append(':').Append(argument.Key).Append('=')
                .Append(argument.Value.Length).Append(':').Append(argument.Value).Append('|');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())));
    }

    private static bool RequestsPersistentQueue(string request)
    {
        var normalized = request.ToLowerInvariant();
        return normalized.Contains("queue this", StringComparison.Ordinal) ||
               normalized.Contains("overnight queue", StringComparison.Ordinal) ||
               normalized.Contains("work overnight", StringComparison.Ordinal) ||
               normalized.Contains("run unattended", StringComparison.Ordinal) ||
               normalized.Contains("three-day run", StringComparison.Ordinal) ||
               normalized.Contains("3-day run", StringComparison.Ordinal) ||
               normalized.Contains("while i am away", StringComparison.Ordinal);
    }

    private static bool TryCreateSequentialQueueDefinition(string request, int iteration, IReadOnlyList<ToolCall> actions,
        out WorkQueueDefinition? definition)
    {
        var steps = new List<WorkQueueStepDefinition>(actions.Count);
        for (var index = 0; index < actions.Count; index++)
            steps.Add(new(actions[index], index == 0 ? [] : [actions[index - 1].Id]));
        var boundedRequest = request.Length <= 72 ? request : request[..72];
        definition = new($"Agent pass {iteration}: {boundedRequest}", steps);
        try { definition.Validate(); return true; }
        catch (ArgumentException) { definition = null; return false; }
    }

    private static string RenderToolResults(IReadOnlyList<ToolResult> results, IReadOnlySet<Guid> ephemeralResultIds, int maxCharacters)
    {
        if (maxCharacters == 0) return "Tool results were recorded locally (display disabled by configuration).";
        var text = string.Join(Environment.NewLine, results.Select(x => x.Success
            ? ephemeralResultIds.Contains(x.ToolCallId)
                ? "Online catalogue metadata was used during this task; the raw result set was omitted from the persisted conversation."
                : x.Output
            : $"Blocked or failed: {x.Error}"));
        return text.Length <= maxCharacters ? text : $"[Earlier tool output omitted]{Environment.NewLine}{text[^maxCharacters..]}";
    }
}
