namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] IndieCharacterArtifacts=["IndieCharacterMeshes.asset","indie-character-report.json"];
    private const string IndieCharacterSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Original wrist-skinned geometry derivative; no finger bones, clip edits or anatomy acceptance.
    public static class ForgeCoreIndieCharacter
    {
        [Serializable] private sealed class Report {
            public int schemaVersion=1,guardCount=3,skinCount=4,sharedMeshCount=2,guardTriangles,armsTriangles,gloveDigitsPerHand=5,addedColliders,addedRenderers;
            public string profile="original_indie_character_v1",source="original_code_authored_derivative_of_hash_bound_blender_skin",fingerMotion="precurled_rigid_to_existing_wrist";
            public bool skeletonUnchanged,bindposesUnchanged,socketsUnchanged,collisionUnchanged,sourceMeshPreserved,weightsNormalized;
            public bool clipsChanged=false,audioChanged=false,individualFingerAnimation=false,visualReviewed=false,rightsReviewed=false,stageComplete=false;
        }
        private sealed class Writer {
            public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<BoneWeight> w=new List<BoneWeight>();
            public readonly List<int>[] t;private readonly Matrix4x4 restToSkin;
            public Writer(int slots,Matrix4x4 map){t=Enumerable.Range(0,slots).Select(_=>new List<int>()).ToArray();restToSkin=map;}
            public void Triangle(int slot,int a,int b,int c,Vector3 outward){
                if(Vector3.Dot(Vector3.Cross(v[b]-v[a],v[c]-v[a]),restToSkin.MultiplyVector(outward))<0){var temp=b;b=c;c=temp;}
                t[slot].AddRange(new[]{a,b,c});
            }
            // Elliptical swept rings retain a rounded silhouette and measurable separated digits.
            public void Sweep(int slot,int bone,Vector3[] centres,Vector2[] radii,int sides=12) {
                if(centres.Length!=radii.Length||centres.Length<2)throw new InvalidOperationException("Invalid original character profile.");
                var start=v.Count;var tangent=(centres.Last()-centres.First()).normalized;
                var axis=Vector3.Cross(tangent,Mathf.Abs(tangent.y)<.8f?Vector3.up:Vector3.forward).normalized;
                var across=Vector3.Cross(tangent,axis).normalized;
                for(var j=0;j<centres.Length;j++)for(var i=0;i<sides;i++){
                    var angle=i*Mathf.PI*2/sides;v.Add(restToSkin.MultiplyPoint3x4(centres[j]+axis*Mathf.Cos(angle)*radii[j].x+across*Mathf.Sin(angle)*radii[j].y));
                    uv.Add(new Vector2(i/(float)sides,j/(float)(centres.Length-1)));w.Add(new BoneWeight{boneIndex0=bone,weight0=1});
                }
                for(var j=0;j<centres.Length-1;j++)for(var i=0;i<sides;i++){
                    var n=(i+1)%sides;var a=start+j*sides+i;var b=start+j*sides+n;var c=start+(j+1)*sides+n;var d=start+(j+1)*sides+i;
                    var angle=(i+.5f)*Mathf.PI*2/sides;var normal=axis*Mathf.Cos(angle)+across*Mathf.Sin(angle);
                    Triangle(slot,a,b,c,normal);Triangle(slot,a,c,d,normal);
                }
                foreach(var end in new[]{0,centres.Length-1}){
                    var centre=v.Count;v.Add(restToSkin.MultiplyPoint3x4(centres[end]));uv.Add(new Vector2(.5f,.5f));w.Add(new BoneWeight{boneIndex0=bone,weight0=1});
                    for(var i=0;i<sides;i++)Triangle(slot,centre,start+end*sides+i,start+end*sides+(i+1)%sides,tangent*(end==0?-1:1));
                }
            }
            public Mesh Finish(string name,Matrix4x4[] bind) {
                // Discard unused source mitten vertices rather than carrying invisible full-body bounds in the viewmodel.
                var used=t.SelectMany(x=>x).Distinct().OrderBy(i=>i).ToArray();var map=used.Select((id,i)=>new{id,i}).ToDictionary(p=>p.id,p=>p.i);
                var m=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                m.SetVertices(used.Select(i=>v[i]).ToList());m.SetUVs(0,used.Select(i=>uv[i]).ToList());m.boneWeights=used.Select(i=>w[i]).ToArray();m.bindposes=bind;
                m.subMeshCount=t.Length;for(var i=0;i<t.Length;i++)m.SetTriangles(t[i].Select(id=>map[id]).ToArray(),i);
                m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();
                if(m.vertexCount>60000||m.triangles.Length/3>45000||!Finite(m)||!Normalized(m))throw new InvalidOperationException("Character geometry/weight budget failed.");
                return m;
            }
        }
        public static bool Finite(Mesh m)=>m.vertices.All(v=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)));
        public static bool Normalized(Mesh m)=>m.boneWeights.All(w=>w.weight0>=0&&w.weight1>=0&&w.weight2>=0&&w.weight3>=0&&Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.0001f);
        private static Mesh Derivative(SkinnedMeshRenderer skin,Transform visual,bool arms) {
            var source=skin.sharedMesh;var restToSkin=skin.transform.worldToLocalMatrix*visual.localToWorldMatrix;var skinToRest=restToSkin.inverse;
            var writer=new Writer(source.subMeshCount,restToSkin);writer.v.AddRange(source.vertices);writer.uv.AddRange(source.uv);writer.w.AddRange(source.boneWeights);
            if(writer.uv.Count!=writer.v.Count)throw new InvalidOperationException("Existing UV convention missing.");
            int Bone(string name){var i=Array.FindIndex(skin.bones,b=>b.name==name);if(i<0)throw new InvalidOperationException("Original bone missing: "+name);return i;}
            Vector3 Origin(int bone)=>skinToRest.MultiplyPoint3x4(source.bindposes[bone].inverse.MultiplyPoint3x4(Vector3.zero));
            var handBones=new[]{Bone("Hand.L"),Bone("Hand.R")};var origins=handBones.Select(Origin).ToArray();
            bool OldHand(int i){var point=skinToRest.MultiplyPoint3x4(writer.v[i]);return origins.Any(o=>Mathf.Sign(o.x)*point.x>Mathf.Abs(o.x)+.006f);}
            for(var sub=0;sub<source.subMeshCount;sub++){var indices=source.GetTriangles(sub);for(var i=0;i<indices.Length;i+=3)if(!OldHand(indices[i])&&!OldHand(indices[i+1])&&!OldHand(indices[i+2]))writer.t[sub].AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}
            var rubber=Array.FindIndex(skin.sharedMaterials,m=>m!=null&&m.name=="HeroGlove");var cloth=Array.FindIndex(skin.sharedMaterials,m=>m!=null&&m.name=="HeroCloth");
            if(rubber<0||cloth<0)throw new InvalidOperationException("Existing reviewed-route cloth/glove material convention required.");
            for(var side=0;side<2;side++){
                var bone=handBones[side];var o=origins[side];var sign=Mathf.Sign(o.x);
                Vector3 P(float along,float up,float across)=>o+new Vector3(sign*along,up,across);
                // Tapered cuff, palm and separately readable finger forms. Only the wrist is animated.
                writer.Sweep(cloth,bone,new[]{P(-.022f,0,0),P(-.010f,0,0),P(.008f,0,0)},new[]{new Vector2(.043f,.039f),new Vector2(.039f,.035f),new Vector2(.033f,.029f)},16);
                writer.Sweep(rubber,bone,new[]{P(-.006f,0,0),P(.012f,0,0),P(.044f,0,0),P(.058f,-.002f,0)},new[]{new Vector2(.030f,.026f),new Vector2(.037f,.026f),new Vector2(.040f,.027f),new Vector2(.033f,.022f)},16);
                for(var finger=0;finger<4;finger++){
                    var z=-.029f+finger*.019f;var length=finger==0?.047f:finger==3?.042f:.055f;
                    writer.Sweep(rubber,bone,new[]{P(.047f,0,z),P(.064f,.001f,z),P(.064f+length*.35f,-.013f,z),P(.060f+length*.34f,-.032f,z),P(.048f+length*.25f,-.039f,z)},new[]{new Vector2(.009f,.011f),new Vector2(.0095f,.0105f),new Vector2(.0085f,.009f),new Vector2(.0075f,.008f),new Vector2(.0015f,.002f)});
                    // Low-profile padded knuckle ridge, not separate renderer objects.
                    writer.Sweep(cloth,bone,new[]{P(.050f,.025f,z),P(.058f,.028f,z),P(.068f,.022f,z)},new[]{new Vector2(.005f,.002f),new Vector2(.006f,.003f),new Vector2(.003f,.002f)},8);
                }
                writer.Sweep(rubber,bone,new[]{P(.006f,-.008f,.026f),P(.024f,-.020f,.047f),P(.044f,-.036f,.044f),P(.055f,-.038f,.026f)},new[]{new Vector2(.014f,.014f),new Vector2(.013f,.013f),new Vector2(.010f,.011f),new Vector2(.002f,.002f)});
                if(!arms){
                    var suffix=skin.bones[bone].name.EndsWith(".L",StringComparison.Ordinal)?".L":".R";
                    var knee=Bone("LowerLeg"+suffix);var k=Origin(knee);
                    // Rounded articulated kneecap insert, stitched boot shaft and toe cap use existing bones.
                    writer.Sweep(rubber,knee,new[]{k+new Vector3(0,-.047f,.057f),k+new Vector3(0,-.015f,.075f),k+new Vector3(0,.034f,.063f)},new[]{new Vector2(.039f,.006f),new Vector2(.052f,.010f),new Vector2(.035f,.006f)},16);
                    var foot=Bone("Foot"+suffix);var f=Origin(foot);
                    for(var seam=0;seam<3;seam++)writer.Sweep(cloth,foot,new[]{f+new Vector3(-.050f,-.027f-seam*.023f,.052f),f+new Vector3(0,-.028f-seam*.023f,.065f),f+new Vector3(.050f,-.027f-seam*.023f,.052f)},new[]{new Vector2(.002f,.002f),new Vector2(.003f,.003f),new Vector2(.002f,.002f)},8);
                }
            }
            return writer.Finish(arms?"Original indie shaped gloves and sleeve skin":"Original indie guard glove and uniform derivative",source.bindposes);
        }
        public static void Apply(GameObject root,string folder){
            var session=root.GetComponentInChildren<SecurityCombatSession>();if(session==null||session.enemies.Length!=3)throw new InvalidOperationException("Complete original combat rig required.");
            var skins=new[]{session.player.arms.GetComponentInChildren<SkinnedMeshRenderer>()}.Concat(session.enemies.Select(e=>e.motion.GetComponentInChildren<SkinnedMeshRenderer>())).ToArray();
            var originals=skins.Select(s=>s.sharedMesh).Distinct().ToDictionary(m=>m,m=>EditorJsonUtility.ToJson(m));
            var bindings=skins.Select(s=>s.bones.Select(b=>b.name).ToArray()).ToArray();var bindposes=skins.Select(s=>s.sharedMesh.bindposes).ToArray();
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));
            var colliders=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("R"));var renderers=root.GetComponentsInChildren<Renderer>(true).Length;
            var guard=Derivative(skins[1],session.enemies[0].motion.transform,false);var arms=Derivative(skins[0],session.player.arms,true);
            AssetDatabase.CreateAsset(guard,folder+"/IndieCharacterMeshes.asset");AssetDatabase.AddObjectToAsset(arms,folder+"/IndieCharacterMeshes.asset");
            skins[0].sharedMesh=arms;skins[0].localBounds=arms.bounds;
            for(var i=1;i<skins.Length;i++){if(!skins[i].bones.Select(b=>b.name).SequenceEqual(bindings[1])||!skins[i].sharedMesh.bindposes.SequenceEqual(bindposes[1]))throw new InvalidOperationException("Shared guard binding differs.");skins[i].sharedMesh=guard;skins[i].localBounds=guard.bounds;}
            var after=root.GetComponentsInChildren<Collider>(true);var report=new Report{guardTriangles=guard.triangles.Length/3,armsTriangles=arms.triangles.Length/3,
                skeletonUnchanged=skins.Select((s,i)=>s.bones.Select(b=>b.name).SequenceEqual(bindings[i])).All(x=>x),
                bindposesUnchanged=skins.Select((s,i)=>s.sharedMesh.bindposes.SequenceEqual(bindposes[i])).All(x=>x),
                socketsUnchanged=sockets.All(p=>p.Key.localToWorldMatrix.ToString("R")==p.Value),
                collisionUnchanged=after.Length==colliders.Count&&colliders.All(p=>p.Value==EditorJsonUtility.ToJson(p.Key)+p.Key.transform.localToWorldMatrix.ToString("R")),
                sourceMeshPreserved=originals.All(p=>p.Value==EditorJsonUtility.ToJson(p.Key)),weightsNormalized=Normalized(guard)&&Normalized(arms),addedColliders=after.Length-colliders.Count,addedRenderers=root.GetComponentsInChildren<Renderer>(true).Length-renderers};
            if(!report.skeletonUnchanged||!report.bindposesUnchanged||!report.socketsUnchanged||!report.collisionUnchanged||!report.sourceMeshPreserved||!report.weightsNormalized||report.addedColliders!=0||report.addedRenderers!=0)throw new InvalidOperationException("Character preservation failed.");
            using(var writer=new StreamWriter(new FileStream(folder+"/indie-character-report.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
        }
    }
}
""";
}

