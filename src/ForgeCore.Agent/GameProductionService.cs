using ForgeCore.Core;

namespace ForgeCore.Agent;

public sealed class GameProductionOptions
{
    public string ProjectPath { get; set; } = "Games/ForgeCore";
}

/// <summary>Owns the persistent workflow for explicit game requests, independently of model tool selection.</summary>
public sealed class GameProductionService(
    IWorkQueueService workflow, IWorkQueueStore queues, IForgeStore store, GameProductionOptions options) : IGameProductionService
{
    public bool CanHandle(string request) => GameProductionPlan.IsCreationRequest(request);

    public async Task<AgentRunResult> RunAsync(Guid conversationId, string request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(request)) throw new ArgumentException("The message is not an explicit game creation request.");
        await store.AddMessageAsync(conversationId, "user", request, cancellationToken);
        GameProductionPlan plan;
        try { plan = GameProductionPlan.Create(request, options.ProjectPath, Guid.NewGuid()); }
        catch (NotSupportedException ex)
        {
            await store.AddMessageAsync(conversationId, "assistant", ex.Message, cancellationToken);
            return new(ex.Message, [], State: ForgeTaskState.Failed);
        }
        if (store is not IGameBlueprintStore)
            throw new InvalidOperationException("Game production requires persistent blueprint storage.");
        var queueId = await workflow.CreateAsync(conversationId, plan.Workflow, cancellationToken, plan.Blueprint);
        await store.AuditAsync(new(DateTimeOffset.UtcNow, "GameProductionCreated", queueId.ToString(),
            $"Genre {plan.Blueprint.PrimaryGenreId}; {plan.Blueprint.Campaign.Levels.Count} levels; {plan.Workflow.Steps.Count} steps; output {plan.OutputPath}."), cancellationToken);
        await store.AddMessageAsync(conversationId, "assistant",
            $"ForgeCore saved the production workflow for {plan.Blueprint.Campaign.Levels.Count} levels in {plan.ProjectPath}. " +
            "It will check reusable assets, prepare Unity, create maps/gameplay/UI, test every level, capture a preview, build Windows, and test the rendered player.\n" +
            string.Join("\n", plan.OutstandingRequirements), cancellationToken);
        await workflow.RunAsync(queueId, WorkQueueDefinition.MaximumSteps, cancellationToken);
        var snapshot = await queues.GetWorkQueueAsync(queueId, cancellationToken)
            ?? throw new InvalidOperationException("The game production workflow disappeared.");
        var response = GameProductionPlan.Describe(snapshot, plan.Blueprint);
        await store.AddMessageAsync(conversationId, "assistant", response, cancellationToken);
        var state = snapshot.State switch
        {
            WorkQueueState.Completed => ForgeTaskState.Completed,
            WorkQueueState.AwaitingApproval => ForgeTaskState.AwaitingApproval,
            WorkQueueState.Failed or WorkQueueState.RequiresReview => ForgeTaskState.Failed,
            WorkQueueState.Cancelled => ForgeTaskState.Cancelled,
            _ => ForgeTaskState.Running
        };
        return new(response, snapshot.Steps.Where(x => x.Result is not null).Select(x => x.Result!).ToArray(), queueId, state);
    }
}
