using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed partial class BuiltInBlenderAssetModel
{
    private static AgentPlan? LearningRoute(ModelRequest request)
    {
        var match = Regex.Match(request.UserMessage,
            @"^\s*plan\s+(?<motion>movement\s+)?improvements\s+for\s+local\s+blender\s+asset\s+(?<hash>[a-f0-9]{64})[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return Regex.IsMatch(request.UserMessage, @"\bplan\s+(?:movement\s+)?improvements\s+for\s+local\s+blender\s+asset\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                ? new("Use: Plan improvements for local Blender asset <exact bundle hash>. Or: Plan movement improvements for local Blender asset <exact bundle hash>. This is a read-only experience plan, not a training run or automatic source rewrite.", []) : null;
        var hash = match.Groups["hash"].Value.ToLowerInvariant();
        var seen = request.Observations.LastOrDefault(o => o.Kind == ToolKind.InspectLocalBlenderAsset && o.Arguments.TryGetValue("bundle_sha256", out var h) && h == hash);
        if (seen is null) return new("Read the exact source and bounded local experience; no editor, provider, training, approval or source changes.",
            [ToolCall.Create(ToolKind.InspectLocalBlenderAsset, PermissionKind.Read, "Read exact source and measured improvement history", ("bundle_sha256", hash))]);
        if (!seen.Success) return new("The experience plan stopped because its exact source/evidence could not be verified. No changes were made.", []);
        try
        {
            using var json = JsonDocument.Parse(seen.Detail);
            var plan = json.RootElement.GetProperty("learningPlan");
            if (plan.GetProperty("schemaVersion").GetInt32() != 1 || plan.GetProperty("policyVersion").GetString() != AssetImprovementModel.Version ||
                plan.GetProperty("bundleSha256").GetString() != hash || plan.GetProperty("neuralWeightsTrained").GetBoolean() ||
                plan.GetProperty("autonomousCodeChanges").GetBoolean() || plan.GetProperty("grantsApproval").GetBoolean()) throw new JsonException();
            var stages = plan.GetProperty("stages"); if (stages.GetArrayLength() != 3) throw new JsonException();
            var counts = new List<string>();
            foreach (var stage in stages.EnumerateArray())
            {
                var name = stage.GetProperty("stage").GetString();
                var attempts = stage.GetProperty("recordedAttempts").GetInt32();
                if (name is not ("source" or "rig" or "motion") || attempts is < 0 or > 256) throw new JsonException();
                counts.Add($"{name}: {attempts} recorded attempt(s)");
            }
            var limited = plan.GetProperty("truncated").GetBoolean() ? " The journal scan is bounded/partial." : "";
            // No journal-supplied command, free-text instruction or approval is ever executed.
            return new("ForgeCore's local experience: " + string.Join("; ", counts) + "." + limited +
                " Failed configurations should be diagnosed before repeating. Comparable rig/motion results retain non-regressing measured baselines; fewer triangles do not prove better art. Exact human source reviews remain separate. This plan changes no model weights, recipe, animation, scene or approval and does not guarantee a better next model. The tool report contains the measured next-step advice.", []);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { return new("The experience report is malformed or incompatible; no learning claim or follow-up changes were made.", []); }
    }
}
