using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ForgeCore.Tools;

internal static class UnityBridgeTemplate
{
    public const string ProtocolVersion = "1.0";
    public const string BridgeVersion = "0.17.15";
    public static readonly IReadOnlyDictionary<string, string> UpgradeableBridges = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["0.16.0"] = "bad2638764d00c40aa8fd3f1f72685589b271abf86703ba4faae1c3c9e63ab11",
        ["0.17.0"] = "f06b55dbf775ee0210c87e1408c65dcba9e45910ccb37db8bbfeb4a1ca5a156d",
        ["0.17.1"] = "719253e0e9674f5c68fbb6fa03a09c26945737f44736d71d890d75d49742ca73",
        ["0.17.2"] = "c9452173e78d8b0fbef0fe76d36a8711308925677d28476fe68fe140ef5024ad",
        ["0.17.3"] = "7b4ac77d27c77eb470a2868173a368951b1bd41d43faf1fbd84cc573afbae3fe",
        ["0.17.4"] = "e4d973fa1dd72cd8a9da2e8cb58c4ac798cc99753d036e655a198094d19f94b9",
        ["0.17.5"] = "87cb2dd1a2a5ff4ad6bbd331fb33fced5556901ebe2ea538d921f00328b868a8",
        ["0.17.6"] = "5bf1e278e92452cba76a3c9019ae12dcd03cd3c6fd01cd36d3dc2f7b5468d2a2",
        ["0.17.7"] = "34379dca4f80da61946899fa6a288584d518d6956be5da97752fc3f4564d63a2",
        ["0.17.8"] = "b57957222403dbe3e7e9216f1142dc4306dcdb4e1b04aee3067c02589fb066c5",
        ["0.17.9"] = "75f8bd8035f5a943c83d062f87b88f2b0e6dded87ad6210a03c2eecb2266d389",
        ["0.17.10"] = "ab69e2a2f73c92c6bdc68abe56cc9f748756ea2ee25d311596931f7fa20a481b",
        ["0.17.11"] = "dcd49f8a42543bf84eff4f3c6124b4bbbff68d457dd22df28875efff402be892",
        ["0.17.12"] = "b050c2ae6d85cf6cf432f8eb5d7cabc69f7b6ae12bbb0c2d211031d713dc6a38",
        ["0.17.13"] = "b2b03369111a8091c9b03b3e41e2e4ea63b3df3706f1d01634e276527171f770",
        ["0.17.14"] = "4da6b9a5003fd8f98e214a16809b9f8dd787d23b4d648996fe306a60db49bbb2"
    };

    public static string Sha256 => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Source))).ToLowerInvariant();
    public static string VersionManifest => JsonSerializer.Serialize(new
    {
        protocolVersion = ProtocolVersion,
        bridgeVersion = BridgeVersion,
        sha256 = Sha256,
        installedUtc = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { WriteIndented = true });

    public const string Source = """
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace ForgeCore.Editor
{
    public static class ForgeCoreBridge
    {
        private const string ProtocolVersion = "1.0";
        private const string BridgeVersion = "0.17.15";
        private const string PlayPending = "ForgeCore.Play.Pending";
        private const string PlayResultPath = "ForgeCore.Play.ResultPath";
        private const string PlayRequestId = "ForgeCore.Play.RequestId";
        private const string PlayOperation = "ForgeCore.Play.Operation";
        private const string PlayStartTicks = "ForgeCore.Play.StartTicks";
        private const string PlayDuration = "ForgeCore.Play.Duration";
        private const string PreviewOutputPath = "ForgeCore.Play.PreviewOutputPath";
        private const string PreviewRequested = "ForgeCore.Play.PreviewRequested";
        private const string PreviewRequestedTicks = "ForgeCore.Play.PreviewRequestedTicks";
        private const string PreviewError = "ForgeCore.Play.PreviewError";
        private const string PreviewMetrics = "ForgeCore.Play.PreviewMetrics";
        private const string GameplayExecuted = "ForgeCore.Play.GameplayExecuted";
        private const string GameplayError = "ForgeCore.Play.GameplayError";
        private const string GameplaySummary = "ForgeCore.Play.GameplaySummary";
        private const string PerformanceError = "ForgeCore.Play.PerformanceError";
        private const string PerformanceMetrics = "ForgeCore.Play.PerformanceMetrics";
        private static readonly List<float> PerformanceUpdateMilliseconds = new List<float>();
        private static double performanceLastRealtimeSeconds = -1d;
        private static int performanceMaximumDrawCalls;
        private static int performanceMaximumSetPassCalls;
        private static long performanceMaximumTriangles;
        private static long performanceMaximumVertices;
        private static long performancePeakAllocatedMemoryBytes;

        [Serializable]
        private sealed class BridgeRequest
        {
            public string protocolVersion;
            public string requestId;
            public string operation;
            public string scenePath;
            public string sceneTemplate;
            public string mapTemplate;
            public string templateId;
            public int seed;
            public int levelCount;
            public string objectName;
            public string primitive;
            public float positionX;
            public float positionY;
            public float positionZ;
            public string componentType;
            public string assetPath;
            public string assetSha256;
            public string assetSetId;
            public string assetRole;
            public string styleFamily;
            public string parentObjectName;
            public float targetHeight;
            public string colliderMode;
            public bool replacePlaceholder;
            public int durationSeconds;
            public string outputPath;
            public bool developmentBuild;
        }

        [Serializable]
        private sealed class BridgeResult
        {
            public string protocolVersion = ProtocolVersion;
            public string bridgeVersion = BridgeVersion;
            public string editorVersion = Application.unityVersion;
            public string requestId;
            public string operation;
            public bool success;
            public string summary;
            public BridgeArtInspection artInspection;
            public BridgePerformance performance;
        }

        [Serializable]
        private sealed class BridgePerformance
        {
            public string environment;
            public int sampledUpdates;
            public float averageUpdateMilliseconds;
            public float p95UpdateMilliseconds;
            public float maximumUpdateMilliseconds;
            public int maximumDrawCalls;
            public int maximumSetPassCalls;
            public long maximumTriangles;
            public long maximumVertices;
            public long peakAllocatedMemoryBytes;
        }

        [Serializable]
        private sealed class BridgeArtInspection
        {
            public string assetPath;
            public string assetSha256;
            public string assetSetId;
            public string assetRole;
            public string styleFamily;
            public int rendererCount;
            public int meshCount;
            public int skinnedMeshRendererCount;
            public int animatorCount;
            public int avatarCount;
            public int materialCount;
            public int pbrMaterialCount;
            public int textureCount;
            public long texturePixelCount;
            public long estimatedTextureMemoryBytes;
            public int maximumTextureWidth;
            public int maximumTextureHeight;
            public int missingMaterialCount;
            public int missingShaderCount;
            public long vertexCount;
            public long triangleCount;
            public int animationClipCount;
            public int lodGroupCount;
            public int lodLevelCount;
            public int colliderCount;
            public float boundsWidth;
            public float boundsHeight;
            public float boundsDepth;
            public float pivotBottomOffsetNormalized;
            public float pivotHorizontalOffsetNormalized;
            public float rootScaleX;
            public float rootScaleY;
            public float rootScaleZ;
            public float rootRotationDegrees;
            public bool hasPbrMaterials;
            public bool isRigged;
            public bool hasLods;
            public bool hasCollision;
            public bool hasGroundedPivot;
            public bool hasUniformRootScale;
            public bool hasCanonicalRootRotation;
        }

        [InitializeOnLoadMethod]
        private static void ResumePendingPlayMode()
        {
            if (SessionState.GetBool(PlayPending, false))
            {
                EditorApplication.update -= MonitorPlayMode;
                EditorApplication.update += MonitorPlayMode;
            }
        }

        public static void Run()
        {
            string resultPath = null;
            BridgeRequest request = null;
            try
            {
                var requestPath = CommandLineValue("-forgecoreRequest");
                resultPath = CommandLineValue("-forgecoreResult");
                var approvedHash = CommandLineValue("-forgecoreRequestSha256");
                var projectRoot = ProjectRoot();
                EnsureInsideProject(projectRoot, requestPath);
                EnsureInsideProject(projectRoot, resultPath);
                var bytes = File.ReadAllBytes(requestPath);
                var actualHash = Hex(SHA256.Create().ComputeHash(bytes));
                if (!FixedEquals(approvedHash, actualHash)) throw new InvalidOperationException("Unity bridge request hash mismatch.");
                request = JsonUtility.FromJson<BridgeRequest>(Encoding.UTF8.GetString(bytes));
                ValidateRequest(request);

                switch (request.operation)
                {
                    case "capabilities":
                        WriteResult(resultPath, request, true,
                            "ForgeCore Unity bridge ready; operations=capabilities,compile,create_scene,create_map_demo,create_game_template,create_game_campaign,create_primitive,add_component,prepare_generated_asset,inspect_game_art_asset,capture_game_art_preview,run_tests,play_mode,verify_gameplay,profile_scene,capture_scene_preview,build_windows; editor=" + Application.unityVersion);
                        return;
                    case "compile":
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Unity reports script compilation errors.");
                        WriteResult(resultPath, request, true, "Unity script compilation completed without reported compiler errors.");
                        return;
                    case "create_scene":
                        CreateScene(request);
                        WriteResult(resultPath, request, true, "Created scene " + request.scenePath + " from " + request.sceneTemplate + " without overwrite.");
                        return;
                    case "create_map_demo":
                        var mapSummary = CreateMapDemo(request);
                        WriteResult(resultPath, request, true, mapSummary);
                        return;
                    case "create_game_template":
                        var gameSummary = CreateGameTemplate(request);
                        WriteResult(resultPath, request, true, gameSummary);
                        return;
                    case "create_game_campaign":
                        var campaignSummary = CreateGameCampaign(request);
                        WriteResult(resultPath, request, true, campaignSummary);
                        return;
                    case "create_primitive":
                        CreatePrimitive(request);
                        WriteResult(resultPath, request, true, "Created " + request.primitive + " object '" + request.objectName + "' in " + request.scenePath + ".");
                        return;
                    case "add_component":
                        AddComponent(request);
                        WriteResult(resultPath, request, true, "Added component " + request.componentType + " to '" + request.objectName + "' in " + request.scenePath + ".");
                        return;
                    case "prepare_generated_asset":
                        var preparationSummary = PrepareGeneratedAsset(request);
                        WriteResult(resultPath, request, true, preparationSummary);
                        return;
                    case "inspect_game_art_asset":
                        var artInspection = InspectGameArtAsset(request);
                        WriteResult(resultPath, request, true,
                            "Inspected game-art asset '" + request.assetSetId + "' as " + request.assetRole + " / " + request.styleFamily +
                            "; renderers=" + artInspection.rendererCount + "; meshes=" + artInspection.meshCount +
                            "; materials=" + artInspection.materialCount + "; vertices=" + artInspection.vertexCount +
                            "; triangles=" + artInspection.triangleCount + "; textures=" + artInspection.textureCount +
                            "; texture memory=" + artInspection.estimatedTextureMemoryBytes + " bytes; grounded pivot=" + artInspection.hasGroundedPivot +
                            "; uniform root scale=" + artInspection.hasUniformRootScale + "; canonical root rotation=" + artInspection.hasCanonicalRootRotation +
                            "; animation clips=" + artInspection.animationClipCount +
                            "; LOD groups=" + artInspection.lodGroupCount + "; colliders=" + artInspection.colliderCount +
                            ". Structural inspection does not complete rights or visual review.", artInspection);
                        return;
                    case "capture_game_art_preview":
                        var artPreviewSummary = CaptureGameArtPreview(request);
                        WriteResult(resultPath, request, true, artPreviewSummary);
                        return;
                    case "play_mode":
                        StartPlayMode(request, resultPath);
                        return;
                    case "verify_gameplay":
                        StartPlayMode(request, resultPath);
                        return;
                    case "profile_scene":
                        StartPlayMode(request, resultPath);
                        return;
                    case "capture_scene_preview":
                        StartPlayMode(request, resultPath);
                        return;
                    case "build_windows":
                        BuildWindows(request);
                        WriteResult(resultPath, request, true, "Windows development build succeeded at " + request.outputPath + ".");
                        return;
                    default:
                        throw new InvalidOperationException("Operation is not implemented by this bridge entry point.");
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(resultPath))
                    WriteResult(resultPath, request, false, ex.GetType().Name + ": " + ex.Message);
                Debug.LogError("ForgeCore bridge failure: " + ex);
            }
        }

        private static void CreateScene(BridgeRequest request)
        {
            var absolute = AssetAbsolutePath(request.scenePath);
            if (File.Exists(absolute)) throw new InvalidOperationException("Scene already exists; ForgeCore never overwrites scenes.");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (request.sceneTemplate == "basic_3d")
            {
                var camera = new GameObject("Main Camera"); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 2, -10); camera.AddComponent<Camera>(); camera.AddComponent<AudioListener>();
                var light = new GameObject("Directional Light"); light.transform.rotation = Quaternion.Euler(50, -30, 0);
                var component = light.AddComponent<Light>(); component.type = LightType.Directional;
            }
            else if (request.sceneTemplate == "basic_2d")
            {
                var camera = new GameObject("Main Camera"); camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
                var component = camera.AddComponent<Camera>(); component.orthographic = true; camera.AddComponent<AudioListener>();
            }
            if (!EditorSceneManager.SaveScene(scene, request.scenePath, false)) throw new InvalidOperationException("Unity could not save the new scene.");
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var registered = false;
            foreach (var buildScene in buildScenes)
                if (string.Equals(buildScene.path, request.scenePath, StringComparison.Ordinal)) { registered = true; break; }
            if (!registered)
            {
                buildScenes.Add(new EditorBuildSettingsScene(request.scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        private static void CreatePrimitive(BridgeRequest request)
        {
            var scene = EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Single);
            if (FindObjects(scene, request.objectName).Count != 0) throw new InvalidOperationException("An object with that exact name already exists.");
            GameObject value;
            switch (request.primitive)
            {
                case "empty": value = new GameObject(); break;
                case "cube": value = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                case "sphere": value = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                case "capsule": value = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                case "cylinder": value = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case "plane": value = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                case "quad": value = GameObject.CreatePrimitive(PrimitiveType.Quad); break;
                default: throw new InvalidOperationException("Unsupported primitive.");
            }
            value.name = request.objectName; value.transform.position = new Vector3(request.positionX, request.positionY, request.positionZ);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unity could not save the modified scene.");
        }

        private static string CreateMapDemo(BridgeRequest request)
        {
            if (request.mapTemplate != "frontier_valley" || request.seed < 1 || request.seed > 999999)
                throw new InvalidOperationException("Map template or seed is invalid.");
            Type builder = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                builder = assembly.GetType("ForgeCoreMapDemo.Editor.FrontierValleyBuilder", false, false);
                if (builder != null) break;
            }
            if (builder == null) throw new InvalidOperationException("The hash-verified Frontier Valley builder type is not compiled.");
            var method = builder.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(string)) throw new InvalidOperationException("The Frontier Valley builder contract is invalid.");
            try { return (string)method.Invoke(null, new object[] { request.scenePath, request.seed }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("Frontier Valley builder failed: " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message)); }
        }

        private static string CreateGameTemplate(BridgeRequest request)
        {
            if ((request.templateId != "first_person_action" && request.templateId != "platformer" &&
                 request.templateId != "top_down_rpg" && request.templateId != "racing") ||
                request.seed < 1 || request.seed > 999999)
                throw new InvalidOperationException("Game template or seed is invalid.");
            Type builder = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                builder = assembly.GetType("ForgeCoreGameTemplates.Editor.ForgeCorePrototypeBuilder", false, false);
                if (builder != null) break;
            }
            if (builder == null) throw new InvalidOperationException("The hash-verified ForgeCore game-template builder type is not compiled.");
            var method = builder.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(string), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(string)) throw new InvalidOperationException("The ForgeCore game-template builder contract is invalid.");
            try { return (string)method.Invoke(null, new object[] { request.scenePath, request.templateId, request.seed }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("ForgeCore game-template builder failed: " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message)); }
        }

        private static string CreateGameCampaign(BridgeRequest request)
        {
            if ((request.templateId != "first_person_action" && request.templateId != "platformer" &&
                 request.templateId != "top_down_rpg" && request.templateId != "racing") ||
                request.seed < 1 || request.seed > 999999 || request.levelCount < 1 || request.levelCount > 12)
                throw new InvalidOperationException("Game campaign template, seed, or level count is invalid.");
            Type builder = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                builder = assembly.GetType("ForgeCoreGameTemplates.Editor.ForgeCorePrototypeBuilder", false, false);
                if (builder != null) break;
            }
            if (builder == null) throw new InvalidOperationException("The hash-verified ForgeCore game-template builder type is not compiled.");
            var method = builder.GetMethod("BuildCampaign", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(string)) throw new InvalidOperationException("The ForgeCore game-campaign builder contract is invalid.");
            try { return (string)method.Invoke(null, new object[] { request.scenePath, request.templateId, request.seed, request.levelCount }); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("ForgeCore game-campaign builder failed: " + (ex.InnerException == null ? ex.Message : ex.InnerException.Message)); }
        }

        private static void AddComponent(BridgeRequest request)
        {
            var scene = EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Single);
            var matches = FindObjects(scene, request.objectName);
            if (matches.Count != 1) throw new InvalidOperationException("object_name must identify exactly one active scene object.");
            var type = ResolveComponentType(request.componentType);
            if (matches[0].GetComponent(type) != null) throw new InvalidOperationException("The object already has that component.");
            matches[0].AddComponent(type);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unity could not save the component change.");
        }

        private static string PrepareGeneratedAsset(BridgeRequest request)
        {
            var absoluteAsset = GeneratedAssetAbsolutePath(request.assetPath);
            var actualHash = Hex(SHA256.Create().ComputeHash(File.ReadAllBytes(absoluteAsset)));
            if (!FixedEquals(request.assetSha256, actualHash)) throw new InvalidOperationException("Generated GLB SHA-256 no longer matches the approved asset.");
            AssetDatabase.ImportAsset(request.assetPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.assetPath);
            if (prefab == null)
                throw new InvalidOperationException("Unity has no active GLB GameObject importer. Install the official com.unity.cloud.gltfast project package manually, then retry; ForgeCore does not install packages.");
            var scene = EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Single);
            var parents = FindObjects(scene, request.parentObjectName);
            if (parents.Count != 1) throw new InvalidOperationException("parent_object_name must identify exactly one active scene anchor.");
            if (FindObjects(scene, request.objectName).Count != 0) throw new InvalidOperationException("The prepared object name already exists; ForgeCore never overwrites scene objects.");
            var anchor = parents[0].transform;
            var priorChildren = new List<GameObject>();
            for (var i = 0; i < anchor.childCount; i++) priorChildren.Add(anchor.GetChild(i).gameObject);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) throw new InvalidOperationException("Unity could not instantiate the imported GLB as a scene object.");
            instance.name = request.objectName; instance.transform.SetParent(anchor, false);
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Imported GLB contains no renderable Unity objects."); }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.0001f) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Imported GLB has an invalid vertical size."); }
            var uniformScale = request.targetHeight / bounds.size.y;
            if (uniformScale < 0.001f || uniformScale > 1000f) { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("Required generated-asset scale is outside the safe range."); }
            instance.transform.localScale = Vector3.one * uniformScale;

            long vertices = 0; long triangles = 0; var colliderCount = 0;
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh; if (mesh == null) continue;
                vertices += mesh.vertexCount;
                for (var sub = 0; sub < mesh.subMeshCount; sub++) triangles += (long)mesh.GetIndexCount(sub) / 3L;
                if (request.colliderMode == "mesh")
                {
                    var collider = filter.gameObject.GetComponent<MeshCollider>();
                    if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh; collider.convex = false; colliderCount++;
                }
            }
            if (request.colliderMode == "box")
            {
                bounds = renderers[0].bounds; for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                var box = instance.AddComponent<BoxCollider>(); box.center = instance.transform.InverseTransformPoint(bounds.center);
                var scale = instance.transform.lossyScale; box.size = new Vector3(bounds.size.x / Mathf.Abs(scale.x), bounds.size.y / Mathf.Abs(scale.y), bounds.size.z / Mathf.Abs(scale.z)); colliderCount = 1;
            }
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true)) transform.gameObject.isStatic = true;
            if (request.replacePlaceholder)
                foreach (var child in priorChildren) if (!string.Equals(child.name, "Survey Marker", StringComparison.Ordinal)) child.SetActive(false);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unity could not save the prepared generated asset in the scene.");
            var materialCount = new HashSet<Material>(); foreach (var renderer in renderers) foreach (var material in renderer.sharedMaterials) if (material != null) materialCount.Add(material);
            return "Prepared generated asset '" + request.objectName + "' under '" + request.parentObjectName + "' in " + request.scenePath +
                "; target height=" + request.targetHeight.ToString("0.##") + "m; uniform scale=" + uniformScale.ToString("0.####") +
                "; renderers=" + renderers.Length + "; meshes=" + filters.Length + "; materials=" + materialCount.Count +
                "; vertices=" + vertices + "; triangles=" + triangles + "; " + request.colliderMode + " colliders=" + colliderCount +
                "; placeholder visuals hidden=" + request.replacePlaceholder + ". Visual, material, rights, and performance review remain required.";
        }

        private static BridgeArtInspection InspectGameArtAsset(BridgeRequest request)
        {
            var absoluteAsset = GeneratedAssetAbsolutePath(request.assetPath);
            var actualHash = Hex(SHA256.Create().ComputeHash(File.ReadAllBytes(absoluteAsset)));
            if (!FixedEquals(request.assetSha256, actualHash)) throw new InvalidOperationException("Generated GLB SHA-256 no longer matches the approved asset.");
            AssetDatabase.ImportAsset(request.assetPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.assetPath);
            if (prefab == null)
                throw new InvalidOperationException("Unity has no active GLB GameObject importer. ForgeCore does not install packages.");

            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Unity could not instantiate the imported GLB for isolated inspection.");
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Imported GLB contains no renderable Unity objects.");

                var meshes = new HashSet<Mesh>();
                var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var filters = instance.GetComponentsInChildren<MeshFilter>(true);
                foreach (var filter in filters) if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
                foreach (var renderer in skinned) if (renderer.sharedMesh != null) meshes.Add(renderer.sharedMesh);
                if (meshes.Count == 0) throw new InvalidOperationException("Imported GLB contains no Unity meshes.");

                long vertices = 0; long triangles = 0;
                foreach (var mesh in meshes)
                {
                    vertices += mesh.vertexCount;
                    for (var sub = 0; sub < mesh.subMeshCount; sub++) triangles += (long)mesh.GetIndexCount(sub) / 3L;
                }

                var materials = new HashSet<Material>(); var missingMaterials = 0;
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null) missingMaterials++; else materials.Add(material);
                var pbrMaterials = 0; var missingShaders = 0; var textures = new HashSet<Texture>();
                foreach (var material in materials)
                {
                    if (material.shader == null || !material.shader.isSupported) { missingShaders++; continue; }
                    if (IsPbrShader(material.shader.name)) pbrMaterials++;
                    var propertyCount = ShaderUtil.GetPropertyCount(material.shader);
                    for (var property = 0; property < propertyCount; property++)
                        if (ShaderUtil.GetPropertyType(material.shader, property) == ShaderUtil.ShaderPropertyType.TexEnv)
                        {
                            var texture = material.GetTexture(ShaderUtil.GetPropertyName(material.shader, property));
                            if (texture != null) textures.Add(texture);
                        }
                }
                long texturePixels = 0; long textureMemory = 0; var maximumTextureWidth = 0; var maximumTextureHeight = 0;
                foreach (var texture in textures)
                {
                    if (texture.width <= 0 || texture.height <= 0) throw new InvalidOperationException("Imported GLB contains a texture with invalid dimensions.");
                    var pixels = checked((long)texture.width * texture.height);
                    texturePixels = checked(texturePixels + pixels);
                    var runtimeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                    textureMemory = checked(textureMemory + Math.Max(runtimeBytes, checked(pixels * 4L)));
                    maximumTextureWidth = Math.Max(maximumTextureWidth, texture.width);
                    maximumTextureHeight = Math.Max(maximumTextureHeight, texture.height);
                }

                var animations = 0;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(request.assetPath))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) animations++;
                var lodGroups = instance.GetComponentsInChildren<LODGroup>(true); var lodLevels = 0; var hasLods = false;
                foreach (var group in lodGroups)
                {
                    var levels = group.GetLODs().Length; lodLevels += levels; if (levels >= 2) hasLods = true;
                }
                var colliders = instance.GetComponentsInChildren<Collider>(true).Length;
                var animators = instance.GetComponentsInChildren<Animator>(true); var avatars = 0;
                foreach (var animator in animators) if (animator.avatar != null && animator.avatar.isValid) avatars++;
                var rigged = skinned.Length > 0;
                foreach (var renderer in skinned)
                    if (renderer.sharedMesh == null || renderer.sharedMesh.bindposes.Length == 0 || renderer.bones == null || renderer.bones.Length == 0) rigged = false;

                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                if (!FinitePositive(bounds.size.x) || !FinitePositive(bounds.size.y) || !FinitePositive(bounds.size.z))
                    throw new InvalidOperationException("Imported GLB has invalid renderer bounds.");
                var root = instance.transform;
                var pivotBottomOffset = Mathf.Abs(root.position.y - bounds.min.y) / bounds.size.y;
                var horizontalOffset = Mathf.Sqrt(Mathf.Pow(root.position.x - bounds.center.x, 2f) + Mathf.Pow(root.position.z - bounds.center.z, 2f)) /
                    Mathf.Max(bounds.size.x, bounds.size.z);
                var rootScale = root.localScale;
                var largestScale = Mathf.Max(rootScale.x, Mathf.Max(rootScale.y, rootScale.z));
                var smallestScale = Mathf.Min(rootScale.x, Mathf.Min(rootScale.y, rootScale.z));
                var uniformRootScale = FinitePositive(rootScale.x) && FinitePositive(rootScale.y) && FinitePositive(rootScale.z) &&
                    largestScale - smallestScale <= largestScale * 0.001f;
                var rootRotationDegrees = Quaternion.Angle(root.localRotation, Quaternion.identity);
                var canonicalRootRotation = !float.IsNaN(rootRotationDegrees) && !float.IsInfinity(rootRotationDegrees) && rootRotationDegrees <= 0.5f;
                var groundedPivot = pivotBottomOffset <= 0.1f && horizontalOffset <= 0.25f;

                return new BridgeArtInspection
                {
                    assetPath = request.assetPath, assetSha256 = actualHash, assetSetId = request.assetSetId,
                    assetRole = request.assetRole, styleFamily = request.styleFamily,
                    rendererCount = renderers.Length, meshCount = meshes.Count, skinnedMeshRendererCount = skinned.Length,
                    animatorCount = animators.Length, avatarCount = avatars, materialCount = materials.Count,
                    pbrMaterialCount = pbrMaterials, textureCount = textures.Count, texturePixelCount = texturePixels,
                    estimatedTextureMemoryBytes = textureMemory, maximumTextureWidth = maximumTextureWidth, maximumTextureHeight = maximumTextureHeight,
                    missingMaterialCount = missingMaterials,
                    missingShaderCount = missingShaders, vertexCount = vertices, triangleCount = triangles,
                    animationClipCount = animations, lodGroupCount = lodGroups.Length, lodLevelCount = lodLevels,
                    colliderCount = colliders, boundsWidth = bounds.size.x, boundsHeight = bounds.size.y, boundsDepth = bounds.size.z,
                    pivotBottomOffsetNormalized = pivotBottomOffset, pivotHorizontalOffsetNormalized = horizontalOffset,
                    rootScaleX = rootScale.x, rootScaleY = rootScale.y, rootScaleZ = rootScale.z, rootRotationDegrees = rootRotationDegrees,
                    hasPbrMaterials = materials.Count > 0 && missingMaterials == 0 && missingShaders == 0 && pbrMaterials == materials.Count,
                    isRigged = rigged, hasLods = hasLods, hasCollision = colliders > 0,
                    hasGroundedPivot = groundedPivot, hasUniformRootScale = uniformRootScale, hasCanonicalRootRotation = canonicalRootRotation
                };
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static string CaptureGameArtPreview(BridgeRequest request)
        {
            var absoluteAsset = GeneratedAssetAbsolutePath(request.assetPath);
            var actualHash = Hex(SHA256.Create().ComputeHash(File.ReadAllBytes(absoluteAsset)));
            if (!FixedEquals(request.assetSha256, actualHash)) throw new InvalidOperationException("Generated GLB SHA-256 no longer matches the approved asset.");
            var output = PreviewAbsolutePath(request.outputPath);
            if (File.Exists(output) || Directory.Exists(output)) throw new InvalidOperationException("Game-art preview output already exists; ForgeCore never overwrites previews.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            AssetDatabase.ImportAsset(request.assetPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.assetPath);
            if (prefab == null) throw new InvalidOperationException("Unity has no active GLB GameObject importer. ForgeCore does not install packages.");

            // Batch mode starts in one empty, unsaved scene. Unity refuses to create another
            // unsaved scene additively, so require that initial scene to be empty and use it
            // only in memory. Every temporary object is destroyed and the scene is never saved.
            var previewScene = SceneManager.GetActiveScene();
            if (!previewScene.IsValid() || !string.IsNullOrEmpty(previewScene.path) || previewScene.GetRootGameObjects().Length != 0)
                throw new InvalidOperationException("Game-art preview requires Unity's empty unsaved batch scene.");
            GameObject instance = null;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            GameObject fillObject = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Unity could not instantiate the imported GLB for isolated preview rendering.");
                instance.SetActive(true);
                foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.SetActive(true);
                    transform.gameObject.layer = 30;
                }
                foreach (var nestedCamera in instance.GetComponentsInChildren<Camera>(true)) nestedCamera.enabled = false;
                foreach (var nestedLight in instance.GetComponentsInChildren<Light>(true)) nestedLight.enabled = false;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Imported GLB contains no renderable Unity objects.");
                var activeRenderers = 0;
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    renderer.forceRenderingOff = false;
                    if (renderer.gameObject.activeInHierarchy && renderer.enabled && !renderer.forceRenderingOff) activeRenderers++;
                }
                if (activeRenderers == 0) throw new InvalidOperationException("Imported GLB has no active renderers after isolated preview activation.");
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                if (!FinitePositive(bounds.size.x) || !FinitePositive(bounds.size.y) || !FinitePositive(bounds.size.z))
                    throw new InvalidOperationException("Imported GLB has invalid renderer bounds.");
                var largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                var uniformScale = 3f / largest;
                instance.transform.localScale = instance.transform.localScale * uniformScale;
                bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                instance.transform.position -= bounds.center;
                bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);

                cameraObject = new GameObject("ForgeCore Art Preview Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f, 1f); camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.01f; camera.farClipPlane = 100f; camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask = 1 << 30;
                keyObject = new GameObject("ForgeCore Art Preview Key"); SceneManager.MoveGameObjectToScene(keyObject, previewScene);
                var key = keyObject.AddComponent<Light>(); key.type = LightType.Directional; key.color = new Color(1f, 0.94f, 0.86f); key.intensity = 1.6f; key.cullingMask = 1 << 30;
                fillObject = new GameObject("ForgeCore Art Preview Fill"); SceneManager.MoveGameObjectToScene(fillObject, previewScene);
                var fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional; fill.color = new Color(0.55f, 0.70f, 1f); fill.intensity = 0.9f; fill.cullingMask = 1 << 30;

                const int tileWidth = 640; const int tileHeight = 360;
                var atlas = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var priorActive = RenderTexture.active;
                var priorAmbientMode = RenderSettings.ambientMode; var priorAmbientLight = RenderSettings.ambientLight;
                try
                {
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.38f, 1f);
                    var radius = Mathf.Max(5f, bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 1.3f);
                    camera.aspect = tileWidth / (float)tileHeight;
                    var angles = new float[] { 35f, 125f, 215f, 305f };
                    for (var view = 0; view < angles.Length; view++)
                    {
                        var rotation = Quaternion.Euler(18f, angles[view], 0f);
                        camera.transform.position = rotation * new Vector3(0f, 0f, -radius);
                        camera.transform.LookAt(Vector3.zero);
                        keyObject.transform.rotation = Quaternion.Euler(35f, angles[view] - 30f, 0f);
                        fillObject.transform.rotation = Quaternion.Euler(20f, angles[view] + 145f, 0f);
                        var render = new RenderTexture(tileWidth, tileHeight, 24, RenderTextureFormat.ARGB32);
                        var tile = new Texture2D(tileWidth, tileHeight, TextureFormat.RGB24, false);
                        var tilePriorActive = RenderTexture.active;
                        try
                        {
                            render.Create();
                            camera.targetTexture = render; RenderTexture.active = render; camera.Render();
                            tile.ReadPixels(new Rect(0, 0, tileWidth, tileHeight), 0, 0, false); tile.Apply(false, false);
                            atlas.SetPixels((view % 2) * tileWidth, (view / 2) * tileHeight, tileWidth, tileHeight, tile.GetPixels());
                        }
                        finally
                        {
                            camera.targetTexture = null; RenderTexture.active = tilePriorActive; render.Release(); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(render);
                        }
                    }
                    atlas.Apply(false, false);
                    var pixels = atlas.GetPixels32(); var step = Math.Max(1, pixels.Length / 16384); var sampled = 0; var magenta = 0;
                    var minimum = 1f; var maximum = 0f;
                    for (var index = 0; index < pixels.Length; index += step)
                    {
                        var pixel = pixels[index]; sampled++;
                        if (pixel.r >= 245 && pixel.g <= 20 && pixel.b >= 245) magenta++;
                        var luminance = (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;
                        if (luminance < minimum) minimum = luminance; if (luminance > maximum) maximum = luminance;
                    }
                    var magentaRatio = sampled == 0 ? 0f : magenta / (float)sampled;
                    if (magentaRatio > 0.2f) throw new InvalidOperationException("Game-art preview detected likely unsupported-shader magenta output.");
                    if (maximum - minimum < 0.08f) throw new InvalidOperationException("Game-art preview detected a blank or near-uniform review sheet (luminance " + minimum.ToString("0.000") + "-" + maximum.ToString("0.000") + "; active renderers " + activeRenderers + "; bounds " + bounds.size.ToString("F3") + ").");
                    File.WriteAllBytes(output, atlas.EncodeToPNG()); ValidatePreviewPng(output);
                    return "Captured non-overwriting four-view game-art preview for '" + request.assetSetId + "' at " + request.outputPath +
                        "; sampled=" + sampled + "; error-magenta=" + (magentaRatio * 100f).ToString("0.00") +
                        "%; luminance=" + minimum.ToString("0.000") + "-" + maximum.ToString("0.000") +
                        ". The preview remains unreviewed and grants no rights or quality acceptance.";
                }
                finally
                {
                    RenderSettings.ambientMode = priorAmbientMode; RenderSettings.ambientLight = priorAmbientLight;
                    RenderTexture.active = priorActive; UnityEngine.Object.DestroyImmediate(atlas);
                }
            }
            finally
            {
                if (fillObject != null) UnityEngine.Object.DestroyImmediate(fillObject);
                if (keyObject != null) UnityEngine.Object.DestroyImmediate(keyObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool IsPbrShader(string shaderName) =>
            shaderName == "Standard" || shaderName == "Universal Render Pipeline/Lit" || shaderName == "HDRP/Lit" ||
            shaderName.IndexOf("PBR", StringComparison.OrdinalIgnoreCase) >= 0 ||
            shaderName.IndexOf("MetallicRoughness", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0.0001f;

        private static void BuildWindows(BridgeRequest request)
        {
            var enabled = new List<string>();
            if (!string.IsNullOrEmpty(request.scenePath))
            {
                if (request.levelCount > 0)
                {
                    foreach (var path in CampaignScenePaths(request.scenePath, request.levelCount))
                    {
                        var campaignScene = AssetAbsolutePath(path);
                        if (!File.Exists(campaignScene)) throw new InvalidOperationException("Requested campaign build scene does not exist.");
                        enabled.Add(path);
                    }
                }
                else
                {
                    var requestedScene = AssetAbsolutePath(request.scenePath);
                    if (!File.Exists(requestedScene)) throw new InvalidOperationException("Requested build scene does not exist.");
                    enabled.Add(request.scenePath);
                }
            }
            else foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled && File.Exists(scene.path)) enabled.Add(scene.path);
            if (enabled.Count == 0) throw new InvalidOperationException("No enabled scenes are configured in Unity Build Settings.");
            var output = Path.GetFullPath(Path.Combine(ProjectRoot(), request.outputPath)); EnsureInsideProject(ProjectRoot(), output);
            var dataDirectory = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data");
            if (File.Exists(output) || Directory.Exists(output) || File.Exists(dataDirectory) || Directory.Exists(dataDirectory))
                throw new InvalidOperationException("Build output already exists; ForgeCore never overwrites builds.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = enabled.ToArray(), locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = request.developmentBuild ? BuildOptions.Development : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unity build result was " + report.summary.result + " with " + report.summary.totalErrors + " errors.");
        }

        private static List<string> CampaignScenePaths(string firstScenePath, int levelCount)
        {
            if (levelCount < 1 || levelCount > 12) throw new InvalidOperationException("Campaign build level count is invalid.");
            var normalized = (firstScenePath ?? "").Replace('\\', '/'); var slash = normalized.LastIndexOf('/');
            if (slash < 0) throw new InvalidOperationException("Campaign build scene path is invalid.");
            var directory = normalized.Substring(0, slash + 1); var file = normalized.Substring(slash + 1);
            const string suffix = "-Level01.unity";
            if (!file.EndsWith(suffix, StringComparison.Ordinal) || file.Length <= suffix.Length)
                throw new InvalidOperationException("Campaign build scene must end with -Level01.unity.");
            var stem = file.Substring(0, file.Length - "01.unity".Length); var paths = new List<string>();
            for (var index = 1; index <= levelCount; index++) paths.Add(directory + stem + index.ToString("00") + ".unity");
            return paths;
        }

        private static void StartPlayMode(BridgeRequest request, string resultPath)
        {
            EditorSceneManager.OpenScene(request.scenePath, OpenSceneMode.Single);
            if (request.operation == "capture_scene_preview")
            {
                var preview = PreviewAbsolutePath(request.outputPath);
                if (File.Exists(preview) || Directory.Exists(preview)) throw new InvalidOperationException("Preview output already exists; ForgeCore never overwrites previews.");
                Directory.CreateDirectory(Path.GetDirectoryName(preview));
                SessionState.SetString(PreviewOutputPath, preview); SessionState.SetBool(PreviewRequested, false);
                SessionState.EraseString(PreviewRequestedTicks); SessionState.EraseString(PreviewError); SessionState.EraseString(PreviewMetrics);
            }
            if (request.operation == "verify_gameplay")
            {
                SessionState.SetBool(GameplayExecuted, false); SessionState.EraseString(GameplayError); SessionState.EraseString(GameplaySummary);
            }
            if (request.operation == "profile_scene")
            {
                ResetPerformanceSampling();
                SessionState.EraseString(PerformanceError); SessionState.EraseString(PerformanceMetrics);
            }
            SessionState.SetBool(PlayPending, true); SessionState.SetString(PlayResultPath, resultPath);
            SessionState.SetString(PlayRequestId, request.requestId); SessionState.SetString(PlayOperation, request.operation);
            SessionState.SetString(PlayStartTicks, DateTime.UtcNow.Ticks.ToString()); SessionState.SetInt(PlayDuration, request.durationSeconds);
            EditorApplication.update -= MonitorPlayMode; EditorApplication.update += MonitorPlayMode; EditorApplication.isPlaying = true;
        }

        private static void MonitorPlayMode()
        {
            long start; if (!long.TryParse(SessionState.GetString(PlayStartTicks, "0"), out start)) start = DateTime.UtcNow.Ticks;
            var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - start).TotalSeconds;
            var duration = SessionState.GetInt(PlayDuration, 1);
            var operation = SessionState.GetString(PlayOperation, "play_mode");
            if (elapsed >= 1d && EditorApplication.isPlaying && operation == "profile_scene")
            {
                try { SamplePerformanceUpdate(); }
                catch (Exception ex)
                {
                    SessionState.SetString(PerformanceError, ex.GetType().Name + ": " + ex.Message);
                    EditorApplication.isPlaying = false; return;
                }
            }
            if (elapsed >= 1d && EditorApplication.isPlaying && operation == "verify_gameplay" && !SessionState.GetBool(GameplayExecuted, false))
            {
                SessionState.SetBool(GameplayExecuted, true);
                try { SessionState.SetString(GameplaySummary, RunGameplayVerification()); }
                catch (Exception ex) { SessionState.SetString(GameplayError, ex.GetType().Name + ": " + ex.Message); }
                EditorApplication.isPlaying = false; return;
            }
            if (elapsed >= duration && EditorApplication.isPlaying && operation == "capture_scene_preview")
            {
                var output = SessionState.GetString(PreviewOutputPath, "");
                try { SessionState.SetString(PreviewMetrics, CaptureCameraPreview(output)); }
                catch (Exception ex) { SessionState.SetString(PreviewError, ex.GetType().Name + ": " + ex.Message); }
                EditorApplication.isPlaying = false; return;
            }
            if (elapsed >= duration && EditorApplication.isPlaying && operation == "play_mode")
            {
                EditorApplication.isPlaying = false; return;
            }
            if (elapsed >= duration && EditorApplication.isPlaying && operation == "profile_scene")
            {
                try { SessionState.SetString(PerformanceMetrics, JsonUtility.ToJson(CompletePerformanceSampling())); }
                catch (Exception ex) { SessionState.SetString(PerformanceError, ex.GetType().Name + ": " + ex.Message); }
                EditorApplication.isPlaying = false; return;
            }
            if (!EditorApplication.isPlaying &&
                (elapsed >= duration || operation == "verify_gameplay" && SessionState.GetBool(GameplayExecuted, false) ||
                 operation == "profile_scene" && !string.IsNullOrEmpty(SessionState.GetString(PerformanceError, ""))))
            {
                var request = new BridgeRequest { requestId = SessionState.GetString(PlayRequestId, ""), operation = operation };
                try
                {
                    var summary = "Bounded Play Mode completed and exited after " + duration + " seconds.";
                    BridgePerformance performance = null;
                    if (operation == "capture_scene_preview")
                    {
                        var output = SessionState.GetString(PreviewOutputPath, "");
                        var error = SessionState.GetString(PreviewError, "");
                        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Runtime camera capture failed: " + error);
                        ValidatePreviewPng(output);
                        summary = "Captured a non-overwriting runtime scene preview at " + output + " after " + duration + " seconds. " + SessionState.GetString(PreviewMetrics, "");
                    }
                    else if (operation == "verify_gameplay")
                    {
                        var error = SessionState.GetString(GameplayError, "");
                        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Runtime gameplay verification failed: " + error);
                        summary = SessionState.GetString(GameplaySummary, "");
                        if (string.IsNullOrEmpty(summary)) throw new InvalidOperationException("Runtime gameplay verification returned no evidence.");
                    }
                    else if (operation == "profile_scene")
                    {
                        var error = SessionState.GetString(PerformanceError, "");
                        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Runtime performance sampling failed: " + error);
                        var metrics = SessionState.GetString(PerformanceMetrics, "");
                        if (string.IsNullOrEmpty(metrics)) throw new InvalidOperationException("Runtime performance sampling returned no evidence.");
                        performance = JsonUtility.FromJson<BridgePerformance>(metrics);
                        if (performance == null || performance.sampledUpdates < 10) throw new InvalidOperationException("Runtime performance sampling returned invalid evidence.");
                        summary = "Profiled the scene in Editor Play Mode for " + duration + " seconds across " + performance.sampledUpdates + " editor updates; p95 update interval=" + performance.p95UpdateMilliseconds.ToString("0.00") + " ms; max draw calls=" + performance.maximumDrawCalls + ". This is a repeatable editor baseline, not rendered-frame or target-hardware certification.";
                    }
                    WriteResult(SessionState.GetString(PlayResultPath, ""), request, true, summary, null, performance);
                }
                catch (Exception ex)
                {
                    WriteResult(SessionState.GetString(PlayResultPath, ""), request, false, ex.GetType().Name + ": " + ex.Message);
                }
                SessionState.EraseBool(PlayPending); SessionState.EraseString(PreviewOutputPath); SessionState.EraseBool(PreviewRequested);
                SessionState.EraseString(PreviewRequestedTicks); SessionState.EraseString(PreviewError); SessionState.EraseString(PreviewMetrics);
                SessionState.EraseBool(GameplayExecuted); SessionState.EraseString(GameplayError); SessionState.EraseString(GameplaySummary);
                SessionState.EraseString(PerformanceError); SessionState.EraseString(PerformanceMetrics); ResetPerformanceSampling();
                EditorApplication.update -= MonitorPlayMode; EditorApplication.Exit(0);
            }
        }

        private static void ResetPerformanceSampling()
        {
            PerformanceUpdateMilliseconds.Clear(); performanceLastRealtimeSeconds = -1d;
            performanceMaximumDrawCalls = 0; performanceMaximumSetPassCalls = 0;
            performanceMaximumTriangles = 0; performanceMaximumVertices = 0; performancePeakAllocatedMemoryBytes = 0;
        }

        private static void SamplePerformanceUpdate()
        {
            if (!Application.isPlaying) return;
            var realtimeSeconds = Time.realtimeSinceStartupAsDouble;
            if (double.IsNaN(realtimeSeconds) || double.IsInfinity(realtimeSeconds) || realtimeSeconds < 0d)
                throw new InvalidOperationException("Unity returned an invalid realtime clock value.");
            if (performanceLastRealtimeSeconds < 0d) { performanceLastRealtimeSeconds = realtimeSeconds; return; }
            var milliseconds = (realtimeSeconds - performanceLastRealtimeSeconds) * 1000d;
            performanceLastRealtimeSeconds = realtimeSeconds;
            if (milliseconds <= 0d) return;
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds > 10000d)
                throw new InvalidOperationException("Unity returned an invalid rendered-frame interval.");
            PerformanceUpdateMilliseconds.Add((float)milliseconds);
            performanceMaximumDrawCalls = Math.Max(performanceMaximumDrawCalls, UnityStats.drawCalls);
            performanceMaximumSetPassCalls = Math.Max(performanceMaximumSetPassCalls, UnityStats.setPassCalls);
            performanceMaximumTriangles = Math.Max(performanceMaximumTriangles, (long)UnityStats.triangles);
            performanceMaximumVertices = Math.Max(performanceMaximumVertices, (long)UnityStats.vertices);
            performancePeakAllocatedMemoryBytes = Math.Max(performancePeakAllocatedMemoryBytes, Profiler.GetTotalAllocatedMemoryLong());
        }

        private static BridgePerformance CompletePerformanceSampling()
        {
            if (PerformanceUpdateMilliseconds.Count < 10) throw new InvalidOperationException("Unity produced fewer than ten usable performance samples.");
            var ordered = new List<float>(PerformanceUpdateMilliseconds); ordered.Sort();
            double total = 0d; foreach (var sample in PerformanceUpdateMilliseconds) total += sample;
            var p95Index = Math.Max(0, Math.Min(ordered.Count - 1, (int)Math.Ceiling(ordered.Count * 0.95d) - 1));
            return new BridgePerformance
            {
                environment = "editor_play_mode", sampledUpdates = ordered.Count,
                averageUpdateMilliseconds = (float)(total / ordered.Count), p95UpdateMilliseconds = ordered[p95Index],
                maximumUpdateMilliseconds = ordered[ordered.Count - 1], maximumDrawCalls = performanceMaximumDrawCalls,
                maximumSetPassCalls = performanceMaximumSetPassCalls, maximumTriangles = performanceMaximumTriangles,
                maximumVertices = performanceMaximumVertices, peakAllocatedMemoryBytes = performancePeakAllocatedMemoryBytes
            };
        }

        private static string RunGameplayVerification()
        {
            Type gameType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var candidate = assembly.GetType("ForgeCoreGameTemplates.PrototypeGame", false, false);
                if (candidate == null) continue;
                if (gameType != null && gameType != candidate) throw new InvalidOperationException("PrototypeGame runtime type is ambiguous.");
                gameType = candidate;
            }
            if (gameType == null || !typeof(MonoBehaviour).IsAssignableFrom(gameType))
                throw new InvalidOperationException("The reviewed ForgeCore PrototypeGame runtime type was not found.");
            var instances = UnityEngine.Object.FindObjectsOfType(gameType);
            if (instances == null || instances.Length != 1) throw new InvalidOperationException("Runtime gameplay verification requires exactly one PrototypeGame instance.");
            var method = gameType.GetMethod("RunAutomatedGameplayCheck", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(string)) throw new InvalidOperationException("The reviewed runtime gameplay-check contract is unavailable.");
            try
            {
                return method.Invoke(instances[0], null) as string ?? throw new InvalidOperationException("Runtime gameplay check returned an invalid result.");
            }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }

        private static List<GameObject> FindObjects(Scene scene, string exactName)
        {
            var result = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects()) FindRecursive(root.transform, exactName, result);
            return result;
        }

        private static void FindRecursive(Transform value, string name, List<GameObject> result)
        {
            if (value.gameObject.name == name) result.Add(value.gameObject);
            for (var i = 0; i < value.childCount; i++) FindRecursive(value.GetChild(i), name, result);
        }

        private static Type ResolveComponentType(string name)
        {
            Type found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types; try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (var type in types)
                {
                    if (type == null || (!string.Equals(type.FullName, name, StringComparison.Ordinal) && !string.Equals(type.Name, name, StringComparison.Ordinal))) continue;
                    if (!typeof(Component).IsAssignableFrom(type) || type.IsAbstract || (type.Namespace ?? "").StartsWith("UnityEditor", StringComparison.Ordinal)) continue;
                    if (found != null && found != type) throw new InvalidOperationException("component_type is ambiguous; use its full namespace.");
                    found = type;
                }
            }
            if (found == null) throw new InvalidOperationException("A concrete runtime Component type was not found.");
            return found;
        }

        private static void ValidateRequest(BridgeRequest request)
        {
            if (request == null || request.protocolVersion != ProtocolVersion || string.IsNullOrEmpty(request.requestId)) throw new InvalidOperationException("Unity bridge protocol/request identifier is invalid.");
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "capabilities", "compile", "create_scene", "create_map_demo", "create_game_template", "create_game_campaign", "create_primitive", "add_component", "prepare_generated_asset", "inspect_game_art_asset", "capture_game_art_preview", "play_mode", "verify_gameplay", "profile_scene", "capture_scene_preview", "build_windows" };
            if (!allowed.Contains(request.operation)) throw new InvalidOperationException("Unity bridge operation is not allowed.");
            if (request.operation == "prepare_generated_asset" &&
                (request.targetHeight < 1f || request.targetHeight > 100f || (request.colliderMode != "mesh" && request.colliderMode != "box") ||
                 string.IsNullOrEmpty(request.parentObjectName) || string.IsNullOrEmpty(request.objectName)))
                throw new InvalidOperationException("Generated-asset preparation arguments are invalid.");
            if (request.operation == "inspect_game_art_asset")
            {
                var roles = new HashSet<string>(StringComparer.Ordinal) { "environment_architecture", "environment_props", "gameplay_objects", "characters", "character_animations", "weapons_and_tools", "vehicles", "effects", "user_interface" };
                var styles = new HashSet<string>(StringComparer.Ordinal) { "coherent_3d", "realistic_pbr", "stylized_3d", "illustrated_2d", "pixel_art_2d", "flexible_prototype" };
                if (string.IsNullOrEmpty(request.assetSetId) || !Regex.IsMatch(request.assetSetId, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant) ||
                    !roles.Contains(request.assetRole) || !styles.Contains(request.styleFamily) || string.IsNullOrEmpty(request.assetPath) ||
                    string.IsNullOrEmpty(request.assetSha256) || !Regex.IsMatch(request.assetSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
                    throw new InvalidOperationException("Game-art inspection arguments are invalid.");
            }
            if (request.operation == "capture_game_art_preview" &&
                (string.IsNullOrEmpty(request.assetSetId) || !Regex.IsMatch(request.assetSetId, "^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$", RegexOptions.CultureInvariant) ||
                 string.IsNullOrEmpty(request.assetPath) || string.IsNullOrEmpty(request.assetSha256) ||
                 !Regex.IsMatch(request.assetSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) || string.IsNullOrEmpty(request.outputPath)))
                throw new InvalidOperationException("Game-art preview arguments are invalid.");
            if (request.operation == "create_game_campaign" && (request.levelCount < 1 || request.levelCount > 12))
                throw new InvalidOperationException("Game campaign level count is invalid.");
            if (request.operation == "build_windows" && request.levelCount != 0 && (request.levelCount < 1 || request.levelCount > 12 || string.IsNullOrEmpty(request.scenePath)))
                throw new InvalidOperationException("Campaign build arguments are invalid.");
            if (request.operation == "capture_scene_preview" && (request.durationSeconds < 1 || request.durationSeconds > 30 || string.IsNullOrEmpty(request.scenePath) || string.IsNullOrEmpty(request.outputPath)))
                throw new InvalidOperationException("Scene-preview arguments are invalid.");
            if (request.operation == "verify_gameplay" && (request.durationSeconds < 2 || request.durationSeconds > 30 || string.IsNullOrEmpty(request.scenePath)))
                throw new InvalidOperationException("Runtime gameplay-verification arguments are invalid.");
            if (request.operation == "profile_scene" && (request.durationSeconds < 5 || request.durationSeconds > 30 || string.IsNullOrEmpty(request.scenePath)))
                throw new InvalidOperationException("Runtime scene-profile arguments are invalid.");
        }

        private static string AssetAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) || !assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || assetPath.Contains(".."))
                throw new InvalidOperationException("Unsafe Unity scene path.");
            var result = Path.GetFullPath(Path.Combine(ProjectRoot(), assetPath)); EnsureInsideProject(ProjectRoot(), result); return result;
        }

        private static string PreviewAbsolutePath(string outputPath)
        {
            var normalized = (outputPath ?? "").Replace('\\', '/');
            if (!normalized.StartsWith("Previews/", StringComparison.Ordinal) || !normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || normalized.Contains(".."))
                throw new InvalidOperationException("Unsafe scene-preview output path.");
            var result = Path.GetFullPath(Path.Combine(ProjectRoot(), normalized)); EnsureInsideProject(ProjectRoot(), result); return result;
        }

        private static void ValidatePreviewPng(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new InvalidOperationException("Unity did not create the requested scene preview.");
            var info = new FileInfo(path); if (info.Length < 24 || info.Length > 33554432) throw new InvalidOperationException("Scene preview has an invalid byte length.");
            var bytes = new byte[24]; using (var stream = File.OpenRead(path)) if (stream.Read(bytes, 0, bytes.Length) != bytes.Length) throw new InvalidOperationException("Scene preview header is truncated.");
            var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (var i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) throw new InvalidOperationException("Scene preview is not a PNG.");
            if (bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82) throw new InvalidOperationException("Scene preview has no PNG IHDR header.");
            var width = ReadBigEndianInt(bytes, 16); var height = ReadBigEndianInt(bytes, 20);
            if (width < 320 || height < 180 || width > 7680 || height > 4320) throw new InvalidOperationException("Scene preview dimensions are outside the supported range.");
        }

        private static string CaptureCameraPreview(string path)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameras = Camera.allCameras;
                for (var i = 0; i < cameras.Length; i++) if (cameras[i] != null && cameras[i].enabled) { camera = cameras[i]; break; }
            }
            if (camera == null) throw new InvalidOperationException("The running scene has no enabled camera.");
            var priorTarget = camera.targetTexture; var priorActive = RenderTexture.active;
            var render = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render; RenderTexture.active = render; camera.Render();
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0, false); texture.Apply(false, false);
                var pixels = texture.GetPixels32(); var step = Math.Max(1, pixels.Length / 16384); var sampled = 0; var magenta = 0;
                var minimum = 1f; var maximum = 0f;
                for (var i = 0; i < pixels.Length; i += step)
                {
                    var pixel = pixels[i]; sampled++;
                    if (pixel.r >= 245 && pixel.g <= 20 && pixel.b >= 245) magenta++;
                    var luminance = (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;
                    if (luminance < minimum) minimum = luminance; if (luminance > maximum) maximum = luminance;
                }
                var magentaRatio = sampled == 0 ? 0f : magenta / (float)sampled;
                if (magentaRatio > 0.2f) throw new InvalidOperationException("Visual QA detected a likely unsupported-shader magenta frame.");
                if (maximum - minimum < 0.08f) throw new InvalidOperationException("Visual QA detected a blank or near-uniform frame.");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return "Visual QA passed: sampled " + sampled + " pixels, error-magenta " + (magentaRatio * 100f).ToString("0.00") + "%, luminance range " + minimum.ToString("0.000") + "-" + maximum.ToString("0.000") + ".";
            }
            finally
            {
                camera.targetTexture = priorTarget; RenderTexture.active = priorActive; render.Release();
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(render);
            }
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset) =>
            (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        private static string GeneratedAssetAbsolutePath(string assetPath)
        {
            var normalized = (assetPath ?? "").Replace('\\', '/');
            if (!normalized.StartsWith("Assets/ForgeCore/Generated/", StringComparison.Ordinal) || !normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || normalized.Contains(".."))
                throw new InvalidOperationException("Unsafe generated GLB asset path.");
            var result = Path.GetFullPath(Path.Combine(ProjectRoot(), normalized)); EnsureInsideProject(ProjectRoot(), result);
            if (!File.Exists(result)) throw new FileNotFoundException("Generated GLB asset does not exist.");
            return result;
        }

        private static string ProjectRoot() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        private static void EnsureInsideProject(string projectRoot, string candidate)
        {
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(candidate);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bridge path escapes the Unity project.");
        }

        private static string CommandLineValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++) if (args[i] == name && !string.IsNullOrEmpty(args[i + 1])) return args[i + 1];
            throw new InvalidOperationException("Missing command-line argument " + name + ".");
        }

        private static void WriteResult(string path, BridgeRequest request, bool success, string summary, BridgeArtInspection artInspection = null, BridgePerformance performance = null)
        {
            if (string.IsNullOrEmpty(path)) return;
            var result = new BridgeResult { requestId = request == null ? "unknown" : request.requestId, operation = request == null ? "unknown" : request.operation, success = success, summary = summary, artInspection = artInspection, performance = performance };
            var temporary = path + ".tmp"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temporary, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            if (File.Exists(path)) throw new InvalidOperationException("Bridge result already exists.");
            File.Move(temporary, path);
        }

        private static string Hex(byte[] value) { var text = new StringBuilder(value.Length * 2); foreach (var b in value) text.Append(b.ToString("x2")); return text.ToString(); }
        private static bool FixedEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0;
        }
    }
}
#endif
""";
}
