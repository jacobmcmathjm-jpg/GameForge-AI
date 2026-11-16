using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ForgeCore.Core;

namespace ForgeCore.LocalAI;

public sealed partial class BuiltInBlenderAssetModel
{
    private static readonly JsonSerializerOptions QualityJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    private AgentPlan? SimpleQualityRoute(ModelRequest request)
    {
        var intent = SimpleAssetRequest.Parse(request.UserMessage);
        if (intent is null)
        {
            // Preserve explicit advanced Blender/game/planning routes. Do not forward an unsupported
            // short modelling instruction to inference and imply it can satisfy realistic art.
            if (!request.UserMessage.Contains("blender", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(request.UserMessage, @"^\s*(?:please\s+)?(?:create|make|generate)\b.*\b(?:model|prop|crate|barrel|guard|mannequin)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(request.UserMessage, @"\b(?:game|level|mission)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return new("That short asset request is not supported by the fixed offline recipes. Try: Make a detailed wooden crate. Also supported: barrel, concrete barrier, fence, shipping container, mannequin or guard. No unsupported quality promise, script or provider fallback will run.", []);
            return null;
        }
        AgentPlan Stop(string detail) => new("Quality workflow unfinished: " + detail + " No finished/refined-product acceptance is claimed.", []);
        AgentPlan Step(ToolCall call, string detail) => request.RemainingActions < 1 ? Stop("the action budget is exhausted; retained outputs are not retried or overwritten.") : new(detail, [call]);
        static string Hash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit)
            ? value.ToLowerInvariant() : throw new ArgumentException("Missing exact quality identity.");
        try
        {
            var imported = request.Observations.LastOrDefault(o => o.Kind == ToolKind.ImportBlenderAsset &&
                o.Arguments.TryGetValue("asset_name", out var name) && name == intent.AssetName &&
                o.Arguments.TryGetValue("project_path", out var project) && project == projectPath);
            if (imported is not null)
            {
                if (!imported.Success) return Stop("the exact reviewed import stopped: " + imported.Detail);
                var spec = BlenderAssetImportSpec.From(new(imported.ToolCallId, imported.Kind, imported.Arguments, PermissionKind.Write, imported.Description));
                if (!spec.RequiresSourceReviews) return Stop("the import did not retain both exact source-review bindings.");
                var character = BlenderAssetSpec.IsCharacter(intent.Recipe);
                var next = InspectImported(request, imported, intent.AssetName, character, character, false, !character);
                if (next.Actions.Count != 0) return next.Actions.Count > request.RemainingActions ? Stop("the technical preparation budget is exhausted.") : next;
                var operation = character ? "prepare_character_animation" : "prepare_industrial_prop";
                var prepared = request.Observations.LastOrDefault(o => o.Kind == ToolKind.UnityEditorAction &&
                    o.Arguments.TryGetValue("operation", out var op) && op == operation && o.Arguments.TryGetValue("expected_sha256", out var h) && h == spec.ExpectedAssetSha256 &&
                    o.Arguments.TryGetValue("project_path", out var scope) && scope == projectPath && o.Arguments.TryGetValue("asset_path", out var source) && source == spec.DestinationPath);
                if (prepared?.Success != true) return Stop(next.Response);
                return new("Technical candidate prepared: exact source visual/rights reviews and the bounded Unity preparation passed. " +
                    "Target appearance, runtime performance and game-specific placement acceptance remain pending. This is not a guaranteed polished/refined final product, a new level or a finished game.", []);
            }

            string? bundle = null; string? asset = null;
            var created = request.Observations.LastOrDefault(o => o.Kind == ToolKind.CreateBlenderAsset &&
                o.Arguments.TryGetValue("asset_name", out var n) && n == intent.AssetName && o.Arguments.TryGetValue("project_path", out var p) && p == projectPath);
            if (created is not null)
            {
                if (!created.Success) return Stop("generation or validation failed. No blind retry, seed reroll or import will run: " + created.Detail);
                var sourceSpec = BlenderAssetSpec.From(new(created.ToolCallId, created.Kind, created.Arguments, PermissionKind.Execute, created.Description));
                if (sourceSpec.Recipe != intent.Recipe || sourceSpec.HeightMillimetres != intent.HeightMillimetres || sourceSpec.RadiusMillimetres != intent.RadiusMillimetres)
                    throw new ArgumentException("Created candidate does not meet the requested fixed quality profile.");
                if (created.Detail.Length > 32 * 1024) throw new JsonException();
                using var receipt = JsonDocument.Parse(created.Detail); var r = receipt.RootElement;
                if (r.GetProperty("recipe").GetString() != intent.Recipe || r.GetProperty("assetName").GetString() != intent.AssetName) throw new JsonException();
                bundle = Hash(r.GetProperty("bundleSha256").GetString()); asset = Hash(r.GetProperty("assetSha256").GetString());
            }
            else
            {
                var search = request.Observations.LastOrDefault(o => o.Kind == ToolKind.SearchLocalBlenderAssets &&
                    o.Arguments.TryGetValue("query", out var q) && q == intent.Label && o.Arguments.TryGetValue("recipe", out var id) && id == intent.Recipe);
                if (search is null) return Step(ToolCall.Create(ToolKind.SearchLocalBlenderAssets, PermissionKind.Read,
                    "Find compatible prior candidates automatically; no generation, import or quality approval",
                    ("query", intent.Label), ("recipe", intent.Recipe), ("height_min_mm", intent.HeightMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("height_max_mm", intent.HeightMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("max_results", "10")),
                    "I will check the local library, select the supported refined recipe where available, validate its source/preview, and request your exact visual/rights reviews before importing. Technical checks do not guarantee refined appearance.");
                if (!search.Success) return Stop("the local candidate search failed.");
                if (search.Detail.Length > 256 * 1024) throw new JsonException();
                using var found = JsonDocument.Parse(search.Detail); var root = found.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("library").GetString() != "local_blender_candidates") throw new JsonException();
                var entries = root.GetProperty("results").EnumerateArray().Select(e => e.Clone()).ToArray();
                if (entries.Length > 10) throw new JsonException();
                var rejected = false;
                foreach (var e in entries.OrderByDescending(e => e.GetProperty("sourceVisualReview").GetString() == "approved" &&
                    e.GetProperty("sourceRightsReview").GetString() == "approved").ThenBy(e => e.GetProperty("bundleSha256").GetString(), StringComparer.Ordinal))
                {
                    if (e.GetProperty("recipe").GetString() != intent.Recipe || e.GetProperty("heightMillimetres").GetInt32() != intent.HeightMillimetres ||
                        e.GetProperty("reviewState").GetString() != "unreviewed_local_recipe" || e.GetProperty("placementApproved").GetBoolean()) throw new JsonException();
                    if (intent.RadiusMillimetres != 0 && e.GetProperty("radiusMillimetres").GetInt32() != intent.RadiusMillimetres) continue;
                    var visual = e.GetProperty("sourceVisualReview").GetString(); var rights = e.GetProperty("sourceRightsReview").GetString();
                    if (visual is not ("pending" or "approved") || rights is not ("pending" or "approved")) { rejected = true; continue; }
                    bundle = Hash(e.GetProperty("bundleSha256").GetString()); asset = Hash(e.GetProperty("assetSha256").GetString()); break;
                }
                if (bundle is null)
                {
                    if (rejected) return Stop("prior compatible sources were rejected or have invalid reviews. A real recipe/parameter revision is required, not regeneration of the same rejected model.");
                    if (root.GetProperty("truncated").GetBoolean() || root.GetProperty("rejectedEntries").GetInt32() != 0) return Stop("the source search is partial or contains invalid evidence.");
                    var fields = new List<(string, string)> { ("project_path", projectPath), ("asset_name", intent.AssetName), ("recipe", intent.Recipe),
                        ("seed", "41"), ("height_mm", intent.HeightMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
                    if (intent.RadiusMillimetres != 0) fields.Add(("radius_mm", intent.RadiusMillimetres.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    return Step(ToolCall.Create(ToolKind.CreateBlenderAsset, PermissionKind.Execute,
                        "Create a bounded candidate using the current supported refined profile, retain editable source and preview; not final appearance approval", fields.ToArray()),
                        "No compatible source was found. I will create the supported candidate locally, then validate it and show the exact source preview for your review.");
                }
            }

            var inspected = request.Observations.LastOrDefault(o => o.Kind == ToolKind.InspectLocalBlenderAsset && o.Arguments.TryGetValue("bundle_sha256", out var b) && b == bundle);
            if (inspected is null) return Step(ToolCall.Create(ToolKind.InspectLocalBlenderAsset, PermissionKind.Read,
                "Check exact source quality measurements, preview, current review decisions and experience", ("bundle_sha256", bundle!)),
                "I will check the exact source against the bounded quality profile before asking for appearance/rights review. No import yet.");
            if (!inspected.Success) return Stop("the exact source/preview or its evidence could not be verified.");
            if (inspected.Detail.Length > 256 * 1024) throw new JsonException();
            using var packet = JsonDocument.Parse(inspected.Detail); var pRoot = packet.RootElement;
            if (pRoot.GetProperty("schemaVersion").GetInt32() != 1 || pRoot.GetProperty("kind").GetString() != "local_blender_source_review_packet" ||
                pRoot.GetProperty("targetProjectApproved").GetBoolean() || pRoot.GetProperty("recipe").GetString() != intent.Recipe) throw new JsonException();
            var identity = JsonSerializer.Deserialize<LocalBlenderReviewIdentity>(pRoot.GetProperty("identity"), QualityJson) ?? throw new JsonException();
            if (identity.BundleSha256 != bundle || identity.AssetSha256 != asset) throw new JsonException();
            var quality = JsonSerializer.Deserialize<BlenderSourceQualityEvidence>(pRoot.GetProperty("quality"), QualityJson) ?? throw new JsonException();
            quality.Validate(intent.Recipe, asset!);
            var learning = pRoot.GetProperty("learningPlan");
            if (learning.GetProperty("policyVersion").GetString() != AssetImprovementModel.Version || learning.GetProperty("bundleSha256").GetString() != bundle ||
                learning.GetProperty("grantsApproval").GetBoolean() || learning.GetProperty("neuralWeightsTrained").GetBoolean()) throw new JsonException();

            string? DecisionHash(string kind)
            {
                var current = pRoot.GetProperty(kind == "visual" ? "sourceVisualEvidence" : "sourceRightsEvidence");
                var decision = request.Observations.LastOrDefault(o => o.Kind == ToolKind.ReviewGameArtAsset &&
                    o.Arguments.TryGetValue("bundle_sha256", out var b) && b == bundle && o.Arguments.TryGetValue("review_kind", out var k) && k == kind);
                if (decision is null)
                {
                    var state = current.GetProperty("state").GetString();
                    return state switch { "approved" => Hash(current.GetProperty("recordSha256").GetString()), "pending" => null,
                        _ => throw new ArgumentException("A source review is rejected, changed or invalid; revision is required.") };
                }
                if (!decision.Success) throw new ArgumentException("A source review was cancelled, rejected or failed; no acceptance or retry is inferred.");
                using var recorded = JsonDocument.Parse(decision.Detail); var r = recorded.RootElement;
                var reviewedIdentity = JsonSerializer.Deserialize<LocalBlenderReviewIdentity>(r.GetProperty("identity"), QualityJson);
                if (r.GetProperty("schemaVersion").GetInt32() != 1 || r.GetProperty("kind").GetString() != "local_blender_source_review" ||
                    r.GetProperty("reviewKind").GetString() != kind || r.GetProperty("decision").GetString() != "approved" ||
                    r.GetProperty("targetProjectApproved").GetBoolean() || reviewedIdentity != identity ||
                    r.GetProperty("recordPath").GetString() != $".forgecore/blender-library/reviews/{bundle}/{decision.ToolCallId:N}.json")
                    throw new ArgumentException("No exact approved human source decision exists for this candidate.");
                return Hash(r.GetProperty("recordSha256").GetString());
            }
            // Resolve both statuses before proposing either review: a known rejection is not a reason to
            // pester the user with another review or to reroll a seed.
            var visualHash = DecisionHash("visual"); var rightsHash = DecisionHash("rights");
            if (visualHash is null) return Step(LocalBlenderReviewSpec.Proposal(identity, "visual"), "Technical source checks passed. Please inspect the exact preview; only your visual decision can accept its appearance. You can reject it without importing anything.");
            if (rightsHash is null) return Step(LocalBlenderReviewSpec.Proposal(identity, "rights"), "The exact visual source decision is retained. Its source usage rights still need your separate review; no target-game acceptance is implied.");
            return Step(ToolCall.Create(ToolKind.ImportBlenderAsset, PermissionKind.Write,
                "Import exact source only while both bound current source reviews remain approved; no overwrite or target-quality certification",
                ("project_path", projectPath), ("asset_name", intent.AssetName), ("bundle_sha256", bundle!), ("expected_asset_sha256", asset!),
                ("expected_visual_review_sha256", visualHash), ("expected_rights_review_sha256", rightsHash)),
                "Both exact source decisions are approved. The importer will recheck their current hashes before writing, then the existing Unity technical workflow can run. Final target quality remains pending.");
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return Stop("quality or review evidence is invalid/incomplete: " + error.Message); }
    }
}
