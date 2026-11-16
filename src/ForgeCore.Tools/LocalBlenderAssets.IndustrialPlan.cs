using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.Tools;
internal sealed partial class LocalBlenderAssets
{
    private sealed record IndustrialSelection(string SlotId,string Recipe,int Count,string Purpose,string State,
        string? BundleSha256,string? AssetSha256,string? PromotionSha256,string[] UnmetRequirements);
    private sealed record BoundIndustrialPlacement(IndustrialPlacementFootprint Footprint,string? BundleSha256,string? AssetSha256);
    private sealed record IndustrialManifest(int SchemaVersion,string Profile,string ProjectPath,string LevelId,
        IndustrialSelection[] Slots,string LayoutVersion,BoundIndustrialPlacement[] Placements,
        IndustrialTargetPreparation[] TargetPreparations,
        bool ReadyForTargetPreparation,bool ReadyForAutomaticPlacement,string[] TargetGates,
        [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]string? RecipeSet=null);
    private async Task<IndustrialManifest> IndustrialManifestAsync(IndustrialAssetPlanSpec spec,CancellationToken token)
    {
        RequireProject(spec.ProjectPath);
        var selections=new List<IndustrialSelection>();
        foreach(var slot in IndustrialAssetPlan.SlotsFor(spec.RecipeSet))
        {
            var recipe=IndustrialPropRecipes.Find(slot.Recipe)!;
            var result=await SearchAsync(new("prop","environment_props",slot.Recipe,false,recipe.DefaultHeight,recipe.DefaultHeight,16000,20),token);
            using var search=JsonDocument.Parse(result);var root=search.RootElement;
            var candidates=root.GetProperty("results").EnumerateArray().OrderBy(e=>e.GetProperty("bundleSha256").GetString(),StringComparer.Ordinal).ToArray();
            IndustrialSelection? selected=null;
            var geometryMismatch=false;
            if(!root.GetProperty("truncated").GetBoolean())foreach(var candidate in candidates)
            {
                var hash=candidate.GetProperty("bundleSha256").GetString()!;var bundle=await VerifyBundle(hash,token);
                if(!IndustrialPlacementLayout.FitsSource(bundle.Request.Recipe,bundle.Request.HeightMillimetres,bundle.Request.RadiusMillimetres))
                {geometryMismatch=true;continue;}
                var identity=IdentityOf(hash,bundle);var visual=await LatestReviewInfoAsync(identity,"visual",token);var rights=await LatestReviewInfoAsync(identity,"rights",token);
                if(visual.State!="approved"||rights.State!="approved"||await PromotionStateAsync(bundle,identity,visual,rights,token)!="current")continue;
                selected=new(slot.Id,slot.Recipe,slot.Count,slot.Purpose,"exact_current_human_reviewed_source",hash,identity.AssetSha256,
                    Hash(JsonSerializer.SerializeToUtf8Bytes(PromotionFor(bundle,identity,visual,rights),Json)),[]);
                break;
            }
            selections.Add(selected??new(slot.Id,slot.Recipe,slot.Count,slot.Purpose,
                candidates.Length==0?"missing_supported_source":geometryMismatch?"source_geometry_or_review_required":"source_review_or_promotion_required",null,null,null,
                root.GetProperty("truncated").GetBoolean()?["candidate_search_truncated"]:
                candidates.Length==0?["generate_supported_recipe","live_source_visual_review","live_source_rights_review","current_source_promotion"]:
                geometryMismatch?["exact_authored_footprint_source","live_source_visual_review","live_source_rights_review","current_source_promotion"]:
                ["live_source_visual_review","live_source_rights_review","current_source_promotion"]));
        }
        var placements=IndustrialPlacementLayout.For(spec.RecipeSet).Select(p=>{
            var selected=selections.Single(s=>s.SlotId==p.SlotId);
            return new BoundIndustrialPlacement(p with{Recipe=selected.Recipe},selected.BundleSha256,selected.AssetSha256);
        }).ToArray();
        var preparations=selections.Where(s=>s.BundleSha256!=null).Select(s=>IndustrialTargetPreparation.Create(spec,
            IndustrialAssetPlan.SlotsFor(spec.RecipeSet).Single(slot=>slot.Id==s.SlotId),s.BundleSha256!,s.AssetSha256!)).ToArray();
        return new(spec.RecipeSet=="legacy_v1"?2:3,IndustrialAssetPlan.MissionProfile,spec.ProjectPath,spec.LevelId,selections.ToArray(),IndustrialPlacementLayout.VersionFor(spec.RecipeSet),placements,preparations,selections.All(s=>s.BundleSha256!=null),
            false,["exact_target_unity_inspection","target_visual_and_rights_review","fresh_prefab_and_scene_placement","collision_lod_navigation_clearance","runtime_playthrough_and_human_balance"],spec.RecipeSet=="legacy_v1"?null:spec.RecipeSet);
    }
    public async Task<string> PlanIndustrialAssetsAsync(IndustrialAssetPlanSpec spec,CancellationToken token)
    {
        var manifest=await IndustrialManifestAsync(spec,token);var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,Json);
        var sourceReviewPreflight=new List<object>();
        foreach(var slot in IndustrialAssetPlan.SlotsFor(spec.RecipeSet)) {
            var recipe=IndustrialPropRecipes.Find(slot.Recipe)!;
            using var search=JsonDocument.Parse(await SearchAsync(new("prop","environment_props",slot.Recipe,false,recipe.DefaultHeight,recipe.DefaultHeight,16000,20),token));
            var candidates=new List<object>();
            if(!search.RootElement.GetProperty("truncated").GetBoolean())foreach(var item in search.RootElement.GetProperty("results").EnumerateArray().OrderBy(x=>x.GetProperty("bundleSha256").GetString(),StringComparer.Ordinal)) {
                var hash=item.GetProperty("bundleSha256").GetString()!;var bundle=await VerifyBundle(hash,token);
                if(!IndustrialPlacementLayout.FitsSource(bundle.Request.Recipe,bundle.Request.HeightMillimetres,bundle.Request.RadiusMillimetres))continue;
                var identity=IdentityOf(hash,bundle);
                candidates.Add(new{identity,recipe=slot.Recipe,previewPath=$"{Library}/objects/{hash}/preview.png",
                    currentVisualReview=await LatestReviewAsync(identity,"visual",token),currentRightsReview=await LatestReviewAsync(identity,"rights",token),
                    inspectProposal=ToolCall.Create(ToolKind.InspectLocalBlenderAsset,PermissionKind.Read,"Read this exact candidate source packet, no decision",("bundle_sha256",hash)),
                    visualReviewProposal=LocalBlenderReviewSpec.Proposal(identity,"visual"),rightsReviewProposal=LocalBlenderReviewSpec.Proposal(identity,"rights")});
            }
            sourceReviewPreflight.Add(new{slotId=slot.Id,recipe=slot.Recipe,candidateSearchTruncated=search.RootElement.GetProperty("truncated").GetBoolean(),
                candidates,humanDecisionsRequired=true,executed=false});
        }
        // Mutable destination occupancy stays OUTSIDE planSha256: importing a source must not invalidate the remaining set's identity.
        var preflight=manifest.TargetPreparations.Select(step=>new{step.SlotId,
            destinationsFresh=new[]{Path.GetDirectoryName(step.PrefabPath)!,step.ScenePath,step.PreviewPath,step.PreviewPath+".forgecore-rig-preview.json"}
                .SelectMany(p=>new[]{p,p+".meta"}).All(p=>{var full=paths.Resolve(Path.Combine(spec.ProjectPath,p),true);return !File.Exists(full)&&!Directory.Exists(full);}),
            action=step.Proposal(spec.ProjectPath),humanTargetReviewRequired=true,automaticPlacementApproved=false}).ToArray();
        var placementAudit=IndustrialPlacementAudit.Assess(manifest.Placements.Select(p=>p.Footprint).ToArray());
        return JsonSerializer.Serialize(new{schemaVersion=1,kind="industrial_asset_manifest",planSha256=Hash(bytes),manifest,placementAudit,
            sourceReviewPreflight,targetPreparationPreflight=preflight,
            qualification="Read-only source/footprint and exact target-review work plan. Target calls are proposals, never automatically executed or approved. Mutable occupancy is outside the source-plan identity and must be rechecked at execution. Missing real reviews remain explicit; no imports, editor operations or placement occurred."},Json);
    }
    public Task<string> ImportIndustrialAssetSetAsync(IndustrialAssetPlanSpec spec,string expectedPlan,CancellationToken token)=>WithSourceDecisionLease(async()=>
    {
        var manifest=await IndustrialManifestAsync(spec,token);var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,Json);
        if(Hash(bytes)!=expectedPlan)throw new InvalidDataException("Industrial plan changed; replan and approve the new exact identity.");
        if(!manifest.ReadyForTargetPreparation)throw new InvalidOperationException("Industrial asset import awaits exact current live source visual/rights reviews and promotion for every slot. Nothing imported.");
        RequireIdleEditors();
        var calls=manifest.Slots.Select(s=>new BlenderAssetImportSpec(spec.ProjectPath,s.BundleSha256!,s.AssetSha256!,spec.LevelId+"-"+s.SlotId+"-"+s.BundleSha256![..8])).ToArray();
        foreach(var call in calls) {
            var folder=Path.GetDirectoryName(paths.Resolve(Path.Combine(call.ProjectPath,call.DestinationPath),true))!;
            if(File.Exists(folder)||Directory.Exists(folder)||File.Exists(folder+".meta")||Directory.Exists(folder+".meta"))
                throw new IOException("An industrial import destination is occupied; no imports started.");
        }
        var journal=paths.Resolve(Path.Combine(spec.ProjectPath,".forgecore/unity/industrial-imports",expectedPlan),true);
        if(File.Exists(journal)||Directory.Exists(journal))throw new IOException("Industrial import journal exists; no ambiguous replay.");
        Directory.CreateDirectory(journal);await WriteNew(Path.Combine(journal,"plan.json"),bytes,token);
        var completed=new List<string>();
        try {
            for(var i=0;i<calls.Length;i++) {
                if(Hash(JsonSerializer.SerializeToUtf8Bytes(await IndustrialManifestAsync(spec,token),Json))!=expectedPlan)
                    throw new InvalidDataException("Source/review selection changed during industrial import.");
                var receipt=await ImportAsync(calls[i],token);
                await WriteNew(Path.Combine(journal,i.ToString("00")+".json"),System.Text.Encoding.UTF8.GetBytes(receipt),CancellationToken.None);
                completed.Add(calls[i].DestinationPath);
            }
            return JsonSerializer.Serialize(new{schemaVersion=1,kind="industrial_source_import_set",planSha256=expectedPlan,importedAssets=completed,
                targetProjectApproved=false,placementApproved=false,nextAction="Prepare exact target Unity prefabs/review evidence; human target approvals remain required."},Json);
        }catch {
            await WriteNew(Path.Combine(journal,"stopped.json"),JsonSerializer.SerializeToUtf8Bytes(new{completed,automaticReplayAllowed=false},Json),CancellationToken.None);
            throw;
        }
    },token);
}
