namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndustrialMissionSource = """
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    public sealed class IndustrialMission:MonoBehaviour
    {
        public SecurityCombatSession session;
        public Transform[] objectives;
        public IndustrialPowerBox powerBox;
        public IndustrialMissionTerminal manifestTerminal,extractionTerminal;
        private int shownStage=-1,shownKills=-1;
        public bool courtyardPresentation;
        public int Stage {get;private set;}
        public bool Completed=>Stage==3;
        public string CurrentObjective=>session!=null&&session.ConnectedLayout?(Stage==0?"Restore loading-gate power":Stage==1?(session.Kills<2?"Clear the warehouse guards":"Retrieve the warehouse manifest"):Stage==2?(session.Kills<3?"Secure the control room":"Transmit manifest and extract"):"Transmission and extraction complete"):Stage==0?(powerBox!=null?"Restore power at the power box":"Restore power at the service kiosk"):Stage==1?
            (session.Kills<2?"Clear the two service-yard guards":"Retrieve the shipping manifest"):
            Stage==2?(session.Kills<3?"Clear the extraction guard":"Reach extraction and use the beacon"):"Extraction complete";
        private void Start(){ResetMission();}
        private void Update()
        {
            if(session==null||session.Paused||session.Finished)return;
            RefreshTerminalStates();
            if(Input.GetKeyDown(KeyCode.E))TryInteract();
        }
        public bool TryInteract()
        {
            if(session==null||session.Paused||session.player.Health<=0||Completed||objectives==null||objectives.Length!=3)return false;
            if(Stage==0&&powerBox!=null) {if(!powerBox.WithinReach(session.player.transform.position))return false;}
            else if(Stage==1&&manifestTerminal!=null){if(!manifestTerminal.WithinReach(session.player.transform.position))return false;}
            else if(Stage==2&&extractionTerminal!=null){if(!extractionTerminal.WithinReach(session.player.transform.position))return false;}
            else if(Vector3.Distance(session.player.transform.position,objectives[Stage].position)>2)return false;
            if(Stage==1&&session.Kills<2||Stage==2&&session.Kills<3)return false;
            Stage++;
            if(Stage==1){session.enemies[0].gameObject.SetActive(true);session.enemies[1].gameObject.SetActive(true);}
            if(Stage==2)session.enemies[2].gameObject.SetActive(true);
            RefreshMarkers();if(session.audio!=null)session.audio.Play("ui",objectives[Stage-1].position,false);
            if(Completed)session.SetPaused(true);else if(session.options!=null)_=session.options.RequestCheckpoint("Objective checkpoint");return true;
        }
        public void ResetMission()
        {
            Stage=0;
            if(session!=null)foreach(var enemy in session.enemies){enemy.EnsureInitialized();enemy.gameObject.SetActive(false);}
            RefreshMarkers();
        }
        private void RefreshMarkers()
        {
            var connected=GetComponent<ConnectedIndustrialLevel>();if(connected!=null)connected.Synchronize(Stage);
            shownStage=shownKills=-1;RefreshTerminalStates();
            if(objectives!=null)for(var i=0;i<objectives.Length;i++)if(objectives[i]!=null)
                objectives[i].gameObject.SetActive(i==Stage||(i==0&&powerBox!=null)||(i==1&&manifestTerminal!=null)||(i==2&&extractionTerminal!=null));
        }
        public void RestoreCheckpoint(int stage,int defeatedMask) {
            if(stage<0||stage>2||defeatedMask<0||defeatedMask>7||stage==0&&defeatedMask!=0||stage==1&&(defeatedMask&4)!=0||stage==2&&(defeatedMask&3)!=3)
                throw new System.ArgumentException("Invalid objective checkpoint.");
            Stage=stage;for(var i=0;i<session.enemies.Length;i++) {
                if((defeatedMask&(1<<i))!=0)session.enemies[i].RestoreDefeated();
                else session.enemies[i].gameObject.SetActive(stage==1&&i<2||stage==2);
            }
            RefreshMarkers();
        }
        public void RefreshTerminalStates() {
            var kills=session!=null?session.Kills:0;if(shownStage==Stage&&shownKills==kills)return;shownStage=Stage;shownKills=kills;
            if(powerBox!=null)powerBox.SetPowered(Stage>0);
            if(manifestTerminal!=null)manifestTerminal.SetState(Stage==1&&kills>=2,Stage>=2);
            if(extractionTerminal!=null)extractionTerminal.SetState(Stage==2&&kills>=3,Stage>=3);
        }
        private void OnGUI()
        {
            if(session==null||session.player==null)return;
            if(session.options!=null&&session.Paused)return;
            var previousFont=GUI.skin.label.fontSize;
            if(session.options!=null&&session.options.Settings.largeText)GUI.skin.label.fontSize=17;
            if(courtyardPresentation) {
                var connected=GetComponent<ConnectedIndustrialLevel>();
                GUI.Box(new Rect(14,14,Mathf.Min(640,Screen.width-28),65),connected!=null?"INDUSTRIAL INFILTRATION / "+connected.Zone(session.player.transform.position):"INDUSTRIAL INFILTRATION  /  COURTYARD VISUAL CANDIDATE");
                GUI.Label(new Rect(26,37,Screen.width-52,25),Mathf.Min(Stage+1,3)+"/3  "+CurrentObjective+"  •  E interacts within 2m");
                GUI.Label(new Rect(18,82,Screen.width-36,25),"Original template presentation • library props / final art review pending");
                if(Stage==0&&powerBox!=null&&!session.Paused&&session.player.Health>0&&powerBox.WithinReach(session.player.transform.position))
                    GUI.Box(new Rect(Screen.width/2-110,Screen.height/2+40,220,32),"E — Restore power");
                if(Stage==1&&manifestTerminal!=null&&!session.Paused&&manifestTerminal.WithinReach(session.player.transform.position))
                    GUI.Box(new Rect(Screen.width/2-130,Screen.height/2+40,260,32),manifestTerminal.Available?"E — Retrieve manifest":"Clear the yard guards first");
                if(Stage==2&&extractionTerminal!=null&&!session.Paused&&extractionTerminal.WithinReach(session.player.transform.position))
                    GUI.Box(new Rect(Screen.width/2-130,Screen.height/2+40,260,32),extractionTerminal.Available?"E — Extract":"Clear the extraction guard first");
                GUI.skin.label.fontSize=previousFont;return;
            }
            GUI.Box(new Rect(10,116,740,85),"INDUSTRIAL INFILTRATION — ordered mission prototype");
            GUI.Label(new Rect(25,140,710,25),Mathf.Min(Stage+1,3)+"/3: "+CurrentObjective+" • E interacts within 2m");
            GUI.Label(new Rect(25,165,710,25),courtyardPresentation?"Courtyard visual candidate • template proxies, not approved Blender assets • art review pending":
                "Greybox slots only: reusable Blender props await human review and target integration. No art acceptance.");
            if(!Completed&&objectives!=null&&objectives.Length==3) {
                var offset=objectives[Stage].position-session.player.transform.position;
                GUI.Label(new Rect(10,210,740,25),"Objective distance "+offset.magnitude.ToString("0")+"m • X "+objectives[Stage].position.x.ToString("0")+" / Z "+objectives[Stage].position.z.ToString("0"));
            }
            GUI.skin.label.fontSize=previousFont;
        }
    }
}
""";
    private const string IndustrialMissionBuilderSource = """
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace ForgeCoreGameTemplates.Editor
{
    public static class ForgeCoreIndustrialMissionBuilder
    {
        [Serializable]private sealed class Report {
            public int schemaVersion=1,objectiveCount=3,encounterCount=2,guardCount=3,greyboxPlacementCount=10;
            public string profile="industrial_infiltration_v1",assetMode="explicit_greybox_pending_approved_industrial_assets";
            public string[] objectiveOrder={"restore_power","retrieve_manifest","extract"};
            public string[] requiredPropRecipes={"industrial_barrel_v2","wooden_crate_v1","concrete_barrier_v1","industrial_fence_v1","shipping_container_v1"};
            public bool spawnSafe=true,routeReachable=true,approvedAssetsPlaced=false,visualReviewed=false,rightsReviewed=false,stageComplete=false;
            public bool physicalPowerBox=true,persistentPowerBox=true,powerPlaceholderSphere=false;
            public int physicalTerminalCount=3,objectivePlaceholderSpheres=0;
            public float minimumRouteMetres;
        }
        public static string Build(string assetPath,string hash,string id,string scenePath,string outputPath)
            =>BuildWithStyle(assetPath,hash,id,scenePath,outputPath,"");
        public static string BuildConnected(string assetPath,string hash,string id,string scenePath,string outputPath)
            =>ForgeCoreConnectedLevelBuilder.Build(assetPath,hash,id,scenePath,outputPath);
        public static string BuildWithStyle(string assetPath,string hash,string id,string scenePath,string outputPath,string style)
        {
            if(style!=""&&style!="retro_64")throw new InvalidOperationException("Unsupported original industrial presentation profile.");
            var retro=style=="retro_64";
            ForgeCoreSecurityCombatBuilder.Build(assetPath,hash,id,scenePath,outputPath);
            var scene=SceneManager.GetActiveScene();var session=UnityEngine.Object.FindFirstObjectByType<SecurityCombatSession>();
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Security combat lab");root.name="Industrial infiltration — explicit greybox mission";
            foreach(var item in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Solid cover A"||t.name=="Solid cover B"||
                t.name=="Low firing cover"||t.name=="Presentation dressing — no gameplay colliders").ToArray())
                UnityEngine.Object.DestroyImmediate(item.gameObject);
            var folder="Assets/ForgeCore/CharacterAnimations/"+id;
            var wall=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Wall.mat");
            void Cover(string name,Vector3 bottom,Vector3 size) {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);
                go.transform.position=bottom+Vector3.up*size.y*.5f;go.transform.localScale=size;go.layer=8;go.GetComponent<Renderer>().sharedMaterial=wall;
            }
            // Fixed authored route; these are visibly labelled greyboxes, never substitutions claimed as approved library art.
            Cover("GREYBOX fuel barrel — approval pending",new Vector3(-12,0,-5),new Vector3(.68f,1.1f,.68f));
            Cover("GREYBOX supply crate A — approval pending",new Vector3(-6,0,-3),new Vector3(1,.8f,.8f));
            Cover("GREYBOX supply crate B — approval pending",new Vector3(8,0,retro?-5:-1),new Vector3(1,.8f,.8f));
            Cover("GREYBOX low barrier A — approval pending",new Vector3(-5,0,-4),new Vector3(2.4f,1,.6f));
            Cover("GREYBOX low barrier B — approval pending",new Vector3(9,0,7),new Vector3(2.4f,1,.6f));
            foreach(var point in new[]{new Vector3(-14,0,4),new Vector3(14,0,4),new Vector3(0,0,14)})
                Cover("GREYBOX solid fence — approval pending",point,new Vector3(3,2.2f,.16f));
            foreach(var x in new[]{3f,9f})Cover("GREYBOX sealed cargo — approval pending",new Vector3(x,0,retro&&x==3?-2:0),new Vector3(2.44f,2.59f,6.06f));
            // A divided approach creates cover and a left service lane, rather than a single open shooting range.
            Cover("Service-yard divider",new Vector3(-2,0,2),new Vector3(12,2.4f,.8f));
            var mission=root.AddComponent<IndustrialMission>();mission.session=session;session.mission=mission;
            session.options=root.AddComponent<SecurityDemoOptions>();session.options.session=session;
            session.audio=root.AddComponent<DemoAudioController>();session.audio.session=session;
            ForgeCoreDemoAudioBuilder.Build(session.audio,folder);
            root.AddComponent<DemoRuntimeTelemetry>();
            // Power approach point is in front of its new solid cabinet, not inside the obstacle's navigation cell.
            var positions=new[]{new Vector3(-10,.3f,-8.8f),new Vector3(-10,.3f,8.8f),new Vector3(10,.3f,12.8f)};
            var names=new[]{"Restore power • E","Shipping manifest • E","Extraction beacon • E"};
            var marker=new Material(Shader.Find("Standard")){name="Original mission beacon",color=new Color(.1f,.8f,.65f)};
            marker.EnableKeyword("_EMISSION");marker.SetColor("_EmissionColor",new Color(.04f,.3f,.2f));
            marker.globalIlluminationFlags=UnityEngine.MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(marker,folder+"/MissionObjective.mat");
            mission.objectives=new Transform[3];
            for(var i=0;i<3;i++) {
                if(i==0) {
                    var box=new GameObject("Power box • E");box.transform.SetParent(root.transform,false);box.transform.position=positions[0];box.layer=8;
                    var power=box.AddComponent<IndustrialPowerBox>();power.solidCollider=box.AddComponent<BoxCollider>();
                    power.solidCollider.center=new Vector3(0,.415f,-.7f);power.solidCollider.size=new Vector3(.8f,1.43f,.6f);
                    power.poweredMaterial=marker;mission.powerBox=power;mission.objectives[0]=box.transform;continue;
                }
                var go=new GameObject(names[i]);go.transform.SetParent(root.transform,false);go.transform.position=positions[i];go.layer=8;
                var terminal=go.AddComponent<IndustrialMissionTerminal>();terminal.label=i==1?"MANIFEST":"EXTRACT";
                terminal.solidCollider=go.AddComponent<BoxCollider>();terminal.solidCollider.center=new Vector3(0,.415f,.7f);
                terminal.solidCollider.size=new Vector3(.8f,1.43f,.6f);terminal.usedMaterial=marker;
                if(i==1)mission.manifestTerminal=terminal;else mission.extractionTerminal=terminal;mission.objectives[i]=go.transform;
            }
            var guardStarts=new[]{new Vector3(-9,.02f,3.5f),new Vector3(-3,.02f,10),new Vector3(11,.02f,9)};
            for(var i=0;i<3;i++)session.enemies[i].transform.position=guardStarts[i];
            // Editor-time generation does not run the session's play-mode Awake lifecycle.
            Physics.SyncTransforms();var navigation=new SecurityGridPathfinder(32,32,new Vector3(-15.5f,0,-15.5f),1,
                p=>Physics.CheckBox(p+Vector3.up*.9f,new Vector3(.39f,.8f,.39f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore));
            var report=new Report();var previous=session.player.transform.position;
            foreach(var destination in positions) {
                var route=navigation.Find(previous,destination);
                if(route.Count==0)throw new InvalidOperationException("Authored mission route is blocked.");
                var last=previous;
                foreach(var point in route){var delta=point-last;delta.y=0;report.minimumRouteMetres+=delta.magnitude;last=point;}
                previous=destination;
            }
            if(Physics.CheckSphere(session.player.transform.position+Vector3.up*.9f,.35f,1<<8,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Mission spawn overlaps static cover.");
            foreach(var enemy in session.enemies)
                if(Physics.Linecast(session.player.view.position,enemy.transform.position+Vector3.up,1<<8,QueryTriggerInteraction.Ignore)==false&&
                    Vector3.Distance(session.player.transform.position,enemy.transform.position)<8)
                    throw new InvalidOperationException("Mission spawn lacks a safe staging distance.");
            ForgeCoreCourtyardPresentation.Apply(root,folder);mission.courtyardPresentation=true;
            ForgeCoreCourtyardGraphics.Apply(root,folder);
            if(!retro)ForgeCoreIndieEnvironment.Apply(root,folder);
            ForgeCoreVisualPolish.Apply(root,folder);
            if(retro)ForgeCoreRetroCourtyard.Apply(root,folder);
            if(PrefabUtility.SaveAsPrefabAsset(root,folder+"/IndustrialMission.prefab")==null)throw new InvalidOperationException("Mission prefab save failed.");
            File.WriteAllText(folder+"/mission-report.json",JsonUtility.ToJson(report,true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Mission scene save failed.");
            return "Fresh authored infiltration mission with original bounded courtyard presentation: restore power at a persistent physical cabinet, two-guard yard encounter, retrieve manifest, extraction guard, extract. Route/spawn clearance is checked with the cabinet's solid collision and clear approach point; other mission structure/combat is retained. Surface/architectural dressing uses template geometry, not approved Blender/vault placement. Human art, source reviews and full S09 integration remain open.";
        }
    }
}
""";
}
