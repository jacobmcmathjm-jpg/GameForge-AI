using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

public sealed partial class ToolExecutor
{
    private async Task<string> TryRecordUnityLearningAsync(UnityEditorActionSpec spec, string evidence,
        string eventId, string stage, CancellationToken token)
    {
        try { return await _blenderAssets.RecordUnityExperienceAsync(spec, evidence, eventId, stage, token); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or OperationCanceledException or KeyNotFoundException)
        { return "not_recorded_execution_result_unchanged"; }
    }
}
