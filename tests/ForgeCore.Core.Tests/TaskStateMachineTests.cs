using ForgeCore.Core;
using ForgeCore.LocalAI;

namespace ForgeCore.Core.Tests;
public sealed class TaskStateMachineTests
{
    [Fact] public void ValidLifecycle_IsAccepted()
    {
        Assert.True(TaskStateMachine.CanTransition(ForgeTaskState.Pending, ForgeTaskState.Planning));
        Assert.True(TaskStateMachine.CanTransition(ForgeTaskState.Planning, ForgeTaskState.Running));
        Assert.True(TaskStateMachine.CanTransition(ForgeTaskState.Running, ForgeTaskState.Completed));
    }
    [Fact] public void TerminalState_CannotTransition() =>
        Assert.Throws<InvalidOperationException>(() => TaskStateMachine.EnsureTransition(ForgeTaskState.Completed, ForgeTaskState.Running));
    [Fact] public void GlobalEmergencyStop_IsPermanentForProcess()
    {
        using var global = new GlobalCancellation(); global.EmergencyStop();
        Assert.True(global.IsEmergencyStopped); Assert.True(global.Token.IsCancellationRequested);
    }

    [Fact] public void LlamaSharp_DoesNotDownloadOrClaimAvailabilityForMissingModel()
    {
        var options = new LocalModelOptions { EnableLlamaSharp = true, ModelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gguf") };
        var backend = LocalInferenceBackendFactory.Create(options, (_, _) => throw new InvalidOperationException("Must not construct backend."));
        Assert.False(backend.Status.IsAvailable); Assert.Contains("not found", backend.Status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void LlamaSharp_RejectsRelativeAndNonGgufPaths()
    {
        static ModelStatus Status(string path) => LocalInferenceBackendFactory.Create(
            new LocalModelOptions { EnableLlamaSharp = true, ModelPath = path }, (_, _) => throw new InvalidOperationException()).Status;
        Assert.Contains("absolute", Status("model.gguf").Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".gguf", Status(Path.Combine(Path.GetTempPath(), "model.bin")).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task ProcessMetrics_ReturnSaneLocalValues()
    {
        var snapshot = await new ProcessSystemMetrics().SampleAsync(default);
        Assert.InRange(snapshot.ProcessCpuPercent, 0, 100); Assert.True(snapshot.ProcessWorkingSetBytes > 0);
    }
}
