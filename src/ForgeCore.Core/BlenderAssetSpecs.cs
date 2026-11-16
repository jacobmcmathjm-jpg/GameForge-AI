using System.Globalization;
using System.Text.RegularExpressions;

namespace ForgeCore.Core;

// No Python, arbitrary input file, URL, executable, or output path is model-controlled.
public sealed record BlenderAssetSpec(string ProjectPath, string AssetName, string Recipe, int Seed,
    int HeightMillimetres, int RadiusMillimetres)
{
    public const string SupportedRecipe = "weathered_barrel_v1";
    public const string CharacterRecipe = "rigged_humanoid_v1";
    public const string SecurityCharacterRecipe = "industrial_security_v1";
    public static bool IsCharacter(string recipe) => recipe is CharacterRecipe or SecurityCharacterRecipe;
    public static BlenderAssetSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.CreateBlenderAsset) throw new ArgumentException("Not a local Blender creation call.");
        var name = Name(StagedAssetInspectionSpec.Get(call, "asset_name"));
        var recipe = StagedAssetInspectionSpec.Get(call, "recipe");
        if (IsCharacter(recipe))
        {
            StagedAssetInspectionSpec.RequireOnly(call, "project_path", "asset_name", "recipe", "seed", "height_mm");
            return new(StagedAssetInspectionSpec.Get(call,"project_path"),name,recipe,
                Integer(call,"seed",0,999999),Integer(call,"height_mm",1600,2000),0);
        }
        if(IndustrialPropRecipes.Find(recipe)is {} industrial)
        {
            StagedAssetInspectionSpec.RequireOnly(call,industrial.IsBarrel
                ? ["project_path","asset_name","recipe","seed","height_mm","radius_mm"]
                : ["project_path","asset_name","recipe","seed","height_mm"]);
            return new(StagedAssetInspectionSpec.Get(call,"project_path"),name,recipe,Integer(call,"seed",0,999999),
                Integer(call,"height_mm",industrial.MinHeight,industrial.MaxHeight),industrial.IsBarrel?Integer(call,"radius_mm",250,500):0);
        }
        if (recipe != SupportedRecipe) throw new ArgumentException("Only bundled, versioned barrel, industrial prop and character recipes are implemented.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "asset_name", "recipe", "seed", "height_mm", "radius_mm");
        return new(StagedAssetInspectionSpec.Get(call, "project_path"), name, recipe,
            Integer(call, "seed", 0, 999999), Integer(call, "height_mm", 1000, 1500), Integer(call, "radius_mm", 250, 500));
    }
    internal static string Name(string value) => Regex.IsMatch(value, "^[a-z][a-z0-9_-]{1,47}$", RegexOptions.CultureInvariant)
        ? value : throw new ArgumentException("asset_name must be a lowercase bounded identifier (2-48 characters).");
    private static int Integer(ToolCall call, string name, int min, int max) =>
        int.TryParse(StagedAssetInspectionSpec.Get(call, name), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
            ? n : throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
}

public sealed record BlenderAssetImportSpec(string ProjectPath, string BundleSha256, string ExpectedAssetSha256, string AssetName,
    string? ExpectedVisualReviewSha256 = null, string? ExpectedRightsReviewSha256 = null)
{
    public bool RequiresSourceReviews => ExpectedVisualReviewSha256 is not null;
    public string DestinationPath => $"Assets/ForgeCore/Generated/Blender/{AssetName}/{AssetName}.glb";
    public static BlenderAssetImportSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.ImportBlenderAsset) throw new ArgumentException("Not a local Blender import call.");
        StagedAssetInspectionSpec.RequireOnly(call, "project_path", "bundle_sha256", "expected_asset_sha256", "asset_name",
            "expected_visual_review_sha256", "expected_rights_review_sha256");
        var reviewed = call.Arguments.ContainsKey("expected_visual_review_sha256") || call.Arguments.ContainsKey("expected_rights_review_sha256");
        return new(StagedAssetInspectionSpec.Get(call, "project_path"),
            ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "bundle_sha256"), "bundle_sha256"),
            ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call, "expected_asset_sha256"), "expected_asset_sha256"),
            BlenderAssetSpec.Name(StagedAssetInspectionSpec.Get(call, "asset_name")),
            reviewed ? ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,"expected_visual_review_sha256"),"expected_visual_review_sha256") : null,
            reviewed ? ReviewedAssetStoreSpec.NormalizeHash(StagedAssetInspectionSpec.Get(call,"expected_rights_review_sha256"),"expected_rights_review_sha256") : null);
    }
}

/// <summary>Read-only candidate discovery. It never confers visual/rights/gameplay approval.</summary>
public sealed record LocalBlenderSearchSpec(string Query, string? AssetRole, string? Recipe, bool RigRequired,
    int MinimumHeightMillimetres, int MaximumHeightMillimetres, int MaximumTriangles, int MaxResults)
{
    public static LocalBlenderSearchSpec From(ToolCall call)
    {
        if (call.Kind != ToolKind.SearchLocalBlenderAssets) throw new ArgumentException("Not a local Blender library search.");
        StagedAssetInspectionSpec.RequireOnly(call,"query","asset_role","recipe","rig_required","height_min_mm","height_max_mm","max_triangles","max_results");
        var query=StagedAssetInspectionSpec.Get(call,"query").Trim().ToLowerInvariant();
        if(query.Length is < 2 or > 120 || !Regex.IsMatch(query,"^[a-z0-9][a-z0-9 _-]*$",RegexOptions.CultureInvariant) || !query.Any(char.IsLetterOrDigit))
            throw new ArgumentException("query must be 2-120 lowercase-compatible letters, numbers, spaces, hyphens or underscores; no paths/scripts.");
        call.Arguments.TryGetValue("asset_role",out var role);call.Arguments.TryGetValue("recipe",out var recipe);
        if(role is not null && role is not ("environment_props" or "characters"))throw new ArgumentException("asset_role must be environment_props or characters.");
        if(recipe is not null && recipe != BlenderAssetSpec.SupportedRecipe && !BlenderAssetSpec.IsCharacter(recipe)&&!IndustrialPropRecipes.Contains(recipe))throw new ArgumentException("recipe is not implemented.");
        var rig=false;
        if(call.Arguments.TryGetValue("rig_required",out var text))rig=text switch {"true"=>true,"false"=>false,_=>throw new ArgumentException("rig_required must be true or false.")};
        int Number(string name,int fallback,int min,int max) => !call.Arguments.TryGetValue(name,out var value)?fallback:
            int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>=min&&n<=max?n:throw new ArgumentException($"{name} must be {min}-{max}.");
        var low=Number("height_min_mm",0,0,20000);var high=Number("height_max_mm",20000,1,20000);
        if(low>high)throw new ArgumentException("Minimum height must not exceed maximum height.");
        return new(query,role,recipe,rig,low,high,Number("max_triangles",60000,1,60000),Number("max_results",10,1,20));
    }
}
