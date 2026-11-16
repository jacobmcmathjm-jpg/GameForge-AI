namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string CourtyardGraphicsTestsSource = """
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class CourtyardGraphicsTests
    {
        private string Folder()=>AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ForgeCore/CharacterAnimations"}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p=>p.EndsWith("/IndustrialMission.prefab")&&!File.Exists(Path.GetDirectoryName(p)+"/connected-level-report.json")&&!File.Exists(Path.GetDirectoryName(p)+"/retro64-report.json")&&File.Exists(Path.GetDirectoryName(p)+"/graphics-report.json"))
            .OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetDirectoryName).FirstOrDefault()??throw new InvalidOperationException("Fresh graphics benchmark fixture required.");
        [Test] public void DetailMapsHaveCorrectSizeColourSpaceNormalsAndMipmaps()
        {
            var folder=Folder();long bytes=0;
            foreach(var surface in new[]{"Asphalt","Concrete","Steel"})foreach(var kind in new[]{"Albedo","Normal","Mask"})
            {
                var path=folder+"/Detail"+surface+kind+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.NotNull(texture);
                Assert.AreEqual(1024,texture.width);Assert.AreEqual(1024,texture.height);bytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);Assert.True(importer.mipmapEnabled);Assert.False(importer.isReadable);
                var platform=importer.GetPlatformTextureSettings("Standalone");Assert.True(platform.overridden);Assert.AreEqual(kind=="Normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7,platform.format);
                Assert.AreEqual(kind=="Albedo",importer.sRGBTexture);Assert.AreEqual(kind=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default,importer.textureType);
            }
            Assert.LessOrEqual(bytes,64*1024*1024);
        }
        [Test] public void DetailDoesNotIntroduceCollisionOrUnboundedGeometry()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder()+"/IndustrialMission.prefab");Assert.NotNull(prefab);
            var detail=prefab.GetComponentsInChildren<Transform>(true).Single(t=>t.name.StartsWith("Courtyard detail v1",StringComparison.Ordinal));
            Assert.IsEmpty(detail.GetComponentsInChildren<Collider>(true));Assert.IsEmpty(detail.GetComponentsInChildren<Light>(true));
            Assert.LessOrEqual(detail.GetComponentsInChildren<Renderer>(true).Length,6);
            Assert.LessOrEqual(detail.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3),25000);
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
            {Assert.NotNull(material);Assert.NotNull(material.shader);if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.True(material.shader.isSupported,material.name);}
            var bytes=File.ReadAllBytes(Folder()+"/graphics-comparison.png");var image=new Texture2D(2,2);
            try{Assert.True(image.LoadImage(bytes));Assert.AreEqual(1920,image.width);Assert.AreEqual(1620,image.height);}
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
""";
}
