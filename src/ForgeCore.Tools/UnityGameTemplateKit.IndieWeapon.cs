namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] IndieWeaponArtifacts=["IndieWeaponMeshes.asset","IndieMachinedMetal.mat","indie-weapon-report.json"];
    private const string IndieWeaponSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreIndieWeapon
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,weaponCount,sharedMeshCount=2,materialCount=3,trianglesPerWeapon,addedRenderers,addedColliders,disabledLegacyRenderers;
            public string profile="original_indie_rifle_v1",source="original_code_authored_shared_meshes";
            public bool socketsUnchanged,magazineTransformsUnchanged,collisionUnchanged,sourceHandsChanged=false,animationsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,stageComplete=false;
        }
        private sealed class Builder {
            private readonly List<Vector3> v=new List<Vector3>();private readonly List<Vector2> uv=new List<Vector2>();private readonly List<int>[] indices={new List<int>(),new List<int>(),new List<int>()};
            public void Face(int m,Vector3 normal,params Vector3[] points) {
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)Array.Reverse(points);
                var start=v.Count;for(var i=0;i<points.Length;i++){v.Add(points[i]);uv.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0));}
                for(var i=1;i<points.Length-1;i++)indices[m].AddRange(new[]{start,start+i,start+i+1});
            }
            // Convex bevelled yz contours, not piles of overlapping scaled cubes.
            public void Extrude(int m,float halfWidth,Vector2[] contour,float bevel=.002f) {
                var c=contour.Aggregate(Vector2.zero,(a,b)=>a+b)/contour.Length;
                Vector3 P(Vector2 p,float x)=>new Vector3(x,p.y,p.x);
                foreach(var side in new[]{-1,1}){
                    var cap=contour.Select(p=>P(c+(p-c)*.94f,side*halfWidth)).ToArray();
                    for(var i=0;i<cap.Length;i++){var j=(i+1)%cap.Length;Face(m,Vector3.right*side,P(c,side*halfWidth),cap[i],cap[j]);
                        var a=P(contour[i],side*(halfWidth-bevel));var b=P(contour[j],side*(halfWidth-bevel));Face(m,new Vector3(side,(a.y+b.y)*.5f-c.y,(a.z+b.z)*.5f-c.x),cap[i],a,b,cap[j]);}
                }
                for(var i=0;i<contour.Length;i++){var j=(i+1)%contour.Length;var a=contour[i];var b=contour[j];Face(m,new Vector3(0,(a.y+b.y)*.5f-c.y,(a.x+b.x)*.5f-c.x),P(a,-halfWidth+bevel),P(b,-halfWidth+bevel),P(b,halfWidth-bevel),P(a,halfWidth-bevel));}
            }
            public void Box(int m,Vector3 centre,Vector3 size,Quaternion rotation=default) {
                if(rotation.x==0&&rotation.y==0&&rotation.z==0&&rotation.w==0)rotation=Quaternion.identity;
                Vector3 P(int x,int y,int z)=>centre+rotation*Vector3.Scale(size,new Vector3(x,y,z))*.5f;
                foreach(var axis in new[]{0,1,2})foreach(var side in new[]{-1,1}){
                    Vector3 Q(int a,int b){var p=Vector3.zero;p[axis]=side;p[(axis+1)%3]=a;p[(axis+2)%3]=b;return centre+rotation*Vector3.Scale(size,p)*.5f;}
                    var normal=Vector3.zero;normal[axis]=side;Face(m,rotation*normal,Q(-1,-1),Q(1,-1),Q(1,1),Q(-1,1));
                }
            }
            public void Tube(int m,Vector3 centre,float length,float outer,float inner,int sides=24,Quaternion rotation=default) {
                if(rotation.x==0&&rotation.y==0&&rotation.z==0&&rotation.w==0)rotation=Quaternion.identity;
                Vector3 P(float angle,float radius,float z)=>centre+rotation*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                for(var i=0;i<sides;i++){
                    var a=i*Mathf.PI*2/sides;var b=(i+1)*Mathf.PI*2/sides;var radial=rotation*new Vector3(Mathf.Cos((a+b)*.5f),Mathf.Sin((a+b)*.5f),0);
                    Face(m,radial,P(a,outer,-length*.5f),P(b,outer,-length*.5f),P(b,outer,length*.5f),P(a,outer,length*.5f));
                    Face(m,-radial,P(a,inner,-length*.5f),P(b,inner,-length*.5f),P(b,inner,length*.5f),P(a,inner,length*.5f));
                    foreach(var side in new[]{-1,1})Face(m,rotation*Vector3.forward*side,P(a,outer,side*length*.5f),P(b,outer,side*length*.5f),P(b,inner,side*length*.5f),P(a,inner,side*length*.5f));
                }
            }
            public Mesh Mesh(string name){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.subMeshCount=3;for(var i=0;i<3;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                if(mesh.triangles.Length/3>12000||mesh.vertices.Any(p=>float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z)))throw new InvalidOperationException("Rifle geometry exceeded finite/resource bounds.");return mesh;}
        }
        private static Vector2 P(float z,float y)=>new Vector2(z,y);
        public static void Apply(GameObject root,string folder) {
            var session=root.GetComponentInChildren<SecurityCombatSession>();var weapons=new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon)).ToArray();
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));
            var magazines=weapons.Select(w=>w.Find("Animated detachable magazine")).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));var collision=root.GetComponentsInChildren<Collider>(true).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("R"));
            var metal=AssetDatabase.LoadAssetAtPath<Material>(folder+"/HeroMetal.mat");var polymer=AssetDatabase.LoadAssetAtPath<Material>(folder+"/HeroPolymer.mat");
            if(metal==null||polymer==null||weapons.Length!=4)throw new InvalidOperationException("Existing complete hero material/weapon convention required.");
            var machined=new Material(metal){name="IndieMachinedMetal",color=new Color(.33f,.35f,.36f),enableInstancing=true};machined.SetFloat("_GlossMapScale",.72f);AssetDatabase.CreateAsset(machined,folder+"/IndieMachinedMetal.mat");
            var b=new Builder();
            b.Extrude(0,.028f,new[]{P(-.146f,-.024f),P(-.146f,.026f),P(-.118f,.043f),P(.036f,.043f),P(.082f,.022f),P(.082f,-.022f),P(.05f,-.038f),P(-.12f,-.038f)});
            b.Extrude(0,.025f,new[]{P(-.13f,-.027f),P(.056f,-.027f),P(.056f,-.054f),P(.005f,-.060f),P(-.035f,-.047f),P(-.13f,-.047f)});
            b.Extrude(1,.023f,new[]{P(-.094f,-.044f),P(-.049f,-.047f),P(-.07f,-.151f),P(-.11f,-.148f),P(-.116f,-.133f)});
            // Stock has a shaped cheek rest, thinner buffer and a ribbed rubber butt, not a solid cuboid.
            b.Tube(0,new Vector3(0,.005f,-.192f),.13f,.014f,.008f,20);
            b.Extrude(1,.025f,new[]{P(-.305f,.005f),P(-.276f,.018f),P(-.207f,.018f),P(-.174f,-.002f),P(-.20f,-.033f),P(-.29f,-.064f),P(-.305f,-.058f)});
            b.Extrude(1,.03f,new[]{P(-.32f,.017f),P(-.305f,.017f),P(-.30f,-.083f),P(-.318f,-.083f)});
            for(var i=0;i<7;i++)b.Box(0,new Vector3(0,-.07f+i*.012f,-.321f),new Vector3(.047f,.003f,.003f));
            // Skeletal side panels expose actual ventilation gaps and the smaller round barrel.
            b.Tube(2,new Vector3(0,.012f,.244f),.274f,.0105f,.0065f,24);
            b.Tube(0,new Vector3(0,.012f,.372f),.034f,.018f,.0075f,24);
            for(var i=0;i<3;i++)b.Tube(1,new Vector3(0,.012f,.365f+i*.006f),.002f,.0184f,.0155f,24);
            foreach(var side in new[]{-1,1}){
                foreach(var y in new[]{-.018f,.030f})b.Box(0,new Vector3(side*.033f,y,.177f),new Vector3(.008f,.014f,.18f));
                for(var i=0;i<6;i++)b.Box(0,new Vector3(side*.033f,.006f,.096f+i*.031f),new Vector3(.008f,.04f,.007f));
                b.Box(1,new Vector3(side*.0286f,.005f,-.008f),new Vector3(.002f,.024f,.067f));
                b.Box(2,new Vector3(side*.0300f,.005f,-.023f),new Vector3(.002f,.017f,.036f));
                foreach(var z in new[]{-.10f,.026f})b.Tube(2,new Vector3(side*.029f,-.018f,z),.002f,.005f,.001f,12,Quaternion.Euler(0,90,0));
                b.Box(1,new Vector3(side*.031f,-.026f,-.075f),new Vector3(.004f,.007f,.025f),Quaternion.Euler(0,0,side*18));
            }
            b.Box(0,new Vector3(0,.044f,.065f),new Vector3(.038f,.008f,.34f));
            for(var i=0;i<19;i++)b.Box(2,new Vector3(0,.050f,-.088f+i*.017f),new Vector3(.042f,.005f,.006f));
            b.Box(1,new Vector3(0,-.109f,-.125f),new Vector3(.023f,.006f,.066f));b.Box(1,new Vector3(0,-.079f,-.156f),new Vector3(.023f,.065f,.006f));b.Box(2,new Vector3(0,-.077f,-.117f),new Vector3(.006f,.031f,.007f),Quaternion.Euler(15,0,0));
            // Existing ADS point and emissive dot remain in the clear 48 mm optic opening.
            b.Box(0,new Vector3(0,.059f,-.02f),new Vector3(.044f,.014f,.072f));b.Tube(1,new Vector3(0,.095f,-.02f),.047f,.032f,.024f,32);b.Tube(2,new Vector3(0,.095f,-.0445f),.003f,.0325f,.024f,32);
            b.Tube(1,new Vector3(.036f,.095f,-.014f),.012f,.013f,.004f,20,Quaternion.Euler(0,90,0));
            var rifle=b.Mesh("Original indie service rifle");AssetDatabase.CreateAsset(rifle,folder+"/IndieWeaponMeshes.asset");
            var mb=new Builder();mb.Extrude(1,.5f,new[]{P(-.40f,.5f),P(.40f,.5f),P(.50f,.15f),P(.63f,-.5f),P(-.37f,-.5f),P(-.5f,.12f)},.04f);
            mb.Box(1,new Vector3(0,-.49f,.12f),new Vector3(1.12f,.09f,1.14f));foreach(var side in new[]{-1,1})for(var i=0;i<5;i++)mb.Box(0,new Vector3(side*.505f,-.33f+i*.15f,.04f),new Vector3(.025f,.022f,.69f));
            var magazineMesh=mb.Mesh("Original curved detachable rifle magazine");AssetDatabase.AddObjectToAsset(magazineMesh,folder+"/IndieWeaponMeshes.asset");var materials=new[]{metal,polymer,machined};var report=new Report{trianglesPerWeapon=(rifle.triangles.Length+magazineMesh.triangles.Length)/3};
            foreach(var weapon in weapons){foreach(var r in weapon.GetComponentsInChildren<MeshRenderer>(true)){if(r.name=="Amber sight dot")continue;if(r.enabled)report.disabledLegacyRenderers++;r.enabled=false;}
                void Part(Transform parent,string name,Mesh mesh){var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=weapon.gameObject.layer;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;report.addedRenderers++;}
                Part(weapon,"Indie service rifle visual",rifle);Part(weapon.Find("Animated detachable magazine"),"Indie curved magazine visual",magazineMesh);report.weaponCount++;
            }
            var after=root.GetComponentsInChildren<Collider>(true);report.addedColliders=after.Length-collision.Count;report.collisionUnchanged=after.Length==collision.Count&&collision.All(p=>p.Value==EditorJsonUtility.ToJson(p.Key)+p.Key.transform.localToWorldMatrix.ToString("R"));report.socketsUnchanged=sockets.All(p=>p.Key.localToWorldMatrix.ToString("R")==p.Value);report.magazineTransformsUnchanged=magazines.All(p=>p.Key.localToWorldMatrix.ToString("R")==p.Value);
            if(!report.socketsUnchanged||!report.magazineTransformsUnchanged||!report.collisionUnchanged||report.addedColliders!=0||report.weaponCount!=4||report.addedRenderers!=8||report.trianglesPerWeapon>12000)throw new InvalidOperationException("Shared rifle rig/resource preservation failed.");
            using(var writer=new StreamWriter(new FileStream(folder+"/indie-weapon-report.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
        }
    }
}
""";
}
