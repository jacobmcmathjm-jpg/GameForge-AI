using System.Text.Json;
using ForgeCore.Core;
namespace ForgeCore.Tools;
public sealed partial class ToolExecutor
{
    private async Task<string> WriteUnityCharacterAnimationEvidenceAsync(UnityEditorActionSpec spec,string projectRoot,string requestId,CancellationToken token)
    {
        if(!await UnityGameTemplateKit.InstallationMatchesAsync(projectRoot,token))
            throw new UnauthorizedAccessException("The animation kit changed during execution; evidence is blocked.");
        var kitManifest=ResolveUserPath(Path.Combine(spec.ProjectPath,"Assets/ForgeCoreGameTemplates/forgecore-game-templates.manifest.json"),false);
        var relative="Assets/ForgeCore/CharacterAnimations/"+spec.AssetSetId;
        var folder=ResolveUserPath(Path.Combine(spec.ProjectPath,relative),false);EnsureWithinProject(projectRoot,folder);
        var reportPath=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"animation-report.json"),false);
        using var json=JsonDocument.Parse(await File.ReadAllBytesAsync(reportPath,token));var r=json.RootElement;
        var names=new[]{"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"};
        if(r.GetProperty("schemaVersion").GetInt32()!=1 || r.GetProperty("recipe").GetString()!=ForgeCore.Motion.GuardMotionProfile.Recipe ||
            r.GetProperty("assetSha256").GetString()!=spec.ExpectedSha256 || r.GetProperty("clipCount").GetInt32()!=8 ||
            r.GetProperty("rootMotionPolicy").GetString()!="runtime_translation_in_place_clips" || !r.GetProperty("finite").GetBoolean() ||
            !r.GetProperty("clips").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(names))throw new InvalidDataException("Animation report identity failed.");
        foreach(var field in new[]{"maximumEdgeStretch","maximumLoopError","maximumStanceSlip"})
        {var value=r.GetProperty(field).GetDouble();if(!double.IsFinite(value)||value<0)throw new InvalidDataException("Animation measurement is invalid.");}
        if(r.GetProperty("maximumLoopError").GetDouble()>.002)throw new InvalidDataException("Animation loop seam exceeds 2mm.");
        if(r.GetProperty("maximumEdgeStretch").GetDouble() is < 1 or > 3.5 || r.GetProperty("maximumStanceSlip").GetDouble()>.01)
            throw new InvalidDataException("Sampled animation deformation or authored stance-target error exceeds its technical limit; this is separate from blended foot-contact acceptance.");
        var content=names.Select(x=>x+".anim").Concat(new[]{"Mannequin.controller","animation-report.json"}).ToArray();
        JsonElement? visualReport=null,graphicsReport=null,propPresentationReport=null,heroFinishReport=null,retroReport=null,indieEnvironmentReport=null,indieWeaponReport=null;
        if(spec.Operation is "create_security_combat_lab" or "create_industrial_mission")
        {
            using var combat=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"combat-report.json"),token));
            var c=combat.RootElement;
            visualReport=c.Clone();
            if(c.GetProperty("schemaVersion").GetInt32()!=1||c.GetProperty("recipe").GetString()!="security_combat_lab_v1"||
                c.GetProperty("assetSha256").GetString()!=spec.ExpectedSha256||c.GetProperty("assetPath").GetString()!=spec.AssetPath||
                c.GetProperty("enemies").GetInt32()!=3||c.GetProperty("gridWidth").GetInt32()!=32||c.GetProperty("gridHeight").GetInt32()!=32||
                c.GetProperty("armsTriangles").GetInt32()<100||c.GetProperty("armsTriangles").GetInt32()>=c.GetProperty("sourceTriangles").GetInt32()||
                c.GetProperty("visualReviewed").GetBoolean()||c.GetProperty("rightsReviewed").GetBoolean()||c.GetProperty("stageComplete").GetBoolean())
                throw new InvalidDataException("Combat fixture report identity/budget failed; no human acceptance is inferred.");
            if(c.GetProperty("visualProfile").GetString()!="industrial_range_finish_v1"||
                !c.GetProperty("visualAssets").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(UnityGameTemplateKit.SecurityVisualArtifacts)||
                c.GetProperty("textureSize").GetInt32()!=256||c.GetProperty("ownedTextureCount").GetInt32()!=7||c.GetProperty("reflectionFaceSize").GetInt32()!=64||
                c.GetProperty("materialSlotCount").GetInt32() is < 1 or > 16||
                c.GetProperty("ownedTextureMemoryBytes").GetInt64() is <= 0 or > 16*1024*1024)
                throw new InvalidDataException("Visual profile artifact identity/resource limits failed; this is not whole-game memory or visual acceptance.");
            foreach(var texture in UnityGameTemplateKit.SecurityVisualArtifacts.Where(x=>x.EndsWith(".png",StringComparison.Ordinal)))
            {
                var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,texture),false);
                _=await StagedAssetInspector.InspectPngAsync(file,token);
                await using var textureStream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);
                var header=new byte[24];await textureStream.ReadExactlyAsync(header,token);
                if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=256||
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=256)
                    throw new InvalidDataException("Owned visual texture dimensions differ from the bounded profile.");
            }
            content=content.Concat(new[]{"FirstPersonArms.asset","RifleMetal.mat","RiflePolymer.mat","RifleTrim.mat","Concrete.mat","CombatLab.prefab","combat-report.json"}).ToArray();
            content=content.Concat(UnityGameTemplateKit.SecurityVisualArtifacts).ToArray();
        }
        var expected=content.Concat(content.Select(x=>x+".meta")).ToHashSet(StringComparer.Ordinal);
        if(spec.Operation=="create_industrial_mission")
        {
            using var mission=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"mission-report.json"),token));var m=mission.RootElement;
            if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("profile").GetString()!=IndustrialAssetPlan.MissionProfile||
                m.GetProperty("assetMode").GetString()!="explicit_greybox_pending_approved_industrial_assets"||
                m.GetProperty("objectiveCount").GetInt32()!=3||m.GetProperty("encounterCount").GetInt32()!=2||m.GetProperty("guardCount").GetInt32()!=3||
                m.GetProperty("greyboxPlacementCount").GetInt32()!=10||!m.GetProperty("spawnSafe").GetBoolean()||!m.GetProperty("routeReachable").GetBoolean()||
                !m.GetProperty("physicalPowerBox").GetBoolean()||!m.GetProperty("persistentPowerBox").GetBoolean()||m.GetProperty("powerPlaceholderSphere").GetBoolean()||
                m.GetProperty("physicalTerminalCount").GetInt32()!=3||m.GetProperty("objectivePlaceholderSpheres").GetInt32()!=0||
                m.GetProperty("approvedAssetsPlaced").GetBoolean()||m.GetProperty("stageComplete").GetBoolean()||m.GetProperty("visualReviewed").GetBoolean()||m.GetProperty("rightsReviewed").GetBoolean()||
                !m.GetProperty("objectiveOrder").EnumerateArray().Select(e=>e.GetString()).SequenceEqual(new[]{"restore_power","retrieve_manifest","extract"})||
                !double.IsFinite(m.GetProperty("minimumRouteMetres").GetDouble())||m.GetProperty("minimumRouteMetres").GetDouble()<45)
                throw new InvalidDataException("Industrial mission design/route report failed its explicit greybox contract.");
            foreach(var name in new[]{"IndustrialMission.prefab","mission-report.json","MissionObjective.mat"})
            {expected.Add(name);expected.Add(name+".meta");}
            using var audio=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"demo-audio-report.json"),token));var sound=audio.RootElement;
            if(sound.GetProperty("schemaVersion").GetInt32()!=3||sound.GetProperty("sampleRate").GetInt32()!=48000||sound.GetProperty("channels").GetInt32()!=1||
                sound.GetProperty("bits").GetInt32()!=16||sound.GetProperty("recipe").GetString()!=ForgeCore.Demo.DemoAudioSynthesis.Recipe||sound.GetProperty("humanListeningAccepted").GetBoolean()||
                sound.GetProperty("variantsPerSurface").GetInt32()!=4||sound.GetProperty("clips").GetArrayLength()!=UnityGameTemplateKit.DemoAudioKeys.Length||
                sound.GetProperty("recordedVariants").GetInt32()!=6||sound.GetProperty("recordedBankId").GetString()!=ForgeCore.Demo.RecordedFootstepBank.Id||
                sound.GetProperty("processingRecipe").GetString()!=ForgeCore.Demo.RecordedFootstepBank.ProcessingRecipe||sound.GetProperty("creator").GetString()!=ForgeCore.Demo.RecordedFootstepBank.Creator||
                sound.GetProperty("license").GetString()!=ForgeCore.Demo.RecordedFootstepBank.License||sound.GetProperty("sourcePage").GetString()!=ForgeCore.Demo.RecordedFootstepBank.SourcePage)throw new InvalidDataException("Audio source/recipe identity failed.");
            long pcmBytes=0;
            for(var i=0;i<UnityGameTemplateKit.DemoAudioKeys.Length;i++) {
                var name=UnityGameTemplateKit.DemoAudioArtifacts[i];var entry=sound.GetProperty("clips")[i];
                var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);var bytes=await File.ReadAllBytesAsync(file,token);
                var sourceIndex=i-ForgeCore.Demo.DemoAudioSynthesis.ClipKeys.Length;var recorded=sourceIndex>=0;
                var samples=recorded?ForgeCore.Demo.RecordedFootstepBank.SourceFrames[sourceIndex]:ForgeCore.Demo.DemoAudioSynthesis.Generate(ForgeCore.Demo.DemoAudioSynthesis.ClipKeys[i]).Length;pcmBytes+=bytes.Length;
                if(entry.GetProperty("sourceKind").GetString()!=(recorded?"recorded":"procedural")||
                    entry.GetProperty("sourceFile").GetString()!=(recorded?ForgeCore.Demo.RecordedFootstepBank.SourceFile(sourceIndex):"")||
                    entry.GetProperty("sourceSha256").GetString()!=(recorded?ForgeCore.Demo.RecordedFootstepBank.SourceHashes[sourceIndex]:""))throw new InvalidDataException("Audio derivative source identity failed.");
                if(entry.GetProperty("file").GetString()!=name||entry.GetProperty("samples").GetInt32()!=samples||entry.GetProperty("bytes").GetInt32()!=44+samples*2||
                    bytes.Length!=44+samples*2||entry.GetProperty("sha256").GetString()!=await HashFileAsync(file,token)||
                    System.Text.Encoding.ASCII.GetString(bytes,0,4)!="RIFF"||System.Text.Encoding.ASCII.GetString(bytes,8,8)!="WAVEfmt "||
                    BitConverter.ToInt16(bytes,20)!=1||BitConverter.ToInt16(bytes,22)!=1||BitConverter.ToInt32(bytes,24)!=48000||BitConverter.ToInt16(bytes,34)!=16||
                    System.Text.Encoding.ASCII.GetString(bytes,36,4)!="data"||BitConverter.ToInt32(bytes,40)!=samples*2)
                    throw new InvalidDataException("Original audio WAV identity/budget failed.");
            }
            for(var i=0;i<6;i++)if(!ForgeCore.Demo.RecordedFootstepBank.MatchesSource(i,await File.ReadAllBytesAsync(Path.Combine(folder,ForgeCore.Demo.RecordedFootstepBank.SourceFile(i)),token)))
                throw new InvalidDataException("Recorded audio source changed after decoding.");
            if(pcmBytes>2*1024*1024)throw new InvalidDataException("Audio bank exceeds its 2 MiB PCM budget.");
            foreach(var name in UnityGameTemplateKit.DemoAudioArtifacts){expected.Add(name);expected.Add(name+".meta");}
            using var courtyard=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"courtyard-report.json"),token));var yard=courtyard.RootElement;
            if(yard.GetProperty("schemaVersion").GetInt32()!=1||yard.GetProperty("profile").GetString()!="industrial_courtyard_presentation_v1"||
                yard.GetProperty("source").GetString()!="original_deterministic_template_geometry_and_surfaces"||
                !yard.GetProperty("collisionUnchanged").GetBoolean()||yard.GetProperty("decorativeColliderCount").GetInt32()!=0||
                yard.GetProperty("textureSize").GetInt32()!=512||yard.GetProperty("ownedTextureCount").GetInt32()!=6||
                yard.GetProperty("decorativeTriangles").GetInt32() is <=0 or >100000||yard.GetProperty("renderers").GetInt32() is <=0 or >40||
                yard.GetProperty("lightCount").GetInt32()!=4||yard.GetProperty("ownedTextureMemoryBytes").GetInt64() is <=0 or >32*1024*1024||
                yard.GetProperty("approvedLibraryAssetsPlaced").GetBoolean()||yard.GetProperty("visualReviewed").GetBoolean()||yard.GetProperty("rightsReviewed").GetBoolean()||yard.GetProperty("stageComplete").GetBoolean())
                throw new InvalidDataException("Courtyard presentation report failed; no library/art approval is inferred.");
            foreach(var name in UnityGameTemplateKit.CourtyardArtifacts){expected.Add(name);expected.Add(name+".meta");}
            foreach(var name in UnityGameTemplateKit.CourtyardArtifacts.Where(n=>n.EndsWith(".png",StringComparison.Ordinal))) {
                var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);_=await StagedAssetInspector.InspectPngAsync(file,token);
                await using var textureStream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var header=new byte[24];await textureStream.ReadExactlyAsync(header,token);
                if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=512||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=512)
                    throw new InvalidDataException("Courtyard texture size differs from its fixed profile.");
            }
        }
        if(spec.Operation=="create_industrial_mission")
        {
            using var graphics=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder,"graphics-report.json"),token));
            CourtyardGraphicsEvidence.Validate(graphics.RootElement);
            graphicsReport=graphics.RootElement.Clone();
            foreach(var name in UnityGameTemplateKit.CourtyardGraphicsArtifacts){expected.Add(name);expected.Add(name+".meta");}
            foreach(var name in UnityGameTemplateKit.CourtyardGraphicsArtifacts.Where(n=>n.EndsWith(".png",StringComparison.Ordinal)))
            {
                var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);_=await StagedAssetInspector.InspectPngAsync(file,token);
                await using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var header=new byte[24];await stream.ReadExactlyAsync(header,token);
                var comparison=name=="graphics-comparison.png";
                if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=(comparison?1920:1024)||
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=(comparison?1620:1024))
                    throw new InvalidDataException("Graphics benchmark artifact dimensions failed.");
            }
        }
        var payload=new List<object>();
        if(spec.Operation=="create_industrial_mission")
        {
            var propReportPath=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"prop-presentation-report.json"),false);
            if(new FileInfo(propReportPath).Length>32768)throw new InvalidDataException("Template prop report oversized.");
            using var props=JsonDocument.Parse(await File.ReadAllBytesAsync(propReportPath,token));CourtyardPropEvidence.Validate(props.RootElement);propPresentationReport=props.RootElement.Clone();
            foreach(var name in UnityGameTemplateKit.CourtyardPropArtifacts){expected.Add(name);expected.Add(name+".meta");}
            foreach(var name in UnityGameTemplateKit.CourtyardPropArtifacts.Where(n=>n.EndsWith(".png",StringComparison.Ordinal))) {
                var path=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);_=await StagedAssetInspector.InspectPngAsync(path,token);
                await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var header=new byte[24];await input.ReadExactlyAsync(header,token);var comparison=name=="prop-comparison.png";
                if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=(comparison?1440:1024)||
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=(comparison?810:1024))throw new InvalidDataException("Template prop image dimensions failed.");
            }
        }
        foreach(var name in expected)
        {var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);if(!File.Exists(file)||new FileInfo(file).Length==0)throw new InvalidDataException("Missing animation artifact.");payload.Add(new{path=relative+"/"+name,sha256=await HashFileAsync(file,token),bytes=new FileInfo(file).Length});}
        var scene=ResolveUserPath(Path.Combine(spec.ProjectPath,spec.ScenePath!),false);
        if(spec.Operation=="create_industrial_mission"&&spec.StyleFamily!=RetroCourtyardProfile.Id){
            var path=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"indie-environment-report.json"),false);
            if(new FileInfo(path).Length>32768)throw new InvalidDataException("Indie environment report oversized.");
            using var report=JsonDocument.Parse(await File.ReadAllBytesAsync(path,token));IndieEnvironmentEvidence.Validate(report.RootElement);indieEnvironmentReport=report.RootElement.Clone();
            foreach(var name in UnityGameTemplateKit.IndieEnvironmentArtifacts)foreach(var item in new[]{name,name+".meta"}){
                var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,item),false);if(new FileInfo(file).Length is <=0 or >10*1024*1024)throw new InvalidDataException("Indie environment artifact missing/oversized.");
                payload.Add(new{path=relative+"/"+item,sha256=await HashFileAsync(file,token),bytes=new FileInfo(file).Length});
            }
            var skyPath=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"IndieSky.png"),false);_=await StagedAssetInspector.InspectPngAsync(skyPath,token);
            var weaponPath=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"indie-weapon-report.json"),false);if(new FileInfo(weaponPath).Length>32768)throw new InvalidDataException("Rifle report oversized.");using var weapon=JsonDocument.Parse(await File.ReadAllBytesAsync(weaponPath,token));IndieWeaponEvidence.Validate(weapon.RootElement);indieWeaponReport=weapon.RootElement.Clone();
            foreach(var name in UnityGameTemplateKit.IndieWeaponArtifacts)foreach(var item in new[]{name,name+".meta"}){var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,item),false);if(new FileInfo(file).Length is <=0 or >10*1024*1024)throw new InvalidDataException("Rifle artifact missing/oversized.");payload.Add(new{path=relative+"/"+item,sha256=await HashFileAsync(file,token),bytes=new FileInfo(file).Length});}
            var sky=await File.ReadAllBytesAsync(skyPath,token);if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(sky.AsSpan(16,4))!=1024||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(sky.AsSpan(20,4))!=512)throw new InvalidDataException("Indie sky dimensions differ.");
        }
        if(spec.Operation=="create_industrial_mission"&&spec.StyleFamily==RetroCourtyardProfile.Id)
        {
            var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"retro64-report.json"),false);
            if(new FileInfo(file).Length>32768)throw new InvalidDataException("Retro report exceeds bounds.");
            using var retro=JsonDocument.Parse(await File.ReadAllBytesAsync(file,token));RetroCourtyardProfile.Validate(retro.RootElement);retroReport=retro.RootElement.Clone();
            if(!IndustrialPlacementAudit.Assess(RetroCourtyardProfile.Placements).NominalLayoutClear)throw new InvalidDataException("Retro nominal placement audit failed.");
            foreach(var name in UnityGameTemplateKit.RetroArtifacts)foreach(var item in new[]{name,name+".meta"}) {
                var path=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,item),false);if(new FileInfo(path).Length is <=0 or >10*1024*1024)throw new InvalidDataException("Retro artifact missing or oversized.");
                payload.Add(new{path=relative+"/"+item,sha256=await HashFileAsync(path,token),bytes=new FileInfo(path).Length});
                if(item.EndsWith(".png",StringComparison.Ordinal)){_=await StagedAssetInspector.InspectPngAsync(path,token);await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var header=new byte[24];await stream.ReadExactlyAsync(header,token);
                    var width=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4));var height=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4));if(width is <32 or >128||width!=height)throw new InvalidDataException("Retro art texture dimensions invalid.");}
            }
        }
        if(spec.Operation=="create_industrial_mission")
        {
            var heroPath=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,"visual-polish-report.json"),false);
            if(new FileInfo(heroPath).Length>32768)throw new InvalidDataException("Hero finish report oversized.");
            using var hero=JsonDocument.Parse(await File.ReadAllBytesAsync(heroPath,token));CourtyardHeroEvidence.Validate(hero.RootElement);heroFinishReport=hero.RootElement.Clone();
            foreach(var name in UnityGameTemplateKit.VisualPolishArtifacts) {
                foreach(var item in new[]{name,name+".meta"}) {
                    var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,item),false);
                    if(new FileInfo(file).Length is <=0 or >100*1024*1024)throw new InvalidDataException("Missing or oversized hero finish artifact.");
                    payload.Add(new{path=relative+"/"+item,sha256=await HashFileAsync(file,token),bytes=new FileInfo(file).Length});
                }
                if(name.EndsWith(".png",StringComparison.Ordinal)) {
                    var file=ResolveUserPath(Path.Combine(spec.ProjectPath,relative,name),false);_=await StagedAssetInspector.InspectPngAsync(file,token);
                    await using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read);var header=new byte[24];await stream.ReadExactlyAsync(header,token);
                    if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16,4))!=1024||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20,4))!=1024)throw new InvalidDataException("Hero map dimensions differ.");
                }
            }
        }
        var png=ResolveUserPath(Path.Combine(spec.ProjectPath,spec.OutputPath!),false);
        if(!File.Exists(scene)||new FileInfo(scene).Length==0)throw new InvalidDataException("Animation lab scene missing.");
        _=await StagedAssetInspector.InspectPngAsync(png,token);
        var evidence=new{schemaVersion=1,requestId,operation=spec.Operation,assetPath=spec.AssetPath,assetSha256=spec.ExpectedSha256,
            bridgeVersion=UnityBridgeTemplate.BridgeVersion,bridgeSha256=UnityBridgeTemplate.Sha256,templateKitVersion=UnityGameTemplateKit.Version,templateKitManifestSha256=await HashFileAsync(kitManifest,token),
            scenePath=spec.ScenePath,sceneSha256=await HashFileAsync(scene,token),previewPath=spec.OutputPath,previewSha256=await HashFileAsync(png,token),
            artifacts=payload,report=r.Clone(),visualReport,graphicsReport,propPresentationReport,heroFinishReport,retroReport,indieEnvironmentReport,indieWeaponReport,selectedStyle=spec.StyleFamily??"default",visualReviewed=false,rightsReviewed=false,stageComplete=false,
            qualification="Animation lab foundation only. Pose samples do not prove transition quality, foot contact, complete gameplay integration or human acceptance.",createdUtc=DateTimeOffset.UtcNow};
        var dir=paths.Resolve(Path.Combine(spec.ProjectPath,".forgecore","unity","character-animation-evidence"),true);Directory.CreateDirectory(dir);
        var output=Path.Combine(dir,requestId+".json");await WriteJsonNewAsync(output,evidence,token);await WriteJsonNewAsync(png+".forgecore-rig-preview.json",evidence,token);
        _ = await TryRecordUnityLearningAsync(spec,output,requestId,"motion",token);
        return output;
    }
}
