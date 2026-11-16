namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] VisualPolishArtifacts =
    ["HeroSurfaceAlbedo.png","HeroSurfaceNormal.png","HeroSurfaceMask.png","HeroClothAlbedo.png","HeroClothNormal.png","HeroClothMask.png",
     "HeroMetal.mat","HeroPolymer.mat","HeroCloth.mat","HeroGlove.mat","VisualPolishMeshes.asset","CourtyardReflection.exr","visual-polish-report.json"];
    private const string VisualPolishSource = """
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Original bounded presentation revision. No source replacement, new motion clips or human art acceptance.
    public static class ForgeCoreVisualPolish
    {
        public const string Profile="industrial_hero_finish_v1";
        [Serializable] private sealed class Report {
            public int schemaVersion=1,weaponCount,guardCount,guardAttachments,addedRenderers,addedTriangles,addedColliders,textureCount=6,textureSize=1024,reflectionResolution=128;
            public string profile=Profile,source="original_template_geometry_and_procedural_pbr",lighting="one_prebaked_custom_box_projected_probe_no_added_realtime_lights";
            public bool collisionUnchanged,socketsUnchanged,opaqueWetOverlayDisabled,animationsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,stageComplete=false,approvedLibraryAssetsPlaced=false;
            public long ownedTextureMemoryBytes;
        }
        private sealed class Maps {public Texture2D albedo,normal,mask;}
        private static Mesh bevel;private static bool first;private static string currentFolder;
        public static void Apply(GameObject root,string folder) {
            if(GraphicsSettings.currentRenderPipeline!=null||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Hero finish requires graphics-enabled built-in Standard.");
            currentFolder=folder;first=true;bevel=Bevel();Store(bevel);var report=new Report();var before=Snapshot(root);
            var session=root.GetComponentInChildren<SecurityCombatSession>();if(session==null||session.enemies.Length!=3)throw new InvalidOperationException("Exact existing mission session required.");
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>t.localPosition.ToString("R")+"|"+t.localRotation.ToString("R"));
            var surface=Generate(folder,"Surface",false);var cloth=Generate(folder,"Cloth",true);report.ownedTextureMemoryBytes=new[]{surface,cloth}.Sum(m=>new[]{m.albedo,m.normal,m.mask}.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)));
            var metal=Finish(folder,"HeroMetal",new Color(.19f,.215f,.23f),surface,.64f,true);
            var polymer=Finish(folder,"HeroPolymer",new Color(.075f,.085f,.078f),surface,.22f,false);
            var fabric=Finish(folder,"HeroCloth",new Color(.30f,.355f,.255f),cloth,.16f,false);
            var glove=Finish(folder,"HeroGlove",new Color(.075f,.082f,.067f),cloth,.2f,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m==null?m:m.name=="RifleMetal"?metal:m.name=="RiflePolymer"?polymer:m.name=="GuardCloth"?fabric:m.name=="GuardRubber"?glove:m).ToArray();
            var weaponRoots=new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon)).ToArray();
            foreach(var weapon in weaponRoots){DressWeapon(weapon,metal,polymer,report);report.weaponCount++;}
            foreach(var enemy in session.enemies){DressGuard(enemy.motion.transform,fabric,polymer,report);report.guardCount++;}
            // Old opaque ellipse overlays do not represent water depth or reflection; use the asphalt's spatial smoothness mask instead.
            var wet=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.name=="YardWet")).ToArray();
            if(wet.Length!=1)throw new InvalidOperationException("Expected the single original wet overlay batch.");foreach(var r in wet)r.enabled=false;report.opaqueWetOverlayDisabled=true;
            var lighting=new GameObject("Courtyard baked reflection — no runtime updates").AddComponent<ReflectionProbe>();lighting.transform.SetParent(root.transform,false);lighting.transform.localPosition=new Vector3(0,2.5f,0);
            lighting.mode=ReflectionProbeMode.Baked;lighting.refreshMode=ReflectionProbeRefreshMode.ViaScripting;lighting.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            lighting.resolution=128;lighting.size=new Vector3(36,12,36);lighting.center=Vector3.zero;lighting.boxProjection=true;lighting.intensity=.65f;lighting.blendDistance=3;lighting.cullingMask=~(1<<9);lighting.nearClipPlane=.1f;lighting.farClipPlane=110;lighting.hdr=true;
            // Distinct cool exterior / warm practicals with bounded shadow distance; no realtime GI or extra shadow lights.
            if(RenderSettings.sun!=null){RenderSettings.sun.transform.rotation=Quaternion.Euler(24,-42,0);RenderSettings.sun.color=new Color(.93f,.96f,1);RenderSettings.sun.intensity=.95f;RenderSettings.sun.shadowStrength=.78f;}
            RenderSettings.ambientSkyColor=new Color(.34f,.41f,.51f);RenderSettings.ambientEquatorColor=new Color(.20f,.24f,.28f);RenderSettings.ambientGroundColor=new Color(.12f,.135f,.15f);
            RenderSettings.fogColor=new Color(.39f,.46f,.53f);RenderSettings.fogDensity=.009f;
            var reflectionPath=folder+"/CourtyardReflection.exr";if(File.Exists(reflectionPath))throw new IOException("Reflection destination occupied; no overwrite.");
            if(!Lightmapping.BakeReflectionProbe(lighting,reflectionPath))throw new InvalidOperationException("Actual scene reflection bake failed.");
            AssetDatabase.ImportAsset(reflectionPath,ImportAssetOptions.ForceSynchronousImport);
            var reflectionImporter=(TextureImporter)AssetImporter.GetAtPath(reflectionPath);reflectionImporter.sRGBTexture=false;reflectionImporter.mipmapEnabled=true;reflectionImporter.isReadable=false;
            reflectionImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=128,format=TextureImporterFormat.BC6H,compressionQuality=75});reflectionImporter.SaveAndReimport();
            var reflection=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            if(reflection==null||reflection.width!=128)throw new InvalidOperationException("Missing actual scene cubemap.");
            // Scene-owned bakedTexture references do not survive prefab instantiation. Persist the independently baked asset explicitly.
            lighting.customBakedTexture=reflection;lighting.mode=ReflectionProbeMode.Custom;
            var after=Snapshot(root);report.collisionUnchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var state)&&state==p.Value);
            report.socketsUnchanged=sockets.All(p=>p.Key.localPosition.ToString("R")+"|"+p.Key.localRotation.ToString("R")==p.Value);report.addedColliders=after.Count-before.Count;
            WriteNew(folder+"/visual-polish-report.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
            if(!report.collisionUnchanged||!report.socketsUnchanged||report.addedColliders!=0||report.weaponCount!=4||report.guardCount!=3||report.guardAttachments!=36||report.addedRenderers>48||report.addedTriangles>18000||report.ownedTextureMemoryBytes>20*1024*1024)
                throw new InvalidOperationException("Hero finish identity/collision/socket/resource gate failed: "+JsonUtility.ToJson(report));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
        }
        private static void DressWeapon(Transform weapon,Material metal,Material polymer,Report report) {
            Transform Named(string name)=>weapon.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
            foreach(var name in new[]{"Stock","Hand guard"})Named(name).GetComponent<MeshRenderer>().enabled=false;
            foreach(var t in weapon.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Foregrip rib ",StringComparison.Ordinal))t.GetComponent<MeshRenderer>().enabled=false;
            var receiver=Named("Receiver");receiver.localScale=new Vector3(.061f,.068f,.27f);receiver.localPosition=new Vector3(0,.012f,0);receiver.GetComponent<MeshFilter>().sharedMesh=bevel;
            var grip=Named("Grip");grip.localScale=new Vector3(.047f,.105f,.052f);grip.GetComponent<MeshFilter>().sharedMesh=bevel;
            var parts=new GameObject("Original hero weapon details").transform;parts.SetParent(weapon,false);parts.gameObject.layer=weapon.gameObject.layer;
            void Box(string name,Vector3 at,Vector3 size,Material material,Vector3 angles=default)=>Part(parts,name,at,size,material,angles);
            Box("Lower receiver contour",new Vector3(0,-.028f,-.02f),new Vector3(.052f,.04f,.155f),metal);
            foreach(var side in new[]{-1,1}) {
                Box("Skeleton stock rail",new Vector3(side*.019f,-.012f,-.22f),new Vector3(.015f,.021f,.17f),polymer);
                Box("Stock diagonal strut",new Vector3(side*.019f,-.047f,-.22f),new Vector3(.014f,.018f,.16f),polymer,new Vector3(-20,0,0));
                for(var i=0;i<6;i++)Box("Recessed cooling slot",new Vector3(side*.0395f,.002f,.065f+i*.027f),new Vector3(.0015f,.016f,.017f),polymer);
                for(var i=0;i<6;i++)Box("Receiver grip serration",new Vector3(side*.031f,.012f,-.10f+i*.012f),new Vector3(.002f,.034f,.003f),polymer);
                Box("Receiver pin",new Vector3(side*.032f,-.008f,-.07f),new Vector3(.003f,.009f,.009f),polymer);
                Box("Selector lever",new Vector3(side*.033f,-.017f,-.09f),new Vector3(.004f,.006f,.025f),polymer,new Vector3(0,0,-20));
            }
            // Octagonal shroud, rather than stacked square foregrip ribs. The muzzle and all action sockets stay unchanged.
            var tube=Octagon();Store(tube);var shroud=new GameObject("Octagonal ventilated handguard");shroud.transform.SetParent(parts,false);shroud.AddComponent<MeshFilter>().sharedMesh=tube;shroud.AddComponent<MeshRenderer>().sharedMaterial=metal;
            Box("Upper accessory rail",new Vector3(0,.049f,.135f),new Vector3(.045f,.011f,.32f),metal);
            for(var i=0;i<17;i++)Box("Rail tooth",new Vector3(0,.057f,-.01f+i*.0175f),new Vector3(.052f,.006f,.008f),polymer);
            Box("Charging latch",new Vector3(0,.045f,-.145f),new Vector3(.078f,.012f,.017f),polymer);
            Box("Magazine well",new Vector3(0,-.048f,.016f),new Vector3(.059f,.038f,.09f),metal);
            foreach(var z in new[]{-.035f,.012f})Box("Optic mounting bolt",new Vector3(.027f,.064f,z),new Vector3(.01f,.009f,.01f),metal);
            Combine(parts,report);foreach(var r in weapon.GetComponentsInChildren<MeshRenderer>(true))r.gameObject.layer=weapon.gameObject.layer;
        }
        private static void DressGuard(Transform visual,Material cloth,Material polymer,Report report) {
            var skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Transform Attach(string boneName,Vector3 restPoint) {
                var index=Array.FindIndex(skin.bones,b=>b.name==boneName);if(index<0)throw new InvalidOperationException("Original rig bone missing.");
                var anchor=new GameObject("Original finish binding "+boneName+" "+report.guardAttachments).transform;anchor.SetParent(skin.bones[index],false);
                var local=skin.transform.InverseTransformPoint(visual.TransformPoint(restPoint));anchor.localPosition=skin.sharedMesh.bindposes[index].MultiplyPoint3x4(local);
                anchor.localRotation=skin.sharedMesh.bindposes[index].rotation*Quaternion.Inverse(skin.transform.rotation)*visual.rotation;return anchor;
            }
            void Detail(string bone,string name,Vector3 point,Vector3 size,Material material) {
                var anchor=Attach(bone,point);Part(anchor,name+" "+report.guardAttachments,Vector3.zero,size,material);report.guardAttachments++;report.addedRenderers++;report.addedTriangles+=bevel.triangles.Length/3;
            }
            foreach(var side in new[]{-1,1}) {
                Detail("Chest","Vest vertical webbing",new Vector3(side*.115f,1.31f,.165f),new Vector3(.018f,.25f,.008f),polymer);
                Detail("Chest","Vest seam highlight",new Vector3(side*.137f,1.31f,.164f),new Vector3(.003f,.25f,.006f),cloth);
                Detail("Chest","Webbing buckle",new Vector3(side*.115f,1.40f,.171f),new Vector3(.027f,.028f,.009f),polymer);
                Detail("Head","Helmet side seam",new Vector3(side*.115f,1.68f,0),new Vector3(.009f,.012f,.14f),polymer);
                Detail("Head","Helmet vent insert",new Vector3(side*.119f,1.735f,-.015f),new Vector3(.008f,.035f,.065f),polymer);
                Detail("Chest","Vest utility fastener",new Vector3(side*.13f,1.21f,.178f),new Vector3(.018f,.022f,.008f),polymer);
            }
        }
        private static void Part(Transform parent,string name,Vector3 at,Vector3 size,Material material,Vector3 rotation=default) {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.transform.localEulerAngles=rotation;go.layer=parent.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh=bevel;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        private static void Combine(Transform parent,Report report) {
            var filters=parent.GetComponentsInChildren<MeshFilter>();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial)) {
                var mesh=new Mesh{name="Original hero batch "+group.Key.name};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateTangents();Store(mesh);
                var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;report.addedRenderers++;report.addedTriangles+=mesh.triangles.Length/3;
            }foreach(var filter in filters)UnityEngine.Object.DestroyImmediate(filter.gameObject);
        }
        private static void Store(Mesh mesh){if(first){AssetDatabase.CreateAsset(mesh,currentFolder+"/VisualPolishMeshes.asset");first=false;}else AssetDatabase.AddObjectToAsset(mesh,currentFolder+"/VisualPolishMeshes.asset");}
        private static Mesh Bevel() {
            var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();const float h=.5f,a=.48f;
            void Face(params Vector3[] points){var center=points.Aggregate(Vector3.zero,(x,y)=>x+y)/points.Length;if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)Array.Reverse(points);
                var start=v.Count;for(var i=0;i<points.Length;i++){v.Add(points[i]);uv.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0));}for(var i=1;i<points.Length-1;i++)indices.AddRange(new[]{start,start+i,start+i+1});}
            Vector3 V(int axis,float fixedValue,float x,float y){var p=Vector3.zero;p[axis]=fixedValue;p[(axis+1)%3]=x;p[(axis+2)%3]=y;return p;}
            for(var axis=0;axis<3;axis++)foreach(var s in new[]{-1,1})Face(V(axis,s*h,-a,-a),V(axis,s*h,a,-a),V(axis,s*h,a,a),V(axis,s*h,-a,a));
            for(var axis=0;axis<3;axis++)foreach(var s in new[]{-1,1})foreach(var t in new[]{-1,1}) {
                Vector3 E(float x,float y,float z){var p=Vector3.zero;p[axis]=x;p[(axis+1)%3]=y;p[(axis+2)%3]=z;return p;}
                Face(E(-a,s*h,t*a),E(a,s*h,t*a),E(a,s*a,t*h),E(-a,s*a,t*h));}
            foreach(var x in new[]{-1,1})foreach(var y in new[]{-1,1})foreach(var z in new[]{-1,1})Face(new Vector3(x*h,y*a,z*a),new Vector3(x*a,y*h,z*a),new Vector3(x*a,y*a,z*h));
            var mesh=new Mesh{name="Original restrained manufactured chamfer"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh Octagon() {
            var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(var side=0;side<8;side++){var start=v.Count;foreach(var end in new[]{0,1})foreach(var step in new[]{0,1}){var angle=(side+step)*Mathf.PI/4+Mathf.PI/8;v.Add(new Vector3(Mathf.Cos(angle)*.042f,Mathf.Sin(angle)*.043f,.045f+end*.225f));uv.Add(new Vector2((side+step)/8f,end));}indices.AddRange(new[]{start,start+1,start+3,start,start+3,start+2});}
            var mesh=new Mesh{name="Original octagonal weapon shroud"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        private static Maps Generate(string folder,string name,bool cloth) {
            const int size=1024;var albedo=new Color32[size*size];var normal=new Color32[size*size];var mask=new Color32[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++){var u=x/(float)size;var v=y/(float)size;var noise=Mathf.PerlinNoise(u*41+13,v*41+7);var coarse=Mathf.PerlinNoise(u*7+33,v*7+29);
                var weave=cloth?((x/3+y/3)%2==0?.018f:-.018f):0;var shade=.78f+noise*.14f+coarse*.08f+weave;
                var scratch=!cloth&&y%157==0&&x%223<73?.08f:0;albedo[y*size+x]=new Color(shade+scratch,shade+scratch,shade+scratch,1);
                normal[y*size+x]=new Color(.5f+(cloth?Mathf.Sin(x*Mathf.PI/3)*.035f:0),.5f+(cloth?Mathf.Sin(y*Mathf.PI/3)*.035f:0),1,1);
                // Only sparse scratches expose bare metal; fabric/polymer use no metallic map.
                mask[y*size+x]=new Color(scratch>0?.75f:.05f,0,0,cloth?.22f+noise*.12f:.38f+noise*.18f);}
            return new Maps{albedo=Map(folder,"Hero"+name+"Albedo.png",albedo,false,true),normal=Map(folder,"Hero"+name+"Normal.png",normal,true,false),mask=Map(folder,"Hero"+name+"Mask.png",mask,false,false)};
        }
        private static Texture2D Map(string folder,string name,Color32[] pixels,bool normal,bool srgb) {
            var path=folder+"/"+name;var t=new Texture2D(1024,1024,TextureFormat.RGBA32,false,!srgb);try{t.SetPixels32(pixels);t.Apply();WriteNew(path,t.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(t);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;imp.sRGBTexture=srgb;imp.mipmapEnabled=true;imp.isReadable=false;imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=4;imp.maxTextureSize=1024;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=1024,format=normal?TextureImporterFormat.BC5:TextureImporterFormat.BC7,compressionQuality=75});imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material Finish(string folder,string name,Color tint,Maps maps,float gloss,bool metal) {
            var m=new Material(Shader.Find("Standard")){name=name,color=tint,mainTexture=maps.albedo,enableInstancing=true};m.SetTexture("_BumpMap",maps.normal);m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.35f);m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",gloss);
            if(metal){m.SetTexture("_MetallicGlossMap",maps.mask);m.EnableKeyword("_METALLICGLOSSMAP");m.SetFloat("_GlossMapScale",gloss);}AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;
        }
        private static Dictionary<Collider,string> Snapshot(GameObject root)=>root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>c.GetType().FullName+"|"+c.transform.localToWorldMatrix.ToString("R")+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+(c is BoxCollider b?"|"+b.center.ToString("R")+"|"+b.size.ToString("R"):c is CharacterController a?"|"+a.height+"|"+a.radius+"|"+a.center.ToString("R"):""));
        private static void WriteNew(string path,byte[] bytes){using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write))output.Write(bytes,0,bytes.Length);}
    }
}
""";
}
