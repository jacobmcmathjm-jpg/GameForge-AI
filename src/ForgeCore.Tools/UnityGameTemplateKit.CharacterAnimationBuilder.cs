namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string CharacterAnimationBuilderSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeCoreGameTemplates.Editor
{
    // Original, deterministic curve authoring, not downloaded mocap or an arbitrary motion model.
    public static class ForgeCoreCharacterAnimationBuilder
    {
        public const string Recipe = ForgeCore.Motion.GuardMotionProfile.Recipe;
        private static readonly string[] Names={"Idle","Walk","Run","Aim","Fire","Reload","Hit","Death"};
        private static readonly float[] Durations={2,1,.6f,1,.25f,1.6f,.4f,1.2f};
        [Serializable] private sealed class Report
        {
            public int schemaVersion=1,clipCount=8,samplesPerClip=31;
            public string recipe=Recipe,assetPath,assetSha256,rootMotionPolicy="runtime_translation_in_place_clips",source="original_parameterized_unity_curves";
            public bool finite=true,visualReviewed=false,rightsReviewed=false;
            public float maximumEdgeStretch,maximumLoopError,maximumStanceSlip;
            public string[] clips=Names;
        }
        private sealed class Bind
        {
            public Transform t;public Vector3 position,worldPosition;public Quaternion rotation;
            public Bind(Transform value){t=value;position=value.localPosition;worldPosition=value.position;rotation=value.localRotation;}
        }
        private static Dictionary<string,Transform> Bones(GameObject root)
        {
            var b=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(!b.ContainsKey(t.name))b.Add(t.name,t);else throw new InvalidOperationException("Duplicate skeleton node name.");
            foreach(var n in new[]{"Hips","Chest","Head","UpperArm.L","LowerArm.L","Hand.L","UpperArm.R","LowerArm.R","Hand.R","UpperLeg.L","LowerLeg.L","Foot.L","Toe.L","UpperLeg.R","LowerLeg.R","Foot.R","Toe.R"})
                if(!b.ContainsKey(n))throw new InvalidOperationException("Unsupported Generic skeleton: "+n);
            return b;
        }
        private static void Restore(Bind[] bind){foreach(var p in bind){p.t.localPosition=p.position;p.t.localRotation=p.rotation;}}
        private static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 bend)
        {
            var a=Vector3.Distance(upper.position,lower.position);var b=Vector3.Distance(lower.position,end.position);
            var delta=target-upper.position;var d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);var axis=delta.normalized;
            var along=(a*a-b*b+d*d)/(2*d);var height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var across=Vector3.ProjectOnPlane(bend,axis).normalized;
            var knee=upper.position+axis*along+across*height;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,knee-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
        }
        private static void Pose(GameObject root,Dictionary<string,Transform> b,Bind[] bind,int clip,float t)
        {
            Restore(bind);var hips=b["Hips"];var cycle=t*Mathf.PI*2;
            var profile=ForgeCore.Motion.GuardMotionProfile.Sample((ForgeCore.Motion.GuardMotionClip)clip,t);
            var moving=clip==1||clip==2;var speed=clip==2?2f:1f;var period=Durations[clip];var stance=clip==2?.4f:.6f;
            hips.position+=root.transform.TransformVector(new Vector3(profile.PelvisSide,profile.PelvisHeight,profile.PelvisForward));
            hips.localRotation*=Quaternion.Euler(profile.PelvisPitch,profile.PelvisYaw,profile.PelvisRoll);
            b["Chest"].localRotation*=Quaternion.Euler(profile.ChestPitch,profile.ChestYaw,profile.ChestRoll);
            b["Head"].localRotation*=Quaternion.Euler(profile.HeadPitch,profile.HeadYaw,0);
            foreach(var side in new[]{"L","R"})
            {
                if(clip!=7)
                {
                    var foot=b["Foot."+side];var toe=b["Toe."+side];var fr=foot.rotation;var tr=toe.rotation;var target=bind.First(p=>p.t==foot).worldPosition;
                    if(moving)
                    {
                        var phase=Mathf.Repeat(t+(side=="L"?0:.5f),1);var half=speed*period*stance*.5f;
                        if(phase<stance)target.z+=half-speed*period*phase;
                        else{var u=(phase-stance)/(1-stance);target.z+=Mathf.Lerp(-half,half,ForgeCore.Motion.GuardMotionProfile.Ease(u));target.y+=(clip==2?.16f:.10f)*ForgeCore.Motion.GuardMotionProfile.SwingLift(u);}
                        // World ankle height is retained while the hips bob; IK fixes feet, not just bone angles.
                        Solve(b["UpperLeg."+side],b["LowerLeg."+side],foot,target,root.transform.forward);
                        foot.rotation=fr;toe.rotation=tr;
                    }
                    var hand=b["Hand."+side];var sign=Mathf.Sign(b["UpperArm."+side].position.x-root.transform.position.x);
                    var carry=clip==0||moving;
                    var handTarget=root.transform.TransformPoint(new Vector3(sign*.13f,carry?1.10f:1.23f,side=="R"?(carry?.30f:.39f):(carry?.44f:.53f)));
                    if(moving)handTarget+=root.transform.up*(.015f*Mathf.Sin(cycle));
                    if(clip==4)handTarget-=root.transform.forward*(.045f*profile.Recoil);
                    if(clip==5&&side=="L")handTarget+=root.transform.TransformVector(new Vector3(.04f,-.18f,-.15f))*profile.ReloadReach;
                    if(clip==6)handTarget+=root.transform.up*(.025f*Mathf.Sin(Mathf.PI*t));
                    Solve(b["UpperArm."+side],b["LowerArm."+side],hand,handTarget,-root.transform.up+root.transform.right*sign*.4f);
                }
                else
                {
                    // Blend out of the carried weapon pose instead of instantly flinging both arms sideways.
                    var release=ForgeCore.Motion.GuardMotionProfile.Ease(Mathf.Clamp01((t-.04f)/.52f));
                    var hand=b["Hand."+side];var sign=side=="L"?-1f:1f;
                    var bodyFrame=root.transform.rotation*Quaternion.Euler(profile.PelvisPitch,profile.PelvisYaw,profile.PelvisRoll);
                    var relaxed=hips.position+bodyFrame*new Vector3(sign*.22f,side=="L"?.12f:.05f,side=="L"?.10f:.035f);
                    var carry=root.transform.TransformPoint(new Vector3(sign*.13f,1.23f,side=="R"?.39f:.53f));
                    Solve(b["UpperArm."+side],b["LowerArm."+side],hand,Vector3.Lerp(carry,relaxed,release),-root.transform.up+root.transform.right*sign*.4f);
                    b["UpperLeg."+side].localRotation*=Quaternion.Euler(-profile.KneeFold*.3f,0,side=="L"?-3*release:2*release);
                    b["LowerLeg."+side].localRotation*=Quaternion.Euler(profile.KneeFold*(side=="L"?1:.7f),0,0);
                }
            }
        }
        private static AnimationClip MakeClip(GameObject root,Dictionary<string,Transform> b,Bind[] bind,int index)
        {
            const int frames=60;var duration=Durations[index];var clip=new AnimationClip{name=Names[index],frameRate=60,legacy=false};
            var curves=new Dictionary<string,AnimationCurve[]>();
            foreach(var p in bind)if(p.t!=root.transform)curves.Add(AnimationUtility.CalculateTransformPath(p.t,root.transform),new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
            var hipCurves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            for(var frame=0;frame<=frames;frame++)
            {
                Pose(root,b,bind,index,frame/(float)frames);var time=duration*frame/frames;
                foreach(var p in bind)if(p.t!=root.transform)
                {var q=p.t.localRotation;var c=curves[AnimationUtility.CalculateTransformPath(p.t,root.transform)];c[0].AddKey(time,q.x);c[1].AddKey(time,q.y);c[2].AddKey(time,q.z);c[3].AddKey(time,q.w);}
                var v=b["Hips"].localPosition;hipCurves[0].AddKey(time,v.x);hipCurves[1].AddKey(time,v.y);hipCurves[2].AddKey(time,v.z);
            }
            foreach(var entry in curves)for(var c=0;c<4;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(entry.Key,typeof(Transform),"m_LocalRotation."+"xyzw"[c]),entry.Value[c]);
            for(var c=0;c<3;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(b["Hips"],root.transform),typeof(Transform),"m_LocalPosition."+"xyz"[c]),hipCurves[c]);
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=index<=3;AnimationUtility.SetAnimationClipSettings(clip,settings);Restore(bind);return clip;
        }
        public static string Build(string assetPath,string hash,string id,string scenePath,string outputPath)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(id??"","^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$")||id.Contains(".."))throw new InvalidOperationException("Invalid animation id.");
            var folder="Assets/ForgeCore/CharacterAnimations/"+id;
            foreach(var p in new[]{folder,folder+".meta",scenePath,scenePath+".meta",outputPath,outputPath+".forgecore-rig-preview.json"})if(File.Exists(p)||Directory.Exists(p))throw new InvalidOperationException("Animation output exists; no overwrite.");
            if(!assetPath.StartsWith("Assets/ForgeCore/Generated/",StringComparison.Ordinal)||!assetPath.EndsWith(".glb",StringComparison.Ordinal)||assetPath.Contains(".."))throw new InvalidOperationException("Unsafe character source.");
            var actual=BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(assetPath))).Replace("-","").ToLowerInvariant();if(actual!=hash)throw new InvalidOperationException("Character hash changed.");
            var scene=SceneManager.GetActiveScene();if(!string.IsNullOrEmpty(scene.path)||scene.GetRootGameObjects().Length!=0)throw new InvalidOperationException("An empty batch scene is required.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);if(prefab==null)throw new InvalidOperationException("Character importer unavailable.");
            var root=UnityEngine.Object.Instantiate(prefab);root.name="S03 animated training mannequin";
            var animator=root.GetComponent<Animator>();if(animator==null)animator=root.AddComponent<Animator>();
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);if(skins.Length!=1||!skins[0].sharedMesh.isReadable)throw new InvalidOperationException("One readable skin required.");
            var skin=skins[0];skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;
            var bones=Bones(root);var bind=root.GetComponentsInChildren<Transform>(true).Select(t=>new Bind(t)).ToArray();
            var clips=new AnimationClip[8];for(var i=0;i<8;i++)clips[i]=MakeClip(root,bones,bind,i);
            var report=Measure(root,skin,bones,bind,clips,hash);report.assetPath=assetPath;
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var clip in clips)AssetDatabase.CreateAsset(clip,folder+"/"+clip.name+".anim");
            var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Mannequin.controller");
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("Mode",AnimatorControllerParameterType.Int);
            var machine=controller.layers[0].stateMachine;
            var locomotion=machine.AddState("Locomotion");machine.defaultState=locomotion;
            var blend=new BlendTree{name="Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            AssetDatabase.AddObjectToAsset(blend,controller);blend.AddChild(clips[0],0);blend.AddChild(clips[1],1);blend.AddChild(clips[2],2);locomotion.motion=blend;
            var states=new[]{locomotion,machine.AddState("Aim"),machine.AddState("Fire"),machine.AddState("Reload"),machine.AddState("Hit"),machine.AddState("Death")};
            for(var i=1;i<states.Length;i++)states[i].motion=clips[i+2];
            for(var i=0;i<states.Length;i++)
            {states[i].writeDefaultValues=true;var transition=machine.AddAnyStateTransition(states[i]);transition.hasExitTime=false;transition.duration=i==2?.035f:.1f;transition.hasFixedDuration=true;transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.Equals,i,"Mode");}
            Pose(root,bones,bind,3,0);
            var rifle=GameObject.CreatePrimitive(PrimitiveType.Cube);rifle.name="Original technical training rifle";UnityEngine.Object.DestroyImmediate(rifle.GetComponent<Collider>());
            rifle.transform.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(.10f,1.23f,.51f)),root.transform.rotation);rifle.transform.localScale=new Vector3(.06f,.10f,.48f);
            rifle.transform.SetParent(bones["Hand.R"],true);
            var muzzle=new GameObject("Muzzle").transform;muzzle.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(.10f,1.23f,.77f)),root.transform.rotation);muzzle.SetParent(bones["Hand.R"],true);
            Restore(bind);animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var driver=root.AddComponent<PrototypeSkinnedAnimator>();driver.animator=animator;driver.muzzle=muzzle;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Animation lab floor";floor.transform.position=new Vector3(0,-.08f,3);floor.transform.localScale=new Vector3(12,.16f,18);
            var camera=new GameObject("Animation lab camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.075f,.09f);camera.transform.position=new Vector3(6,4,11);camera.transform.LookAt(new Vector3(0,.8f,2.3f));camera.nearClipPlane=.1f;
            var light=new GameObject("Animation lab key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-140,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.4f,.45f);
            Sheet(root,bind,clips,camera,outputPath);
            Restore(bind);AssetDatabase.SaveAssets();Directory.CreateDirectory(Path.GetDirectoryName(scenePath));if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Animation lab scene save failed.");
            File.WriteAllText(folder+"/animation-report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "Eight original Generic skeletal clips, Idle/Walk/Run blend tree and exclusive action state machine saved. Max sampled edge stretch="+report.maximumEdgeStretch+"; loop error="+report.maximumLoopError+"m; stance displacement residual="+report.maximumStanceSlip+"m. Diagnostic pose sheet only; rendered transition/foot-contact/gameplay acceptance remains open.";
        }
        private static Report Measure(GameObject root,SkinnedMeshRenderer skin,Dictionary<string,Transform> bones,Bind[] bind,AnimationClip[] clips,string hash)
        {
            var report=new Report{assetSha256=hash};var baked=new Mesh();skin.BakeMesh(baked);var rest=baked.vertices;var triangles=skin.sharedMesh.triangles;
            var first=new Vector3[rest.Length];
            for(var clip=0;clip<8;clip++)for(var frame=0;frame<=30;frame++)
            {
                Restore(bind);clips[clip].SampleAnimation(root,clips[clip].length*frame/30f);skin.BakeMesh(baked);var v=baked.vertices;
                for(var j=0;j<v.Length;j++){if(float.IsNaN(v[j].x)||float.IsInfinity(v[j].x)||float.IsNaN(v[j].y)||float.IsInfinity(v[j].y)||float.IsNaN(v[j].z)||float.IsInfinity(v[j].z))throw new InvalidOperationException("Nonfinite animation geometry.");if(frame==0)first[j]=v[j];else if(frame==30&&clip<=3)report.maximumLoopError=Mathf.Max(report.maximumLoopError,Vector3.Distance(first[j],v[j]));}
                for(var j=0;j<triangles.Length;j+=3)for(var e=0;e<3;e++){var a=triangles[j+e];var b=triangles[j+(e+1)%3];var length=Vector3.Distance(rest[a],rest[b]);if(length>.00001f)report.maximumEdgeStretch=Mathf.Max(report.maximumEdgeStretch,Vector3.Distance(v[a],v[b])/length);}
                if(clip==1||clip==2)
                {
                    var speed=clip==1?1f:2f;var stance=clip==1?.6f:.4f;var t=frame/30f;
                    foreach(var side in new[]{"L","R"}){var phase=Mathf.Repeat(t+(side=="L"?0:.5f),1);if(phase>=stance)continue;
                        var expected=(speed*clips[clip].length*stance*.5f)-speed*clips[clip].length*phase;
                        var bindFoot=bind.First(p=>p.t==bones["Foot."+side]);
                        // Compare actual world ankle against its rest position plus analytically planted displacement.
                        var actual=bones["Foot."+side].position;Pose(root,bones,bind,0,0);var reference=bones["Foot."+side].position;
                        report.maximumStanceSlip=Mathf.Max(report.maximumStanceSlip,Mathf.Abs(actual.z-reference.z-expected));
                        Restore(bind);clips[clip].SampleAnimation(root,clips[clip].length*t);
                    }
                }
            }
            Restore(bind);UnityEngine.Object.DestroyImmediate(baked);
            if(report.maximumLoopError>.002f)throw new InvalidOperationException("Animation loop seam exceeds 2mm.");
            // Stretch/slip measurements are retained, never silently classified as art acceptance.
            return report;
        }
        private static void Sheet(GameObject root,Bind[] bind,AnimationClip[] clips,Camera camera,string output)
        {
            const int w=480,h=520;var atlas=new Texture2D(w*4,h*2,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var position=camera.transform.position;var rotation=camera.transform.rotation;camera.transform.position=new Vector3(2.7f,1.9f,4);camera.transform.LookAt(new Vector3(0,.85f,0));camera.orthographic=true;camera.orthographicSize=1.2f;
            var animator=root.GetComponent<Animator>();animator.enabled=false;
            // Camera.Render can reuse this frame's GPU skin cache after SampleAnimation. Bake the actual
            // evaluated skeleton to a temporary render mesh for each tile; never present stale skin as proof.
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();var display=new GameObject("Temporary evaluated pose");
            display.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);display.transform.localScale=skin.transform.lossyScale;
            display.AddComponent<MeshFilter>().sharedMesh=baked;display.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
            try
            {
                for(var i=0;i<8;i++)
                {
                    Restore(bind);clips[i].SampleAnimation(root,clips[i].length*(i==7?.95f:.35f));skin.BakeMesh(baked);var render=new RenderTexture(w,h,24);var tile=new Texture2D(w,h,TextureFormat.RGB24,false);
                    try{render.Create();camera.aspect=w/(float)h;camera.targetTexture=render;RenderTexture.active=render;camera.Render();tile.ReadPixels(new Rect(0,0,w,h),0,0);tile.Apply();atlas.SetPixels(i%4*w,(1-i/4)*h,w,h,tile.GetPixels());}
                    finally{camera.targetTexture=null;RenderTexture.active=previous;render.Release();UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(tile);}
                }
                atlas.Apply();Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,atlas.EncodeToPNG());
            }
            finally{skin.enabled=true;UnityEngine.Object.DestroyImmediate(display);UnityEngine.Object.DestroyImmediate(baked);animator.enabled=true;Restore(bind);camera.orthographic=false;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(atlas);}
        }
    }
}
""";
}
