namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string ConnectedInteriorSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Original bounded template dressing, not downloaded/approved art or functional computers.
    public static class ForgeCoreConnectedInterior
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,screenWidth=256,screenHeight=128,partsBeforeMaterialBatch,addedTriangles,addedColliders,additionalMaterialBatches=1;
            public string profile="industrial_connected_interior_v1",source="original_code_authored_geometry_and_screen_pattern";
            public bool collisionUnchanged,visualReviewed=false,rightsReviewed=false,stageComplete=false,animationsChanged=false,audioChanged=false;
            public long screenTextureBytes;
        }
        public static void Apply(GameObject root,Transform parent,string folder,Material blue,Material steel,Material concrete,Material lamp) {
            var before=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+"|"+c.transform.localToWorldMatrix.ToString("R"));
            var report=new Report();var bevel=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/PresentationBevel.asset");
            if(bevel==null)throw new InvalidOperationException("Owned bevel source required.");
            void Part(string name,Vector3 at,Vector3 size,Material material,Vector3 angles=default){
                var go=new GameObject("Connected interior / "+name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.transform.localEulerAngles=angles;
                go.AddComponent<MeshFilter>().sharedMesh=bevel;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                report.partsBeforeMaterialBatch++;report.addedTriangles+=bevel.triangles.Length/3;
            }
            var path=folder+"/ConnectedScreen.png";var data=new Color32[256*128];
            for(var y=0;y<128;y++)for(var x=0;x<256;x++){
                var grid=x%32==0||y%16==0;var c=grid?new Color32(23,49,54,255):new Color32(8,18,23,255);
                if(x>12&&x<244&&y>106&&y<118)c=new Color32(68,137,143,255);
                if(x>18&&x<154&&y>23&&y<94&&((y/9)%2==0)&&x<42+(y/9)*10)c=new Color32(65,117,118,255);
                if(x>178&&x<230&&y>22&&y<95&&(x<183||x>225||y<27||y>90||Math.Abs(y-(x-178))<2))c=new Color32(150,159,107,255);
                data[y*256+x]=c;
            }
            var source=new Texture2D(256,128,TextureFormat.RGBA32,false);
            try{source.SetPixels32(data);source.Apply();var bytes=source.EncodeToPNG();using(var file=new FileStream(path,FileMode.CreateNew))file.Write(bytes,0,bytes.Length);}
            finally{UnityEngine.Object.DestroyImmediate(source);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=256,format=TextureImporterFormat.BC7,compressionQuality=75});importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);report.screenTextureBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            var screen=new Material(Shader.Find("Standard")){name="Original connected terminal pattern",mainTexture=texture,color=Color.white};
            screen.SetFloat("_Glossiness",.24f);screen.EnableKeyword("_EMISSION");screen.SetTexture("_EmissionMap",texture);screen.SetColor("_EmissionColor",new Color(.7f,.85f,.85f));screen.globalIlluminationFlags=UnityEngine.MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(screen,folder+"/ConnectedScreen.mat");
            // Frame the large room with readable structure. All additions are visual only.
            foreach(var z in new[]{20f,28f,36f,44f}){
                Part("warehouse roof beam",new Vector3(0,6.82f,z),new Vector3(32.4f,.36f,.32f),steel);
                foreach(var x in new[]{-16.15f,16.15f})Part("warehouse wall column",new Vector3(x,3.45f,z),new Vector3(.24f,6.9f,.35f),steel);
            }
            foreach(var x in new[]{-14f,-10f,-6f,-2f,2f,6f,10f,14f})
                Part("warehouse north rib",new Vector3(x,3.4f,46.16f),new Vector3(.12f,6.4f,.12f),steel);
            foreach(var z in new[]{23f,31f,40f}){
                foreach(var x in new[]{3.65f,8.35f})Part("cargo corner protection",new Vector3(x,1.25f,z),new Vector3(.13f,2.5f,3.08f),steel);
                for(var i=0;i<10;i++)Part("cargo front corrugation",new Vector3(3.85f+i*.48f,1.25f,z-1.54f),new Vector3(.09f,2.25f,.09f),blue);
                Part("cargo top rail",new Vector3(6,2.52f,z),new Vector3(5.1f,.1f,3.1f),steel);
            }
            // Nine angled monitors and tactile workstation rails: decorative, no promised computer gameplay.
            foreach(var x in new[]{23f,32f,41f}){
                Part("console plinth",new Vector3(x,.12f,27),new Vector3(3.2f,.24f,1.5f),steel);
                for(var i=-1;i<=1;i++){
                    var p=new Vector3(x+i*.88f,1.62f,27.05f);
                    Part("monitor casing",p,new Vector3(.82f,.64f,.13f),steel,new Vector3(12,0,0));
                    Part("monitor display",p+new Vector3(0,.012f,.083f),new Vector3(.72f,.51f,.035f),screen,new Vector3(12,0,0));
                    Part("monitor support",new Vector3(p.x,1.37f,26.99f),new Vector3(.12f,.35f,.12f),steel);
                }
                Part("console keyboard rail",new Vector3(x,1.31f,27.55f),new Vector3(2.7f,.055f,.28f),concrete);
            }
            foreach(var x in new[]{21f,28f,35f,42f}){
                Part("control north wall panel",new Vector3(x,2.5f,46.19f),new Vector3(6.6f,4.6f,.12f),blue);
                Part("control ceiling rib",new Vector3(x,4.92f,31.5f),new Vector3(.14f,.16f,29),steel);
            }
            foreach(var z in new[]{22f,30f,38f})Part("control east wall conduit",new Vector3(46.23f,2.4f,z),new Vector3(.12f,4.5f,.12f),steel);
            foreach(var p in new[]{new Vector3(25,4.76f,32),new Vector3(39,4.76f,32)})Part("control ceiling light",p,new Vector3(3,.08f,.45f),lamp);
            var after=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+"|"+c.transform.localToWorldMatrix.ToString("R"));
            report.addedColliders=after.Count-before.Count;report.collisionUnchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var value)&&value==p.Value);
            if(!report.collisionUnchanged||report.addedColliders!=0||report.addedTriangles>20000||report.partsBeforeMaterialBatch>180||report.screenTextureBytes<=0||report.screenTextureBytes>256*128*4)throw new InvalidOperationException("Connected visual-only interior resource/preservation gate failed.");
            using(var writer=new StreamWriter(new FileStream(folder+"/connected-interior-report.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));
        }
        [Serializable]private sealed class Review {
            public int schemaVersion=1,width=1536,height=1296,viewCount=3,magentaPixels,changedPixels,protectedPoseTransforms;
            public bool combatAndObjectivePosesUnchanged;
            public string scope="static_editor_comparison_left_before_right_after_not_runtime_animation",profile="industrial_connected_interior_v1";
            public string baseline="fresh_same_output_before_optional_interior_dressing_not_previous_v6_binary";
            public string[] views={"warehouse_entry","warehouse_reverse","control_room_workstations"};
            public bool humanArtAccepted=false,stageComplete=false;
        }
        public static ReviewCapture BeginReview(GameObject root)=>new ReviewCapture(root);
        public sealed class ReviewCapture:IDisposable {
            private Texture2D before;private readonly int beforeMagenta;private bool written;
            private readonly Dictionary<Transform,string> poses;
            public ReviewCapture(GameObject root){
                var session=root.GetComponentInChildren<SecurityCombatSession>();var mission=root.GetComponent<IndustrialMission>();
                poses=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones.Concat(new[]{r.transform})).Concat(mission.objectives).Concat(session.enemies.Select(e=>e.transform)).Concat(new[]{session.player.transform}).Where(t=>t!=null).Distinct().ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));
                if(poses.Count<10||poses.Count>1024)throw new InvalidOperationException("Bounded actor/objective comparison pose set required.");
                before=Snapshot(out beforeMagenta);
            }
            public void Save(string folder) {
                if(written||before==null)throw new InvalidOperationException("One fresh owned static comparison required.");
                var after=Snapshot(out var afterMagenta);var sheet=new Texture2D(1536,1296,TextureFormat.RGB24,false);
                try{
                    var a=before.GetPixels32();var b=after.GetPixels32();
                    var report=new Review{magentaPixels=beforeMagenta+afterMagenta,changedPixels=a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>15?1:0).Sum(),protectedPoseTransforms=poses.Count,combatAndObjectivePosesUnchanged=poses.All(p=>p.Key!=null&&p.Key.localToWorldMatrix.ToString("R")==p.Value)};
                    if(!report.combatAndObjectivePosesUnchanged)throw new InvalidOperationException("Static comparison actor/objective poses changed across dressing.");
                    if(report.magentaPixels>0)throw new InvalidOperationException("Static interior comparison contains error-magenta; candidate not accepted.");
                    if(report.changedPixels<1000)throw new InvalidOperationException("Static interior comparison does not demonstrate the bounded geometry/material change.");
                    sheet.SetPixels(0,0,768,1296,before.GetPixels());sheet.SetPixels(768,0,768,1296,after.GetPixels());sheet.Apply();
                    var bytes=sheet.EncodeToPNG();using(var file=new FileStream(folder+"/connected-interior-review.png",FileMode.CreateNew))file.Write(bytes,0,bytes.Length);
                    using(var writer=new StreamWriter(new FileStream(folder+"/connected-interior-review.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));
                    var reviewPath=folder+"/connected-interior-review.png";
                    AssetDatabase.ImportAsset(reviewPath,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(reviewPath);importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();written=true;
                }finally{UnityEngine.Object.DestroyImmediate(after);UnityEngine.Object.DestroyImmediate(sheet);}
            }
            public void Dispose(){if(before!=null)UnityEngine.Object.DestroyImmediate(before);before=null;}
        }
        private static Texture2D Snapshot(out int magenta) {
            magenta=0;var cameraObject=new GameObject("Owned static connected interior review camera");var camera=cameraObject.AddComponent<Camera>();
            var rt=new RenderTexture(768,432,24,RenderTextureFormat.ARGB32);var frame=new Texture2D(768,432,TextureFormat.RGB24,false);var sheet=new Texture2D(768,1296,TextureFormat.RGB24,false);var prior=RenderTexture.active;var success=false;
            try {
                camera.enabled=false;camera.fieldOfView=66;camera.nearClipPlane=.08f;camera.farClipPlane=100;camera.allowHDR=false;camera.clearFlags=CameraClearFlags.Skybox;camera.targetTexture=rt;
                var positions=new[]{new Vector3(-10,1.7f,20.5f),new Vector3(-12,1.7f,40),new Vector3(19.5f,1.7f,34)};
                var targets=new[]{new Vector3(0,2.8f,34),new Vector3(7,2.2f,30),new Vector3(34,1.6f,27)};
                for(var i=0;i<3;i++){
                    camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);camera.Render();RenderTexture.active=rt;frame.ReadPixels(new Rect(0,0,768,432),0,0);frame.Apply();
                    var colors=frame.GetPixels32();magenta+=colors.Count(c=>c.r>200&&c.b>200&&c.g<80);sheet.SetPixels(0,(2-i)*432,768,432,frame.GetPixels());
                }
                sheet.Apply();success=true;return sheet;
            }finally{RenderTexture.active=prior;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraObject);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(frame);if(!success)UnityEngine.Object.DestroyImmediate(sheet);}
        }
    }
}
""";
}
