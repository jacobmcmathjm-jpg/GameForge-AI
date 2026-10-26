using System.Globalization;
using System.Text.RegularExpressions;

namespace ForgeCore.Core;

// No Python, arbitrary input file, URL, executable, or output path is model-controlled.
public sealed record BlenderAssetSpec(string ProjectPath, string AssetName, string Recipe, int Seed,
    int HeightMillimetres, int RadiusMillimetres)
{
    public const string SupportedRecipe = "weathered_barrel_v1";
    public static BlenderAssetSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.CreateBlenderAsset) throw new ArgumentException("Not a local Blender creation call.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "asset_name", "recipe", "seed", "height_mm", "radius_mm");
        var name = Name(StagedAssetInspectionSpec.Get(call, "asset_name"));
        var recipe = StagedAssetInspectionSpec.Get(call, "recipe");
        if (recipe != SupportedRecipe) throw new ArgumentException("Only weathered_barrel_v1 is implemented.");
        return new(StagedAssetInspectionSpec.Get(call, "project_path"), name, recipe,
            Integer(call, "seed", 0, 999999), Integer(call, "height_mm", 1000, 1500), Integer(call, "radius_mm", 250, 500));
    }
    internal static string Name(string value) => Regex.IsMatch(value, "^[a-z][a-z0-9_-]{1,47}$", RegexOptions.CultureInvariant)
        ? value : throw new ArgumentException("asset_name must be a lowercase bounded identifier (2-48 characters).");
    private static int Integer(ToolCall call, string name, int min, int max) =>
        int.TryParse(StagedAssetInspectionSpec.Get(call, name), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
            ? n : throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
}

public sealed record BlenderAssetImportSpec(string ProjectPath, string BundleSha256, string ExpectedAssetSha256, string AssetName)
{
    public string DestinationPath => $"Assets/ForgeCore/Generated/Blender/{AssetName}/{AssetName}.glb";
    public static BlenderAssetImportSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ImportBlenderAsset) throw new ArgumentException("Not a local Blender import call.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "bundle_sha256", "expected_asset_sha256", "asset_name");
        return new(StagedAssetInspectionSpec.Get(call, "project_path"),
            ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "bundle_sha256"), "bundle_sha256"),
            ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "expected_asset_sha256"), "expected_asset_sha256"),
            BlenderAssetSpec.Name(StagedAssetInspectionSpec.Get(call, "asset_name")));
    }
}
