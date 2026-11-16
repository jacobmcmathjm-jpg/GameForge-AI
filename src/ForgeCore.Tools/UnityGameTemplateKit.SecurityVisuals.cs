namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] SecurityVisualArtifacts =
    ["SurfaceAlbedo.png","SurfaceNormal.png","ClothAlbedo.png","ClothNormal.png","MetalMask.png","SafetyAlbedo.png",
     "PresentationBevel.asset","Floor.mat","Wall.mat","Safety.mat","LightHousing.mat","Skybox.mat","SecurityReflection.cubemap",
     "GuardCloth.mat","GuardRubber.mat","GuardVisor.mat","GuardMetal.mat","GuardTrim.mat"];
    private const string SecurityVisualsSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ForgeCoreGameTemplates.Editor
{
    // Original deterministic local finishes/geometry. Not downloaded art, image-model output or a new Blender weapon.
    public sealed class SecurityVisualPalette
    {
        public const string Profile="industrial_range_finish_v1";
        public static readonly string[] Artifacts={"SurfaceAlbedo.png","SurfaceNormal.png","ClothAlbedo.png","ClothNormal.png","MetalMask.png","SafetyAlbedo.png",
            "PresentationBevel.asset","Floor.mat","Wall.mat","Safety.mat","LightHousing.mat","Skybox.mat","SecurityReflection.cubemap",
            "GuardCloth.mat","GuardRubber.mat","GuardVisor.mat","GuardMetal.mat","GuardTrim.mat"};
        public Material Metal,Polymer,Amber,Concrete,Floor,Wall,Safety,Housing,Cloth,Rubber,Visor,GuardMetal,GuardTrim,Sky;
        public Mesh Bevel;public Cubemap Reflection;
        public long OwnedTextureBytes;public const int TextureSize=256;
        public static SecurityVisualPalette Create(string folder)
        {
            var p=new SecurityVisualPalette();
            var surface=Texture(folder,"SurfaceAlbedo.png",false,false,(x,y)=> {
                var n=Mathf.PerlinNoise(x*.11f+17,y*.11f+31);var grain=Mathf.PerlinNoise(x*.85f,y*.85f);
                var scratch=(y%59==0&&x%83<38)?.15f:0;return Color.white*(.68f+n*.19f+grain*.08f+scratch);});
            var normal=Texture(folder,"SurfaceNormal.png",true,true,(x,y)=>new Color(.5f+.025f*Mathf.Sin(x*.5f),.5f+.025f*Mathf.Sin(y*.37f),1,1));
            var cloth=Texture(folder,"ClothAlbedo.png",false,false,(x,y)=> {
                var weave=((x%4<2)^(y%4<2))?.055f:0;return new Color(1,1,1,1)*(.80f+weave+Mathf.PerlinNoise(x*.16f,y*.16f)*.10f);});
            var clothNormal=Texture(folder,"ClothNormal.png",true,true,(x,y)=>new Color(.5f+.07f*Mathf.Sin(x*Mathf.PI/2),.5f+.07f*Mathf.Sin(y*Mathf.PI/2),1,1));
            var mask=Texture(folder,"MetalMask.png",true,false,(x,y)=>new Color(.72f,0,0,.43f+.15f*Mathf.PerlinNoise(x*.2f,y*.2f)));
            var safety=Texture(folder,"SafetyAlbedo.png",false,false,(x,y)=>(x+y)/32%2==0?new Color(.95f,.64f,.14f,1):new Color(.025f,.03f,.035f,1));
            p.Metal=Finish(folder,"RifleMetal.mat",new Color(.32f,.37f,.41f),.72f,.5f,surface,normal,mask);
            p.Polymer=Finish(folder,"RiflePolymer.mat",new Color(.10f,.12f,.13f),0,.25f,surface,normal);
            p.Amber=Finish(folder,"RifleTrim.mat",new Color(1,.45f,.055f),0,.4f,null,null);
            p.Amber.EnableKeyword("_EMISSION");p.Amber.SetColor("_EmissionColor",new Color(1,.25f,.025f)*1.3f);
            p.Amber.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None; // Clear default EmissiveIsBlack so Unity's Standard validator preserves the keyword.
            p.Concrete=Finish(folder,"Concrete.mat",new Color(.53f,.51f,.46f),0,.15f,surface,normal);p.Concrete.mainTextureScale=new Vector2(4,2);
            p.Floor=Finish(folder,"Floor.mat",new Color(.37f,.40f,.40f),0,.23f,surface,normal);p.Floor.mainTextureScale=new Vector2(17,17);
            p.Wall=Finish(folder,"Wall.mat",new Color(.23f,.32f,.40f),.2f,.3f,surface,normal);p.Wall.mainTextureScale=new Vector2(6,2);
            p.Safety=Finish(folder,"Safety.mat",Color.white,0,.2f,safety,null);p.Safety.mainTextureScale=new Vector2(8,1);
            p.Housing=Finish(folder,"LightHousing.mat",new Color(.12f,.16f,.19f),.55f,.45f,surface,normal,mask);
            p.Cloth=Finish(folder,"GuardCloth.mat",new Color(.35f,.45f,.37f),0,.15f,cloth,clothNormal);p.Cloth.SetFloat("_BumpScale",.5f);
            p.Rubber=Finish(folder,"GuardRubber.mat",new Color(.06f,.075f,.085f),0,.25f,surface,normal);
            p.Visor=Finish(folder,"GuardVisor.mat",new Color(.075f,.19f,.25f),.4f,.72f,null,null);
            p.GuardMetal=Finish(folder,"GuardMetal.mat",new Color(.24f,.29f,.33f),.72f,.46f,surface,normal,mask);
            p.GuardTrim=Finish(folder,"GuardTrim.mat",new Color(.95f,.49f,.10f),0,.25f,null,null);
            p.Bevel=BevelMesh();AssetDatabase.CreateAsset(p.Bevel,folder+"/PresentationBevel.asset");
            p.Reflection=SkyReflection();AssetDatabase.CreateAsset(p.Reflection,folder+"/SecurityReflection.cubemap");
            var shader=Shader.Find("Skybox/Procedural");if(shader==null)throw new InvalidOperationException("Built-in procedural sky shader required.");
            p.Sky=new Material(shader){name="Original industrial daylight sky"};p.Sky.SetFloat("_Exposure",1.15f);
            p.Sky.SetColor("_SkyTint",new Color(.47f,.57f,.68f));p.Sky.SetColor("_GroundColor",new Color(.25f,.28f,.30f));
            AssetDatabase.CreateAsset(p.Sky,folder+"/Skybox.mat");
            p.OwnedTextureBytes=new UnityEngine.Texture[]{surface,normal,cloth,clothNormal,mask,safety,p.Reflection}.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            foreach(var material in new[]{p.Metal,p.Polymer,p.Amber,p.Concrete,p.Floor,p.Wall,p.Safety,p.Housing,p.Cloth,p.Rubber,p.Visor,p.GuardMetal,p.GuardTrim,p.Sky})EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();return p;
        }
        private static Texture2D Texture(string folder,string name,bool linear,bool normal,Func<int,int,Color> pixel)
        {
            var t=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,false,linear);var pixels=new Color[TextureSize*TextureSize];
            for(var y=0;y<TextureSize;y++)for(var x=0;x<TextureSize;x++){var c=pixel(x,y);if(name!="MetalMask.png")c.a=1;pixels[y*TextureSize+x]=c;}
            t.SetPixels(pixels);t.Apply();var path=folder+"/"+name;File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!linear;importer.mipmapEnabled=true;importer.maxTextureSize=TextureSize;importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material Finish(string folder,string name,Color color,float metal,float smooth,Texture2D albedo,Texture2D normal,Texture2D mask=null)
        {
            var shader=Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Built-in Standard shader required.");
            var m=new Material(shader){name=Path.GetFileNameWithoutExtension(name),color=color,mainTexture=albedo,enableInstancing=true};
            m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",smooth);
            if(normal!=null){m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.3f);m.EnableKeyword("_NORMALMAP");}
            if(mask!=null){m.SetTexture("_MetallicGlossMap",mask);m.SetFloat("_GlossMapScale",.85f);m.EnableKeyword("_METALLICGLOSSMAP");}
            AssetDatabase.CreateAsset(m,folder+"/"+name);return m;
        }
        public void Character(GameObject visual)
        {
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var slots=skin.sharedMaterials.Select(m=> {
                    if(m==null)throw new InvalidOperationException("Missing original character material.");var n=m.name.ToLowerInvariant();
                    if(n.Contains("visor"))return Visor;if(n.Contains("amber")||n.Contains("trim"))return GuardTrim;
                    if(n.Contains("graphite")||n.Contains("equipment"))return GuardMetal;
                    if(n.Contains("boot")||n.Contains("glove")||n.Contains("seal")||n.Contains("rubber"))return Rubber;
                    if(n.Contains("woven")||n.Contains("cloth")||n.Contains("suit"))return Cloth;
                    return m; // Unknown sources keep their own finishes; never flatten arbitrary imported slots.
                }).ToArray();skin.sharedMaterials=slots;
            }
        }
        private static Mesh BevelMesh()
        {
            var vertices=new List<Vector3>();var indices=new List<int>();var uv=new List<Vector2>();const float h=.5f,a=.42f;
            void Face(params Vector3[] points) {
                var center=points.Aggregate(Vector3.zero,(x,y)=>x+y)/points.Length;
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)Array.Reverse(points);
                var first=vertices.Count;for(var i=0;i<points.Length;i++){vertices.Add(points[i]);uv.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0));}
                for(var i=1;i<points.Length-1;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
            }
            Vector3 V(int axis,float fixedValue,float x,float y){var p=Vector3.zero;p[axis]=fixedValue;p[(axis+1)%3]=x;p[(axis+2)%3]=y;return p;}
            for(var axis=0;axis<3;axis++)foreach(var s in new[]{-1,1})Face(V(axis,s*h,-a,-a),V(axis,s*h,a,-a),V(axis,s*h,a,a),V(axis,s*h,-a,a));
            for(var axis=0;axis<3;axis++)foreach(var s in new[]{-1,1})foreach(var t in new[]{-1,1}) {
                Vector3 E(float x,float y,float z){var p=Vector3.zero;p[axis]=x;p[(axis+1)%3]=y;p[(axis+2)%3]=z;return p;}
                Face(E(-a,s*h,t*a),E(a,s*h,t*a),E(a,s*a,t*h),E(-a,s*a,t*h));
            }
            foreach(var x in new[]{-1,1})foreach(var y in new[]{-1,1})foreach(var z in new[]{-1,1})Face(new Vector3(x*h,y*a,z*a),new Vector3(x*a,y*h,z*a),new Vector3(x*a,y*a,z*h));
            var mesh=new Mesh{name="Original 44-triangle chamfered presentation box"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.SetUVs(0,uv);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        private static Cubemap SkyReflection()
        {
            const int size=64;var cube=new Cubemap(size,TextureFormat.RGBAHalf,true){name="Original synthetic daylight reflection",filterMode=FilterMode.Trilinear};
            for(var face=0;face<6;face++) {
                var pixels=new Color[size*size];for(var y=0;y<size;y++)for(var x=0;x<size;x++) {
                    var u=(x+.5f)/size*2-1;var v=(y+.5f)/size*2-1;
                    var dir=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);
                    dir.Normalize();var c=Color.Lerp(new Color(.18f,.20f,.22f),new Color(.62f,.73f,.86f),Mathf.Clamp01(dir.y*.5f+.5f));
                    c+=new Color(1,.88f,.70f)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(dir,new Vector3(.4f,.7f,-.4f).normalized)),48)*1.4f;c.a=1;pixels[y*size+x]=c;
                }cube.SetPixels(pixels,(CubemapFace)face);
            }cube.Apply(true,false);return cube;
        }
        public GameObject Decor(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=Bevel;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        public void Stage(Camera camera)
        {
            camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.farClipPlane=180;
            RenderSettings.skybox=Sky;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientIntensity=1;
            RenderSettings.ambientSkyColor=new Color(.48f,.57f,.69f);RenderSettings.ambientEquatorColor=new Color(.31f,.34f,.37f);RenderSettings.ambientGroundColor=new Color(.16f,.18f,.19f);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=Reflection;RenderSettings.reflectionIntensity=.85f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.53f,.62f,.70f);RenderSettings.fogDensity=.007f;
            var light=new GameObject("Warm industrial daylight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;
            light.color=new Color(1,.90f,.78f);light.shadows=LightShadows.Soft;light.shadowBias=.02f;light.shadowNormalBias=.1f;light.transform.rotation=Quaternion.Euler(42,-32,0);RenderSettings.sun=light;
            foreach(var x in new[]{-7f,7f}) {var fill=new GameObject("Cool work-light fill "+x).AddComponent<Light>();fill.type=LightType.Point;fill.transform.position=new Vector3(x,3,-10);fill.color=new Color(.61f,.78f,1);fill.intensity=1.6f;fill.range=13;fill.shadows=LightShadows.None;}
            var dressing=new GameObject("Presentation dressing — no gameplay colliders").transform;
            for(var i=0;i<6;i++)Decor(dressing,"Lane paint "+i,new Vector3(-2,.006f,-13+i*2),new Vector3(.08f,.012f,1.2f),GuardTrim).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            foreach(var z in new[]{-1f,5f}) {
                var x=z<0?-3f:5f;var width=z<0?10f:8f;
                Decor(dressing,"Cover hazard band "+z,new Vector3(x,1.15f,z-.607f),new Vector3(width-.15f,.13f,.015f),Safety);
                Decor(dressing,"Cover top cap "+z,new Vector3(x,2.11f,z),new Vector3(width+.08f,.05f,1.24f),Housing);
                for(var i=0;i<5;i++)Decor(dressing,"Cover steel seam "+z+"-"+i,new Vector3(x-width*.4f+i*width*.2f,1.05f,z-.61f),new Vector3(.025f,2.1f,.025f),Housing);
            }
            // Architectural silhouettes are outside the collision-bounded playable envelope.
            Decor(dressing,"Industrial back facade",new Vector3(0,4.5f,19),new Vector3(33,9,2),Wall);
            for(var i=0;i<12;i++){var x=-15+i*2.7f;Decor(dressing,"Facade rib "+i,new Vector3(x,4.5f,17.97f),new Vector3(.08f,9,.07f),Housing);
                Decor(dressing,"Facade upper light panel "+i,new Vector3(x,6.6f,17.9f),new Vector3(1.6f,.8f,.08f),Visor);}
            foreach(var x in new[]{-20f,20f})Decor(dressing,"Outer utility block "+x,new Vector3(x,3,4),new Vector3(4,6,20),Wall);
        }
    }
}
""";
}
