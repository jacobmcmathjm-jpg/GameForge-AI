namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string ConnectedLevelTestsSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class ConnectedLevelTests
    {
        private GameObject root;private SecurityCombatSession session;private ConnectedIndustrialLevel level;
        [UnitySetUp]public IEnumerator Setup(){
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/IndustrialMission.prefab")&&File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).First();
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));level=root.GetComponent<ConnectedIndustrialLevel>();session=level.session;
            session.enabled=false;session.mission.enabled=false;session.SetPaused(true);yield return null;session.player.enabled=false;
            foreach(var enemy in session.enemies)enemy.enabled=false;foreach(var motion in root.GetComponentsInChildren<PrototypeSkinnedAnimator>(true))motion.enabled=false;
            session.options.StartNewGame();session.SetPaused(false);Physics.SyncTransforms();session.Navigation.Refresh();
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        [Test]public void ExpandedSingleFloorHasThreeNamedReachableZonesAndNoVoidShortcut(){
            Assert.AreSame(level,session.GetComponentInParent<ConnectedIndustrialLevel>(),"The session is a child, not the level-component owner.");
            Assert.True(session.ConnectedLayout);Assert.AreEqual("SERVICE YARD",level.Zone(new Vector3(0,0,-10)));Assert.AreEqual("WAREHOUSE",level.Zone(new Vector3(-10,0,30)));Assert.AreEqual("CONTROL ROOM",level.Zone(new Vector3(32,0,32)));
            Assert.AreEqual(0,session.Navigation.Find(new Vector3(0,0,-12),new Vector3(30,0,-10)).Count);
            Assert.AreEqual(0,session.Navigation.Find(new Vector3(0,0,-12),new Vector3(90,0,90)).Count);
            Assert.LessOrEqual(session.Navigation.LastExpanded,4096);
        }
        [Test]public void LockedGatesPreventSkippingWarehouseAndControlRoom(){
            Assert.True(level.doors.All(d=>d.enabled));Assert.AreEqual(0,session.Navigation.Find(session.player.transform.position,session.mission.objectives[1].position).Count);
            level.Synchronize(1);Assert.False(level.doors[0].enabled);Assert.True(level.doors[1].enabled);
            Assert.Greater(session.Navigation.Find(session.player.transform.position,session.mission.objectives[1].position).Count,20);
            Assert.AreEqual(0,session.Navigation.Find(session.mission.objectives[1].position,session.mission.objectives[2].position).Count);
            level.Synchronize(2);Assert.Greater(session.Navigation.Find(session.mission.objectives[1].position,session.mission.objectives[2].position).Count,20);
        }
        [Test]public void ResetAndMissionRestoreSynchronizeDoorsWithoutOpeningWrongStage(){
            session.mission.RestoreCheckpoint(2,3);Assert.True(level.doors.All(d=>!d.enabled));session.Restart();Assert.AreEqual(0,session.mission.Stage);Assert.True(level.doors.All(d=>d.enabled));
            Assert.AreEqual(DemoStateRules.ConnectedProfile,session.CheckpointProfile);Assert.False(session.enemies.Any(e=>e.gameObject.activeSelf));
        }
        [Test]public void HypotheticalCheckpointCollisionDoesNotChangeDoorOrMissionState(){
            var position=new Vector3(-10,.02f,16.5f);Assert.True(level.doors[0].enabled);
            Assert.True(level.CheckCheckpointClear(position,1));Assert.True(level.doors[0].enabled);Assert.AreEqual(0,session.mission.Stage);
            Assert.False(level.CheckCheckpointClear(new Vector3(-16.5f,.02f,25),2));Assert.True(level.doors.All(d=>d.enabled));
            level.Synchronize(2);var pose=level.leaves[1].localPosition;
            Assert.False(level.CheckCheckpointClear(new Vector3(16.5f,.02f,36),1),"An earlier closed-door pose must block a corrupt portal checkpoint.");
            Assert.AreEqual(pose,level.leaves[1].localPosition);Assert.True(level.doors.All(d=>!d.enabled));
            Assert.False(level.CheckCheckpointClear(new Vector3(30,.02f,-10),2),"Unsupported void coordinates are not loadable just because they are in numeric bounds.");
        }
        [UnityTest]public IEnumerator ExpandedCheckpointRoundtripPreservesFarRoomAndRejectsOtherProfile(){
            session.mission.RestoreCheckpoint(2,3);session.player.RestoreCheckpoint(83,44,4,new Vector3(38,.02f,40.5f),20,0);session.SetPaused(false);
            var save=session.options.RequestCheckpoint("Owned connected far-room check");while(!save.IsCompleted)yield return null;Assert.True(save.Result);
            session.Restart();Assert.True(session.options.TryLoadCheckpoint());Assert.AreEqual(2,session.mission.Stage);Assert.Less(Vector3.Distance(new Vector3(38,.02f,40.5f),session.player.transform.position),.1f);Assert.AreEqual(83,session.player.Health);Assert.True(level.doors.All(d=>!d.enabled));
            // The real options path must find the parent owner when restoring in an open portal.
            session.mission.RestoreCheckpoint(1,3);session.player.RestoreCheckpoint(83,44,4,new Vector3(-10,.02f,16.5f),0,0);
            save=session.options.RequestCheckpoint("Owned open-portal checkpoint");while(!save.IsCompleted)yield return null;Assert.True(save.Result);
            session.Restart();Assert.True(level.doors[0].enabled);Assert.True(session.options.TryLoadCheckpoint());Assert.AreEqual(1,session.mission.Stage);Assert.False(level.doors[0].enabled);
            var file=Path.Combine(session.options.StoreRoot,"checkpoint.json");var data=JsonUtility.FromJson<DemoCheckpoint>(File.ReadAllText(file));data.profile=DemoStateRules.Profile;File.WriteAllText(file,JsonUtility.ToJson(data));var before=session.player.transform.position;
            Assert.False(session.options.TryLoadCheckpoint());Assert.AreEqual(before,session.player.transform.position);Assert.AreEqual(1,session.mission.Stage);
        }
    }
}
""";
}

