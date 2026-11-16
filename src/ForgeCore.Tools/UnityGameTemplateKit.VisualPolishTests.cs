namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string VisualPolishTestsSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.TestTools;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class VisualPolishTests
    {
        private GameObject root;private string folder;
        [UnitySetUp]public IEnumerator Setup() {
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")&&!File.Exists(Path.GetDirectoryName(p)+"/retro64-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/visual-polish-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            Assert.NotNull(path,"Fresh final visual fixture required.");folder=Path.GetDirectoryName(path);PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());
            root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));var session=root.GetComponentInChildren<SecurityCombatSession>();session.enabled=false;session.SetPaused(true);
            yield return null;session.player.enabled=false;foreach(var enemy in session.enemies)enemy.enabled=false;
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        [Test]public void OriginalHeroFinishHasCorrectMapsAndBoundedMemory() {
            long bytes=0;foreach(var family in new[]{"Surface","Cloth"})foreach(var kind in new[]{"Albedo","Normal","Mask"}) {
                var path=folder+"/Hero"+family+kind+".png";var image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.NotNull(image);Assert.AreEqual(1024,image.width);Assert.AreEqual(1024,image.height);
                bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(image);var importer=(TextureImporter)AssetImporter.GetAtPath(path);Assert.False(importer.isReadable);Assert.True(importer.mipmapEnabled);
                Assert.AreEqual(kind=="Albedo",importer.sRGBTexture);Assert.AreEqual(kind=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default,importer.textureType);
                Assert.AreEqual(kind=="Normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7,importer.GetPlatformTextureSettings("Standalone").format);
            }Assert.LessOrEqual(bytes,20*1024*1024);
            foreach(var name in new[]{"HeroCloth","HeroGlove","HeroPolymer"}){var m=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat");Assert.AreEqual(0,m.GetFloat("_Metallic"));Assert.False(m.IsKeywordEnabled("_METALLICGLOSSMAP"));}
        }
        [Test]public void RifleSocketsMagazineAndVisualBindingsRemainOnTheOriginalRig() {
            var session=root.GetComponentInChildren<SecurityCombatSession>();var weapons=new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon)).ToArray();
            foreach(var weapon in weapons) {
                Assert.IsEmpty(weapon.GetComponentsInChildren<Collider>(true));Assert.AreEqual(new Vector3(0,.095f,-.043f),weapon.Find("Sight aim point").localPosition);
                Assert.AreEqual(new Vector3(0,.012f,.39f),weapon.Find("Evaluated muzzle").localPosition);
                var details=weapon.Find("Original hero weapon details");Assert.NotNull(details);Assert.LessOrEqual(details.GetComponentsInChildren<MeshRenderer>().Length,2);
                Assert.NotNull(weapon.Find("Animated detachable magazine"));Assert.False(weapon.Find("Hand guard").GetComponent<MeshRenderer>().enabled);
                var receiver=weapon.Find("Receiver");Assert.Less(receiver.localScale.y,.075f);
            }
            foreach(var enemy in session.enemies) {
                var transforms=enemy.motion.GetComponentsInChildren<Transform>(true);Assert.AreEqual(transforms.Length,transforms.Select(t=>t.name).Distinct().Count(),"Contact rig depends on unique transform names.");
                var attachments=transforms.Where(t=>t.name.StartsWith("Original finish binding ",StringComparison.Ordinal)).ToArray();Assert.AreEqual(12,attachments.Length);
                foreach(var a in attachments){Assert.Contains(a.parent.name,new[]{"Head","Chest"});Assert.IsEmpty(a.GetComponentsInChildren<Collider>(true));Assert.Less(a.localPosition.magnitude,.5f);}
            }
        }
        [Test]public void LightingUsesOneBakedProbeAndNoOpaquePuddleOrNewRealtimeLight() {
            var probes=root.GetComponentsInChildren<ReflectionProbe>(true);Assert.AreEqual(1,probes.Length);var probe=probes[0];Assert.AreEqual(ReflectionProbeMode.Custom,probe.mode);Assert.AreEqual(128,probe.resolution);Assert.True(probe.boxProjection);
            Assert.NotNull(probe.customBakedTexture);Assert.AreEqual(128,probe.customBakedTexture.width);Assert.NotNull(probe.texture);
            var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/CourtyardReflection.exr");Assert.False(importer.sRGBTexture);Assert.True(importer.mipmapEnabled);Assert.AreEqual(TextureImporterFormat.BC6H,importer.GetPlatformTextureSettings("Standalone").format);
            var wet=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.name=="YardWet")).ToArray();Assert.AreEqual(1,wet.Length);Assert.False(wet[0].enabled);
            Assert.AreEqual(4,root.GetComponentsInChildren<Light>(true).Count(l=>l.name=="Bounded warm work-light"));
            Assert.IsEmpty(root.GetComponentsInChildren<IndustrialPropIdentity>(true));
        }
    }
}
""";
}
