using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
using Microsoft.Extensions.Logging;

namespace ForgeCore.Desktop;

public sealed class MainViewModel(IAgentRunner agent, IForgeStore store, ILocalModel model, IGlobalCancellation global, ISystemMetrics metrics,
    IApprovalInboxStore approvalInbox, WpfApprovalService liveApprovals, IUnityGameplayEvidenceCatalog gameplayEvidence,
    IApprovalRecoveryService approvalRecovery, IWorkQueueService workQueueService, IWorkQueueStore workQueueStore,
    TaskAuthorizationSession authorization, ForgeCoreOptions options, ILogger<MainViewModel> logger) : INotifyPropertyChanged
{
    private const int MaxConcurrentTasks = 4;
    private Guid _conversationId; private string _input = string.Empty; private ConversationSummary? _selectedConversation;
    private readonly Dictionary<Guid, CancellationTokenSource> _activeRuns = [];
    private readonly HashSet<Guid> _renamedConversations = [];
    private string _cpuStatus = "CPU: sampling..."; private string _ramStatus = "RAM: sampling..."; private string _gpuStatus = "GPU: unavailable";
    private string _pendingApprovalStatus = "NO PENDING APPROVALS";
    private string _gameplayEvidenceStatus = "GAMEPLAY EVIDENCE: CHECKING";
    private readonly DispatcherTimer _metricsTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public ObservableCollection<ChatMessageItem> Messages { get; } = []; public ObservableCollection<string> Tasks { get; } = []; public ObservableCollection<string> Activity { get; } = [];
    public ObservableCollection<ApprovalInboxItemView> PendingApprovals { get; } = [];
    public ObservableCollection<ConversationSummary> Conversations { get; } = [];
    public string ModelStatus => $"{model.Status.Provider}: {model.Status.Detail}";
    public string UnityAiStatus => !options.EnableUnityMcp ? "UNITY MCP OFF"
        : File.Exists(options.UnityMcpRelayPath) ? "UNITY MCP CONFIG" : "UNITY MCP SETUP";
    public string SeeleStatus => !options.EnableSeeleGeneration ? "3D CLOUD OFF"
        : HasApiKey(options.SeeleApiKeyEnvironmentVariable) ? "SEELE 3D READY" : "SEELE KEY NEEDED";
    public string MeshyStatus => !options.EnableMeshyGeneration ? "MESHY 3D OFF"
        : HasApiKey(options.MeshyApiKeyEnvironmentVariable) ? "MESHY 3D READY" : "MESHY KEY NEEDED";
    public string AgentState => global.IsEmergencyStopped ? "LOCKED" : _activeRuns.Count == 0 ? "READY" : $"WORKING · {_activeRuns.Count}";
    public string AuthorizationStatus
    {
        get
        {
            var snapshot = authorization.Snapshot;
            if (!snapshot.Active || snapshot.ExpiresUtc is null) return "SESSION OFF";
            var minutes = Math.Max(1, (int)Math.Ceiling((snapshot.ExpiresUtc.Value - DateTimeOffset.UtcNow).TotalMinutes));
            return $"TRUSTED PROJECT · {snapshot.RemainingActions} ACTIONS · {minutes} MIN";
        }
    }
    public string AuthorizationButtonText => authorization.Snapshot.Active ? "END TRUSTED MODE" : "TRUST PROJECT";
    public string AuthorizationHint => authorization.Snapshot.Active
        ? "Revoke the current task-scoped authorization immediately"
        : "Allow standard or explicit overnight typed, guarded project actions";
    public string Input { get => _input; set { _input = value; Changed(); } }
    public ConversationSummary? SelectedConversation { get => _selectedConversation; set { _selectedConversation = value; Changed(); } }
    public string CpuStatus { get => _cpuStatus; private set { _cpuStatus = value; Changed(); } }
    public string RamStatus { get => _ramStatus; private set { _ramStatus = value; Changed(); } }
    public string GpuStatus { get => _gpuStatus; private set { _gpuStatus = value; Changed(); } }
    public string PendingApprovalStatus { get => _pendingApprovalStatus; private set { _pendingApprovalStatus = value; Changed(); } }
    public string GameplayEvidenceStatus { get => _gameplayEvidenceStatus; private set { _gameplayEvidenceStatus = value; Changed(); } }
    public ICommand SendCommand => new AsyncCommand(SendAsync, () => !string.IsNullOrWhiteSpace(Input) && _activeRuns.Count < MaxConcurrentTasks && !global.IsEmergencyStopped);
    public ICommand StopCommand => new RelayCommand(CancelActiveTasks, () => _activeRuns.Count > 0);
    public ICommand NewConversationCommand => new AsyncCommand(NewConversationAsync, () => _activeRuns.Count == 0);
    public ICommand LoadConversationCommand => new AsyncCommand(LoadConversationAsync, () => SelectedConversation is not null && _activeRuns.Count == 0);
    public ICommand AuthorizationCommand => new RelayCommand(ToggleAuthorization, () => !global.IsEmergencyStopped);
    public ICommand ApprovePendingCommand => new AsyncParameterCommand<ApprovalInboxItemView>(item => ResolvePendingAsync(item, true), item => item.CanApprove);
    public ICommand RejectPendingCommand => new AsyncParameterCommand<ApprovalInboxItemView>(item => ResolvePendingAsync(item, false));
    public ICommand EmergencyStopCommand => new RelayCommand(() => { authorization.Revoke(); global.EmergencyStop(); CancelActiveTasks(); Activity.Add("EMERGENCY STOP ACTIVATED - authorization revoked; restart required."); Changed(nameof(AgentState)); Changed(nameof(AuthorizationStatus)); Changed(nameof(AuthorizationButtonText)); });
    public event PropertyChangedEventHandler? PropertyChanged;
    public async Task InitializeAsync()
    {
        authorization.Changed += (_, _) => Application.Current.Dispatcher.Invoke(() => { Changed(nameof(AuthorizationStatus)); Changed(nameof(AuthorizationButtonText)); Changed(nameof(AuthorizationHint)); });
        await RefreshHistoryAsync();
        if (Conversations.Count == 0) await NewConversationAsync(); else { SelectedConversation = Conversations[0]; await LoadConversationAsync(); }
        await ResumePersistedWorkAsync();
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync(); _metricsTimer.Start(); await RefreshMetricsAsync();
    }
    private async Task ResumePersistedWorkAsync()
    {
        var runId = Guid.NewGuid(); var cancellation = new CancellationTokenSource(); _activeRuns[runId] = cancellation;
        Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
        try
        {
            var resumed = await workQueueService.ResumeReadyAsync(MaxConcurrentTasks, WorkQueueDefinition.MaximumSteps, cancellation.Token);
            foreach (var result in resumed)
                Activity.Add($"Persistent queue {result.State}: {result.Message}");
        }
        catch (OperationCanceledException) { Activity.Add("Persistent queue recovery cancelled; in-flight work was not replayed."); }
        catch (Exception ex) { logger.LogError(ex, "Persistent work queue recovery failed"); Activity.Add("Persistent queue recovery failed safely: " + ex.Message); }
        finally
        {
            _activeRuns.Remove(runId); cancellation.Dispose(); Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
        }
    }
    private void ToggleAuthorization()
    {
        if (authorization.Snapshot.Active)
        {
            authorization.Revoke(); Activity.Add("Task-scoped authorization revoked."); return;
        }
        var dialog = new SessionAuthorizationDialogWindow(options.UnityProjectPath) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) { Activity.Add("Task-scoped authorization was not enabled."); return; }
        authorization.Activate(options.UnityProjectPath, dialog.Duration, dialog.MaxActions);
        Activity.Add($"Authorized {dialog.MaxActions} guarded project actions for {dialog.Duration.TotalMinutes:F0} minutes in {options.UnityProjectPath}.");
    }
    private async Task NewConversationAsync()
    {
        if (_activeRuns.Count != 0) return;
        _conversationId = await store.CreateConversationAsync("New conversation", CancellationToken.None);
        Messages.Clear(); Tasks.Clear(); Activity.Add("New local conversation created.");
        await RefreshHistoryAsync(); SelectedConversation = Conversations.FirstOrDefault(x => x.Id == _conversationId);
    }
    private async Task LoadConversationAsync()
    {
        if (SelectedConversation is null) return; _conversationId = SelectedConversation.Id; Messages.Clear();
        foreach (var message in await store.GetMessagesAsync(_conversationId, CancellationToken.None)) Messages.Add(new(message.Role, message.Content));
        Tasks.Clear();
        foreach (var task in await store.GetRecentTasksAsync(_conversationId, 25, CancellationToken.None))
        {
            if (task.Request.StartsWith("Queued workflow: " + GameProductionPlan.QueuePrefix, StringComparison.Ordinal) &&
                await workQueueStore.GetWorkQueueAsync(task.Id, CancellationToken.None) is { } production)
            {
                Tasks.Add(DescribeProductionRow(production));
            }
            else Tasks.Add($"{task.State}: {task.Request}");
        }
        Activity.Add($"Loaded conversation: {SelectedConversation.Title}");
    }
    private async Task SendAsync()
    {
        if (_activeRuns.Count >= MaxConcurrentTasks) return;
        var request = Input.Trim();
        if (request.Length == 0) return;
        var runId = Guid.NewGuid();
        var conversationId = _conversationId;
        var cancellation = new CancellationTokenSource();
        _activeRuns.Add(runId, cancellation);
        Input = string.Empty; Messages.Add(new("user", request));
        var taskRow = Tasks.Count; Tasks.Add($"Running: {request}");
        Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
        try
        {
            if (SelectedConversation?.Title == "New conversation" && _renamedConversations.Add(conversationId))
                await store.RenameConversationAsync(conversationId, request[..Math.Min(request.Length, 60)], cancellation.Token);
            var result = await agent.RunAsync(conversationId, request, cancellation.Token);
            Messages.Add(new("assistant", result.Response));
            if (result.WorkQueueId is Guid queueId && await workQueueStore.GetWorkQueueAsync(queueId, cancellation.Token) is { } production)
            {
                Activity.Add(DescribeProductionRow(production)); Tasks[taskRow] = DescribeProductionRow(production);
            }
            else
            {
                Activity.Add($"{result.State}: {result.ToolResults.Count} action result(s)."); Tasks[taskRow] = $"{result.State}: {request}";
            }
        }
        catch (OperationCanceledException) { Activity.Add("Task cancelled."); Tasks[taskRow] = $"Cancelled: {request}"; }
        catch (Exception ex) { logger.LogError(ex, "Request failed"); Activity.Add($"Failed: {ex.Message}"); Tasks[taskRow] = $"Failed: {request}"; }
        finally
        {
            _activeRuns.Remove(runId); cancellation.Dispose();
            Changed(nameof(ModelStatus)); Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
            await RefreshHistoryAsync();
        }
    }
    private void CancelActiveTasks()
    {
        foreach (var cancellation in _activeRuns.Values.ToArray()) cancellation.Cancel();
    }
    private static string DescribeProductionRow(WorkQueueSnapshot production)
    {
        var passed = production.Steps.Count(x => x.State == WorkQueueStepState.Succeeded && x.Result?.Success == true);
        var stage = production.Steps.FirstOrDefault(x => x.State is WorkQueueStepState.Executing or WorkQueueStepState.AwaitingApproval or WorkQueueStepState.Failed or WorkQueueStepState.RequiresReview or WorkQueueStepState.Pending);
        return $"Game production: {passed}/{production.Steps.Count} build steps passed. " +
            (production.State == WorkQueueState.Completed ? "Build validated; full game requirements remain open." : production.State + ": " + stage?.Call.Description);
    }
    private async Task RefreshHistoryAsync()
    {
        var conversations = await store.GetRecentConversationsAsync(25, CancellationToken.None); Conversations.Clear(); foreach (var conversation in conversations) Conversations.Add(conversation);
        var audit = await store.GetRecentAuditEventsAsync(50, CancellationToken.None); Activity.Clear(); foreach (var item in audit.Reverse()) Activity.Add($"{item.Timestamp.LocalDateTime:t}  {item.EventType}: {item.Detail}");
    }
    private async Task RefreshMetricsAsync()
    {
        var snapshot = await metrics.SampleAsync(CancellationToken.None); CpuStatus = $"CPU: {snapshot.ProcessCpuPercent:F1}%";
        RamStatus = $"RAM: {snapshot.ProcessWorkingSetBytes / 1024d / 1024d:F0} MB"; GpuStatus = $"GPU: {snapshot.GpuStatus}";
        await RefreshApprovalsAsync();
        var evidence = await gameplayEvidence.ReadAsync(CancellationToken.None);
        GameplayEvidenceStatus = evidence.TotalRecords == 0 ? "GAMEPLAY EVIDENCE: NONE" :
            evidence.StaleRecords == 0 && evidence.InvalidRecords == 0 ? $"GAMEPLAY EVIDENCE: {evidence.CurrentRecords} CURRENT" :
            $"EVIDENCE: {evidence.CurrentRecords} CURRENT Â· {evidence.StaleRecords} STALE Â· {evidence.InvalidRecords} INVALID";
        Changed(nameof(AuthorizationStatus)); Changed(nameof(AuthorizationButtonText)); Changed(nameof(SeeleStatus)); Changed(nameof(MeshyStatus));
    }
    private async Task RefreshApprovalsAsync()
    {
        var pending = await approvalInbox.GetPendingApprovalsAsync(100, CancellationToken.None);
        PendingApprovalStatus = pending.Count == 0 ? "NO PENDING APPROVALS" : $"{pending.Count} APPROVAL{(pending.Count == 1 ? string.Empty : "S")} PENDING";
        PendingApprovals.Clear();
        foreach (var item in pending)
        {
            var live = liveApprovals.IsLive(item.Id);
            var queuedStep = await workQueueStore.GetWorkQueueStepByCallIdAsync(item.Call.Id, CancellationToken.None);
            if (live && item.Call.Kind == ToolKind.ReviewGameArtAsset)
            {
                PendingApprovals.Add(new(item.Id, item.Call.Description,
                    $"{item.Call.Kind} - decide in the full attestation window (reject is available here)", false));
                continue;
            }
            if (!live && item.Call.Kind != ToolKind.ReviewGameArtAsset)
            {
                PendingApprovals.Add(new(item.Id, item.Call.Description,
                    queuedStep is null
                        ? $"{item.Call.Kind} - prior session - exact call will be revalidated"
                        : $"{item.Call.Kind} - persistent queue step {queuedStep.Ordinal + 1} - exact call will be revalidated", true));
                continue;
            }
            PendingApprovals.Add(new(item.Id, item.Call.Description,
                live ? $"{item.Call.Kind} · expires {item.ExpiresUtc.LocalDateTime:t}" : $"{item.Call.Kind} · prior session (reject only)", live));
        }
        CommandManager.InvalidateRequerySuggested();
    }
    private async Task ResolvePendingAsync(ApprovalInboxItemView item, bool approved)
    {
        if (approved && !liveApprovals.IsLive(item.Id))
        {
            var cancellation = new CancellationTokenSource(); _activeRuns[item.Id] = cancellation;
            Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
            try
            {
                var recovery = await approvalRecovery.ExecuteAsync(item.Id, cancellation.Token);
                Activity.Add(recovery.Message);
                if (SelectedConversation is not null && SelectedConversation.Id == _conversationId) await LoadConversationAsync();
            }
            catch (OperationCanceledException) { Activity.Add($"Recovered approval cancelled: {item.Title}"); }
            finally
            {
                _activeRuns.Remove(item.Id); cancellation.Dispose(); Changed(nameof(AgentState)); CommandManager.InvalidateRequerySuggested();
                await RefreshApprovalsAsync();
            }
            return;
        }

        var resolved = liveApprovals.IsLive(item.Id)
            ? await liveApprovals.ResolveLiveAsync(item.Id, approved, CancellationToken.None)
            : await approvalInbox.ResolveApprovalAsync(item.Id, ApprovalInboxStatus.Rejected, CancellationToken.None);
        Activity.Add(resolved ? $"Approval {(approved ? "accepted" : "rejected")}: {item.Title}"
            : $"Approval was already resolved or expired: {item.Title}");
        await RefreshApprovalsAsync();
    }
    private static bool HasApiKey(string environmentVariable)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(value) && value.Length is >= 16 and <= 512 && !value.Any(char.IsWhiteSpace);
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed record ChatMessageItem(string Role, string Content)
{
    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    public string DisplayRole => IsUser ? "YOU" : "FORGECORE";
}

public sealed record ApprovalInboxItemView(Guid Id, string Title, string Detail, bool CanApprove);

public sealed class WpfApprovalService(TaskAuthorizationSession authorization, IApprovalInboxStore inbox) : IApprovalService
{
    private sealed class LiveApproval
    {
        public TaskCompletionSource<ApprovalDecision> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SemaphoreSlim DecisionGate { get; } = new(1, 1);
    }

    private readonly ConcurrentDictionary<Guid, LiveApproval> _live = new();

    public bool IsLive(Guid approvalId) => _live.ContainsKey(approvalId);

    public async Task<bool> ResolveLiveAsync(Guid approvalId, bool approved, CancellationToken cancellationToken)
    {
        if (!_live.TryGetValue(approvalId, out var live)) return false;
        await live.DecisionGate.WaitAsync(cancellationToken);
        try
        {
            if (live.Completion.Task.IsCompleted) return false;
            var decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
            if (!await inbox.ResolveApprovalAsync(approvalId, approved ? ApprovalInboxStatus.Approved : ApprovalInboxStatus.Rejected, cancellationToken)) return false;
            return live.Completion.TrySetResult(decision);
        }
        finally { live.DecisionGate.Release(); }
    }

    public async Task<ApprovalDecision> RequestApprovalAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization.TryConsume(request)) return ApprovalDecision.Approved;
        Guid? inboxId = request.TaskId is Guid taskId && taskId != Guid.Empty
            ? await inbox.EnqueueApprovalAsync(taskId, request, DateTimeOffset.UtcNow.AddHours(24), cancellationToken)
            : null;
        var dispatcher = Application.Current.Dispatcher;
        var live = new LiveApproval();
        if (inboxId is Guid registeredId && !_live.TryAdd(registeredId, live))
            throw new InvalidOperationException("The approval request is already active.");
        PermissionDialogWindow? dialog = null;
        await dispatcher.InvokeAsync(() =>
        {
            dialog = new PermissionDialogWindow(request) { Owner = Application.Current.MainWindow };
            dialog.DecisionMade += approved => ResolveFromDialog(inboxId, live, approved);
            dialog.Closed += (_, _) => CancelFromDialog(inboxId, live);
            dialog.Show(); dialog.Activate();
        }, DispatcherPriority.Normal, cancellationToken);
        using var registration = cancellationToken.Register(() =>
        {
            live.Completion.TrySetCanceled(cancellationToken);
            _ = dispatcher.BeginInvoke(() => dialog?.Close());
        });
        try
        {
            return await live.Completion.Task;
        }
        finally
        {
            if (inboxId is Guid id)
            {
                _live.TryRemove(id, out _);
                if (!live.Completion.Task.IsCompletedSuccessfully)
                    try { await inbox.ResolveApprovalAsync(id, ApprovalInboxStatus.Cancelled, CancellationToken.None); } catch { }
            }
            await dispatcher.InvokeAsync(() => { if (dialog?.IsVisible == true) dialog.Close(); });
        }
    }

    private void ResolveFromDialog(Guid? inboxId, LiveApproval live, bool approved)
    {
        if (inboxId is Guid id) _ = ResolveLiveAsync(id, approved, CancellationToken.None);
        else live.Completion.TrySetResult(approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected);
    }

    private void CancelFromDialog(Guid? inboxId, LiveApproval live)
    {
        if (live.Completion.Task.IsCompleted) return;
        if (inboxId is Guid id) _ = CancelLiveAsync(id, live);
        else live.Completion.TrySetResult(ApprovalDecision.Cancelled);
    }

    private async Task CancelLiveAsync(Guid approvalId, LiveApproval live)
    {
        await live.DecisionGate.WaitAsync(CancellationToken.None);
        try
        {
            if (live.Completion.Task.IsCompleted) return;
            if (!await inbox.ResolveApprovalAsync(approvalId, ApprovalInboxStatus.Cancelled, CancellationToken.None)) return;
            live.Completion.TrySetResult(ApprovalDecision.Cancelled);
        }
        finally { live.DecisionGate.Release(); }
    }
}

internal sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
}
internal sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) => await execute();
}
internal sealed class AsyncParameterCommand<T>(Func<T, Task> execute, Func<T, bool>? canExecute = null) : ICommand where T : class
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => parameter is T item && (canExecute?.Invoke(item) ?? true);
    public async void Execute(object? parameter) { if (parameter is T item) await execute(item); }
}
