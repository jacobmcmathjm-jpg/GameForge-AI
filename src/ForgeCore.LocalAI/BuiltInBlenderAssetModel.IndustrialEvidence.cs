using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.LocalAI;
public sealed partial class BuiltInBlenderAssetModel
{
    // Defensive observation validation only. The executor still recomputes current source/review selection before any write.
    private static IndustrialPlacementAuditResult ValidateIndustrialObservation(JsonElement root,string recipeSet)
    {
        var manifest=root.GetProperty("manifest");
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.GetRawText()))).ToLowerInvariant();
        if(root.GetProperty("planSha256").GetString()!=hash||manifest.GetProperty("schemaVersion").GetInt32()!=(recipeSet=="legacy_v1"?2:3)||
            manifest.GetProperty("layoutVersion").GetString()!=IndustrialPlacementLayout.VersionFor(recipeSet))throw new JsonException();
        var slots=manifest.GetProperty("slots").EnumerateArray().ToArray();var required=IndustrialAssetPlan.SlotsFor(recipeSet);
        if(slots.Length!=required.Count)throw new JsonException();
        var allReviewed=true;
        for(var i=0;i<slots.Length;i++) {
            var slot=slots[i];var expected=required[i];
            if(slot.GetProperty("slotId").GetString()!=expected.Id||slot.GetProperty("recipe").GetString()!=expected.Recipe||slot.GetProperty("count").GetInt32()!=expected.Count)throw new JsonException();
            var reviewed=slot.GetProperty("state").GetString()=="exact_current_human_reviewed_source";
            foreach(var name in new[]{"bundleSha256","assetSha256","promotionSha256"}) {
                var value=slot.GetProperty(name);
                if(reviewed){var text=value.GetString();if(text is null||text.Length!=64||text.Any(c=>c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))throw new JsonException();}
                else if(value.ValueKind!=JsonValueKind.Null)throw new JsonException();
            }
            allReviewed&=reviewed;
        }
        if(manifest.GetProperty("readyForTargetPreparation").GetBoolean()!=allReviewed)throw new JsonException();
        var placements=manifest.GetProperty("placements").EnumerateArray().ToArray();var layout=IndustrialPlacementLayout.For(recipeSet);
        if(placements.Length!=layout.Count)throw new JsonException();
        for(var i=0;i<placements.Length;i++) {
            var p=placements[i];var f=p.GetProperty("footprint");var expected=layout[i];
            var actual=new IndustrialPlacementFootprint(f.GetProperty("placementId").GetString()!,f.GetProperty("slotId").GetString()!,f.GetProperty("recipe").GetString()!,
                f.GetProperty("x").GetDouble(),f.GetProperty("y").GetDouble(),f.GetProperty("z").GetDouble(),f.GetProperty("yawDegrees").GetDouble(),
                f.GetProperty("width").GetDouble(),f.GetProperty("height").GetDouble(),f.GetProperty("depth").GetDouble());
            if(actual!=expected||f.GetProperty("uniformScale").GetDouble()!=1||f.GetProperty("pivot").GetString()!="bottom_center")throw new JsonException();
            var source=slots.Single(s=>s.GetProperty("slotId").GetString()==expected.SlotId);
            if(p.GetProperty("bundleSha256").GetString()!=source.GetProperty("bundleSha256").GetString()||
                p.GetProperty("assetSha256").GetString()!=source.GetProperty("assetSha256").GetString())throw new JsonException();
        }
        return IndustrialPlacementAudit.Assess(layout);
    }
}
