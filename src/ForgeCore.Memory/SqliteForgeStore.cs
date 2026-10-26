using System.Text.Json;
using ForgeCore.Core;
using Microsoft.Data.Sqlite;

namespace ForgeCore.Memory;

public sealed class SqliteForgeStore(string databasePath) : IForgeStore, IProjectIndexStore, IGameBlueprintStore, IApprovalInboxStore, IWorkQueueStore
{
    private readonly string _path = Environment.ExpandEnvironmentVariables(databasePath);
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Conversations(Id TEXT PRIMARY KEY, Title TEXT NOT NULL, CreatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Messages(Id INTEGER PRIMARY KEY AUTOINCREMENT, ConversationId TEXT NOT NULL, Role TEXT NOT NULL, Content TEXT NOT NULL, CreatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Tasks(Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL, Request TEXT NOT NULL, State TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ToolCalls(Id TEXT PRIMARY KEY, TaskId TEXT NOT NULL, Kind TEXT NOT NULL, ArgumentsJson TEXT NOT NULL, Permission TEXT NOT NULL, Status TEXT NOT NULL, ResultJson TEXT NULL, UpdatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS AuditEvents(Id INTEGER PRIMARY KEY AUTOINCREMENT, TimestampUtc TEXT NOT NULL, EventType TEXT NOT NULL, SubjectId TEXT NOT NULL, Detail TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ProjectIndexes(ProjectPath TEXT PRIMARY KEY, FileCount INTEGER NOT NULL, SkippedCount INTEGER NOT NULL, TotalBytes INTEGER NOT NULL, IsTruncated INTEGER NOT NULL, IndexedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ProjectIndexFiles(ProjectPath TEXT NOT NULL, RelativePath TEXT NOT NULL, Extension TEXT NOT NULL, Symbols TEXT NOT NULL, SearchTerms TEXT NOT NULL, Sha256 TEXT NOT NULL, ByteLength INTEGER NOT NULL, LastWriteUtc TEXT NOT NULL, PRIMARY KEY(ProjectPath, RelativePath));
            CREATE TABLE IF NOT EXISTS GameBlueprints(TaskId TEXT PRIMARY KEY, BlueprintJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ApprovalInbox(Id TEXT PRIMARY KEY, TaskId TEXT NOT NULL, CallJson TEXT NOT NULL, Reason TEXT NOT NULL, Status TEXT NOT NULL, CreatedUtc TEXT NOT NULL, ExpiresUtc TEXT NOT NULL, ResolvedUtc TEXT NULL);
            CREATE TABLE IF NOT EXISTS WorkQueues(Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL, Name TEXT NOT NULL, State TEXT NOT NULL, CreatedUtc TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS WorkQueueSteps(Id TEXT PRIMARY KEY, QueueId TEXT NOT NULL, Ordinal INTEGER NOT NULL, CallJson TEXT NOT NULL, DependenciesJson TEXT NOT NULL, State TEXT NOT NULL, ResultJson TEXT NULL, ExecutionOwner TEXT NULL, UpdatedUtc TEXT NOT NULL, UNIQUE(QueueId,Ordinal));
            CREATE INDEX IF NOT EXISTS IX_ProjectIndexFiles_ProjectPath ON ProjectIndexFiles(ProjectPath);
            CREATE INDEX IF NOT EXISTS IX_ApprovalInbox_StatusExpires ON ApprovalInbox(Status,ExpiresUtc);
            CREATE INDEX IF NOT EXISTS IX_WorkQueues_StateUpdated ON WorkQueues(State,UpdatedUtc);
            CREATE INDEX IF NOT EXISTS IX_WorkQueueSteps_QueueState ON WorkQueueSteps(QueueId,State,Ordinal);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Guid> CreateConversationAsync(string title, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO Conversations(Id,Title,CreatedUtc) VALUES($id,$title,$now)", cancellationToken,
            ("$id", id), ("$title", title), ("$now", DateTimeOffset.UtcNow));
        return id;
    }

    public Task RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken) =>
        ExecuteAsync("UPDATE Conversations SET Title=$title WHERE Id=$id", cancellationToken, ("$id", conversationId), ("$title", title));

    public Task AddMessageAsync(Guid conversationId, string role, string content, CancellationToken cancellationToken) =>
        ExecuteAsync("INSERT INTO Messages(ConversationId,Role,Content,CreatedUtc) VALUES($conversation,$role,$content,$now)", cancellationToken,
            ("$conversation", conversationId), ("$role", role), ("$content", content), ("$now", DateTimeOffset.UtcNow));

    public Task CreateTaskAsync(Guid taskId, Guid conversationId, string request, ForgeTaskState state, CancellationToken cancellationToken) =>
        ExecuteAsync("INSERT INTO Tasks(Id,ConversationId,Request,State,UpdatedUtc) VALUES($id,$conversation,$request,$state,$now)", cancellationToken,
            ("$id", taskId), ("$conversation", conversationId), ("$request", request), ("$state", state), ("$now", DateTimeOffset.UtcNow));

    public Task UpdateTaskStateAsync(Guid taskId, ForgeTaskState state, CancellationToken cancellationToken) =>
        ExecuteAsync("UPDATE Tasks SET State=$state,UpdatedUtc=$now WHERE Id=$id", cancellationToken,
            ("$id", taskId), ("$state", state), ("$now", DateTimeOffset.UtcNow));

    public async Task<TaskSummary?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("A task lookup requires an id.", nameof(taskId));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,ConversationId,Request,State,UpdatedUtc FROM Tasks WHERE Id=$id";
        command.Parameters.AddWithValue("$id", taskId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2),
                Enum.Parse<ForgeTaskState>(reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4)))
            : null;
    }

    public Task RecordToolCallAsync(Guid taskId, ToolCall call, string status, ToolResult? result, CancellationToken cancellationToken)
    {
        if (call.Kind == ToolKind.SearchAssetCatalog && result is not null)
            result = result with { Output = result.Success ? "[Ephemeral online catalogue result omitted from local persistence.]" : string.Empty };
        object persistedResult = result is null ? DBNull.Value : JsonSerializer.Serialize(result);
        return ExecuteAsync("""
            INSERT INTO ToolCalls(Id,TaskId,Kind,ArgumentsJson,Permission,Status,ResultJson,UpdatedUtc)
            VALUES($id,$task,$kind,$args,$permission,$status,$result,$now)
            ON CONFLICT(Id) DO UPDATE SET Status=$status,ResultJson=$result,UpdatedUtc=$now
            """, cancellationToken, ("$id", call.Id), ("$task", taskId), ("$kind", call.Kind),
            ("$args", JsonSerializer.Serialize(call.Arguments)), ("$permission", call.RequiredPermission),
            ("$status", status), ("$result", persistedResult), ("$now", DateTimeOffset.UtcNow));
    }

    public Task AuditAsync(AuditEntry entry, CancellationToken cancellationToken) =>
        ExecuteAsync("INSERT INTO AuditEvents(TimestampUtc,EventType,SubjectId,Detail) VALUES($time,$type,$subject,$detail)", cancellationToken,
            ("$time", entry.Timestamp), ("$type", entry.EventType), ("$subject", entry.SubjectId), ("$detail", entry.Detail));

    public async Task<IReadOnlyList<ConversationSummary>> GetRecentConversationsAsync(int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit); var result = new List<ConversationSummary>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT Id,Title,CreatedUtc FROM Conversations ORDER BY CreatedUtc DESC LIMIT $limit"; command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2))));
        return result;
    }

    public async Task<IReadOnlyList<StoredMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = new List<StoredMessage>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT Id,ConversationId,Role,Content,CreatedUtc FROM Messages WHERE ConversationId=$id ORDER BY Id"; command.Parameters.AddWithValue("$id", conversationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetInt64(0), Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4))));
        return result;
    }

    public async Task<IReadOnlyList<AuditEvent>> GetRecentAuditEventsAsync(int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit); var result = new List<AuditEvent>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT Id,TimestampUtc,EventType,SubjectId,Detail FROM AuditEvents ORDER BY Id DESC LIMIT $limit"; command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetInt64(0), DateTimeOffset.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return result;
    }

    public async Task<IReadOnlyList<TaskSummary>> GetRecentTasksAsync(Guid conversationId, int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit); var result = new List<TaskSummary>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT Id,ConversationId,Request,State,UpdatedUtc FROM Tasks WHERE ConversationId=$conversation ORDER BY UpdatedUtc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$conversation", conversationId); command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2), Enum.Parse<ForgeTaskState>(reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4))));
        return result;
    }

    public Task UpsertGameBlueprintAsync(Guid taskId, GameBlueprint blueprint, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("A game blueprint requires a task id.", nameof(taskId));
        ArgumentNullException.ThrowIfNull(blueprint); blueprint.Validate();
        return ExecuteAsync("""
            INSERT INTO GameBlueprints(TaskId,BlueprintJson,UpdatedUtc) VALUES($task,$blueprint,$now)
            ON CONFLICT(TaskId) DO UPDATE SET BlueprintJson=$blueprint,UpdatedUtc=$now
            """, cancellationToken, ("$task", taskId), ("$blueprint", JsonSerializer.Serialize(blueprint)), ("$now", DateTimeOffset.UtcNow));
    }

    public async Task<GameBlueprint?> GetGameBlueprintAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("A game blueprint requires a task id.", nameof(taskId));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT BlueprintJson FROM GameBlueprints WHERE TaskId=$task";
        command.Parameters.AddWithValue("$task", taskId);
        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (json is null) return null;
        var blueprint = (JsonSerializer.Deserialize<GameBlueprint>(json) ?? throw new InvalidDataException("Stored game blueprint is empty.")).UpgradeToCurrent();
        blueprint.Validate(); return blueprint;
    }

    public async Task<Guid> EnqueueApprovalAsync(Guid taskId, ApprovalRequest request, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("An approval inbox item requires a task id.", nameof(taskId));
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 4000) throw new ArgumentException("Approval reason must contain between 1 and 4000 characters.", nameof(request));
        var now = DateTimeOffset.UtcNow;
        if (expiresUtc <= now || expiresUtc > now.AddHours(24)) throw new ArgumentOutOfRangeException(nameof(expiresUtc), "Approval expiry must be within the next 24 hours.");
        var id = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO ApprovalInbox(Id,TaskId,CallJson,Reason,Status,CreatedUtc,ExpiresUtc,ResolvedUtc) VALUES($id,$task,$call,$reason,$status,$created,$expires,NULL)",
            cancellationToken, ("$id", id.ToString()), ("$task", taskId.ToString()), ("$call", JsonSerializer.Serialize(request.Call)), ("$reason", request.Reason),
            ("$status", ApprovalInboxStatus.Pending.ToString()), ("$created", now.ToString("O")), ("$expires", expiresUtc.ToUniversalTime().ToString("O")));
        return id;
    }

    public async Task<IReadOnlyList<ApprovalInboxItem>> GetPendingApprovalsAsync(int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit); var now = DateTimeOffset.UtcNow;
        await ExecuteAsync("UPDATE ApprovalInbox SET Status=$expired,ResolvedUtc=$now WHERE Status=$pending AND ExpiresUtc<=$now",
            cancellationToken, ("$expired", ApprovalInboxStatus.Expired.ToString()), ("$pending", ApprovalInboxStatus.Pending.ToString()), ("$now", now.ToString("O")));
        var result = new List<ApprovalInboxItem>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,TaskId,CallJson,Reason,Status,CreatedUtc,ExpiresUtc,ResolvedUtc FROM ApprovalInbox WHERE Status=$pending ORDER BY CreatedUtc LIMIT $limit";
        command.Parameters.AddWithValue("$pending", ApprovalInboxStatus.Pending.ToString()); command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var call = JsonSerializer.Deserialize<ToolCall>(reader.GetString(2)) ?? throw new InvalidDataException("Stored approval tool call is empty.");
            result.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), call, reader.GetString(3),
                Enum.Parse<ApprovalInboxStatus>(reader.GetString(4)), DateTimeOffset.Parse(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6)),
                reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7))));
        }
        return result;
    }

    public async Task<bool> ResolveApprovalAsync(Guid approvalId, ApprovalInboxStatus resolution, CancellationToken cancellationToken)
    {
        if (approvalId == Guid.Empty) throw new ArgumentException("An approval resolution requires an inbox id.", nameof(approvalId));
        if (resolution is not ApprovalInboxStatus.Approved and not ApprovalInboxStatus.Rejected and not ApprovalInboxStatus.Cancelled)
            throw new ArgumentOutOfRangeException(nameof(resolution), "Approval resolution must be Approved, Rejected, or Cancelled.");
        var now = DateTimeOffset.UtcNow; await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ApprovalInbox SET Status=$status,ResolvedUtc=$now WHERE Id=$id AND Status=$pending AND ExpiresUtc>$now";
        command.Parameters.AddWithValue("$status", resolution.ToString());
        command.Parameters.AddWithValue("$now", now.ToString("O")); command.Parameters.AddWithValue("$id", approvalId.ToString());
        command.Parameters.AddWithValue("$pending", ApprovalInboxStatus.Pending.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<ApprovalInboxItem?> TryClaimApprovalExecutionAsync(Guid approvalId, CancellationToken cancellationToken)
    {
        if (approvalId == Guid.Empty) throw new ArgumentException("An approval execution claim requires an inbox id.", nameof(approvalId));
        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ApprovalInbox SET Status=$executing,ResolvedUtc=$now
            WHERE Id=$id AND Status=$pending AND ExpiresUtc>$now
            RETURNING Id,TaskId,CallJson,Reason,Status,CreatedUtc,ExpiresUtc,ResolvedUtc
            """;
        command.Parameters.AddWithValue("$executing", ApprovalInboxStatus.Executing.ToString());
        command.Parameters.AddWithValue("$pending", ApprovalInboxStatus.Pending.ToString());
        command.Parameters.AddWithValue("$now", now.ToString("O")); command.Parameters.AddWithValue("$id", approvalId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var call = JsonSerializer.Deserialize<ToolCall>(reader.GetString(2)) ?? throw new InvalidDataException("Claimed approval tool call is empty.");
        return new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), call, reader.GetString(3),
            Enum.Parse<ApprovalInboxStatus>(reader.GetString(4)), DateTimeOffset.Parse(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)));
    }

    public async Task<bool> CompleteApprovalExecutionAsync(Guid approvalId, ApprovalInboxStatus outcome, CancellationToken cancellationToken)
    {
        if (approvalId == Guid.Empty) throw new ArgumentException("An approval execution completion requires an inbox id.", nameof(approvalId));
        if (outcome is not ApprovalInboxStatus.Executed and not ApprovalInboxStatus.Failed and not ApprovalInboxStatus.Cancelled)
            throw new ArgumentOutOfRangeException(nameof(outcome), "Recovered execution must finish as Executed, Failed, or Cancelled.");
        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ApprovalInbox SET Status=$outcome,ResolvedUtc=$now WHERE Id=$id AND Status=$executing";
        command.Parameters.AddWithValue("$outcome", outcome.ToString()); command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.Parameters.AddWithValue("$id", approvalId.ToString()); command.Parameters.AddWithValue("$executing", ApprovalInboxStatus.Executing.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task CreateWorkQueueAsync(Guid queueId, Guid conversationId, WorkQueueDefinition definition, CancellationToken cancellationToken)
    {
        if (queueId == Guid.Empty || conversationId == Guid.Empty) throw new ArgumentException("A work queue requires non-empty queue and conversation ids.");
        ArgumentNullException.ThrowIfNull(definition); definition.Validate();
        var now = DateTimeOffset.UtcNow.ToString("O");
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await ExecuteInTransactionAsync(connection, transaction,
                "INSERT INTO WorkQueues(Id,ConversationId,Name,State,CreatedUtc,UpdatedUtc) VALUES($id,$conversation,$name,$state,$now,$now)",
                cancellationToken, ("$id", queueId), ("$conversation", conversationId), ("$name", definition.Name),
                ("$state", WorkQueueState.Pending.ToString()), ("$now", now));
            for (var ordinal = 0; ordinal < definition.Steps.Count; ordinal++)
            {
                var step = definition.Steps[ordinal];
                await ExecuteInTransactionAsync(connection, transaction, """
                    INSERT INTO WorkQueueSteps(Id,QueueId,Ordinal,CallJson,DependenciesJson,State,ResultJson,ExecutionOwner,UpdatedUtc)
                    VALUES($id,$queue,$ordinal,$call,$dependencies,$state,NULL,NULL,$now)
                    """, cancellationToken, ("$id", step.Call.Id), ("$queue", queueId), ("$ordinal", ordinal),
                    ("$call", JsonSerializer.Serialize(step.Call)), ("$dependencies", JsonSerializer.Serialize(step.DependsOn)),
                    ("$state", WorkQueueStepState.Pending.ToString()), ("$now", now));
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task<WorkQueueSnapshot?> GetWorkQueueAsync(Guid queueId, CancellationToken cancellationToken)
    {
        if (queueId == Guid.Empty) throw new ArgumentException("A work queue lookup requires an id.", nameof(queueId));
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,ConversationId,Name,State,CreatedUtc,UpdatedUtc FROM WorkQueues WHERE Id=$id";
        command.Parameters.AddWithValue("$id", queueId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var id = Guid.Parse(reader.GetString(0)); var conversation = Guid.Parse(reader.GetString(1)); var name = reader.GetString(2);
        var state = Enum.Parse<WorkQueueState>(reader.GetString(3)); var created = DateTimeOffset.Parse(reader.GetString(4)); var updated = DateTimeOffset.Parse(reader.GetString(5));
        await reader.DisposeAsync();
        var steps = await ReadWorkQueueStepsAsync(connection, id, cancellationToken);
        return new(id, conversation, name, state, steps, created, updated);
    }

    public async Task<WorkQueueStep?> GetWorkQueueStepByCallIdAsync(Guid callId, CancellationToken cancellationToken)
    {
        if (callId == Guid.Empty) throw new ArgumentException("A work queue step lookup requires a call id.", nameof(callId));
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,QueueId,Ordinal,CallJson,DependenciesJson,State,ResultJson,ExecutionOwner,UpdatedUtc FROM WorkQueueSteps WHERE Id=$id";
        command.Parameters.AddWithValue("$id", callId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWorkQueueStep(reader) : null;
    }

    public async Task<IReadOnlyList<WorkQueueSnapshot>> GetRecoverableWorkQueuesAsync(int limit, CancellationToken cancellationToken)
    {
        ValidateLimit(limit); var ids = new List<Guid>();
        await using (var connection = await OpenAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id FROM WorkQueues WHERE State IN ($pending,$running,$approval) ORDER BY CreatedUtc LIMIT $limit";
            command.Parameters.AddWithValue("$pending", WorkQueueState.Pending.ToString()); command.Parameters.AddWithValue("$running", WorkQueueState.Running.ToString());
            command.Parameters.AddWithValue("$approval", WorkQueueState.AwaitingApproval.ToString()); command.Parameters.AddWithValue("$limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) ids.Add(Guid.Parse(reader.GetString(0)));
        }
        var queues = new List<WorkQueueSnapshot>(ids.Count);
        foreach (var id in ids) if (await GetWorkQueueAsync(id, cancellationToken) is { } queue) queues.Add(queue);
        return queues;
    }

    public async Task<WorkQueueStep?> TryClaimNextWorkQueueStepAsync(Guid queueId, string executionOwner, CancellationToken cancellationToken)
    {
        if (queueId == Guid.Empty) throw new ArgumentException("A work queue claim requires an id.", nameof(queueId));
        if (!Guid.TryParse(executionOwner, out _)) throw new ArgumentException("A work queue execution owner must be a GUID.", nameof(executionOwner));
        var snapshot = await GetWorkQueueAsync(queueId, cancellationToken);
        if (snapshot is null || snapshot.State is WorkQueueState.Completed or WorkQueueState.Failed or WorkQueueState.Cancelled or WorkQueueState.RequiresReview ||
            snapshot.Steps.Any(x => x.State is WorkQueueStepState.Executing or WorkQueueStepState.AwaitingApproval)) return null;
        var states = snapshot.Steps.ToDictionary(x => x.Id, x => x.State);
        var ready = snapshot.Steps.OrderBy(x => x.Ordinal).FirstOrDefault(x => x.State == WorkQueueStepState.Pending && x.DependsOn.All(id => states[id] == WorkQueueStepState.Succeeded));
        if (ready is null) return null;
        var now = DateTimeOffset.UtcNow.ToString("O"); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var claim = connection.CreateCommand(); claim.Transaction = (SqliteTransaction)transaction;
            claim.CommandText = """
                UPDATE WorkQueueSteps SET State=$executing,ExecutionOwner=$owner,UpdatedUtc=$now
                WHERE Id=$id AND QueueId=$queue AND State=$pending
                """;
            claim.Parameters.AddWithValue("$executing", WorkQueueStepState.Executing.ToString()); claim.Parameters.AddWithValue("$owner", executionOwner);
            claim.Parameters.AddWithValue("$now", now); claim.Parameters.AddWithValue("$id", ready.Id); claim.Parameters.AddWithValue("$queue", queueId);
            claim.Parameters.AddWithValue("$pending", WorkQueueStepState.Pending.ToString());
            if (await claim.ExecuteNonQueryAsync(cancellationToken) != 1) { await transaction.RollbackAsync(CancellationToken.None); return null; }
            await ExecuteInTransactionAsync(connection, transaction, "UPDATE WorkQueues SET State=$state,UpdatedUtc=$now WHERE Id=$id",
                cancellationToken, ("$state", WorkQueueState.Running.ToString()), ("$now", now), ("$id", queueId));
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        return (await GetWorkQueueAsync(queueId, cancellationToken))?.Steps.Single(x => x.Id == ready.Id);
    }

    public async Task<bool> MarkWorkQueueStepAwaitingApprovalAsync(Guid queueId, Guid stepId, string executionOwner, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("O"); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "UPDATE WorkQueueSteps SET State=$approval,UpdatedUtc=$now WHERE Id=$step AND QueueId=$queue AND State=$executing AND ExecutionOwner=$owner";
            command.Parameters.AddWithValue("$approval", WorkQueueStepState.AwaitingApproval.ToString()); command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$step", stepId); command.Parameters.AddWithValue("$queue", queueId); command.Parameters.AddWithValue("$executing", WorkQueueStepState.Executing.ToString());
            command.Parameters.AddWithValue("$owner", executionOwner);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) { await transaction.RollbackAsync(CancellationToken.None); return false; }
            await ExecuteInTransactionAsync(connection, transaction, "UPDATE WorkQueues SET State=$state,UpdatedUtc=$now WHERE Id=$id",
                cancellationToken, ("$state", WorkQueueState.AwaitingApproval.ToString()), ("$now", now), ("$id", queueId));
            await transaction.CommitAsync(cancellationToken); return true;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task<WorkQueueStep?> MarkApprovedWorkQueueStepExecutingAsync(Guid callId, string executionOwner, CancellationToken cancellationToken)
    {
        if (callId == Guid.Empty) throw new ArgumentException("An approved work queue step requires a call id.", nameof(callId));
        if (!Guid.TryParse(executionOwner, out _)) throw new ArgumentException("A work queue execution owner must be a GUID.", nameof(executionOwner));
        var step = await GetWorkQueueStepByCallIdAsync(callId, cancellationToken); if (step is null) return null;
        var now = DateTimeOffset.UtcNow.ToString("O"); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                UPDATE WorkQueueSteps SET State=$executing,ExecutionOwner=$owner,UpdatedUtc=$now
                WHERE Id=$id AND QueueId=$queue AND State IN ($approval,$executing)
                """;
            command.Parameters.AddWithValue("$executing", WorkQueueStepState.Executing.ToString()); command.Parameters.AddWithValue("$owner", executionOwner);
            command.Parameters.AddWithValue("$now", now); command.Parameters.AddWithValue("$id", callId); command.Parameters.AddWithValue("$queue", step.QueueId);
            command.Parameters.AddWithValue("$approval", WorkQueueStepState.AwaitingApproval.ToString());
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) { await transaction.RollbackAsync(CancellationToken.None); return null; }
            await ExecuteInTransactionAsync(connection, transaction, "UPDATE WorkQueues SET State=$state,UpdatedUtc=$now WHERE Id=$id",
                cancellationToken, ("$state", WorkQueueState.Running.ToString()), ("$now", now), ("$id", step.QueueId));
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        return await GetWorkQueueStepByCallIdAsync(callId, cancellationToken);
    }

    public Task<WorkQueueSnapshot?> CompleteClaimedWorkQueueStepAsync(Guid queueId, Guid stepId, string executionOwner, ToolResult result, CancellationToken cancellationToken) =>
        CompleteWorkQueueStepCoreAsync(queueId, stepId, executionOwner, false, result, cancellationToken);

    public async Task<WorkQueueSnapshot?> CompleteApprovedWorkQueueStepAsync(Guid callId, ToolResult result, CancellationToken cancellationToken)
    {
        var step = await GetWorkQueueStepByCallIdAsync(callId, cancellationToken);
        return step is null ? null : await CompleteWorkQueueStepCoreAsync(step.QueueId, step.Id, null, true, result, cancellationToken);
    }

    public async Task<WorkQueueSnapshot?> MarkInterruptedWorkQueueForReviewAsync(Guid queueId, string currentExecutionOwner, CancellationToken cancellationToken)
    {
        var snapshot = await GetWorkQueueAsync(queueId, cancellationToken); if (snapshot is null) return null;
        var interrupted = snapshot.Steps.Where(x => x.State == WorkQueueStepState.Executing && x.ExecutionOwner != currentExecutionOwner).ToArray();
        if (interrupted.Length == 0) return snapshot;
        var now = DateTimeOffset.UtcNow.ToString("O"); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var step in interrupted)
                await ExecuteInTransactionAsync(connection, transaction,
                    "UPDATE WorkQueueSteps SET State=$review,UpdatedUtc=$now WHERE Id=$id AND State=$executing",
                    cancellationToken, ("$review", WorkQueueStepState.RequiresReview.ToString()), ("$now", now), ("$id", step.Id), ("$executing", WorkQueueStepState.Executing.ToString()));
            await ExecuteInTransactionAsync(connection, transaction, "UPDATE WorkQueues SET State=$state,UpdatedUtc=$now WHERE Id=$id",
                cancellationToken, ("$state", WorkQueueState.RequiresReview.ToString()), ("$now", now), ("$id", queueId));
            await transaction.CommitAsync(cancellationToken);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        return await GetWorkQueueAsync(queueId, cancellationToken);
    }

    public async Task<bool> CancelWorkQueueAsync(Guid queueId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("O"); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                UPDATE WorkQueues SET State=$cancelled,UpdatedUtc=$now WHERE Id=$id
                AND State NOT IN ($completed,$failed,$cancelled,$review)
                AND NOT EXISTS(SELECT 1 FROM WorkQueueSteps WHERE QueueId=$id AND State=$executing)
                """;
            command.Parameters.AddWithValue("$cancelled", WorkQueueState.Cancelled.ToString()); command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$id", queueId); command.Parameters.AddWithValue("$completed", WorkQueueState.Completed.ToString());
            command.Parameters.AddWithValue("$failed", WorkQueueState.Failed.ToString()); command.Parameters.AddWithValue("$review", WorkQueueState.RequiresReview.ToString());
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) { await transaction.RollbackAsync(CancellationToken.None); return false; }
            await ExecuteInTransactionAsync(connection, transaction,
                "UPDATE WorkQueueSteps SET State=$cancelled,UpdatedUtc=$now WHERE QueueId=$id AND State IN ($pending,$approval)", cancellationToken,
                ("$cancelled", WorkQueueStepState.Cancelled.ToString()), ("$now", now), ("$id", queueId), ("$pending", WorkQueueStepState.Pending.ToString()), ("$approval", WorkQueueStepState.AwaitingApproval.ToString()));
            await transaction.CommitAsync(cancellationToken); return true;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task<WorkQueueSnapshot?> CompleteWorkQueueStepCoreAsync(Guid queueId, Guid stepId, string? executionOwner, bool approved, ToolResult result, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(result); var now = DateTimeOffset.UtcNow.ToString("O");
        var nextStepState = result.Success ? WorkQueueStepState.Succeeded : WorkQueueStepState.Failed;
        await using var connection = await OpenAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        try
        {
            await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = approved
                ? "UPDATE WorkQueueSteps SET State=$next,ResultJson=$result,ExecutionOwner=NULL,UpdatedUtc=$now WHERE Id=$step AND QueueId=$queue AND State IN ($approval,$executing)"
                : "UPDATE WorkQueueSteps SET State=$next,ResultJson=$result,ExecutionOwner=NULL,UpdatedUtc=$now WHERE Id=$step AND QueueId=$queue AND State=$executing AND ExecutionOwner=$owner";
            command.Parameters.AddWithValue("$next", nextStepState.ToString()); command.Parameters.AddWithValue("$result", JsonSerializer.Serialize(result));
            command.Parameters.AddWithValue("$now", now); command.Parameters.AddWithValue("$step", stepId); command.Parameters.AddWithValue("$queue", queueId);
            command.Parameters.AddWithValue("$executing", WorkQueueStepState.Executing.ToString());
            if (approved) command.Parameters.AddWithValue("$approval", WorkQueueStepState.AwaitingApproval.ToString());
            else command.Parameters.AddWithValue("$owner", executionOwner!);
            if (await command.ExecuteNonQueryAsync(token) != 1) { await transaction.RollbackAsync(CancellationToken.None); return null; }

            WorkQueueState queueState;
            if (!result.Success)
            {
                await ExecuteInTransactionAsync(connection, transaction,
                    "UPDATE WorkQueueSteps SET State=$blocked,UpdatedUtc=$now WHERE QueueId=$queue AND State=$pending", token,
                    ("$blocked", WorkQueueStepState.Blocked.ToString()), ("$now", now), ("$queue", queueId), ("$pending", WorkQueueStepState.Pending.ToString()));
                queueState = WorkQueueState.Failed;
            }
            else
            {
                await using var remaining = connection.CreateCommand(); remaining.Transaction = (SqliteTransaction)transaction;
                remaining.CommandText = "SELECT COUNT(*) FROM WorkQueueSteps WHERE QueueId=$queue AND State<>$succeeded";
                remaining.Parameters.AddWithValue("$queue", queueId); remaining.Parameters.AddWithValue("$succeeded", WorkQueueStepState.Succeeded.ToString());
                queueState = Convert.ToInt32(await remaining.ExecuteScalarAsync(token)) == 0 ? WorkQueueState.Completed : WorkQueueState.Running;
            }
            await ExecuteInTransactionAsync(connection, transaction, "UPDATE WorkQueues SET State=$state,UpdatedUtc=$now WHERE Id=$id",
                token, ("$state", queueState.ToString()), ("$now", now), ("$id", queueId));
            await transaction.CommitAsync(token);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        return await GetWorkQueueAsync(queueId, token);
    }

    private static WorkQueueStep ReadWorkQueueStep(SqliteDataReader reader)
    {
        var call = JsonSerializer.Deserialize<ToolCall>(reader.GetString(3)) ?? throw new InvalidDataException("Stored work queue call is empty.");
        var dependencies = JsonSerializer.Deserialize<Guid[]>(reader.GetString(4)) ?? throw new InvalidDataException("Stored work queue dependencies are empty.");
        var result = reader.IsDBNull(6) ? null : JsonSerializer.Deserialize<ToolResult>(reader.GetString(6));
        return new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetInt32(2), call, dependencies,
            Enum.Parse<WorkQueueStepState>(reader.GetString(5)), result, reader.IsDBNull(7) ? null : reader.GetString(7), DateTimeOffset.Parse(reader.GetString(8)));
    }

    private static async Task<IReadOnlyList<WorkQueueStep>> ReadWorkQueueStepsAsync(SqliteConnection connection, Guid queueId, CancellationToken token)
    {
        var result = new List<WorkQueueStep>(); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,QueueId,Ordinal,CallJson,DependenciesJson,State,ResultJson,ExecutionOwner,UpdatedUtc FROM WorkQueueSteps WHERE QueueId=$queue ORDER BY Ordinal";
        command.Parameters.AddWithValue("$queue", queueId); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadWorkQueueStep(reader));
        return result;
    }

    public async Task ReplaceProjectIndexAsync(ProjectIndexSummary summary, IReadOnlyList<ProjectIndexDocument> documents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary); ArgumentNullException.ThrowIfNull(documents);
        if (documents.Any(x => !string.Equals(x.ProjectPath, summary.ProjectPath, StringComparison.Ordinal)))
            throw new ArgumentException("Every indexed document must use the summary project path.", nameof(documents));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteInTransactionAsync(connection, transaction, "DELETE FROM ProjectIndexFiles WHERE ProjectPath=$project", cancellationToken, ("$project", summary.ProjectPath));
        await ExecuteInTransactionAsync(connection, transaction, "DELETE FROM ProjectIndexes WHERE ProjectPath=$project", cancellationToken, ("$project", summary.ProjectPath));
        await ExecuteInTransactionAsync(connection, transaction,
            "INSERT INTO ProjectIndexes(ProjectPath,FileCount,SkippedCount,TotalBytes,IsTruncated,IndexedUtc) VALUES($project,$files,$skipped,$bytes,$truncated,$indexed)",
            cancellationToken, ("$project", summary.ProjectPath), ("$files", summary.FileCount), ("$skipped", summary.SkippedCount),
            ("$bytes", summary.TotalBytes), ("$truncated", summary.IsTruncated ? 1 : 0), ("$indexed", summary.IndexedUtc));
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteInTransactionAsync(connection, transaction, """
                INSERT INTO ProjectIndexFiles(ProjectPath,RelativePath,Extension,Symbols,SearchTerms,Sha256,ByteLength,LastWriteUtc)
                VALUES($project,$path,$extension,$symbols,$terms,$hash,$bytes,$modified)
                """, cancellationToken, ("$project", document.ProjectPath), ("$path", document.RelativePath),
                ("$extension", document.Extension), ("$symbols", document.Symbols), ("$terms", document.SearchTerms),
                ("$hash", document.Sha256), ("$bytes", document.ByteLength), ("$modified", document.LastWriteUtc));
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProjectIndexSummary?> GetProjectIndexSummaryAsync(string projectPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProjectPath,FileCount,SkippedCount,TotalBytes,IsTruncated,IndexedUtc FROM ProjectIndexes WHERE ProjectPath=$project";
        command.Parameters.AddWithValue("$project", projectPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetInt32(4) != 0, DateTimeOffset.Parse(reader.GetString(5)))
            : null;
    }

    public async Task<IReadOnlyList<ProjectIndexDocument>> GetProjectIndexDocumentsAsync(string projectPath, CancellationToken cancellationToken)
    {
        var result = new List<ProjectIndexDocument>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProjectPath,RelativePath,Extension,Symbols,SearchTerms,Sha256,ByteLength,LastWriteUtc FROM ProjectIndexFiles WHERE ProjectPath=$project ORDER BY RelativePath";
        command.Parameters.AddWithValue("$project", projectPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetInt64(6), DateTimeOffset.Parse(reader.GetString(7))));
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync(token); return connection;
    }

    private async Task ExecuteAsync(string sql, CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteInTransactionAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction,
        string sql, CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction; command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1000.");
    }
}
