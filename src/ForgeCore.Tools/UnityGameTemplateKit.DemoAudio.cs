namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] DemoAudioKeys=ForgeCore.Demo.DemoAudioSynthesis.ClipKeys.Concat(ForgeCore.Demo.RecordedFootstepBank.Keys).ToArray();
    internal static readonly string[] DemoAudioArtifacts=DemoAudioKeys.Select(k=>"Demo-"+k+".wav")
        .Concat(Enumerable.Range(0,6).Select(ForgeCore.Demo.RecordedFootstepBank.SourceFile)).Append("demo-audio-report.json").ToArray();
    private static string RecordedFootstepPackSource {
        get {using var stream=typeof(UnityGameTemplateKit).Assembly.GetManifestResourceStream("ForgeCore.Tools.RecordedFootsteps.json")
            ?? throw new InvalidOperationException("Bundled recording source/rights manifest missing.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    }
    private const string DemoAudioSurfaceSource="""
using UnityEngine;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates
{
    // Explicit generated surface metadata; collision mesh names/material guesses do not determine sound.
    public sealed class DemoAudioSurface:MonoBehaviour
    {
        public DemoFootstepSurface surface=DemoFootstepSurface.Concrete;
        public static DemoFootstepSurface Under(Vector3 feet) {
            RaycastHit hit;if(Physics.Raycast(feet+Vector3.up*.25f,Vector3.down,out hit,.65f,1<<8,QueryTriggerInteraction.Ignore)) {
                var tag=hit.collider.GetComponentInParent<DemoAudioSurface>();if(tag!=null&&(int)tag.surface>=0&&(int)tag.surface<=4)return tag.surface;
            }
            return DemoFootstepSurface.Concrete;
        }
    }
}
""";
    private const string DemoAudioSource="""
using System;
using UnityEngine;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates
{
    public sealed class DemoAudioController:MonoBehaviour
    {
        public SecurityCombatSession session;
        public AudioClip[] clips;
        public AudioClip[] variationClips;
        public string[] variationKeys;
        public string audioRecipe;
        public string recordedFootstepBankId;
        public int RecordedFootstepEvents {get;private set;}
        public string LastClipKey {get;private set;}
        public int FootstepEvents {get;private set;}
        public int Events {get;private set;}
        public int Dropped {get;private set;}
        public int ActiveVoices {get{var n=0;if(voices!=null)foreach(var voice in voices)if(voice.isPlaying)n++;return n;}}
        private AudioSource[] voices;private AudioSource ambient;private int cursor;
        private readonly string[] voiceKinds=new string[16];private readonly float[] voiceLevels=new float[16];
        private bool paused;private float captionUntil;private string caption="";
        private Vector3 lastPlayer;private bool wasReloading;private int shotSerial;
        private readonly DemoFootstepClock playerSteps=new DemoFootstepClock();
        private Vector3[] lastGuards;private DemoFootstepClock[] guardSteps;private CharacterController[] guardControllers;
        public void Initialize() {
            if(voices!=null)return;voices=new AudioSource[16];
            for(var i=0;i<voices.Length;i++){
                var go=new GameObject("Bounded effect voice "+i);go.transform.SetParent(transform,false);
                voices[i]=go.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].dopplerLevel=0;
                voices[i].mute=Application.isEditor||DemoRuntimeScope.Diagnostic;
                voices[i].rolloffMode=AudioRolloffMode.Logarithmic;voices[i].minDistance=2;voices[i].maxDistance=70;
                go.AddComponent<AudioLowPassFilter>().cutoffFrequency=22000;
            }
            ambient=gameObject.AddComponent<AudioSource>();ambient.playOnAwake=false;ambient.loop=true;ambient.spatialBlend=0;ambient.volume=.18f;
            ambient.mute=Application.isEditor||DemoRuntimeScope.Diagnostic;
            if(clips!=null&&clips.Length==6){ambient.clip=clips[5];ambient.Play();}
            if(paused)ambient.Pause();
            ApplyMix();
            lastPlayer=session.player.transform.position;lastGuards=new Vector3[session.enemies.Length];guardSteps=new DemoFootstepClock[lastGuards.Length];guardControllers=new CharacterController[lastGuards.Length];
            for(var i=0;i<lastGuards.Length;i++){lastGuards[i]=session.enemies[i].transform.position;guardSteps[i]=new DemoFootstepClock();guardControllers[i]=session.enemies[i].GetComponent<CharacterController>();}
        }
        private void Start(){Initialize();SetPaused(session.Paused);}
        private void Update() {
            if(voices==null)Initialize();
            var p=session.player.transform.position;var delta=p-lastPlayer;delta.y=0;lastPlayer=p;
            var stride=session.player.IsSprinting?2.2f:session.player.AimBlend>.5f?1.2f:1.7f;
            if(playerSteps.Advance(delta.magnitude,Mathf.Min(1,Time.deltaTime),session.player.IsGrounded,!session.Paused&&session.player.Health>0,stride))PlayFootstep(p,false,playerSteps.Steps);
            if(!session.Paused&&session.player.Health>0){
                var reload=session.player.motion.Action=="Reload";if(reload&&!wasReloading)Play("reload",p,false);wasReloading=reload;
            }
            for(var i=0;i<lastGuards.Length;i++) {
                var guard=session.enemies[i];var gp=guard.transform.position;var movement=gp-lastGuards[i];movement.y=0;lastGuards[i]=gp;
                var moving=!session.Paused&&guard.gameObject.activeInHierarchy&&guard.Health>0;
                if(guardSteps[i].Advance(movement.magnitude,Mathf.Min(1,Time.deltaTime),guardControllers[i].enabled&&guardControllers[i].isGrounded,moving,.85f))PlayFootstep(gp,true,guardSteps[i].Steps);
            }
        }
        public bool PlayFootstep(Vector3 feet,bool spatial,int stepNumber) {
            var surface=DemoAudioSurface.Under(feet);
            if(recordedFootstepBankId==RecordedFootstepBank.Id&&(surface==DemoFootstepSurface.Asphalt||surface==DemoFootstepSurface.Concrete))
                return PlayKey("footstep",RecordedFootstepBank.Key(surface,stepNumber),feet,spatial);
            var variant=new[]{0,2,1,3}[Math.Abs(stepNumber%4)];
            return PlayKey("footstep",DemoAudioSynthesis.Key("footstep",surface,variant),feet,spatial);
        }
        public bool Play(string kind,Vector3 position,bool spatial=true) {
            Initialize();var index=Array.IndexOf(DemoAudioSynthesis.Kinds,kind);
            if(paused||index<0||index>=5||clips==null||clips.Length!=6||clips[index]==null)return false;
            if(kind=="footstep")return PlayFootstep(position,spatial,FootstepEvents);
            var key=kind=="shot"?DemoAudioSynthesis.Key(kind,DemoFootstepSurface.Asphalt,shotSerial++%4):kind=="footstep"?DemoAudioSynthesis.Key(kind,DemoAudioSurface.Under(position),FootstepEvents%4):kind;
            return PlayKey(kind,key,position,spatial);
        }
        private bool PlayKey(string kind,string key,Vector3 position,bool spatial) {
            Initialize();var index=Array.IndexOf(DemoAudioSynthesis.Kinds,kind);if(paused||index<0||index>=5)return false;
            var variation=variationKeys!=null?Array.IndexOf(variationKeys,key):-1;
            var recorded=key.StartsWith("recorded-hardfloor-",StringComparison.Ordinal);
            if(recorded&&(recordedFootstepBankId!=RecordedFootstepBank.Id||variation<0||variationClips==null||variation>=variationClips.Length||variationClips[variation]==null)){Dropped++;return false;}
            var clip=variation>=0&&variationClips!=null&&variation<variationClips.Length?variationClips[variation]:clips!=null&&clips.Length==6?clips[index]:null;
            if(clip==null)return false;
            AudioSource selected=null;var selectedIndex=-1;
            for(var offset=0;offset<voices.Length;offset++){var n=(cursor+offset)%voices.Length;if(!voices[n].isPlaying){selected=voices[n];selectedIndex=n;cursor=(n+1)%voices.Length;break;}}
            if(selected==null){Dropped++;return false;}
            selected.transform.position=position;selected.clip=clip;selected.spatialBlend=spatial?1:0;
            selected.minDistance=kind=="shot"?3:1;selected.maxDistance=kind=="shot"?70:kind=="footstep"?15:24;
            selected.priority=kind=="shot"?64:kind=="footstep"?160:128;
            var distance=Vector3.Distance(session.player.view.position,position);
            selected.GetComponent<AudioLowPassFilter>().cutoffFrequency=spatial?Mathf.Lerp(18000,2500,Mathf.InverseLerp(4,60,distance)):22000;
            voiceKinds[selectedIndex]=kind;voiceLevels[selectedIndex]=kind=="shot"?.75f:kind=="footstep"?.34f:.4f;
            selected.volume=voiceLevels[selectedIndex]*DemoStateRules.AudioGain(session.options!=null?session.options.Settings:null,kind);
            selected.pitch=1;selected.Play();Events++;LastClipKey=key;if(kind=="footstep")FootstepEvents++;if(recorded)RecordedFootstepEvents++;
            if(kind!="footstep"){caption=kind=="shot"?"[Gunfire]":kind=="reload"?"[Reload]":kind=="impact"?"[Impact]":"[Objective updated]";captionUntil=Time.unscaledTime+1.1f;}
            return true;
        }
        public void SetPaused(bool value) {
            if(voices==null){paused=value;return;}if(paused==value)return;paused=value;
            foreach(var source in voices){if(value)source.Pause();else source.UnPause();}
            if(value)ambient.Pause();else ambient.UnPause();
        }
        public void ApplyMix() {
            if(voices==null)return;var settings=session.options!=null?session.options.Settings:null;
            if(ambient!=null)ambient.volume=.18f*DemoStateRules.AudioGain(settings,"ambient");
            for(var i=0;i<voices.Length;i++)voices[i].volume=voiceLevels[i]*DemoStateRules.AudioGain(settings,voiceKinds[i]);
        }
        private void OnGUI(){if(session.options!=null&&session.options.Settings.audioCaptions&&!session.Paused&&Time.unscaledTime<captionUntil)
            GUI.Box(new Rect(Screen.width/2-100,Screen.height-112,200,30),caption);}
    }
}
""";
    private const string DemoAudioBuilderSource="""
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreDemoAudioBuilder
    {
        [Serializable]private sealed class SourceEntry{public string file,sha256,base64;public int frames;}
        [Serializable]private sealed class Pack{public int schemaVersion;public string id,creator,license,sourcePage,processingRecipe;public bool humanListeningAccepted;public SourceEntry[] files;}
        [Serializable]private sealed class Entry{public string file,sha256,sourceKind,sourceFile,sourceSha256;public int samples,bytes;}
        [Serializable]private sealed class Report{
            public int schemaVersion=3,sampleRate=48000,channels=1,bits=16,variantsPerSurface=4,recordedVariants=6;
            public string recipe=DemoAudioSynthesis.Recipe,recordedBankId=RecordedFootstepBank.Id,processingRecipe=RecordedFootstepBank.ProcessingRecipe;
            public string creator=RecordedFootstepBank.Creator,license=RecordedFootstepBank.License,sourcePage=RecordedFootstepBank.SourcePage;
            public string surfaceMapping="Asphalt and concrete use a recorded subway hard-floor candidate; metal/gravel/wood retain procedural candidates.";
            public bool humanListeningAccepted=false;public Entry[] clips;
        }
        public static void Build(DemoAudioController target,string folder) {
            var report=new Report();var keys=DemoAudioSynthesis.ClipKeys.Concat(RecordedFootstepBank.Keys).ToArray();report.clips=new Entry[keys.Length];target.clips=new AudioClip[6];
            target.variationKeys=(string[])keys.Clone();target.variationClips=new AudioClip[keys.Length];target.audioRecipe=DemoAudioSynthesis.Recipe;target.recordedFootstepBankId=RecordedFootstepBank.Id;
            var recordings=DecodeSources(folder);
            for(var i=0;i<keys.Length;i++){
                var recorded=i>=DemoAudioSynthesis.ClipKeys.Length;var sourceIndex=i-DemoAudioSynthesis.ClipKeys.Length;
                var data=recorded?recordings[sourceIndex]:DemoAudioSynthesis.Generate(keys[i]);var name="Demo-"+keys[i]+".wav";var path=folder+"/"+name;
                using(var stream=new FileStream(path,FileMode.CreateNew))using(var writer=new BinaryWriter(stream)){
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(DemoAudioSynthesis.SampleRate);writer.Write(DemoAudioSynthesis.SampleRate*2);
                    writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*2);
                    foreach(var value in data)writer.Write((short)Math.Round(Math.Max(-1,Math.Min(1,value))*32767));
                }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
                target.variationClips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(target.variationClips[i]==null)throw new IOException("Audio import failed.");if(i<6)target.clips[i]=target.variationClips[i];
                var bytes=File.ReadAllBytes(path);using(var hash=SHA256.Create())report.clips[i]=new Entry{file=name,samples=data.Length,bytes=bytes.Length,
                    sourceKind=recorded?"recorded":"procedural",sourceFile=recorded?RecordedFootstepBank.SourceFile(sourceIndex):"",sourceSha256=recorded?RecordedFootstepBank.SourceHashes[sourceIndex]:"",
                    sha256=string.Concat(hash.ComputeHash(bytes).Select(b=>b.ToString("x2")))};
            }
            using(var stream=new FileStream(folder+"/demo-audio-report.json",FileMode.CreateNew))using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(report,true));
        }
        private static float[][] DecodeSources(string folder) {
            var json=File.ReadAllText("Assets/ForgeCoreGameTemplates/Editor/RecordedFootsteps.json");if(json.Length>160000)throw new IOException("Recording pack exceeds bound.");
            var pack=JsonUtility.FromJson<Pack>(json);
            if(pack==null||pack.schemaVersion!=1||pack.id!=RecordedFootstepBank.Id||pack.creator!=RecordedFootstepBank.Creator||pack.license!=RecordedFootstepBank.License||
                pack.sourcePage!=RecordedFootstepBank.SourcePage||pack.processingRecipe!=RecordedFootstepBank.ProcessingRecipe||pack.humanListeningAccepted||pack.files==null||pack.files.Length!=6)throw new IOException("Recording source/rights contract changed.");
            // Validate every exact source before creating any imported source file. No arbitrary path or network retrieval.
            var bytes=new byte[6][];
            for(var i=0;i<6;i++){
                var entry=pack.files[i];if(entry.file!=RecordedFootstepBank.SourceFile(i)||entry.sha256!=RecordedFootstepBank.SourceHashes[i]||entry.frames!=RecordedFootstepBank.SourceFrames[i]||entry.base64==null||entry.base64.Length>180000)throw new IOException("Recording identity changed.");
                bytes[i]=Convert.FromBase64String(entry.base64);if(!RecordedFootstepBank.MatchesSource(i,bytes[i]))throw new IOException("Recorded source hash mismatch.");
            }
            var data=new float[6][];
            for(var i=0;i<6;i++){
                var path=folder+"/"+RecordedFootstepBank.SourceFile(i);using(var output=new FileStream(path,FileMode.CreateNew))output.Write(bytes[i],0,bytes[i].Length);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(clip==null||clip.channels!=1||clip.frequency!=48000||clip.samples!=RecordedFootstepBank.SourceFrames[i])throw new IOException("Recording decoder format/duration mismatch.");
                clip.LoadAudioData();var decoded=new float[clip.samples];if(!clip.GetData(decoded,0))throw new IOException("Recording decoder returned no PCM.");
                data[i]=RecordedFootstepBank.Process(i,decoded,clip.frequency,clip.channels);
            }
            return data;
        }
    }
}
""";
}
