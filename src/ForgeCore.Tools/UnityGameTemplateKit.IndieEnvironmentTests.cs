namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndieEnvironmentTestsSource="""
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class IndieEnvironmentTests
    {
        private string Folder()=>AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/indie-environment-report.json")).OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetDirectoryName).First();
        [Test]public void SkyIsBoundedCompressedAndExplicitlyPanoramic(){var folder=Folder();var sky=AssetDatabase.LoadAssetAtPath<Material>(folder+"/IndieSky.mat");Assert.NotNull(sky);Assert.AreEqual("Skybox/Panoramic",sky.shader.name);Assert.True(sky.shader.isSupported);Assert.AreEqual(1,sky.GetFloat("_Mapping"));var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/IndieSky.png");Assert.AreEqual(1024,texture.width);Assert.AreEqual(512,texture.height);Assert.LessOrEqual(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),4*1024*1024);var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/IndieSky.png");Assert.True(importer.sRGBTexture);Assert.True(importer.mipmapEnabled);Assert.False(importer.isReadable);Assert.AreEqual(TextureImporterFormat.BC7,importer.GetPlatformTextureSettings("Standalone").format);}
        [Test]public void DecorativeBatchesHaveNoCollisionOrRuntimeLight(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder()+"/IndustrialMission.prefab");var detail=prefab.transform.Find("Indie environment v1");Assert.NotNull(detail);Assert.IsEmpty(detail.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(detail.GetComponentsInChildren<Light>(true));Assert.AreEqual(2,detail.GetComponentsInChildren<MeshRenderer>().Length);Assert.LessOrEqual(detail.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3),12000);}
        [Test]public void OriginalCombatAndAudioRemainOnTheDetailedRig(){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder()+"/IndustrialMission.prefab");var session=prefab.GetComponentInChildren<SecurityCombatSession>();Assert.AreEqual(3,session.enemies.Length);Assert.NotNull(session.player.sight);Assert.NotNull(session.player.motion.muzzle);Assert.NotNull(session.audio);Assert.NotNull(session.options);Assert.NotNull(session.player.weapon.Find("Original hero weapon details"));Assert.False(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.sharedMesh.name.Contains("retro")));}
    }
}
""";
}
