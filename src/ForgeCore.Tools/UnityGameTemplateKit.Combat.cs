namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    private const string ProjectileSource = """
using System;
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    // Visible, swept projectiles: damage occurs on arrival, not on an unrelated hitscan.
    public sealed class PrototypeProjectile : MonoBehaviour
    {
        private static int active;
        private Transform owner;
        private bool hostile;
        private float age;
        private bool spent;
        private Material material;
        public static int ActiveCount => active;
        public static int PlayerLaunchCount { get; private set; }
        public const int MaximumActive = 96;

        public static PrototypeProjectile Launch(Vector3 origin, Vector3 direction, Transform owner, bool hostile)
        {
            var game = PrototypeGame.Instance;
            if (active >= MaximumActive || direction.sqrMagnitude < 0.5f || owner == null ||
                game != null && (game.MenuOpen || game.Complete)) return null;
            var value = GameObject.CreatePrimitive(PrimitiveType.Sphere); value.name = hostile ? "Enemy visible bullet" : "Player visible bullet";
            var collider = value.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            value.transform.position = origin; value.transform.localScale = Vector3.one * 0.055f;
            var bullet = value.AddComponent<PrototypeProjectile>(); bullet.owner = owner; bullet.hostile = hostile;
            value.transform.rotation = Quaternion.LookRotation(direction.normalized);
            var shader = Shader.Find("Unlit/Color"); if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard"); // Included by the actual primitive scene materials in standalone builds.
            if (shader == null) { Destroy(value); return null; }
            bullet.material = new Material(shader); bullet.material.color = hostile ? new Color(1f, 0.27f, 0.07f) : new Color(1f, 0.84f, 0.3f);
            if (bullet.material.HasProperty("_EmissionColor")) { bullet.material.EnableKeyword("_EMISSION"); bullet.material.SetColor("_EmissionColor", bullet.material.color * 2f); }
            var renderer = value.GetComponent<Renderer>(); renderer.sharedMaterial = bullet.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            var trail = value.AddComponent<TrailRenderer>(); trail.sharedMaterial = bullet.material;
            trail.time = 0.075f; trail.startWidth = 0.045f; trail.endWidth = 0.008f;
            trail.minVertexDistance = 0.08f; trail.emitting = true;
            if (!hostile) PlayerLaunchCount++;
            return bullet;
        }
        private void Awake() { active++; }
        private void OnDestroy() { active = Mathf.Max(0, active - 1); if (material != null) Destroy(material); }
        private void Update()
        {
            var game = PrototypeGame.Instance;
            if (game != null && game.Complete) { Destroy(gameObject); return; }
            if (game != null && game.MenuOpen) return;
            Advance(Time.deltaTime);
        }
        private void Advance(float delta)
        {
            if (spent) return;
            age += delta; if (age > 3f) { spent = true; Destroy(gameObject); return; }
            var distance = (hostile ? 22f : 70f) * delta;
            if (distance <= 0f) return;
            var hits = Physics.RaycastAll(transform.position, transform.forward, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner))) continue;
                spent = true;
                if (hostile)
                {
                    var actor = hit.collider.GetComponentInParent<PrototypeActor>();
                    if (actor != null && PrototypeGame.Instance != null && PrototypeGame.Instance.player == actor)
                        PrototypeGame.Instance.Damage(6);
                }
                else
                {
                    var target = hit.collider.GetComponentInParent<PrototypeObjective>();
                    if (target != null) target.Hit();
                }
                PrototypeFeedback.Hit(hit.point, false); Destroy(gameObject); return;
            }
            transform.position += transform.forward * distance;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void AdvanceForVerification(float delta) { Advance(Mathf.Clamp(delta, 0f, 1f)); }
#endif
    }
}
""";

    private const string CombatAnimationSource = """
using System.Collections.Generic;
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    // Original keyframed joint clips, deliberately separate from gameplay hit/ammo decisions.
    // This is a generic rigid-part rig, not a finished skinned Blender character or mocap set.
    public sealed class PrototypeCombatAnimation : MonoBehaviour
    {
        public bool firstPerson;
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private AnimationClip walk, idle, firing, reloading;
        private float walkTime, fireTime = 99f, reloadTime = 99f, reloadDuration = 1.2f;
        private Vector3 previous;
        public Transform Muzzle => transform.Find(firstPerson ? "Muzzle" : "Spine/Gun/Muzzle");
        public bool ReloadActive => reloadTime < reloadDuration;
        public bool FireActive => fireTime < 0.25f;
        public int ShotAnimations { get; private set; }
        public int ReloadAnimations { get; private set; }
        private void Awake() { previous = transform.position; }
        private void Start() { EnsureClips(); }
        public void Fire() { EnsureClips(); if (ReloadActive) return; fireTime = 0f; ShotAnimations++; }
        public void Reload(float duration) { EnsureClips(); reloadDuration = Mathf.Clamp(duration, 0.5f, 4f); reloadTime = 0f; ReloadAnimations++; }
        public void ResetPose() { fireTime = reloadTime = 99f; }
        private void LateUpdate()
        {
            var game = PrototypeGame.Instance;
            if (game != null && (game.MenuOpen || game.Complete)) { previous = transform.position; return; }
            var planar = transform.position - previous; planar.y = 0f; previous = transform.position;
            Sample(Time.deltaTime, planar.magnitude / Mathf.Max(0.0001f, Time.deltaTime));
        }
        private void Sample(float delta, float speed)
        {
            EnsureClips(); fireTime += delta; reloadTime += delta;
            idle.SampleAnimation(gameObject, 0f); // Restore all animated channels before applying layered poses.
            if (!firstPerson)
            {
                if (speed > 0.1f) { walkTime += delta * Mathf.Clamp(speed / 1.4f, 0.5f, 2f); walk.SampleAnimation(gameObject, walkTime % 1f); }
                else idle.SampleAnimation(gameObject, 0f);
            }
            if (ReloadActive) reloading.SampleAnimation(gameObject, reloadTime / reloadDuration);
            else if (FireActive) firing.SampleAnimation(gameObject, fireTime / 0.25f);
            else if (firstPerson) idle.SampleAnimation(gameObject, 0f);
        }
        private void EnsureClips()
        {
            if (idle != null) return;
            idle = Clip("Idle"); walk = Clip("Walk"); firing = Clip("Fire"); reloading = Clip("Reload");
            if (firstPerson)
            {
                Position(idle, "", Vector3.zero, Vector3.zero, Vector3.zero);
                Rotation(idle, "", Vector3.zero, Vector3.zero, Vector3.zero);
                Position(idle, "Yard.Rubber.Weapon magazine", new Vector3(0f, -0.14f, 0.05f), new Vector3(0f, -0.14f, 0.05f), new Vector3(0f, -0.14f, 0.05f));
                Position(idle, "Yard.Skin.Supporting hand", new Vector3(-0.04f, -0.09f, 0.25f), new Vector3(-0.04f, -0.09f, 0.25f), new Vector3(-0.04f, -0.09f, 0.25f));
                Position(firing, "", Vector3.zero, new Vector3(0f, 0.025f, -0.075f), Vector3.zero);
                Rotation(firing, "", Vector3.zero, new Vector3(-8f, 0f, 2f), Vector3.zero);
                Position(reloading, "", Vector3.zero, new Vector3(-0.08f, -0.13f, 0.03f), Vector3.zero);
                Rotation(reloading, "", Vector3.zero, new Vector3(18f, -12f, -32f), Vector3.zero);
                Position(reloading, "Yard.Rubber.Weapon magazine", new Vector3(0f, -0.14f, 0.05f), new Vector3(-0.06f, -0.4f, 0.05f), new Vector3(0f, -0.14f, 0.05f));
                Position(reloading, "Yard.Skin.Supporting hand", new Vector3(-0.04f, -0.09f, 0.25f), new Vector3(-0.07f, -0.35f, 0.04f), new Vector3(-0.04f, -0.09f, 0.25f));
            }
            else
            {
                foreach (var path in new[] { "LeftHip", "RightHip", "LeftHip/Knee", "RightHip/Knee", "Spine", "Spine/LeftShoulder", "Spine/Gun" })
                    Rotation(idle, path, Vector3.zero, Vector3.zero, Vector3.zero);
                Position(idle, "Spine/Gun/Yard.Rubber.Enemy magazine", new Vector3(0f, -0.13f, 0.06f), new Vector3(0f, -0.13f, 0.06f), new Vector3(0f, -0.13f, 0.06f));
                Gait(walk, "LeftHip", 28f, 0f); Gait(walk, "RightHip", 28f, Mathf.PI);
                Gait(walk, "LeftHip/Knee", 35f, 0f, true); Gait(walk, "RightHip/Knee", 35f, Mathf.PI, true);
                Rotation(walk, "Spine", new Vector3(2f, -3f, 0f), new Vector3(2f, 3f, 0f), new Vector3(2f, -3f, 0f));
                Rotation(firing, "Spine", Vector3.zero, new Vector3(-5f, 0f, 0f), Vector3.zero);
                Rotation(reloading, "Spine", Vector3.zero, new Vector3(10f, -8f, 0f), Vector3.zero);
                Rotation(reloading, "Spine/LeftShoulder", Vector3.zero, new Vector3(35f, 0f, -22f), Vector3.zero);
                Rotation(reloading, "Spine/Gun", Vector3.zero, new Vector3(18f, 0f, -28f), Vector3.zero);
                Position(reloading, "Spine/Gun/Yard.Rubber.Enemy magazine", new Vector3(0f, -0.13f, 0.06f), new Vector3(-0.04f, -0.4f, 0.06f), new Vector3(0f, -0.13f, 0.06f));
            }
        }
        private AnimationClip Clip(string name)
        {
            var clip = new AnimationClip { name = "ForgeCore original " + name, legacy = true, frameRate = 30f };
            clips.Add(clip); return clip;
        }
        private static void Position(AnimationClip clip, string path, Vector3 a, Vector3 b, Vector3 c)
        {
            for (var axis = 0; axis < 3; axis++) clip.SetCurve(path, typeof(Transform), "localPosition." + "xyz"[axis],
                new AnimationCurve(new Keyframe(0f, a[axis]), new Keyframe(0.4f, b[axis]), new Keyframe(1f, c[axis])));
        }
        private static void Rotation(AnimationClip clip, string path, Vector3 a, Vector3 b, Vector3 c)
        {
            var qa = Quaternion.Euler(a); var qb = Quaternion.Euler(b); var qc = Quaternion.Euler(c);
            for (var axis = 0; axis < 4; axis++) clip.SetCurve(path, typeof(Transform), "localRotation." + "xyzw"[axis],
                new AnimationCurve(new Keyframe(0f, qa[axis]), new Keyframe(0.4f, qb[axis]), new Keyframe(1f, qc[axis])));
            clip.EnsureQuaternionContinuity();
        }
        private static void Gait(AnimationClip clip, string path, float amplitude, float phase, bool knee = false)
        {
            var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            for (var key = 0; key <= 24; key++)
            {
                var t = key / 24f; var wave = Mathf.Sin(t * Mathf.PI * 2f + phase);
                var rotation = Quaternion.Euler(knee ? -Mathf.Max(0f, wave) * amplitude : wave * amplitude, 0f, 0f);
                for (var axis = 0; axis < 4; axis++) curves[axis].AddKey(t, rotation[axis]);
            }
            for (var axis = 0; axis < 4; axis++) clip.SetCurve(path, typeof(Transform), "localRotation." + "xyzw"[axis], curves[axis]);
            clip.EnsureQuaternionContinuity();
        }
        private void OnDestroy() { foreach (var clip in clips) if (clip != null) Destroy(clip); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SampleForVerification(float delta, float speed) { Sample(delta, speed); }
#endif
    }
}
""";

    private const string RangedEnemySource = """
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    [RequireComponent(typeof(PrototypeObjective))]
    public sealed class PrototypeRangedEnemy : MonoBehaviour
    {
        private PrototypeObjective target;
        private PrototypeCombatAnimation pose;
        private float nextShot, reloadUntil;
        private int rounds = 4, reserve = 24;
        public int ShotsFired { get; private set; }
        public int ReloadCount { get; private set; }
        public bool Reloading => Time.time < reloadUntil;
        private void Start()
        {
            target = GetComponent<PrototypeObjective>(); pose = GetComponentInChildren<PrototypeCombatAnimation>();
            nextShot = Time.time + 1.5f + target.order * 0.15f;
        }
        private void Update()
        {
            var game = PrototypeGame.Instance;
            if (game == null || game.MenuOpen || game.Complete || game.player == null || pose == null) return;
            var position = transform.position; position.y = transform.lossyScale.y; transform.position = position;
            var offset = game.player.transform.position - position; offset.y = 0f; var distance = offset.magnitude;
            if (distance > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), 7f * Time.deltaTime);
            if (distance > 9f && distance < 28f)
            {
                var motion = offset.normalized * PrototypeObjective.MoveSpeedFor(target.archetype, game.levelNumber) * Time.deltaTime;
                var origin = position + Vector3.up * 0.15f;
                if (!Physics.SphereCast(origin, 0.4f, motion.normalized, out _, motion.magnitude + 0.15f, ~0, QueryTriggerInteraction.Ignore))
                    transform.position += motion;
            }
            if (reloadUntil > 0f)
            {
                if (Reloading) return;
                var loaded = Mathf.Min(4, reserve); rounds = loaded; reserve -= loaded; reloadUntil = 0f;
            }
            if (distance > 26f || Time.time < nextShot || reserve == 0 && rounds == 0) return;
            var muzzle = pose.Muzzle; if (muzzle == null) return;
            var aim = game.player.transform.position + Vector3.up * 0.2f;
            var direction = (aim - muzzle.position).normalized;
            if (Physics.Linecast(transform.position + Vector3.up * 0.3f, muzzle.position, out _, ~0, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(muzzle.position, direction, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponentInParent<PrototypeActor>() != game.player) return;
            if (rounds <= 0)
            {
                reloadUntil = Time.time + 1.8f; ReloadCount++; pose.Reload(1.8f); return;
            }
            if (PrototypeProjectile.Launch(muzzle.position, direction, transform, true) == null) return;
            rounds--; ShotsFired++; pose.Fire(); PrototypeFeedback.Fire(muzzle.position);
            nextShot = Time.time + 0.9f + target.order * 0.06f;
            if (rounds == 0 && reserve > 0) { reloadUntil = Time.time + 1.8f; ReloadCount++; pose.Reload(1.8f); }
        }
    }
}
""";
}
