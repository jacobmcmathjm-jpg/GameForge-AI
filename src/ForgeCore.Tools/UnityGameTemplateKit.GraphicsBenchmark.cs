namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] CourtyardGraphicsArtifacts =
    ["DetailAsphaltAlbedo.png", "DetailAsphaltNormal.png", "DetailAsphaltMask.png",
     "DetailConcreteAlbedo.png", "DetailConcreteNormal.png", "DetailConcreteMask.png",
     "DetailSteelAlbedo.png", "DetailSteelNormal.png", "DetailSteelMask.png",
     "DetailAsphalt.mat", "DetailConcrete.mat", "DetailBlueSteel.mat", "DetailRustSteel.mat", "DetailDarkSteel.mat", "DetailYellow.mat",
     "CourtyardDetail.asset", "graphics-comparison.png", "graphics-report.json"];

    private const string CourtyardGraphicsSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace ForgeCoreGameTemplates.Editor
{
    // Versioned original procedural art candidate. No source-library approval or photorealism is inferred.
    public static class ForgeCoreCourtyardGraphics
    {
        public const string Profile="industrial_courtyard_detail_v2";
        private const int Size=1024,Width=960,Height=540;
        [Serializable] private sealed class Report
        {
            public int schemaVersion=1,textureSize=Size,textureCount=9,viewCount=3,comparisonWidth=Width*2,comparisonHeight=Height*3;
            public string profile=Profile,source="original_bounded_procedural_template",renderScope="static_editor_comparison_not_runtime_animation";
            public bool collisionUnchanged,visualReviewed=false,rightsReviewed=false,stageComplete=false,approvedLibraryAssetsPlaced=false;
            public int decorativeColliderCount,addedRenderers,addedTriangles,magentaPixels;
            public long ownedTextureMemoryBytes;
            public float minimumLuminanceRange=1;
        }
        private sealed class Surface {public Texture2D albedo,normal,mask;}
        private static readonly Vector3[] Positions={new Vector3(-7,1.7f,-12),new Vector3(-9.5f,1.7f,-4),new Vector3(7,1.7f,7)};
        private static readonly Vector3[] Targets={new Vector3(1,2.4f,12),new Vector3(-11,1,-7.7f),new Vector3(-1,3.6f,17)};
        public static void Apply(GameObject root,string folder)
        {
            if(GraphicsSettings.currentRenderPipeline!=null)throw new InvalidOperationException("Courtyard detail v1 requires the built-in Standard pipeline; unsupported pipelines are not silently converted.");
            var report=new Report();var before=Snapshot(root);
            var originals=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
            if(originals.Any(m=>m.shader==null||!m.shader.isSupported))throw new InvalidOperationException("Baseline material shader unsupported.");
            var baseline=Capture(report);
            Texture2D candidate=null;
            try
            {
                var asphalt=Generate(folder,"Asphalt",0);var concrete=Generate(folder,"Concrete",1);var steel=Generate(folder,"Steel",2);
                var surfaces=new[]{asphalt,concrete,steel};
                report.ownedTextureMemoryBytes=surfaces.Sum(s=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(s.albedo)+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(s.normal)+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(s.mask));
                var replacements=new Dictionary<Material,Material>();
                foreach(var old in originals)
                {
                    var name=old.name;Surface surface=null;float metallic=0,smoothness=.2f;
                    if(name=="YardAsphalt"){surface=asphalt;smoothness=.8f;}
                    else if(name=="YardConcrete"){surface=concrete;smoothness=.16f;}
                    else if(name=="YardBlueSteel"||name=="YardRustSteel"||name=="YardDarkSteel"||name=="YardYellow")
                    {surface=steel;metallic=name=="YardDarkSteel"?.55f:0;smoothness=.32f;}
                    if(surface==null)continue;
                    var detailName=name.Replace("Yard","Detail");
                    var material=new Material(old){name=detailName,mainTexture=surface.albedo};
                    material.SetTexture("_BumpMap",surface.normal);material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.55f);
                    material.SetTexture("_MetallicGlossMap",surface.mask);material.EnableKeyword("_METALLICGLOSSMAP");
                    // Mask red encodes dielectric coating / exposed steel; alpha is smoothness, not roughness.
                    // Concrete/asphalt remain dielectric; steel mask has only sparse exposed chips.
                    material.SetFloat("_Metallic",metallic);material.SetFloat("_GlossMapScale",smoothness);material.SetFloat("_Glossiness",smoothness);
                    material.mainTextureScale=name=="YardAsphalt"?new Vector2(14,14):old.mainTextureScale;
                    AssetDatabase.CreateAsset(material,folder+"/"+detailName+".mat");replacements.Add(old,material);
                }
                if(replacements.Count!=6)throw new InvalidOperationException("Exact six courtyard surface materials required.");
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null&&replacements.TryGetValue(m,out var replacement)?replacement:m).ToArray();
                var blue=replacements.Single(p=>p.Key.name=="YardBlueSteel").Value;
                var dark=replacements.Single(p=>p.Key.name=="YardDarkSteel").Value;
                var yellow=replacements.Single(p=>p.Key.name=="YardYellow").Value;
                var detail=new GameObject("Courtyard detail v1 — bounded original candidate");detail.transform.SetParent(root.transform,false);
                Dress(detail.transform,blue,dark,yellow,folder);
                report.decorativeColliderCount=detail.GetComponentsInChildren<Collider>(true).Length;
                report.addedRenderers=detail.GetComponentsInChildren<Renderer>(true).Length;
                report.addedTriangles=detail.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
                // Soft directional definition plus existing bounded warm practicals. No new real-time lights.
                var sun=RenderSettings.sun;if(sun!=null){sun.intensity=.9f;sun.color=new Color(.94f,.95f,1);sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;}
                RenderSettings.ambientSkyColor=new Color(.39f,.45f,.52f);RenderSettings.ambientEquatorColor=new Color(.26f,.30f,.35f);
                RenderSettings.ambientGroundColor=new Color(.16f,.18f,.20f);RenderSettings.fogColor=new Color(.46f,.51f,.56f);RenderSettings.fogDensity=.008f;
                if(RenderSettings.skybox!=null)RenderSettings.skybox.SetFloat("_Exposure",.72f);
                candidate=Capture(report);
                var after=Snapshot(root);report.collisionUnchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var state)&&p.Value==state);
                WriteNew(folder+"/graphics-report.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
                if(!report.collisionUnchanged||report.decorativeColliderCount!=0||report.addedRenderers>6||report.addedTriangles>25000||report.ownedTextureMemoryBytes>64*1024*1024||report.magentaPixels!=0||report.minimumLuminanceRange<.08f)
                    throw new InvalidOperationException("Courtyard graphics collision/resource/render sanity gate failed; candidate is not accepted art: "+JsonUtility.ToJson(report));
                var sheet=new Texture2D(Width*2,Height*3,TextureFormat.RGB24,false);
                try{sheet.SetPixels(0,0,Width,Height*3,baseline.GetPixels());sheet.SetPixels(Width,0,Width,Height*3,candidate.GetPixels());sheet.Apply();WriteNew(folder+"/graphics-comparison.png",sheet.EncodeToPNG());}
                finally{UnityEngine.Object.DestroyImmediate(sheet);}
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
            }
            finally{UnityEngine.Object.DestroyImmediate(baseline);if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);}
        }
        private static Dictionary<Collider,string> Snapshot(GameObject root)=>root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>
            c.GetType().FullName+"|"+c.transform.localToWorldMatrix.ToString("R")+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+
            (c is BoxCollider b?"|"+b.center.ToString("R")+"|"+b.size.ToString("R"):c is CharacterController a?"|"+a.height+"|"+a.radius+"|"+a.center.ToString("R"):""));
        private static void WriteNew(string path,byte[] data){using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))stream.Write(data,0,data.Length);}
        // Periodic domain blending avoids obvious tile seams; all noise is deterministic.
        private static float Noise(float u,float v,float scale)
        {
            float N(float x,float y)=>Mathf.PerlinNoise(x*scale+17.13f,y*scale+31.71f);
            return Mathf.Lerp(Mathf.Lerp(N(u,v),N(u-1,v),u),Mathf.Lerp(N(u,v-1),N(u-1,v-1),u),v);
        }
        private static Surface Generate(string folder,string name,int kind)
        {
            var heights=new float[Size*Size];var noise=new float[Size*Size];
            for(var y=0;y<Size;y++)for(var x=0;x<Size;x++)
            {
                var u=x/(float)Size;var v=y/(float)Size;var fine=Noise(u,v,140);var coarse=Noise(u,v,7);
                var crack=Mathf.Pow(1-Mathf.Abs(Noise(u,v,19)*2-1),20);
                var i=y*Size+x;noise[i]=fine*.6f+coarse*.4f;
                heights[i]=kind==0?fine*.28f+coarse*.12f-crack*.10f:kind==1?fine*.12f+coarse*.15f-crack*.035f:fine*.025f+coarse*.04f;
            }
            var albedo=new Color32[Size*Size];var normal=new Color32[Size*Size];var mask=new Color32[Size*Size];
            for(var y=0;y<Size;y++)for(var x=0;x<Size;x++)
            {
                var i=y*Size+x;var n=noise[i];var h=heights[i];var wet=kind==0?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.76f,Noise(x/(float)Size,y/(float)Size,3))):0;
                // Aggregate-scale roughness, restrained concrete relief and co-located wet/dark/smooth regions.
                var shade=kind==0?(.32f+h*.30f)*(1-wet*.23f):kind==1?.59f+h*.24f:.70f+n*.30f;
                albedo[i]=new Color(shade*(kind==0?.93f:1.02f),shade,shade*(kind==0?1.04f:.95f),1);
                float H(int a,int b)=>heights[((b+Size)%Size)*Size+(a+Size)%Size];
                var strength=kind==0?Mathf.Lerp(1.6f,.35f,wet):kind==1?1.1f:.7f;
                var direction=new Vector3((H(x-1,y)-H(x+1,y))*strength,(H(x,y-1)-H(x,y+1))*strength,1).normalized;
                normal[i]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);
                var exposed=kind==2&&n>.72f?.65f:0;mask[i]=new Color(exposed,0,0,kind==0?Mathf.Lerp(.23f+n*.13f,.92f,wet):kind==1?.35f+n*.2f:.65f+n*.25f);
            }
            return new Surface{albedo=Map(folder,"Detail"+name+"Albedo.png",albedo,false,true),normal=Map(folder,"Detail"+name+"Normal.png",normal,true,false),mask=Map(folder,"Detail"+name+"Mask.png",mask,false,false)};
        }
        private static Texture2D Map(string folder,string name,Color32[] pixels,bool normal,bool srgb)
        {
            var path=folder+"/"+name;var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,!srgb);
            try{texture.SetPixels32(pixels);texture.Apply();WriteNew(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;
            importer.mipmapEnabled=true;importer.isReadable=false;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.maxTextureSize=Size;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=75;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=Size,format=normal?TextureImporterFormat.BC5:TextureImporterFormat.BC7,compressionQuality=75});
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void Dress(Transform parent,Material blue,Material dark,Material yellow,string folder)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var cubeMesh=cube.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(cube);
            var cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);var cylinderMesh=cylinder.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(cylinder);
            void Part(string name,Vector3 position,Vector3 scale,Material material,bool round=false,Vector3 rotation=default)
            {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localEulerAngles=rotation;
             go.AddComponent<MeshFilter>().sharedMesh=round?cylinderMesh:cubeMesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
            // Mounted against existing boundary/backdrop surfaces; deliberately outside the walkable envelope.
            for(var side=-1;side<=1;side+=2)
            {
                for(var n=0;n<4;n++)
                {
                    var z=-10+n*7;var x=side*17.5f;
                    Part("Wall-mounted ventilation cabinet",new Vector3(x,3.5f,z),new Vector3(.7f,1.15f,1.6f),blue);
                    for(var l=0;l<9;l++)Part("Ventilation grille louvre",new Vector3(x-side*.37f,3.08f+l*.10f,z),new Vector3(.04f,.035f,1.4f),dark);
                    for(var b=0;b<2;b++)Part("Exposed pipe flange",new Vector3(x-side*.4f,1.2f+b*3,z+1.3f),new Vector3(.21f,.035f,.21f),dark,true);
                    Part("Vertical service pipe",new Vector3(x-side*.4f,2.8f,z+1.3f),new Vector3(.12f,2.5f,.12f),dark,true);
                }
                for(var n=0;n<12;n++)Part("Facade horizontal support",new Vector3(side*18.1f,.6f+n*.52f,0),new Vector3(.1f,.06f,30),dark);
            }
            // Rooftop machinery and visible cable tray: skyline detail, not new navigation obstacles.
            for(var i=0;i<4;i++)
            {
                Part("Control-room rooftop unit",new Vector3(-2+i*1.5f,9,20),new Vector3(1.1f,.7f,1.4f),blue);
                Part("Rooftop vent cap",new Vector3(-2+i*1.5f,9.45f,20),new Vector3(.42f,.1f,.42f),dark,true);
            }
            Part("Cable tray spine",new Vector3(0,3.2f,16.8f),new Vector3(28,.07f,.15f),dark);
            for(var i=0;i<35;i++)Part("Cable-tray rung",new Vector3(-13.6f+i*.8f,3.22f,16.8f),new Vector3(.045f,.05f,.44f),dark);
            for(var i=0;i<18;i++)Part("Painted pipe identification band",new Vector3(i%2==0?-17.1f:17.1f,1.8f,-11+(i/2)*3),new Vector3(.13f,.04f,.13f),yellow,true);
            var filters=parent.GetComponentsInChildren<MeshFilter>();var first=true;
            foreach(var group in filters.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial))
            {
                var mesh=new Mesh{name="Courtyard detail batch "+group.Key.name};mesh.indexFormat=IndexFormat.UInt32;
                mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
                if(first){AssetDatabase.CreateAsset(mesh,folder+"/CourtyardDetail.asset");first=false;}else AssetDatabase.AddObjectToAsset(mesh,folder+"/CourtyardDetail.asset");
                var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var filter in filters)UnityEngine.Object.DestroyImmediate(filter.gameObject);
        }
        private static Texture2D Capture(Report report)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Graphics-enabled Unity is required for the courtyard benchmark.");
            var sheet=new Texture2D(Width,Height*3,TextureFormat.RGB24,false);
            var camera=new GameObject("Temporary fixed graphics benchmark camera").AddComponent<Camera>();camera.fieldOfView=66;camera.nearClipPlane=.08f;camera.farClipPlane=180;camera.allowHDR=false;
            camera.clearFlags=CameraClearFlags.Skybox;var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try
            {
                target.Create();camera.targetTexture=target;
                for(var view=0;view<3;view++)
                {
                    camera.transform.position=Positions[view];camera.transform.LookAt(Targets[view]);camera.Render();RenderTexture.active=target;
                    var frame=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                    try
                    {
                        frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply();var pixels=frame.GetPixels32();float min=1,max=0;
                        foreach(var p in pixels){if(p.r>170&&p.b>170&&p.g<70)report.magentaPixels++;var l=(p.r*.2126f+p.g*.7152f+p.b*.0722f)/255;min=Mathf.Min(min,l);max=Mathf.Max(max,l);}
                        report.minimumLuminanceRange=Mathf.Min(report.minimumLuminanceRange,max-min);sheet.SetPixels32(0,(2-view)*Height,Width,Height,pixels);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(frame);}
                }
                sheet.Apply();return sheet;
            }
            catch{UnityEngine.Object.DestroyImmediate(sheet);throw;}
            finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
""";
}
