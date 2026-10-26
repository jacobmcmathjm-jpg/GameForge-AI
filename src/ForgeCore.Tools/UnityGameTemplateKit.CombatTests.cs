namespace ForgeCore.Tools;

internal static partial class UnityGameTemplateKit
{
    private const string CombatTestsSource = """
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ForgeCoreGameTemplates.Tests
{
    public sealed class PrototypeCombatTests
    {
        [UnityTest]
        public IEnumerator ArtDirection_PreservesImportedMaterialSlotsTexturesAndTheirOwnership()
        {
            var root = new GameObject("Imported asset material regression");
            var imported = new GameObject("Original Blender barrel renderer"); imported.transform.SetParent(root.transform);
            var yard = new GameObject("Yard.Steel.Regression"); yard.transform.SetParent(root.transform);
            var shader = Shader.Find("Standard"); Assert.That(shader, Is.Not.Null);
            var first = new Material(shader); var second = new Material(shader); var texture = new Texture2D(2, 2);
            first.mainTexture = texture;
            var renderer = imported.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { first, second };
            var yardRenderer = yard.AddComponent<MeshRenderer>(); yardRenderer.sharedMaterial = first;
            try
            {
                root.AddComponent<PrototypeArtDirection>().Apply();
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(first));
                Assert.That(renderer.sharedMaterials[1], Is.SameAs(second));
                Assert.That(renderer.sharedMaterials[0].mainTexture, Is.SameAs(texture));
                Assert.That(yardRenderer.sharedMaterial, Is.Not.SameAs(first), "Only explicitly Yard-tagged geometry should get procedural finishes.");
                Object.DestroyImmediate(root);
                Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null); Assert.That(texture, Is.Not.Null);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(texture);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator JointClips_BendKneesAndRestoreWeaponAfterFireAndReload()
        {
            var rig = Rig(); var weapon = new GameObject("Weapon animation test");
            var magazine = Child(weapon.transform, "Yard.Rubber.Weapon magazine", new Vector3(0f, -0.14f, 0.05f));
            Child(weapon.transform, "Yard.Skin.Supporting hand", new Vector3(-0.04f, -0.09f, 0.25f));
            try
            {
                var pose = rig.AddComponent<PrototypeCombatAnimation>();
                pose.SampleForVerification(0.25f, 1.4f);
                Assert.That(Quaternion.Angle(rig.transform.Find("LeftHip/Knee").localRotation, Quaternion.identity), Is.GreaterThan(20f));
                Assert.That(Quaternion.Angle(rig.transform.Find("LeftHip").localRotation, rig.transform.Find("RightHip").localRotation), Is.GreaterThan(30f));
                pose.SampleForVerification(0f, 0f);
                Assert.That(Quaternion.Angle(rig.transform.Find("LeftHip/Knee").localRotation, Quaternion.identity), Is.LessThan(0.1f));
                var firstPerson = weapon.AddComponent<PrototypeCombatAnimation>(); firstPerson.firstPerson = true;
                firstPerson.Fire(); firstPerson.SampleForVerification(0.1f, 0f);
                Assert.That(weapon.transform.localPosition.z, Is.LessThan(-0.03f));
                firstPerson.SampleForVerification(0.3f, 0f);
                Assert.That(weapon.transform.localPosition.magnitude, Is.LessThan(0.001f));
                firstPerson.Reload(1.2f); firstPerson.SampleForVerification(0.48f, 0f);
                Assert.That(magazine.localPosition.y, Is.LessThan(-0.3f));
                var shotCount = firstPerson.ShotAnimations; firstPerson.Fire();
                Assert.That(firstPerson.ShotAnimations, Is.EqualTo(shotCount), "Reload pose must reject a firing animation.");
                firstPerson.SampleForVerification(1f, 0f);
                Assert.That(firstPerson.ReloadActive, Is.False);
                Assert.That(Vector3.Distance(magazine.localPosition, new Vector3(0f, -0.14f, 0.05f)), Is.LessThan(0.001f));
                Assert.That(weapon.transform.localPosition.magnitude, Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(rig); Object.DestroyImmediate(weapon); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisibleBullets_TravelBeforeDamageAndCannotCrossCover()
        {
            var root = new GameObject("Projectile game test"); var actorObject = Player(root.transform);
            var targetObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                targetObject.transform.position = new Vector3(0f, 1f, 6f);
                var target = targetObject.AddComponent<PrototypeObjective>(); target.kind = ObjectiveKind.Target; target.health = 10; target.enabled = false;
                wall.transform.position = new Vector3(0f, 1f, 3f); wall.transform.localScale = new Vector3(4f, 4f, 0.05f); wall.SetActive(false);
                var game = root.AddComponent<PrototypeGame>(); game.templateId = "first_person_action"; game.requiredObjectives = 2; game.player = actorObject.GetComponent<PrototypeActor>();
                yield return null; yield return StartGame();
                Physics.SyncTransforms();
                var bullet = PrototypeProjectile.Launch(new Vector3(0f, 1f, 0.6f), Vector3.forward, actorObject.transform, false);
                Assert.That(bullet, Is.Not.Null); Assert.That(bullet.GetComponent<TrailRenderer>(), Is.Not.Null);
                bullet.AdvanceForVerification(0.01f);
                Assert.That(target.health, Is.EqualTo(10), "Shot applied hitscan damage before visible arrival.");
                bullet.AdvanceForVerification(0.1f);
                Assert.That(target.health, Is.EqualTo(9)); yield return null;
                wall.SetActive(true); Physics.SyncTransforms();
                var blocked = PrototypeProjectile.Launch(new Vector3(0f, 1f, 0.6f), Vector3.forward, actorObject.transform, false);
                blocked.AdvanceForVerification(0.2f);
                Assert.That(target.health, Is.EqualTo(9), "A large frame step allowed the bullet to tunnel through thin cover.");
                wall.SetActive(false); Physics.SyncTransforms();
                var health = game.Health;
                var hostile = PrototypeProjectile.Launch(new Vector3(0f, 1f, 5f), Vector3.back, targetObject.transform, true);
                hostile.AdvanceForVerification(0.3f);
                Assert.That(game.Health, Is.EqualTo(health - 6), "Enemy projectile did not damage the actual player on arrival.");
            }
            finally { CleanupBullets(); PrototypeInput.ClearScriptedFrame(); Time.timeScale = 1f; Object.DestroyImmediate(root); Object.DestroyImmediate(actorObject); Object.DestroyImmediate(targetObject); Object.DestroyImmediate(wall); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerFireAndReloadInputs_DriveAmmoAndViewAnimationTogether()
        {
            var root = new GameObject("Player animated weapon test"); var actorObject = Player(root.transform);
            try
            {
                var actor = actorObject.GetComponent<PrototypeActor>();
                var camera = Child(actorObject.transform, "Main Camera", new Vector3(0f, 0.65f, 0f));
                camera.gameObject.tag = "MainCamera"; camera.gameObject.AddComponent<Camera>(); actor.view = camera;
                var view = Child(camera, "View weapon", new Vector3(0.27f, -0.24f, 0.45f)); actor.weaponView = view;
                var motion = Child(view, "Weapon Motion", Vector3.zero);
                Child(motion, "Muzzle", Vector3.forward * 0.81f);
                Child(motion, "Yard.Rubber.Weapon magazine", new Vector3(0f, -0.14f, 0.05f));
                Child(motion, "Yard.Skin.Supporting hand", new Vector3(-0.04f, -0.09f, 0.25f));
                var pose = motion.gameObject.AddComponent<PrototypeCombatAnimation>(); pose.firstPerson = true;
                var game = root.AddComponent<PrototypeGame>(); game.templateId = "first_person_action"; game.requiredObjectives = 1; game.player = actor;
                yield return null; yield return StartGame();
                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(fireHeld: true)); yield return null;
                PrototypeInput.SetScriptedFrame(default); yield return null;
                Assert.That(actor.AmmoInMagazine, Is.EqualTo(11)); Assert.That(pose.ShotAnimations, Is.EqualTo(1));
                Assert.That(PrototypeProjectile.ActiveCount, Is.GreaterThan(0));
                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(reloadPressed: true, fireHeld: true)); yield return null;
                PrototypeInput.SetScriptedFrame(default); yield return null;
                Assert.That(actor.IsReloading, Is.True); Assert.That(pose.ReloadAnimations, Is.EqualTo(1));
                Assert.That(actor.AmmoInMagazine, Is.EqualTo(11)); Assert.That(pose.ShotAnimations, Is.EqualTo(1));
                yield return new WaitForSeconds(1.4f);
                Assert.That(actor.IsReloading, Is.False); Assert.That(actor.AmmoInMagazine, Is.EqualTo(12));
                Assert.That(actor.ReserveAmmo, Is.EqualTo(47));
            }
            finally { CleanupBullets(); PrototypeInput.ClearScriptedFrame(); Time.timeScale = 1f; Object.DestroyImmediate(actorObject); Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArmedEnemy_RespectsCoverFiresReloadsAndPauses()
        {
            var root = new GameObject("Armed enemy runtime test"); var actorObject = Player(root.transform);
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule); var rig = Rig();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                enemy.transform.position = new Vector3(0f, 1f, 9f); enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var target = enemy.AddComponent<PrototypeObjective>(); target.kind = ObjectiveKind.Target; target.health = 100;
                rig.transform.SetParent(enemy.transform, false); rig.AddComponent<PrototypeCombatAnimation>();
                var ranged = enemy.AddComponent<PrototypeRangedEnemy>();
                wall.transform.position = new Vector3(0f, 1f, 4f); wall.transform.localScale = new Vector3(8f, 4f, 0.1f);
                var game = root.AddComponent<PrototypeGame>(); game.templateId = "first_person_action"; game.requiredObjectives = 2; game.player = actorObject.GetComponent<PrototypeActor>();
                yield return null; yield return StartGame(); Physics.SyncTransforms();
                yield return new WaitForSeconds(2f);
                Assert.That(ranged.ShotsFired, Is.EqualTo(0), "Enemy fired through solid cover.");
                wall.SetActive(false); Physics.SyncTransforms();
                var deadline = Time.realtimeSinceStartup + 8f;
                while (ranged.ReloadCount == 0 && Time.realtimeSinceStartup < deadline) yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(ranged.ShotsFired, Is.EqualTo(4)); Assert.That(ranged.ReloadCount, Is.EqualTo(1)); Assert.That(ranged.Reloading, Is.True);
                var shots = ranged.ShotsFired; yield return new WaitForSeconds(0.25f);
                Assert.That(ranged.ShotsFired, Is.EqualTo(shots), "Enemy fired during reload.");
                PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(pausePressed: true)); yield return null;
                PrototypeInput.SetScriptedFrame(default); yield return null;
                Assert.That(game.MenuOpen, Is.True); var before = enemy.transform.position;
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(ranged.ShotsFired, Is.EqualTo(shots)); Assert.That(enemy.transform.position, Is.EqualTo(before));
            }
            finally { CleanupBullets(); PrototypeInput.ClearScriptedFrame(); Time.timeScale = 1f; Object.DestroyImmediate(root); Object.DestroyImmediate(actorObject); Object.DestroyImmediate(enemy); Object.DestroyImmediate(wall); }
            yield return null;
        }

        private static IEnumerator StartGame()
        {
            PrototypeInput.SetScriptedFrame(new PrototypeInputFrame(confirmPressed: true)); yield return null;
            PrototypeInput.SetScriptedFrame(default); yield return null;
        }
        private static GameObject Player(Transform environment)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground combat test";
            ground.transform.SetParent(environment, false); ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            var value = GameObject.CreatePrimitive(PrimitiveType.Capsule); value.transform.position = Vector3.up;
            Object.DestroyImmediate(value.GetComponent<Collider>()); value.AddComponent<CharacterController>();
            value.AddComponent<PrototypeActor>().mode = PrototypeMode.FirstPersonAction; return value;
        }
        private static GameObject Rig()
        {
            var value = new GameObject("Combat rig test");
            foreach (var side in new[] { "Left", "Right" }) Child(Child(value.transform, side + "Hip", Vector3.zero), "Knee", Vector3.zero);
            var spine = Child(value.transform, "Spine", Vector3.zero); Child(spine, "LeftShoulder", Vector3.zero);
            var gun = Child(spine, "Gun", new Vector3(0.18f, 0.3f, 0.48f)); Child(gun, "Muzzle", new Vector3(0f, 0f, 0.6f));
            Child(gun, "Yard.Rubber.Enemy magazine", new Vector3(0f, -0.13f, 0.06f)); return value;
        }
        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var value = new GameObject(name); value.transform.SetParent(parent, false); value.transform.localPosition = position; return value.transform;
        }
        private static void CleanupBullets()
        {
            foreach (var bullet in Object.FindObjectsByType<PrototypeProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(bullet.gameObject);
        }
    }
}
""";
}
