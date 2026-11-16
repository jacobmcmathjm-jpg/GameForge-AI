namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string CombatRenderProbeSource = """
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    // Opt-in, bounded standalone-player diagnostic. No external paths or normal-play input automation.
    public sealed class CombatRenderProbe:MonoBehaviour
    {
        [Serializable]public sealed class Sample
        {
            public string name;public int warmPixels,magentaPixels,flashes,projectiles,particles;
            public string[] materials;
        }
        [Serializable]public sealed class Report
        {
            public string kind="standalone_actual_gameplay_render",device,scene,error,guardDiagnostic;
            public bool actualShotMarker,actualGuardShotMarker;public List<Sample> samples=new List<Sample>();
            public bool physicalPowerBox,powerBoxPersistent,powerBoxReset,powerPlaceholderAbsent;
            public List<CorpseSample> corpseSamples=new List<CorpseSample>();
        }
        [Serializable]public sealed class CorpseSample
        {
            public float time,minY,floorY,torsoMinY,visualY,pelvisY,cachedGroundError;public string lowestBone;
        }
        private readonly Report report=new Report();private string folder;private bool captured;private string shotName;
        private SecurityCombatPlayer player;private Camera camera;private bool corpseMode,courtyardMode,powerBoxMode;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();var corpse=args.Contains("-forgecore-corpse-probe");
            var courtyard=args.Contains("-forgecore-courtyard-probe");
            var power=args.Contains("-forgecore-powerbox-probe");
            if(!corpse&&!courtyard&&!power&&!args.Contains("-forgecore-render-probe"))return;
            Application.runInBackground=true;
            var probe=new GameObject("Owned opt-in standalone render probe").AddComponent<CombatRenderProbe>();probe.corpseMode=corpse;probe.courtyardMode=courtyard;
            probe.powerBoxMode=power;
        }
        private IEnumerator Start()
        {
            folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ForgeCoreRenderProbe-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);report.device=SystemInfo.graphicsDeviceType.ToString();
            report.scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            yield return null;yield return null;
            var session=SecurityCombatSession.Instance;
            if(session==null||session.player==null){Finish("No real security session",2);yield break;}
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){Finish("A real graphics device is required",2);yield break;}
            session.SetPaused(false);session.enabled=false;
            foreach(var enemy in session.enemies){enemy.enabled=false;enemy.motion.SetPaused(true);}
            player=session.player;player.enabled=false;camera=player.view.GetComponent<Camera>();
            if(powerBoxMode){yield return ProbePowerBox(session);yield break;}
            if(courtyardMode){yield return ProbeCourtyard(session);yield break;}
            if(corpseMode){yield return ProbeCorpse(session);yield break;}
            player.motion.ShotEmitted+=OnShot;
            for(var pass=0;pass<2;pass++)
            {
                var aim=pass==1;
                for(var i=0;i<60;i++){player.Simulate(new PrototypeInputFrame(),aim,1f/60);yield return null;}
                shotName=aim?"ads-shot":"hip-shot";Capture(aim?"ads-before":"hip-before");captured=false;
                player.Simulate(new PrototypeInputFrame(fireHeld:true),aim,1f/60);
                var deadline=Time.realtimeSinceStartup+3;
                while(!captured&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!captured){Finish("No actual firing marker: "+shotName,2);yield break;}
                yield return new WaitForSecondsRealtime(.6f);
            }
            // Actual gameplay feedback path, including the particle renderer absent from the old flash fixture.
            PrototypeFeedback.Hit(player.motion.muzzle.position,false);yield return new WaitForSecondsRealtime(.06f);Capture("hit-feedback");
            player.motion.ShotEmitted-=OnShot;
            yield return new WaitForSecondsRealtime(.5f);
            // The authored mission deliberately keeps guards inactive until power is restored.
            // Exercise that real interaction gate rather than bypassing it with SetActive.
            if(session.mission!=null&&session.mission.Stage==0)
            {
                var gateController=player.GetComponent<CharacterController>();gateController.enabled=false;
                player.transform.position=session.mission.objectives[0].position;gateController.enabled=true;
                if(!session.mission.TryInteract()){Finish("Mission power interaction refused",2);yield break;}
            }
            var guard=session.enemies[0];guard.EnsureInitialized();
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            player.transform.position=guard.transform.position-Vector3.forward*3;cc.enabled=true;
            var guardSkin=guard.GetComponentInChildren<SkinnedMeshRenderer>();
            camera.transform.LookAt(guardSkin.bounds.center);
            Physics.SyncTransforms();guard.motion.SetPaused(false);guard.enabled=true;
            Capture("guard-before");captured=false;var previousShots=guard.Shots;
            Action<Vector3,Vector3> guardMarker=(p,d)=>{Capture("guard-shot");captured=true;report.actualGuardShotMarker=guard.Shots>previousShots;};
            guard.motion.ShotEmitted+=guardMarker;var guardDeadline=Time.realtimeSinceStartup+5;
            while(!captured&&Time.realtimeSinceStartup<guardDeadline){camera.transform.LookAt(guardSkin.bounds.center);yield return null;}
            guard.motion.ShotEmitted-=guardMarker;
            report.guardDiagnostic="health="+guard.Health+" action="+guard.motion.Action+" paused="+guard.motion.Paused+
                " ammo="+guard.motion.Ammo+" actor="+guard.transform.position+" visual="+guard.motion.transform.position+
                " skin="+guardSkin.bounds.center+" player="+player.transform.position+" sees="+guard.CanSee(player.transform.position+Vector3.up*1.1f,player.transform);
            if(!captured||!report.actualGuardShotMarker){Finish("Guard firing path did not launch a projectile",2);yield break;}
            var bad=report.samples.Any(s=>s.magentaPixels>0);
            foreach(var name in new[]{"hip","ads","guard"})
            {
                var before=report.samples.Single(s=>s.name==name+"-before");var shot=report.samples.Single(s=>s.name==name+"-shot");
                if(shot.flashes<1||shot.warmPixels<=before.warmPixels+3)bad=true;
            }
            Finish(bad?"Rendered magenta or missing visible warm shot":null,bad?1:0);
        }
        private IEnumerator ProbePowerBox(SecurityCombatSession session)
        {
            report.kind="standalone_actual_power_box_interaction";
            var mission=session.mission;var power=mission!=null?mission.powerBox:null;
            if(power==null||power.statusLamp==null||power.solidCollider==null){Finish("Physical power box missing",2);yield break;}
            var cc=player.GetComponent<CharacterController>();
            void View(){cc.enabled=false;player.transform.position=new Vector3(-10,.03f,-11.05f);cc.enabled=true;camera.transform.LookAt(power.solidCollider.bounds.center);Physics.SyncTransforms();}
            View();for(var i=0;i<10;i++)yield return null;Capture("power-box-offline");
            report.physicalPowerBox=power.solidCollider.enabled&&!power.solidCollider.isTrigger&&power.statusLamp.sharedMaterial==power.standbyMaterial;
            report.powerPlaceholderAbsent=power.GetComponent<SphereCollider>()==null&&power.GetComponent<MeshRenderer>()==null&&
                !mission.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text=="POWER");
            if(!mission.TryInteract()){Finish("Real power box interaction refused",2);yield break;}
            for(var i=0;i<10;i++)yield return null;Capture("power-box-online");
            report.powerBoxPersistent=mission.Stage==1&&power.gameObject.activeInHierarchy&&power.IsPowered&&
                power.solidCollider.enabled&&power.statusLamp.sharedMaterial==power.poweredMaterial;
            session.Restart();View();for(var i=0;i<10;i++)yield return null;Capture("power-box-reset");
            report.powerBoxReset=mission.Stage==0&&!power.IsPowered&&power.gameObject.activeInHierarchy&&power.statusLamp.sharedMaterial==power.standbyMaterial;
            var ok=report.physicalPowerBox&&report.powerPlaceholderAbsent&&report.powerBoxPersistent&&report.powerBoxReset&&!report.samples.Any(s=>s.magentaPixels>0);
            Finish(ok?null:"Power box identity/state/render check failed",ok?0:1);
        }
        private IEnumerator ProbeCourtyard(SecurityCombatSession session)
        {
            report.kind="standalone_courtyard_visual_candidate";
            if(session.mission==null||!session.mission.courtyardPresentation){Finish("No courtyard presentation",2);yield break;}
            for(var i=0;i<30;i++){player.Simulate(new PrototypeInputFrame(),false,1f/60);yield return null;}
            Capture("courtyard-spawn");
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;
            player.transform.position=new Vector3(-10,.03f,-5);camera.transform.LookAt(new Vector3(2,4,17));cc.enabled=true;
            for(var i=0;i<10;i++)yield return null;Capture("courtyard-control-office");
            cc.enabled=false;player.transform.position=new Vector3(-10,.03f,8);camera.transform.LookAt(new Vector3(5,2,-4));cc.enabled=true;
            for(var i=0;i<10;i++)yield return null;Capture("courtyard-service-lane");
            Finish(report.samples.Any(s=>s.magentaPixels>0)?"Rendered magenta in courtyard":null,report.samples.Any(s=>s.magentaPixels>0)?1:0);
        }
        private IEnumerator ProbeCorpse(SecurityCombatSession session)
        {
            report.kind="standalone_actual_corpse_grounding";
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            if(session.mission!=null&&session.mission.Stage==0)
            {
                player.transform.position=session.mission.objectives[0].position;
                if(!session.mission.TryInteract()){Finish("Mission power interaction refused",2);yield break;}
            }
            var enemy=session.enemies[0];enemy.EnsureInitialized();enemy.enabled=false;enemy.motion.SetPaused(false);
            player.motion.SetPaused(true);
            player.transform.position=enemy.transform.position-Vector3.right*3-Vector3.forward*1.5f;controller.enabled=true;
            foreach(var r in player.arms.GetComponentsInChildren<Renderer>())r.enabled=false;
            foreach(var r in player.weapon.GetComponentsInChildren<Renderer>())r.enabled=false;
            camera.transform.LookAt(enemy.transform.position+Vector3.up*.65f+Vector3.forward*.3f);
            Physics.SyncTransforms();for(var i=0;i<5;i++)yield return null;
            Capture("corpse-before");enemy.Damage(3);
            var skin=enemy.motion.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            var weights=skin.sharedMesh.boneWeights;
            var hips=enemy.motion.GetComponentsInChildren<Transform>().Single(b=>b.name=="Hips");
            foreach(var target in new[]{.15f,.4f,.8f,1.2f,1.8f,2.2f,3f,5f})
            {
                var deadline=Time.realtimeSinceStartup+8;
                while(enemy.motion.ActionTime<target&&Time.realtimeSinceStartup<deadline)yield return null;
                if(enemy.motion.ActionTime<target){Destroy(baked);Finish("Death clock did not advance",2);yield break;}
                yield return new WaitForEndOfFrame();skin.BakeMesh(baked);var vertices=baked.vertices;
                var stat=new CorpseSample{time=enemy.motion.ActionTime,minY=float.PositiveInfinity,
                    torsoMinY=float.PositiveInfinity,visualY=enemy.motion.transform.position.y,pelvisY=hips.position.y,cachedGroundError=enemy.CorpseGroundError};
                RaycastHit ground;
                if(!Physics.Raycast(enemy.transform.position+Vector3.up*.7f,Vector3.down,out ground,2,1<<8,QueryTriggerInteraction.Ignore))
                {Destroy(baked);Finish("No actual floor ray hit",2);yield break;}
                stat.floorY=ground.point.y;
                for(var i=0;i<vertices.Length;i++)
                {
                    var y=skin.transform.TransformPoint(vertices[i]).y;
                    var w=weights[i];var indices=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                    var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var torso=0f;
                    if(y<stat.minY){stat.minY=y;var major=Array.IndexOf(amounts,amounts.Max());stat.lowestBone=skin.bones[indices[major]].name;}
                    for(var b=0;b<4;b++){var n=skin.bones[indices[b]].name;if(n=="Hips"||n=="Spine"||n=="Chest")torso+=amounts[b];}
                    if(torso>.5f)stat.torsoMinY=Mathf.Min(stat.torsoMinY,y);
                }
                report.corpseSamples.Add(stat);Capture("corpse-"+Mathf.RoundToInt(target*1000));
            }
            Destroy(baked);
            var bad=report.corpseSamples.Where(s=>s.time>=1.8f).Any(s=>s.minY-s.floorY>.02f||s.minY-s.floorY<-.02f||s.torsoMinY-s.floorY>.015f);
            Finish(bad?"Independent corpse/torso vertices do not meet the floor":null,bad?1:0);
        }
        private void OnShot(Vector3 position,Vector3 direction)
        {
            report.actualShotMarker=true;Capture(shotName);captured=true;
        }
        private void Capture(string name)
        {
            var sample=new Sample{name=name,flashes=PrototypeMuzzleFlash.ActiveCount,
                projectiles=UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None).Length};
            foreach(var system in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))sample.particles+=system.particleCount;
            var materials=new List<string>();
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(renderer.GetComponent<PrototypeMuzzleFlash>()==null&&renderer.GetComponent<SecurityCombatBullet>()==null)continue;
                foreach(var material in renderer.sharedMaterials)materials.Add(material==null?"NULL":
                    material.name+" | "+(material.shader==null?"NULL":material.shader.name)+" | supported="+
                    (material.shader!=null&&material.shader.isSupported)+" | passes="+material.passCount+" | "+string.Join(",",material.shaderKeywords));
            }
            sample.materials=materials.ToArray();
            var previous=RenderTexture.active;var oldTarget=camera.targetTexture;var target=new RenderTexture(960,540,24);
            var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
                foreach(var p in pixels.GetPixels())
                {
                    if(p.r>.3f&&p.g>.15f&&p.r>p.b*1.2f)sample.warmPixels++;
                    if(p.r>.4f&&p.b>.4f&&p.g<.25f)sample.magentaPixels++;
                }
                using(var file=new FileStream(Path.Combine(folder,name+".png"),FileMode.CreateNew))
                {var bytes=pixels.EncodeToPNG();file.Write(bytes,0,bytes.Length);}
                report.samples.Add(sample);
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(pixels);}
        }
        private void Finish(string error,int exitCode)
        {
            report.error=error;
            using(var file=new StreamWriter(new FileStream(Path.Combine(folder,"report.json"),FileMode.CreateNew)))file.Write(JsonUtility.ToJson(report,true));
            Debug.Log("FORGECORE_RENDER_PROBE "+folder+" exit="+exitCode);Application.Quit(exitCode);
        }
    }
}
#endif
""";
}
