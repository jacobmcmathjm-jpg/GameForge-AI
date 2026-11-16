namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndieWeaponTestsSource="""
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class IndieWeaponTests
    {
        private GameObject Prefab()=>AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/indie-weapon-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).Select(AssetDatabase.LoadAssetAtPath<GameObject>).First();
        [Test]public void FourWeaponsShareOnlyTwoFiniteBoundedMeshes(){var session=Prefab().GetComponentInChildren<SecurityCombatSession>();var weapons=new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon)).ToArray();var rifles=weapons.Select(w=>w.Find("Indie service rifle visual").GetComponent<MeshFilter>().sharedMesh).ToArray();Assert.AreEqual(1,rifles.Distinct().Count());var magazines=weapons.Select(w=>w.Find("Animated detachable magazine/Indie curved magazine visual").GetComponent<MeshFilter>().sharedMesh).ToArray();Assert.AreEqual(1,magazines.Distinct().Count());Assert.AreNotSame(rifles[0],magazines[0]);Assert.LessOrEqual((rifles[0].triangles.Length+magazines[0].triangles.Length)/3,12000);foreach(var mesh in new[]{rifles[0],magazines[0]}){Assert.AreEqual(3,mesh.subMeshCount);Assert.IsTrue(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)));}}
        [Test]public void NewVisualsKeepSightMuzzleAndAnimatedMagazineRoots(){var session=Prefab().GetComponentInChildren<SecurityCombatSession>();foreach(var w in new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon))){Assert.AreEqual(new Vector3(0,.012f,.39f),w.Find("Evaluated muzzle").localPosition);Assert.AreEqual(new Vector3(0,.095f,-.043f),w.Find("Sight aim point").localPosition);Assert.IsTrue(w.Find("Amber sight dot").GetComponent<Renderer>().enabled);Assert.IsFalse(w.Find("Receiver").GetComponent<Renderer>().enabled);Assert.IsFalse(w.Find("Animated detachable magazine").GetComponent<Renderer>().enabled);Assert.IsEmpty(w.GetComponentsInChildren<Collider>(true));Assert.AreEqual(new Vector3(.045f,.15f,.07f),w.Find("Animated detachable magazine").localScale);}}
        [Test]public void NoOpaqueSurfaceOccludesTheSightOrMuzzleAxis(){var w=Prefab().GetComponentInChildren<SecurityCombatSession>().player.weapon;var mesh=w.Find("Indie service rifle visual").GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            bool Hit(Vector3 origin,Vector3 dir,float distance){for(var i=0;i<triangles.Length;i+=3){var a=vertices[triangles[i]];var e1=vertices[triangles[i+1]]-a;var e2=vertices[triangles[i+2]]-a;var p=Vector3.Cross(dir,e2);var det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-10f)continue;var inv=1/det;var t=origin-a;var u=Vector3.Dot(t,p)*inv;if(u<0||u>1)continue;var q=Vector3.Cross(t,e1);var v=Vector3.Dot(dir,q)*inv;if(v<0||u+v>1)continue;var d=Vector3.Dot(e2,q)*inv;if(d>0&&d<distance)return true;}return false;}
            Assert.False(Hit(new Vector3(0,.095f,-.08f),Vector3.forward,.2f),"ADS opening obstructed by replacement art.");Assert.False(Hit(new Vector3(0,.012f,.34f),Vector3.forward,.06f),"Muzzle bore obstructed by replacement art.");}
        [Test]public void ReloadMagazineKeepsItsOwnVisualNotAnotherFixedBody(){var session=Prefab().GetComponentInChildren<SecurityCombatSession>();foreach(var w in new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon))){var mag=w.Find("Animated detachable magazine/Indie curved magazine visual");Assert.NotNull(mag);Assert.AreEqual(w.Find("Animated detachable magazine"),mag.parent);Assert.AreEqual(Vector3.zero,mag.localPosition);Assert.AreEqual(Vector3.one,mag.localScale);Assert.AreEqual(w.gameObject.layer,mag.gameObject.layer);}}
    }
}
""";
}
