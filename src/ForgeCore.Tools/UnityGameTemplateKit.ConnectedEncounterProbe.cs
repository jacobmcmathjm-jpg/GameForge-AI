namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string ConnectedEncounterProbeSource="""
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    // Opt-in bounded fixture in the exported real map. Explicit repositioning, not mission traversal/balance acceptance.
    public sealed class ConnectedEncounterProbe:MonoBehaviour
    {
        [Serializable]public sealed class Encounter {
            public int guardIndex,healthBefore,healthAfter,shots,markers,projectilePeak;public string zone;
            public float elapsed,guardTravel;public bool clearLane,pausedWithoutShotsOrDamage;
        }
        [Serializable]public sealed class Report {
            public int schemaVersion=1;public string kind="connected_room_controlled_return_fire_fixture",device,scene,error,storageRoot;
            public bool controlledRepositioning=true,directDamageUsed=false,missionTraversalAccepted=false,humanBalanceAccepted=false;
            public List<Encounter> encounters=new List<Encounter>();
        }
        private readonly Report report=new Report();private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap(){
            if(!Environment.GetCommandLineArgs().Contains("-forgecore-connected-encounters-probe"))return;
            Application.runInBackground=true;new GameObject("Owned connected room encounter fixture").AddComponent<ConnectedEncounterProbe>();
        }
        private IEnumerator Start(){
            folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ForgeCoreEncounterProbe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            report.device=SystemInfo.graphicsDeviceType.ToString();report.scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            yield return null;yield return null;
            var session=SecurityCombatSession.Instance;var level=session!=null?session.GetComponentInParent<ConnectedIndustrialLevel>():null;
            if(level==null||session.enemies.Length!=3||!DemoRuntimeScope.Diagnostic||report.device=="Null"){Finish("Expected connected scene, diagnostic save isolation and real graphics",2);yield break;}
            report.storageRoot=session.options.StoreRoot;
            if(!Path.GetFileName(report.storageRoot).StartsWith("ForgeCoreDemoProbe-",StringComparison.Ordinal)){Finish("Normal save root refused",2);yield break;}
            session.enabled=false;session.player.enabled=false;session.mission.enabled=false;session.options.enabled=false;
            foreach(var guard in session.enemies)guard.enabled=false;
            for(var index=0;index<3;index++){
                // Restore stages and position a stationary non-firing player six metres from the unchanged authored guard.
                session.Restart();session.mission.RestoreCheckpoint(index==2?2:1,index==2?3:0);
                var enemy=session.enemies[index];enemy.EnsureInitialized();foreach(var other in session.enemies)if(other!=enemy)other.gameObject.SetActive(false);
                var player=session.player;player.RestoreCheckpoint(100,60,6,enemy.transform.position-Vector3.forward*6,0,0);
                var camera=player.view.GetComponent<Camera>();camera.transform.LookAt(enemy.transform.position+Vector3.up*1.1f);Physics.SyncTransforms();
                var item=new Encounter{guardIndex=index,zone=level.Zone(player.transform.position),healthBefore=player.Health};report.encounters.Add(item);
                item.clearLane=enemy.CanSee(player.transform.position+Vector3.up*1.1f,player.transform);
                if(!item.clearLane){Finish("Room fixture has no clear initial firing lane",2);yield break;}
                enemy.enabled=true;session.SetPaused(true);var pausedUntil=Time.realtimeSinceStartup+.5f;
                while(Time.realtimeSinceStartup<pausedUntil)yield return null;
                item.pausedWithoutShotsOrDamage=enemy.Shots==0&&player.Health==100;
                session.SetPaused(false);var start=Time.realtimeSinceStartup;var position=enemy.transform.position;
                Action<Vector3,Vector3> marker=(p,d)=>item.markers++;enemy.motion.ShotEmitted+=marker;
                while(Time.realtimeSinceStartup-start<6&&player.Health==100){
                    item.projectilePeak=Math.Max(item.projectilePeak,FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None).Length);yield return null;
                }
                enemy.motion.ShotEmitted-=marker;item.elapsed=Time.realtimeSinceStartup-start;item.shots=enemy.Shots;item.healthAfter=player.Health;item.guardTravel=Vector3.Distance(position,enemy.transform.position);
                enemy.enabled=false;session.SetPaused(true);
                foreach(var bullet in FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))Destroy(bullet.gameObject);
                if(!item.pausedWithoutShotsOrDamage||item.shots<1||item.markers<1||item.projectilePeak<1||item.healthAfter>=100||item.healthAfter<=0){Finish("Actual room return fire / pause / projectile damage failed",1);yield break;}
                yield return null;
            }
            Finish(null,0);
        }
        private void Finish(string error,int code){
            report.error=error;using(var stream=new StreamWriter(new FileStream(Path.Combine(folder,"report.json"),FileMode.CreateNew)))stream.Write(JsonUtility.ToJson(report,true));
            Debug.Log("FORGECORE_ENCOUNTER_PROBE "+folder+" exit="+code);Application.Quit(code);
        }
    }
}
#endif
""";
}
