namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndieCharacterTestsSource="""
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class IndieCharacterTests
    {
        private static bool Finite(Mesh m)=>m.vertices.All(v=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)));
        private static bool Normalized(Mesh m)=>m.boneWeights.All(w=>w.weight0>=0&&w.weight1>=0&&w.weight2>=0&&w.weight3>=0&&Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.0001f);
        private string Folder()=>AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/IndustrialMission.prefab")&&File.Exists(Path.GetDirectoryName(p)+"/indie-character-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetDirectoryName).First();
        private GameObject Prefab()=>AssetDatabase.LoadAssetAtPath<GameObject>(Folder()+"/IndustrialMission.prefab");
        [Test]public void SharedGuardAndViewmodelMeshesAreFiniteWeightedDerivatives(){
            var s=Prefab().GetComponentInChildren<SecurityCombatSession>();var guards=s.enemies.Select(e=>e.motion.GetComponentInChildren<SkinnedMeshRenderer>()).ToArray();
            Assert.AreEqual(1,guards.Select(r=>r.sharedMesh).Distinct().Count());var arms=s.player.arms.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreNotSame(arms.sharedMesh,guards[0].sharedMesh);Assert.AreNotSame(arms.sharedMesh,AssetDatabase.LoadAssetAtPath<Mesh>(Folder()+"/FirstPersonArms.asset"));
            foreach(var skin in new[]{arms}.Concat(guards)){Assert.True(Finite(skin.sharedMesh));Assert.True(Normalized(skin.sharedMesh));Assert.LessOrEqual(skin.sharedMesh.vertexCount,60000);Assert.LessOrEqual(skin.sharedMesh.triangles.Length/3,45000);Assert.AreEqual(skin.bones.Length,skin.sharedMesh.bindposes.Length);Assert.False(skin.bones.Any(b=>b.name.Contains("Finger")));}
        }
        [Test]public void OriginalHandAssetAndBindMatricesRemainAvailable(){
            var s=Prefab().GetComponentInChildren<SecurityCombatSession>();var arms=s.player.arms.GetComponentInChildren<SkinnedMeshRenderer>();var original=AssetDatabase.LoadAssetAtPath<Mesh>(Folder()+"/FirstPersonArms.asset");
            Assert.True(original.bindposes.SequenceEqual(arms.sharedMesh.bindposes));Assert.Less(arms.sharedMesh.vertexCount,original.vertexCount);Assert.AreEqual(9,arms.gameObject.layer);
            foreach(var e in s.enemies){var skin=e.motion.GetComponentInChildren<SkinnedMeshRenderer>();Assert.True(skin.sharedMesh.bindposes.SequenceEqual(original.bindposes));}
        }
        [Test]public void AllEightUnchangedClipsBakeFiniteBoundedDerivativeSkin(){
            var root=UnityEngine.Object.Instantiate(Prefab());try{
                var s=root.GetComponentInChildren<SecurityCombatSession>();var visuals=new[]{s.player.arms}.Concat(s.enemies.Select(e=>e.motion.transform)).ToArray();
                foreach(var visual in visuals){
                    var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();var rest=skin.sharedMesh.vertices;var indices=skin.sharedMesh.triangles;
                    var bake=new Mesh();try{foreach(var name in new[]{"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"}){
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder()+"/"+name+".anim");Assert.NotNull(clip);
                        foreach(var sample in new[]{0f,.25f,.5f,.75f,1f}){
                            clip.SampleAnimation(visual.gameObject,clip.length*sample);skin.BakeMesh(bake);Assert.True(Finite(bake));var posed=bake.vertices;
                            Assert.AreEqual(rest.Length,posed.Length);for(var i=0;i<indices.Length;i+=3)for(var edge=0;edge<3;edge++){var a=indices[i+edge];var b=indices[i+(edge+1)%3];var length=Vector3.Distance(rest[a],rest[b]);if(length>.001f)Assert.LessOrEqual(Vector3.Distance(posed[a],posed[b])/length,3.5f,"New geometry stretches beyond original technical envelope: "+name);}
                        }
                    }}finally{UnityEngine.Object.DestroyImmediate(bake);}
                }
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test]public void WristDrivenDigitsDoNotAddCollisionOrAlterWeaponAnchors(){
            var s=Prefab().GetComponentInChildren<SecurityCombatSession>();Assert.AreEqual(new Vector3(0,-.095f,-.065f),s.player.rightGrip.localPosition);
            foreach(var w in new[]{s.player.weapon}.Concat(s.enemies.Select(e=>e.weapon))){Assert.IsEmpty(w.GetComponentsInChildren<Collider>(true));Assert.AreEqual(new Vector3(0,.012f,.39f),w.Find("Evaluated muzzle").localPosition);Assert.AreEqual(new Vector3(0,.095f,-.043f),w.Find("Sight aim point").localPosition);}
            Assert.AreEqual(4,Prefab().GetComponentsInChildren<SkinnedMeshRenderer>(true).Length);
        }
    }
}
""";
}

