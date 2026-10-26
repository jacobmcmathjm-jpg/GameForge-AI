using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ForgeCore.Tools;

internal static class UnityMapDemoTemplate
{
    public const string Version = "0.11.3";
    private const string Root = "Assets/ForgeCoreMapDemo";
    private const string ManifestName = "forgecore-map-demo.manifest.json";

    private static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Runtime/FrontierPlayer.cs"] = FrontierPlayer,
        ["Runtime/FrontierCamera.cs"] = FrontierCamera,
        ["Runtime/FrontierLandmark.cs"] = FrontierLandmark,
        ["Runtime/FrontierGame.cs"] = FrontierGame,
        ["Runtime/FrontierDayNight.cs"] = FrontierDayNight,
        ["Runtime/ForgeCoreMapDemo.Runtime.asmdef"] = RuntimeAssembly,
        ["Editor/FrontierValleyBuilder.cs"] = FrontierValleyBuilder
    };

    public static async Task<string> InstallAsync(string projectRoot, CancellationToken token)
    {
        var root = Path.Combine(projectRoot, Root.Replace('/', Path.DirectorySeparatorChar));
        var manifest = Path.Combine(root, ManifestName);
        if (Directory.Exists(root))
        {
            if (await InstallationMatchesAsync(projectRoot, token))
                return $"Frontier Valley map kit {Version} is already installed and hash-verified at {root}.";
            throw new InvalidOperationException("A ForgeCore map-demo directory already exists and does not exactly match this release; it was not overwritten.");
        }

        var created = new List<string>();
        try
        {
            foreach (var (relative, content) in Files)
            {
                token.ThrowIfCancellationRequested();
                var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, content, new UTF8Encoding(false), token);
                created.Add(target);
            }
            var manifestText = JsonSerializer.Serialize(new
            {
                version = Version,
                template = "frontier_valley",
                files = Files.Select(x => new { path = x.Key, sha256 = Hash(x.Value) }).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifest, manifestText, new UTF8Encoding(false), token);
            created.Add(manifest);
            return $"Installed hash-verified Frontier Valley map kit {Version} with {Files.Count} source files at {root}.";
        }
        catch
        {
            foreach (var file in created.AsEnumerable().Reverse()) if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Any()) Directory.Delete(root, true);
            throw;
        }
    }

    public static async Task<bool> InstallationMatchesAsync(string projectRoot, CancellationToken token)
    {
        var root = Path.Combine(projectRoot, Root.Replace('/', Path.DirectorySeparatorChar));
        var manifestPath = Path.Combine(root, ManifestName);
        if (!File.Exists(manifestPath)) return false;
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, token));
            var value = document.RootElement;
            if (value.GetProperty("version").GetString() != Version || value.GetProperty("template").GetString() != "frontier_valley") return false;
            var listed = value.GetProperty("files").EnumerateArray().ToDictionary(
                x => x.GetProperty("path").GetString()!, x => x.GetProperty("sha256").GetString()!, StringComparer.Ordinal);
            if (listed.Count != Files.Count) return false;
            foreach (var (relative, expectedContent) in Files)
            {
                token.ThrowIfCancellationRequested();
                if (!listed.TryGetValue(relative, out var listedHash) || !string.Equals(listedHash, Hash(expectedContent), StringComparison.Ordinal)) return false;
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return false;
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(listedHash))) return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private const string FrontierPlayer = """
using UnityEngine;

namespace ForgeCoreMapDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FrontierPlayer : MonoBehaviour
    {
        public float walkSpeed = 7f;
        public float sprintSpeed = 11f;
        public float acceleration = 12f;
        public float turnSpeed = 12f;
        public float jumpHeight = 1.8f;
        public float gravity = -24f;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        private void Awake() { controller = GetComponent<CharacterController>(); }

        private void Update()
        {
            if (FrontierGame.Instance != null && !FrontierGame.Instance.PlayerCanMove) return;
            var x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            var z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            var input = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
            var camera = Camera.main;
            var forward = camera == null ? Vector3.forward : Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            var right = camera == null ? Vector3.right : Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            var desiredDirection = (forward * input.y + right * input.x).normalized;
            var speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? sprintSpeed : walkSpeed;
            var desiredVelocity = desiredDirection * speed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (Input.GetKeyDown(KeyCode.Space)) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if (desiredDirection.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredDirection), turnSpeed * Time.deltaTime);
            if (transform.position.y < -20f && FrontierGame.Instance != null) FrontierGame.Instance.Respawn();
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false; transform.position = position; controller.enabled = true;
            planarVelocity = Vector3.zero; verticalVelocity = 0f;
        }
    }
}
""";

    private const string FrontierCamera = """
using UnityEngine;

namespace ForgeCoreMapDemo
{
    public sealed class FrontierCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 8f, -13f);
        public float smoothTime = 0.16f;
        public float orbitSpeed = 60f;
        private Vector3 velocity;
        private float yaw;

        private void LateUpdate()
        {
            if (target == null) return;
            var direction = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            yaw += direction * orbitSpeed * Time.deltaTime;
            var desired = target.position + Quaternion.Euler(0f, yaw, 0f) * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * 1.4f);
        }
    }

    public sealed class FrontierMinimap : MonoBehaviour
    {
        public Transform target;
        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + Vector3.up * 85f;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
""";

    private const string FrontierLandmark = """
using UnityEngine;

namespace ForgeCoreMapDemo
{
    public sealed class FrontierLandmark : MonoBehaviour
    {
        public string displayName = "Landmark";
        public Transform marker;
        public bool Discovered { get; private set; }
        private Vector3 markerBase;

        private void Start() { if (marker != null) markerBase = marker.localPosition; }
        private void Update()
        {
            if (marker == null) return;
            marker.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
            marker.localPosition = markerBase + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.18f);
        }
        private void OnTriggerEnter(Collider other)
        {
            if (Discovered || other.GetComponent<FrontierPlayer>() == null) return;
            Discovered = true;
            if (marker != null) marker.gameObject.SetActive(false);
            if (FrontierGame.Instance != null) FrontierGame.Instance.Discover(this);
        }
    }
}
""";

    private const string FrontierGame = """
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeCoreMapDemo
{
    public sealed class FrontierGame : MonoBehaviour
    {
        public static FrontierGame Instance { get; private set; }
        public FrontierPlayer player;
        public Transform spawnPoint;
        public string mapName = "FRONTIER VALLEY";
        public int mapSeed;
        public bool PlayerCanMove => !completed;

        private readonly List<FrontierLandmark> landmarks = new List<FrontierLandmark>();
        private int discovered;
        private float started;
        private bool completed;
        private string latest = "Follow the trail and discover every landmark.";
        private AudioSource audioSource;

        private void Awake() { Instance = this; audioSource = gameObject.AddComponent<AudioSource>(); }
        private void Start()
        {
            landmarks.AddRange(FindObjectsByType<FrontierLandmark>());
            started = Time.time; PlayTone(320f, 0.10f);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        public void Discover(FrontierLandmark landmark)
        {
            discovered++; latest = "Discovered: " + landmark.displayName; PlayTone(520f + discovered * 70f, 0.16f);
            if (discovered >= landmarks.Count)
            {
                completed = true; latest = "VALLEY SURVEY COMPLETE"; PlayTone(880f, 0.35f);
            }
        }
        public void Respawn()
        {
            if (player != null && spawnPoint != null) player.Teleport(spawnPoint.position);
            latest = "Returned to the trailhead.";
        }
        private void OnGUI()
        {
            var scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.25f);
            var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * scale), fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.93f, 0.96f, 0.88f) } };
            var body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * scale), normal = { textColor = new Color(0.82f, 0.88f, 0.78f) } };
            var panel = new Rect(24, 22, 410 * scale, 138 * scale);
            DrawPanel(panel, new Color(0.035f, 0.055f, 0.045f, 0.90f));
            GUI.Label(new Rect(panel.x + 18, panel.y + 12, panel.width - 30, 34), mapName, title);
            GUI.Label(new Rect(panel.x + 18, panel.y + 50, panel.width - 30, 25), "LANDMARKS  " + discovered + " / " + landmarks.Count + "     SEED  " + mapSeed, body);
            GUI.Label(new Rect(panel.x + 18, panel.y + 77, panel.width - 30, 25), latest, body);
            GUI.Label(new Rect(panel.x + 18, panel.y + 104, panel.width - 30, 25), "WASD move   SHIFT sprint   SPACE jump   Q/E camera   R restart", body);
            if (!completed) return;
            var complete = new Rect(Screen.width * 0.5f - 230 * scale, Screen.height * 0.5f - 70 * scale, 460 * scale, 140 * scale);
            DrawPanel(complete, new Color(0.04f, 0.08f, 0.055f, 0.94f));
            GUI.Label(new Rect(complete.x + 34, complete.y + 24, complete.width - 68, 38), "VALLEY SURVEY COMPLETE", title);
            GUI.Label(new Rect(complete.x + 34, complete.y + 70, complete.width - 68, 30), "Time " + (Time.time - started).ToString("0.0") + "s   •   Press R to explore again", body);
        }
        private static void DrawPanel(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private void PlayTone(float frequency, float duration)
        {
            var sampleRate = 22050; var samples = Mathf.CeilToInt(sampleRate * duration); var data = new float[samples];
            for (var i = 0; i < samples; i++) data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * (1f - i / (float)samples) * 0.12f;
            var clip = AudioClip.Create("FrontierTone", samples, 1, sampleRate, false); clip.SetData(data, 0); audioSource.PlayOneShot(clip);
        }
    }
}
""";

    private const string FrontierDayNight = """
using UnityEngine;

namespace ForgeCoreMapDemo
{
    public sealed class FrontierDayNight : MonoBehaviour
    {
        public Light sun;
        public float fullCycleSeconds = 420f;
        private void Update()
        {
            if (sun == null) return;
            sun.transform.Rotate(Vector3.right, 360f / fullCycleSeconds * Time.deltaTime, Space.Self);
            var daylight = Mathf.Clamp01(Vector3.Dot(-sun.transform.forward, Vector3.up) * 1.5f + 0.2f);
            sun.intensity = Mathf.Lerp(0.25f, 1.25f, daylight);
            RenderSettings.fogColor = Color.Lerp(new Color(0.08f, 0.12f, 0.17f), new Color(0.52f, 0.64f, 0.67f), daylight);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.05f, 0.07f, 0.12f), new Color(0.42f, 0.46f, 0.40f), daylight);
        }
    }
}
""";

    private const string RuntimeAssembly = """
{
  "name": "ForgeCoreMapDemo.Runtime",
  "rootNamespace": "ForgeCoreMapDemo",
  "references": [],
  "autoReferenced": true,
  "overrideReferences": false,
  "noEngineReferences": false
}
""";

    private const string FrontierValleyBuilder = """
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ForgeCoreMapDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ForgeCoreMapDemo.Editor
{
    public static class FrontierValleyBuilder
    {
        private const string Generated = "Assets/ForgeCoreMapDemo/Generated";
        private const float MapSize = 512f;
        private static System.Random random;

        private sealed class MapSurface
        {
            private readonly int seed;

            public MapSurface(int seedValue) { seed = seedValue; }

            public float Height(float worldX, float worldZ)
            {
                return HeightNormalized(worldX / MapSize, worldZ / MapSize) * 72f;
            }

            public float HeightNormalized(float nx, float nz)
            {
                nx = Mathf.Clamp01(nx); nz = Mathf.Clamp01(nz);
                var value = RawHeight(nx, nz);
                var spawn = new Vector2(0.14f, 0.14f);
                var distance = Vector2.Distance(new Vector2(nx, nz), spawn);
                var blend = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / 0.075f));
                return Mathf.Lerp(value, RawHeight(spawn.x, spawn.y), blend);
            }

            public float Slope(float nx, float nz)
            {
                const float step = 1f / 128f;
                var dx = (HeightNormalized(nx + step, nz) - HeightNormalized(nx - step, nz)) * 72f / (step * 2f * MapSize);
                var dz = (HeightNormalized(nx, nz + step) - HeightNormalized(nx, nz - step)) * 72f / (step * 2f * MapSize);
                return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
            }

            private float RawHeight(float nx, float nz)
            {
                var broad = Mathf.PerlinNoise(seed * 0.0013f + nx * 2.3f, seed * 0.0021f + nz * 2.3f) * 0.34f;
                var ridges = Mathf.Abs(Mathf.PerlinNoise(seed * 0.0037f + nx * 5.1f, seed * 0.0041f + nz * 5.1f) - 0.5f) * 0.25f;
                var detail = Mathf.PerlinNoise(seed * 0.0083f + nx * 13f, seed * 0.0067f + nz * 13f) * 0.055f;
                var river = Mathf.Clamp01(Mathf.Abs(nx - RiverX(nz)) / 0.055f);
                var edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(nz, 1f - nz)) * 6f);
                return Mathf.Clamp01((0.075f + broad + ridges + detail) * (0.68f + edge * 0.32f) * Mathf.Lerp(0.48f, 1f, river));
            }
        }

        public static string Build(string scenePath, int seed)
        {
            ValidateScenePath(scenePath);
            if (seed < 1 || seed > 999999) throw new InvalidOperationException("Seed is outside the approved range.");
            if (File.Exists(Absolute(scenePath)) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) throw new InvalidOperationException("Map scene already exists.");
            if (AssetDatabase.IsValidFolder(Generated)) throw new InvalidOperationException("Generated map assets already exist; ForgeCore never overwrites them.");
            EnableBuiltInInputForPortableControls();
            random = new System.Random(seed);
            try
            {
                EnsureFolder("Assets/ForgeCoreMapDemo", "Generated");
                EnsureFolder(Generated, "Materials"); EnsureFolder(Generated, "Textures"); EnsureFolder(Generated, "Prefabs"); EnsureFolder(Generated, "Profiles");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UseBuiltInRenderPipeline();
                var materials = CreateMaterials(seed);
                var terrain = CreateTerrain(seed, materials);
                CreateWater(materials, terrain);
                CreateVegetation(terrain, materials);
                var landmarks = CreateLandmarks(terrain, materials);
                CreatePlayerAndCameras(terrain, materials, landmarks, seed);
                CreateLightingAndPost(materials);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.0022f;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.38f, 0.43f, 0.37f);
                Directory.CreateDirectory(Path.GetDirectoryName(Absolute(scenePath)));
                if (!EditorSceneManager.SaveScene(scene, scenePath, false)) throw new InvalidOperationException("Unity could not save the Frontier Valley scene.");
                var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(scenePath, true) };
                foreach (var item in EditorBuildSettings.scenes) if (!string.Equals(item.path, scenePath, StringComparison.Ordinal)) scenes.Add(new EditorBuildSettingsScene(item.path, false));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return "Created Frontier Valley map at " + scenePath + " with a seeded mesh landscape, trail network, river, seeded forest, five landmark objectives, third-person controls, minimap, day/night lighting, procedural materials, and build registration. Seed=" + seed + ".";
            }
            catch
            {
                if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) AssetDatabase.DeleteAsset(scenePath);
                AssetDatabase.Refresh(); throw;
            }
        }

        private static void UseBuiltInRenderPipeline()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            var originalQuality = QualitySettings.GetQualityLevel();
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);
        }

        private static void EnableBuiltInInputForPortableControls()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0) throw new InvalidOperationException("Unity PlayerSettings asset was not found.");
            var serialized = new SerializedObject(settings[0]);
            var activeInputHandler = serialized.FindProperty("activeInputHandler");
            if (activeInputHandler == null) throw new InvalidOperationException("Unity activeInputHandler setting was not found.");
            if (activeInputHandler.intValue == 1)
            {
                activeInputHandler.intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        private static Dictionary<string, Material> CreateMaterials(int seed)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            result["grass"] = Material("Grass", new Color(0.20f, 0.34f, 0.16f), 0f, 0.18f);
            result["dirt"] = Material("Dirt", new Color(0.32f, 0.23f, 0.14f), 0f, 0.1f);
            result["rock"] = Material("Rock", new Color(0.30f, 0.32f, 0.30f), 0.05f, 0.32f);
            result["wood"] = Material("Wood", new Color(0.23f, 0.13f, 0.07f), 0f, 0.15f);
            result["leaf"] = Material("Leaf", new Color(0.10f, 0.28f, 0.12f), 0f, 0.12f);
            result["water"] = Material("Water", new Color(0.05f, 0.30f, 0.42f), 0.05f, 0.92f);
            result["cloth"] = Material("Cloth", new Color(0.46f, 0.18f, 0.08f), 0f, 0.16f);
            result["marker"] = Material("Marker", new Color(0.88f, 0.61f, 0.18f), 0.1f, 0.65f, new Color(1.2f, 0.55f, 0.08f));
            result["player"] = Material("Player", new Color(0.18f, 0.31f, 0.36f), 0.15f, 0.55f);
            CreateTerrainTexture("GrassTex", new Color(0.16f, 0.31f, 0.12f), seed + 11);
            CreateTerrainTexture("DirtTex", new Color(0.34f, 0.23f, 0.12f), seed + 29);
            CreateTerrainTexture("RockTex", new Color(0.31f, 0.33f, 0.32f), seed + 47);
            return result;
        }

        private static MapSurface CreateTerrain(int seed, Dictionary<string, Material> materials)
        {
            const int resolution = 129;
            var surface = new MapSurface(seed);
            var vertices = new Vector3[resolution * resolution];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            for (var z = 0; z < resolution; z++) for (var x = 0; x < resolution; x++)
            {
                var nx = x / (float)(resolution - 1); var nz = z / (float)(resolution - 1); var index = z * resolution + x;
                vertices[index] = new Vector3(nx * MapSize, surface.Height(nx * MapSize, nz * MapSize), nz * MapSize);
                uv[index] = new Vector2(nx * 32f, nz * 32f);
            }
            var triangle = 0;
            for (var z = 0; z < resolution - 1; z++) for (var x = 0; x < resolution - 1; x++)
            {
                var a = z * resolution + x; var b = a + 1; var c = a + resolution; var d = c + 1;
                triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = b;
                triangles[triangle++] = b; triangles[triangle++] = c; triangles[triangle++] = d;
            }
            var mesh = new Mesh { name = "Frontier Valley Ground" }; mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Generated + "/FrontierGround.asset");
            var ground = new GameObject("Frontier Valley Landscape"); ground.AddComponent<MeshFilter>().sharedMesh = mesh; ground.AddComponent<MeshRenderer>().sharedMaterial = materials["grass"]; ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            CreateTrails(surface, materials);
            return surface;
        }

        private static void CreateTrails(MapSurface surface, Dictionary<string, Material> materials)
        {
            var root = new GameObject("Trail Network");
            var points = new[] { new Vector2(0.14f, 0.14f), new Vector2(0.28f, 0.28f), new Vector2(0.49f, 0.47f), new Vector2(0.70f, 0.36f), new Vector2(0.62f, 0.72f), new Vector2(0.83f, 0.86f) };
            for (var i = 0; i + 1 < points.Length; i++)
            {
                var p0 = new Vector3(points[i].x * MapSize, 0f, points[i].y * MapSize); var p1 = new Vector3(points[i + 1].x * MapSize, 0f, points[i + 1].y * MapSize);
                p0.y = surface.Height(p0.x, p0.z) + 0.18f; p1.y = surface.Height(p1.x, p1.z) + 0.18f;
                var segment = Primitive(PrimitiveType.Cube, "Trail " + (i + 1), root.transform, (p0 + p1) * 0.5f, new Vector3(7.5f, 0.16f, Vector3.Distance(p0, p1)), materials["dirt"]);
                segment.transform.rotation = Quaternion.LookRotation(p1 - p0); Object.DestroyImmediate(segment.GetComponent<Collider>());
            }
        }

        private static void CreateWater(Dictionary<string, Material> materials, MapSurface surface)
        {
            var root = new GameObject("River System");
            for (var i = 0; i < 12; i++)
            {
                var a = i / 12f; var b = (i + 1) / 12f; var p0 = new Vector3(RiverX(a) * MapSize, 0f, a * MapSize); var p1 = new Vector3(RiverX(b) * MapSize, 0f, b * MapSize);
                p0.y = surface.Height(p0.x, p0.z) + 0.22f; p1.y = surface.Height(p1.x, p1.z) + 0.22f;
                var segment = Primitive(PrimitiveType.Cube, "River " + (i + 1), root.transform, (p0 + p1) * 0.5f, new Vector3(23f, 0.22f, Vector3.Distance(p0, p1) + 2f), materials["water"]);
                segment.transform.rotation = Quaternion.LookRotation(p1 - p0); Object.DestroyImmediate(segment.GetComponent<Collider>());
            }
        }

        private static void CreateVegetation(MapSurface surface, Dictionary<string, Material> materials)
        {
            var tree = new GameObject("Frontier Pine");
            Primitive(PrimitiveType.Cylinder, "Trunk", tree.transform, new Vector3(0, 1.5f, 0), new Vector3(0.42f, 1.5f, 0.42f), materials["wood"]);
            Primitive(PrimitiveType.Sphere, "Crown Lower", tree.transform, new Vector3(0, 3.2f, 0), new Vector3(2.3f, 1.65f, 2.3f), materials["leaf"]);
            Primitive(PrimitiveType.Sphere, "Crown Upper", tree.transform, new Vector3(0, 4.6f, 0), new Vector3(1.55f, 1.45f, 1.55f), materials["leaf"]);
            foreach (var collider in tree.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var treePrefab = PrefabUtility.SaveAsPrefabAsset(tree, Generated + "/Prefabs/FrontierPine.prefab"); Object.DestroyImmediate(tree);
            var forest = new GameObject("Pine Forest");
            for (var i = 0; i < 260; i++)
            {
                var x = Next(0.035f, 0.965f); var z = Next(0.035f, 0.965f);
                if (TrailWeight(new Vector2(x, z)) > 0.18f || Mathf.Abs(x - RiverX(z)) < 0.065f || surface.Slope(x, z) > 31f) continue;
                var scale = Next(0.72f, 1.38f);
                var world = new Vector3(x * MapSize, 0f, z * MapSize); world.y = surface.Height(world.x, world.z);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab); instance.name = "Pine"; instance.transform.SetParent(forest.transform);
                instance.transform.position = world; instance.transform.localScale = Vector3.one * scale; instance.transform.rotation = Quaternion.Euler(0f, Next(0f, 360f), 0f);
            }
            var rocks = new GameObject("Rock Clusters");
            for (var i = 0; i < 65; i++)
            {
                var x = Next(18f, MapSize - 18f); var z = Next(18f, MapSize - 18f); var nx = x / MapSize; var nz = z / MapSize;
                if (TrailWeight(new Vector2(nx, nz)) > 0.25f || Mathf.Abs(nx - RiverX(nz)) < 0.055f) continue;
                var y = surface.Height(x, z); var rock = Primitive(PrimitiveType.Sphere, "Boulder", rocks.transform, new Vector3(x, y + 0.6f, z), new Vector3(Next(1.0f, 3.2f), Next(0.7f, 1.8f), Next(1.0f, 2.8f)), materials["rock"]);
                rock.transform.rotation = Quaternion.Euler(Next(-15, 15), Next(0, 360), Next(-15, 15));
            }
        }

        private static List<FrontierLandmark> CreateLandmarks(MapSurface surface, Dictionary<string, Material> materials)
        {
            var list = new List<FrontierLandmark>();
            list.Add(Landmark("Forest Watchtower", surface, materials, new Vector2(0.28f, 0.28f), BuildWatchtower));
            list.Add(Landmark("Old River Bridge", surface, materials, new Vector2(RiverX(0.47f), 0.47f), BuildBridge));
            list.Add(Landmark("Stone Arch", surface, materials, new Vector2(0.70f, 0.36f), BuildArch));
            list.Add(Landmark("Ranger Camp", surface, materials, new Vector2(0.62f, 0.72f), BuildCamp));
            list.Add(Landmark("North Summit", surface, materials, new Vector2(0.83f, 0.86f), BuildSummit));
            return list;
        }

        private static FrontierLandmark Landmark(string name, MapSurface surface, Dictionary<string, Material> materials, Vector2 normalized, Action<Transform, Dictionary<string, Material>> build)
        {
            var root = new GameObject(name); var world = new Vector3(normalized.x * MapSize, 0, normalized.y * MapSize); world.y = surface.Height(world.x, world.z); root.transform.position = world;
            build(root.transform, materials);
            var trigger = root.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 11f;
            var landmark = root.AddComponent<FrontierLandmark>(); landmark.displayName = name;
            var marker = Primitive(PrimitiveType.Cylinder, "Survey Marker", root.transform, new Vector3(0, 8f, 0), new Vector3(0.34f, 1.8f, 0.34f), materials["marker"]); Object.DestroyImmediate(marker.GetComponent<Collider>()); landmark.marker = marker.transform;
            return landmark;
        }

        private static void BuildWatchtower(Transform root, Dictionary<string, Material> m)
        {
            for (var x = -1; x <= 1; x += 2) for (var z = -1; z <= 1; z += 2) Primitive(PrimitiveType.Cylinder, "Tower Post", root, new Vector3(x * 2.2f, 3f, z * 2.2f), new Vector3(0.38f, 3f, 0.38f), m["wood"]);
            Primitive(PrimitiveType.Cube, "Tower Deck", root, new Vector3(0, 6f, 0), new Vector3(6f, 0.45f, 6f), m["wood"]);
            Primitive(PrimitiveType.Cube, "Tower Roof", root, new Vector3(0, 9f, 0), new Vector3(7f, 0.35f, 7f), m["cloth"]);
        }
        private static void BuildBridge(Transform root, Dictionary<string, Material> m)
        {
            for (var i = -7; i <= 7; i++) Primitive(PrimitiveType.Cube, "Bridge Plank", root, new Vector3(i * 1.15f, 1.2f, 0), new Vector3(1f, 0.22f, 8f), m["wood"]);
            Primitive(PrimitiveType.Cube, "Rail Left", root, new Vector3(0, 2.1f, -4f), new Vector3(18f, 0.22f, 0.22f), m["wood"]);
            Primitive(PrimitiveType.Cube, "Rail Right", root, new Vector3(0, 2.1f, 4f), new Vector3(18f, 0.22f, 0.22f), m["wood"]);
        }
        private static void BuildArch(Transform root, Dictionary<string, Material> m)
        {
            Primitive(PrimitiveType.Cube, "Arch Left", root, new Vector3(-3f, 3f, 0), new Vector3(2f, 6f, 2.2f), m["rock"]);
            Primitive(PrimitiveType.Cube, "Arch Right", root, new Vector3(3f, 3f, 0), new Vector3(2f, 6f, 2.2f), m["rock"]);
            Primitive(PrimitiveType.Cube, "Arch Crown", root, new Vector3(0, 6.4f, 0), new Vector3(8f, 1.7f, 2.2f), m["rock"]);
        }
        private static void BuildCamp(Transform root, Dictionary<string, Material> m)
        {
            var tent = Primitive(PrimitiveType.Cube, "Ranger Tent", root, new Vector3(0, 1.3f, 0), new Vector3(5f, 2.4f, 4f), m["cloth"]); tent.transform.rotation = Quaternion.Euler(0, 18f, 45f);
            Primitive(PrimitiveType.Cylinder, "Fire Ring", root, new Vector3(5f, 0.3f, 1f), new Vector3(1.2f, 0.28f, 1.2f), m["rock"]);
            var lightObject = new GameObject("Camp Fire Light"); lightObject.transform.SetParent(root); lightObject.transform.localPosition = new Vector3(5f, 1.5f, 1f);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 18f; light.intensity = 3f; light.color = new Color(1f, 0.47f, 0.18f);
        }
        private static void BuildSummit(Transform root, Dictionary<string, Material> m)
        {
            for (var i = 0; i < 7; i++) Primitive(PrimitiveType.Sphere, "Cairn Stone", root, new Vector3(0, 0.55f + i * 0.72f, 0), new Vector3(2.8f - i * 0.28f, 0.8f, 2.5f - i * 0.25f), m["rock"]);
        }

        private static void CreatePlayerAndCameras(MapSurface surface, Dictionary<string, Material> materials, List<FrontierLandmark> landmarks, int seed)
        {
            var spawnWorld = new Vector3(MapSize * 0.14f, 0, MapSize * 0.14f); spawnWorld.y = surface.Height(spawnWorld.x, spawnWorld.z) + 2f;
            var spawn = new GameObject("Trailhead Spawn"); spawn.transform.position = spawnWorld;
            var player = Primitive(PrimitiveType.Capsule, "Explorer", null, spawnWorld, new Vector3(1.05f, 1.05f, 1.05f), materials["player"]);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>()); var controller = player.AddComponent<CharacterController>(); controller.height = 2f; controller.radius = 0.5f; controller.center = new Vector3(0, 1f, 0);
            var playerScript = player.AddComponent<FrontierPlayer>();
            Primitive(PrimitiveType.Cube, "Backpack", player.transform, new Vector3(0, 1.1f, -0.48f), new Vector3(0.75f, 0.9f, 0.35f), materials["cloth"]);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera"; var camera = cameraObject.AddComponent<Camera>(); camera.allowHDR = true; camera.fieldOfView = 62f; camera.nearClipPlane = 0.12f; camera.farClipPlane = 1400f; cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FrontierCamera>(); follow.target = player.transform; cameraObject.transform.position = spawnWorld + follow.offset;
            var minimapObject = new GameObject("Map Camera"); var minimap = minimapObject.AddComponent<Camera>(); minimap.orthographic = true; minimap.orthographicSize = 58f; minimap.depth = 2; minimap.rect = new Rect(0.78f, 0.68f, 0.20f, 0.28f); minimap.clearFlags = CameraClearFlags.SolidColor; minimap.backgroundColor = new Color(0.06f, 0.10f, 0.08f); minimapObject.AddComponent<FrontierMinimap>().target = player.transform;
            var gameObject = new GameObject("Frontier Valley Game"); var game = gameObject.AddComponent<FrontierGame>(); game.player = playerScript; game.spawnPoint = spawn.transform; game.mapSeed = seed;
        }

        private static void CreateLightingAndPost(Dictionary<string, Material> materials)
        {
            var sunObject = new GameObject("Frontier Sun"); sunObject.transform.rotation = Quaternion.Euler(46f, -32f, 0); var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, 0.91f, 0.76f); sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.86f;
            var cycle = new GameObject("Day Night System").AddComponent<FrontierDayNight>(); cycle.sun = sun;
            var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_AtmosphereThickness", 0.85f); sky.SetColor("_SkyTint", new Color(0.38f, 0.55f, 0.68f)); sky.SetColor("_GroundColor", new Color(0.18f, 0.20f, 0.16f)); AssetDatabase.CreateAsset(sky, Generated + "/Materials/FrontierSky.mat"); RenderSettings.skybox = sky;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) throw new InvalidOperationException("A supported lit shader was not found.");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness); else if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            if (emission.HasValue) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission.Value); }
            AssetDatabase.CreateAsset(material, Generated + "/Materials/" + name + ".mat"); return material;
        }
        private static void CreateTerrainTexture(string name, Color baseColor, int seed)
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true); var pixels = new Color[128 * 128];
            for (var y = 0; y < 128; y++) for (var x = 0; x < 128; x++) { var noise = Mathf.PerlinNoise(seed * 0.001f + x * 0.075f, seed * 0.0013f + y * 0.075f); var color = baseColor * Mathf.Lerp(0.72f, 1.22f, noise); color.a = 1f; pixels[y * 128 + x] = color; }
            texture.SetPixels(pixels); texture.Apply(); var path = Generated + "/Textures/" + name + ".png"; File.WriteAllBytes(Absolute(path), texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode = TextureWrapMode.Repeat; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.sRGBTexture = true; importer.SaveAndReimport();
        }
        private static TerrainLayer TerrainLayer(string name, string texture)
        {
            var layer = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + "/Textures/" + texture + ".png"), tileSize = new Vector2(16f, 16f), metallic = 0f, smoothness = name == "Rock" ? 0.28f : 0.08f };
            AssetDatabase.CreateAsset(layer, Generated + "/" + name + "Layer.terrainlayer"); return layer;
        }
        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var value = GameObject.CreatePrimitive(type); value.name = name; if (parent != null) { value.transform.SetParent(parent); value.transform.localPosition = position; } else value.transform.position = position; value.transform.localScale = scale; value.GetComponent<Renderer>().sharedMaterial = material; return value;
        }
        private static void Flatten(float[,] heights, float cx, float cz, float radius)
        {
            var resolution = heights.GetLength(0); var center = heights[Mathf.RoundToInt(cz * (resolution - 1)), Mathf.RoundToInt(cx * (resolution - 1))];
            for (var z = 0; z < resolution; z++) for (var x = 0; x < resolution; x++) { var distance = Vector2.Distance(new Vector2(x / (float)(resolution - 1), z / (float)(resolution - 1)), new Vector2(cx, cz)); var blend = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / radius)); heights[z, x] = Mathf.Lerp(heights[z, x], center, blend); }
        }
        private static float RiverX(float z) { return 0.49f + Mathf.Sin(z * 8.4f + 0.7f) * 0.055f + Mathf.Sin(z * 19f) * 0.018f; }
        private static float TrailWeight(Vector2 point)
        {
            var points = new[] { new Vector2(0.14f, 0.14f), new Vector2(0.28f, 0.28f), new Vector2(0.49f, 0.47f), new Vector2(0.70f, 0.36f), new Vector2(0.62f, 0.72f), new Vector2(0.83f, 0.86f) };
            var distance = 10f; for (var i = 0; i + 1 < points.Length; i++) distance = Mathf.Min(distance, Distance(point, points[i], points[i + 1])); return 1f - Mathf.SmoothStep(0.012f, 0.035f, distance);
        }
        private static float Distance(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f)); return Vector2.Distance(p, a + ab * t); }
        private static float Next(float min, float max) { return Mathf.Lerp(min, max, (float)random.NextDouble()); }
        private static string Absolute(string assetPath) { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath)); }
        private static void EnsureFolder(string parent, string child) { var path = parent + "/" + child; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child); }
        private static void ValidateScenePath(string path) { var normalized = path == null ? "" : path.Replace('\\', '/'); if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || !normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || normalized.Contains("..")) throw new InvalidOperationException("Unsafe Frontier Valley scene path."); }
    }
}
#endif
""";
}
