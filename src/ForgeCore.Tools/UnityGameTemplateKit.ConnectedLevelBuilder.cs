namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    internal static readonly string[] ConnectedLevelArtifacts=["ConnectedDressing.asset","ConnectedSigns.mat","connected-level-report.json"];
    private const string ConnectedLevelBuilderSource="""
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreConnectedLevelBuilder
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,gridWidth=64,gridHeight=64,zones=3,doors=2,guardCount=3,renderers,colliders;
            public string profile="industrial_connected_v1",source="original_template_extension_with_final_physics_audit";
            public string parentPresentationReports="intermediate_original_32m_art_pass_not_final_layout_measurements";
            public bool spawnSafe,stagedRouteReachable,doorGatingVerified,socketsUnchanged,baseCombatAssetsUnchanged,separateSaveProfile=true;
            public bool stairsSupported=false,approvedLibraryPlacement=false,visualReviewed=false,rightsReviewed=false,stageComplete=false,freezeFixed=false;
            public bool worldSignsDepthTested,legacyRoofFittingsTrimmed;
            public float routeMetres;
        }
        public static string Build(string assetPath,string hash,string id,string scenePath,string outputPath){
            // Base art/motion reports describe this prepass. Only the final connected report describes expanded physics.
            ForgeCoreIndustrialMissionBuilder.Build(assetPath,hash,id,scenePath,outputPath);
            var scene=SceneManager.GetActiveScene();var root=scene.GetRootGameObjects().Single(g=>g.GetComponent<IndustrialMission>()!=null);
            var session=root.GetComponentInChildren<SecurityCombatSession>();var mission=session.mission;var folder="Assets/ForgeCore/CharacterAnimations/"+id;
            string Socket(Transform t)=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R");
            var sockets=new[]{session.player.sight,session.player.rightGrip,session.player.leftGrip,session.player.motion.muzzle}.Concat(session.enemies.SelectMany(e=>new[]{e.rightGrip,e.leftGrip,e.motion.muzzle})).ToDictionary(t=>t,t=>Socket(t));
            var combatAssets=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>s.sharedMesh).ToArray();
            var level=root.AddComponent<ConnectedIndustrialLevel>();level.session=session;session.layoutProfile="industrial_connected_v1";
            var concrete=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailConcrete.mat");
            var steel=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailDarkSteel.mat");
            var blue=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailBlueSteel.mat");
            var lamp=AssetDatabase.LoadAssetAtPath<Material>(folder+"/YardLamp.mat");
            var floor=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DetailAsphalt.mat");
            var group=new GameObject("Connected level — three accessible single-floor zones").transform;group.SetParent(root.transform,false);
            GameObject Box(string name,Vector3 at,Vector3 size,Material material,bool solid=true){
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(group,false);go.transform.localPosition=at;go.transform.localScale=size;go.layer=8;go.GetComponent<Renderer>().sharedMaterial=material;
                if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
            }
            void Sign(string text,Vector3 p,Vector3 angles){var t=new GameObject("Connected route sign").AddComponent<TextMesh>();t.transform.SetParent(group,false);t.transform.localPosition=p;t.transform.localEulerAngles=angles;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.characterSize=.16f;t.fontSize=48;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(1,.88f,.57f);}
            // Retain the service yard and its props. Remove only the former north backdrop/fittings.
            // These derivatives belong to this new output folder; previous art assets are never edited.
            var first=true;
            var legacyBranches=new[]{root.transform.Find("Courtyard presentation — original template, not reviewed library art"),root.transform.Find("Indie environment v1"),root.transform.Find("Courtyard detail v1 — bounded original candidate")};
            foreach(var branch in legacyBranches){
                foreach(var filter in branch.GetComponentsInChildren<MeshFilter>()){
                    var source=filter.sharedMesh;var clone=UnityEngine.Object.Instantiate(source);clone.name="Connected retained yard derivative";
                    for(var sub=0;sub<clone.subMeshCount;sub++){var old=source.GetTriangles(sub);var keep=new List<int>();var vertices=source.vertices;
                        for(var i=0;i<old.Length;i+=3){var a=filter.transform.TransformPoint(vertices[old[i]]);var b=filter.transform.TransformPoint(vertices[old[i+1]]);var c=filter.transform.TransformPoint(vertices[old[i+2]]);
                            if(a.z<17&&b.z<17&&c.z<17)keep.AddRange(new[]{old[i],old[i+1],old[i+2]});}
                        clone.SetTriangles(keep,sub);
                    }clone.RecalculateBounds();if(first){AssetDatabase.CreateAsset(clone,folder+"/ConnectedDressing.asset");first=false;}else AssetDatabase.AddObjectToAsset(clone,folder+"/ConnectedDressing.asset");filter.sharedMesh=clone;
                }
                foreach(var text in branch.GetComponentsInChildren<TextMesh>().Where(t=>t.transform.position.z>=17).ToArray())UnityEngine.Object.DestroyImmediate(text.gameObject);
            }
            // Replace the old sealed north boundary with a real ground-level loading portal.
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Solid end boundary"&&t.position.z>0).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            Box("Yard north portal west pier",new Vector3(-14,1.5f,16.5f),new Vector3(4,3,.5f),concrete);
            Box("Yard north portal east wall",new Vector3(4,1.5f,16.5f),new Vector3(24,3,.5f),concrete);
            Box("Warehouse floor",new Vector3(0,-.12f,31.5f),new Vector3(33,.24f,30),floor);
            Box("Warehouse west wall",new Vector3(-16.5f,3.5f,31.5f),new Vector3(.5f,7,30),concrete);
            Box("Warehouse north wall",new Vector3(0,3.5f,46.5f),new Vector3(33,7,.5f),blue);
            Box("Warehouse ceiling",new Vector3(0,7.1f,31.5f),new Vector3(33,.2f,30),steel);
            Box("Warehouse south upper facade",new Vector3(0,5,16.5f),new Vector3(33,4,.5f),blue);
            // Control-room portal at east wall z=36, matching a clear walkable 4m opening.
            Box("Warehouse east south wall",new Vector3(16.5f,3.5f,25.25f),new Vector3(.5f,7,17.5f),concrete);
            Box("Warehouse east north wall",new Vector3(16.5f,3.5f,42.25f),new Vector3(.5f,7,8.5f),concrete);
            Box("Warehouse east door lintel",new Vector3(16.5f,5.6f,36),new Vector3(.5f,3,4),blue);
            Box("Control room floor",new Vector3(31.5f,-.12f,31.5f),new Vector3(30,.24f,30),floor);
            Box("Control room north wall",new Vector3(31.5f,2.5f,46.5f),new Vector3(30,5,.5f),concrete);
            Box("Control room east wall",new Vector3(46.5f,2.5f,31.5f),new Vector3(.5f,5,30),concrete);
            Box("Control room south wall",new Vector3(31.5f,2.5f,16.5f),new Vector3(30,5,.5f),concrete);
            Box("Control room ceiling",new Vector3(31.5f,5.1f,31.5f),new Vector3(30,.2f,30),steel);
            var gate0=Box("Power-locked loading door",new Vector3(-10,1.9f,16.5f),new Vector3(4,3.8f,.35f),blue);
            var gate1=Box("Manifest-locked control door",new Vector3(16.5f,1.9f,36),new Vector3(.35f,3.8f,4),blue);
            level.doors=new[]{gate0.GetComponent<BoxCollider>(),gate1.GetComponent<BoxCollider>()};level.leaves=new[]{gate0.transform,gate1.transform};level.closedPositions=level.leaves.Select(t=>t.localPosition).ToArray();
            // Cover and readable lanes, not random repeated object scatter.
            foreach(var z in new[]{23f,31f,40f}){
                Box("Warehouse cargo bay",new Vector3(6,1.25f,z),new Vector3(5,2.5f,3),blue);
                Box("Warehouse structural column",new Vector3(-2,3.5f,z),new Vector3(.5f,7,.5f),steel);
                Box("Warehouse ceiling light",new Vector3(-8,6.75f,z),new Vector3(3,.12f,.65f),lamp,false);
                Box("Warehouse safe walkway stripe",new Vector3(-10,.006f,z),new Vector3(.12f,.012f,6),lamp,false);
            }
            foreach(var x in new[]{23f,32f,41f}){
                Box("Control console island",new Vector3(x,.6f,27),new Vector3(3,1.2f,1.3f),steel);
                Box("Control console emissive display",new Vector3(x,1.28f,27),new Vector3(2.6f,.16f,.8f),lamp,false);
            }
            Box("Control room firing cover",new Vector3(29,.65f,37),new Vector3(2.8f,1.3f,1.2f),blue);
            foreach(var point in new[]{new Vector3(-8,5,25),new Vector3(-8,5,39),new Vector3(32,4.4f,34)}){
                var go=new GameObject("Connected interior practical");go.transform.SetParent(group,false);go.transform.position=point;var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=18;light.intensity=2.5f;light.color=new Color(1,.85f,.62f);light.shadows=LightShadows.None;
            }
            Sign("WAREHOUSE / 02 — POWER REQUIRED",new Vector3(-10,4.3f,16.18f),Vector3.zero);
            Sign("CONTROL ROOM / 03",new Vector3(16.12f,4.4f,36),new Vector3(0,90,0));
            Sign("DISPATCH / MANIFEST",new Vector3(-9,3.4f,45.9f),Vector3.zero);
            Sign("TRANSMIT / EXTRACT",new Vector3(38,2.8f,45.9f),Vector3.zero);
            // TextMesh's default overlay shader exposes destinations through solid walls. Bind a
            // fresh output-owned font material; never edit the shared font or existing candidates.
            var signs=root.GetComponentsInChildren<TextMesh>(true);
            var signShader=Resources.Load<Shader>("ForgeCoreWorldSignText");
            if(signShader==null||!signShader.isSupported)throw new InvalidOperationException("Depth-tested world-sign shader unavailable.");
            foreach(var sign in signs){if(sign.font==null)throw new InvalidOperationException("World sign font missing.");sign.font.RequestCharactersInTexture(sign.text,sign.fontSize,sign.fontStyle);}
            var atlas=signs[0].font.material.mainTexture;
            if(atlas==null||signs.Any(t=>t.font.material.mainTexture!=atlas))throw new InvalidOperationException("Exact shared world-sign atlas required.");
            var signMaterial=new Material(signShader){name="Connected physical world signs",mainTexture=atlas};
            AssetDatabase.CreateAsset(signMaterial,folder+"/ConnectedSigns.mat");
            foreach(var sign in signs)sign.GetComponent<MeshRenderer>().sharedMaterial=signMaterial;
            // Static architecture shares material batches; keep independent solid colliders and movable gate leaves.
            var staticFilters=group.GetComponentsInChildren<MeshFilter>().Where(f=>f.transform!=gate0.transform&&f.transform!=gate1.transform).ToArray();
            foreach(var materialGroup in staticFilters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)){
                var mesh=new Mesh{name="Connected static architecture "+materialGroup.Key.name};
                mesh.CombineMeshes(materialGroup.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();mesh.RecalculateTangents();
                AssetDatabase.AddObjectToAsset(mesh,folder+"/ConnectedDressing.asset");
                var batch=new GameObject(mesh.name);batch.transform.SetParent(group,false);batch.layer=8;batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=materialGroup.Key;
            }
            foreach(var f in staticFilters){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            // Keep approach cells in front of the solid cabinets, not inside their 1m navigation cells.
            mission.objectives[1].position=new Vector3(-9,.3f,41.8f);mission.objectives[2].position=new Vector3(38,.3f,40.8f);
            session.enemies[0].transform.position=new Vector3(-8,.02f,26);session.enemies[1].transform.position=new Vector3(0,.02f,37);session.enemies[2].transform.position=new Vector3(34,.02f,36);
            var report=new Report();var previous=session.player.transform.position;
            report.worldSignsDepthTested=signs.All(t=>t.GetComponent<MeshRenderer>().sharedMaterial.shader==signShader);
            report.legacyRoofFittingsTrimmed=legacyBranches.SelectMany(b=>b.GetComponentsInChildren<MeshFilter>()).All(f=>{var vertices=f.sharedMesh.vertices;return f.sharedMesh.triangles.All(i=>f.transform.TransformPoint(vertices[i]).z<17);});
            SecurityGridPathfinder Navigation()=>new SecurityGridPathfinder(64,64,new Vector3(-15.5f,0,-15.5f),1,p=>!Physics.Raycast(p+Vector3.up*.2f,Vector3.down,.5f,1<<8,QueryTriggerInteraction.Ignore)||Physics.CheckBox(p+Vector3.up*.9f,new Vector3(.39f,.8f,.39f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore));
            level.Synchronize(0);var nav=Navigation();
            var closedYard=nav.Find(previous,mission.objectives[1].position).Count==0;
            var closedControl=nav.Find(previous,mission.objectives[2].position).Count==0;
            report.spawnSafe=!Physics.CheckSphere(previous+Vector3.up*.9f,.35f,1<<8,QueryTriggerInteraction.Ignore);
            for(var i=0;i<3;i++){level.Synchronize(i);nav.Refresh();var route=nav.Find(previous,mission.objectives[i].position);if(route.Count==0)throw new InvalidOperationException("Final connected objective route blocked at "+i);
                var last=previous;foreach(var point in route){report.routeMetres+=Vector3.Distance(last,point);last=point;}previous=mission.objectives[i].position;}
            level.Synchronize(1);nav.Refresh();var controlStillLocked=nav.Find(mission.objectives[1].position,mission.objectives[2].position).Count==0;
            level.Synchronize(0);
            report.stagedRouteReachable=true;report.doorGatingVerified=closedYard&&closedControl&&controlStillLocked;
            report.socketsUnchanged=sockets.All(pair=>pair.Value==Socket(pair.Key));
            report.baseCombatAssetsUnchanged=combatAssets.SequenceEqual(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>s.sharedMesh));
            report.renderers=root.GetComponentsInChildren<Renderer>(true).Length;report.colliders=root.GetComponentsInChildren<Collider>(true).Length;
            if(!report.worldSignsDepthTested||!report.legacyRoofFittingsTrimmed||!report.spawnSafe||!report.doorGatingVerified||!report.socketsUnchanged||!report.baseCombatAssetsUnchanged||report.routeMetres<110||report.routeMetres>350||report.renderers>300||report.colliders>100)throw new InvalidOperationException("Connected final physics/resource/preservation gate failed: "+JsonUtility.ToJson(report));
            using(var writer=new StreamWriter(new FileStream(folder+"/connected-level-report.json",FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));
            PrefabUtility.SaveAsPrefabAsset(root,folder+"/IndustrialMission.prefab");AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Connected scene save failed.");
            return "Fresh three-zone single-floor connected candidate saved: power-gated service yard, accessible warehouse with combat/manifest, manifest-gated control room and extraction. Final physics route and door gating audited. Existing character, rifle, audio and clips retained; human art/balance and original freeze remain open.";
        }
    }
}
""";
}

