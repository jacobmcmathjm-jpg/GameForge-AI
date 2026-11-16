namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string RetroCourtyardTestsSource="""
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
    public sealed class RetroCourtyardTests
    {
        private GameObject root;private string folder;private SecurityCombatSession session;
        [UnitySetUp]public IEnumerator Setup() {
            var path=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&File.Exists(Path.GetDirectoryName(p)+"/retro64-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            Assert.NotNull(path,"A fresh explicit retro_64 fixture is required.");folder=Path.GetDirectoryName(path);PrototypeInput.SetScriptedFrame(new PrototypeInputFrame());
            root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));session=root.GetComponentInChildren<SecurityCombatSession>();session.enabled=false;session.SetPaused(true);
            yield return null;session.player.enabled=false;foreach(var enemy in session.enemies)enemy.enabled=false;
        }
        [TearDown]public void Cleanup(){PrototypeInput.ClearScriptedFrame();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        [Test]public void RetroUsesActualLowPolySkinsAndWeaponsNotOnlyAnImageFilter() {
            foreach(var enemy in session.enemies) {
                var skin=enemy.motion.GetComponentInChildren<SkinnedMeshRenderer>();Assert.That(skin.sharedMesh.triangles.Length/3,Is.InRange(200,2000));
                var mesh=skin.sharedMesh;Assert.AreEqual(mesh.vertexCount,mesh.boneWeights.Length);Assert.AreEqual(skin.bones.Length,mesh.bindposes.Length);
                foreach(var w in mesh.boneWeights){Assert.AreEqual(1,w.weight0+w.weight1+w.weight2+w.weight3,.0001f);Assert.That(w.boneIndex0,Is.InRange(0,skin.bones.Length-1));Assert.That(w.boneIndex1,Is.InRange(0,skin.bones.Length-1));}
                Assert.True(mesh.uv.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)));Assert.True(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
                Assert.Less(mesh.bounds.size.y,1.85f);Assert.Greater(mesh.bounds.size.y,1.7f);
            }
            var arms=session.player.arms.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;Assert.That(arms.triangles.Length/3,Is.InRange(48,500));
            foreach(var weapon in new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon))) {
                var shape=weapon.Find("Original retro low-poly rifle");Assert.NotNull(shape);Assert.LessOrEqual(shape.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,400);
                Assert.Less(shape.GetComponent<MeshFilter>().sharedMesh.bounds.max.z,weapon.Find("Evaluated muzzle").localPosition.z-.025f,"Retro barrel must not bury the fixed muzzle feedback origin.");
                var mag=weapon.Find("Animated detachable magazine/Original retro animated magazine");Assert.NotNull(mag);Assert.True(mag.GetComponent<MeshRenderer>().enabled);
                Assert.AreEqual(new Vector3(0,.095f,-.043f),weapon.Find("Sight aim point").localPosition);Assert.AreEqual(new Vector3(0,.012f,.39f),weapon.Find("Evaluated muzzle").localPosition);
                Assert.IsEmpty(weapon.GetComponentsInChildren<Collider>(true));
            }
            var lods=root.GetComponentsInChildren<LODGroup>(true).Where(g=>g.name.StartsWith("Template ",StringComparison.Ordinal)).ToArray();Assert.AreEqual(10,lods.Length);
            foreach(var group in lods)Assert.AreEqual(1,group.GetLODs().Length);
        }
        [Test]public void RetroPaintedMapsAreSmallBilinearAndQuantisedNotCopiedGameArt() {
            long bytes=0;foreach(var role in new[]{"Stone","Steel","Wood","Cloth","Skin","Dark","Glass","Glow"}) {
                var path=folder+"/Retro"+role+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.NotNull(texture);Assert.That(texture.width,Is.InRange(32,128));Assert.AreEqual(texture.width,texture.height);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);Assert.AreEqual(FilterMode.Bilinear,imp.filterMode);Assert.True(imp.mipmapEnabled);Assert.True(imp.sRGBTexture);Assert.False(imp.isReadable);
                bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Retro"+role+".mat");Assert.AreEqual("Legacy Shaders/VertexLit",material.shader.name);
                var image=new Texture2D(2,2);try{Assert.True(image.LoadImage(File.ReadAllBytes(path)));foreach(var c in image.GetPixels32())foreach(var component in new[]{c.r,c.g,c.b})Assert.LessOrEqual(Mathf.Abs(component/255f*31-Mathf.Round(component/255f*31)),.08f);}finally{UnityEngine.Object.DestroyImmediate(image);}
            }Assert.LessOrEqual(bytes,1024*1024);Assert.IsEmpty(root.GetComponentsInChildren<ReflectionProbe>(true).Where(p=>p.enabled));
        }
        [Test]public void RetroSkinStaysFiniteAcrossAllEightRetainedActionClips() {
            var skin=session.enemies[0].motion.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            try{foreach(var name in new[]{"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"}) {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/"+name+".anim");Assert.NotNull(clip);clip.SampleAnimation(session.enemies[0].motion.gameObject,clip.length*.5f);skin.BakeMesh(baked);
                Assert.True(baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));Assert.Less(baked.bounds.size.magnitude,4,"Exploding low-poly rig derivative.");
            }}finally{UnityEngine.Object.DestroyImmediate(baked);}
        }
        [Test]public void RetroCorrectsTheTwoKnownPlacementIntersectionsWithoutLibraryApproval() {
            var transforms=root.GetComponentsInChildren<Transform>(true);
            Assert.AreEqual(-5,transforms.Single(t=>t.name=="GREYBOX supply crate B — approval pending").position.z,.001f);
            Assert.NotNull(transforms.Single(t=>t.name=="GREYBOX sealed cargo — approval pending"&&Mathf.Abs(t.position.x-3)<.001f&&Mathf.Abs(t.position.z+2)<.001f));
            var props=transforms.Where(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)).Select(t=>t.GetComponent<BoxCollider>()).ToArray();
            for(var i=0;i<props.Length;i++)for(var j=i+1;j<props.Length;j++)Assert.False(props[i].bounds.Intersects(props[j].bounds));
            Assert.IsEmpty(root.GetComponentsInChildren<IndustrialPropIdentity>(true)); // Technical original template, not promoted vault placement.
        }
    }
}
""";
}
