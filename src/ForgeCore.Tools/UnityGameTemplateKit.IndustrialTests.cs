namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndustrialPropTestsSource = """
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class IndustrialPropTests
    {
        [Test]public void EveryS08RecipeHasMeasuredLodsUvPbrAndConservativeGroundedCollision()
        {
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/IndustrialProps"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialProp.prefab")).ToArray();
            var recipes=new[]{"industrial_barrel_v2","wooden_crate_v1","concrete_barrier_v1","industrial_fence_v1","shipping_container_v1"};
            foreach(var recipe in recipes) {
                var prefab=paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).LastOrDefault(p=>p.GetComponent<IndustrialPropIdentity>().recipe==recipe);
                Assert.NotNull(prefab,recipe+" requires an actual generated target prefab.");
                var identity=prefab.GetComponent<IndustrialPropIdentity>();Assert.AreEqual(64,identity.assetSha256.Length);
                Assert.AreEqual("solid_box_no_walkable_interior",identity.collisionConvention);
                Assert.AreEqual(Vector3.one,prefab.transform.localScale);Assert.AreEqual(Quaternion.identity,prefab.transform.localRotation);
                Assert.AreEqual(1,prefab.GetComponentsInChildren<Collider>().Length);
                var box=prefab.GetComponent<BoxCollider>();Assert.NotNull(box);Assert.AreEqual(0,box.center.y-box.size.y/2,.006f);
                Assert.False(box.isTrigger);
                var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.AreEqual(3,lods.Length);
                var counts=lods.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
                Assert.Greater(counts[0],counts[1]);Assert.Greater(counts[1],counts[2]);Assert.LessOrEqual(counts[0],16000);
                foreach(var level in lods)foreach(var renderer in level.renderers) {
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);
                    Assert.True(mesh.uv.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)));
                    Assert.True(renderer.sharedMaterials.All(m=>m!=null&&m.shader!=null&&
                        (SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null||m.shader.isSupported)));
                }
                Assert.True(File.Exists(identity.sourceAssetPath));Assert.False(string.IsNullOrWhiteSpace(identity.recipe));
            }
        }
    }
}
""";
    private const string IndustrialMissionTestsSource = """
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using ForgeCore.Demo;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class IndustrialPropRuntimeTests
    {
        [TestCase("industrial_barrel_v2")][TestCase("wooden_crate_v1")][TestCase("concrete_barrier_v1")]
        [TestCase("industrial_fence_v1")][TestCase("shipping_container_v1")]
        public void ActualPlacedPropBlocksARealControllerWithoutMutatingItsPrefab(string recipe)
        {
            var prefab=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/IndustrialProps"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialProp.prefab")).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault(p=>p.GetComponent<IndustrialPropIdentity>().recipe==recipe);
            Assert.NotNull(prefab);
            GameObject prop=null,ground=null,player=null;
            try {
                prop=UnityEngine.Object.Instantiate(prefab);prop.transform.position=new Vector3(1000,0,1000);
                ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(1000,-.1f,1000);ground.transform.localScale=new Vector3(30,.2f,30);
                player=new GameObject("S08 actual collision probe");var controller=player.AddComponent<CharacterController>();controller.height=1.8f;controller.center=new Vector3(0,.9f,0);controller.radius=.32f;controller.skinWidth=.02f;
                Physics.SyncTransforms();var box=prop.GetComponent<BoxCollider>();var front=box.bounds.min.z;
                player.transform.position=new Vector3(1000,.03f,front-2);Physics.SyncTransforms();
                var start=player.transform.position;
                for(var frame=0;frame<180;frame++)controller.Move(new Vector3(0,-.01f,3f/60));
                Assert.Greater(player.transform.position.z,start.z+1,"Controller must actually advance, not merely pass a stationary overlap check.");
                Assert.Less(player.transform.position.z,front-.20f,"Solid-box collision must stop the controller before the visible prop.");
                Assert.AreEqual(3,prop.GetComponent<LODGroup>().GetLODs().Length);Assert.False(box.isTrigger);
                Assert.AreEqual(Vector3.zero,prefab.transform.position,"Runtime placement must not mutate the source prefab.");
            }finally {
                if(player!=null)UnityEngine.Object.DestroyImmediate(player);if(prop!=null)UnityEngine.Object.DestroyImmediate(prop);if(ground!=null)UnityEngine.Object.DestroyImmediate(ground);
            }
        }
    }
    public sealed class IndustrialMissionTests
    {
        private GameObject root;private SecurityCombatSession session;private IndustrialMission mission;
        [UnitySetUp]public IEnumerator Setup()
        {
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            Assert.NotNull(path,"Generate a fresh industrial mission fixture.");
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            mission=root.GetComponent<IndustrialMission>();session=mission.session;session.enabled=false;mission.enabled=false;session.SetPaused(true);
            yield return null;
            session.player.enabled=false;foreach(var enemy in session.enemies)enemy.enabled=false;
            foreach(var motion in root.GetComponentsInChildren<PrototypeSkinnedAnimator>())motion.enabled=false;
            mission.ResetMission();session.SetPaused(false);Physics.SyncTransforms();session.Navigation.Refresh();
        }
        [TearDown]public void Cleanup()
        {
            PrototypeInput.ClearScriptedFrame();
            foreach(var bullet in UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(bullet.gameObject);
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);
        }
        private void MovePlayer(Vector3 position)
        {
            var controller=session.player.GetComponent<CharacterController>();controller.enabled=false;
            position.y=.03f;session.player.transform.position=position;controller.enabled=true;Physics.SyncTransforms();
        }
        [Test]public void ObjectiveOrderAndEncounterGatesCannotBeSkippedAndLastKillDoesNotPrematurelyWin()
        {
            Assert.AreEqual(0,mission.Stage);Assert.True(session.enemies.All(e=>!e.gameObject.activeSelf));
            MovePlayer(mission.objectives[2].position);Assert.False(mission.TryInteract());
            MovePlayer(mission.objectives[0].position);Assert.True(mission.TryInteract());Assert.AreEqual(1,mission.Stage);
            Assert.True(session.enemies[0].gameObject.activeSelf);Assert.True(session.enemies[1].gameObject.activeSelf);Assert.False(session.enemies[2].gameObject.activeSelf);
            MovePlayer(mission.objectives[1].position);Assert.False(mission.TryInteract());
            session.enemies[0].Damage(3);session.enemies[1].Damage(3);Assert.True(mission.TryInteract());
            Assert.AreEqual(2,mission.Stage);Assert.True(session.enemies[2].gameObject.activeSelf);
            MovePlayer(mission.objectives[2].position);Assert.False(mission.TryInteract());session.enemies[2].Damage(3);
            Assert.False(session.Finished,"The last kill is not extraction.");Assert.True(mission.TryInteract());
            Assert.True(mission.Completed);Assert.True(session.Finished);Assert.True(session.Paused);Assert.False(mission.TryInteract());
        }
        [Test]public void PowerBoxReplacesSphereStaysVisibleAndRestoresItsIndicatorOnRestart()
        {
            var power=mission.powerBox;Assert.NotNull(power);Assert.AreSame(mission.objectives[0],power.transform);
            Assert.IsNull(power.GetComponent<SphereCollider>());Assert.IsNull(power.GetComponent<MeshRenderer>());
            Assert.NotNull(power.solidCollider);Assert.False(power.solidCollider.isTrigger);Assert.AreEqual(8,power.gameObject.layer);
            Assert.True(power.statusLamp.transform.IsChildOf(power.transform));Assert.True(power.statusPlate.transform.IsChildOf(power.transform));
            Assert.False(root.GetComponentsInChildren<TextMesh>(true).Any(t=>t.text=="POWER"),"No detached floating POWER label.");
            Assert.False(power.IsPowered);Assert.AreSame(power.standbyMaterial,power.statusLamp.sharedMaterial);
            Assert.AreEqual("POWER / OFFLINE",power.statusPlate.text);
            MovePlayer(power.transform.position);Assert.True(mission.TryInteract());Assert.AreEqual(1,mission.Stage);
            Assert.True(power.IsPowered);Assert.True(power.gameObject.activeSelf);Assert.True(power.solidCollider.enabled);
            Assert.AreSame(power.poweredMaterial,power.statusLamp.sharedMaterial);Assert.AreEqual("POWER / ONLINE",power.statusPlate.text);
            Assert.False(mission.TryInteract(),"A second press at power must not skip to manifest.");
            session.Restart();Assert.AreEqual(0,mission.Stage);Assert.True(power.gameObject.activeSelf);Assert.False(power.IsPowered);
            Assert.AreSame(power.standbyMaterial,power.statusLamp.sharedMaterial);Assert.AreEqual("POWER / OFFLINE",power.statusPlate.text);
        }
        [Test]public void PowerBoxHasActualSolidCollisionAndInteractionDistanceComesFromTheCabinet()
        {
            var power=mission.powerBox;var box=power.solidCollider;Physics.SyncTransforms();
            var front=box.bounds.min.z;MovePlayer(new Vector3(box.bounds.center.x,0,front-2.1f));
            Assert.False(mission.TryInteract(),"Outside the physical cabinet's 2m reach.");
            var start=session.player.transform.position;
            for(var i=0;i<180;i++)session.player.Simulate(new PrototypeInputFrame(moveY:1),false,1f/60);
            Assert.Greater(session.player.transform.position.z,start.z+1);
            Assert.Less(session.player.transform.position.z,front-.20f,"Actual controller must not walk through the power box.");
            Assert.True(power.WithinReach(session.player.transform.position));Assert.True(mission.TryInteract());
        }
        [Test]public void PausedDeadAndRemotePlayerCannotUseKioskAndRestartRestoresInactiveGuardsWithoutDetachingWeapons()
        {
            Assert.False(mission.TryInteract());MovePlayer(mission.objectives[0].position);session.SetPaused(true);Assert.False(mission.TryInteract());
            session.SetPaused(false);session.player.Damage(100);Assert.False(mission.TryInteract());session.Restart();
            Assert.AreEqual(100,session.player.Health);Assert.AreEqual(6,session.player.motion.Ammo);Assert.AreEqual(0,mission.Stage);
            Assert.True(session.enemies.All(e=>!e.gameObject.activeSelf&&e.Health==3&&e.weapon.IsChildOf(root.transform)));
            Assert.True(mission.objectives[0].gameObject.activeSelf);Assert.True(mission.objectives[1].gameObject.activeSelf);
            Assert.False(mission.manifestTerminal.Available);Assert.False(mission.manifestTerminal.Used);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void RealControllerCanWalkAuthoredStaticRouteAndReset(int rate)
        {
            var player=session.player;var travelled=0f;
            for(var stage=0;stage<3;stage++) {
                // Clear the required encounter before walking to its kiosk; frozen living guards are dynamic capsule blockers.
                // Direct damage isolates route/progression testing and is deliberately not a combat-playthrough claim.
                if(stage==1){session.enemies[0].Damage(3);session.enemies[1].Damage(3);}
                if(stage==2)session.enemies[2].Damage(3);
                var route=session.Navigation.Find(player.transform.position,mission.objectives[stage].position);Assert.IsNotEmpty(route);
                foreach(var point in route) {
                    var reached=false;
                    for(var frame=0;frame<rate*3;frame++) {
                        var delta=point-player.transform.position;delta.y=0;
                        if(delta.magnitude<.13f){reached=true;break;}
                        var move=delta.normalized;var before=player.transform.position;
                        player.Simulate(new PrototypeInputFrame(moveX:move.x,moveY:move.z),false,1f/rate);
                        travelled+=Vector3.Distance(before,player.transform.position);
                    }
                    Assert.True(reached,"Actual controller must walk each authored waypoint without tunnelling/teleporting. Stage "+stage+" target "+point+" actual "+player.transform.position);
                }
                Assert.True(mission.TryInteract());
            }
            Assert.Greater(travelled,45);Assert.True(mission.Completed);
            session.Restart();Assert.AreEqual(0,mission.Stage);Assert.False(session.Finished);
        }
        [Test]public void GreyboxPlacementIsExplicitAndSpawnAndAllObjectiveRoutesHaveClearance()
        {
            Assert.AreEqual(10,root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)));
            Assert.IsEmpty(root.GetComponentsInChildren<IndustrialPropIdentity>(),"Unreviewed source props may not silently replace the greybox.");
            Assert.False(Physics.CheckSphere(session.player.transform.position+Vector3.up*.9f,.35f,1<<8,QueryTriggerInteraction.Ignore));
            var previous=session.player.transform.position;
            foreach(var point in mission.objectives){Assert.IsNotEmpty(session.Navigation.Find(previous,point.position));previous=point.position;}
            foreach(var collider in root.GetComponentsInChildren<Collider>())Assert.False(collider.enabled&&collider.isTrigger&&collider.gameObject.layer==8);
        }
        [Test]public void AllThreePhysicalTerminalsStayVisibleAndRespectEncounterState()
        {
            Assert.NotNull(mission.manifestTerminal);Assert.NotNull(mission.extractionTerminal);
            Assert.True(mission.objectives.All(t=>t.gameObject.activeSelf));Assert.True(mission.objectives.All(t=>t.GetComponent<SphereCollider>()==null));
            MovePlayer(mission.objectives[0].position);Assert.True(mission.TryInteract());Assert.False(mission.manifestTerminal.Available);
            session.enemies[0].Damage(3);session.enemies[1].Damage(3);mission.RefreshTerminalStates();Assert.True(mission.manifestTerminal.Available);
            MovePlayer(mission.objectives[1].position);Assert.True(mission.TryInteract());Assert.True(mission.manifestTerminal.Used);Assert.False(mission.extractionTerminal.Available);
            session.enemies[2].Damage(3);mission.RefreshTerminalStates();Assert.True(mission.extractionTerminal.Available);
            MovePlayer(mission.objectives[2].position);Assert.True(mission.TryInteract());Assert.True(mission.extractionTerminal.Used);
            Assert.True(mission.objectives.All(t=>t.gameObject.activeSelf));session.Restart();
            Assert.False(mission.manifestTerminal.Used);Assert.False(mission.extractionTerminal.Used);
        }
        [Test]public void CheckpointRoundtripRestoresProgressVitalsAmmoAndPhysicalTerminals()
        {
            var options=session.options;Assert.NotNull(options);options.StartNewGame();
            MovePlayer(mission.objectives[0].position);Assert.True(mission.TryInteract());session.enemies[0].Damage(3);session.enemies[1].Damage(3);
            MovePlayer(mission.objectives[1].position);Assert.True(mission.TryInteract());
            session.player.Damage(14);session.player.motion.RestoreMagazine(2);Assert.True(options.SaveCheckpoint());
            var saved=session.player.transform.position;session.Restart();Assert.AreEqual(0,mission.Stage);
            Assert.True(options.TryLoadCheckpoint());Assert.AreEqual(2,mission.Stage);Assert.AreEqual(2,session.Kills);
            Assert.AreEqual(86,session.player.Health);Assert.AreEqual(2,session.player.motion.Ammo);Assert.AreEqual(60,session.player.Reserve);
            Assert.Less(Vector3.Distance(saved,session.player.transform.position),.001f);Assert.True(mission.powerBox.IsPowered);
            Assert.True(mission.manifestTerminal.Used);Assert.True(session.enemies[2].gameObject.activeSelf);Assert.False(mission.Completed);
            Assert.True(File.Exists(Path.Combine(options.StoreRoot,"checkpoint.json.previous")));
        }
        [Test]public void CorruptWrongProfileAndBlockedCheckpointCannotMutateCurrentGame()
        {
            var options=session.options;options.StartNewGame();Assert.True(options.CheckpointCompletion.GetAwaiter().GetResult());var file=Path.Combine(options.StoreRoot,"checkpoint.json");var good=File.ReadAllText(file);
            foreach(var json in new[]{"{broken",good.Replace(DemoStateRules.Profile,"unrelated-profile"),"{}"}) {
                File.WriteAllText(file,json);Assert.False(options.TryLoadCheckpoint());Assert.AreEqual(100,session.player.Health);Assert.AreEqual(0,mission.Stage);
            }
            var blocked=JsonUtility.FromJson<DemoCheckpoint>(good);var p=mission.powerBox.solidCollider.bounds.center;
            blocked.x=p.x;blocked.y=0;blocked.z=p.z;File.WriteAllText(file,JsonUtility.ToJson(blocked));
            Assert.False(options.TryLoadCheckpoint());Assert.AreEqual(0,mission.Stage);
        }
        [Test]public void OccupiedAtomicTemporaryPreservesPreviousCheckpointAndSettingsStayValidated()
        {
            var options=session.options;options.StartNewGame();Assert.True(options.CheckpointCompletion.GetAwaiter().GetResult());var file=Path.Combine(options.StoreRoot,"checkpoint.json");var before=File.ReadAllText(file);
            File.WriteAllText(file+".new","retained interrupted write");Assert.False(options.SaveCheckpoint());Assert.AreEqual(before,File.ReadAllText(file));
            Assert.AreEqual("retained interrupted write",File.ReadAllText(file+".new"));
            Assert.False(options.SetSettings(new DemoSettings{sensitivity=float.NaN},true));Assert.AreEqual(1.7f,options.Settings.sensitivity);
            Assert.True(options.SetSettings(new DemoSettings{sensitivity=2.3f,fieldOfView=84,invertY=true,largeText=true,masterVolume=0},true));
            var settings=JsonUtility.FromJson<DemoSettings>(File.ReadAllText(Path.Combine(options.StoreRoot,"settings.json")));
            Assert.AreEqual(2.3f,settings.sensitivity);Assert.True(settings.invertY);Assert.True(settings.largeText);Assert.AreEqual(84,session.player.view.GetComponent<Camera>().fieldOfView);
            Assert.That(options.StoreRoot,Does.Contain("ForgeCoreDemoTests-"));Assert.That(options.StoreRoot,Does.Not.Contain("ForgeCoreIndustrialDemo-v1"));
        }
        [Test]public void AudioBankIsSourceBoundedSpatialAndPausedWithoutUnboundedVoiceCreation()
        {
            var audio=session.audio;Assert.NotNull(audio);audio.Initialize();audio.SetPaused(false);Assert.AreEqual(6,audio.clips.Length);
            Assert.AreEqual(DemoAudioSynthesis.Recipe,audio.audioRecipe);Assert.AreEqual(34,audio.variationClips.Length);
            Assert.AreEqual(RecordedFootstepBank.Id,audio.recordedFootstepBankId);
            Assert.AreEqual(DemoAudioSynthesis.ClipKeys.Concat(RecordedFootstepBank.Keys).ToArray(),audio.variationKeys);
            foreach(var clip in audio.variationClips) {Assert.AreEqual(48000,clip.frequency);Assert.AreEqual(1,clip.channels);
                var data=new float[clip.samples];Assert.True(clip.GetData(data,0));Assert.True(data.Any(v=>Mathf.Abs(v)>.005f));Assert.True(data.All(v=>!float.IsNaN(v)&&Mathf.Abs(v)<.87f));}
            var before=root.GetComponentsInChildren<AudioSource>().Length;
            for(var i=0;i<40;i++)audio.Play("shot",Vector3.zero,true);
            Assert.LessOrEqual(audio.ActiveVoices,16);Assert.Greater(audio.Events,0);Assert.Greater(audio.Dropped,0);
            Assert.AreEqual(before,root.GetComponentsInChildren<AudioSource>().Length);Assert.LessOrEqual(before,17);
            audio.SetPaused(true);Assert.False(audio.Play("shot",Vector3.zero));audio.SetPaused(false);Assert.False(audio.Play("unknown",Vector3.zero));
            Assert.True(root.GetComponentsInChildren<AudioSource>().All(s=>s.mute));
        }
        [Test]public void GeneratedGroundSelectsSurfaceVariationsWithoutGuessingMaterialNames()
        {
            var feet=mission.objectives[0].position;feet.y=.03f;Assert.AreEqual(DemoFootstepSurface.Asphalt,DemoAudioSurface.Under(feet));
            var ground=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Lab ground").GetComponent<DemoAudioSurface>();Assert.NotNull(ground);
            ground.surface=DemoFootstepSurface.Metal;Assert.AreEqual(DemoFootstepSurface.Metal,DemoAudioSurface.Under(feet));
            Assert.True(session.audio.PlayFootstep(feet,false,1));Assert.That(session.audio.LastClipKey,Does.StartWith("footstep-metal-"));
            var first=session.audio.LastClipKey;Assert.True(session.audio.PlayFootstep(feet,false,2));Assert.AreNotEqual(first,session.audio.LastClipKey);
            ground.surface=(DemoFootstepSurface)999;Assert.AreEqual(DemoFootstepSurface.Concrete,DemoAudioSurface.Under(feet));
            session.SetPaused(true);Assert.False(session.audio.PlayFootstep(feet,false,3));
        }
        [Test]public void RealHardFloorTakesAreSelectedAndMissingRecordedVariantsDoNotSilentlyBecomeSynthetic()
        {
            var feet=mission.objectives[0].position;feet.y=.03f;var audio=session.audio;audio.Initialize();audio.SetPaused(false);
            var before=audio.RecordedFootstepEvents;
            for(var i=0;i<6;i++){Assert.True(audio.PlayFootstep(feet,false,i));Assert.AreEqual(RecordedFootstepBank.Key(DemoFootstepSurface.Asphalt,i),audio.LastClipKey);}
            Assert.AreEqual(before+6,audio.RecordedFootstepEvents);
            var ground=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Lab ground").GetComponent<DemoAudioSurface>();
            ground.surface=DemoFootstepSurface.Concrete;Assert.True(audio.PlayFootstep(feet,false,0));Assert.AreEqual(RecordedFootstepBank.Keys[0],audio.LastClipKey);
            var index=Array.IndexOf(audio.variationKeys,RecordedFootstepBank.Keys[0]);var clip=audio.variationClips[index];
            try{audio.variationClips[index]=null;var events=audio.Events;Assert.False(audio.PlayFootstep(feet,false,0));Assert.AreEqual(events,audio.Events);}
            finally{audio.variationClips[index]=clip;}
        }
        [Test]public void GraphicsPresetsAreDistinctAndTelemetryHasABoundedWindow()
        {
            var shadows=QualitySettings.shadows;var resolution=QualitySettings.shadowResolution;var distance=QualitySettings.shadowDistance;
            var aa=QualitySettings.antiAliasing;var lights=QualitySettings.pixelLightCount;var mip=QualitySettings.globalTextureMipmapLimit;
            var vsync=QualitySettings.vSyncCount;var cap=Application.targetFrameRate;
            try {DemoQuality.Apply(0,30);Assert.AreEqual(ShadowQuality.Disable,QualitySettings.shadows);Assert.AreEqual(0,QualitySettings.antiAliasing);Assert.AreEqual(30,Application.targetFrameRate);
                DemoQuality.Apply(1,60);Assert.AreEqual(ShadowQuality.HardOnly,QualitySettings.shadows);Assert.AreEqual(35,QualitySettings.shadowDistance);
                DemoQuality.Apply(2,120);Assert.AreEqual(ShadowQuality.All,QualitySettings.shadows);Assert.AreEqual(4,QualitySettings.antiAliasing);Assert.AreEqual(120,Application.targetFrameRate);
                Assert.NotNull(root.GetComponent<DemoRuntimeTelemetry>());Assert.AreSame(session,root.GetComponent<DemoRuntimeTelemetry>().Session);
                var w=new DemoFrameWindow();for(var i=0;i<5000;i++)w.Add(16.7);Assert.AreEqual(4096,w.Snapshot().windowFrames);
            }finally{QualitySettings.shadows=shadows;QualitySettings.shadowResolution=resolution;QualitySettings.shadowDistance=distance;QualitySettings.antiAliasing=aa;
                QualitySettings.pixelLightCount=lights;QualitySettings.globalTextureMipmapLimit=mip;QualitySettings.vSyncCount=vsync;Application.targetFrameRate=cap;}
        }
        [Test]public void LegacyJsonSettingsKeepGunshotLevelsAndNewMixControlsStayIndependent()
        {
            var legacy=JsonUtility.FromJson<DemoSettings>("{\"schemaVersion\":1,\"quality\":1,\"frameLimit\":60,\"sensitivity\":2,\"fieldOfView\":75,\"masterVolume\":0.4}");
            var migrated=DemoStateRules.UpgradeSettings(legacy);Assert.True(DemoStateRules.SettingsValid(migrated));Assert.AreEqual(1,migrated.effectsVolume);Assert.AreEqual(1,migrated.footstepsVolume);
            Assert.True(session.options.SetSettings(migrated,false));var audio=session.audio;audio.Initialize();audio.SetPaused(false);
            Assert.True(audio.Play("shot",Vector3.zero,false));var shot=root.GetComponentsInChildren<AudioSource>().Single(s=>s.clip==audio.variationClips[0]);Assert.AreEqual(.75f,shot.volume,.0001f);
            migrated.footstepsVolume=.25f;migrated.ambienceVolume=.3f;migrated.interfaceVolume=.1f;Assert.True(session.options.SetSettings(migrated,true));
            Assert.True(audio.PlayFootstep(session.player.transform.position,false,0));
            var step=audio.variationClips[Array.IndexOf(audio.variationKeys,audio.LastClipKey)];var foot=root.GetComponentsInChildren<AudioSource>().Single(s=>s.clip==step);
            Assert.AreEqual(.34f*.25f,foot.volume,.0001f);Assert.AreEqual(.75f,shot.volume,.0001f);
            var stored=JsonUtility.FromJson<DemoSettings>(File.ReadAllText(Path.Combine(session.options.StoreRoot,"settings.json")));Assert.AreEqual(2,stored.schemaVersion);Assert.AreEqual(.25f,stored.footstepsVolume);
        }
        [UnityTest]public IEnumerator AutomaticCheckpointCompletesWithoutBlockingFramesAndPersistsTheCapturedState()
        {
            var options=session.options;options.StartNewGame();session.player.Damage(7);
            var request=options.RequestCheckpoint("Async checkpoint complete");Assert.NotNull(request);
            session.player.Damage(4);var frames=0;var deadline=Time.realtimeSinceStartup+10;
            do{yield return null;frames++;}while(!request.IsCompleted&&Time.realtimeSinceStartup<deadline);
            Assert.True(request.IsCompleted);Assert.True(request.Result);Assert.Greater(frames,0);Assert.False(options.AutosavePending);
            var saved=JsonUtility.FromJson<DemoCheckpoint>(File.ReadAllText(Path.Combine(options.StoreRoot,"checkpoint.json")));
            Assert.AreEqual(93,saved.health);Assert.AreEqual(89,session.player.Health);Assert.True(options.TryLoadCheckpoint());Assert.AreEqual(93,session.player.Health);
        }
        [UnityTest]public IEnumerator NewGameReturnsAndFramesAdvanceWhileItsDurableWriteIsBlocked()
        {
            var options=session.options;options.Initialize();var store=options.StoreRoot;
            using(var entered=new System.Threading.ManualResetEventSlim())
            using(var release=new System.Threading.ManualResetEventSlim()){
                options.SetCheckpointWriterForEditorTest((text,measured)=>{entered.Set();return release.Wait(5000)&&DemoAtomicStateFiles.TryWrite(store,"checkpoint.json",text,measured,out _);});
                var began=System.Diagnostics.Stopwatch.StartNew();options.StartNewGame();var returned=began.Elapsed.TotalMilliseconds;
                try{
                    Assert.Less(returned,100,"New game must enqueue, not wait for the blocked writer.");
                    Assert.True(entered.Wait(5000));Assert.True(options.AutosavePending);Assert.That(options.CheckpointStatus,Does.Contain("Saving"));
                    session.player.Damage(7);var frame=Time.frameCount;
                    Assert.False(options.PrepareQuit());Assert.AreEqual(DemoCheckpointExitState.Waiting,options.ExitState);Assert.True(session.Paused);
                    for(var i=0;i<3;i++)yield return null;
                    Assert.Greater(Time.frameCount,frame);Assert.True(options.AutosavePending);options.CancelPendingQuit();Assert.False(session.Paused);
                }finally{release.Set();Assert.True(options.CheckpointCompletion.GetAwaiter().GetResult());}
                var saved=JsonUtility.FromJson<DemoCheckpoint>(File.ReadAllText(Path.Combine(store,"checkpoint.json")));
                Assert.AreEqual(100,saved.health);Assert.AreEqual(93,session.player.Health);Assert.False(options.CheckpointFailed);
            }
        }
        [UnityTest]public IEnumerator FailedNewGameSaveKeepsTheDurableCheckpointAndBlocksUnacknowledgedQuit()
        {
            var options=session.options;options.StartNewGame();Assert.True(options.CheckpointCompletion.GetAwaiter().GetResult());
            var file=Path.Combine(options.StoreRoot,"checkpoint.json");var before=File.ReadAllText(file);File.WriteAllText(file+".new","owned interrupted fixture");
            options.StartNewGame();var deadline=Time.realtimeSinceStartup+10;
            while(options.AutosavePending&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.False(options.AutosavePending);Assert.False(options.CheckpointCompletion.Result);Assert.True(options.CheckpointFailed);
            Assert.That(options.CheckpointStatus,Does.Contain("previous checkpoint kept"));Assert.AreEqual(before,File.ReadAllText(file));Assert.AreEqual("owned interrupted fixture",File.ReadAllText(file+".new"));
            Assert.False(options.PrepareQuit());Assert.AreEqual(DemoCheckpointExitState.Blocked,options.ExitState);options.CancelPendingQuit();
        }
        [Test]public void RuntimeTelemetryRecordsNamedScopesWithoutChangingItsFrameBudget()
        {
            var telemetry=root.GetComponent<DemoRuntimeTelemetry>();Assert.NotNull(telemetry);telemetry.ResetMeasurement();
            using(var measured=DemoRuntimeTelemetry.Measure(DemoWorkKind.StateWrite)){}
            Assert.That(telemetry.WorkSnapshot().statistics.Single(s=>s.kind=="StateWrite").count,Is.EqualTo(1));
            Assert.LessOrEqual(telemetry.WorkSnapshot().recent.Length,DemoWorkJournal.Capacity);Assert.IsEmpty(telemetry.Spikes());
        }
    }
}
""";
}
