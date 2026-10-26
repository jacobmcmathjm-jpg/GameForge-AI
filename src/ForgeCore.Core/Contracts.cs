namespace ForgeCore.Core;

public interface ILocalModel
{
    ModelStatus Status { get; }
    Task<AgentPlan> CreatePlanAsync(ModelRequest request, CancellationToken cancellationToken);
}

public interface ISecurityPolicy { ValueTask<PolicyResult> ValidateAsync(ToolCall call, CancellationToken cancellationToken); }
public interface IApprovalService { Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken cancellationToken); }
public interface IAutomaticApprovalGrant { bool TryConsume(ApprovalRequest request); }
public interface IGameProductionService
{
    bool CanHandle(string request);
    Task<AgentRunResult> RunAsync(Guid conversationId, string request, CancellationToken cancellationToken);
}
public interface IApprovalInboxStore
{
    Task<Guid> EnqueueApprovalAsync(Guid taskId, ApprovalRequest request, DateTimeOffset expiresUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalInboxItem>> GetPendingApprovalsAsync(int limit, CancellationToken cancellationToken);
    Task<bool> ResolveApprovalAsync(Guid approvalId, ApprovalInboxStatus resolution, CancellationToken cancellationToken);
    Task<ApprovalInboxItem?> TryClaimApprovalExecutionAsync(Guid approvalId, CancellationToken cancellationToken);
    Task<bool> CompleteApprovalExecutionAsync(Guid approvalId, ApprovalInboxStatus outcome, CancellationToken cancellationToken);
}
public interface IApprovalRecoveryService
{
    Task<ApprovalRecoveryResult> ExecuteAsync(Guid approvalId, CancellationToken cancellationToken);
}
public interface IWorkQueueStore
{
    Task CreateWorkQueueAsync(Guid queueId, Guid conversationId, WorkQueueDefinition definition, CancellationToken cancellationToken);
    Task<WorkQueueSnapshot?> GetWorkQueueAsync(Guid queueId, CancellationToken cancellationToken);
    Task<WorkQueueStep?> GetWorkQueueStepByCallIdAsync(Guid callId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkQueueSnapshot>> GetRecoverableWorkQueuesAsync(int limit, CancellationToken cancellationToken);
    Task<WorkQueueStep?> TryClaimNextWorkQueueStepAsync(Guid queueId, string executionOwner, CancellationToken cancellationToken);
    Task<bool> MarkWorkQueueStepAwaitingApprovalAsync(Guid queueId, Guid stepId, string executionOwner, CancellationToken cancellationToken);
    Task<WorkQueueStep?> MarkApprovedWorkQueueStepExecutingAsync(Guid callId, string executionOwner, CancellationToken cancellationToken);
    Task<WorkQueueSnapshot?> CompleteClaimedWorkQueueStepAsync(Guid queueId, Guid stepId, string executionOwner, ToolResult result, CancellationToken cancellationToken);
    Task<WorkQueueSnapshot?> CompleteApprovedWorkQueueStepAsync(Guid callId, ToolResult result, CancellationToken cancellationToken);
    Task<WorkQueueSnapshot?> MarkInterruptedWorkQueueForReviewAsync(Guid queueId, string currentExecutionOwner, CancellationToken cancellationToken);
    Task<bool> CancelWorkQueueAsync(Guid queueId, CancellationToken cancellationToken);
}
public interface IWorkQueueService
{
    Task<Guid> CreateAsync(Guid conversationId, WorkQueueDefinition definition, CancellationToken cancellationToken, GameBlueprint? blueprint = null);
    Task<WorkQueueRunResult> RunAsync(Guid queueId, int maxSteps, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkQueueRunResult>> ResumeReadyAsync(int maxQueues, int maxStepsPerQueue, CancellationToken cancellationToken);
}
public interface IToolExecutor { Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken cancellationToken); }

public interface IProjectIndexStore
{
    Task ReplaceProjectIndexAsync(ProjectIndexSummary summary, IReadOnlyList<ProjectIndexDocument> documents, CancellationToken cancellationToken);
    Task<ProjectIndexSummary?> GetProjectIndexSummaryAsync(string projectPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectIndexDocument>> GetProjectIndexDocumentsAsync(string projectPath, CancellationToken cancellationToken);
}

public interface IGameBlueprintStore
{
    Task UpsertGameBlueprintAsync(Guid taskId, GameBlueprint blueprint, CancellationToken cancellationToken);
    Task<GameBlueprint?> GetGameBlueprintAsync(Guid taskId, CancellationToken cancellationToken);
}

public interface IForgeStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateConversationAsync(string title, CancellationToken cancellationToken);
    Task RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken);
    Task AddMessageAsync(Guid conversationId, string role, string content, CancellationToken cancellationToken);
    Task CreateTaskAsync(Guid taskId, Guid conversationId, string request, ForgeTaskState state, CancellationToken cancellationToken);
    Task UpdateTaskStateAsync(Guid taskId, ForgeTaskState state, CancellationToken cancellationToken);
    Task<TaskSummary?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task RecordToolCallAsync(Guid taskId, ToolCall call, string status, ToolResult? result, CancellationToken cancellationToken);
    Task AuditAsync(AuditEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationSummary>> GetRecentConversationsAsync(int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskSummary>> GetRecentTasksAsync(Guid conversationId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEvent>> GetRecentAuditEventsAsync(int limit, CancellationToken cancellationToken);
}

public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(Guid conversationId, string request, CancellationToken cancellationToken);
}

public interface IGlobalCancellation
{
    CancellationToken Token { get; }
    bool IsEmergencyStopped { get; }
    void EmergencyStop();
}

public interface ISystemMetrics
{
    ValueTask<SystemSnapshot> SampleAsync(CancellationToken cancellationToken);
}

public sealed class GlobalCancellation : IGlobalCancellation, IDisposable
{
    private readonly CancellationTokenSource _source = new();
    public CancellationToken Token => _source.Token;
    public bool IsEmergencyStopped => _source.IsCancellationRequested;
    public void EmergencyStop() => _source.Cancel();
    public void Dispose() => _source.Dispose();
}
