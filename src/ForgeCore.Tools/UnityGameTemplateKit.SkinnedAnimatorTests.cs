namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SkinnedAnimatorTestsSource = """
using System;
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class PrototypeSkinnedAnimatorTests
    {
        [Serializable]private sealed class MotionSource{public string assetPath;}
        private GameObject root;
        private PrototypeSkinnedAnimator driver;
        private static void Advance(Animator animator,float duration)
        {for(var time=0f;time<duration;time+=1f/60)animator.Update(Mathf.Min(1f/60,duration-time));}
        [SetUp]public void Setup()
        {
            root=new GameObject("S03 isolated test");driver=root.AddComponent<PrototypeSkinnedAnimator>();driver.automaticDemo=false;driver.showControls=false;driver.enabled=false;
            driver.muzzle=new GameObject("Test muzzle").transform;driver.muzzle.SetParent(root.transform);driver.muzzle.localPosition=new Vector3(.1f,1.2f,.7f);
        }
        [TearDown]public void Cleanup(){UnityEngine.Object.DestroyImmediate(root);}
        [Test]public void FireReloadAreExclusiveAndReloadCommitsAtEnd()
        {
            Assert.True(driver.TryFire());Assert.False(driver.TryReload());Assert.False(driver.TryFire());driver.Tick(.1f);driver.FlushShotMarkerForTest();Assert.AreEqual(1,driver.Shots);Assert.AreEqual(5,driver.Ammo);
            driver.Tick(.2f);Assert.True(driver.TryReload());Assert.False(driver.TryFire());driver.Tick(1);Assert.AreEqual(5,driver.Ammo);driver.Tick(.61f);Assert.AreEqual(6,driver.Ammo);
        }
        [Test]public void InterruptedReloadDoesNotGrantAmmoAndDeathIsTerminal()
        {
            driver.TryFire();driver.Tick(.3f);driver.FlushShotMarkerForTest();driver.TryReload();driver.Tick(.8f);driver.Hit();driver.Tick(.5f);Assert.AreEqual(5,driver.Ammo);
            driver.Hit(true);Assert.True(driver.Dead);Assert.False(driver.TryFire());Assert.False(driver.TryReload());driver.Tick(1);Assert.AreEqual("Death",driver.Action);driver.ResetDemo();Assert.False(driver.Dead);Assert.AreEqual(6,driver.Ammo);
        }
        [Test]public void PauseFreezesMotionMarkersAndReloadTime()
        {
            driver.SetLocomotion(1,30,false);driver.Tick(.5f);var position=root.transform.position;var rotation=root.transform.rotation;driver.TryFire();driver.SetPaused(true);driver.Tick(.5f);driver.FlushShotMarkerForTest();
            Assert.AreEqual(position,root.transform.position);Assert.AreEqual(rotation,root.transform.rotation);Assert.AreEqual(0,driver.ActionTime);Assert.AreEqual(0,driver.Shots);Assert.False(driver.TryReload());
            driver.SetPaused(false);driver.Tick(.1f);driver.FlushShotMarkerForTest();Assert.AreEqual(1,driver.Shots);
        }
        [Test]public void ProjectileStartsAtEvaluatedMuzzleAndPendingShotIsCancelledByDamage()
        {
            driver.TryFire();driver.Tick(.1f);driver.muzzle.localPosition+=Vector3.right*.2f;driver.FlushShotMarkerForTest();Assert.AreEqual(driver.muzzle.position,driver.LastMuzzlePosition);Assert.AreEqual(driver.muzzle.forward,driver.LastShotDirection);
            driver.Tick(.2f);driver.TryFire();driver.Tick(.1f);driver.Hit(true);driver.FlushShotMarkerForTest();Assert.AreEqual(1,driver.Shots);
        }
        [Test]public void NonfiniteMotionIsRejectedAndRootTranslationHasSingleOwner()
        {
            Assert.Throws<ArgumentException>(()=>driver.SetLocomotion(float.NaN,0,false));Assert.Throws<ArgumentException>(()=>driver.Tick(float.PositiveInfinity));
            driver.SetLocomotion(1,0,false);driver.Tick(.5f);Assert.That(root.transform.position.z,Is.EqualTo(.5f).Within(.0001f));driver.SetLocomotion(2,0,true);driver.Tick(.5f);Assert.That(root.transform.position.z,Is.EqualTo(.5f).Within(.0001f));
        }
        [UnityTest]public IEnumerator GeneratedAnimatorBlendsAndEntersExclusiveActionStates()
        {
#if UNITY_EDITOR
            var paths=AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/Mannequin.controller",StringComparison.Ordinal)&&File.Exists(Path.GetDirectoryName(p)+"/animation-report.json"))
                .Where(p=>!string.IsNullOrEmpty(JsonUtility.FromJson<MotionSource>(File.ReadAllText(Path.GetDirectoryName(p)+"/animation-report.json")).assetPath)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            Assert.That(paths.Length,Is.GreaterThan(0),"Generate the S03 lab before animation acceptance tests.");
            var source=JsonUtility.FromJson<MotionSource>(File.ReadAllText(Path.GetDirectoryName(paths.Last())+"/animation-report.json"));
            UnityEngine.Object.DestroyImmediate(root);root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source.assetPath));
            driver=root.AddComponent<PrototypeSkinnedAnimator>();driver.automaticDemo=false;driver.showControls=false;driver.enabled=false;
            var animator=root.GetComponent<Animator>();if(animator==null)animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(paths.Last());driver.animator=animator;animator.applyRootMotion=false;
            yield return null;
            animator.Rebind();animator.Update(0);
            driver.SetLocomotion(1,0,false);driver.Tick(.01f);Advance(animator,.2f);Assert.AreEqual(1,animator.GetFloat("Speed"));
            var bones=root.GetComponentsInChildren<Transform>();var foot=bones.Single(t=>t.name=="Foot.L");var hips=bones.Single(t=>t.name=="Hips");
            var footStart=foot.position;Advance(animator,.25f);Assert.That(Vector3.Distance(footStart,foot.position),Is.GreaterThan(.05f),"Locomotion must animate the actual skeleton, not only change a parameter.");
            Assert.True(driver.TryFire(),"Action driver must accept fire");Advance(animator,.16f);Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Fire"),"Animator must enter Fire within 160ms");Assert.False(driver.TryReload());
            driver.Tick(.3f);Advance(animator,.2f);Assert.True(driver.TryReload(),"Action driver must accept reload after fire ends");Advance(animator,.25f);Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Reload"),"Animator must enter Reload within 250ms; state="+animator.GetCurrentAnimatorStateInfo(0).shortNameHash+", transition="+animator.IsInTransition(0));
            driver.SetPaused(true);var pausedHip=hips.position;Advance(animator,.25f);Assert.AreEqual(pausedHip,hips.position,"Animator skeleton must freeze while paused");driver.SetPaused(false);
            driver.Hit(true);Advance(animator,.95f);Assert.True(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),"Animator must enter Death");Assert.False(animator.applyRootMotion);Assert.That(hips.position.y,Is.LessThan(.5f),"Death must move the actual rig.");
#else
            Assert.Fail("This generated-controller test requires the Editor PlayMode runner.");yield return null;
#endif
        }
    }
}
""";
}
