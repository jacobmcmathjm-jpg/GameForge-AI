namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] IndieEnvironmentArtifacts=["IndieSky.png","IndieSky.mat","IndieDressing.asset","indie-environment-report.json"];
    private const string IndieEnvironmentSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Reusable original environment pass; not downloaded art or an automatic art approval.
    public static class ForgeCoreIndieEnvironment
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,skyWidth=1024,skyHeight=512,addedRenderers,addedTriangles,addedColliders,addedLights;
            public string profile="grounded_indie_environment_v1",source="original_template_geometry_and_procedural_sky";
            public bool collisionUnchanged,socketsUnchanged,animationsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,stageComplete=false;
            public long ownedTextureBytes;
        }
        public static void Apply(GameObject root,string folder) {
            if(GraphicsSettings.currentRenderPipeline!=null||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Indie environment requires actual built-in graphics.");
            var session=root.GetComponentInChildren<SecurityCombatSession>();var before=Collision(root);var lightCount=root.GetComponentsInChildren<Light>(true).Length;
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));
            var report=new Report();var skyPath=folder+"/IndieSky.png";var pixels=new Color32[1024*512];
            for(var y=0;y<512;y++)for(var x=0;x<1024;x++){
                var latitude=(y/511f-.5f)*Mathf.PI;var angle=x/1024f*Mathf.PI*2;var direction=new Vector3(Mathf.Cos(latitude)*Mathf.Cos(angle),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(angle));
                var height=Mathf.Clamp01(direction.y);var baseColor=Color.Lerp(new Color(.66f,.69f,.70f),new Color(.23f,.33f,.45f),Mathf.Sqrt(height));
                var broad=Mathf.PerlinNoise(direction.x*3.1f+direction.z*1.7f+14,direction.y*5.3f+direction.z*2.1f+31);
                var fine=Mathf.PerlinNoise(direction.x*13+direction.z*7+5,direction.y*17+direction.z*9+9);
                var cloud=Mathf.SmoothStep(0,1,Mathf.Clamp01((broad*.8f+fine*.2f-.38f)*3.2f));
                cloud*=Mathf.SmoothStep(0,.2f,height);var color=Color.Lerp(baseColor,new Color(.76f,.77f,.76f),cloud*.9f);
                if(direction.y<0)color=Color.Lerp(new Color(.25f,.28f,.30f),new Color(.66f,.69f,.70f),Mathf.Clamp01(1+direction.y*2));
                pixels[y*1024+x]=color;
            }
            var source=new Texture2D(1024,512,TextureFormat.RGBA32,false);try{source.SetPixels32(pixels);source.Apply();using(var file=new FileStream(skyPath,FileMode.CreateNew)) {var bytes=source.EncodeToPNG();file.Write(bytes,0,bytes.Length);}}finally{UnityEngine.Object.DestroyImmediate(source);}
            AssetDatabase.ImportAsset(skyPath,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(skyPath);importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.BC7,compressionQuality=75});importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(skyPath);report.ownedTextureBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            var shader=Shader.Find("Skybox/Panoramic");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Panoramic sky unavailable; no silent fallback.");
            var sky=new Material(shader){name="IndieSky"};sky.SetTexture("_MainTex",texture);sky.SetFloat("_Mapping",1);sky.SetFloat("_ImageType",0);sky.SetFloat("_Exposure",1);sky.SetColor("_Tint",new Color(.5f,.5f,.5f));AssetDatabase.CreateAsset(sky,folder+"/IndieSky.mat");RenderSettings.skybox=sky;
            // Reuse this output's own PBR surfaces; never mutate shared source/library materials.
            foreach(var name in new[]{"DetailConcrete","DetailBlueSteel","DetailRustSteel","DetailDarkSteel","DetailYellow"}) {
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat");if(material==null)throw new InvalidOperationException("Exact detailed surface required.");
                material.color=name=="DetailConcrete"?new Color(.88f,.88f,.83f):name=="DetailBlueSteel"?new Color(.30f,.40f,.43f):name=="DetailRustSteel"?new Color(.48f,.26f,.18f):name=="DetailYellow"?new Color(.78f,.55f,.21f):new Color(.22f,.25f,.26f);
            }
            var parent=new GameObject("Indie environment v1").transform;parent.SetParent(root.transform,false);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/PresentationBevel.asset");if(mesh==null)throw new InvalidOperationException("Original bevel mesh required.");
            var metal=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailDarkSteel.mat");var concrete=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailConcrete.mat");
            var groups=new Dictionary<Material,List<CombineInstance>>();
            void Box(Material material,Vector3 p,Vector3 s){if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups.Add(material,list);}list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,Quaternion.identity,s)});}
            // Shallow divider fittings sit against existing solid cover. Roof services stay outside the walkable envelope.
            foreach(var x in new[]{-7.8f,-5.9f,-4f,-2.1f,-.2f,1.7f,3.6f})foreach(var z in new[]{1.59f,2.41f}){
                Box(metal,new Vector3(x,1.2f,z),new Vector3(.055f,2.35f,.025f));
                foreach(var y in new[]{.24f,2.14f})Box(metal,new Vector3(x,y,z),new Vector3(.14f,.08f,.045f));
            }
            foreach(var side in new[]{-1,1})for(var i=0;i<6;i++){
                var z=-13+i*5.1f;Box(concrete,new Vector3(side*16.19f,1.5f,z),new Vector3(.11f,3,.19f));
                Box(metal,new Vector3(side*17.35f,3.8f,z),new Vector3(.14f,4.8f,.14f));
                Box(metal,new Vector3(side*17.35f,6.15f,z+.65f),new Vector3(.14f,.14f,1.3f));
            }
            foreach(var x in new[]{-9f,-5f,8f,12f}){
                Box(metal,new Vector3(x,8.3f,19),new Vector3(1.5f,.6f,1.2f));
                for(var i=0;i<5;i++)Box(concrete,new Vector3(x-.6f+i*.3f,8.35f,18.37f),new Vector3(.11f,.37f,.06f));
            }
            var first=true;foreach(var pair in groups){var combined=new Mesh{name="Original indie facade fittings"};combined.CombineMeshes(pair.Value.ToArray(),true,true);if(first){AssetDatabase.CreateAsset(combined,folder+"/IndieDressing.asset");first=false;}else AssetDatabase.AddObjectToAsset(combined,folder+"/IndieDressing.asset");
                var go=new GameObject("Batched facade fittings "+pair.Key.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=combined;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=ShadowCastingMode.On;report.addedTriangles+=combined.triangles.Length/3;report.addedRenderers++;}
            foreach(var sign in root.GetComponentsInChildren<TextMesh>(true).Where(t=>t.text=="SERVICE YARD / A1"||t.text=="CONTROL / 01"||t.text=="LOGISTICS / 02"||t.text=="SERVICE YARD"))sign.characterSize*=.72f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.54f,.62f);RenderSettings.ambientEquatorColor=new Color(.35f,.38f,.40f);RenderSettings.ambientGroundColor=new Color(.22f,.23f,.24f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.57f,.62f,.66f);RenderSettings.fogDensity=.008f;
            if(RenderSettings.sun==null)throw new InvalidOperationException("Existing courtyard sun required.");RenderSettings.sun.transform.rotation=Quaternion.Euler(32,-28,0);RenderSettings.sun.color=new Color(1,.94f,.85f);RenderSettings.sun.intensity=1.05f;RenderSettings.sun.shadowStrength=.66f;RenderSettings.sun.shadows=LightShadows.Soft;
            foreach(var camera in root.GetComponentsInChildren<Camera>(true)){camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;}
            var after=Collision(root);report.collisionUnchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var value)&&p.Value==value);report.addedColliders=after.Count-before.Count;report.addedLights=root.GetComponentsInChildren<Light>(true).Length-lightCount;report.socketsUnchanged=sockets.All(p=>p.Key.localToWorldMatrix.ToString("R")==p.Value);
            if(!report.collisionUnchanged||!report.socketsUnchanged||report.addedColliders!=0||report.addedLights!=0||report.addedRenderers>2||report.addedTriangles>12000||report.ownedTextureBytes>4*1024*1024)throw new InvalidOperationException("Indie environment preservation/resource gate failed.");
            using(var writer=new StreamWriter(new FileStream(folder+"/indie-environment-report.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
        }
        private static Dictionary<Collider,string> Collision(GameObject root)=>root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+"|"+c.transform.localToWorldMatrix.ToString("R"));
    }
}
""";
}
