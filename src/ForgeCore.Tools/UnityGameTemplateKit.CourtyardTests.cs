namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string CourtyardTestsSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class CourtyardPresentationTests
    {
        private GameObject root;private string folder;
        [UnitySetUp]public IEnumerator Setup() {
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/retro64-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/courtyard-report.json"))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            Assert.NotNull(path,"Generate a fresh courtyard fixture; old greyboxes do not satisfy this suite.");folder=Path.GetDirectoryName(path);
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var session=root.GetComponentInChildren<SecurityCombatSession>();session.enabled=false;session.SetPaused(true);
            yield return null;session.player.enabled=false;foreach(var enemy in session.enemies)enemy.enabled=false;
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        [Test]public void PresentationHasNoNewCollisionAndNoFalselyApprovedLibrarySources() {
            var decor=root.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Courtyard presentation",StringComparison.Ordinal));
            Assert.IsEmpty(decor.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(root.GetComponentsInChildren<IndustrialPropIdentity>(true));
            Assert.True(root.GetComponent<IndustrialMission>().courtyardPresentation);
            Assert.AreEqual(10,root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)));
            var ground=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Lab ground");
            Assert.AreEqual(new Vector3(34,.24f,34),ground.localScale);Assert.AreEqual(-.12f,ground.position.y,.0001f);Assert.AreEqual(8,ground.gameObject.layer);
            var divider=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Service-yard divider");
            Assert.AreEqual(new Vector3(-2,1.2f,2),divider.position);Assert.AreEqual(new Vector3(12,2.4f,.8f),divider.localScale);
            Assert.AreEqual(4,decor.GetComponentsInChildren<Light>().Length);
            Assert.True(decor.GetComponentsInChildren<Light>().All(l=>l.shadows==LightShadows.None&&l.range<=10));
        }
        [Test]public void OriginalSurfacesHaveBoundedMemoryCorrectNormalImportAndSupportedMaterials() {
            foreach(var stem in new[]{"YardAsphalt","YardConcrete","YardMetal"})foreach(var suffix in new[]{"","Normal"}) {
                var path=folder+"/"+stem+suffix+".png";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.NotNull(t);
                Assert.AreEqual(512,t.width);Assert.AreEqual(512,t.height);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.True(importer.mipmapEnabled);Assert.False(importer.isReadable);
                Assert.AreEqual(suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default,importer.textureType);
                Assert.AreEqual(suffix!="Normal",importer.sRGBTexture);
            }
            var decor=root.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Courtyard presentation",StringComparison.Ordinal));
            Assert.LessOrEqual(decor.GetComponentsInChildren<Renderer>().Length,40);
            var filters=decor.GetComponentsInChildren<MeshFilter>();Assert.LessOrEqual(filters.Sum(f=>f.sharedMesh.triangles.Length/3),100000);
            foreach(var f in filters){Assert.True(AssetDatabase.Contains(f.sharedMesh));Assert.True(f.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));}
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials) {
                Assert.NotNull(m);Assert.NotNull(m.shader);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.True(m.shader.isSupported,m.name);
            }
        }
        [UnityTest]public IEnumerator ActualCourtyardCameraHasContrastAndNoMagentaNotArtAcceptance() {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Real graphics device required.");
            yield return null;var camera=root.GetComponentInChildren<SecurityCombatPlayer>().view.GetComponent<Camera>();
            var target=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);var active=RenderTexture.active;
            try {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                var pixels=image.GetPixels();Assert.Less(pixels.Count(p=>p.r>.4f&&p.b>.4f&&p.g<.25f),5);
                Assert.Greater(pixels.Max(p=>p.grayscale)-pixels.Min(p=>p.grayscale),.25f);
            }finally{camera.targetTexture=null;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
""";
}
