namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] CourtyardPropArtifacts =
    ["PropWoodAlbedo.png","PropWoodNormal.png","PropWood.mat","PropWoodDark.mat","PropBareSteel.mat",
     "CourtyardProps.asset","prop-comparison.png","prop-presentation-report.json"];
    private const string CourtyardPropsSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Original template geometry. Never promotes, imports or impersonates a reviewed Blender source.
    public static class ForgeCoreCourtyardProps
    {
        public const string Profile="industrial_template_props_v1";
        [Serializable] private sealed class Family {public string family;public int[] triangles;}
        [Serializable] private sealed class Report {
            public int schemaVersion=1,propCount,lodGroupCount,lodLevels=3,disabledProxyRenderers,decorativeColliderCount,addedLights,highestDetailTriangles,maximumDrawSlots;
            public string profile=Profile,source="original_code_authored_template_not_blender_or_vault",collisionPolicy="existing_conservative_solid_boxes_unchanged";
            public bool collisionUnchanged,visualReviewed=false,rightsReviewed=false,approvedLibraryAssetsPlaced=false,stageComplete=false;
            public int textureSize=1024,textureCount=2,comparisonWidth=1440,comparisonHeight=810,viewCount=2,magentaPixels;
            public float minimumLuminanceRange=1;public long ownedTextureMemoryBytes;public Family[] families;
        }
        private sealed class Palette {public Mesh box;public Material wood,woodDark,steel,blue,rust,concrete,yellow;}
        private sealed class Shape {public Mesh mesh;public Material[] materials;}
        private sealed class Parts {
            public readonly List<(Mesh Mesh,Matrix4x4 Transform,Material Material)> items=new List<(Mesh,Matrix4x4,Material)>();
            public readonly HashSet<Mesh> owned=new HashSet<Mesh>();public Palette p;
            public void Box(Vector3 at,Vector3 size,Material material,Vector3 rotation=default)=>items.Add((p.box,Matrix4x4.TRS(at,Quaternion.Euler(rotation),size),material));
            public void Beam(Vector3 a,Vector3 b,float width,Material material) {var delta=b-a;items.Add((p.box,Matrix4x4.TRS((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,delta),new Vector3(width,delta.magnitude,width)),material));}
            public void Cylinder(Vector3 at,float radius,float height,int sides,Material material) {var mesh=Round(sides);owned.Add(mesh);items.Add((mesh,Matrix4x4.TRS(at,Quaternion.identity,new Vector3(radius,height,radius)),material));}
            public Shape Finish(string name,string folder,ref bool first) {
                var materials=items.Select(i=>i.Material).Distinct().ToArray();var intermediate=new List<Mesh>();
                try {
                    foreach(var material in materials) {var sub=new Mesh();sub.indexFormat=IndexFormat.UInt32;
                        sub.CombineMeshes(items.Where(i=>i.Material==material).Select(i=>new CombineInstance{mesh=i.Mesh,transform=i.Transform}).ToArray(),true,true);intermediate.Add(sub);}
                    var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(intermediate.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,true);mesh.RecalculateTangents();mesh.RecalculateBounds();
                    if(first){AssetDatabase.CreateAsset(mesh,folder+"/CourtyardProps.asset");first=false;}else AssetDatabase.AddObjectToAsset(mesh,folder+"/CourtyardProps.asset");
                    return new Shape{mesh=mesh,materials=materials};
                }finally{foreach(var mesh in intermediate.Concat(owned))UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
        public static void Apply(GameObject root,string folder)
        {
            if(GraphicsSettings.currentRenderPipeline!=null)throw new InvalidOperationException("Template prop presentation requires built-in Standard.");
            var report=new Report();var before=Snapshot(root);var proxies=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)).ToArray();
            if(proxies.Length!=10||root.GetComponentsInChildren<IndustrialPropIdentity>(true).Length!=0)throw new InvalidOperationException("Exactly ten original proxy slots required; never replace a library identity.");
            var baseline=Capture(report);Texture2D candidate=null;
            try {
                Material M(string name)=>AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat")??throw new InvalidOperationException("Missing original prop palette.");
                var maps=Wood(folder);var p=new Palette{box=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/PresentationBevel.asset"),blue=M("YardBlueSteel"),rust=M("YardRustSteel"),concrete=M("YardConcrete"),yellow=M("YardYellow")};
                p.wood=WoodMaterial(folder,"PropWood",Color.white,maps[0],maps[1]);p.woodDark=WoodMaterial(folder,"PropWoodDark",new Color(.66f,.64f,.59f),maps[0],maps[1]);
                p.steel=new Material(Shader.Find("Standard")){name="PropBareSteel",color=new Color(.16f,.18f,.19f),enableInstancing=true};p.steel.SetFloat("_Metallic",.82f);p.steel.SetFloat("_Glossiness",.34f);AssetDatabase.CreateAsset(p.steel,folder+"/PropBareSteel.mat");
                report.ownedTextureMemoryBytes=maps.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
                var decor=new GameObject("Courtyard shaped props — original template candidate").transform;decor.SetParent(root.transform,false);
                var families=new Dictionary<string,Shape[]>();var measurements=new List<Family>();var first=true;
                foreach(var family in new[]{"barrel","crate","barrier","fence","cargo"}) {
                    var shapes=new Shape[3];for(var level=0;level<3;level++)shapes[level]=Build(family,level,p,folder,ref first);
                    families.Add(family,shapes);measurements.Add(new Family{family=family,triangles=shapes.Select(s=>s.mesh.triangles.Length/3).ToArray()});
                }
                foreach(var proxy in proxies) {
                    var family=families.Keys.Single(k=>proxy.name.Contains(k));var collider=proxy.GetComponent<BoxCollider>();var renderer=proxy.GetComponent<MeshRenderer>();
                    if(collider==null||!collider.enabled||collider.isTrigger||proxy.gameObject.layer!=8||renderer==null||!renderer.enabled)throw new InvalidOperationException("Original solid proxy contract changed.");
                    var shapes=families[family];var instance=new GameObject("Template "+family+" • unreviewed").transform;instance.SetParent(decor,false);
                    // Independent unit-scale root, not a child of the non-uniformly scaled collider cube.
                    instance.position=collider.bounds.center-Vector3.up*collider.bounds.extents.y;
                    var lods=new LOD[3];for(var level=0;level<3;level++) {
                        var child=new GameObject("LOD"+level);child.transform.SetParent(instance,false);child.AddComponent<MeshFilter>().sharedMesh=shapes[level].mesh;
                        var r=child.AddComponent<MeshRenderer>();r.sharedMaterials=shapes[level].materials;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                        lods[level]=new LOD(level==0?.42f:level==1?.14f:.012f,new Renderer[]{r});
                    }
                    var group=instance.gameObject.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(lods);group.RecalculateBounds();
                    var bounds=shapes[0].mesh.bounds;var expected=collider.bounds.size;
                    if(Vector3.Distance(bounds.size,expected)>.015f||Mathf.Abs(bounds.min.y)>.006f)throw new InvalidOperationException("Shaped prop escaped the authored metre-scale footprint.");
                    // Rendering only. Keep the original collider, layer, transform and object identity intact.
                    renderer.enabled=false;report.disabledProxyRenderers++;report.propCount++;report.highestDetailTriangles+=shapes[0].mesh.triangles.Length/3;report.maximumDrawSlots+=shapes[0].materials.Length;
                }
                report.families=measurements.ToArray();report.lodGroupCount=decor.GetComponentsInChildren<LODGroup>().Length;
                report.decorativeColliderCount=decor.GetComponentsInChildren<Collider>(true).Length;report.addedLights=decor.GetComponentsInChildren<Light>(true).Length;
                var after=Snapshot(root);report.collisionUnchanged=before.Count==after.Count&&before.All(e=>after.TryGetValue(e.Key,out var state)&&state==e.Value);
                candidate=Capture(report);
                WriteNew(folder+"/prop-presentation-report.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
                if(!report.collisionUnchanged||report.disabledProxyRenderers!=10||report.lodGroupCount!=10||report.decorativeColliderCount!=0||report.addedLights!=0||
                    report.highestDetailTriangles>60000||report.maximumDrawSlots>40||report.ownedTextureMemoryBytes>8*1024*1024||report.magentaPixels!=0||report.minimumLuminanceRange<.08f||
                    report.families.Any(f=>f.triangles[0]<=f.triangles[1]||f.triangles[1]<=f.triangles[2]))throw new InvalidOperationException("Template prop resource/collision/LOD/render gates failed: "+JsonUtility.ToJson(report));
                var sheet=new Texture2D(1440,810,TextureFormat.RGB24,false);
                try{sheet.SetPixels(0,0,720,810,baseline.GetPixels());sheet.SetPixels(720,0,720,810,candidate.GetPixels());sheet.Apply();WriteNew(folder+"/prop-comparison.png",sheet.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(sheet);}
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
            }finally{UnityEngine.Object.DestroyImmediate(baseline);if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);}
        }
        private static Shape Build(string family,int lod,Palette p,string folder,ref bool first)
        {
            var b=new Parts{p=p};var round=lod==0?48:lod==1?24:12;
            void Box(float x,float y,float z,float w,float h,float d,Material material)=>b.Box(new Vector3(x,y,z),new Vector3(w,h,d),material);
            if(family=="barrel") {
                b.Cylinder(new Vector3(0,.55f,0),.332f,1.1f,round,p.blue);
                foreach(var y in new[]{.025f,.29f,.81f,1.075f})b.Cylinder(new Vector3(0,y,0),.34f,.035f,round,p.steel);
                if(lod<2){b.Cylinder(new Vector3(.15f,1.091f,0),.028f,.016f,12,p.steel);b.Cylinder(new Vector3(-.15f,1.089f,0),.012f,.018f,8,p.steel);}
                if(lod==0)foreach(var y in new[]{.12f,.98f})b.Cylinder(new Vector3(0,y,0),.334f,.007f,round,p.rust);
            } else if(family=="crate") {
                var count=lod==0?7:lod==1?4:1;
                for(var side=-1;side<=1;side+=2)for(var n=0;n<count;n++) {
                    var y=.045f+(n+.5f)*.71f/count;Box(0,y,side*.37f,.9f,.71f/count*.975f,.06f,n%3==0?p.woodDark:p.wood);
                    Box(side*.47f,y,0,.06f,.71f/count*.975f,.8f,n%3==0?p.woodDark:p.wood);
                }
                for(var n=0;n<count;n++)Box(0,.775f,-.4f+(n+.5f)*.8f/count,1,.05f,.8f/count*.98f,p.wood);
                Box(0,.025f,0,1,.05f,.8f,p.woodDark);
                if(lod<2)for(var side=-1;side<=1;side+=2) {
                    foreach(var x in new[]{-.39f,.39f})Box(x,.4f,side*.363f,.08f,.71f,.074f,p.woodDark);
                    b.Beam(new Vector3(-.35f,.12f,side*.365f),new Vector3(.35f,.68f,side*.365f),.045f,p.woodDark);
                    if(lod==0)foreach(var x in new[]{-.39f,.39f})foreach(var y in new[]{.12f,.68f})Box(x,y,side*.396f,.021f,.021f,.008f,p.steel);
                }
            } else if(family=="barrier") {
                var mesh=Jersey();b.owned.Add(mesh);b.items.Add((mesh,Matrix4x4.identity,p.concrete));
                if(lod<2)for(var side=-1;side<=1;side+=2)for(var i=0;i<5;i++)Box(-.96f+i*.48f,.13f,side*.299f,.25f,.075f,.001f,p.yellow);
                if(lod==0)foreach(var x in new[]{-.8f,.8f}) {Box(x,.99f,0,.11f,.02f,.11f,p.steel);Box(x,.017f,0,.018f,.034f,.59f,p.steel);}
            } else if(family=="fence") {
                foreach(var x in new[]{-1.465f,1.465f})Box(x,1.1f,0,.07f,2.2f,.16f,p.steel);
                foreach(var y in new[]{.045f,2.155f})Box(0,y,0,3,.06f,.06f,p.steel);
                var nx=lod==0?25:lod==1?13:7;var ny=lod==0?16:lod==1?9:5;
                for(var i=0;i<nx;i++)Box(-1.39f+i*2.78f/(nx-1),1.1f,0,.009f,2.08f,.009f,p.steel);
                for(var i=0;i<ny;i++)Box(0,.10f+i*2f/(ny-1),.007f,2.86f,.009f,.009f,p.steel);
                if(lod==0)foreach(var x in new[]{-1.465f,1.465f})foreach(var y in new[]{.2f,2f})Box(x,y,-.05f,.065f,.1f,.04f,p.blue);
            } else if(family=="cargo") {
                Box(0,1.295f,0,2.34f,2.47f,5.98f,p.blue);
                foreach(var x in new[]{-1.18f,1.18f})foreach(var z in new[]{-2.99f,2.99f})Box(x,1.295f,z,.08f,2.59f,.08f,p.steel);
                var ribs=lod==0?28:lod==1?14:5;
                for(var side=-1;side<=1;side+=2) {
                    for(var n=0;n<ribs;n++)Box(side*1.19f,1.295f,-2.8f+n*5.6f/(ribs-1),.06f,2.34f,.06f,p.blue);
                    Box(0,.075f,side*2.98f,2.36f,.15f,.1f,p.steel);Box(0,2.515f,side*2.98f,2.36f,.15f,.1f,p.steel);
                    foreach(var x in new[]{-.585f,.585f})Box(x,1.295f,side*3.011f,1.145f,2.35f,.038f,p.blue);
                    if(lod<2)foreach(var x in new[]{-.65f,.65f}) {
                        Box(x,1.295f,side*3.023f,.028f,2.20f,.014f,p.steel);
                        Box(x,.95f,side*3.023f,.20f,.035f,.014f,p.steel);
                        if(lod==0)foreach(var y in new[]{.35f,1.25f,2.20f})Box(Mathf.Sign(x)*1.09f,y,side*3.02f,.12f,.055f,.02f,p.steel);
                    }
                }
                if(lod==0)for(var n=0;n<14;n++)Box(0,2.55f,-2.7f+n*.415f,2.25f,.04f,.06f,p.blue);
            } else throw new ArgumentException("Unknown original template prop.");
            return b.Finish(family+" LOD"+lod,folder,ref first);
        }
        private static Mesh Jersey()
        {
            var profile=new[]{new Vector2(-.3f,0),new Vector2(.3f,0),new Vector2(.3f,.22f),new Vector2(.105f,.65f),new Vector2(.105f,1),new Vector2(-.105f,1),new Vector2(-.105f,.65f),new Vector2(-.3f,.22f)};
            var verts=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            void Face(params Vector3[] points){var start=verts.Count;foreach(var point in points){verts.Add(point);uv.Add(new Vector2(point.x+point.z,point.y));}for(var i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}}
            Vector3 P(int i,float x)=>new Vector3(x,profile[i].y,profile[i].x);
            // Profile lies in YZ; outward side faces and end caps use separate vertices for hard cast edges.
            for(var i=0;i<8;i++){var next=(i+1)%8;Face(P(i,-1.2f),P(i,1.2f),P(next,1.2f),P(next,-1.2f));}
            Face(Enumerable.Range(0,8).Select(i=>P(i,-1.2f)).ToArray());Face(Enumerable.Range(0,8).Select(i=>P(7-i,1.2f)).ToArray());
            var mesh=new Mesh{name="Original jersey profile"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh Round(int sides)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var normals=new List<Vector3>();var triangles=new List<int>();
            void V(Vector3 at,Vector3 normal,Vector2 tex){vertices.Add(at);normals.Add(normal);uv.Add(tex);}
            for(var n=0;n<=sides;n++){var angle=n*2*Mathf.PI/sides;var normal=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));for(var y=0;y<2;y++)V(normal+Vector3.up*(y-.5f),normal,new Vector2(n/(float)sides,y));}
            for(var n=0;n<sides;n++){var i=n*2;triangles.AddRange(new[]{i,i+1,i+3,i,i+3,i+2});}
            foreach(var sign in new[]{-1,1}){var start=vertices.Count;V(new Vector3(0,sign*.5f,0),Vector3.up*sign,new Vector2(.5f,.5f));
                for(var n=0;n<=sides;n++){var angle=n*2*Mathf.PI/sides;V(new Vector3(Mathf.Cos(angle),sign*.5f,Mathf.Sin(angle)),Vector3.up*sign,new Vector2(Mathf.Cos(angle)*.5f+.5f,Mathf.Sin(angle)*.5f+.5f));}
                for(var n=0;n<sides;n++)triangles.AddRange(sign>0?new[]{start,start+n+2,start+n+1}:new[]{start,start+n+1,start+n+2});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        private static Texture2D[] Wood(string folder)
        {
            const int size=1024;var albedo=new Color32[size*size];var normals=new Color32[size*size];
            float H(float u,float v) {var warp=Mathf.Sin(u*6.28318f)*.008f+Mathf.Sin(u*18.8495f)*.003f;return Mathf.Sin((v+warp)*6.28318f*60)*.035f+Mathf.Sin((v+warp)*6.28318f*211)*.014f;}
            for(var y=0;y<size;y++)for(var x=0;x<size;x++) {var u=x/(float)size;var v=y/(float)size;var value=H(u,v);var n=Mathf.PerlinNoise(u*11+23,v*11+7);
                var shade=.88f+value+n*.16f;albedo[y*size+x]=new Color(.43f*shade,.31f*shade,.185f*shade,1);
                var direction=new Vector3((H(u-1f/size,v)-H(u+1f/size,v))*.6f,(H(u,v-1f/size)-H(u,v+1f/size))*.6f,1).normalized;
                normals[y*size+x]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);}
            Texture2D Map(string name,Color32[] pixels,bool normal){var path=folder+"/"+name;var t=new Texture2D(size,size,TextureFormat.RGBA32,false,normal);
                try{t.SetPixels32(pixels);t.Apply();WriteNew(path,t.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(t);}
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                imp.sRGBTexture=!normal;imp.isReadable=false;imp.mipmapEnabled=true;imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=8;imp.maxTextureSize=size;imp.textureCompression=TextureImporterCompression.CompressedHQ;
                imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=size,format=normal?TextureImporterFormat.BC5:TextureImporterFormat.BC7,compressionQuality=75});imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
            return new[]{Map("PropWoodAlbedo.png",albedo,false),Map("PropWoodNormal.png",normals,true)};
        }
        private static Material WoodMaterial(string folder,string name,Color tint,Texture albedo,Texture normal){var m=new Material(Shader.Find("Standard")){name=name,color=tint,mainTexture=albedo,enableInstancing=true};
            m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",.12f);m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.4f);m.EnableKeyword("_NORMALMAP");AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
        private static Dictionary<Collider,string> Snapshot(GameObject root)=>root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>c.GetType().FullName+"|"+c.transform.localToWorldMatrix.ToString("R")+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+(c is BoxCollider b?"|"+b.center.ToString("R")+"|"+b.size.ToString("R"):c is CharacterController a?"|"+a.height+"|"+a.radius+"|"+a.center.ToString("R"):""));
        private static void WriteNew(string path,byte[] bytes){using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))file.Write(bytes,0,bytes.Length);}
        private static Texture2D Capture(Report report)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Real graphics required for original prop comparison.");
            var sheet=new Texture2D(720,810,TextureFormat.RGB24,false);var camera=new GameObject("Temporary prop comparison camera").AddComponent<Camera>();camera.fieldOfView=64;camera.nearClipPlane=.06f;camera.farClipPlane=180;camera.allowHDR=false;
            var positions=new[]{new Vector3(-7,1.7f,-12),new Vector3(-9,1.5f,-6)};var targets=new[]{new Vector3(2,1.6f,3),new Vector3(-6,.55f,-3)};
            var old=RenderTexture.active;var target=new RenderTexture(720,405,24,RenderTextureFormat.ARGB32);
            try{target.Create();camera.targetTexture=target;for(var view=0;view<2;view++) {
                camera.transform.position=positions[view];camera.transform.LookAt(targets[view]);camera.Render();RenderTexture.active=target;var frame=new Texture2D(720,405,TextureFormat.RGB24,false);
                try{frame.ReadPixels(new Rect(0,0,720,405),0,0);frame.Apply();var pixels=frame.GetPixels32();float min=1,max=0;
                    foreach(var pixel in pixels){if(pixel.r>170&&pixel.b>170&&pixel.g<70)report.magentaPixels++;var luminance=(pixel.r*.2126f+pixel.g*.7152f+pixel.b*.0722f)/255;min=Mathf.Min(min,luminance);max=Mathf.Max(max,luminance);}
                    report.minimumLuminanceRange=Mathf.Min(report.minimumLuminanceRange,max-min);sheet.SetPixels32(0,(1-view)*405,720,405,pixels);
                }finally{UnityEngine.Object.DestroyImmediate(frame);}}
                sheet.Apply();return sheet;
            }catch{UnityEngine.Object.DestroyImmediate(sheet);throw;}finally{camera.targetTexture=null;RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
""";
}
