namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private static string SharedDemoSource(string name)
    {
        using var stream=typeof(UnityGameTemplateKit).Assembly.GetManifestResourceStream("ForgeCore.Tools."+name+".cs")
            ??throw new InvalidOperationException("Shared demo source missing.");
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
    private const string DemoOptionsSource = """
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates
{
    public static class DemoRuntimeScope
    {
        private static string diagnosticRoot;
        public static bool Diagnostic {
            get {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                return Environment.GetCommandLineArgs().Any(s=>s.StartsWith("-forgecore-",StringComparison.Ordinal)&&s.EndsWith("-probe",StringComparison.Ordinal));
#else
                return false;
#endif
            }
        }
        public static string StorageRoot()=>Application.isEditor?
            Path.Combine(Application.temporaryCachePath,"ForgeCoreDemoTests-"+Guid.NewGuid().ToString("N")):
            Diagnostic?(diagnosticRoot??(diagnosticRoot=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ForgeCoreDemoProbe-"+Guid.NewGuid().ToString("N")))):
            Path.Combine(Application.persistentDataPath,"ForgeCoreIndustrialDemo-v1");
    }
    public sealed class SecurityDemoOptions:MonoBehaviour
    {
        public SecurityCombatSession session;
        public DemoSettings Settings {get;private set;}=new DemoSettings();
        public bool Intro {get;private set;}=true;
        public string Status {get;private set;}="";
        public string StoreRoot {get;private set;}
        public bool HasCheckpoint=>StoreRoot!=null&&File.Exists(Path.Combine(StoreRoot,"checkpoint.json"));
        private bool settingsOpen,initialized;
        private DemoSettings draft;
        private Vector2 menuScroll;
        private DemoCheckpointQueue saves;private Task<bool> latestSave;private string saveReason;
        public bool AutosavePending=>saves!=null&&saves.Pending;
        private void Start(){Initialize();if(!Application.isEditor&&!DemoRuntimeScope.Diagnostic)session.SetPaused(true);}
        public void Initialize() {
            if(initialized)return;initialized=true;StoreRoot=DemoRuntimeScope.StorageRoot();
            var root=StoreRoot;saves=new DemoCheckpointQueue((text,measured)=>DemoAtomicStateFiles.TryWrite(root,"checkpoint.json",text,measured,out _));
            try {var json=ReadOwned("settings.json");if(json!=null){var value=DemoStateRules.UpgradeSettings(JsonUtility.FromJson<DemoSettings>(json));if(DemoStateRules.SettingsValid(value))Settings=value;else Status="Invalid settings; safe defaults used.";}}
            catch{Status="Settings unreadable; safe defaults used.";}
            ApplySettings();
        }
        private void Update() {
            PublishSaveStatus();
            if(Intro&&!settingsOpen&&Input.GetKeyDown(KeyCode.Return))StartNewGame();
            if(!Intro&&Input.GetKeyDown(KeyCode.F9))TryLoadCheckpoint();
        }
        private void OnApplicationFocus(bool focused){if(!focused&&!Application.isBatchMode&&!Application.isEditor&&!DemoRuntimeScope.Diagnostic&&session!=null)session.SetPaused(true);}
        public bool SetSettings(DemoSettings value,bool persist) {
            Initialize();if(!DemoStateRules.SettingsValid(value)){Status="Settings values rejected.";return false;}
            if(persist&&!WriteOwned("settings.json",JsonUtility.ToJson(value,true)))return false;
            Settings=value;ApplySettings();return true;
        }
        private void ApplySettings() {
            if(!Application.isEditor)AudioListener.volume=DemoRuntimeScope.Diagnostic?0:Settings.masterVolume;
            if(session!=null&&session.player!=null)session.player.SetHipFieldOfView(Settings.fieldOfView);
            if(session!=null&&session.audio!=null)session.audio.ApplyMix();
            if(!Application.isEditor)DemoQuality.Apply(Settings.quality,Settings.frameLimit);
        }
        public void StartNewGame() {
            Initialize();Intro=false;settingsOpen=false;session.Restart();SaveCheckpoint("New mission checkpoint");session.SetPaused(false);
        }
        public bool SaveCheckpoint(string reason="Checkpoint saved") {
            var pending=RequestCheckpoint(reason);var ok=pending.GetAwaiter().GetResult();PublishSaveStatus();return ok;
        }
        public Task<bool> RequestCheckpoint(string reason="Checkpoint saved") {
            Initialize();if(Intro||session==null||session.mission==null||session.Finished||session.player.Health<=0)return Task.FromResult(false);
            var player=session.player;player.CommitCompletedReload();var p=player.transform.position;var mask=0;
            for(var i=0;i<session.enemies.Length;i++)if(session.enemies[i].Health<=0)mask|=1<<i;
            var data=new DemoCheckpoint{schemaVersion=1,profile=DemoStateRules.Profile,stage=session.mission.Stage,defeatedMask=mask,
                health=player.Health,reserve=player.Reserve,magazine=player.motion.Ammo,x=p.x,y=Mathf.Max(0,p.y),z=p.z,yaw=Mathf.Repeat(player.transform.eulerAngles.y,360),pitch=player.ViewPitch};
            if(!DemoStateRules.CheckpointValid(data)){Status="Not a valid checkpoint state.";return Task.FromResult(false);}
            var request=saves.Request(JsonUtility.ToJson(data,true),DemoRuntimeTelemetry.BackgroundRecorder());latestSave=request;saveReason=reason;Status="Saving checkpoint...";
            PublishSaveStatus();return request;
        }
        private void PublishSaveStatus() {
            if(latestSave==null||!latestSave.IsCompleted)return;
            Status=latestSave.Result?saveReason:"Save failed; previous checkpoint kept. Check retained .new/storage and restart autosave.";latestSave=null;
        }
        private void OnApplicationQuit() {
            if(saves!=null&&saves.Pending&&!saves.Completion.Wait(2000))Status="Exit save still pending; previous durable checkpoint retained.";
        }
        public bool TryLoadCheckpoint() {
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.CheckpointRestore))return LoadCheckpointCore();
        }
        private bool LoadCheckpointCore() {
            Initialize();DemoCheckpoint data;
            // Explicit load waits for the ordered queue; automatic gameplay events never wait for disk I/O.
            if(saves.Pending)saves.Completion.GetAwaiter().GetResult();PublishSaveStatus();
            try{var json=ReadOwned("checkpoint.json");if(json==null){Status="No checkpoint saved yet.";return false;}data=JsonUtility.FromJson<DemoCheckpoint>(json);}
            catch{Status="Checkpoint unreadable; current mission unchanged.";return false;}
            if(!DemoStateRules.CheckpointValid(data)){Status="Checkpoint rejected; current mission unchanged.";return false;}
            var p=new Vector3(data.x,data.y,data.z);Physics.SyncTransforms();
            if(Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.45f,.25f,1<<8,QueryTriggerInteraction.Ignore)){
                Status="Checkpoint location blocked; current mission unchanged.";return false;}
            session.Restart();session.mission.RestoreCheckpoint(data.stage,data.defeatedMask);
            session.player.RestoreCheckpoint(data.health,data.reserve,data.magazine,p,data.yaw,data.pitch);
            Intro=false;settingsOpen=false;session.SetPaused(false);Status="Checkpoint loaded (not an exact physics/animation snapshot).";return true;
        }
        private string ReadOwned(string name) {
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.StateRead))return ReadOwnedCore(name);
        }
        private string ReadOwnedCore(string name) {
            var path=OwnedPath(name);if(!File.Exists(path))return null;
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>65536)throw new IOException("Unsafe/oversized demo state.");
            return File.ReadAllText(path,Encoding.UTF8);
        }
        private bool WriteOwned(string name,string text) {
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.StateWrite))return WriteOwnedCore(name,text);
        }
        private bool WriteOwnedCore(string name,string text) {
            try {
                if(Encoding.UTF8.GetByteCount(text)>65536)throw new IOException("State budget.");
                var path=OwnedPath(name);Directory.CreateDirectory(StoreRoot);
                if((File.GetAttributes(StoreRoot)&FileAttributes.ReparsePoint)!=0)throw new IOException("State root link blocked.");
                var temporary=path+".new";var backup=path+".previous";
                foreach(var target in new[]{path,backup,temporary})if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("State link blocked.");
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    var bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes,0,bytes.Length);
                    using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.StateFlush))file.Flush(true);
                }
                using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.StateReplace)) {
                    if(File.Exists(path))File.Replace(temporary,path,backup);else File.Move(temporary,path);
                }
                return true;
            }catch{Status="Save failed; previous checkpoint/settings kept. Check storage and retained .new file.";return false;}
        }
        private string OwnedPath(string name) {
            if(name!="settings.json"&&name!="checkpoint.json")throw new ArgumentException("Unknown demo state file.");
            var root=Path.GetFullPath(StoreRoot);
            if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Demo state root is a link.");
            return Path.Combine(root,name);
        }
        public void DrawMenu() {
            Initialize();var width=Mathf.Min(560,Screen.width-24);var height=Mathf.Min(580,Screen.height-24);
            var r=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);GUI.Box(r,Intro?"INDUSTRIAL INFILTRATION — demo":"Paused");
            GUILayout.BeginArea(new Rect(r.x+18,r.y+32,r.width-36,r.height-48));
            menuScroll=GUILayout.BeginScrollView(menuScroll);
            if(settingsOpen) {
                GUILayout.Label("Keyboard/mouse settings — gamepad support not implemented");
                draft.sensitivity=Slider("Mouse sensitivity",draft.sensitivity,.2f,5);draft.fieldOfView=Slider("Field of view",draft.fieldOfView,60,100);
                draft.masterVolume=Slider("Master volume",draft.masterVolume,0,1);
                draft.effectsVolume=Slider("Gunshots / impacts / reload",draft.effectsVolume,0,1);
                draft.footstepsVolume=Slider("Footsteps",draft.footstepsVolume,0,1);
                draft.ambienceVolume=Slider("Ambience",draft.ambienceVolume,0,1);
                draft.interfaceVolume=Slider("Objective / interface",draft.interfaceVolume,0,1);
                draft.invertY=GUILayout.Toggle(draft.invertY,"Invert mouse Y");draft.largeText=GUILayout.Toggle(draft.largeText,"Larger HUD text");
                draft.audioCaptions=GUILayout.Toggle(draft.audioCaptions,"Gameplay sound captions (no dialogue)");
                GUILayout.Label("Graphics: Low / Medium / High");draft.quality=GUILayout.SelectionGrid(draft.quality,new[]{"Low","Medium","High"},3);
                GUILayout.Label("Frame cap");var cap=draft.frameLimit==30?0:draft.frameLimit==60?1:2;cap=GUILayout.SelectionGrid(cap,new[]{"30","60","120"},3);draft.frameLimit=cap==0?30:cap==1?60:120;
                if(GUILayout.Button("Apply and save settings",GUILayout.Height(32))&&SetSettings(draft,true))settingsOpen=false;
                if(GUILayout.Button("Back",GUILayout.Height(30)))settingsOpen=false;
            }else {
                if(session.Finished)GUILayout.Label(session.player.Health<=0?"Mission failed — load a checkpoint or start again.":"Extraction complete — mission accomplished.");
                if(!Intro&&!session.Finished&&GUILayout.Button("Resume",GUILayout.Height(36)))session.SetPaused(false);
                if(GUILayout.Button("New game / replace checkpoint",GUILayout.Height(36)))StartNewGame();
                GUI.enabled=HasCheckpoint;if(GUILayout.Button("Continue saved checkpoint (F9)",GUILayout.Height(36)))TryLoadCheckpoint();GUI.enabled=true;
                if(!Intro&&!session.Finished&&GUILayout.Button("Save checkpoint",GUILayout.Height(32)))SaveCheckpoint();
                if(GUILayout.Button("Settings",GUILayout.Height(32))){draft=JsonUtility.FromJson<DemoSettings>(JsonUtility.ToJson(Settings));settingsOpen=true;}
                if(GUILayout.Button("Quit",GUILayout.Height(32)))Application.Quit();
                GUILayout.Label("WASD · mouse look · Shift sprint · Space jump · RMB sights · R reload · E interact · Esc pause · F5 restart · F6 metrics");
                GUILayout.Label("Checkpoint restores objective progress, defeated guards, vitals, ammo and position; not exact animation/projectile state.");
            }
            GUILayout.Label(Status);GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private static float Slider(string label,float value,float min,float max){GUILayout.Label(label+": "+value.ToString("0.00"));return GUILayout.HorizontalSlider(value,min,max);}
    }
    public static class DemoQuality
    {
        public static void Apply(int preset,int frameLimit) {
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.GraphicsSettings))ApplyCore(preset,frameLimit);
        }
        private static void ApplyCore(int preset,int frameLimit) {
            if(preset<0||preset>2)return;
            QualitySettings.shadows=preset==0?ShadowQuality.Disable:preset==1?ShadowQuality.HardOnly:ShadowQuality.All;
            QualitySettings.shadowResolution=preset==2?ShadowResolution.High:ShadowResolution.Low;
            QualitySettings.shadowDistance=preset==0?0:preset==1?35:65;QualitySettings.antiAliasing=preset==2?4:preset==1?2:0;
            QualitySettings.pixelLightCount=preset==0?1:preset==1?2:4;QualitySettings.globalTextureMipmapLimit=preset==0?1:0;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=frameLimit;
        }
    }
}
""";
}
