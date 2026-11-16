namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] CourtyardArtifacts =
    ["YardAsphalt.png","YardAsphaltNormal.png","YardConcrete.png","YardConcreteNormal.png","YardMetal.png","YardMetalNormal.png",
     "YardAsphalt.mat","YardConcrete.mat","YardBlueSteel.mat","YardRustSteel.mat","YardDarkSteel.mat","YardYellow.mat","YardGlass.mat","YardLamp.mat","YardWet.mat","YardSky.mat",
     "CourtyardDressing.asset","courtyard-report.json"];
    private const string CourtyardBuilderSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ForgeCoreGameTemplates.Editor
{
    // A bounded, original code-authored presentation recipe. Not approved Blender/vault placement.
    public static class ForgeCoreCourtyardPresentation
    {
        public const string Profile="industrial_courtyard_presentation_v1";
        [Serializable]private sealed class Report {
            public int schemaVersion=1,textureSize=512,ownedTextureCount=6,decorativeColliderCount,decorativeTriangles,renderers,lightCount;
            public string profile=Profile,source="original_deterministic_template_geometry_and_surfaces";
            public bool collisionUnchanged,approvedLibraryAssetsPlaced=false,visualReviewed=false,rightsReviewed=false,stageComplete=false;
            public long ownedTextureMemoryBytes;
        }
        private sealed class Palette {
            public Transform root;public Mesh bevel;public Material asphalt,concrete,blue,rust,steel,yellow,glass,lamp,wet;
        }
        public static void Apply(GameObject root,string folder)
        {
            var before=CollisionSnapshot(root);var p=new Palette();
            p.root=new GameObject("Courtyard presentation — original template, not reviewed library art").transform;p.root.SetParent(root.transform,false);
            p.bevel=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/PresentationBevel.asset");
            var asphalt=Texture(folder,"YardAsphalt.png",false,(x,y)=> {
                var noise=Mathf.PerlinNoise(x*.17f+31,y*.17f+19);var grit=Mathf.PerlinNoise(x*1.7f,y*1.7f);
                var seam=Mathf.Abs(Mathf.Sin(x*.023f+Mathf.PerlinNoise(x*.02f,y*.02f)*1.8f))<.013f?.35f:1;
                return new Color(.22f,.245f,.25f)*((.65f+noise*.3f+grit*.18f)*seam);});
            var asphaltNormal=Texture(folder,"YardAsphaltNormal.png",true,(x,y)=>Normal(x,y,1.2f,.12f));
            var concrete=Texture(folder,"YardConcrete.png",false,(x,y)=> {
                var n=Mathf.PerlinNoise(x*.035f+13,y*.035f+39);var g=Mathf.PerlinNoise(x*.8f,y*.8f);
                var stain=Mathf.PerlinNoise(x*.012f+25,y*.12f)*.25f;
                return new Color(.58f,.56f,.50f)*(.66f+n*.25f+g*.15f-stain);});
            var concreteNormal=Texture(folder,"YardConcreteNormal.png",true,(x,y)=>Normal(x,y,.8f,.09f));
            var metal=Texture(folder,"YardMetal.png",false,(x,y)=> {
                var edge=Mathf.Min(Mathf.Min(x,511-x),Mathf.Min(y,511-y));
                var chip=edge<5&&Mathf.PerlinNoise(x*.23f+5,y*.23f+12)>.54f?.38f:0;
                var c=Color.Lerp(new Color(.84f,.86f,.86f),new Color(.39f,.24f,.13f),chip);
                return c*(.91f+Mathf.PerlinNoise(x*.65f,y*.65f)*.09f);});
            var metalNormal=Texture(folder,"YardMetalNormal.png",true,(x,y)=>Normal(x,y,.6f,.06f));
            p.asphalt=Finish(folder,"YardAsphalt",Color.white,0,.29f,asphalt,asphaltNormal);p.asphalt.mainTextureScale=new Vector2(9,9);
            p.concrete=Finish(folder,"YardConcrete",Color.white,0,.16f,concrete,concreteNormal);p.concrete.mainTextureScale=new Vector2(6,3);
            p.blue=Finish(folder,"YardBlueSteel",new Color(.23f,.37f,.43f),.45f,.35f,metal,metalNormal);
            p.rust=Finish(folder,"YardRustSteel",new Color(.55f,.27f,.15f),.35f,.26f,metal,metalNormal);
            p.steel=Finish(folder,"YardDarkSteel",new Color(.17f,.20f,.21f),.65f,.4f,metal,metalNormal);
            p.yellow=Finish(folder,"YardYellow",new Color(.94f,.58f,.10f),.12f,.3f,metal,metalNormal);
            p.glass=Finish(folder,"YardGlass",new Color(.085f,.19f,.23f),.55f,.86f,null,null);
            p.lamp=Finish(folder,"YardLamp",new Color(1,.75f,.4f),0,.5f,null,null);
            p.lamp.EnableKeyword("_EMISSION");p.lamp.SetColor("_EmissionColor",new Color(1,.65f,.28f)*2.2f);p.lamp.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
            p.wet=Finish(folder,"YardWet",new Color(.16f,.18f,.18f),.08f,.78f,null,null);
            root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Lab ground").GetComponent<Renderer>().sharedMaterial=p.asphalt;
            root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Lab ground").gameObject.AddComponent<DemoAudioSurface>().surface=ForgeCore.Demo.DemoFootstepSurface.Asphalt;
            foreach(var t in root.GetComponentsInChildren<Transform>()) {
                if(t.name=="Solid side boundary"||t.name=="Solid end boundary"||t.name=="Service-yard divider")t.GetComponent<Renderer>().sharedMaterial=p.concrete;
                if(!t.name.StartsWith("GREYBOX",StringComparison.Ordinal))continue;
                var mat=t.name.Contains("cargo")?p.blue:t.name.Contains("crate")?p.rust:t.name.Contains("fence")?p.steel:p.concrete;
                t.GetComponent<Renderer>().sharedMaterial=mat;
                // Shaped render meshes are generated separately; these objects retain collision only.
            }
            // All architecture is outside the existing 32m navigation/collision envelope.
            Building(p,new Vector3(0,0,19),new Vector3(33,8,3),"CONTROL / 01");
            Building(p,new Vector3(-19,0,0),new Vector3(3,7,34),"SERVICE YARD");
            Building(p,new Vector3(19,0,0),new Vector3(3,6,34),"LOGISTICS / 02");
            // Visible upper office: fixed backdrop, not a falsely advertised accessible second floor.
            Box(p,"Control office concrete shell",new Vector3(2,5.3f,17.7f),new Vector3(11,3.6f,1),p.concrete);
            for(var i=0;i<6;i++) {
                var x=-2.5f+i*1.8f;
                Box(p,"Reflective office window",new Vector3(x,5.45f,17.18f),new Vector3(1.65f,2.35f,.04f),p.glass);
                Box(p,"Office window mullion",new Vector3(x-.86f,5.45f,17.10f),new Vector3(.07f,2.5f,.09f),p.steel);
                Box(p,"Office warm ceiling panel",new Vector3(x,6.45f,17.12f),new Vector3(.85f,.07f,.1f),p.lamp);
            }
            Box(p,"Office sill",new Vector3(2,4.16f,17.02f),new Vector3(11.5f,.12f,.45f),p.steel);
            for(var i=0;i<12;i++) {
                var x=-3.4f+i;
                Box(p,"Outside-envelope handrail post",new Vector3(x,3.92f,16.96f),new Vector3(.045f,.9f,.045f),p.yellow);
            }
            Box(p,"Outside-envelope handrail",new Vector3(2,4.35f,16.96f),new Vector3(11.1f,.045f,.045f),p.yellow);
            foreach(var z in new[]{-9f,9f}) {
                foreach(var x in new[]{-17.7f,17.7f})Box(p,"Outside-envelope crane upright",new Vector3(x,5,z),new Vector3(.35f,10,.5f),p.yellow);
                Box(p,"Outside-envelope crane crosshead",new Vector3(0,9.6f,z),new Vector3(36,.65f,.45f),p.yellow);
                Box(p,"Crane dark running rail",new Vector3(0,9.98f,z),new Vector3(36,.07f,.12f),p.steel);
                Box(p,"Crane trolley",new Vector3(-8,9.22f,z),new Vector3(1.1f,.35f,.75f),p.steel);
                Box(p,"Crane hanging cable",new Vector3(-8,7.8f,z),new Vector3(.025f,2.6f,.025f),p.steel);
                Box(p,"Crane hook block",new Vector3(-8,6.45f,z),new Vector3(.26f,.36f,.20f),p.yellow);
            }
            var power=root.GetComponent<IndustrialMission>().powerBox;
            if(power==null)throw new InvalidOperationException("Physical mission power box required.");
            var powerBottom=new Vector3(-10,0,-9.5f);
            Box(p,"Power box concrete plinth",powerBottom+Vector3.up*.15f,new Vector3(.8f,.3f,.6f),p.concrete,power.transform);
            Box(p,"Power box cabinet",powerBottom+Vector3.up*.83f,new Vector3(.62f,1.2f,.35f),p.steel,power.transform);
            Box(p,"Power box door",powerBottom+new Vector3(0,.86f,-.183f),new Vector3(.53f,.98f,.025f),p.blue,power.transform);
            Box(p,"Power box handle",powerBottom+new Vector3(.18f,.80f,-.21f),new Vector3(.035f,.17f,.04f),p.steel,power.transform);
            power.statusLamp=Box(p,"Power box status lamp",powerBottom+new Vector3(.17f,1.28f,-.205f),new Vector3(.05f,.05f,.03f),p.lamp,power.transform).GetComponent<Renderer>();
            power.standbyMaterial=p.lamp;
            for(var row=0;row<4;row++)Box(p,"Power box louvre",powerBottom+new Vector3(0,.5f+row*.045f,-.205f),new Vector3(.38f,.018f,.025f),p.steel,power.transform);
            var plate=new GameObject("Cabinet-mounted power status plate").AddComponent<TextMesh>();plate.transform.SetParent(power.transform,false);
            plate.transform.position=powerBottom+new Vector3(0,1.10f,-.21f);plate.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            plate.fontSize=48;plate.characterSize=.012f;plate.anchor=TextAnchor.MiddleCenter;plate.color=new Color(.95f,.85f,.5f);
            plate.GetComponent<Renderer>().sharedMaterial=plate.font.material;plate.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            power.statusPlate=plate;power.SetPowered(false);
            foreach(var point in new[]{new Vector3(-10,0,9.5f),new Vector3(10,0,13.5f)}) {
                var terminal=point.x<0?root.GetComponent<IndustrialMission>().manifestTerminal:root.GetComponent<IndustrialMission>().extractionTerminal;
                if(terminal==null)throw new InvalidOperationException("Physical objective terminal required.");var parent=terminal.transform;
                Box(p,"Mission terminal plinth",point+Vector3.up*.15f,new Vector3(.8f,.3f,.6f),p.concrete,parent);
                Box(p,"Mission terminal housing",point+Vector3.up*.83f,new Vector3(.62f,1.2f,.35f),p.steel,parent);
                Box(p,"Mission terminal panel",point+new Vector3(0,.85f,-.18f),new Vector3(.45f,.35f,.02f),p.glass,parent);
                terminal.statusLamp=Box(p,"Mission terminal indicator",point+new Vector3(.16f,1.28f,-.20f),new Vector3(.05f,.05f,.025f),p.lamp,parent).GetComponent<Renderer>();
                terminal.lockedMaterial=p.lamp;terminal.readyMaterial=terminal.usedMaterial;
                var text=new GameObject("Mounted terminal state").AddComponent<TextMesh>();text.transform.SetParent(parent,false);text.transform.position=point+new Vector3(0,1.10f,-.21f);
                text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=48;text.characterSize=.0095f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.95f,.85f,.5f);
                text.GetComponent<Renderer>().sharedMaterial=text.font.material;text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;terminal.statusPlate=text;terminal.SetState(false,false);
                for(var row=0;row<4;row++)Box(p,"Mission terminal vent",point+new Vector3(0,.5f+row*.045f,-.18f),new Vector3(.38f,.018f,.025f),p.blue,parent);
            }
            // Detail on existing divider, not new obstruction across the service route.
            Box(p,"Divider cap",new Vector3(-2,2.42f,2),new Vector3(12,.07f,.8f),p.steel);
            for(var i=0;i<7;i++)Box(p,"Divider vertical seam",new Vector3(-7.85f+i*1.94f,1.2f,1.61f),new Vector3(.025f,2.3f,.014f),p.steel);
            foreach(var z in new[]{1.595f,2.405f})Box(p,"Divider hazard band",new Vector3(-2,.85f,z),new Vector3(11.8f,.12f,.016f),p.yellow);
            for(var i=0;i<9;i++) {
                var z=-14+i*3.2f;
                Flat(p,"Worn yard lane paint",new Vector3(-11.7f,.006f,z),new Vector3(.1f,.004f,2.1f),p.yellow);
                Flat(p,"Yard parking line",new Vector3(12.6f,.006f,z),new Vector3(3,.004f,.09f),p.yellow);
            }
            // Small irregular wet patches, opaque PBR/cubemap approximations; no planar-reflection claim.
            for(var i=0;i<14;i++) {
                var x=-12+(i*7%25);var z=-12+(i*11%26);
                Patch(p,new Vector3(x,.009f,z),.5f+(i%4)*.25f,.25f+(i%3)*.15f,i);
            }
            for(var i=0;i<28;i++)Flat(p,"Drain grate slat",new Vector3(-9.2f+i*.11f,.007f,-7),new Vector3(.05f,.008f,1.2f),p.steel);
            foreach(var point in new[]{new Vector3(-15.9f,5,-11),new Vector3(15.9f,5,-4),new Vector3(-15.9f,5,9),new Vector3(15.9f,5,12)}) {
                Box(p,"Work-light pole",new Vector3(point.x,2.5f,point.z),new Vector3(.10f,5,.10f),p.steel);
                Box(p,"Work-light housing",point,new Vector3(.55f,.17f,.3f),p.steel);
                Box(p,"Warm work-light lens",point-Vector3.up*.1f,new Vector3(.45f,.025f,.23f),p.lamp);
                var light=new GameObject("Bounded warm work-light").AddComponent<Light>();light.transform.SetParent(p.root,false);light.transform.position=point-Vector3.up*.25f;
                light.type=LightType.Point;light.color=new Color(1,.72f,.40f);light.range=10;light.intensity=2.4f;light.shadows=LightShadows.None;
            }
            Sign(p,"SERVICE YARD / A1",new Vector3(-10,2,16.20f),Vector3.zero,.16f);
            Sign(p,"CONTROL / 01",new Vector3(2,7.4f,17.1f),Vector3.zero,.17f);
            Lighting(root,folder);
            Combine(p,folder);
            ForgeCoreCourtyardProps.Apply(root,folder);
            var after=CollisionSnapshot(root);
            var report=new Report{collisionUnchanged=before.Count==after.Count&&before.All(pair=>after.TryGetValue(pair.Key,out var state)&&state==pair.Value),decorativeColliderCount=p.root.GetComponentsInChildren<Collider>().Length,
                decorativeTriangles=p.root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),renderers=p.root.GetComponentsInChildren<Renderer>().Length,
                lightCount=p.root.GetComponentsInChildren<Light>().Length,ownedTextureMemoryBytes=new[]{asphalt,asphaltNormal,concrete,concreteNormal,metal,metalNormal}.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t))};
            if(!report.collisionUnchanged||report.decorativeColliderCount!=0||report.decorativeTriangles>100000||report.renderers>40||report.ownedTextureMemoryBytes>32*1024*1024)
                throw new InvalidOperationException("Courtyard presentation exceeded bounded collision/resource contract.");
            File.WriteAllText(folder+"/courtyard-report.json",JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
        }
        private static Dictionary<Collider,string> CollisionSnapshot(GameObject root)=>root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>
            c.GetType().FullName+"|"+c.transform.position.ToString("R")+"|"+c.transform.rotation.ToString("R")+"|"+c.transform.lossyScale.ToString("R")+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+
            (c is BoxCollider box?"|"+box.center.ToString("R")+"|"+box.size.ToString("R"):c is CharacterController actor?"|"+actor.height+"|"+actor.radius+"|"+actor.center.ToString("R"):""));
        private static Color Normal(int x,int y,float frequency,float strength) {
            float H(float a,float b)=>Mathf.PerlinNoise(a*frequency+37,b*frequency+11);
            var n=new Vector3((H(x-1,y)-H(x+1,y))*strength,(H(x,y-1)-H(x,y+1))*strength,1).normalized;
            return new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
        }
        private static Texture2D Texture(string folder,string name,bool normal,Func<int,int,Color> pixel) {
            const int size=512;var image=new Texture2D(size,size,TextureFormat.RGBA32,false,normal);var colors=new Color[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++){var c=pixel(x,y);c.a=1;colors[y*size+x]=c;}
            image.SetPixels(colors);image.Apply();var path=folder+"/"+name;File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.mipmapEnabled=true;
            importer.maxTextureSize=size;importer.isReadable=false;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=4;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material Finish(string folder,string name,Color color,float metallic,float smoothness,Texture albedo,Texture normal) {
            var shader=Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Built-in PBR shader required.");
            var m=new Material(shader){name=name,color=color,mainTexture=albedo};m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smoothness);
            if(normal!=null){m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.5f);}
            AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;
        }
        private static GameObject Box(Palette p,string name,Vector3 position,Vector3 size,Material material,Transform parent=null) {
            var go=new GameObject(name);go.transform.SetParent(parent!=null?parent:p.root,false);
            go.transform.localPosition=parent!=null?parent.InverseTransformPoint(position):position;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=p.bevel;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static void Flat(Palette p,string name,Vector3 position,Vector3 size,Material material) {
            var r=Box(p,name,position,size,material).GetComponent<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static void Building(Palette p,Vector3 bottom,Vector3 size,string label) {
            Box(p,"Warehouse architectural backdrop",bottom+Vector3.up*size.y*.5f,size,p.blue);
            Box(p,"Warehouse roof coping",bottom+Vector3.up*(size.y+.05f),size+new Vector3(.2f,-size.y+.15f,.2f),p.steel);
            if(size.x>size.z)for(var i=0;i<18;i++) {
                var x=bottom.x-size.x*.48f+i*size.x*.96f/17;
                Box(p,"Warehouse vertical cladding seam",new Vector3(x,size.y*.5f,bottom.z-size.z*.5f-.02f),new Vector3(.05f,size.y,.08f),p.steel);
            }
            else for(var i=0;i<18;i++) {
                var z=bottom.z-size.z*.48f+i*size.z*.96f/17;var x=bottom.x+Mathf.Sign(-bottom.x)*(size.x*.5f+.025f);
                Box(p,"Warehouse side seam",new Vector3(x,size.y*.5f,z),new Vector3(.08f,size.y,.05f),p.steel);
                if(i%3==0)Box(p,"Warehouse high clerestory",new Vector3(x,size.y*.78f,z),new Vector3(.05f,.85f,1.5f),p.glass);
            }
        }
        private static void Patch(Palette p,Vector3 centre,float radiusX,float radiusZ,int seed) {
            const int sides=22;var v=new Vector3[sides+1];var uv=new Vector2[sides+1];var tri=new int[sides*3];v[0]=centre;uv[0]=new Vector2(.5f,.5f);
            for(var i=0;i<sides;i++) {
                var angle=i*2*Mathf.PI/sides;var r=.94f+.04f*Mathf.Sin(angle*3+seed)+.02f*Mathf.Sin(angle*5-seed);
                v[i+1]=centre+new Vector3(Mathf.Cos(angle)*radiusX*r,0,Mathf.Sin(angle)*radiusZ*r);uv[i+1]=new Vector2(.5f+Mathf.Cos(angle)*.5f,.5f+Mathf.Sin(angle)*.5f);
                tri[i*3]=0;tri[i*3+1]=(i+1)%sides+1;tri[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="Original irregular wet patch"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Wet asphalt approximation");go.transform.SetParent(p.root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=p.wet;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static void Sign(Palette p,string text,Vector3 position,Vector3 rotation,float size) {
            var sign=new GameObject(text).AddComponent<TextMesh>();sign.transform.SetParent(p.root,false);sign.transform.localPosition=position;sign.transform.localEulerAngles=rotation;
            sign.text=text;sign.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");sign.fontSize=64;sign.characterSize=size;
            sign.anchor=TextAnchor.MiddleCenter;sign.color=new Color(.95f,.78f,.38f);sign.GetComponent<Renderer>().sharedMaterial=sign.font.material;
            sign.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
        private static void Lighting(GameObject root,string folder) {
            var sky=new Material(Shader.Find("Skybox/Procedural")){name="Courtyard muted late-afternoon sky"};sky.SetFloat("_Exposure",.8f);
            sky.SetFloat("_AtmosphereThickness",1.3f);sky.SetColor("_SkyTint",new Color(.48f,.49f,.50f));sky.SetColor("_GroundColor",new Color(.23f,.25f,.27f));
            AssetDatabase.CreateAsset(sky,folder+"/YardSky.mat");RenderSettings.skybox=sky;
            RenderSettings.ambientSkyColor=new Color(.38f,.44f,.51f);RenderSettings.ambientEquatorColor=new Color(.27f,.31f,.35f);RenderSettings.ambientGroundColor=new Color(.15f,.17f,.18f);
            RenderSettings.fogColor=new Color(.41f,.47f,.53f);RenderSettings.fogDensity=.010f;RenderSettings.reflectionIntensity=.7f;
            var sun=RenderSettings.sun;if(sun!=null){sun.intensity=1.05f;sun.color=new Color(1,.88f,.70f);sun.transform.rotation=Quaternion.Euler(30,-38,0);}
            foreach(var light in root.GetComponentsInChildren<Light>())if(light.name.StartsWith("Cool work-light fill",StringComparison.Ordinal))light.enabled=false;
        }
        private static void Combine(Palette p,string folder) {
            // Group by material and shadow policy, preserving source asset meshes and TextMesh renderers.
            var filters=p.root.GetComponentsInChildren<MeshFilter>();var first=true;
            foreach(var group in filters.GroupBy(f=>(f.GetComponent<MeshRenderer>().sharedMaterial,f.GetComponent<MeshRenderer>().shadowCastingMode))) {
                var mesh=new Mesh{name="Courtyard batch "+group.Key.Item1.name+" "+group.Key.Item2};
                mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=p.root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                if(first){AssetDatabase.CreateAsset(mesh,folder+"/CourtyardDressing.asset");first=false;}else AssetDatabase.AddObjectToAsset(mesh,folder+"/CourtyardDressing.asset");
                var go=new GameObject(mesh.name);go.transform.SetParent(p.root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=group.Key.Item1;r.shadowCastingMode=group.Key.Item2;
            }
            var generated=filters.Select(f=>f.sharedMesh).Where(m=>!AssetDatabase.Contains(m)).Distinct().ToArray();
            foreach(var filter in filters)UnityEngine.Object.DestroyImmediate(filter.gameObject);
            foreach(var mesh in generated)UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
""";
}
