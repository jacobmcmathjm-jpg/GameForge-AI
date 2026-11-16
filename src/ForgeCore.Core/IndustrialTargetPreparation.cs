namespace ForgeCore.Core;

/// <summary>Exact read-only technical-review destinations; not an execution, target review or placement approval.</summary>
public sealed record IndustrialTargetPreparation(string SlotId,string Recipe,string BundleSha256,string AssetSha256,
    string ImportedAssetPath,string AssetSetId,string PrefabPath,string ScenePath,string PreviewPath)
{
    public static IndustrialTargetPreparation Create(IndustrialAssetPlanSpec spec,IndustrialAssetSlot slot,string bundle,string asset)
    {
        _=IndustrialAssetPlanSpec.From(ToolCall.Create(ToolKind.PlanIndustrialAssets,PermissionKind.Read,"Validate bounded target plan",("project_path",spec.ProjectPath),("level_id",spec.LevelId),("recipe_set",spec.RecipeSet)));
        if(!IndustrialAssetPlan.SlotsFor(spec.RecipeSet).Contains(slot))throw new ArgumentException("Unknown fixed industrial slot/revision.");
        bundle=ReviewedAssetStoreSpec.NormalizeHash(bundle,"bundle_sha256");asset=ReviewedAssetStoreSpec.NormalizeHash(asset,"asset_sha256");
        var importId=spec.LevelId+"-"+slot.Id+"-"+bundle[..8];var targetId=importId+"-unity";
        return new(slot.Id,slot.Recipe,bundle,asset,new BlenderAssetImportSpec(spec.ProjectPath,bundle,asset,importId).DestinationPath,
            targetId,"Assets/ForgeCore/IndustrialProps/"+targetId+"/IndustrialProp.prefab",
            "Assets/Scenes/IndustrialReview/"+targetId+".unity","Previews/IndustrialReview/"+targetId+".png");
    }
    public ToolCall Proposal(string project)=>ToolCall.Create(ToolKind.UnityEditorAction,PermissionKind.Execute,
        "Prepare this exact imported prop for technical Unity review, not human approval or level placement",
        ("project_path",project),("operation","prepare_industrial_prop"),("asset_path",ImportedAssetPath),("expected_sha256",AssetSha256),
        ("asset_set_id",AssetSetId),("scene_path",ScenePath),("output_path",PreviewPath));
}
