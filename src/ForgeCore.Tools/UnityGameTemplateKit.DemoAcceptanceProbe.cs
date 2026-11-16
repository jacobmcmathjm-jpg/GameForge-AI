namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string DemoAcceptanceProbeSource="""
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates
{
    // Explicit opt-in owned test, not a normal player bot, source approval or a human playtest.
    public sealed class DemoAcceptanceProbe:MonoBehaviour
    {
        [Serializable]private sealed class Pass {
            public int preset,health,shots,hostileShots,kills,audioEvents,droppedVoices,footsteps,recordedFootsteps;
            public bool completed,checkpointRoundtrip;public float travelled,seconds;
            public long allocatedStart,allocatedEnd,reservedEnd;public DemoFrameSummary frames;
            public DemoWorkSnapshot work;public DemoRuntimeTelemetry.Spike[] spikes;
            public float idleSeconds;public DemoFrameSummary idleFrames;
        }
        [Serializable]private sealed class Report {
            public string profile="three_preset_actual_controller_projectile_mission_v1",device,error="",storageScope;
            public string gpu,cpu;public int width,height;public double unityReadyMilliseconds;
            public string audioRecipe,recordedFootstepBankId;public int audioClips;
            public string activity="startup";public Vector3 playerPosition,targetPosition,waypoint;public int stage,kills,ammo,reserve,blockedShots;
            public bool viewClear,muzzleBlocked;
            public bool muted=true,directDamageUsed=false,playerTeleportUsed=false,humanArtAccepted=false,humanListeningAccepted=false;
            public DemoTimingReport timingDiagnostics;
            public List<Pass> passes=new List<Pass>();
        }
        private Report report=new Report();private string folder;private SecurityCombatSession session;private DemoRuntimeTelemetry telemetry;
        private int shots,hostileShots;private float travelled,deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap() {
            if(!Environment.GetCommandLineArgs().Contains("-forgecore-demo-probe"))return;
            Application.runInBackground=true;new GameObject("Owned bounded complete-demo acceptance").AddComponent<DemoAcceptanceProbe>();
        }
        private IEnumerator Start() {
            folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ForgeCoreDemoAcceptance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            report.device=SystemInfo.graphicsDeviceType.ToString();deadline=Time.realtimeSinceStartup+240;
            var routines=new Stack<IEnumerator>();routines.Push(Exercise());
            while(routines.Count>0){bool more=false;object current=null;string error=null;
                try{more=routines.Peek().MoveNext();if(more)current=routines.Peek().Current;}catch(Exception ex){error=ex.ToString();}
                if(error!=null){Finish(error,2);yield break;}if(!more){routines.Pop();continue;}
                if(current is IEnumerator nested){routines.Push(nested);continue;}yield return current;
            }
            Finish("",0);
        }
        private IEnumerator Exercise() {
            yield return null;yield return null;
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Real graphics required.");
            session=SecurityCombatSession.Instance;if(session==null||session.options==null||session.audio==null)throw new InvalidOperationException("Fresh complete-demo fixture required.");
            session.enabled=false;session.player.enabled=false;session.mission.enabled=false;session.options.enabled=false;
            telemetry=session.options.GetComponent<DemoRuntimeTelemetry>();Require(telemetry!=null&&telemetry.Session==session,"Telemetry root/session wiring missing");
            report.audioRecipe=session.audio.audioRecipe;report.audioClips=session.audio.variationClips!=null?session.audio.variationClips.Length:0;
            report.recordedFootstepBankId=session.audio.recordedFootstepBankId;
            Require(report.audioRecipe==DemoAudioSynthesis.Recipe&&report.audioClips==DemoAudioSynthesis.ClipKeys.Length+RecordedFootstepBank.Keys.Length&&report.recordedFootstepBankId==RecordedFootstepBank.Id,"Fresh source-bound audio recipe/variations required");
            report.gpu=SystemInfo.graphicsDeviceName;report.cpu=SystemInfo.processorType;report.width=Screen.width;report.height=Screen.height;
            report.unityReadyMilliseconds=telemetry.ReadyMilliseconds;session.player.motion.ShotEmitted+=(p,d)=>shots++;
            foreach(var guard in session.enemies)guard.motion.ShotEmitted+=(p,d)=>hostileShots++;
            for(var preset=0;preset<3;preset++) {
                var result=new Pass{preset=preset};report.passes.Add(result);var began=Time.realtimeSinceStartup;shots=hostileShots=0;travelled=0;
                var stepsAtStart=session.audio.FootstepEvents;
                var recordedStepsAtStart=session.audio.RecordedFootstepEvents;
                if(!session.options.SetSettings(new DemoSettings{quality=preset,frameLimit=60,masterVolume=0},true))throw new IOException("Probe settings save failed.");
                session.options.StartNewGame();telemetry.ResetMeasurement();result.allocatedStart=telemetry.AllocatedBytes;
                yield return Walk(session.mission.objectives[0].position);Require(session.mission.TryInteract(),"Power interaction failed");
                yield return Fight();
                Require(session.Kills==2,"First encounter not cleared");
                var save=session.options.RequestCheckpoint("Owned probe checkpoint");
                while(!save.IsCompleted){Check();yield return null;}Require(save.Result,"Checkpoint save failed");
                var health=session.player.Health;var ammo=session.player.motion.Ammo;var reserve=session.player.Reserve;
                Require(session.options.TryLoadCheckpoint(),"Checkpoint restore failed");
                result.checkpointRoundtrip=session.Kills==2&&session.mission.Stage==1&&session.player.Health==health&&session.player.motion.Ammo==ammo&&session.player.Reserve==reserve;
                Require(result.checkpointRoundtrip,"Checkpoint altered mission/vitals/ammo");
                yield return Walk(session.mission.objectives[1].position);Require(session.mission.TryInteract(),"Manifest interaction failed");
                yield return Fight();yield return Walk(session.mission.objectives[2].position);Require(session.mission.TryInteract(),"Extraction failed");
                result.completed=session.mission.Completed;result.health=session.player.Health;result.kills=session.Kills;result.shots=shots;
                result.hostileShots=hostileShots;result.travelled=travelled;result.seconds=Time.realtimeSinceStartup-began;
                result.frames=telemetry.Snapshot();result.allocatedEnd=telemetry.AllocatedBytes;result.reservedEnd=telemetry.ReservedBytes;
                result.work=telemetry.WorkSnapshot();result.spikes=telemetry.Spikes();
                result.audioEvents=session.audio.Events;result.droppedVoices=session.audio.Dropped;
                result.footsteps=session.audio.FootstepEvents-stepsAtStart;
                Require(result.footsteps>15,"Actual grounded movement did not emit footstep events");
                result.recordedFootsteps=session.audio.RecordedFootstepEvents-recordedStepsAtStart;
                Require(result.recordedFootsteps>15,"Actual grounded movement did not select recorded footsteps");
                Require(result.completed&&result.kills==3&&result.shots>=9&&result.travelled>45,"Actual mission evidence incomplete");
                Capture("preset-"+preset+"-completed.png");
                // Additional bounded warm idle soak, explicitly excluded from the combat percentile above.
                session.options.StartNewGame();telemetry.ResetMeasurement("idle");var idleStart=Time.realtimeSinceStartup;
                var until=idleStart+(Environment.GetCommandLineArgs().Contains("-forgecore-soak-30")?30:8);
                while(Time.realtimeSinceStartup<until){Check();session.player.Simulate(new PrototypeInputFrame(),false,Dt());yield return null;}
                result.idleSeconds=Time.realtimeSinceStartup-idleStart;result.idleFrames=telemetry.Snapshot();
            }
            report.storageScope=session.options.StoreRoot;
            Require(!session.options.AutosavePending&&session.options.CheckpointCompletion.IsCompleted&&session.options.CheckpointCompletion.Result,"All scripted checkpoints must be durable by the end of the soak");
            Require(report.storageScope.Contains("ForgeCoreDemoProbe-"),"Probe touched normal user save scope");
        }
        private IEnumerator Fight() {
            var path=new List<Vector3>();var next=0;SecurityCombatEnemy previous=null;var encounterDeadline=Time.realtimeSinceStartup+45;
            var lastHealth=-1;var lastDamage=Time.realtimeSinceStartup;
            while(session.enemies.Any(e=>e.gameObject.activeInHierarchy&&e.Health>0)) {
                Check();var target=session.enemies.Where(e=>e.gameObject.activeInHierarchy&&e.Health>0)
                    .OrderBy(e=>Vector3.Distance(e.transform.position,session.player.transform.position)).First();
                Require(Time.realtimeSinceStartup<encounterDeadline,"45s encounter deadline; inspect final position/LOS/muzzle/ammo diagnostics");
                if(previous!=target){previous=target;path.Clear();next=0;lastHealth=target.Health;lastDamage=Time.realtimeSinceStartup;}
                if(target.Health!=lastHealth){lastHealth=target.Health;lastDamage=Time.realtimeSinceStartup;}
                var point=target.transform.position+Vector3.up*1.05f;
                var canShoot=SecurityCombatBullet.FirstHitIsTarget(session.player.view.position,point,session.player.transform,target.transform);
                var muzzleBlocked=SecurityCombatBullet.Blocked(session.player.view.position,session.player.motion.muzzle.position,session.player.transform);
                report.activity="fight";report.targetPosition=target.transform.position;report.viewClear=canShoot;report.muzzleBlocked=muzzleBlocked;
                var move=Vector3.zero;
                var reposition=!canShoot||muzzleBlocked||Time.realtimeSinceStartup-lastDamage>4;
                if(reposition){
                    while(next<path.Count&&Flat(path[next]-session.player.transform.position).magnitude<.16f)next++;
                    if(next>=path.Count){path=FiringRoute(target);next=0;Require(path.Count>0,"No reachable clear firing position");lastDamage=Time.realtimeSinceStartup;}
                    report.waypoint=path[next];move=path[next]-session.player.transform.position;
                }else{path.Clear();next=0;}
                Drive(move,point,canShoot&&!muzzleBlocked&&!reposition);yield return null;
            }
        }
        private List<Vector3> FiringRoute(SecurityCombatEnemy target) {
            List<Vector3> best=null;var score=float.PositiveInfinity;var eyeTarget=target.transform.position+Vector3.up*1.05f;
            foreach(var radius in new[]{4f,7f,10f})for(var angle=0;angle<8;angle++){
                var a=angle*Mathf.PI/4;var candidate=target.transform.position+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius;
                candidate.x=Mathf.Round(candidate.x+.5f)-.5f;candidate.z=Mathf.Round(candidate.z+.5f)-.5f;candidate.y=0;
                if(Flat(candidate-session.player.transform.position).magnitude<1.5f||Physics.Linecast(candidate+Vector3.up*1.55f,eyeTarget,1<<8,QueryTriggerInteraction.Ignore))continue;
                var route=session.Navigation.Find(session.player.transform.position,candidate);var cost=route.Count+radius*.05f;
                if(route.Count>0&&cost<score){score=cost;best=route;}
            }
            return best??new List<Vector3>();
        }
        private IEnumerator Walk(Vector3 destination) {
            report.activity="objective approach";
            var route=session.Navigation.Find(session.player.transform.position,destination);Require(route.Count>0,"No static route");
            foreach(var point in route){var until=Time.realtimeSinceStartup+4;
                while(Flat(point-session.player.transform.position).magnitude>.16f) {
                    Check();Require(Time.realtimeSinceStartup<until,"Controller stuck at "+point+" from "+session.player.transform.position);
                    report.waypoint=point;Drive(point-session.player.transform.position,null,false);yield return null;
                }
            }
        }
        private void Drive(Vector3 worldMove,Vector3? target,bool fire) {
            var player=session.player;var sensitivity=session.options.Settings.sensitivity;var lookX=0f;var lookY=0f;
            if(target.HasValue){var d=target.Value-player.view.position;var desiredYaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
                var desiredPitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
                lookX=Mathf.DeltaAngle(player.transform.eulerAngles.y,desiredYaw)/sensitivity;lookY=(player.ViewPitch-desiredPitch)/sensitivity;}
            worldMove=Flat(worldMove);var direction=worldMove.sqrMagnitude>.01f?worldMove.normalized:Vector3.zero;
            var local=player.transform.InverseTransformDirection(direction);var before=player.transform.position;
            var aligned=target.HasValue&&Vector3.Angle(player.view.forward,target.Value-player.view.position)<2;
            player.Simulate(new PrototypeInputFrame(moveX:local.x,moveY:local.z,lookX:lookX,lookY:lookY,fireHeld:fire&&aligned,
                reloadPressed:player.motion.Ammo==0&&player.motion.Action!="Reload"),fire,Dt());
            travelled+=Vector3.Distance(before,player.transform.position);
        }
        private void Check(){report.playerPosition=session.player.transform.position;report.stage=session.mission.Stage;report.kills=session.Kills;
            report.ammo=session.player.motion.Ammo;report.reserve=session.player.Reserve;report.blockedShots=session.ShotsBlockedByCover;
            Require(Time.realtimeSinceStartup<deadline,"240s bounded probe deadline");Require(session.player.Health>0,"Actual combat killed probe player");}
        private static Vector3 Flat(Vector3 value){value.y=0;return value;}
        private static float Dt()=>Mathf.Clamp(Time.deltaTime,.001f,.1f);
        private static void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        private void Capture(string name) {
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.ProbeCapture))CaptureCore(name);
        }
        private void CaptureCore(string name) {
            var camera=session.player.view.GetComponent<Camera>();var old=camera.targetTexture;var previous=RenderTexture.active;
            var rt=new RenderTexture(960,540,24);var png=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,960,540),0,0);png.Apply();
                using(var file=new FileStream(Path.Combine(folder,name),FileMode.CreateNew)){var bytes=png.EncodeToPNG();file.Write(bytes,0,bytes.Length);}}
            finally{camera.targetTexture=old;RenderTexture.active=previous;Destroy(rt);Destroy(png);}
        }
        private void Finish(string error,int code){report.error=error;if(telemetry!=null)report.timingDiagnostics=telemetry.TimingSnapshot();
            if(session!=null&&report.passes.Count>0){var pass=report.passes[report.passes.Count-1];if(!pass.completed){pass.health=session.player.Health;pass.kills=session.Kills;pass.shots=shots;pass.hostileShots=hostileShots;pass.travelled=travelled;
                if(telemetry!=null){pass.frames=telemetry.Snapshot();pass.allocatedEnd=telemetry.AllocatedBytes;}}
                if(code!=0)try{Capture("failure.png");}catch{} }
            using(var stream=new FileStream(Path.Combine(folder,"report.json"),FileMode.CreateNew))using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(report,true));Application.Quit(code);}
    }
}
#endif
""";
}
