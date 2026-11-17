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
        private GameObject root;private SecurityCombatSession session;private ConnectedIndustrialLevel level;private string assetFolder;
        [UnitySetUp]public IEnumerator Setup(){
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/IndustrialMission.prefab")&&File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).First();
            assetFolder=Path.GetDirectoryName(path);PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));level=root.GetComponent<ConnectedIndustrialLevel>();session=level.session;
            session.enabled=false;session.mission.enabled=false;session.SetPaused(true);yield return null;session.player.enabled=false;
            foreach(var enemy in session.enemies)enemy.enabled=false;foreach(var motion in root.GetComponentsInChildren<PrototypeSkinnedAnimator>(true))motion.enabled=false;
            session.options.StartNewGame();session.SetPaused(false);Physics.SyncTransforms();session.Navigation.Refresh();
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();foreach(var bullet in UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(bullet.gameObject);if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        // Controlled room fixtures, not a traversal or human combat-balance acceptance.
        private SecurityCombatEnemy ArrangeEncounter(int guardIndex){
            session.mission.RestoreCheckpoint(guardIndex==2?2:1,guardIndex==2?3:0);
            var enemy=session.enemies[guardIndex];enemy.EnsureInitialized();
            foreach(var other in session.enemies)if(other!=enemy)other.gameObject.SetActive(false);
            session.player.RestoreCheckpoint(100,60,6,enemy.transform.position-Vector3.forward*6,0,0);
            session.SetPaused(false);Physics.SyncTransforms();
            Assert.AreEqual(guardIndex==2?"CONTROL ROOM":"WAREHOUSE",level.Zone(session.player.transform.position));
            Assert.True(enemy.CanSee(session.player.transform.position+Vector3.up*1.1f,session.player.transform),"Fixture must first prove a clear real-room firing lane.");
            return enemy;
        }
        private void StepEncounter(SecurityCombatEnemy enemy,float dt,bool ai=true){
            if(ai)enemy.Tick(dt,session);
            enemy.motion.Tick(dt);enemy.motion.animator.Update(dt);enemy.motion.FlushShotMarkerForTest();
            foreach(var bullet in UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))bullet.Advance(dt);
        }
        [TestCase(0,30)][TestCase(0,60)][TestCase(0,120)]
        [TestCase(1,30)][TestCase(1,60)][TestCase(1,120)]
        [TestCase(2,30)][TestCase(2,60)][TestCase(2,120)]
        public void ConnectedRoomGuardAnimationLaunchesProjectileAndDamagesPlayer(int guardIndex,int rate){
            var enemy=ArrangeEncounter(guardIndex);var markers=0;Action<Vector3,Vector3> marker=(p,d)=>markers++;
            enemy.motion.ShotEmitted+=marker;
            try{for(var frame=0;frame<rate*5;frame++)StepEncounter(enemy,1f/rate);}
            finally{enemy.motion.ShotEmitted-=marker;}
            Assert.Greater(markers,0);Assert.Greater(enemy.Shots,0,"Actual AI -> evaluated animation marker -> muzzle recheck -> projectile route.");
            Assert.Less(session.player.Health,100,"Damage must come from swept projectile arrival, never a direct test Damage call.");
            Assert.Greater(session.player.Health,0);Assert.AreEqual(3,enemy.Health);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]public void ConnectedRoomPausePreventsReturnFire(int guardIndex){
            var enemy=ArrangeEncounter(guardIndex);session.SetPaused(true);var pose=enemy.transform.position;
            for(var frame=0;frame<180;frame++)StepEncounter(enemy,1f/60);
            Assert.AreEqual(0,enemy.Shots);Assert.AreEqual(100,session.player.Health);Assert.AreEqual(pose,enemy.transform.position);
            session.SetPaused(false);for(var frame=0;frame<300;frame++)StepEncounter(enemy,1f/60);
            Assert.Greater(enemy.Shots,0);Assert.Less(session.player.Health,100);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]public void ConnectedRoomMuzzleMarkerRechecksNewOpaqueCover(int guardIndex){
            var enemy=ArrangeEncounter(guardIndex);enemy.transform.rotation=Quaternion.LookRotation(session.player.transform.position-enemy.transform.position);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try{
                wall.name="Owned inserted-cover fixture, not authored map cover";wall.layer=8;
                wall.transform.position=(enemy.transform.position+session.player.transform.position)*.5f+Vector3.up*1.5f;wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();
                Assert.False(enemy.CanSee(session.player.transform.position+Vector3.up*1.1f,session.player.transform));
                Assert.True(enemy.motion.TryFire(),"Deliberately queue a marker despite newly blocked LOS to test the final muzzle recheck.");
                for(var frame=0;frame<60;frame++)StepEncounter(enemy,1f/60,false);
                Assert.AreEqual(0,enemy.Shots);Assert.AreEqual(100,session.player.Health);Assert.Greater(session.ShotsBlockedByCover,0);
            }finally{UnityEngine.Object.DestroyImmediate(wall);}
        }
        [Test]public void ConnectedInteriorScreensAreBoundedAndDoNotAddCollision(){
            var material=AssetDatabase.LoadAssetAtPath<Material>(assetFolder+"/ConnectedScreen.mat");Assert.NotNull(material);Assert.AreEqual("Standard",material.shader.name);
            var texture=material.mainTexture;Assert.NotNull(texture);Assert.AreEqual(256,texture.width);Assert.AreEqual(128,texture.height);
            var importer=(TextureImporter)AssetImporter.GetAtPath(assetFolder+"/ConnectedScreen.png");Assert.AreEqual(TextureImporterFormat.BC7,importer.GetPlatformTextureSettings("Standalone").format);
            var report=File.ReadAllText(assetFolder+"/connected-interior-report.json");StringAssert.Contains("\"collisionUnchanged\": true",report);StringAssert.Contains("\"addedColliders\": 0",report);StringAssert.Contains("\"visualReviewed\": false",report);
            Assert.AreEqual(49,root.GetComponentsInChildren<Collider>(true).Length);Assert.LessOrEqual(root.GetComponentsInChildren<Renderer>(true).Length,300);
            Assert.True(root.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.sharedMaterial==material));
        }
        [Test]public void InteriorComparisonIsStaticChangedAndExplicitlyUnaccepted(){
            var report=File.ReadAllText(assetFolder+"/connected-interior-review.json");StringAssert.Contains("static_editor_comparison_left_before_right_after_not_runtime_animation",report);StringAssert.Contains("fresh_same_output_before_optional_interior_dressing_not_previous_v6_binary",report);
            StringAssert.Contains("\"humanArtAccepted\": false",report);StringAssert.Contains("\"stageComplete\": false",report);StringAssert.Contains("\"magentaPixels\": 0",report);
            StringAssert.Contains("\"combatAndObjectivePosesUnchanged\": true",report);
            var changed=System.Text.RegularExpressions.Regex.Match(report,"\"changedPixels\"\\s*:\\s*(\\d+)");Assert.True(changed.Success);Assert.GreaterOrEqual(int.Parse(changed.Groups[1].Value),1000);
            var image=AssetDatabase.LoadAssetAtPath<Texture2D>(assetFolder+"/connected-interior-review.png");Assert.AreEqual(1536,image.width);Assert.AreEqual(1296,image.height);
        }
        [Test]public void RetainedCourtyardBatchesHaveNoRooftopOrphanTriangles(){
            foreach(var name in new[]{"Courtyard presentation — original template, not reviewed library art","Indie environment v1","Courtyard detail v1 — bounded original candidate"}){
                var branch=root.transform.Find(name);Assert.NotNull(branch);
                foreach(var filter in branch.GetComponentsInChildren<MeshFilter>()){
                    var vertices=filter.sharedMesh.vertices;
                    Assert.True(filter.sharedMesh.triangles.All(i=>filter.transform.TransformPoint(vertices[i]).z<17),name);
                }
            }
        }
        [UnityTest]public IEnumerator PhysicalWorldSignsRenderAndAreOccludedByOpaqueGeometry(){
            var signs=root.GetComponentsInChildren<TextMesh>(true);Assert.Greater(signs.Length,3);
            Assert.True(signs.All(t=>t.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="ForgeCore/WorldSignText"));
            var cameraObject=new GameObject("Owned sign occlusion camera");var camera=cameraObject.AddComponent<Camera>();
            var textObject=new GameObject("Owned isolated sign");var text=textObject.AddComponent<TextMesh>();
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var rt=new RenderTexture(256,128,24,RenderTextureFormat.ARGB32);var pixels=new Texture2D(256,128,TextureFormat.RGB24,false);
            var wallMaterial=new Material(Shader.Find("Unlit/Color"));var previous=RenderTexture.active;
            try {
                camera.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=1;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<30;camera.targetTexture=rt;camera.allowHDR=false;
                textObject.layer=30;text.font=signs[0].font;text.fontSize=48;text.characterSize=.15f;text.anchor=TextAnchor.MiddleCenter;text.text="WAREHOUSE";text.color=Color.white;
                text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);text.GetComponent<MeshRenderer>().sharedMaterial=signs[0].GetComponent<MeshRenderer>().sharedMaterial;
                wall.layer=30;wall.transform.position=new Vector3(0,0,-1);wall.transform.localScale=new Vector3(4,2,.1f);wallMaterial.color=Color.black;wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;wall.SetActive(false);
                yield return null;
                int BrightPixels(){camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,256,128),0,0);pixels.Apply();return pixels.GetPixels32().Count(c=>c.r>180&&c.g>180&&c.b>180);}
                var visible=BrightPixels();Assert.Greater(visible,30,"The test must first prove the actual font shader renders visible text.");
                wall.SetActive(true);var hidden=BrightPixels();Assert.Less(hidden,visible/20,"A physical sign must not bleed through the opaque wall.");
            } finally {
                RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(textObject);UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(wallMaterial);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
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

