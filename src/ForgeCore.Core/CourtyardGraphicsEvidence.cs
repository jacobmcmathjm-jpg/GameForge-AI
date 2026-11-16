using System.Text.Json;
namespace ForgeCore.Core;

/// <summary>Technical benchmark limits, never an artistic acceptance score.</summary>
public static class CourtyardGraphicsEvidence
{
    public const string Profile = "industrial_courtyard_detail_v2";
    public static void Validate(JsonElement r)
    {
        if (r.GetProperty("schemaVersion").GetInt32() != 1 || r.GetProperty("profile").GetString() != Profile ||
            r.GetProperty("source").GetString() != "original_bounded_procedural_template" ||
            r.GetProperty("renderScope").GetString() != "static_editor_comparison_not_runtime_animation" ||
            r.GetProperty("textureSize").GetInt32() != 1024 || r.GetProperty("textureCount").GetInt32() != 9 ||
            r.GetProperty("viewCount").GetInt32() != 3 || r.GetProperty("comparisonWidth").GetInt32() != 1920 || r.GetProperty("comparisonHeight").GetInt32() != 1620 ||
            !r.GetProperty("collisionUnchanged").GetBoolean() || r.GetProperty("decorativeColliderCount").GetInt32() != 0 ||
            r.GetProperty("addedRenderers").GetInt32() is <= 0 or > 6 || r.GetProperty("addedTriangles").GetInt32() is <= 0 or > 25000 ||
            r.GetProperty("ownedTextureMemoryBytes").GetInt64() is <= 0 or > 64 * 1024 * 1024 ||
            r.GetProperty("magentaPixels").GetInt32() != 0 ||
            r.GetProperty("visualReviewed").GetBoolean() || r.GetProperty("rightsReviewed").GetBoolean() ||
            r.GetProperty("stageComplete").GetBoolean() || r.GetProperty("approvedLibraryAssetsPlaced").GetBoolean())
            throw new InvalidDataException("Graphics benchmark identity, resource, collision or acceptance boundary failed.");
        var range = r.GetProperty("minimumLuminanceRange").GetDouble();
        if (!double.IsFinite(range) || range is < .08 or > 1)
            throw new InvalidDataException("Graphics comparison did not pass its bounded render sanity check.");
    }
}
