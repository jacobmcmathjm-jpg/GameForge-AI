namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string EffectTestsSource = """
using System;
using System.IO;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
namespace ForgeCoreGameTemplates.Tests
{
    public sealed class MuzzleEffectTests
    {
        [TearDown]public void Cleanup()
        {foreach(var effect in UnityEngine.Object.FindObjectsByType<PrototypeMuzzleFlash>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(effect.gameObject);}
        [UnityTest]public IEnumerator FlashIsDirectionalWarmBriefAndReleasesOwnedResources()
        {
            var start=PrototypeMuzzleFlash.ActiveCount;
            var position=new Vector3(1000,1000,1000);var direction=new Vector3(1,0,1).normalized;
            var effect=PrototypeMuzzleFlash.Spawn(position,direction);Assert.NotNull(effect);
            var renderer=effect.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
            var mesh=effect.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2,materials.Length);Assert.AreEqual(start+1,PrototypeMuzzleFlash.ActiveCount);
            Assert.Less(Vector3.Angle(direction,effect.transform.forward),.01f);
            foreach(var material in materials) {
                Assert.NotNull(material);Assert.AreEqual("ForgeCore/SoftWarmFeedback",material.shader.name);
                Assert.Greater(material.color.r,material.color.b);Assert.Greater(material.color.g,.3f);
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)Assert.True(material.shader.isSupported);
            }
            Assert.Less(mesh.bounds.size.z,.20f);Assert.Less(mesh.bounds.size.x,.15f);
            yield return new WaitForSeconds(.15f);yield return null;
            Assert.True(effect==null);Assert.True(mesh==null);Assert.True(materials.All(m=>m==null));Assert.AreEqual(start,PrototypeMuzzleFlash.ActiveCount);
        }
        [UnityTest]public IEnumerator OtherFeedbackParticlesAlsoHaveAnExplicitMaterial()
        {
            PrototypeFeedback.Hit(new Vector3(1000,1000,1000),false);
            var effect=UnityEngine.Object.FindObjectsByType<PrototypeMuzzleFlash>(FindObjectsSortMode.None).Single();
            var renderer=effect.GetComponent<ParticleSystemRenderer>();Assert.NotNull(renderer);
            Assert.NotNull(renderer.sharedMaterial);Assert.AreEqual("ForgeCore/SoftWarmFeedback",renderer.sharedMaterial.shader.name);
            yield return null;
        }
        [UnityTest]public IEnumerator RealCameraRecordsWarmFlashAndNoMagentaErrorPixels()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){Assert.Ignore("Actual GPU render required.");yield break;}
            GameObject cameraObject=null;PrototypeMuzzleFlash effect=null;RenderTexture target=null;Texture2D pixels=null;
            var previous=RenderTexture.active;
            try {
                effect=PrototypeMuzzleFlash.Spawn(new Vector3(1000,1000,1000),Vector3.forward);Assert.NotNull(effect);effect.gameObject.layer=16;
                cameraObject=new GameObject("Owned flash regression camera");var camera=cameraObject.AddComponent<Camera>();
                camera.transform.position=new Vector3(1000,1000,999);camera.transform.rotation=Quaternion.identity;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=.13f;
                camera.cullingMask=1<<16;camera.nearClipPlane=.01f;camera.farClipPlane=3;camera.enabled=false;
                target=new RenderTexture(256,256,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(256,256,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                var warm=0;var magenta=0;
                foreach(var p in pixels.GetPixels()) {
                    if(p.r>.25f&&p.g>.15f&&p.r>p.b*1.2f)warm++;
                    if(p.r>.4f&&p.b>.4f&&p.g<.2f)magenta++;
                }
                Assert.Greater(warm,20,"The real renderer must show a visible warm flash, not a missing/black effect.");
                Assert.AreEqual(0,magenta,"No magenta missing-material pixels.");
                Directory.CreateDirectory("Previews");var path="Previews/S10-warm-flash-"+Guid.NewGuid().ToString("N")+".png";
                using(var file=new FileStream(path,FileMode.CreateNew)){var bytes=pixels.EncodeToPNG();file.Write(bytes,0,bytes.Length);}
                TestContext.WriteLine("Actual warm flash render: "+path+"; warm="+warm+"; magenta="+magenta);
            }finally {
                RenderTexture.active=previous;if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(effect!=null)UnityEngine.Object.DestroyImmediate(effect.gameObject);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            }
            yield return null;
        }
    }
}
""";
}
