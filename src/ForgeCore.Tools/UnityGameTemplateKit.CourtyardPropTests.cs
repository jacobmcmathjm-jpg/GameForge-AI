namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string CourtyardPropTestsSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class CourtyardPropTests
    {
        private GameObject root;private string folder;
        [UnitySetUp]public IEnumerator Setup() {
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")&&!File.Exists(Path.GetDirectoryName(p)+"/retro64-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/prop-presentation-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            Assert.NotNull(path,"Fresh shaped-prop mission required, not an old cube fixture.");folder=Path.GetDirectoryName(path);
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var session=root.GetComponentInChildren<SecurityCombatSession>();session.enabled=false;session.SetPaused(true);yield return null;session.player.enabled=false;foreach(var enemy in session.enemies)enemy.enabled=false;
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        private Transform Decor()=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Courtyard shaped props",StringComparison.Ordinal));
        [Test]public void TenUnitScaleLodPropsReplaceOnlyRenderingInsideExistingCollisionFootprints() {
            var decor=Decor();Assert.IsEmpty(decor.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(decor.GetComponentsInChildren<Light>(true));Assert.IsEmpty(root.GetComponentsInChildren<IndustrialPropIdentity>(true));
            var proxies=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)).ToArray();Assert.AreEqual(10,proxies.Length);
            var groups=decor.GetComponentsInChildren<LODGroup>(true);Assert.AreEqual(10,groups.Length);
            foreach(var proxy in proxies) {
                Assert.False(proxy.GetComponent<MeshRenderer>().enabled);var collider=proxy.GetComponent<BoxCollider>();Assert.True(collider.enabled);Assert.False(collider.isTrigger);Assert.AreEqual(8,proxy.gameObject.layer);
                var bottom=collider.bounds.center-Vector3.up*collider.bounds.extents.y;var group=groups.Single(g=>Vector3.Distance(g.transform.position,bottom)<.001f);Assert.AreEqual(Vector3.one,group.transform.lossyScale);
                var lods=group.GetLODs();Assert.AreEqual(3,lods.Length);Assert.AreEqual(LODFadeMode.None,group.fadeMode);var previous=int.MaxValue;
                for(var i=0;i<3;i++) {
                    var renderer=lods[i].renderers.Single();Assert.True(renderer.enabled);Assert.IsTrue(renderer.gameObject.activeSelf);var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.True(AssetDatabase.Contains(mesh));Assert.Less(mesh.triangles.Length/3,previous);previous=mesh.triangles.Length/3;
                    Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.AreEqual(mesh.vertexCount,mesh.tangents.Length);
                    Assert.True(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
                    Assert.Less(Vector3.Distance(mesh.bounds.size,collider.bounds.size),.015f);Assert.Less(Mathf.Abs(mesh.bounds.min.y),.006f);
                    foreach(var m in renderer.sharedMaterials){Assert.NotNull(m);Assert.NotNull(m.shader);if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.True(m.shader.isSupported,m.name);}
                }
                Assert.Greater(lods[0].screenRelativeTransitionHeight,lods[1].screenRelativeTransitionHeight);Assert.Greater(lods[1].screenRelativeTransitionHeight,lods[2].screenRelativeTransitionHeight);
            }
        }
        [Test]public void BarrelIsRoundBarrierNarrowsAtTopAndFenceHasRealOpenMesh() {
            Mesh Shape(string family)=>Decor().GetComponentsInChildren<LODGroup>(true).First(g=>g.name.StartsWith("Template "+family+" ",StringComparison.Ordinal)).GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var barrel=Shape("barrel");Assert.True(barrel.vertices.All(v=>new Vector2(v.x,v.z).magnitude<.341f));
            var barrier=Shape("barrier");Assert.Less(barrier.vertices.Where(v=>v.y>.95f).Max(v=>Mathf.Abs(v.z)),.12f);
            var fence=Shape("fence");var vertices=fence.vertices;var indices=fence.triangles;var ray=new Ray(new Vector3(.061f,1.157f,-1),Vector3.forward);
            var hits=0;for(var i=0;i<indices.Length;i+=3)if(Hit(ray,vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]))hits++;
            Assert.AreEqual(0,hits,"Fence is visually open, not an opaque box. Existing conservative solid collision remains intentional.");
            Assert.Greater(fence.triangles.Length,500);Assert.Greater(Shape("crate").triangles.Length,1000);Assert.Greater(Shape("cargo").triangles.Length,1000);
        }
        private static bool Hit(Ray ray,Vector3 a,Vector3 b,Vector3 c) {
            var edge1=b-a;var edge2=c-a;var p=Vector3.Cross(ray.direction,edge2);var determinant=Vector3.Dot(edge1,p);if(Mathf.Abs(determinant)<.000001f)return false;
            var inverse=1/determinant;var t=ray.origin-a;var u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;var q=Vector3.Cross(t,edge1);var v=Vector3.Dot(ray.direction,q)*inverse;
            return v>=0&&u+v<=1&&Vector3.Dot(edge2,q)*inverse>=0;
        }
        [Test]public void WoodUsesCompressedNonmetallicPbrAndExactComparisonDimensions() {
            long bytes=0;foreach(var kind in new[]{"Albedo","Normal"}) {
                var path=folder+"/PropWood"+kind+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.NotNull(texture);Assert.AreEqual(1024,texture.width);Assert.AreEqual(1024,texture.height);
                bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);var importer=(TextureImporter)AssetImporter.GetAtPath(path);Assert.False(importer.isReadable);Assert.True(importer.mipmapEnabled);
                Assert.AreEqual(kind=="Albedo",importer.sRGBTexture);Assert.AreEqual(kind=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default,importer.textureType);
                Assert.AreEqual(kind=="Normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7,importer.GetPlatformTextureSettings("Standalone").format);
            }
            Assert.LessOrEqual(bytes,8*1024*1024);foreach(var name in new[]{"PropWood","PropWoodDark"}){var m=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat");Assert.AreEqual(0,m.GetFloat("_Metallic"));Assert.Less(m.GetFloat("_Glossiness"),.2f);}
            var image=new Texture2D(2,2);try{Assert.True(image.LoadImage(File.ReadAllBytes(folder+"/prop-comparison.png")));Assert.AreEqual(1440,image.width);Assert.AreEqual(810,image.height);}finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
""";
}
