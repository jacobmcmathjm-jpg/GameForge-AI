using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.Tools;
public sealed partial class ToolExecutor
{
    private sealed record IndustrialInputState(Dictionary<string,string> Hashes,Dictionary<string,string> MetaBackups);
    private async Task<IndustrialInputState> CaptureIndustrialInputStateAsync(UnityEditorActionSpec spec,string requestId,CancellationToken token)
    {
        var hashes=await _blenderAssets.VerifyIndustrialImportAsync(spec.ProjectPath,spec.AssetPath!,spec.ExpectedSha256!,token);
        var backups=new Dictionary<string,string>(StringComparer.Ordinal);
        var folder=paths.Resolve(Path.Combine(spec.ProjectPath,".forgecore/unity/industrial-input-backups",requestId),true);Directory.CreateDirectory(folder);
        foreach(var entry in hashes.Where(e=>e.Key.EndsWith(".meta",StringComparison.Ordinal))) {
            var backup=Path.Combine(folder,Path.GetFileName(entry.Key)+".bak");File.Copy(entry.Key,backup,false);backups.Add(entry.Key,backup);
        }
        return new(hashes,backups);
    }
    private async Task VerifyIndustrialInputStateAsync(IndustrialInputState state,CancellationToken token)
    {
        var changed=false;
        foreach(var entry in state.Hashes) {
            if(File.Exists(entry.Key)&&await HashFileAsync(entry.Key,token)==entry.Value)continue;
            changed=true;
            if(state.MetaBackups.TryGetValue(entry.Key,out var backup)) {
                if(await HashFileAsync(backup,token)!=entry.Value)throw new InvalidDataException("Industrial metadata backup changed.");
                File.Copy(backup,entry.Key,true);
            }
        }
        if(changed)throw new InvalidDataException("Industrial imported inputs changed during Unity execution; metadata restored, evidence blocked.");
    }
    private async Task<string> WriteIndustrialPropEvidenceAsync(UnityEditorActionSpec spec,string projectRoot,string requestId,CancellationToken token)
    {
        if(!await UnityGameTemplateKit.InstallationMatchesAsync(projectRoot,token))throw new InvalidDataException("Industrial kit changed.");
        await _blenderAssets.VerifyIndustrialImportAsync(spec.ProjectPath,spec.AssetPath!,spec.ExpectedSha256!,token);
        var folder="Assets/ForgeCore/IndustrialProps/"+spec.AssetSetId;
        var reportFile=ResolveUserPath(Path.Combine(spec.ProjectPath,folder,"prop-report.json"),false);
        if(new FileInfo(reportFile).Length>16384)throw new InvalidDataException("Industrial report exceeds bounds.");
        using var document=JsonDocument.Parse(await File.ReadAllBytesAsync(reportFile,token));var r=document.RootElement;
        if(r.GetProperty("schemaVersion").GetInt32()!=1||r.GetProperty("assetPath").GetString()!=spec.AssetPath||
            r.GetProperty("assetSha256").GetString()!=spec.ExpectedSha256||!IndustrialPropRecipes.Contains(r.GetProperty("recipe").GetString()!)||
            r.GetProperty("lodLevels").GetInt32()!=3||r.GetProperty("colliderCount").GetInt32()!=1||
            r.GetProperty("missingResources").GetInt32()!=0||!r.GetProperty("uvFinite").GetBoolean()||!r.GetProperty("rootIdentity").GetBoolean()||
            r.GetProperty("visualReviewed").GetBoolean()||r.GetProperty("rightsReviewed").GetBoolean()||r.GetProperty("placementApproved").GetBoolean())
            throw new InvalidDataException("Industrial target report failed its structural identity gates.");
        var counts=r.GetProperty("triangles").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(counts.Length!=3||counts.Any(n=>n is <12 or >16000)||counts[0]<=counts[1]||counts[1]<=counts[2])throw new InvalidDataException("Industrial target LOD budgets/order failed.");
        var payload=new List<object>();
        foreach(var name in new[]{"IndustrialProp.prefab","prop-report.json","ReviewGround.mat","ReviewSky.mat"}.SelectMany(n=>new[]{n,n+".meta"})) {
            var path=ResolveUserPath(Path.Combine(spec.ProjectPath,folder,name),false);
            if(new FileInfo(path).Length is <1 or >100*1024*1024)throw new InvalidDataException("Industrial target artifact missing/oversized.");
            payload.Add(new{path=folder+"/"+name,sha256=await HashFileAsync(path,token)});
        }
        var png=ResolveUserPath(Path.Combine(spec.ProjectPath,spec.OutputPath!),false);_=await StagedAssetInspector.InspectPngAsync(png,token);
        var evidence=new{schemaVersion=1,requestId,operation=spec.Operation,assetPath=spec.AssetPath,assetSha256=spec.ExpectedSha256,
            kitVersion=UnityGameTemplateKit.Version,bridgeVersion=UnityBridgeTemplate.BridgeVersion,report=r.Clone(),artifacts=payload,
            scenePath=spec.ScenePath,sceneSha256=await HashFileAsync(ResolveUserPath(Path.Combine(spec.ProjectPath,spec.ScenePath!),false),token),
            previewPath=spec.OutputPath,previewSha256=await HashFileAsync(png,token),visualReviewed=false,rightsReviewed=false,placementApproved=false};
        var output=paths.Resolve(Path.Combine(spec.ProjectPath,".forgecore/unity/industrial-prop-evidence",requestId+".json"),true);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);await WriteJsonNewAsync(output,evidence,token);
        await WriteJsonNewAsync(png+".forgecore-rig-preview.json",evidence,token);return output;
    }
}
