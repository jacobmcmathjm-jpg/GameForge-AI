using System.Text.RegularExpressions;

namespace ForgeCore.Core;

/// <summary>Ordinary words select fixed local workflows, never arbitrary scripts or guaranteed art quality.</summary>
public sealed record SimpleAssetRequest(string Label, string Recipe, string AssetName, int HeightMillimetres, int RadiusMillimetres)
{
    public static SimpleAssetRequest? Parse(string text)
    {
        if (text.Length > 500) return null;
        var match = Regex.Match(text,
            @"^\s*(?:please\s+)?(?:create|make|generate)\s+(?:me\s+)?(?:an?\s+)?(?:(?:refined|detailed|realistic|game-ready|high-detail|high\s+detail)\s+)?(?<label>industrial\s+barrel|weathered\s+barrel|barrel|wooden\s+crate|crate|concrete\s+barrier|industrial\s+fence|fence|shipping\s+container|container|rigged\s+mannequin|mannequin|industrial\s+security\s+character|security\s+guard|guard)(?:\s+(?:named|called)\s+(?<name>[a-z][a-z0-9_-]{1,39}))?[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var label = Regex.Replace(match.Groups["label"].Value.ToLowerInvariant(), @"\s+", " ");
        var recipe = label switch
        {
            "industrial barrel" or "weathered barrel" or "barrel" => "industrial_barrel_v3",
            "wooden crate" or "crate" => "wooden_crate_v2", "concrete barrier" => "concrete_barrier_v2",
            "industrial fence" or "fence" => "industrial_fence_v2", "shipping container" or "container" => "shipping_container_v2",
            "rigged mannequin" or "mannequin" => BlenderAssetSpec.CharacterRecipe, _ => BlenderAssetSpec.SecurityCharacterRecipe
        };
        var prop = IndustrialPropRecipes.Find(recipe);
        var canonical = prop?.FamilyId switch
        {
            "industrial_barrel_v2" => "industrial barrel", "wooden_crate_v1" => "wooden crate", "concrete_barrier_v1" => "concrete barrier",
            "industrial_fence_v1" => "industrial fence", "shipping_container_v1" => "shipping container",
            _ => recipe == BlenderAssetSpec.CharacterRecipe ? "rigged mannequin" : "industrial security character"
        };
        var name = match.Groups["name"].Success ? match.Groups["name"].Value.ToLowerInvariant() : "quality-" + canonical.Replace(' ', '-');
        return new(canonical, recipe, name, prop?.DefaultHeight ?? 1800, prop?.IsBarrel == true ? 340 : 0);
    }
}

public sealed record BlenderSourceQualityEvidence(int SchemaVersion, string Profile, string Recipe, string AssetSha256,
    int Triangles, int TextureResolution, bool EditableSource, bool PreviewVerified, bool HasNormalMap,
    int LodCount, bool RigVerified, bool TechnicalChecksPassed, bool HumanVisualAccepted)
{
    public const string ProfileVersion = "bounded_source_quality_v1";
    public void Validate(string expectedRecipe, string expectedAssetHash)
    {
        var character = BlenderAssetSpec.IsCharacter(expectedRecipe);
        if (SchemaVersion != 1 || Profile != ProfileVersion || Recipe != expectedRecipe || AssetSha256 != expectedAssetHash ||
            !EditableSource || !PreviewVerified || !TechnicalChecksPassed || HumanVisualAccepted ||
            Triangles is < 100 or > 60000 || TextureResolution != (expectedRecipe == BlenderAssetSpec.CharacterRecipe ? 512 : 1024) ||
            HasNormalMap != (expectedRecipe != BlenderAssetSpec.CharacterRecipe) || LodCount != (character ? 1 : 3) || RigVerified != character)
            throw new ArgumentException("Source quality evidence is incomplete or incompatible. Technical checks cannot certify refined appearance.");
    }
}
