using System.Text.Json;
using ForgeCore.Core;

namespace ForgeCore.Tools;

internal sealed partial class LocalBlenderAssets
{
    // Bound enumeration, verification I/O and output independently. No cache/index writes or editor launch.
    private const int MaximumSearchEntries=128;
    private const long MaximumSearchVerificationBytes=512L*1024*1024;
    public async Task<string> SearchAsync(LocalBlenderSearchSpec spec,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var objects=paths.Resolve(Library+"/objects",true);
        var candidates=new List<object>();var scanned=0;var rejected=0;var truncated=false;long verifiedBytes=0;
        var query=spec.Query.Split([' ','-','_'],StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
        if(Directory.Exists(objects))
        {
            var directories=new List<string>();
            using(var iterator=Directory.EnumerateDirectories(objects).GetEnumerator())
            {
                while(iterator.MoveNext())
                {
                    token.ThrowIfCancellationRequested();
                    if(directories.Count==MaximumSearchEntries){truncated=true;break;}
                    directories.Add(iterator.Current);
                }
            }
            foreach(var directory in directories.Order(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();scanned++;
                try
                {
                    var hash=Path.GetFileName(directory);
                    if(hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigit(c))||
                        (new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Invalid candidate directory.");
                    var entry=paths.Resolve(Path.Combine(directory,"entry.json"),false);
                    if(await HashFile(entry,128*1024,token)!=hash)throw new InvalidDataException("Changed entry.");
                    var metadata=await ReadJson<Bundle>(entry,128*1024,token);
                    var request=metadata.Request ?? throw new InvalidDataException("Missing request.");
                    var role=BlenderAssetSpec.IsCharacter(request.Recipe)?"characters":"environment_props";
                    var words=IndustrialPropRecipes.Find(request.Recipe)?.Tags??(request.Recipe==BlenderAssetSpec.SupportedRecipe?"industrial metal weathered barrel prop":
                        request.Recipe==BlenderAssetSpec.SecurityCharacterRecipe?"industrial security guard uniform humanoid rigged character":"rigged humanoid mannequin character");
                    var haystack=(request.AssetName+" "+request.Recipe+" "+words).ToLowerInvariant();
                    if(query.Any(q=>!haystack.Contains(q,StringComparison.Ordinal)) ||
                        spec.AssetRole is not null&&spec.AssetRole!=role || spec.Recipe is not null&&spec.Recipe!=request.Recipe ||
                        spec.RigRequired&&!BlenderAssetSpec.IsCharacter(request.Recipe) ||
                        request.HeightMillimetres<spec.MinimumHeightMillimetres || request.HeightMillimetres>spec.MaximumHeightMillimetres ||
                        metadata.Metrics is null || metadata.Metrics.Triangles>spec.MaximumTriangles)continue;
                    if(metadata.Files is null||metadata.Files.Length>16||metadata.Files.Any(f=>f is null||f.Bytes is < 1 or > MaxFileBytes))
                        throw new InvalidDataException("Unbounded candidate files.");
                    var bytes=metadata.Files.Sum(f=>f.Bytes);
                    if(bytes>200L*1024*1024)throw new InvalidDataException("Oversized candidate bundle.");
                    // Account for metadata, hashing and structural readers. A budget skip is explicit, never a claimed full scan.
                    if(bytes>(MaximumSearchVerificationBytes-verifiedBytes)/3){truncated=true;continue;}
                    verifiedBytes+=bytes*3;
                    var bundle=await VerifyBundle(hash,token);
                    var asset=bundle.Files.Single(f=>f.Name=="asset.glb");
                    var identity=new LocalBlenderReviewIdentity(hash,asset.Sha256,bundle.Files.Single(f=>f.Name=="model.blend").Sha256,
                        bundle.Files.Single(f=>f.Name=="preview.png").Sha256,bundle.Files.Single(f=>f.Name=="metrics.json").Sha256);
                    var visual=await LatestReviewInfoAsync(identity,"visual",token);var rights=await LatestReviewInfoAsync(identity,"rights",token);
                    candidates.Add(new
                    {
                        bundleSha256=hash,assetSha256=asset.Sha256,request.AssetName,request.Recipe,assetRole=role,
                        heightMillimetres=request.HeightMillimetres,boundsMin=bundle.Metrics.BoundsMin,boundsMax=bundle.Metrics.BoundsMax,
                        request.Seed,radiusMillimetres=request.RadiusMillimetres,
                        boundsCoordinateSystem="Blender Z-up metres; target Unity measurements still required",
                        bundle.Metrics.Triangles,bundle.Metrics.Materials,bundle.Metrics.TextureResolution,bundle.Metrics.Pivot,
                        rigged=BlenderAssetSpec.IsCharacter(request.Recipe),animationCount=bundle.Metrics.AnimationCount,
                        editableSource=true,sourcePath=$"{Library}/objects/{hash}/model.blend",previewPath=$"{Library}/objects/{hash}/preview.png",
                        reviewState="unreviewed_local_recipe",visualReviewed=false,rightsReviewed=false,placementApproved=false,
                        sourceVisualReview=visual.State,sourceRightsReview=rights.State,
                        sourcePromotionState=await PromotionStateAsync(bundle,identity,visual,rights,token),
                        technicalState="exact_bundle_and_recipe_verified_target_unity_inspection_required",
                        provenance=$"forgecore://local-blender/{hash}",licenseState="original_recipe_generated_review_required"
                    });
                }
                catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
                {rejected++;}
            }
        }
        return JsonSerializer.Serialize(new {schemaVersion=1,library="local_blender_candidates",scannedEntries=scanned,rejectedEntries=rejected,
            truncated=truncated||candidates.Count>spec.MaxResults,verificationBytesCharged=verifiedBytes,
            qualification="Local source candidates; source review/promotion status is reported separately. No target Unity import, visual/rights approval, placement, animation compatibility or performance acceptance is implied.",
            results=candidates.Take(spec.MaxResults)},Json);
    }
}
