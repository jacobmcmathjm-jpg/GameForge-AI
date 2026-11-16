using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.Tools;
public sealed partial class ToolExecutor
{
    private async Task<string> WriteUnityCharacterRigEvidenceAsync(UnityEditorActionSpec spec,string projectRoot,
        CharacterRigEvidence? rig,string requestId,CancellationToken token)
    {
        if(rig is null)throw new InvalidDataException("Unity returned no character rig evidence.");
        rig.Validate(spec.ExpectedSha256!);
        var scene=ResolveUserPath(Path.Combine(spec.ProjectPath,spec.ScenePath!),false);
        var preview=ResolveUserPath(Path.Combine(spec.ProjectPath,spec.OutputPath!),false);
        EnsureWithinProject(projectRoot,scene);EnsureWithinProject(projectRoot,preview);
        if(!File.Exists(scene) || new FileInfo(scene).Length<1)throw new InvalidDataException("Character review scene was not saved.");
        _=await StagedAssetInspector.InspectPngAsync(preview,token);
        var evidence=new
        {
            schemaVersion=1,requestId,operation=spec.Operation,editorVersion=_unityEditorVersion,
            bridgeVersion=UnityBridgeTemplate.BridgeVersion,bridgeSha256=UnityBridgeTemplate.Sha256,
            assetPath=spec.AssetPath,assetSha256=spec.ExpectedSha256,scenePath=spec.ScenePath,
            sceneSha256=await HashFileAsync(scene,token),previewPath=spec.OutputPath,previewSha256=await HashFileAsync(preview,token),
            technicalReady=true,visualReviewed=false,rightsReviewed=false,rig,
            qualification="Bounded original named-bone technical mannequin; not detailed character art, animation-set, retargeting or gameplay acceptance.",
            createdUtc=DateTimeOffset.UtcNow
        };
        var directory=paths.Resolve(Path.Combine(spec.ProjectPath,".forgecore","unity","character-rig-evidence"),true);
        Directory.CreateDirectory(directory);
        var evidencePath=Path.Combine(directory,requestId+".json");
        await WriteJsonNewAsync(evidencePath,evidence,token);
        await WriteJsonNewAsync(preview+".forgecore-rig-preview.json",evidence,token);
        _ = await TryRecordUnityLearningAsync(spec,evidencePath,requestId,"rig",token);
        return evidencePath;
    }
}
