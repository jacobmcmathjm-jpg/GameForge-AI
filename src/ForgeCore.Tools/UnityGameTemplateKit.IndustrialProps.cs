namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndustrialPropIdentitySource = """
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    public sealed class IndustrialPropIdentity:MonoBehaviour
    {
        public string recipe,assetSha256,sourceAssetPath;
        public string collisionConvention="solid_box_no_walkable_interior";
        // Identity is not an art/rights attestation; the library/review service owns those decisions.
    }
}
""";
    private const string IndustrialPropBuilderSource = """
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreIndustrialPropBuilder
    {
        [Serializable]private sealed class SourceMetadata {
            public int schemaVersion;public string recipe,collisionConvention,coordinateSystem;public float[] collisionCentre,collisionSize;
            public int[] lodTriangles;public bool uvFinite,rootIdentity;
        }
        [Serializable]private sealed class Report {
            public int schemaVersion=1,lodLevels=3,colliderCount=1,missingResources=0;
            public string recipe,assetPath,assetSha256;public int[] triangles;public float[] unityBounds;
            public bool uvFinite=true,rootIdentity=true,visualReviewed=false,rightsReviewed=false,placementApproved=false;
        }
        public static string Build(string assetPath,string hash,string id,string scenePath,string outputPath)
        {
            var folder="Assets/ForgeCore/IndustrialProps/"+id;
            foreach(var path in new[]{folder,folder+".meta",scenePath,scenePath+".meta",outputPath,outputPath+".forgecore-rig-preview.json"})
                if(File.Exists(path)||Directory.Exists(path))throw new InvalidOperationException("Industrial review destination occupied; no overwrite.");
            if(!assetPath.StartsWith("Assets/ForgeCore/Generated/Blender/",StringComparison.Ordinal)||!assetPath.EndsWith(".glb",StringComparison.Ordinal))
                throw new InvalidOperationException("Imported local GLB required.");
            if(Sha(assetPath)!=hash)throw new InvalidOperationException("Industrial main source changed.");
            var stem=assetPath.Substring(0,assetPath.Length-4);
            var source=JsonUtility.FromJson<SourceMetadata>(File.ReadAllText(stem+".prop.json"));
            if(source.schemaVersion!=1||source.collisionConvention!="solid_box_no_walkable_interior"||source.coordinateSystem!="Blender_Z_up_metres"||
                !source.uvFinite||!source.rootIdentity||source.lodTriangles==null||source.lodTriangles.Length!=3||source.collisionSize==null||source.collisionSize.Length!=3)
                throw new InvalidOperationException("Industrial source conventions failed.");
            Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.GetDirectoryName(scenePath));Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Industrial prop — unreviewed technical fixture");
            var lods=new LOD[3];var counts=new int[3];Bounds bounds=default;var first=true;
            for(var level=0;level<3;level++) {
                var path=level==0?assetPath:stem+"-lod"+level+".glb";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)throw new InvalidOperationException("Industrial LOD import is missing.");
                var child=UnityEngine.Object.Instantiate(prefab,root.transform);child.name="LOD"+level;child.transform.localPosition=Vector3.zero;
                child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;
                if(child.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("Source must not inject colliders.");
                var renderers=child.GetComponentsInChildren<Renderer>();if(renderers.Length<1)throw new InvalidOperationException("Empty LOD.");
                foreach(var filter in child.GetComponentsInChildren<MeshFilter>()) {
                    var mesh=filter.sharedMesh;if(mesh==null||mesh.uv.Length!=mesh.vertexCount)throw new InvalidOperationException("Missing mesh UVs.");
                    counts[level]+=mesh.triangles.Length/3;
                    if(mesh.uv.Any(uv=>!Finite(uv.x)||!Finite(uv.y)))throw new InvalidOperationException("Invalid UVs.");
                }
                foreach(var renderer in renderers) {
                    if(renderer.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported))throw new InvalidOperationException("Missing/unsupported PBR resource.");
                    if(level==0){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                }
                lods[level]=new LOD(level==0?.55f:level==1?.25f:.06f,renderers);
                if(counts[level]!=source.lodTriangles[level])throw new InvalidOperationException("Actual Unity LOD triangles differ from Blender.");
            }
            var expected=new Vector3(source.collisionSize[0],source.collisionSize[2],source.collisionSize[1]);
            if(Vector3.Distance(bounds.size,expected)>.015f||Mathf.Abs(bounds.min.y)>.006f)throw new InvalidOperationException("Unity metre scale or grounded bounds differ from source.");
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;root.layer=8;
            var identity=root.AddComponent<IndustrialPropIdentity>();identity.recipe=source.recipe;identity.assetSha256=hash;identity.sourceAssetPath=assetPath;
            if(PrefabUtility.SaveAsPrefabAsset(root,folder+"/IndustrialProp.prefab")==null)throw new InvalidOperationException("Industrial prefab save failed.");
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Review ground";
            ground.transform.position=new Vector3(0,-.12f,0);ground.transform.localScale=new Vector3(30,.24f,30);
            var groundMaterial=new Material(Shader.Find("Standard")){color=new Color(.10f,.12f,.13f)};groundMaterial.SetFloat("_Glossiness",.1f);
            AssetDatabase.CreateAsset(groundMaterial,folder+"/ReviewGround.mat");ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_Exposure",1.1f);AssetDatabase.CreateAsset(sky,folder+"/ReviewSky.mat");
            RenderSettings.skybox=sky;RenderSettings.ambientLight=new Color(.45f,.48f,.5f);RenderSettings.ambientIntensity=1;
            var sun=new GameObject("Review sunlight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-30,0);sun.shadows=LightShadows.Soft;
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;
            camera.orthographic=true;var extent=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            camera.orthographicSize=extent*.75f;camera.transform.position=bounds.center+new Vector3(extent*1.3f,extent*.85f,-extent*1.5f);
            camera.transform.LookAt(bounds.center);camera.nearClipPlane=.03f;camera.farClipPlane=200;
            group.ForceLOD(0);SaveCamera(camera,outputPath);group.ForceLOD(-1);
            var report=new Report{recipe=source.recipe,assetPath=assetPath,assetSha256=hash,triangles=counts,unityBounds=new[]{bounds.size.x,bounds.size.y,bounds.size.z}};
            File.WriteAllText(folder+"/prop-report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Industrial review scene save failed.");
            return "Industrial prop review fixture: actual 3-level LOD, measured metre bounds, original PBR, finite UVs and one conservative solid-box collider. Human source/target visual and rights reviews remain required; not an approved level asset.";
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static string Sha(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        private static void SaveCamera(Camera camera,string path) {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("A real graphics device is required for industrial visual evidence.");
            var old=RenderTexture.active;var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                using(var file=new FileStream(path,FileMode.CreateNew)){var bytes=texture.EncodeToPNG();file.Write(bytes,0,bytes.Length);}}
            finally{camera.targetTexture=null;RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
""";
}
