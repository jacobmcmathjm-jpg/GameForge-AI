namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] RetroArtifacts=["RetroStone.png","RetroSteel.png","RetroWood.png","RetroCloth.png","RetroSkin.png","RetroDark.png","RetroGlass.png","RetroGlow.png",
        "RetroStone.mat","RetroSteel.mat","RetroWood.mat","RetroCloth.mat","RetroSkin.mat","RetroDark.mat","RetroGlass.mat","RetroGlow.mat","RetroGuard.asset","RetroArms.asset","RetroWeapon.asset","RetroMagazine.asset","retro64-report.json"];
    private const string RetroCourtyardSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace ForgeCoreGameTemplates.Editor
{
    // Original code-authored low-poly derivative of our original rig. No GoldenEye/ROM/model/texture extraction.
    public static class ForgeCoreRetroCourtyard
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,textureCount=8,maximumTextureSize=128,paletteLevels=32,guardCount=3,guardTriangles,armsTriangles,weaponTriangles,propLodGroups,activePropTriangles,remainingActiveReflections,addedColliders,nominalPlacementConflicts;
            public string profile="retro_64",recipe="original_retro_industrial_v1",layout="retro_industrial_layout_v1",source="original_template_derivative_no_game_assets";
            public long ownedTextureBytes;public bool socketsUnchanged,routesReachable=true,animationClipsChanged=false,audioChanged=false,visualReviewed=false,rightsReviewed=false,approvedLibraryAssetsPlaced=false,hardwareEmulated=false;
        }
        private sealed class MeshWriter {
            public readonly List<Vector3> v=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<BoneWeight> weights=new List<BoneWeight>();
            public readonly List<int>[] indices={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            public void Face(int material,Vector3 centre,Vector3[] points,Vector2[] tex,BoneWeight[] skin) {
                if(points.Length<3)return;var normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]);
                if(normal.sqrMagnitude<1e-12f&&points.Length==4)normal=Vector3.Cross(points[2]-points[0],points[3]-points[0]);
                if(Vector3.Dot(normal,points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length-centre)<0){Array.Reverse(points);Array.Reverse(tex);Array.Reverse(skin);}
                var first=v.Count;v.AddRange(points);uv.AddRange(tex);weights.AddRange(skin);
                for(var i=1;i<points.Length-1;i++)if(Vector3.Cross(points[i]-points[0],points[i+1]-points[0]).sqrMagnitude>1e-12f)indices[material].AddRange(new[]{first,first+i,first+i+1});
            }
            public void Box(Vector3 centre,Vector3 size,int material,BoneWeight weight) {
                Vector3 P(int x,int y,int z)=>centre+Vector3.Scale(size,new Vector3(x,y,z))*.5f;
                foreach(var axis in new[]{0,1,2})foreach(var side in new[]{-1,1}) {
                    Vector3 Q(int a,int b){var p=Vector3.zero;p[axis]=side;p[(axis+1)%3]=a;p[(axis+2)%3]=b;return centre+Vector3.Scale(size,p)*.5f;}
                    Face(material,centre,new[]{Q(-1,-1),Q(1,-1),Q(1,1),Q(-1,1)},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},Enumerable.Repeat(weight,4).ToArray());
                }
            }
            public Mesh Make(string name,Matrix4x4[] bind=null) {
                var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.subMeshCount=4;for(var i=0;i<4;i++)mesh.SetTriangles(indices[i],i);
                if(bind!=null){mesh.bindposes=bind;mesh.boneWeights=weights.ToArray();}mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
        public static void Apply(GameObject root,string folder) {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||GraphicsSettings.currentRenderPipeline!=null)throw new InvalidOperationException("Original retro profile needs graphics-enabled built-in rendering.");
            var report=new Report();var session=root.GetComponentInChildren<SecurityCombatSession>();var colliderCount=root.GetComponentsInChildren<Collider>(true).Length;
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>t.localToWorldMatrix.ToString("R"));
            var palette=new Dictionary<string,Material>();foreach(var role in new[]{"Stone","Steel","Wood","Cloth","Skin","Dark","Glass","Glow"}) {
                var texture=Texture(folder,role);report.ownedTextureBytes+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                var shader=Shader.Find("Legacy Shaders/VertexLit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Retro vertex-lit shader unavailable; no shader substitution.");
                var m=new Material(shader){name="Retro"+role,mainTexture=texture,color=Color.white,enableInstancing=true};AssetDatabase.CreateAsset(m,folder+"/Retro"+role+".mat");palette.Add(role,m);
            }
            Material Select(Material material) {
                var name=material.name.ToLowerInvariant();if(name.Contains("lamp")||name.Contains("amber")||name.Contains("objective"))return palette["Glow"];
                if(name.Contains("wood")||name.Contains("rust"))return palette["Wood"];if(name.Contains("cloth"))return palette["Cloth"];
                if(name.Contains("glass")||name.Contains("visor"))return palette["Glass"];if(name.Contains("rubber")||name.Contains("glove")||name.Contains("polymer")||name.Contains("dark"))return palette["Dark"];
                if(name.Contains("steel")||name.Contains("metal")||name.Contains("yellow"))return palette["Steel"];return palette["Stone"];
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                if(renderer.GetComponent<TextMesh>()!=null)continue; // Font atlas/UI is not counted as an owned scene-art texture.
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m==null?null:Select(m)).ToArray();
            }
            foreach(var group in root.GetComponentsInChildren<LODGroup>(true)) {
                if(!group.name.StartsWith("Template ",StringComparison.Ordinal))continue;
                var lods=group.GetLODs();if(lods.Length!=3)throw new InvalidOperationException("Expected our original three-level prop family.");var chosen=lods[2].renderers;
                group.SetLODs(new[]{new LOD(.005f,chosen)});group.ForceLOD(0);group.RecalculateBounds();report.propLodGroups++;
                report.activePropTriangles+=chosen.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                foreach(var renderer in lods.Take(2).SelectMany(l=>l.renderers))UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            }
            foreach(var transform in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Original finish binding ",StringComparison.Ordinal)).ToArray())UnityEngine.Object.DestroyImmediate(transform.gameObject);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))if(renderer.transform.parent!=null&&renderer.transform.parent.name.StartsWith("Courtyard detail v1",StringComparison.Ordinal))renderer.enabled=false;
            var originalSkin=session.enemies[0].motion.GetComponentInChildren<SkinnedMeshRenderer>();var guard=Guard(originalSkin);AssetDatabase.CreateAsset(guard,folder+"/RetroGuard.asset");
            var guardMaterials=new[]{palette["Cloth"],palette["Skin"],palette["Dark"],palette["Steel"]};
            foreach(var enemy in session.enemies) {
                var skin=enemy.motion.GetComponentInChildren<SkinnedMeshRenderer>();
                if(!skin.bones.Select(b=>b.name).SequenceEqual(originalSkin.bones.Select(b=>b.name)))throw new InvalidOperationException("Retro derivative requires the exact original bone convention.");
                skin.sharedMesh=guard;skin.sharedMaterials=guardMaterials;skin.localBounds=guard.bounds;
            }
            var arms=UnityEngine.Object.Instantiate(guard);arms.name="Original retro forearms and mittens";
            bool Arm(int index){var w=arms.boneWeights[index];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};float total=0;
                for(var i=0;i<4;i++)if(originalSkin.bones[ids[i]].name.StartsWith("LowerArm.",StringComparison.Ordinal)||originalSkin.bones[ids[i]].name.StartsWith("Hand.",StringComparison.Ordinal))total+=amounts[i];return total>.75f;}
            for(var sub=0;sub<4;sub++){var old=arms.GetTriangles(sub);var chosen=new List<int>();for(var i=0;i<old.Length;i+=3)if(Arm(old[i])&&Arm(old[i+1])&&Arm(old[i+2]))chosen.AddRange(new[]{old[i],old[i+1],old[i+2]});arms.SetTriangles(chosen,sub);}arms.RecalculateBounds();
            AssetDatabase.CreateAsset(arms,folder+"/RetroArms.asset");var firstPerson=session.player.arms.GetComponentInChildren<SkinnedMeshRenderer>();firstPerson.sharedMesh=arms;firstPerson.sharedMaterials=guardMaterials;firstPerson.localBounds=arms.bounds;
            var weapon=Weapon();AssetDatabase.CreateAsset(weapon,folder+"/RetroWeapon.asset");
            var magWriter=new MeshWriter();magWriter.Box(Vector3.zero,Vector3.one,1,W(0));var magazine=magWriter.Make("Original retro detachable magazine");AssetDatabase.CreateAsset(magazine,folder+"/RetroMagazine.asset");
            foreach(var owner in new[]{session.player.weapon}.Concat(session.enemies.Select(e=>e.weapon))) {
                foreach(var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=renderer.name=="Amber sight dot";
                var go=new GameObject("Original retro low-poly rifle");go.transform.SetParent(owner,false);go.layer=owner.gameObject.layer;go.AddComponent<MeshFilter>().sharedMesh=weapon;go.AddComponent<MeshRenderer>().sharedMaterials=new[]{palette["Steel"],palette["Dark"],palette["Dark"],palette["Steel"]};
                var mag=new GameObject("Original retro animated magazine");mag.transform.SetParent(owner.Find("Animated detachable magazine"),false);mag.layer=owner.gameObject.layer;mag.AddComponent<MeshFilter>().sharedMesh=magazine;mag.AddComponent<MeshRenderer>().sharedMaterials=new[]{palette["Steel"],palette["Dark"],palette["Dark"],palette["Steel"]};
            }
            foreach(var probe in root.GetComponentsInChildren<ReflectionProbe>(true)){probe.customBakedTexture=null;probe.bakedTexture=null;probe.enabled=false;}
            report.remainingActiveReflections=root.GetComponentsInChildren<ReflectionProbe>(true).Count(p=>p.enabled);
            RenderSettings.skybox=null;RenderSettings.customReflectionTexture=null;RenderSettings.reflectionIntensity=0;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.43f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=18;RenderSettings.fogEndDistance=58;RenderSettings.fogColor=new Color(.36f,.43f,.48f);
            if(RenderSettings.sun!=null){RenderSettings.sun.intensity=.8f;RenderSettings.sun.color=new Color(1,.94f,.8f);RenderSettings.sun.shadows=LightShadows.None;}
            foreach(var light in root.GetComponentsInChildren<Light>(true))if(light.type!=LightType.Directional)light.enabled=false;
            foreach(var camera in root.GetComponentsInChildren<Camera>(true)){camera.allowHDR=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;}
            report.guardTriangles=guard.triangles.Length/3;report.armsTriangles=arms.triangles.Length/3;report.weaponTriangles=(weapon.triangles.Length+magazine.triangles.Length)/3;
            report.addedColliders=root.GetComponentsInChildren<Collider>(true).Length-colliderCount;report.socketsUnchanged=sockets.All(p=>p.Key.localToWorldMatrix.ToString("R")==p.Value);
            // Independent live-physics three-route check after the corrected layout and derivative art are in place.
            Physics.SyncTransforms();var navigation=new SecurityGridPathfinder(32,32,new Vector3(-15.5f,0,-15.5f),1,p=>Physics.CheckBox(p+Vector3.up*.9f,new Vector3(.39f,.8f,.39f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore));
            var propBoxes=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("GREYBOX",StringComparison.Ordinal)).Select(t=>t.GetComponent<BoxCollider>()).ToArray();
            var fixedBoxes=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Service-yard divider").Select(t=>t.GetComponent<BoxCollider>()).Concat(new[]{root.GetComponent<IndustrialMission>().powerBox.solidCollider,root.GetComponent<IndustrialMission>().manifestTerminal.solidCollider,root.GetComponent<IndustrialMission>().extractionTerminal.solidCollider}).ToArray();
            var allBoxes=propBoxes.Concat(fixedBoxes).ToArray();for(var i=0;i<propBoxes.Length;i++)for(var j=i+1;j<allBoxes.Length;j++)if(propBoxes[i].bounds.Intersects(allBoxes[j].bounds))report.nominalPlacementConflicts++;
            var previous=session.player.transform.position;foreach(var objective in root.GetComponent<IndustrialMission>().objectives){if(navigation.Find(previous,objective.position).Count==0)report.routesReachable=false;previous=objective.position;}
            Write(folder+"/retro64-report.json",System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report,true)));
            if(!report.socketsUnchanged||!report.routesReachable||report.nominalPlacementConflicts!=0||report.addedColliders!=0||report.guardTriangles>2000||report.armsTriangles<48||report.armsTriangles>500||report.weaponTriangles>400||report.activePropTriangles>6000||report.ownedTextureBytes>1024*1024)
                throw new InvalidOperationException("Original retro derivative failed resource/rig/route gates: "+JsonUtility.ToJson(report));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
        }
        private static BoneWeight W(int index)=>new BoneWeight{boneIndex0=index,weight0=1};
        private static BoneWeight Blend(int a,int b,float value)=>new BoneWeight{boneIndex0=a,boneIndex1=b,weight0=1-value,weight1=value};
        private static Mesh Guard(SkinnedMeshRenderer skin) {
            var bind=skin.sharedMesh.bindposes;int I(string name)=>Array.FindIndex(skin.bones,b=>b.name==name);
            Vector3 B(string name){var i=I(name);if(i<0)throw new InvalidOperationException("Original rig bone missing.");return bind[i].inverse.MultiplyPoint3x4(Vector3.zero);}
            var root=B("Root");var up=(B("Neck")-B("Hips")).normalized;var right=(B("UpperArm.L")-B("UpperArm.R")).normalized;
            var forward=Vector3.ProjectOnPlane(B("Toe.L")-B("Foot.L"),up).normalized;var scale=Vector3.Distance(B("Neck"),B("Hips"))/.52f;var writer=new MeshWriter();
            void Rings(Vector3[] centres,float[] widths,float[] depths,BoneWeight[] weights,int sides,int material) {
                var axis=(centres[centres.Length-1]-centres[0]).normalized;var x=Vector3.ProjectOnPlane(right,axis).normalized;if(x.sqrMagnitude<.1f)x=Vector3.ProjectOnPlane(forward,axis).normalized;var z=Vector3.Cross(x,axis).normalized;
                Vector3 P(int row,int column){var a=column*2*Mathf.PI/sides;return centres[row]+x*(Mathf.Cos(a)*widths[row])+z*(Mathf.Sin(a)*depths[row]);}
                var centre=(centres[0]+centres[centres.Length-1])*.5f;
                for(var row=0;row<centres.Length-1;row++)for(var column=0;column<sides;column++)writer.Face(material,centre,new[]{P(row,column),P(row+1,column),P(row+1,column+1),P(row,column+1)},
                    new[]{new Vector2(column/(float)sides,row/(float)(centres.Length-1)),new Vector2(column/(float)sides,(row+1f)/(centres.Length-1)),new Vector2((column+1f)/sides,(row+1f)/(centres.Length-1)),new Vector2((column+1f)/sides,row/(float)(centres.Length-1))},
                    new[]{weights[row],weights[row+1],weights[row+1],weights[row]});
                foreach(var row in new[]{0,centres.Length-1})writer.Face(material,centre,Enumerable.Range(0,sides).Select(c=>P(row,c)).ToArray(),Enumerable.Range(0,sides).Select(c=>new Vector2(c/(float)sides,.5f)).ToArray(),Enumerable.Repeat(weights[row],sides).ToArray());
            }
            void Limb(string a,string b,float radiusA,float radiusB,int material) {
                var p=B(a);var q=B(b);Rings(new[]{p,(p+q)*.5f,q},new[]{radiusA,(radiusA+radiusB)*.5f,radiusB}.Select(r=>r*scale).ToArray(),new[]{radiusA,radiusA*.8f,radiusB}.Select(r=>r*scale*.85f).ToArray(),new[]{W(I(a)),Blend(I(a),I(b),.5f),W(I(b))},6,material);
            }
            void Ellipsoid(Vector3 centre,Vector3 radii,int material,int bone,int sides=8) {
                Vector3 P(int row,int col){var y=-1+row*.5f;var ring=Mathf.Sqrt(Mathf.Max(0,1-y*y));var angle=(col/(float)sides-.5f)*2*Mathf.PI;
                    return centre+right*(Mathf.Sin(angle)*radii.x*ring)+forward*(Mathf.Cos(angle)*radii.z*ring)+up*(y*radii.y);}
                for(var row=0;row<4;row++)for(var col=0;col<sides;col++)writer.Face(material,centre,new[]{P(row,col),P(row+1,col),P(row+1,col+1),P(row,col+1)},
                    new[]{new Vector2(col/(float)sides,row*.25f),new Vector2(col/(float)sides,(row+1)*.25f),new Vector2((col+1f)/sides,(row+1)*.25f),new Vector2((col+1f)/sides,row*.25f)},Enumerable.Repeat(W(bone),4).ToArray());
            }
            Rings(new[]{B("Hips")-up*.075f*scale,B("Hips"),B("Spine"),B("Chest")+up*.06f*scale,B("Neck")-up*.012f*scale},
                new[]{.16f,.165f,.16f,.225f,.095f}.Select(v=>v*scale).ToArray(),new[]{.105f,.115f,.115f,.13f,.085f}.Select(v=>v*scale).ToArray(),new[]{W(I("Hips")),W(I("Hips")),W(I("Spine")),W(I("Chest")),W(I("Neck"))},8,0);
            Ellipsoid(B("Head")+up*.085f*scale,new Vector3(.115f,.145f,.112f)*scale,1,I("Head"));
            foreach(var side in new[]{"L","R"}) {
                Limb("UpperArm."+side,"LowerArm."+side,.075f,.058f,0);Limb("LowerArm."+side,"Hand."+side,.058f,.037f,0);
                var hand=B("Hand."+side);var delta=(hand-B("LowerArm."+side)).normalized;Ellipsoid(hand+delta*.045f*scale,new Vector3(.052f,.038f,.035f)*scale,2,I("Hand."+side),6);
                Limb("UpperLeg."+side,"LowerLeg."+side,.10f,.067f,0);Limb("LowerLeg."+side,"Foot."+side,.067f,.047f,0);
                var foot=(B("Foot."+side)+B("Toe."+side))*.5f+forward*.04f*scale;foot+=up*(.08f*scale-Vector3.Dot(foot-root,up));Ellipsoid(foot,new Vector3(.07f,.08f,.16f)*scale,2,I("Foot."+side),6);
            }
            var chest=B("Chest")+up*.045f*scale+forward*.132f*scale;writer.Box(chest,new Vector3(.28f,.20f,.014f)*scale,2,W(I("Chest")));
            return writer.Make("Original low-poly retro guard — original rig derivative",bind);
        }
        private static Mesh Weapon() {
            // Leave the fixed evaluated muzzle visibly clear in ADS, not embedded in the barrel face.
            var w=new MeshWriter();var weight=W(0);w.Box(new Vector3(0,-.025f,0),new Vector3(.062f,.055f,.27f),0,weight);
            w.Box(new Vector3(0,-.02f,-.23f),new Vector3(.055f,.10f,.18f),1,weight);w.Box(new Vector3(0,-.095f,-.065f),new Vector3(.045f,.115f,.052f),1,weight);
            w.Box(new Vector3(0,-.02f,.14f),new Vector3(.07f,.045f,.15f),1,weight);w.Box(new Vector3(0,.012f,.29f),new Vector3(.026f,.026f,.12f),0,weight);
            w.Box(new Vector3(0,.061f,-.015f),new Vector3(.042f,.022f,.13f),0,weight);
            foreach(var side in new[]{-1,1})w.Box(new Vector3(side*.024f,.095f,-.02f),new Vector3(.007f,.046f,.035f),1,weight);
            w.Box(new Vector3(0,.117f,-.02f),new Vector3(.055f,.007f,.035f),1,weight);return w.Make("Original low-poly retro rifle");
        }
        private static Texture2D Texture(string folder,string role) {
            var size=role=="Stone"||role=="Steel"||role=="Wood"?128:role=="Glass"||role=="Glow"?32:64;var pixels=new Color32[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++) {
                var u=x/(float)size;var v=y/(float)size;var noise=((x*73+y*137+x*y*17)%31)/30f;var shade=.87f+noise*.13f;
                var colour=role=="Stone"?new Color(.43f,.44f,.40f):role=="Steel"?new Color(.28f,.36f,.36f):role=="Wood"?new Color(.41f,.28f,.14f):role=="Cloth"?new Color(.30f,.35f,.19f):role=="Skin"?new Color(.67f,.45f,.29f):role=="Dark"?new Color(.12f,.13f,.10f):role=="Glass"?new Color(.16f,.23f,.27f):new Color(.87f,.72f,.28f);
                if(role=="Stone"&&(y%32==0||x%64==0))shade*=.67f;
                if(role=="Steel"&&(x%24<2||y%64<2))shade*=.55f;
                if(role=="Wood"){shade*=.87f+.13f*Mathf.Sin((v*30+Mathf.Sin(u*9)*.12f)*Mathf.PI);if(y%21<2)shade*=.5f;}
                if(role=="Cloth"&&((x/3+y/3)%2==0))shade*=.88f;
                if(role=="Skin") {
                    if(v>.77f)colour=new Color(.17f,.12f,.07f);
                    var front=Mathf.Abs(u-.5f)<.24f;
                    if(front&&v>.59f&&v<.64f&&(Mathf.Abs(u-.42f)<.034f||Mathf.Abs(u-.58f)<.034f))colour=new Color(.10f,.075f,.045f);
                    if(front&&v>.37f&&v<.405f&&Mathf.Abs(u-.5f)<.09f)colour=new Color(.34f,.16f,.10f);
                    if(front&&v>.45f&&v<.57f&&Mathf.Abs(u-.5f)<.025f)colour*=.78f;
                }
                colour*=shade;float Q(float value)=>Mathf.Round(Mathf.Clamp01(value)*31)/31;pixels[y*size+x]=new Color(Q(colour.r),Q(colour.g),Q(colour.b),1);
            }
            var path=folder+"/Retro"+role+".png";var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);try{texture.SetPixels32(pixels);texture.Apply();Write(path,texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.filterMode=FilterMode.Bilinear;importer.anisoLevel=1;importer.maxTextureSize=128;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=128,format=TextureImporterFormat.RGBA32});importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static void Write(string path,byte[] bytes){using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))file.Write(bytes,0,bytes.Length);}
    }
}
""";
}
