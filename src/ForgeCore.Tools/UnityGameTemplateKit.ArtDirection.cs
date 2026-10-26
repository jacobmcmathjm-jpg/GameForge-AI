namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    private const string PrototypeArtDirectionSource = """
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForgeCoreGameTemplates
{
    // Original procedural look; no downloaded textures, models, packages, or network calls.
    public sealed class PrototypeArtDirection : MonoBehaviour
    {
        public enum Finish { Asphalt, Concrete, Brick, Steel, Rust, Paint, Wood, Glass, Leaf, Glow, Cloth, Skin, Rubber }
        private readonly Dictionary<Finish, Material> materials = new Dictionary<Finish, Material>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Material sky;
        private PrototypeObjective[] actors;
        private Transform[] leftLegs, rightLegs;

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.56f, 0.67f);
            RenderSettings.ambientEquatorColor = new Color(0.37f, 0.4f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.16f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.47f, 0.54f, 0.59f); RenderSettings.fogDensity = 0.009f;
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                sky = new Material(skyShader); sky.SetColor("_SkyTint", new Color(0.5f, 0.58f, 0.68f));
                sky.SetColor("_GroundColor", new Color(0.31f, 0.32f, 0.3f)); sky.SetFloat("_Exposure", 1.05f);
                sky.SetFloat("_AtmosphereThickness", 1.3f); RenderSettings.skybox = sky;
            }
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = sky == null ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                camera.backgroundColor = RenderSettings.fogColor; camera.allowHDR = true;
                camera.nearClipPlane = 0.04f; camera.farClipPlane = 220f; camera.fieldOfView = 70f;
            }
            foreach (var lamp in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (lamp.type == LightType.Directional)
                {
                    lamp.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
                    lamp.color = new Color(1f, 0.83f, 0.64f); lamp.intensity = 1.35f;
                    lamp.shadows = LightShadows.Soft; lamp.shadowBias = 0.025f; RenderSettings.sun = lamp;
                }
            QualitySettings.shadowDistance = 70f;
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled) continue;
                var parts = renderer.name.Split('.');
                // Only our explicitly tagged procedural geometry belongs to this material pass.
                // Preserve every imported mesh's original multi-material PBR slots and textures.
                if (parts.Length <= 2 || parts[0] != "Yard" || !Enum.TryParse(parts[1], out Finish finish)) continue;
                renderer.sharedMaterial = MaterialFor(finish);
            }
            actors = FindObjectsByType<PrototypeObjective>(FindObjectsSortMode.None);
            leftLegs = new Transform[actors.Length]; rightLegs = new Transform[actors.Length];
            for (var i = 0; i < actors.Length; i++)
            {
                leftLegs[i] = actors[i].transform.Find("Yard.Cloth.Left leg");
                rightLegs[i] = actors[i].transform.Find("Yard.Cloth.Right leg");
            }
        }

        private void Update()
        {
            if (actors == null || PrototypeGame.Instance == null || PrototypeGame.Instance.MenuOpen) return;
            for (var i = 0; i < actors.Length; i++)
            {
                if (actors[i] == null || !actors[i].gameObject.activeInHierarchy || leftLegs[i] == null) continue;
                var wave = Mathf.Sin(Time.time * (actors[i].archetype == 0 ? 9f : 6f) + i) * 19f;
                leftLegs[i].localRotation = Quaternion.Euler(wave, 0f, 0f);
                rightLegs[i].localRotation = Quaternion.Euler(-wave, 0f, 0f);
                var position = actors[i].transform.position; position.y = actors[i].transform.lossyScale.y;
                actors[i].transform.position = position;
            }
        }

        private Material MaterialFor(Finish finish)
        {
            if (materials.TryGetValue(finish, out var existing)) return existing;
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Industrial yard needs a supported lit shader.");
            var material = new Material(shader) { name = "ForgeCore original " + finish, enableInstancing = true };
            var color = finish == Finish.Asphalt ? new Color(0.22f, 0.24f, 0.24f) :
                finish == Finish.Concrete ? new Color(0.53f, 0.51f, 0.46f) :
                finish == Finish.Brick ? new Color(0.48f, 0.3f, 0.23f) :
                finish == Finish.Steel ? new Color(0.3f, 0.34f, 0.35f) :
                finish == Finish.Rust ? new Color(0.61f, 0.27f, 0.12f) :
                finish == Finish.Paint ? new Color(0.73f, 0.57f, 0.2f) :
                finish == Finish.Wood ? new Color(0.39f, 0.26f, 0.14f) :
                finish == Finish.Glass ? new Color(0.19f, 0.34f, 0.4f) :
                finish == Finish.Leaf ? new Color(0.31f, 0.35f, 0.18f) :
                finish == Finish.Glow ? new Color(1f, 0.76f, 0.34f) :
                finish == Finish.Cloth ? new Color(0.26f, 0.3f, 0.25f) :
                finish == Finish.Skin ? new Color(0.52f, 0.47f, 0.37f) : new Color(0.07f, 0.08f, 0.075f);
            material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            const int size = 128; var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size]; var normals = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var broad = Mathf.PerlinNoise(x * 0.075f + (int)finish * 3.7f, y * 0.075f);
                var fine = Mathf.PerlinNoise(x * 0.72f + 37f, y * 0.72f + 21f);
                var value = 0.67f + broad * 0.29f + fine * 0.12f;
                if (finish == Finish.Brick)
                {
                    var row = y / 16; var seam = y % 16 < 2 || (x + (row % 2) * 16) % 32 < 2;
                    if (seam) value = 0.43f;
                }
                if (finish == Finish.Wood) value *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.33f, y * 0.025f);
                if (finish == Finish.Asphalt && Mathf.PerlinNoise(x * 0.035f, y * 0.035f) > 0.72f) value *= 0.6f;
                if (finish == Finish.Glass || finish == Finish.Glow) value = 1f;
                pixels[y * size + x] = new Color(value, value, value, 1f);
                var bump = finish == Finish.Glass || finish == Finish.Glow ? 0f : 0.06f;
                normals[y * size + x] = new Color(0.5f + (fine - 0.5f) * bump, 0.5f + (broad - 0.5f) * bump, 1f, 0.5f + (fine - 0.5f) * bump);
            }
            albedo.name = "Original " + finish + " albedo"; normal.name = "Original " + finish + " normal";
            albedo.SetPixels(pixels); albedo.Apply(); normal.SetPixels(normals); normal.Apply();
            albedo.wrapMode = normal.wrapMode = TextureWrapMode.Repeat; albedo.anisoLevel = normal.anisoLevel = 8;
            textures.Add(albedo); textures.Add(normal); material.mainTexture = albedo;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_BumpMap")) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            var tiling = finish == Finish.Asphalt ? new Vector2(16f, 16f) : finish == Finish.Brick ? new Vector2(4f, 3f) : Vector2.one;
            material.mainTextureScale = tiling;
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", tiling);
            var smooth = finish == Finish.Glass ? 0.85f : finish == Finish.Steel ? 0.46f : 0.18f;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smooth);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", finish == Finish.Steel ? 0.65f : 0f);
            if (finish == Finish.Glow || finish == Finish.Glass)
            {
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * (finish == Finish.Glow ? 1.7f : 0.12f));
            }
            materials.Add(finish, material); return material;
        }

        private void OnDestroy()
        {
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            foreach (var texture in textures) if (texture != null) Destroy(texture);
            if (sky != null) { if (RenderSettings.skybox == sky) RenderSettings.skybox = null; Destroy(sky); }
        }
    }
}
""";

    private const string IndustrialYardSource = """
using UnityEngine;

namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreIndustrialYard
    {
        public static void Build(PrototypeGame game)
        {
            game.gameObject.AddComponent<PrototypeArtDirection>();
            Box("Asphalt", "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(100f, 1f, 100f));
            // Keep the original 30m gameplay envelope and open centre; dressing lives at its edges.
            Box("Concrete", "Rear boundary", new Vector3(0f, 1.2f, 16f), new Vector3(32f, 2.4f, 0.6f));
            Box("Concrete", "Entry boundary", new Vector3(0f, 1.2f, -16f), new Vector3(32f, 2.4f, 0.6f));
            Box("Concrete", "West boundary", new Vector3(-16f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 32f));
            Box("Concrete", "East boundary", new Vector3(16f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 32f));
            Warehouse(new Vector3(0f, 0f, 20f), 26f, 6f);
            Warehouse(new Vector3(-24f, 0f, -3f), 10f, 9f);
            Warehouse(new Vector3(27f, 0f, 12f), 13f, 11f);
            Container(new Vector3(11f, 0f, 6f));
            TankTower(new Vector3(-11f, 0f, 13f));
            for (var i = 0; i < 4; i++)
            {
                var z = -8f + i * 5f;
                Box("Paint", "Lane centre", new Vector3(0f, 0.008f, z), new Vector3(0.1f, 0.012f, 2.3f), false);
                Box("Paint", "Parking left", new Vector3(-8f, 0.008f, z), new Vector3(3.5f, 0.012f, 0.09f), false);
                Barrel(new Vector3(-12f + i % 2 * 0.9f, 0f, -3f + i / 2 * 1.0f));
            }
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < 9; i++)
                {
                    var z = -15f + i * 3.8f;
                    Box("Steel", "Fence post", new Vector3(side * 16f, 3.1f, z), new Vector3(0.09f, 2.1f, 0.09f), false);
                    Box("Steel", "Fence rail", new Vector3(side * 16f, 3.8f, z), new Vector3(0.07f, 0.07f, 3.8f), false);
                }
                for (var i = 0; i < 8; i++) Tree(new Vector3(side * (21f + i % 3 * 3f), 0f, -22f + i * 7f), 6f + i % 3 * 1.3f);
                for (var i = 0; i < 24; i++)
                {
                    var p = new Vector3(side * (14.1f + Random.Range(0f, 1f)), 0f, Random.Range(-14f, 14f));
                    Tuft(p);
                }
            }
            // Authored modular cover, not a ring of anonymous blue blocks.
            for (var side = -1; side <= 1; side += 2)
            {
                var p = new Vector3(side * 5.8f, 0f, 1f);
                Box("Concrete", "Road barrier", p + Vector3.up * 0.48f, new Vector3(2.8f, 0.95f, 0.55f));
                Box("Concrete", "Barrier foot", p + Vector3.up * 0.1f, new Vector3(3.1f, 0.2f, 0.95f));
                for (var i = 0; i < 5; i++) Box("Paint", "Barrier stripe", p + new Vector3(-1.1f + i * 0.55f, 0.57f, -0.285f), new Vector3(0.24f, 0.5f, 0.025f), false, Quaternion.Euler(0f, 0f, -25f));
                Crate(new Vector3(side * 8f, 0f, -5f));
                Lamp(new Vector3(side * 13f, 0f, -10f));
            }
            for (var i = 0; i < 7; i++)
                Box("Concrete", "Distant silhouette", new Vector3(-45f + i * 15f, 7f, 68f + i % 2 * 10f), new Vector3(10f, 14f + i % 3 * 5f, 8f), false);
        }

        private static void Warehouse(Vector3 p, float width, float height)
        {
            Box("Brick", "Warehouse", p + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 8f));
            Box("Steel", "Roof fascia", p + new Vector3(0f, height, -0.15f), new Vector3(width + 0.8f, 0.28f, 8.6f));
            Box("Steel", "Loading shutter", p + new Vector3(0f, 2f, -4.03f), new Vector3(4.8f, 4f, 0.12f), false);
            for (var i = 0; i < 12; i++) Box("Rust", "Shutter slat", p + new Vector3(0f, 0.24f + i * 0.32f, -4.12f), new Vector3(4.7f, 0.035f, 0.045f), false);
            for (var i = 0; i < 6; i++)
            {
                var x = -width * 0.42f + i * width * 0.168f;
                Box("Steel", "Window frame", p + new Vector3(x, height - 1.25f, -4.05f), new Vector3(width * 0.13f, 1.35f, 0.12f), false);
                Box("Glass", "Window glass", p + new Vector3(x, height - 1.25f, -4.13f), new Vector3(width * 0.12f, 1.18f, 0.05f), false);
                Box("Steel", "Window mullion", p + new Vector3(x, height - 1.25f, -4.18f), new Vector3(0.07f, 1.2f, 0.05f), false);
            }
            for (var i = 0; i <= 5; i++) Box("Concrete", "Brick pilaster", p + new Vector3(-width / 2f + i * width / 5f, height / 2f, -4.06f), new Vector3(0.28f, height, 0.22f), false);
            for (var i = 0; i < 2; i++) Cylinder("Rust", "Roof vent", p + new Vector3(-width * 0.25f + i * width * 0.5f, height + 0.8f, 0f), new Vector3(0.55f, 0.9f, 0.55f), false);
        }

        private static void Container(Vector3 p)
        {
            Box("Rust", "Cargo container", p + new Vector3(0f, 1.4f, 0f), new Vector3(3f, 2.8f, 6f));
            for (var i = 0; i < 17; i++)
            {
                Box("Rust", "Cargo corrugation", p + new Vector3(-1.53f, 1.4f, -2.8f + i * 0.35f), new Vector3(0.07f, 2.65f, 0.09f), false);
                Box("Rust", "Cargo corrugation", p + new Vector3(1.53f, 1.4f, -2.8f + i * 0.35f), new Vector3(0.07f, 2.65f, 0.09f), false);
            }
            for (var i = -1; i <= 1; i += 2)
                Box("Steel", "Door locking bar", p + new Vector3(i * 0.7f, 1.4f, -3.07f), new Vector3(0.07f, 2.6f, 0.07f), false);
        }

        private static void TankTower(Vector3 p)
        {
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
                Box("Steel", "Tank support", p + new Vector3(x * 1.5f, 3.5f, z * 1.5f), new Vector3(0.18f, 7f, 0.18f));
            Box("Steel", "Tank platform", p + Vector3.up * 6.9f, new Vector3(4.2f, 0.18f, 4.2f));
            Cylinder("Rust", "Water tank", p + Vector3.up * 8.7f, new Vector3(3.8f, 1.7f, 3.8f));
            Cylinder("Steel", "Tank lid", p + Vector3.up * 10.45f, new Vector3(3.95f, 0.06f, 3.95f), false);
            for (var i = 0; i < 12; i++) Box("Steel", "Ladder rung", p + new Vector3(1.65f, 0.5f + i * 0.53f, -1.55f), new Vector3(0.7f, 0.055f, 0.055f), false);
            for (var i = 0; i < 3; i++) Box("Steel", "Tower brace", p + new Vector3(0f, 1.5f + i * 2f, -1.5f), new Vector3(3.9f, 0.1f, 0.1f), false, Quaternion.Euler(0f, 0f, 32f));
        }

        private static void Barrel(Vector3 p)
        {
            Cylinder("Rust", "Drum", p + Vector3.up * 0.55f, new Vector3(0.72f, 0.55f, 0.72f));
            Cylinder("Steel", "Drum rim", p + Vector3.up * 1.09f, new Vector3(0.75f, 0.025f, 0.75f), false);
            Cylinder("Steel", "Drum band", p + Vector3.up * 0.4f, new Vector3(0.74f, 0.025f, 0.74f), false);
        }
        private static void Crate(Vector3 p)
        {
            Box("Wood", "Pallet crate", p + Vector3.up * 0.65f, new Vector3(1.6f, 1.3f, 1.3f));
            for (var i = 0; i < 5; i++) Box("Wood", "Crate plank", p + new Vector3(-0.64f + i * 0.32f, 0.65f, -0.67f), new Vector3(0.28f, 1.22f, 0.06f), false);
            Box("Steel", "Crate band", p + Vector3.up * 0.42f, new Vector3(1.64f, 0.08f, 1.34f), false);
        }
        private static void Tree(Vector3 p, float h)
        {
            Cylinder("Wood", "Bare trunk", p + Vector3.up * h * 0.5f, new Vector3(0.3f, h * 0.5f, 0.3f), false);
            for (var i = 0; i < 5; i++)
            {
                var arm = Cylinder("Wood", "Bare branch", p + new Vector3(i % 2 == 0 ? -0.7f : 0.7f, h * 0.5f + i * 0.55f, i % 2 * 0.4f), new Vector3(0.1f, h * 0.23f, 0.1f), false);
                arm.transform.rotation = Quaternion.Euler(12f * i, i * 61f, i % 2 == 0 ? 42f : -42f);
            }
        }
        private static void Tuft(Vector3 p)
        {
            for (var i = 0; i < 3; i++) Box("Leaf", "Grass", p + new Vector3(i * 0.09f, 0.25f, 0f), new Vector3(0.05f, 0.5f, 0.04f), false, Quaternion.Euler(0f, i * 61f, -20f + i * 20f));
        }
        private static void Lamp(Vector3 p)
        {
            Cylinder("Steel", "Lamp pole", p + Vector3.up * 3.3f, new Vector3(0.13f, 3.3f, 0.13f));
            Box("Steel", "Lamp arm", p + new Vector3(-0.45f, 6.4f, 0f), new Vector3(1f, 0.1f, 0.12f), false);
            Box("Glow", "Lamp lens", p + new Vector3(-0.85f, 6.3f, 0f), new Vector3(0.4f, 0.06f, 0.3f), false);
        }

        public static void BuildWeapon(Transform parent)
        {
            Part(parent, "Steel", "Weapon receiver", PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.13f, 0.4f));
            Part(parent, "Wood", "Weapon stock", PrimitiveType.Cube, new Vector3(0f, -0.045f, -0.23f), new Vector3(0.12f, 0.16f, 0.25f));
            Part(parent, "Steel", "Weapon barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.48f), new Vector3(0.042f, 0.31f, 0.042f), Quaternion.Euler(90f, 0f, 0f));
            Part(parent, "Steel", "Weapon magazine tube", PrimitiveType.Cylinder, new Vector3(0f, -0.025f, 0.37f), new Vector3(0.038f, 0.22f, 0.038f), Quaternion.Euler(90f, 0f, 0f));
            Part(parent, "Wood", "Weapon foregrip", PrimitiveType.Cube, new Vector3(0f, -0.025f, 0.3f), new Vector3(0.1f, 0.1f, 0.25f));
            for (var i = 0; i < 6; i++) Part(parent, "Rubber", "Weapon grip rib", PrimitiveType.Cube, new Vector3(0f, -0.025f, 0.2f + i * 0.036f), new Vector3(0.105f, 0.105f, 0.009f));
            Part(parent, "Rubber", "Weapon port", PrimitiveType.Cube, new Vector3(0.057f, 0.015f, 0.04f), new Vector3(0.008f, 0.045f, 0.12f));
            Part(parent, "Steel", "Weapon sight", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.74f), new Vector3(0.016f, 0.05f, 0.025f));
            Part(parent, "Cloth", "Sleeve", PrimitiveType.Capsule, new Vector3(-0.12f, -0.17f, 0.09f), new Vector3(0.13f, 0.2f, 0.13f), Quaternion.Euler(65f, 25f, -30f));
            Part(parent, "Skin", "Supporting hand", PrimitiveType.Sphere, new Vector3(-0.04f, -0.09f, 0.25f), new Vector3(0.1f, 0.085f, 0.16f));
            Part(parent, "Skin", "Trigger hand", PrimitiveType.Sphere, new Vector3(0.05f, -0.09f, -0.1f), new Vector3(0.085f, 0.12f, 0.1f));
            Part(parent, "Rubber", "Weapon magazine", PrimitiveType.Cube, new Vector3(0f, -0.14f, 0.05f), new Vector3(0.07f, 0.18f, 0.11f));
            var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(parent, false); muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.81f);
        }

        public static void BuildTarget(Transform parent)
        {
            parent.GetComponent<Renderer>().enabled = false; // Retain the gameplay hit collider, hide capsule placeholder.
            var rig = Joint(parent, "Combat Rig", Vector3.zero);
            var spine = Joint(rig, "Spine", new Vector3(0f, 0.1f, 0f));
            Part(spine, "Cloth", "Jacket", PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0f), new Vector3(0.56f, 0.34f, 0.32f));
            Part(spine, "Skin", "Head", PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0f), new Vector3(0.34f, 0.42f, 0.33f));
            foreach (var side in new[] { -1f, 1f })
            {
                var label = side < 0f ? "Left" : "Right";
                var hip = Joint(rig, label + "Hip", new Vector3(side * 0.16f, -0.05f, 0f));
                Part(hip, "Cloth", label + " thigh", PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0f), new Vector3(0.2f, 0.23f, 0.22f));
                var knee = Joint(hip, "Knee", new Vector3(0f, -0.45f, 0f));
                Part(knee, "Cloth", label + " shin", PrimitiveType.Capsule, new Vector3(0f, -0.19f, 0f), new Vector3(0.17f, 0.2f, 0.19f));
                var foot = Joint(knee, "Foot", new Vector3(0f, -0.42f, 0f));
                Part(foot, "Rubber", label + " boot", PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.07f), new Vector3(0.22f, 0.16f, 0.35f));
                var shoulder = Joint(spine, label + "Shoulder", new Vector3(side * 0.32f, 0.27f, 0f));
                Part(shoulder, "Cloth", label + " upper arm", PrimitiveType.Capsule, new Vector3(0f, -0.03f, 0.16f), new Vector3(0.17f, 0.17f, 0.17f), Quaternion.Euler(80f, 0f, 0f));
                var elbow = Joint(shoulder, "Elbow", new Vector3(0f, -0.05f, 0.3f));
                Part(elbow, "Cloth", label + " forearm", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.12f), new Vector3(0.15f, 0.15f, 0.15f), Quaternion.Euler(90f, 0f, 0f));
                Part(elbow, "Skin", label + " hand", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.27f), new Vector3(0.12f, 0.12f, 0.17f));
            }
            var gun = Joint(spine, "Gun", new Vector3(0.18f, 0.18f, 0.48f));
            Part(gun, "Steel", "Enemy receiver", PrimitiveType.Cube, Vector3.zero, new Vector3(0.11f, 0.13f, 0.4f));
            Part(gun, "Steel", "Enemy barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.35f), new Vector3(0.045f, 0.22f, 0.045f), Quaternion.Euler(90f, 0f, 0f));
            Part(gun, "Rubber", "Enemy magazine", PrimitiveType.Cube, new Vector3(0f, -0.13f, 0.06f), new Vector3(0.07f, 0.2f, 0.12f));
            Joint(gun, "Muzzle", new Vector3(0f, 0.025f, 0.6f));
            rig.gameObject.AddComponent<PrototypeCombatAnimation>();
            parent.gameObject.AddComponent<PrototypeRangedEnemy>();
        }

        private static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var value = new GameObject(name); value.transform.SetParent(parent, false); value.transform.localPosition = position; return value.transform;
        }

        private static void Part(Transform parent, string finish, string name, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation = default)
        {
            var part = Shape(finish, name, type, Vector3.zero, scale, false); part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
        }
        private static GameObject Box(string finish, string name, Vector3 position, Vector3 scale, bool collision = true, Quaternion rotation = default)
        {
            var value = Shape(finish, name, PrimitiveType.Cube, position, scale, collision);
            value.transform.rotation = rotation == default ? Quaternion.identity : rotation; return value;
        }
        private static GameObject Cylinder(string finish, string name, Vector3 position, Vector3 scale, bool collision = true) => Shape(finish, name, PrimitiveType.Cylinder, position, scale, collision);
        private static GameObject Shape(string finish, string name, PrimitiveType type, Vector3 position, Vector3 scale, bool collision)
        {
            var value = GameObject.CreatePrimitive(type); value.name = "Yard." + finish + "." + name;
            value.transform.position = position; value.transform.localScale = scale;
            if (!collision) UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
            return value;
        }
    }
}
""";
}
