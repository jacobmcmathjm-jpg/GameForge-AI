using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed partial class BuiltInBlenderAssetModel
{
    private Task<AgentPlan>? ExperienceGuidedRoute(ModelRequest request, CancellationToken token)
    {
        var match = Regex.Match(request.UserMessage,
            @"^\s*(?:create|make|generate)\s+(?:an?\s+)?experience-guided\s+(?<label>industrial barrel|wooden crate|concrete barrier|industrial fence|shipping container|rigged mannequin|industrial security character)\s+in\s+blender[.;]\s*name\s+it\s+(?<name>[a-z][a-z0-9_-]{1,47})[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return request.UserMessage.Contains("experience-guided", StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(new AgentPlan("Use: Create an experience-guided wooden crate in Blender. Name it yard-crate. Supported families also include barrel, barrier, fence, container, mannequin and industrial security character. This route reuses compatible non-rejected sources; it does not invent a new recipe or train weights.", [])) : null;
        var label = match.Groups["label"].Value.ToLowerInvariant(); var name = match.Groups["name"].Value.ToLowerInvariant();
        var recipe = label switch
        {
            "industrial barrel" => "industrial_barrel_v3", "wooden crate" => "wooden_crate_v2",
            "concrete barrier" => "concrete_barrier_v2", "industrial fence" => "industrial_fence_v2",
            "shipping container" => "shipping_container_v2", "rigged mannequin" => BlenderAssetSpec.CharacterRecipe,
            _ => BlenderAssetSpec.SecurityCharacterRecipe
        };
        var height = IndustrialPropRecipes.Find(recipe)?.DefaultHeight ?? 1800;
        var canonical = "Create a " + (BlenderAssetSpec.IsCharacter(recipe) ? "" : "refined ") + label + " in Blender. Name it " + name + ".";
        Task<AgentPlan> Stop(string text) => Task.FromResult(new AgentPlan(text, []));
        // Subsequent technical work uses the existing exact import/Unity pipeline and its scoped approvals.
        if (request.Observations.Any(o => o.Kind is ToolKind.CreateBlenderAsset or ToolKind.ImportBlenderAsset))
            return CreatePlanAsync(request with { UserMessage = canonical }, token);
        var search = request.Observations.LastOrDefault(o => o.Kind == ToolKind.SearchLocalBlenderAssets &&
            o.Arguments.TryGetValue("recipe", out var id) && id == recipe && o.Arguments.TryGetValue("query", out var q) && q == label);
        if (search is null) return Task.FromResult(new AgentPlan("Consult the verified local source library before generating another model. Prefer compatible reviewed sources; source/target approval remains separate.",
            [ToolCall.Create(ToolKind.SearchLocalBlenderAssets, PermissionKind.Read, "Find compatible prior models for experience-guided reuse",
                ("query", label), ("recipe", recipe), ("height_min_mm", height.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("height_max_mm", height.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("max_results", "10"))]));
        if (!search.Success) return Stop("Experience-guided preparation stopped at library discovery. No blind regeneration or provider fallback.");
        try
        {
            using var json = JsonDocument.Parse(search.Detail); var root = json.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("library").GetString() != "local_blender_candidates") throw new JsonException();
            var candidates = root.GetProperty("results").EnumerateArray().Select(c => c.Clone()).ToArray();
            if (candidates.Length > 10) throw new JsonException();
            var rejected = 0;
            foreach (var c in candidates.OrderByDescending(c => c.GetProperty("sourceVisualReview").GetString() == "approved" &&
                c.GetProperty("sourceRightsReview").GetString() == "approved").ThenBy(c => c.GetProperty("bundleSha256").GetString(), StringComparer.Ordinal))
            {
                if (c.GetProperty("recipe").GetString() != recipe || c.GetProperty("heightMillimetres").GetInt32() != height ||
                    c.GetProperty("reviewState").GetString() != "unreviewed_local_recipe" || c.GetProperty("placementApproved").GetBoolean()) throw new JsonException();
                var visual = c.GetProperty("sourceVisualReview").GetString(); var rights = c.GetProperty("sourceRightsReview").GetString();
                if (visual == "rejected" || rights == "rejected") { rejected++; continue; }
                if (visual is not ("pending" or "approved") || rights is not ("pending" or "approved")) { rejected++; continue; }
                if (IndustrialPropRecipes.Find(recipe)?.IsBarrel == true && c.GetProperty("radiusMillimetres").GetInt32() != 340) continue;
                static string CheckedHash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit)
                    ? value.ToLowerInvariant() : throw new ArgumentException("Invalid exact source hash.");
                var bundle = CheckedHash(c.GetProperty("bundleSha256").GetString());
                var asset = CheckedHash(c.GetProperty("assetSha256").GetString());
                var inspected = request.Observations.LastOrDefault(o => o.Kind == ToolKind.InspectLocalBlenderAsset && o.Arguments.TryGetValue("bundle_sha256", out var h) && h == bundle);
                if (inspected is null) return Task.FromResult(new AgentPlan("Read the selected exact source, current feedback and measured history before proposing reuse. No quality acceptance is inferred.",
                    [ToolCall.Create(ToolKind.InspectLocalBlenderAsset, PermissionKind.Read, "Recheck candidate feedback and measured experience", ("bundle_sha256", bundle))]));
                if (!inspected.Success) return Stop("The selected experience source could not be reverified; no import or blind regeneration will run.");
                using var packet = JsonDocument.Parse(inspected.Detail); var p = packet.RootElement;
                var learning = p.GetProperty("learningPlan");
                if (p.GetProperty("identity").GetProperty("bundleSha256").GetString() != bundle ||
                    p.GetProperty("identity").GetProperty("assetSha256").GetString() != asset || p.GetProperty("recipe").GetString() != recipe ||
                    learning.GetProperty("policyVersion").GetString() != AssetImprovementModel.Version || learning.GetProperty("bundleSha256").GetString() != bundle ||
                    learning.GetProperty("grantsApproval").GetBoolean() || learning.GetProperty("neuralWeightsTrained").GetBoolean()) throw new JsonException();
                visual = learning.GetProperty("sourceVisualReview").GetString(); rights = learning.GetProperty("sourceRightsReview").GetString();
                if (visual == "rejected" || rights == "rejected") { rejected++; continue; }
                if (visual is not ("pending" or "approved") || rights is not ("pending" or "approved")) throw new JsonException();
                var import = ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write, "Reuse this exact compatible source after current-feedback check; no approval or visual improvement is inferred",
                    ("project_path", projectPath), ("asset_name", name), ("bundle_sha256", bundle), ("expected_asset_sha256", asset));
                _ = BlenderAssetImportSpec.From(import);
                return Task.FromResult(new AgentPlan("Reuse the verified compatible model rather than regenerate it. Human source decisions informed selection; pending source/target reviews stay pending. This is experience-guided reuse, not newly learned modelling skill.", [import]));
            }
            if (rejected != 0) return Stop("Prior compatible sources were rejected or their reviews are invalid. A new bounded recipe/parameter revision and comparison is required; ForgeCore will not silently repeat the rejected model or approve it itself.");
            if (root.GetProperty("truncated").GetBoolean() || root.GetProperty("rejectedEntries").GetInt32() != 0)
                return Stop("Library evidence is partial or invalid; no absence-of-history claim or blind regeneration will be made.");
            return CreatePlanAsync(request with { UserMessage = canonical }, token);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        { return Stop("Experience-guided evidence is malformed or incompatible. No reuse, generation, approval or provider fallback will run."); }
    }
}
