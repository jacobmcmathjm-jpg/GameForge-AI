using ForgeCore.Core;

namespace ForgeCore.Core.Tests;

public sealed class WorkQueueTests
{
    [Fact] public void DefinitionAcceptsBoundedAcyclicOfflineDependencyGraph()
    {
        var root = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Root", ("path", "Game"));
        var left = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Left", ("path", "Game/a.txt"));
        var right = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Right", ("path", "Game/b.txt"));
        var verify = ToolCall.Create(ToolKind.DotnetTest, PermissionKind.Execute, "Verify", ("project_path", "Game"), ("configuration", "Release"));
        var definition = new WorkQueueDefinition("Diamond",
            [new(root, []), new(left, [root.Id]), new(right, [root.Id]), new(verify, [left.Id, right.Id])]);

        definition.Validate();
    }

    [Fact] public void DefinitionRejectsCyclesAndExternalOrHumanTrustBoundaries()
    {
        var first = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "First", ("path", "Game"));
        var second = ToolCall.Create(ToolKind.ReadTextFile, PermissionKind.Read, "Second", ("path", "Game/a.txt"));
        Assert.Throws<ArgumentException>(() => new WorkQueueDefinition("Cycle",
            [new(first, [second.Id]), new(second, [first.Id])]).Validate());

        var network = ToolCall.Create(ToolKind.Generate3DAsset, PermissionKind.Network, "Paid generation", ("project_path", "Game"));
        Assert.Throws<ArgumentException>(() => new WorkQueueDefinition("Network", [new(network, [])]).Validate());
        var review = ToolCall.Create(ToolKind.ReviewGameArtAsset, PermissionKind.Write, "Human review", ("project_path", "Game"));
        Assert.Throws<ArgumentException>(() => new WorkQueueDefinition("Attestation", [new(review, [])]).Validate());
    }

    [Fact] public void DefinitionRejectsDuplicateIdsAndUnboundedStepCounts()
    {
        var duplicate = ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Duplicate", ("path", "Game"));
        Assert.Throws<ArgumentException>(() => new WorkQueueDefinition("Duplicate ids", [new(duplicate, []), new(duplicate, [])]).Validate());
        var steps = Enumerable.Range(0, WorkQueueDefinition.MaximumSteps + 1)
            .Select(i => new WorkQueueStepDefinition(ToolCall.Create(ToolKind.ListFiles, PermissionKind.Read, "Step " + i, ("path", "Game/" + i)), []))
            .ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkQueueDefinition("Too many", steps).Validate());
    }
}
