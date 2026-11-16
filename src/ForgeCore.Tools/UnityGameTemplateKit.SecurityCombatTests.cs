namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SecurityCombatTestsSource = """
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class SecurityCombatTests
    {
        private GameObject root;private SecurityCombatSession session;
        [UnitySetUp]public IEnumerator Setup()
        {
#if UNITY_EDITOR
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"})
                .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/CombatLab.prefab",StringComparison.Ordinal))
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            Assert.NotNull(path,"Generate the S06 isolated lab before running its integration tests.");
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());
            root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            session=root.GetComponentInChildren<SecurityCombatSession>();session.enabled=false;session.SetPaused(true);
            yield return null;
            session.player.enabled=false;foreach(var e in session.enemies)e.enabled=false;
            foreach(var motion in root.GetComponentsInChildren<PrototypeSkinnedAnimator>())motion.enabled=false;
            session.SetPaused(false);Physics.SyncTransforms();session.Navigation.Refresh();
#else
            Assert.Fail("Editor PlayMode runner required.");yield return null;
#endif
        }
        [TearDown]public void Cleanup()
        {
            PrototypeInput.ClearScriptedFrame();
            foreach(var b in UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(b.gameObject);
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);
        }
        [Test]public void GridRoutesAroundWallAndNeverWrapsRowsOrReturnsBlockedGoal()
        {
            var nav=new SecurityGridPathfinder(5,5,Vector3.zero,1,p=>Mathf.RoundToInt(p.x)==2&&p.z<4);
            var route=nav.Find(Vector3.zero,new Vector3(4,0,0));
            Assert.That(route.Count,Is.GreaterThan(4));Assert.That(route.Any(p=>p.z==4),Is.True);
            Assert.That(route.All(p=>!(p.x==2&&p.z<4)),Is.True);Assert.LessOrEqual(nav.LastExpanded,25);
            Assert.IsEmpty(nav.Find(Vector3.zero,new Vector3(2,0,0)));
            Assert.IsEmpty(nav.Find(Vector3.zero,new Vector3(99,0,0)));
        }
        [Test]public void FirstPersonUsesRealSkinAndExclusiveActionsWithPartialReserveCommit()
        {
            var player=session.player;var motion=player.motion;
            Assert.IsNotNull(player.arms.GetComponentInChildren<SkinnedMeshRenderer>());
            Assert.False(motion.driveRootTranslation);Assert.False(motion.emitTrainingProjectiles);
            Assert.True(motion.TryFire());Assert.False(motion.TryReload());motion.Tick(.3f);motion.FlushShotMarkerForTest();
            Assert.AreEqual(5,motion.Ammo);motion.AvailableReloadRounds=1;Assert.True(motion.TryReload());
            motion.Tick(1);Assert.AreEqual(5,motion.Ammo);motion.Tick(.7f);Assert.AreEqual(6,motion.Ammo);
            motion.TryFire();motion.Tick(.3f);motion.TryReload();motion.Tick(.5f);player.Damage(7);motion.Tick(.5f);
            Assert.AreEqual(5,motion.Ammo,"Interrupted reload must grant nothing.");
        }
        [Test]public void PauseFreezesNavigationBulletsAndActionTime()
        {
            var enemy=session.enemies[0];var position=enemy.transform.position;
            var bullet=SecurityCombatBullet.Launch(Vector3.up*3,Vector3.forward,session.player.transform,false);Assert.NotNull(bullet);
            var bulletPosition=bullet.transform.position;enemy.motion.TryFire();session.SetPaused(true);
            enemy.Tick(.2f,session);enemy.motion.Tick(.2f);bullet.Advance(.2f);
            Assert.AreEqual(position,enemy.transform.position);Assert.AreEqual(bulletPosition,bullet.transform.position);Assert.AreEqual(0,enemy.motion.ActionTime);
        }
        [Test]public void CoverBlocksSightAndSweptProjectileDamageAndResetClearsDeaths()
        {
            var enemy=session.enemies[0];var controller=enemy.GetComponent<CharacterController>();controller.enabled=false;
            enemy.transform.position=new Vector3(0,.02f,1.5f);controller.enabled=true;Physics.SyncTransforms();
            Assert.False(enemy.CanSee(session.player.transform.position+Vector3.up,session.player.transform),"Solid divider must block player sight.");
            var bullet=SecurityCombatBullet.Launch(new Vector3(0,1,-4),Vector3.forward,session.player.transform,false);
            bullet.Advance(.2f);Assert.AreEqual(3,enemy.Health,"Swept projectile must stop on cover, not damage the enemy behind it.");
            enemy.Damage(3);Assert.AreEqual(0,enemy.Health);Assert.True(enemy.motion.Dead);Assert.False(controller.enabled);
            Assert.False(enemy.motion.TryFire());session.Restart();
            Assert.AreEqual(3,enemy.Health);Assert.False(enemy.motion.Dead);Assert.True(controller.enabled);Assert.AreEqual(0,session.Kills);
        }
        [Test]public void MultipleEnemiesHaveOneMovementOwnerAndPathAroundStaticObstacles()
        {
            var before=session.enemies.Select(e=>e.transform.position).ToArray();
            for(var frame=0;frame<120;frame++)foreach(var enemy in session.enemies){enemy.Tick(1f/60,session);enemy.motion.Tick(1f/60);}
            Assert.Greater(session.PathSearches,0);
            for(var i=0;i<session.enemies.Length;i++)
            {
                var enemy=session.enemies[i];Assert.False(enemy.motion.driveRootTranslation);Assert.False(enemy.motion.animator.applyRootMotion);
                Assert.LessOrEqual(Vector3.Distance(before[i],enemy.transform.position),4);
                Assert.IsFalse(Physics.CheckSphere(enemy.transform.position+Vector3.up,.25f,1<<8,QueryTriggerInteraction.Ignore),"Guard may not enter wall geometry.");
            }
        }
        [Test]public void MuzzleLineIsRecheckedAndActualProjectileDamagesOnArrival()
        {
            var enemy=session.enemies[0];var cc=enemy.GetComponent<CharacterController>();cc.enabled=false;
            enemy.transform.position=new Vector3(8,.02f,-9);cc.enabled=true;Physics.SyncTransforms();
            var start=new Vector3(8,1,-12);Assert.True(SecurityCombatBullet.FirstHitIsTarget(start,enemy.transform.position+Vector3.up,session.player.transform,enemy.transform));
            var bullet=SecurityCombatBullet.Launch(start,Vector3.forward,session.player.transform,false);Assert.NotNull(bullet);
            Assert.AreEqual(3,enemy.Health);bullet.Advance(.02f);Assert.AreEqual(3,enemy.Health);
            bullet.Advance(.04f);Assert.AreEqual(2,enemy.Health);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void FirstPersonGripAndMuzzleStayAlignedAcrossSupportedSimulationRates(int frameRate)
        {
            var player=session.player;var motion=player.motion;
            var right=player.arms.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.R");
            var left=player.arms.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.L");
            motion.SetLocomotion(0,0,true);
            for(var i=0;i<frameRate/2;i++){player.Simulate(new PrototypeInputFrame(),false,1f/frameRate);motion.Tick(1f/frameRate);motion.animator.Update(1f/frameRate);player.ApplyHandContacts();}
            Assert.Greater(Vector3.Dot(motion.muzzle.forward,player.view.forward),.99f,"Weapon must point down the camera axis, not inherit wrist drift.");
            Assert.Less(Vector3.Distance(right.position,player.rightGrip.position),.025f);
            Assert.Less(Vector3.Distance(left.position,player.leftGrip.position),.025f);
            motion.TryFire();motion.Tick(.3f);player.Simulate(new PrototypeInputFrame(reloadPressed:true),false,1f/frameRate);
            for(var i=0;i<frameRate;i++){motion.Tick(1f/frameRate);motion.animator.Update(1f/frameRate);player.Simulate(new PrototypeInputFrame(),false,1f/frameRate);player.ApplyHandContacts();
                Assert.Less(Vector3.Distance(right.position,player.rightGrip.position),.025f);Assert.Less(Vector3.Distance(left.position,player.leftGrip.position),.025f);}
        }
        [Test]public void AnimatedGuardActuallyFiresAfterFacingAndProjectileArrivalDamagesPlayer()
        {
            var enemy=session.enemies[0];Assert.True(enemy.CanSee(session.player.transform.position+Vector3.up*1.1f,session.player.transform));
            for(var frame=0;frame<210;frame++)
            {
                enemy.Tick(1f/60,session);enemy.motion.Tick(1f/60);enemy.motion.animator.Update(1f/60);enemy.motion.FlushShotMarkerForTest();
                foreach(var bullet in UnityEngine.Object.FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))bullet.Advance(1f/60);
            }
            Assert.Greater(enemy.Shots,0,"Must exercise the actual AI -> animation marker -> rechecked muzzle -> swept projectile route.");
            Assert.Less(session.player.Health,100,"Player damage must occur after projectile arrival, not just a changed animation parameter.");
        }
        [Test]public void ReloadHasWithdrawalHoldInsertionAndRestBeforeAmmoCommit()
        {
            Assert.AreEqual(0,SecurityWeaponPose.MagazineTravel(0));
            Assert.AreEqual(1,SecurityWeaponPose.MagazineTravel(.7f),.0001f);
            Assert.Less(SecurityWeaponPose.MagazineTravel(1.15f),1);
            Assert.AreEqual(0,SecurityWeaponPose.MagazineTravel(1.5f),.0001f);
            var player=session.player;var motion=player.motion;
            motion.TryFire();motion.Tick(.3f);player.Simulate(new PrototypeInputFrame(reloadPressed:true),false,0);
            motion.Tick(.7f);player.Simulate(new PrototypeInputFrame(),false,0);
            var withdrawn=player.magazine.localPosition;
            motion.Tick(.8f);player.Simulate(new PrototypeInputFrame(),false,0);
            Assert.Greater(player.magazine.localPosition.y,withdrawn.y+.15f);Assert.AreEqual(5,motion.Ammo);
            motion.Tick(.2f);player.Simulate(new PrototypeInputFrame(),false,0);
            Assert.AreEqual(6,motion.Ammo);Assert.AreEqual(59,player.Reserve);
            motion.TryFire();motion.Tick(.3f);player.Simulate(new PrototypeInputFrame(reloadPressed:true),false,0);
            motion.Tick(.7f);player.Simulate(new PrototypeInputFrame(),false,0);player.Damage(1);
            player.Simulate(new PrototypeInputFrame(),false,0);
            Assert.AreEqual(59,player.Reserve);Assert.AreEqual(5,motion.Ammo);
            Assert.AreEqual(0,Vector3.Distance(player.magazine.localPosition,new Vector3(0,-.11f,.015f)),.0001f);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void AimSmoothingIsTimeBasedAndReloadUnseatsAim(int rate)
        {
            var p=session.player;
            p.Simulate(new PrototypeInputFrame(),true,1f/rate);
            Assert.Greater(p.AimBlend,0);Assert.Less(p.AimBlend,.5f,"Aim must not snap in one frame.");
            for(var i=1;i<rate/2;i++)p.Simulate(new PrototypeInputFrame(),true,1f/rate);
            Assert.AreEqual(1-Mathf.Exp(-7),p.AimBlend,.0001f);
            p.motion.TryFire();p.motion.Tick(.3f);p.Simulate(new PrototypeInputFrame(reloadPressed:true),true,.5f);
            Assert.Less(p.AimBlend,.002f);session.Restart();Assert.AreEqual(0,p.AimBlend);
        }
        [Test]public void CombatDeviceBindingsSeparateSpaceFromMouseWithoutChangingLegacyFrames()
        {
            var legacy=new PrototypeInputFrame(jumpPressed:true,firePressed:true,fireHeld:true,sprintHeld:true);
            var combat=PrototypeInput.CombatBindings(legacy,false,false);
            Assert.True(legacy.FireHeld);Assert.True(combat.JumpPressed);Assert.True(combat.SprintHeld);
            Assert.False(combat.FireHeld);Assert.False(combat.FirePressed);
            combat=PrototypeInput.CombatBindings(legacy,true,true);Assert.True(combat.FireHeld);Assert.True(combat.FirePressed);
            PrototypeInput.SetScriptedFrame(combat);Assert.True(PrototypeInput.ReadSecurityCombat().FireHeld);
        }
        private void SettlePlayer(int rate)
        {for(var i=0;i<rate/2;i++)session.player.Simulate(new PrototypeInputFrame(),false,1f/rate);}
        [TestCase(30)][TestCase(60)][TestCase(120)]public void SprintIsFasterAndAimAndBackpedallingOverrideIt(int rate)
        {
            var p=session.player;SettlePlayer(rate);var start=p.transform.position;
            for(var i=0;i<rate/4;i++)p.Simulate(new PrototypeInputFrame(moveY:1),false,1f/rate);
            var walk=Vector3.Distance(start,p.transform.position);session.Restart();SettlePlayer(rate);start=p.transform.position;
            for(var i=0;i<rate/4;i++)p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true),false,1f/rate);
            Assert.True(p.IsSprinting);Assert.AreEqual(6.5f/4,Vector3.Distance(start,p.transform.position)/walk,.025f);
            p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true),true,1f/rate);Assert.False(p.IsSprinting);
            p.Simulate(new PrototypeInputFrame(moveY:-1,sprintHeld:true),false,1f/rate);Assert.False(p.IsSprinting);
            p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true,fireHeld:true),false,1f/rate);Assert.False(p.IsSprinting);
            p.motion.Tick(.3f);
            p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true,reloadPressed:true),false,1f/rate);Assert.False(p.IsSprinting);
            p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true),false,1f/rate);Assert.False(p.IsSprinting);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void SpaceJumpsOnceLandsAndDoesNotConsumeAmmo(int rate)
        {
            var p=session.player;SettlePlayer(rate);Assert.True(p.IsGrounded);var ground=p.transform.position.y;var ammo=p.motion.Ammo;
            var apex=ground;
            for(var i=0;i<rate*2;i++) {
                p.Simulate(PrototypeInput.CombatBindings(new PrototypeInputFrame(jumpPressed:true,firePressed:true,fireHeld:true),false,false),false,1f/rate);
                p.motion.Tick(1f/rate);apex=Mathf.Max(apex,p.transform.position.y);
                if(i>rate)Assert.Less(p.transform.position.y,ground+.04f,"Holding jump may not bounce after landing.");
            }
            Assert.Greater(apex-ground,.85f);Assert.Less(apex-ground,1.1f);Assert.True(p.IsGrounded);Assert.AreEqual(ammo,p.motion.Ammo);
            p.Simulate(new PrototypeInputFrame(),false,1f/rate);
            p.Simulate(new PrototypeInputFrame(jumpPressed:true),false,1f/rate);Assert.Greater(p.VerticalVelocity,0);
            var position=p.transform.position;var velocity=p.VerticalVelocity;session.SetPaused(true);
            p.Simulate(new PrototypeInputFrame(),false,.5f);Assert.AreEqual(position,p.transform.position);Assert.AreEqual(velocity,p.VerticalVelocity);
            session.Restart();Assert.AreEqual(0,p.VerticalVelocity);Assert.False(p.IsSprinting);
        }
        [Test]public void JumpCannotResetVerticalVelocityInAirAndDeathFreezesTravel()
        {
            var p=session.player;SettlePlayer(60);
            p.Simulate(new PrototypeInputFrame(jumpPressed:true),false,1f/60);
            p.Simulate(new PrototypeInputFrame(),false,1f/60);Assert.False(p.IsGrounded);
            var velocity=p.VerticalVelocity;
            p.Simulate(new PrototypeInputFrame(jumpPressed:true),false,1f/60);
            Assert.Less(p.VerticalVelocity,velocity,"A second Space edge in air must not grant another impulse.");
            p.Damage(100);var position=p.transform.position;
            p.Simulate(new PrototypeInputFrame(moveY:1,sprintHeld:true,jumpPressed:true),false,.5f);
            Assert.AreEqual(position,p.transform.position);session.Restart();Assert.AreEqual(0,p.VerticalVelocity);
        }
        [Test]public void JumpStopsAtCeiling()
        {
            var p=session.player;SettlePlayer(60);var ground=p.transform.position.y;
            var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.layer=8;
            ceiling.transform.position=p.transform.position+Vector3.up*2.15f;ceiling.transform.localScale=new Vector3(2,.1f,2);
            try {
                Physics.SyncTransforms();var apex=ground;
                for(var i=0;i<120;i++) {
                    p.Simulate(new PrototypeInputFrame(jumpPressed:i%2==0),false,1f/60);
                    apex=Mathf.Max(apex,p.transform.position.y);
                    if(i>45)break;
                }
                Assert.Less(apex-ground,.4f,"Controller must stop against the ceiling rather than pass through it.");
            }finally{UnityEngine.Object.DestroyImmediate(ceiling);}
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void PhysicalSightIsCentredWhileAimingTurningFiringAndNearCover(int rate)
        {
            var p=session.player;var camera=p.view.GetComponent<Camera>();var hipFov=camera.fieldOfView;
            Assert.NotNull(p.sight);Assert.AreEqual("Sight aim point",p.sight.name);
            for(var i=0;i<rate;i++)p.Simulate(new PrototypeInputFrame(lookX:.1f,lookY:.05f),true,1f/rate);
            AssertSightCentred(p,camera);Assert.AreEqual(60,camera.fieldOfView,.001f);Assert.Greater(p.AimBlend,.999f);
            p.motion.TryFire();p.motion.Tick(.3f);p.motion.FlushShotMarkerForTest();
            p.Simulate(new PrototypeInputFrame(),true,1f/rate);AssertSightCentred(p,camera);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=8;
            wall.transform.position=p.view.position+p.view.forward*.5f;wall.transform.rotation=p.view.rotation;wall.transform.localScale=new Vector3(2,2,.1f);
            try {
                Physics.SyncTransforms();for(var i=0;i<rate/2;i++)p.Simulate(new PrototypeInputFrame(),true,1f/rate);
                Assert.Greater(p.CoverBlend,.7f);AssertSightCentred(p,camera);
            }finally{UnityEngine.Object.DestroyImmediate(wall);}
            for(var i=0;i<rate;i++)p.Simulate(new PrototypeInputFrame(),false,1f/rate);
            Assert.AreEqual(hipFov,camera.fieldOfView,.001f);session.Restart();Assert.AreEqual(hipFov,camera.fieldOfView);
        }
        private static void AssertSightCentred(SecurityCombatPlayer p,Camera camera)
        {
            var point=camera.WorldToViewportPoint(p.sight.position);
            Assert.AreEqual(.5f,point.x,.0001f);Assert.AreEqual(.5f,point.y,.0001f);Assert.Greater(point.z,camera.nearClipPlane);
        }
#if UNITY_EDITOR
        [Test]public void CaptureActualHipAndDownSightCameraEvidence()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Actual image evidence requires a graphics-enabled test run; never render on Unity's Null device.");
            var p=session.player;var camera=p.view.GetComponent<Camera>();SettlePlayer(60);
            var folder=Path.Combine("Previews","ControlsAds-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            SaveCamera(camera,Path.Combine(folder,"hip.png"));
            for(var i=0;i<120;i++){p.Simulate(new PrototypeInputFrame(),true,1f/60);p.motion.animator.Update(1f/60);p.ApplyHandContacts();}
            AssertSightCentred(p,camera);SaveCamera(camera,Path.Combine(folder,"ads.png"));
            Debug.Log("CONTROLS_ADS_CAMERA_EVIDENCE "+folder);
        }
        private static void SaveCamera(Camera camera,string path)
        {
            var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                using(var stream=new FileStream(path,FileMode.CreateNew)) {var bytes=texture.EncodeToPNG();stream.Write(bytes,0,bytes.Length);}
            }finally{camera.targetTexture=prior;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
#endif
        [Test]public void NearCoverRetractsWeaponAndPauseFreezesPose()
        {
            var p=session.player;var start=p.weapon.localPosition;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=8;
            wall.transform.position=p.view.position+p.view.forward*.5f;wall.transform.localScale=new Vector3(2,2,.1f);
            try {
                Physics.SyncTransforms();p.Simulate(new PrototypeInputFrame(),false,.5f);
                Assert.Greater(p.CoverBlend,.7f);Assert.Less(p.weapon.localPosition.z,start.z-.19f);
                var position=p.weapon.localPosition;session.SetPaused(true);
                p.Simulate(new PrototypeInputFrame(),true,.5f);Assert.AreEqual(position,p.weapon.localPosition);
                session.Restart();Assert.AreEqual(0,p.CoverBlend);
            }finally{UnityEngine.Object.DestroyImmediate(wall);}
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void GuardContactsAndBarrelFollowEvaluatedAimAndDeathReleasesPose(int rate)
        {
            var enemy=session.enemies[0];var motion=enemy.motion;
            var right=motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.R");
            var left=motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.L");
            var target=session.player.transform.position+Vector3.up*1.1f;
            for(var frame=0;frame<rate*2;frame++) {
                enemy.Tick(1f/rate,session);motion.Tick(1f/rate);motion.animator.Update(1f/rate);
                // Flush invokes the post-animation contact event before the muzzle shot marker.
                motion.FlushShotMarkerForTest();
                Assert.Less(Vector3.Distance(right.position,enemy.rightGrip.position),.025f);
                Assert.Less(Vector3.Distance(left.position,enemy.leftGrip.position),.025f);
            }
            Assert.Less(Vector3.Angle(motion.muzzle.forward,target-motion.muzzle.position),3);
            Assert.True(motion.TryReload());
            var magazineContactFrames=0;
            for(var frame=0;frame<rate*1.6f;frame++) {
                enemy.Tick(1f/rate,session);motion.Tick(1f/rate);motion.animator.Update(1f/rate);motion.FlushShotMarkerForTest();
                Assert.Less(Vector3.Distance(right.position,enemy.rightGrip.position),.025f,"Right hand during reload");
                Assert.Less(Vector3.Distance(left.position,enemy.SupportHandTarget),.025f,"Support hand must reach the actual magazine target during reload");
                if(motion.Action=="Reload"&&motion.ActionTime>.4f&&motion.ActionTime<1.1f) {
                    Assert.Greater(Vector3.Distance(left.position,enemy.leftGrip.position),.03f,"Reload must visibly leave the foregrip");
                    magazineContactFrames++;
                }
            }
            Assert.Greater(magazineContactFrames,rate/3,"Must exercise magazine contact, not just entry/exit poses");
            enemy.Damage(3);Assert.AreSame(right,enemy.weapon.parent);var shots=enemy.Shots;
            motion.Tick(.5f);motion.animator.Update(.5f);motion.FlushShotMarkerForTest();Assert.AreEqual(shots,enemy.Shots);
            session.Restart();Assert.AreNotSame(right,enemy.weapon.parent);Assert.False(motion.Dead);
        }
        [Test]public void TargetMovingBehindGuardBeforeShotMarkerDoesNotReceiveAutoAimedShot()
        {
            var e=session.enemies[0];e.transform.rotation=Quaternion.LookRotation(session.player.transform.position-e.transform.position);
            e.motion.TryFire();e.motion.Tick(.1f);e.motion.animator.Update(.1f);
            e.transform.rotation=Quaternion.LookRotation(e.transform.position-session.player.transform.position);
            e.motion.FlushShotMarkerForTest();Assert.AreEqual(0,e.Shots);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void DistanceDrivenFeetPlantThroughWalkingAndTurnsWithoutRootMotion(int rate)
        {
            var e=session.enemies[1];var motion=e.motion;var start=e.transform.position;
            var feet=new[]{motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot.L"),motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot.R")};
            var previous=new Vector3[2];var targets=new Vector3[2];var planted=new bool[2];var observations=0;
            for(var frame=0;frame<rate*4;frame++) {
                var heading=e.transform.rotation;var previousSpeed=e.MoveSpeed;
                e.Tick(1f/rate,session);motion.Tick(1f/rate);motion.animator.Update(1f/rate);motion.FlushShotMarkerForTest();
                Assert.LessOrEqual(Quaternion.Angle(heading,e.transform.rotation),120f/rate+.03f);
                Assert.LessOrEqual(e.MoveSpeed-previousSpeed,6f/rate+.001f,"Acceleration must be bounded.");
                Assert.Less(e.PlantError,.025f,"Actual evaluated ankle must meet its planted world target.");
                for(var side=0;side<2;side++) {
                    if(frame>0&&planted[side]&&e.FootPlanted(side)&&Vector3.Distance(targets[side],e.FootTarget(side))<.00001f) {
                        Assert.Less(Vector3.Distance(previous[side],feet[side].position),.008f,"A planted ankle may not slide with the controller.");observations++;
                    }
                    previous[side]=feet[side].position;targets[side]=e.FootTarget(side);planted[side]=e.FootPlanted(side);
                }
                var phase=e.GaitPhase;var position=feet[0].position;e.ApplyWeaponPose();
                Assert.AreEqual(phase,e.GaitPhase);Assert.Less(Vector3.Distance(position,feet[0].position),.002f,"Repeated post-animation solve must be idempotent.");
            }
            Assert.Greater(Vector3.Distance(start,e.transform.position),.5f,"Must exercise actual routed controller travel, not idle poses only.");
            Assert.Greater(observations,rate);Assert.False(motion.driveRootTranslation);Assert.False(motion.animator.applyRootMotion);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void HitStopsHeadingAndTravelDeathGroundsSkinAndResetRestoresVisual(int rate)
        {
            var e=session.enemies[0];var motion=e.motion;
            e.Tick(.15f,session);motion.Tick(.15f);motion.animator.Update(.15f);motion.FlushShotMarkerForTest();
            e.Damage(1);var position=e.transform.position;var heading=e.transform.rotation;
            for(var frame=0;frame<rate/4;frame++) {
                e.Tick(1f/rate,session);motion.Tick(1f/rate);motion.animator.Update(1f/rate);motion.FlushShotMarkerForTest();
                Assert.Less(Vector3.Distance(position,e.transform.position),.001f);Assert.Less(Quaternion.Angle(heading,e.transform.rotation),.01f);
                Assert.AreEqual(0,e.MoveSpeed);Assert.Less(e.PlantError,.025f);
            }
            e.Damage(2);Assert.True(motion.Dead);Assert.False(e.GetComponent<CharacterController>().enabled);
            for(var frame=0;frame<rate*2;frame++){e.Tick(1f/rate,session);motion.Tick(1f/rate);motion.animator.Update(1f/rate);motion.FlushShotMarkerForTest();}
            var skin=motion.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();
            try {
                skin.BakeMesh(mesh);var min=mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);
                Assert.AreEqual(.005f,min,.012f,"Independently baked corpse vertices must meet the actual flat floor.");
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
            Assert.Less(e.CorpseGroundError,.002f);Assert.AreEqual(position,e.transform.position);
            session.Restart();Assert.False(motion.Dead);Assert.AreEqual(0,e.GaitPhase);Assert.AreEqual(Vector3.zero,motion.transform.localPosition);
        }
        private static float BakedTorsoMinY(SkinnedMeshRenderer skin,Mesh mesh)
        {
            skin.BakeMesh(mesh);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;var min=float.PositiveInfinity;
            for(var i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var torso=0f;
                for(var b=0;b<4;b++){var n=skin.bones[ids[b]].name;if(n=="Hips"||n=="Spine"||n=="Chest")torso+=amounts[b];}
                if(torso>.5f)min=Mathf.Min(min,skin.transform.TransformPoint(vertices[i]).y);
            }
            return min;
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]public void CorpseTorsoRestsOnFloorAt30_60_120Hz(int rate)
        {
            var enemy=session.enemies[0];var motion=enemy.motion;var origin=enemy.transform.position;
            var skin=motion.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();enemy.Damage(3);
            try
            {
                for(var frame=0;frame<rate*4;frame++)
                {
                    motion.Tick(1f/rate);motion.animator.Update(1f/rate);motion.FlushShotMarkerForTest();
                    if(frame>rate*2&&frame%(rate/4)==0)
                    {
                        var torso=BakedTorsoMinY(skin,mesh);
                        Assert.That(torso,Is.InRange(-.01f,.015f),"A touching boot must not hide a floating torso.");
                        Assert.That(mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y),Is.InRange(-.01f,.02f));
                    }
                }
                Assert.AreEqual(origin,enemy.transform.position,"The gameplay root must not be teleported.");
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [UnityTest]public IEnumerator AutomaticDeathKeepsBodySupportedAcrossFramesAndPause()
        {
            var enemy=session.enemies[0];var motion=enemy.motion;var skin=motion.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh=new Mesh();motion.enabled=true;enemy.Damage(3);
            try
            {
                yield return new WaitForSeconds(2.5f);
                Assert.That(BakedTorsoMinY(skin,mesh),Is.InRange(-.01f,.015f));
                session.SetPaused(true);yield return new WaitForSeconds(.2f);
                Assert.That(BakedTorsoMinY(skin,mesh),Is.InRange(-.01f,.015f));
                session.Restart();Assert.False(motion.Dead);Assert.AreEqual(Vector3.zero,motion.transform.localPosition);
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test]public void PauseFreezesGaitTargetsAndFootContactThenRestartClearsThem()
        {
            var e=session.enemies[1];var motion=e.motion;
            for(var i=0;i<90;i++){e.Tick(1f/60,session);motion.Tick(1f/60);motion.animator.Update(1f/60);motion.FlushShotMarkerForTest();}
            var phase=e.GaitPhase;var target=e.FootTarget(0);session.SetPaused(true);
            for(var i=0;i<20;i++){e.Tick(1f/60,session);motion.Tick(1f/60);motion.FlushShotMarkerForTest();}
            Assert.AreEqual(phase,e.GaitPhase);Assert.AreEqual(target,e.FootTarget(0));session.Restart();Assert.AreEqual(0,e.GaitPhase);
        }
        [Test]public void LargeTurnCompletesAnAlternatingShuffleInsteadOfLeavingCrossedFeet()
        {
            var e=session.enemies[0];var motion=e.motion;
            var feet=new[]{motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot.L"),motion.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot.R")};
            var signs=feet.Select(f=>Mathf.Sign(e.transform.InverseTransformPoint(f.position).x)).ToArray();
            var rotations=feet.Select(f=>Quaternion.Inverse(e.transform.rotation)*f.rotation).ToArray();
            for(var i=0;i<300;i++){e.Tick(1f/60,session);motion.Tick(1f/60);motion.animator.Update(1f/60);motion.FlushShotMarkerForTest();}
            for(var i=0;i<2;i++) {
                Assert.Greater(e.transform.InverseTransformPoint(feet[i].position).x*signs[i],.04f,"Each foot must settle on its own side of the body.");
                Assert.Less(Quaternion.Angle(feet[i].rotation,e.transform.rotation*rotations[i]),25);
                Assert.True(e.FootPlanted(i));
            }
        }
#if UNITY_EDITOR
        private string VisualFolder()=>Path.GetDirectoryName(AssetDatabase.GetAssetPath(session.player.weapon.GetComponentInChildren<Renderer>().sharedMaterial)).Replace(Path.DirectorySeparatorChar,'/');
        [Serializable]private sealed class VisualReport {
            public string assetPath="",visualProfile="";public string[] visualAssets=Array.Empty<string>();
            public int materialSlotCount=0,textureSize=0,ownedTextureCount=0,reflectionFaceSize=0;public long ownedTextureMemoryBytes=0;
        }
        [Test]public void CharacterFinishVariantsPreserveEveryOriginalSkinMaterialSlot()
        {
            var folder=VisualFolder();var report=JsonUtility.FromJson<VisualReport>(File.ReadAllText(folder+"/combat-report.json"));
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(report.assetPath).GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(source.sharedMaterials.Length,report.materialSlotCount);
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                Assert.AreEqual(source.sharedMaterials.Length,skin.sharedMaterials.Length);Assert.AreEqual(source.sharedMesh.subMeshCount,skin.sharedMesh.subMeshCount);
                foreach(var material in skin.sharedMaterials){Assert.AreEqual("Standard",material.shader.name);Assert.True(AssetDatabase.GetAssetPath(material).StartsWith(folder+"/Guard",StringComparison.Ordinal));}
            }
            Assert.True(source.sharedMaterials.All(m=>!AssetDatabase.GetAssetPath(m).StartsWith(folder+"/",StringComparison.Ordinal)),"Original imported materials may not be replaced in the source prefab.");
            var cloth=AssetDatabase.LoadAssetAtPath<Material>(folder+"/GuardCloth.mat");Assert.NotNull(cloth.mainTexture);Assert.NotNull(cloth.GetTexture("_BumpMap"));
        }
        [Test]public void RifleTrimEmissionSurvivesStandardShaderValidationAndReimport()
        {
            var path=VisualFolder()+"/RifleTrim.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var before=File.ReadAllBytes(path);
            Assert.False((material.globalIlluminationFlags&MaterialGlobalIlluminationFlags.EmissiveIsBlack)!=0);
            Assert.True(material.IsKeywordEnabled("_EMISSION"));
            var guiType=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("UnityEditor",StringComparison.Ordinal))
                .SelectMany(a=>a.GetTypes()).Single(t=>t.Name=="StandardShaderGUI"&&typeof(ShaderGUI).IsAssignableFrom(t));
            var gui=(ShaderGUI)Activator.CreateInstance(guiType,true);gui.ValidateMaterial(material);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path),"Standard validation must not invalidate generated artifact hashes.");
            Assert.True(AssetDatabase.LoadAssetAtPath<Material>(path).IsKeywordEnabled("_EMISSION"));
        }
        [Test]public void WeaponUsesChamferedMeshRoundBarrelOpenSightAndBoundedTriangles()
        {
            var weapon=session.player.weapon;var bevel=AssetDatabase.LoadAssetAtPath<Mesh>(VisualFolder()+"/PresentationBevel.asset");
            Assert.NotNull(bevel);Assert.AreEqual(44,bevel.triangles.Length/3);Assert.Greater(bevel.tangents.Length,0);
            Assert.AreSame(bevel,weapon.Find("Receiver").GetComponent<MeshFilter>().sharedMesh);
            Assert.NotNull(weapon.Find("Round barrel"));Assert.NotNull(weapon.Find("Sight left post"));Assert.NotNull(weapon.Find("Sight right post"));
            Assert.IsNull(weapon.Find("Sight housing"),"The solid sight block must not obstruct the opening.");
            Assert.Less(weapon.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3),6000);
            Assert.IsEmpty(weapon.GetComponentsInChildren<Collider>());Assert.AreEqual(new Vector3(0,.012f,.39f),session.player.motion.muzzle.localPosition);
        }
        [Test]public void DecorativeRangeFinishAddsNoGameplayCollisionOrNavigationBlockers()
        {
            var dressing=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Presentation dressing — no gameplay colliders");
            Assert.IsEmpty(dressing.GetComponentsInChildren<Collider>());
            Assert.AreEqual(8,root.GetComponentsInChildren<Collider>().Count(c=>c.gameObject.layer==8));
            Assert.IsNotEmpty(session.Navigation.Find(session.player.transform.position,session.enemies[1].transform.position));
        }
        [Test]public void LightingHasOneShadowedSunTwoUnshadowedFillsAndNoRealtimeProbe()
        {
            var lights=root.GetComponentsInChildren<Light>();Assert.AreEqual(1,lights.Count(l=>l.type==LightType.Directional));
            Assert.AreEqual(2,lights.Count(l=>l.type==LightType.Point));Assert.AreEqual(1,lights.Count(l=>l.shadows!=LightShadows.None));
            Assert.IsEmpty(root.GetComponentsInChildren<ReflectionProbe>());
            var camera=session.player.view.GetComponent<Camera>();Assert.True(camera.allowHDR);Assert.AreEqual(CameraClearFlags.Skybox,camera.clearFlags);
        }
        [Test]public void OwnedVisualProfileHasExactAssetsAndBoundedTextureDimensionsWithoutArtAcceptance()
        {
            var folder=VisualFolder();var report=JsonUtility.FromJson<VisualReport>(File.ReadAllText(folder+"/combat-report.json"));
            Assert.AreEqual("industrial_range_finish_v1",report.visualProfile);Assert.AreEqual(18,report.visualAssets.Length);
            Assert.AreEqual(256,report.textureSize);Assert.AreEqual(7,report.ownedTextureCount);Assert.AreEqual(64,report.reflectionFaceSize);Assert.Greater(report.ownedTextureMemoryBytes,0);Assert.LessOrEqual(report.ownedTextureMemoryBytes,16*1024*1024);
            foreach(var path in report.visualAssets)Assert.True(File.Exists(folder+"/"+path),path);
            foreach(var path in report.visualAssets.Where(p=>p.EndsWith(".png",StringComparison.Ordinal))) {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+path);Assert.AreEqual(256,texture.width);Assert.AreEqual(256,texture.height);
                if(path.EndsWith("Normal.png",StringComparison.Ordinal))Assert.AreEqual(TextureImporterType.NormalMap,((TextureImporter)AssetImporter.GetAtPath(folder+"/"+path)).textureType);
            }
        }
#endif
    }
}
""";
}
