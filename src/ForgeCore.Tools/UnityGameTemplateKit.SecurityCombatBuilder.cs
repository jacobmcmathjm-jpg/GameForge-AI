namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SecurityCombatBuilderSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreSecurityCombatBuilder
    {
        public const string Recipe="security_combat_lab_v1";
        private static Mesh presentationBevel;
        [Serializable]private sealed class Report
        {
            public int schemaVersion=1,enemies=3,gridWidth=32,gridHeight=32,sourceTriangles,armsTriangles;
            public string recipe=Recipe,assetPath,assetSha256,armsMeshPath;
            public string navigation="bounded_4_neighbour_static_grid_with_character_controller_collision";
            public string weapon="original_unity_authored_modular_training_rifle";
            public string arms="extracted_original_blender_skin_with_generic_action_controller";
            public string visualProfile=SecurityVisualPalette.Profile;
            public string[] visualAssets=SecurityVisualPalette.Artifacts;
            public int materialSlotCount,textureSize=SecurityVisualPalette.TextureSize,ownedTextureCount=7,reflectionFaceSize=64;
            public long ownedTextureMemoryBytes;
            public bool visualReviewed=false,rightsReviewed=false,stageComplete=false;
        }
        public static string Build(string assetPath,string hash,string id,string scenePath,string outputPath)
        {
            // Existing builder validates hash/paths, refuses all occupied outputs, and measures all eight clips.
            ForgeCoreCharacterAnimationBuilder.Build(assetPath,hash,id,scenePath,outputPath);
            var scene=SceneManager.GetActiveScene();foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);
            var folder="Assets/ForgeCore/CharacterAnimations/"+id;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(folder+"/Mannequin.controller");
            var palette=SecurityVisualPalette.Create(folder);presentationBevel=palette.Bevel;
            var metal=palette.Metal;var polymer=palette.Polymer;var amber=palette.Amber;var concrete=palette.Concrete;
            var session=new GameObject("Security combat session").AddComponent<SecurityCombatSession>();
            var ground=Block("Lab ground",new Vector3(0,-.12f,0),new Vector3(34,.24f,34),concrete);
            ground.layer=8;
            ground.GetComponent<Renderer>().sharedMaterial=palette.Floor;
            foreach(var x in new[]{-16.5f,16.5f})Block("Solid side boundary",new Vector3(x,1.5f,0),new Vector3(.5f,3,33),palette.Wall).layer=8;
            foreach(var z in new[]{-16.5f,16.5f})Block("Solid end boundary",new Vector3(0,1.5f,z),new Vector3(33,3,.5f),palette.Wall).layer=8;
            // Two staggered solid dividers force actual route planning around cover.
            Block("Solid cover A",new Vector3(-3,1.05f,-1),new Vector3(10,2.1f,1.2f),concrete).layer=8;
            Block("Solid cover B",new Vector3(5,1.05f,5),new Vector3(8,2.1f,1.2f),concrete).layer=8;
            Block("Low firing cover",new Vector3(-8,.5f,-7),new Vector3(3,1,1),concrete).layer=8;
            var playerObject=new GameObject("Player");playerObject.transform.position=new Vector3(0,.02f,-12);
            var pc=playerObject.AddComponent<CharacterController>();Configure(pc);
            var player=playerObject.AddComponent<SecurityCombatPlayer>();session.player=player;
            var camera=new GameObject("First-person camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,1.60f,0);
            camera.nearClipPlane=.025f;camera.fieldOfView=72;camera.backgroundColor=new Color(.09f,.12f,.15f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();player.view=camera.transform;
            var firstPerson=Visual(source,controller,palette);firstPerson.name="Original skinned first-person arms";
            firstPerson.transform.SetParent(camera.transform,false);
            firstPerson.transform.localScale=Vector3.one;firstPerson.transform.localPosition=new Vector3(.18f,-1.75f,.20f);
            SetLayer(firstPerson,9);
            var skin=firstPerson.GetComponentInChildren<SkinnedMeshRenderer>();
            var armsMesh=UnityEngine.Object.Instantiate(skin.sharedMesh);armsMesh.name="S05 original sleeve and glove skin";
            var vertices=armsMesh.vertices;var weights=armsMesh.boneWeights;
            bool IsArm(int vertex)
            {
                var w=weights[vertex];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                var sum=0f;for(var i=0;i<4;i++){var name=skin.bones[ids[i]].name;if(name.StartsWith("LowerArm.",StringComparison.Ordinal)||name.StartsWith("Hand.",StringComparison.Ordinal))sum+=values[i];}
                return sum>.75f;
            }
            var sourceTriangles=armsMesh.triangles.Length/3;var armTriangles=0;
            for(var sub=0;sub<armsMesh.subMeshCount;sub++)
            {
                var indices=armsMesh.GetTriangles(sub);var filtered=new List<int>();
                for(var i=0;i<indices.Length;i+=3)if(IsArm(indices[i])&&IsArm(indices[i+1])&&IsArm(indices[i+2])){filtered.Add(indices[i]);filtered.Add(indices[i+1]);filtered.Add(indices[i+2]);}
                armsMesh.SetTriangles(filtered,sub);armTriangles+=filtered.Count/3;
            }
            if(armTriangles<100||armTriangles>=sourceTriangles)throw new InvalidOperationException("First-person extraction did not isolate a usable original arm skin.");
            armsMesh.RecalculateBounds();AssetDatabase.CreateAsset(armsMesh,folder+"/FirstPersonArms.asset");skin.sharedMesh=armsMesh;
            var playerMotion=firstPerson.AddComponent<PrototypeSkinnedAnimator>();Configure(playerMotion,firstPerson);
            player.motion=playerMotion;player.arms=firstPerson.transform;
            Transform playerMagazine;playerMotion.muzzle=Rifle(firstPerson,folder,metal,polymer,amber,out playerMagazine);player.magazine=playerMagazine;
            // Camera-owned weapon avoids wrist-rotation drift. Final two-bone contacts follow its grip sockets.
            player.weapon=playerMotion.muzzle.parent;player.weapon.SetParent(camera.transform,false);
            player.weapon.localPosition=new Vector3(.18f,-.17f,.60f);player.weapon.localRotation=Quaternion.identity;player.weapon.localScale=Vector3.one;
            player.sight=player.weapon.Find("Sight aim point");
            if(player.sight==null)throw new InvalidOperationException("Training rifle is missing its sight alignment socket.");
            player.rightGrip=new GameObject("Right-hand weapon contact").transform;player.rightGrip.SetParent(player.weapon,false);player.rightGrip.localPosition=new Vector3(0,-.095f,-.065f);
            player.leftGrip=new GameObject("Left-hand magazine contact").transform;player.leftGrip.SetParent(playerMagazine,false);player.leftGrip.localPosition=new Vector3(0,0,0);
            session.enemies=new SecurityCombatEnemy[3];
            var starts=new[]{new Vector3(1,.02f,-5),new Vector3(0,.02f,12),new Vector3(10,.02f,10)};
            for(var i=0;i<3;i++)
            {
                var go=new GameObject("Security guard "+(i+1));go.transform.position=starts[i];
                var cc=go.AddComponent<CharacterController>();Configure(cc);
                var enemy=go.AddComponent<SecurityCombatEnemy>();session.enemies[i]=enemy;
                var visual=Visual(source,controller,palette);visual.transform.SetParent(go.transform,false);
                var motion=visual.AddComponent<PrototypeSkinnedAnimator>();Configure(motion,visual);enemy.motion=motion;
                Transform magazine;motion.muzzle=Rifle(visual,folder,metal,polymer,amber,out magazine);
                enemy.weapon=motion.muzzle.parent;enemy.weapon.SetParent(visual.transform,true);enemy.magazine=magazine;
                // Keep both live grips within the original 0.50m two-bone reach, including magazine withdrawal.
                enemy.weapon.position=visual.transform.TransformPoint(new Vector3(0,1.38f,.28f));
                enemy.rightGrip=new GameObject("Guard right-hand contact").transform;enemy.rightGrip.SetParent(enemy.weapon,false);enemy.rightGrip.localPosition=new Vector3(0,-.095f,-.065f);
                // A stable firing grip is distinct from the moving reload magazine.
                enemy.leftGrip=new GameObject("Guard foregrip contact").transform;enemy.leftGrip.SetParent(enemy.weapon,false);
                enemy.leftGrip.localPosition=new Vector3(0,0,.13f);
            }
            palette.Stage(camera);
            var report=new Report{assetPath=assetPath,assetSha256=hash,sourceTriangles=sourceTriangles,armsTriangles=armTriangles,armsMeshPath=folder+"/FirstPersonArms.asset",
                materialSlotCount=source.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.Length,ownedTextureMemoryBytes=palette.OwnedTextureBytes};
            File.WriteAllText(folder+"/combat-report.json",JsonUtility.ToJson(report,true));
            var labRoot=new GameObject("Security combat lab");
            foreach(var item in scene.GetRootGameObjects())if(item!=labRoot)item.transform.SetParent(labRoot.transform,true);
            if(PrefabUtility.SaveAsPrefabAsset(labRoot,folder+"/CombatLab.prefab")==null)throw new InvalidOperationException("Combat fixture prefab save failed.");
            AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Combat scene save failed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "Fresh isolated security combat lab saved: original Blender character, extracted skinned arms ("+armTriangles+" visible triangles), original Unity modular rifle, three animated guards, static-grid routes, swept damage, cover checks, pause/death/reset. Technical fixture only; visual/play-feel/performance and rights acceptance remain open.";
        }
        private static void Configure(CharacterController c){c.height=1.8f;c.radius=.32f;c.center=Vector3.up*.9f;c.stepOffset=.2f;c.skinWidth=.03f;}
        private static void Configure(PrototypeSkinnedAnimator motion,GameObject visual)
        {
            motion.animator=visual.GetComponent<Animator>();motion.automaticDemo=false;motion.showControls=false;
            motion.driveRootTranslation=false;motion.emitTrainingProjectiles=false;
        }
        private static GameObject Visual(GameObject source,RuntimeAnimatorController controller,SecurityVisualPalette palette)
        {
            var value=UnityEngine.Object.Instantiate(source);value.name="Original industrial guard skin";
            var animator=value.GetComponent<Animator>();if(animator==null)animator=value.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in value.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;}
            palette.Character(value);
            return value;
        }
        private static Transform Rifle(GameObject visual,string folder,Material metal,Material polymer,Material amber,out Transform magazine)
        {
            // Bind the world-space two-hand reference pose before computing local socket offsets.
            var aim=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/Aim.anim");aim.SampleAnimation(visual,0);
            var hand=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand.R");
            var weapon=new GameObject("Original modular training rifle").transform;
            weapon.SetPositionAndRotation(visual.transform.TransformPoint(new Vector3(.10f,1.23f,.51f)),visual.transform.rotation);
            weapon.SetParent(hand,true);
            weapon.localScale=Vector3.one;weapon.gameObject.layer=hand.gameObject.layer;
            Part(weapon,"Receiver",new Vector3(0,0,0),new Vector3(.065f,.095f,.27f),metal);
            Cylinder(weapon,"Round barrel",new Vector3(0,.012f,.24f),.016f,.26f,metal);
            Cylinder(weapon,"Muzzle sleeve",new Vector3(0,.012f,.365f),.023f,.04f,metal);
            Cylinder(weapon,"Dark muzzle bore",new Vector3(0,.012f,.387f),.014f,.004f,polymer);
            Part(weapon,"Stock",new Vector3(0,-.02f,-.22f),new Vector3(.06f,.10f,.17f),polymer);
            Part(weapon,"Butt plate",new Vector3(0,-.02f,-.31f),new Vector3(.075f,.13f,.025f),polymer);
            Part(weapon,"Hand guard",new Vector3(0,.0f,.135f),new Vector3(.077f,.09f,.13f),polymer);
            Part(weapon,"Grip",new Vector3(0,-.095f,-.065f),new Vector3(.055f,.12f,.055f),polymer).localRotation=Quaternion.Euler(-12,0,0);
            magazine=Part(weapon,"Animated detachable magazine",new Vector3(0,-.11f,.015f),new Vector3(.045f,.15f,.07f),metal);
            for(var i=0;i<7;i++)Part(weapon,"Foregrip rib "+i,new Vector3(0,0,.085f+i*.018f),new Vector3(.084f,.10f,.006f),metal);
            Part(weapon,"Sight base",new Vector3(0,.06f,-.015f),new Vector3(.042f,.025f,.13f),metal);
            Part(weapon,"Sight left post",new Vector3(-.024f,.095f,-.02f),new Vector3(.007f,.046f,.035f),polymer);
            Part(weapon,"Sight right post",new Vector3(.024f,.095f,-.02f),new Vector3(.007f,.046f,.035f),polymer);
            Part(weapon,"Sight top bridge",new Vector3(0,.117f,-.02f),new Vector3(.055f,.007f,.035f),polymer);
            Part(weapon,"Amber sight dot",new Vector3(0,.095f,-.043f),new Vector3(.004f,.004f,.003f),amber);
            var sight=new GameObject("Sight aim point").transform;sight.SetParent(weapon,false);sight.localPosition=new Vector3(0,.095f,-.043f);
            Part(weapon,"Ejection recess",new Vector3(.034f,.008f,.005f),new Vector3(.004f,.035f,.075f),polymer);
            Part(weapon,"Bolt plate",new Vector3(.038f,.008f,-.003f),new Vector3(.006f,.024f,.043f),metal);
            Part(weapon,"Trigger guard bottom",new Vector3(0,-.108f,-.13f),new Vector3(.027f,.009f,.07f),metal);
            Part(weapon,"Trigger guard rear",new Vector3(0,-.075f,-.16f),new Vector3(.027f,.066f,.009f),metal);
            Part(weapon,"Trigger",new Vector3(0,-.072f,-.115f),new Vector3(.009f,.04f,.012f),polymer);
            for(var i=0;i<5;i++) {
                Part(magazine,"Magazine flute left "+i,new Vector3(-.52f,-.36f+i*.17f,0),new Vector3(.07f,.035f,.82f),polymer);
                Part(magazine,"Magazine flute right "+i,new Vector3(.52f,-.36f+i*.17f,0),new Vector3(.07f,.035f,.82f),polymer);
            }
            var muzzle=new GameObject("Evaluated muzzle").transform;muzzle.SetParent(weapon,false);muzzle.localPosition=new Vector3(0,.012f,.39f);
            return muzzle;
        }
        private static Transform Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=Block(name,Vector3.zero,size,material);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshFilter>().sharedMesh=presentationBevel;
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.layer=parent.gameObject.layer;return go.transform;
        }
        private static Transform Cylinder(Transform parent,string name,Vector3 position,float radius,float length,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(90,0,0);
            go.transform.localScale=new Vector3(radius*2,length*.5f,radius*2);go.layer=parent.gameObject.layer;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        private static GameObject Block(string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static Material Material(string path,Color color,float metallic,float smoothness)
        {
            var shader=Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Built-in Standard shader required.");
            var m=new Material(shader){color=color};m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smoothness);
            AssetDatabase.CreateAsset(m,path);return m;
        }
        private static void SetLayer(GameObject go,int layer){foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
    }
}
""";
}
