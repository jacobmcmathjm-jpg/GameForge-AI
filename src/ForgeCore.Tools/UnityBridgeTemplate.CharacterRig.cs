namespace ForgeCore.Tools;
internal static partial class UnityBridgeTemplate
{
    private const string CharacterRigSource = """
#if UNITY_EDITOR
namespace ForgeCore.Editor
{
    public static partial class ForgeCoreBridge
    {
        [Serializable] private sealed class RigPose
        {
            public string pose; public int movedVertices;
            public float maximumDisplacement, maximumEdgeStretch; public bool finite;
        }
        [Serializable] private sealed class RigInspection
        {
            public int schemaVersion = 1;
            public string assetSha256, rigConvention = "generic_named_bones_v1";
            public int boneCount, vertexCount, unweightedVertices;
            public float maximumWeightSumError, maximumRestError, maximumRecoveryError, height, groundOffset;
            public RigPose[] poses;
        }
        private static readonly string[] RigRequiredBones = { "Hips","Spine","Chest","Neck","Head",
            "Clavicle.L","UpperArm.L","LowerArm.L","Hand.L","UpperLeg.L","LowerLeg.L","Foot.L","Toe.L",
            "Clavicle.R","UpperArm.R","LowerArm.R","Hand.R","UpperLeg.R","LowerLeg.R","Foot.R","Toe.R" };
        private static bool RigFinite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool RigFinite(Vector3 p) { return RigFinite(p.x) && RigFinite(p.y) && RigFinite(p.z); }
        private static Dictionary<string,Transform> RigBones(GameObject root)
        {
            var result = new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(Array.IndexOf(RigRequiredBones,t.name)>=0)
                { if(result.ContainsKey(t.name)) throw new InvalidOperationException("Ambiguous named character bone."); result.Add(t.name,t); }
            foreach(var name in RigRequiredBones) if(!result.ContainsKey(name)) throw new InvalidOperationException("Missing character bone: "+name);
            foreach(var side in new[]{"L","R"})
                foreach(var pair in new[]{new[]{"UpperArm","Clavicle"},new[]{"LowerArm","UpperArm"},new[]{"Hand","LowerArm"},
                    new[]{"LowerLeg","UpperLeg"},new[]{"Foot","LowerLeg"},new[]{"Toe","Foot"}})
                    if(!result[pair[0]+"."+side].IsChildOf(result[pair[1]+"."+side])) throw new InvalidOperationException("Character limb hierarchy is invalid.");
            return result;
        }
        private static void RigTurn(Dictionary<string,Transform> bones,string name,Vector3 worldAxis,float angle)
        { var t=bones[name]; t.rotation=Quaternion.AngleAxis(angle,worldAxis)*t.rotation; }
        private static void RigPoseApply(GameObject root,int pose)
        {
            var b=RigBones(root);
            if(pose==3) { RigTurn(b,"Hips",Vector3.up,15); RigTurn(b,"Chest",Vector3.forward,10); }
            if(pose==1 || pose==3)
            {
                RigTurn(b,"UpperArm.L",Vector3.forward,-30); RigTurn(b,"LowerArm.L",Vector3.up,-65);
                RigTurn(b,"UpperArm.R",Vector3.forward,30); RigTurn(b,"LowerArm.R",Vector3.up,65);
            }
            if(pose==2 || pose==3)
            {
                RigTurn(b,"UpperLeg.L",Vector3.right,-35); RigTurn(b,"LowerLeg.L",Vector3.right,70);
                RigTurn(b,"UpperLeg.R",Vector3.right,25); RigTurn(b,"LowerLeg.R",Vector3.right,-45);
            }
        }
        private static Vector3[] RigBake(SkinnedMeshRenderer skin)
        {
            var mesh=new Mesh();
            try { skin.BakeMesh(mesh); return mesh.vertices; }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static RigPose RigMeasurePose(SkinnedMeshRenderer skin,Vector3[] rest,string label)
        {
            var posed=RigBake(skin);
            if(posed.Length!=rest.Length) throw new InvalidOperationException("Baked skin vertex count changed.");
            var result=new RigPose { pose=label, finite=true, maximumEdgeStretch=1 };
            for(var i=0;i<posed.Length;i++)
            {
                if(!RigFinite(posed[i])) throw new InvalidOperationException("Character pose has nonfinite vertices.");
                var delta=Vector3.Distance(rest[i],posed[i]);
                result.maximumDisplacement=Mathf.Max(result.maximumDisplacement,delta);
                if(delta>.001f) result.movedVertices++;
            }
            var triangles=skin.sharedMesh.triangles;
            for(var i=0;i<triangles.Length;i+=3)
                for(var edge=0;edge<3;edge++)
                {
                    var a=triangles[i+edge]; var b=triangles[i+(edge+1)%3];
                    var length=Vector3.Distance(rest[a],rest[b]);
                    if(length>.00001f) result.maximumEdgeStretch=Mathf.Max(result.maximumEdgeStretch,Vector3.Distance(posed[a],posed[b])/length);
                }
            if(result.movedVertices<100 || result.maximumDisplacement<.02f || result.maximumDisplacement>4f || result.maximumEdgeStretch>3.5f)
                throw new InvalidOperationException("Diagnostic pose "+label+" failed: moved="+result.movedVertices+", displacement="+result.maximumDisplacement+", edge stretch="+result.maximumEdgeStretch);
            return result;
        }
        private static RigInspection InspectCharacterRig(BridgeRequest request)
        {
            var source=GeneratedAssetAbsolutePath(request.assetPath);
            if(!FixedEquals(request.assetSha256,Hex(SHA256.Create().ComputeHash(File.ReadAllBytes(source)))))
                throw new InvalidOperationException("Character GLB hash changed.");
            var scenePath=AssetAbsolutePath(request.scenePath); var output=PreviewAbsolutePath(request.outputPath);
            foreach(var target in new[]{scenePath,scenePath+".meta",output,output+".forgecore-rig-preview.json"})
                if(File.Exists(target) || Directory.Exists(target)) throw new InvalidOperationException("Rig review destinations already exist; no overwrite.");
            var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid() || !string.IsNullOrEmpty(scene.path) || scene.GetRootGameObjects().Length!=0)
                throw new InvalidOperationException("Rig inspection requires the empty unsaved batch scene.");
            AssetDatabase.ImportAsset(request.assetPath,ImportAssetOptions.ForceSynchronousImport);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.assetPath);
            if(prefab==null) throw new InvalidOperationException("No installed GLB GameObject importer.");
            var roots=new GameObject[4];
            var result=new RigInspection { assetSha256=request.assetSha256, poses=new RigPose[3] };
            for(var pose=0;pose<4;pose++)
            {
                roots[pose]=PrefabUtility.InstantiatePrefab(prefab,scene) as GameObject;
                var root=roots[pose]; if(root==null)throw new InvalidOperationException("Character prefab instantiation failed.");
                root.name=new[]{"Rest T pose","Shoulders and elbows","Hips and knees","Combined torso and limbs"}[pose];
                if(Vector3.Distance(root.transform.localScale,Vector3.one)>.001f || Quaternion.Angle(root.transform.localRotation,Quaternion.identity)>.5f)
                    throw new InvalidOperationException("Character requires identity scale and canonical rotation.");
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) {t.gameObject.SetActive(true);t.gameObject.layer=30;}
                var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length!=1 || skins[0].sharedMesh==null) throw new InvalidOperationException("One actual skinned character mesh is required.");
                var skin=skins[0];skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;
                var mesh=skin.sharedMesh; var bones=skin.bones; var bind=mesh.bindposes;
                if(!mesh.isReadable || bones.Length<21 || bones.Length>64 || bind.Length!=bones.Length || mesh.vertexCount<100 || mesh.vertexCount>150000)
                    throw new InvalidOperationException("Readable skin/bind-pose/bone budget failed.");
                var named=RigBones(root);
                for(var i=0;i<bones.Length;i++)
                {
                    if(bones[i]==null || !bones[i].IsChildOf(root.transform) || !RigFinite(bind[i].determinant) || Mathf.Abs(bind[i].determinant)<.00000001f)
                        throw new InvalidOperationException("Invalid bone transform or bind matrix.");
                    for(var c=0;c<16;c++) if(!RigFinite(bind[i][c]))throw new InvalidOperationException("Nonfinite bind pose.");
                }
                foreach(var material in skin.sharedMaterials)
                    if(material==null || material.shader==null || !material.shader.isSupported || !IsPbrShader(material.shader.name))
                        throw new InvalidOperationException("Character material is missing or unsupported.");
                if(skin.sharedMaterials.Length!=mesh.subMeshCount)throw new InvalidOperationException("Missing character material slots.");
                var weights=mesh.boneWeights;
                if(weights.Length!=mesh.vertexCount)throw new InvalidOperationException("Skin has unweighted vertices.");
                foreach(var w in weights)
                {
                    var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var indices=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                    float sum=0;
                    for(var i=0;i<4;i++) { if(!RigFinite(values[i]) || values[i]<0 || values[i]>1 || indices[i]<0 || indices[i]>=bones.Length)throw new InvalidOperationException("Invalid skin weights or joint indices.");sum+=values[i]; }
                    result.maximumWeightSumError=Mathf.Max(result.maximumWeightSumError,Mathf.Abs(sum-1));
                    if(sum<=0)result.unweightedVertices++;
                }
                if(result.maximumWeightSumError>.002f || result.unweightedVertices!=0)throw new InvalidOperationException("Skin weights are not normalized.");
                var rest=RigBake(skin);var raw=mesh.vertices;
                for(var i=0;i<rest.Length;i++) {if(!RigFinite(rest[i]))throw new InvalidOperationException("Invalid rest geometry."); result.maximumRestError=Mathf.Max(result.maximumRestError,Vector3.Distance(rest[i],raw[i]));}
                if(result.maximumRestError>.002f)throw new InvalidOperationException("Bind pose does not recover the authored rest mesh: "+result.maximumRestError);
                var restRotations=new Dictionary<Transform,Quaternion>();foreach(var t in named.Values)restRotations.Add(t,t.localRotation);
                if(pose==0)
                {
                    var bounds=new Bounds(skin.transform.TransformPoint(rest[0]),Vector3.zero);
                    foreach(var v in rest)bounds.Encapsulate(skin.transform.TransformPoint(v));
                    result.height=bounds.size.y;result.groundOffset=Mathf.Abs(bounds.min.y-root.transform.position.y);
                    result.boneCount=bones.Length;result.vertexCount=mesh.vertexCount;
                    if(result.height<1.59f || result.height>2.01f || result.groundOffset>.01f)throw new InvalidOperationException("Character scale/grounded feet failed.");
                }
                else
                {
                    RigPoseApply(root,pose);
                    result.poses[pose-1]=RigMeasurePose(skin,rest,new[]{"arms","legs","combined"}[pose-1]);
                    foreach(var pair in restRotations)pair.Key.localRotation=pair.Value;
                    var recovered=RigBake(skin);
                    for(var i=0;i<rest.Length;i++)result.maximumRecoveryError=Mathf.Max(result.maximumRecoveryError,Vector3.Distance(rest[i],recovered[i]));
                    if(result.maximumRecoveryError>.0001f)throw new InvalidOperationException("Skin pose recovery drifted.");
                    RigPoseApply(root,pose);
                }
            }
            var cameraObject=new GameObject("S02 review camera"); var camera=cameraObject.AddComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.08f);camera.cullingMask=1<<30;
            camera.orthographic=true;camera.orthographicSize=1.18f;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;
            camera.transform.position=new Vector3(2.8f,1.85f,5f);camera.transform.LookAt(new Vector3(0,.9f,0));
            var key=new GameObject("S02 soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.5f;key.transform.rotation=Quaternion.Euler(35,-145,0);key.cullingMask=1<<30;
            var fill=new GameObject("S02 cool fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.color=new Color(.65f,.8f,1);fill.transform.rotation=Quaternion.Euler(20,35,0);fill.cullingMask=1<<30;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.34f,.38f);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            RigRenderSheet(camera,roots,output);
            for(var p=0;p<4;p++){roots[p].SetActive(true);roots[p].transform.position=new Vector3(p%2==0?-1.2f:1.2f,0,p<2?0:-2.4f);}
            camera.transform.position=new Vector3(3.8f,3.8f,6f);camera.transform.LookAt(new Vector3(0,.9f,-1.2f));camera.orthographicSize=3;
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            if(!EditorSceneManager.SaveScene(scene,request.scenePath))throw new InvalidOperationException("Failed saving new rig test scene.");
            AssetDatabase.SaveAssets();
            return result;
        }
        private static void RigRenderSheet(Camera camera,GameObject[] roots,string output)
        {
            const int w=640,h=720;var atlas=new Texture2D(w*2,h*2,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                for(var p=0;p<4;p++)
                {
                    for(var i=0;i<4;i++)roots[i].SetActive(i==p);
                    var render=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);var tile=new Texture2D(w,h,TextureFormat.RGB24,false);
                    try
                    {
                        camera.aspect=w/(float)h;render.Create();camera.targetTexture=render;RenderTexture.active=render;camera.Render();
                        tile.ReadPixels(new Rect(0,0,w,h),0,0,false);tile.Apply(false,false);
                        var pixels=tile.GetPixels32();var min=1f;var max=0f;var magenta=0;
                        foreach(var px in pixels){var lum=(px.r*.2126f+px.g*.7152f+px.b*.0722f)/255;min=Mathf.Min(min,lum);max=Mathf.Max(max,lum);if(px.r>245&&px.g<20&&px.b>245)magenta++;}
                        if(max-min<.08f || magenta/(float)pixels.Length>.01f)throw new InvalidOperationException("Character pose review render is blank or has unsupported materials.");
                        atlas.SetPixels((p%2)*w,(1-p/2)*h,w,h,tile.GetPixels());
                    }
                    finally {camera.targetTexture=null;RenderTexture.active=previous;render.Release();UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(tile);}
                }
                atlas.Apply(false,false);File.WriteAllBytes(output,atlas.EncodeToPNG());ValidatePreviewPng(output);
            }
            finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(atlas);}
        }
    }
}
#endif
""";
}
