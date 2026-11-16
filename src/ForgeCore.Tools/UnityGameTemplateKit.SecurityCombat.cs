namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SecuritySessionSource = """
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    // Isolated bounded combat acceptance fixture. No old prototype scene is modified.
    public sealed class SecurityCombatSession : MonoBehaviour
    {
        public static SecurityCombatSession Instance { get; private set; }
        public SecurityCombatPlayer player;
        public SecurityCombatEnemy[] enemies;
        public IndustrialMission mission;
        public SecurityDemoOptions options;
        public DemoAudioController audio;
        public bool Paused { get; private set; }
        public bool Finished => player != null && (player.Health <= 0 || (mission!=null?mission.Completed:Kills==enemies.Length));
        public int Kills { get { var n=0;foreach(var e in enemies)if(e!=null&&e.Health<=0)n++;return n;} }
        public int ShotsBlockedByCover { get; set; }
        public int PathSearches { get; set; }
        public SecurityGridPathfinder Navigation { get; private set; }
        private void Awake()
        {
            Instance=this;
            Navigation=new SecurityGridPathfinder(32,32,new Vector3(-15.5f,0,-15.5f),1,
                p=>Physics.CheckBox(p+Vector3.up*.9f,new Vector3(.39f,.8f,.39f),Quaternion.identity,1<<8,QueryTriggerInteraction.Ignore));
        }
        private void Update()
        {
            var input=PrototypeInput.Read();
            if(input.PausePressed&&(options==null||!options.Intro))SetPaused(!Paused);
            if(input.RestartPressed&&(options==null||!options.Intro))Restart();
            if(Finished)SetPaused(true);
            Cursor.lockState=Paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=Paused;
        }
        private void Start(){Physics.SyncTransforms();Navigation.Refresh();}
        public void SetPaused(bool value)
        {
            Paused=value;
            if(audio!=null)audio.SetPaused(value);
            if(player!=null&&player.motion!=null)player.motion.SetPaused(value);
            foreach(var e in enemies)if(e!=null&&e.motion!=null)e.motion.SetPaused(value);
        }
        public void Restart()
        {
            foreach(var bullet in FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None))Destroy(bullet.gameObject);
            foreach(var effect in FindObjectsByType<PrototypeMuzzleFlash>(FindObjectsSortMode.None))Destroy(effect.gameObject);
            ShotsBlockedByCover=0;PathSearches=0;SetPaused(false);
            player.ResetActor();foreach(var e in enemies)e.ResetActor();
            if(mission!=null)mission.ResetMission();
            Navigation.Refresh();
        }
        private void OnGUI()
        {
            if(player==null)return;
            if(options!=null&&Paused){options.DrawMenu();return;}
            var large=options!=null&&options.Settings.largeText;
            var oldLabel=GUI.skin.label.fontSize;var oldBox=GUI.skin.box.fontSize;
            if(large){GUI.skin.label.fontSize=17;GUI.skin.box.fontSize=17;}
            if(mission!=null&&mission.courtyardPresentation) {
                GUI.Box(new Rect(14,Screen.height-68,210,50),"HEALTH  "+player.Health+"     |     GUARDS  "+Kills+"/"+enemies.Length);
                GUI.Box(new Rect(Screen.width-234,Screen.height-68,220,50),"AMMO  "+player.motion.Ammo+" / "+player.Reserve+"     "+player.motion.Action);
                GUI.Label(new Rect(18,Screen.height-36,Screen.width-36,25),"WASD/mouse · Shift sprint · Space jump · LMB fire · RMB sights · R reload · Esc pause · F5 restart");
            }else {
            GUI.Box(new Rect(10,10,740,100),mission!=null?"INDUSTRIAL INFILTRATION — technical mission / greybox props":"S04–S06 SECURITY COMBAT LAB — original procedural art / technical work in progress");
            GUI.Label(new Rect(25,36,710,25),"Health "+player.Health+"  |  Ammo "+player.motion.Ammo+" / "+player.Reserve+"  |  Guards "+Kills+"/"+enemies.Length+"  |  "+player.motion.Action);
            GUI.Label(new Rect(25,62,710,25),"WASD/mouse • Shift sprint • Space jump • LMB fire • RMB sights • R reload • Esc pause • F5 restart");
            }
            if(player.AimBlend<.1f)GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-10,25,25),"+");
            GUI.skin.label.fontSize=oldLabel;GUI.skin.box.fontSize=oldBox;
            if(Paused)
            {
                GUI.Box(new Rect(Screen.width/2-170,Screen.height/2-90,340,170),
                    Finished?(player.Health<=0?"Training failed":mission!=null?"Extraction complete":"All guards disabled"):"Paused");
                if(!Finished&&GUI.Button(new Rect(Screen.width/2-140,Screen.height/2-40,280,35),"Resume"))SetPaused(false);
                if(GUI.Button(new Rect(Screen.width/2-140,Screen.height/2+5,280,35),"Restart lab"))Restart();
                if(GUI.Button(new Rect(Screen.width/2-140,Screen.height/2+45,280,25),"Quit"))Application.Quit();
            }
        }
        private void OnDestroy(){if(Instance==this)Instance=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
    // Four-neighbour bounded breadth-first search; conservative 0.78m clearance, static obstacles.
    // Supports this flat 32m lab only: not stairs, moving platforms, crowds or arbitrary Unity navigation.
    public sealed class SecurityGridPathfinder
    {
        private readonly int width,height;private readonly Vector3 origin;private readonly float cell;
        private readonly Func<Vector3,bool> blocked;private readonly bool[] solid;
        public int LastExpanded { get; private set; }
        public SecurityGridPathfinder(int w,int h,Vector3 o,float size,Func<Vector3,bool> query)
        {
            if(w<2||h<2||w>64||h>64||size<=0||float.IsNaN(size)||float.IsInfinity(size)||query==null)throw new ArgumentException("Invalid bounded navigation grid.");
            width=w;height=h;origin=o;cell=size;blocked=query;solid=new bool[w*h];Refresh();
        }
        public void Refresh(){using(var measured=DemoRuntimeTelemetry.Measure(ForgeCore.Demo.DemoWorkKind.NavigationRefresh))for(var i=0;i<solid.Length;i++)solid[i]=blocked(Point(i));}
        private Vector3 Point(int i)=>origin+new Vector3(i%width*cell,0,i/width*cell);
        private int Index(Vector3 p){var x=Mathf.RoundToInt((p.x-origin.x)/cell);var y=Mathf.RoundToInt((p.z-origin.z)/cell);return x<0||x>=width||y<0||y>=height?-1:y*width+x;}
        public List<Vector3> Find(Vector3 start,Vector3 goal)
        {
            using(var measured=DemoRuntimeTelemetry.Measure(ForgeCore.Demo.DemoWorkKind.RouteSearch))return FindCore(start,goal);
        }
        private List<Vector3> FindCore(Vector3 start,Vector3 goal)
        {
            var result=new List<Vector3>();LastExpanded=0;var a=Index(start);var b=Index(goal);
            if(a<0||b<0||solid[a]||solid[b])return result;
            var parent=new int[solid.Length];for(var i=0;i<parent.Length;i++)parent[i]=-2;
            var queue=new Queue<int>();queue.Enqueue(a);parent[a]=-1;
            while(queue.Count>0)
            {
                var n=queue.Dequeue();LastExpanded++;if(n==b)break;
                for(var edge=0;edge<4;edge++) {
                    var d=edge==0?-1:edge==1?1:edge==2?-width:width;
                    var next=n+d;if(next<0||next>=solid.Length||solid[next]||parent[next]!=-2)continue;
                    if(Mathf.Abs(d)==1&&n/width!=next/width)continue;
                    parent[next]=n;queue.Enqueue(next);
                }
            }
            if(parent[b]==-2)return result;
            for(var n=b;n>=0;n=parent[n])result.Add(Point(n));
            result.Reverse();if(result.Count>0)result.RemoveAt(0);return result;
        }
    }
}
""";
    private const string SecurityPlayerSource = """
using System;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SecurityCombatPlayer : MonoBehaviour
    {
        public Transform view,arms,magazine,weapon,rightGrip,leftGrip,sight; public PrototypeSkinnedAnimator motion;
        public int Health { get; private set; }=100; public int Reserve { get; private set; }=60;
        private CharacterController controller;private Vector3 spawn,magazineBase,armsBase;
        private Quaternion magazineRotation;private Vector3 weaponBase;private float yaw,pitch,recoil,elapsed,aimBlend,coverBlend;
        public float AimBlend => aimBlend;
        public float CoverBlend => coverBlend;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public float ViewPitch=>pitch;
        private float verticalVelocity,hipFieldOfView;private bool jumpHeld;private Camera viewCamera;private Vector3 sightLocal;
        private const float Gravity=18f,JumpHeight=1.05f;
        private Transform rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand;
        private bool wasReloading;private int reloadStartAmmo;
        private void Awake(){controller=GetComponent<CharacterController>();spawn=transform.position;}
        private void Start()
        {
            armsBase=arms.localPosition;magazineBase=magazine.localPosition;magazineRotation=magazine.localRotation;
            weaponBase=weapon.localPosition;
            if(sight==null)sight=weapon.Find("Sight aim point")??weapon.Find("Amber sight dot");
            if(sight==null)throw new InvalidOperationException("A verified sight aim point is required for down-sight alignment.");
            sightLocal=weapon.InverseTransformPoint(sight.position);viewCamera=view.GetComponent<Camera>();
            hipFieldOfView=viewCamera!=null?viewCamera.fieldOfView:72;
            if(SecurityCombatSession.Instance!=null&&SecurityCombatSession.Instance.options!=null)hipFieldOfView=SecurityCombatSession.Instance.options.Settings.fieldOfView;
            foreach(var bone in arms.GetComponentsInChildren<Transform>()) {
                if(bone.name=="UpperArm.R")rightUpper=bone;if(bone.name=="LowerArm.R")rightLower=bone;if(bone.name=="Hand.R")rightHand=bone;
                if(bone.name=="UpperArm.L")leftUpper=bone;if(bone.name=="LowerArm.L")leftLower=bone;if(bone.name=="Hand.L")leftHand=bone;
            }
            motion.ShotEmitted+=Shot;
            motion.PoseEvaluated+=ApplyHandContacts;
        }
        private void OnDestroy(){if(motion!=null){motion.ShotEmitted-=Shot;motion.PoseEvaluated-=ApplyHandContacts;}}
        private void Update()
        {
            var session=SecurityCombatSession.Instance;if(session==null||session.Paused||session.Finished)return;
            Simulate(PrototypeInput.ReadSecurityCombat(),Input.GetMouseButton(1),Time.deltaTime);
        }
        public void Simulate(PrototypeInputFrame input,bool aim,float dt)
        {
            SecurityWeaponPose.ValidateDelta(dt);
            if(Health<=0||motion.Paused)return;
            var options=SecurityCombatSession.Instance!=null?SecurityCombatSession.Instance.options:null;
            var sensitivity=options!=null?options.Settings.sensitivity:1.7f;
            elapsed+=dt;yaw+=input.LookX*sensitivity;pitch=Mathf.Clamp(pitch-input.LookY*sensitivity*(options!=null&&options.Settings.invertY?-1:1),-75,75);
            transform.rotation=Quaternion.Euler(0,yaw,0);view.localRotation=Quaternion.Euler(pitch,0,0);
            IsSprinting=input.SprintHeld&&input.MoveY>.01f&&!aim&&motion.Action!="Reload"&&!input.ReloadPressed&&!input.FireHeld;
            var move=Vector3.ClampMagnitude(transform.right*input.MoveX+transform.forward*input.MoveY,1)*(aim?2.4f:IsSprinting?6.5f:4f);
            if(dt>0)
            {
                if(controller.isGrounded&&verticalVelocity<0)verticalVelocity=-2;
                if(input.JumpPressed&&!jumpHeld&&controller.isGrounded)verticalVelocity=Mathf.Sqrt(2*Gravity*JumpHeight);
                jumpHeld=input.JumpPressed;verticalVelocity-=Gravity*dt;
                var flags=controller.Move((move+Vector3.up*verticalVelocity)*dt);
                if((flags&CollisionFlags.Above)!=0&&verticalVelocity>0)verticalVelocity=0;
                if((flags&CollisionFlags.Below)!=0&&verticalVelocity<0)verticalVelocity=-2;
            }
            motion.SetLocomotion(0,0,true); // First-person view owns bob; skeleton owns action pose.
            CommitCompletedReload();
            motion.AvailableReloadRounds=Reserve;
            if(input.ReloadPressed&&motion.TryReload()){reloadStartAmmo=motion.Ammo;wasReloading=true;}
            if(input.FireHeld)motion.TryFire();
            recoil*=Mathf.Exp(-12*dt);
            var reload=motion.Action=="Reload";
            aimBlend=SecurityWeaponPose.Damp(aimBlend,aim&&!reload?1:0,14,dt);
            // Retract away from nearby static cover. This is not a separate overlay camera:
            // world occlusion and the muzzle sweep still block shooting through walls.
            RaycastHit coverHit;
            var nearCover=Physics.SphereCast(view.position,.06f,view.forward,out coverHit,1.10f,1<<8,QueryTriggerInteraction.Ignore);
            var obstruction=nearCover?1-Mathf.InverseLerp(.35f,1.10f,coverHit.distance):0;
            coverBlend=SecurityWeaponPose.Damp(coverBlend,obstruction,20,dt);
            var bob=Mathf.Sin(elapsed*(IsSprinting?12:9))*Mathf.Min(1,move.magnitude)*(IsSprinting?.012f:.008f);
            var hipShift=new Vector3(bob,-Mathf.Abs(bob)-coverBlend*.06f,-recoil*.045f-coverBlend*.28f);
            var rotation=Quaternion.Euler(-recoil*4*(1-aimBlend),0,0);
            // Eye, sight bead and camera axis coincide at full ADS. Recoil moves the view with the sight,
            // rather than knocking the optic off-screen; near-cover retraction stays on the same eye axis.
            var adsPosition=new Vector3(0,0,.42f-coverBlend*.20f)-rotation*sightLocal;
            var shift=Vector3.Lerp(hipShift,adsPosition-weaponBase,aimBlend);
            arms.localPosition=armsBase+shift;weapon.localPosition=weaponBase+shift;
            weapon.localRotation=rotation;
            view.localRotation=Quaternion.Euler(pitch-recoil*1.5f*aimBlend,0,0);
            if(viewCamera!=null)viewCamera.fieldOfView=Mathf.Lerp(hipFieldOfView,60,aimBlend);
            SecurityWeaponPose.Magazine(magazine,magazineBase,magazineRotation,motion.Action,motion.ActionTime);
        }
        private void LateUpdate(){ApplyHandContacts();}
        public void ApplyHandContacts()
        {
            if(Health<=0||rightUpper==null||leftUpper==null)return;
            SecurityWeaponPose.Contact(rightUpper,rightLower,rightHand,rightGrip,view.right*.25f-view.up);
            SecurityWeaponPose.Contact(leftUpper,leftLower,leftHand,leftGrip,-view.right*.25f-view.up);
        }
        private void Shot(Vector3 muzzle,Vector3 forward)
        {
            var session=SecurityCombatSession.Instance;if(Health<=0||session==null||session.Paused)return;
            recoil=Mathf.Min(1.4f,recoil+.85f);
            // A view ray chooses the aim point; a second muzzle sweep still owns collision/damage.
            var destination=view.position+view.forward*60;RaycastHit aimHit;
            if(Physics.Raycast(view.position,view.forward,out aimHit,60,~(1<<9),QueryTriggerInteraction.Ignore))destination=aimHit.point;
            if(SecurityCombatBullet.Blocked(view.position,muzzle,transform)){session.ShotsBlockedByCover++;return;}
            SecurityCombatBullet.Launch(muzzle,(destination-muzzle).normalized,transform,false);
            PrototypeFeedback.Fire(muzzle,forward,false);
        }
        public void Damage(int value){if(Health<=0||value<=0)return;Health=Mathf.Max(0,Health-value);motion.Hit(Health==0);}
        public void SetHipFieldOfView(float value){hipFieldOfView=value;if(viewCamera==null&&view!=null)viewCamera=view.GetComponent<Camera>();if(viewCamera!=null)viewCamera.fieldOfView=value;}
        public void CommitCompletedReload() {
            if(!wasReloading||motion.Action=="Reload")return;
            if(motion.Ammo>reloadStartAmmo)Reserve=Mathf.Max(0,Reserve-(motion.Ammo-reloadStartAmmo));wasReloading=false;
        }
        public void RestoreCheckpoint(int health,int reserve,int ammo,Vector3 position,float heading,float lookPitch) {
            controller.enabled=false;transform.SetPositionAndRotation(position,Quaternion.Euler(0,heading,0));controller.enabled=true;
            Health=health;Reserve=reserve;yaw=heading;pitch=lookPitch;verticalVelocity=0;motion.RestoreMagazine(ammo);view.localRotation=Quaternion.Euler(pitch,0,0);
        }
        public void ResetActor()
        {
            controller.enabled=false;transform.SetPositionAndRotation(spawn,Quaternion.identity);controller.enabled=true;
            Health=100;Reserve=60;yaw=pitch=recoil=elapsed=aimBlend=coverBlend=verticalVelocity=0;
            IsSprinting=false;jumpHeld=false;wasReloading=false;motion.ResetDemo();
            view.localRotation=Quaternion.identity;arms.localPosition=armsBase;
            magazine.localPosition=magazineBase;magazine.localRotation=magazineRotation;
            weapon.localPosition=weaponBase;weapon.localRotation=Quaternion.identity;
            if(viewCamera!=null)viewCamera.fieldOfView=hipFieldOfView;
        }
    }
    // Shared, original procedural contact/reload pass. No arbitrary IK package or mocap source.
    public static class SecurityWeaponPose
    {
        public static void ValidateDelta(float dt)
        { if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>1)throw new ArgumentException("Invalid simulation delta."); }
        public static float Damp(float current,float target,float rate,float dt)=>Mathf.Lerp(current,target,1-Mathf.Exp(-rate*dt));
        public static float MagazineTravel(float seconds)
        {
            // Seated -> withdraw -> hold -> insert -> seated. Ammo commits later at 1.6s.
            var t=Mathf.Clamp01(seconds/1.6f);
            if(t<.12f)return 0;
            if(t<.35f)return Mathf.SmoothStep(0,1,(t-.12f)/.23f);
            if(t<.58f)return 1;
            if(t<.88f)return 1-Mathf.SmoothStep(0,1,(t-.58f)/.30f);
            return 0;
        }
        public static void Magazine(Transform magazine,Vector3 rest,Quaternion rotation,string action,float seconds)
        {
            var drop=action=="Reload"?MagazineTravel(seconds):0;
            magazine.localPosition=rest+new Vector3(-.025f*drop,-.16f*drop,-.05f*drop);
            magazine.localRotation=rotation*Quaternion.Euler(0,0,15*drop);
        }
        public static void Contact(Transform upper,Transform lower,Transform hand,Transform grip,Vector3 bend)
        {
            if(grip==null)return;
            ContactPoint(upper,lower,hand,grip.position,grip.forward,bend);
        }
        public static Vector3 ReloadSupportPoint(Transform grip,Transform magazine,string action,float seconds)
        {
            if(grip==null)throw new ArgumentNullException(nameof(grip));
            if(action!="Reload"||magazine==null)return grip.position;
            // The support hand follows the actual moving magazine, not a permanently fixed foregrip.
            var reach=ForgeCore.Motion.GuardMotionProfile.ReloadContact(seconds);
            return Vector3.Lerp(grip.position,magazine.TransformPoint(new Vector3(0,-.02f,0)),reach);
        }
        public static void ContactPoint(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 forward,Vector3 bend)
        {
            if(upper==null||lower==null||hand==null)return;
            var a=Vector3.Distance(upper.position,lower.position);var b=Vector3.Distance(lower.position,hand.position);
            var offset=target-upper.position;if(offset.sqrMagnitude<.000001f)return;
            var distance=Mathf.Clamp(offset.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            var axis=offset.normalized;var along=(a*a-b*b+distance*distance)/(2*distance);
            var across=Vector3.ProjectOnPlane(bend,axis).normalized;
            if(across.sqrMagnitude<.01f)across=Vector3.ProjectOnPlane(upper.right,axis).normalized;
            var elbow=upper.position+axis*along+across*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            hand.rotation=Quaternion.FromToRotation(hand.up,forward)*hand.rotation;
        }
    }
}
""";
    private const string SecurityEnemySource = """
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SecurityCombatEnemy : MonoBehaviour
    {
        public PrototypeSkinnedAnimator motion;
        public Transform weapon,magazine,rightGrip,leftGrip;
        public int Health { get; private set; }=3;
        public int Shots { get; private set; }
        public string State { get; private set; }="Idle";
        public float MoveSpeed { get; private set; }
        public float PlantError => footwork==null?0:footwork.MaximumPlantError;
        public float CorpseGroundError => footwork==null?0:footwork.CorpseGroundError;
        public bool FootPlanted(int side)=>footwork!=null&&footwork.IsPlanted(side);
        public Vector3 FootTarget(int side)=>footwork.Target(side);
        public float GaitPhase => footwork==null?0:footwork.Phase;
        public Vector3 SupportHandTarget => SecurityWeaponPose.ReloadSupportPoint(leftGrip,magazine,motion.Action,motion.ActionTime);
        private SecurityEnemyFootwork footwork;
        private CharacterController controller;private Vector3 spawn;private Quaternion heading;
        private float replan,nextShot;private int waypoint;private List<Vector3> path=new List<Vector3>();
        private Transform rightUpper,rightLower,rightHand,leftUpper,leftLower,leftHand,chest,weaponParent;
        private Vector3 weaponChestOffset,magazineBase;private Quaternion magazineRotation;private float recoil;
        private void Awake(){controller=GetComponent<CharacterController>();spawn=transform.position;heading=transform.rotation;}
        private bool initialized;
        private void Start(){EnsureInitialized();}
        public void EnsureInitialized()
        {
            if(initialized||motion==null)return;initialized=true;
            foreach(var bone in motion.GetComponentsInChildren<Transform>()) {
                if(bone.name=="UpperArm.R")rightUpper=bone;if(bone.name=="LowerArm.R")rightLower=bone;if(bone.name=="Hand.R")rightHand=bone;
                if(bone.name=="UpperArm.L")leftUpper=bone;if(bone.name=="LowerArm.L")leftLower=bone;if(bone.name=="Hand.L")leftHand=bone;
                if(bone.name=="Chest")chest=bone;
            }
            if(weapon!=null) {
                weaponParent=weapon.parent;weaponChestOffset=transform.InverseTransformVector(weapon.position-chest.position);
                magazineBase=magazine.localPosition;magazineRotation=magazine.localRotation;
            }
            footwork=new SecurityEnemyFootwork(transform,motion);
            motion.ShotEmitted+=Shot;motion.PoseEvaluated+=ApplyWeaponPose;nextShot=1.3f;
        }
        private void OnDestroy(){if(motion!=null){motion.ShotEmitted-=Shot;motion.PoseEvaluated-=ApplyWeaponPose;}if(footwork!=null)footwork.Dispose();}
        // PrototypeSkinnedAnimator.PoseEvaluated owns the post-animation solve and runs
        // before the muzzle marker. A second LateUpdate used to solve the same body twice.
        public void ApplyWeaponPose()
        {
            if(footwork!=null)footwork.Apply(motion.Dead,motion.ActionTime);
            if(Health<=0||weapon==null||chest==null)return;
            var position=chest.position+transform.TransformVector(weaponChestOffset);
            var direction=transform.forward;
            var session=SecurityCombatSession.Instance;
            if(session!=null&&session.player!=null) {
                var target=session.player.transform.position+Vector3.up*1.1f;
                var horizontal=target-transform.position;horizontal.y=0;
                if(horizontal.sqrMagnitude>0.01f&&Vector3.Angle(transform.forward,horizontal)<20)
                    direction=(target-position).normalized;
            }
            weapon.SetPositionAndRotation(position,Quaternion.LookRotation(direction,transform.up)*Quaternion.Euler(-recoil*2,0,0));
            SecurityWeaponPose.Magazine(magazine,magazineBase,magazineRotation,motion.Action,motion.ActionTime);
            SecurityWeaponPose.Contact(rightUpper,rightLower,rightHand,rightGrip,transform.right*.4f-transform.up);
            SecurityWeaponPose.ContactPoint(leftUpper,leftLower,leftHand,SupportHandTarget,leftGrip.forward,-transform.right*.4f-transform.up);
        }
        private void Update()
        {
            var session=SecurityCombatSession.Instance;if(session==null||session.Paused||session.Finished||Health<=0)return;
            Tick(Time.deltaTime,session);
        }
        public bool CanSee(Vector3 target,Transform targetRoot)
        {
            var eye=transform.position+Vector3.up*1.35f;
            return SecurityCombatBullet.FirstHitIsTarget(eye,target,transform,targetRoot);
        }
        public void Tick(float dt,SecurityCombatSession session)
        {
            SecurityWeaponPose.ValidateDelta(dt);
            if(Health<=0||motion.Paused)return;
            recoil*=Mathf.Exp(-12*dt);
            replan-=dt;nextShot-=dt;var player=session.player;
            var target=player.transform.position;var offset=target-transform.position;offset.y=0;
            var distance=offset.magnitude;var sees=CanSee(target+Vector3.up*1.1f,player.transform);
            var canAttack=sees&&distance<=14;
            var movement=Vector3.zero;
            if(motion.Action=="Locomotion"&&!canAttack)
            {
                State="Pursue";
                if(replan<=0) {path=session.Navigation.Find(transform.position,target);waypoint=0;replan=.6f;session.PathSearches++;}
                if(waypoint<path.Count)
                {
                    var delta=path[waypoint]-transform.position;delta.y=0;
                    if(delta.magnitude<.16f)waypoint++;
                    else movement=delta.normalized*Mathf.Min(1.7f,delta.magnitude/Mathf.Max(.001f,dt));
                }
            }
            else State=motion.Action=="Locomotion"?"Aim":motion.Action;
            var facing=movement.sqrMagnitude>.01f?movement:offset;var oldHeading=transform.eulerAngles.y;
            if(facing.sqrMagnitude>.001f&&motion.Action!="Hit")transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),120*dt);
            var angle=movement.sqrMagnitude>.001f?Vector3.Angle(transform.forward,movement):0;
            var desired=movement.magnitude*Mathf.InverseLerp(85,15,angle);
            if(motion.Action!="Locomotion"||canAttack||angle>=85)MoveSpeed=0;
            else MoveSpeed=Mathf.MoveTowards(MoveSpeed,desired,(desired>MoveSpeed?6:10)*dt);
            movement=transform.forward*MoveSpeed;
            var before=transform.position;
            controller.Move((movement+Vector3.down*3)*dt);
            var actual=transform.position-before;actual.y=0;var speed=actual.magnitude/Mathf.Max(dt,.001f);
            var turned=Mathf.DeltaAngle(oldHeading,transform.eulerAngles.y);
            if(footwork!=null)footwork.Advance(actual.magnitude,turned,dt,motion.Action!="Hit");
            motion.SetLocomotion(speed,Mathf.DeltaAngle(oldHeading,transform.eulerAngles.y)/Mathf.Max(dt,.001f),canAttack);
            if(canAttack&&nextShot<=0&&Vector3.Angle(transform.forward,offset)<12)
            {
                if(motion.Ammo==0)motion.TryReload();
                else if(motion.TryFire())nextShot=.8f;
            }
        }
        private void Shot(Vector3 muzzle,Vector3 forward)
        {
            var session=SecurityCombatSession.Instance;if(Health<=0||session==null||session.Paused)return;
            var target=session.player.transform.position+Vector3.up*1.1f;
            var flat=target-transform.position;flat.y=0;
            if(flat.magnitude>14||Vector3.Angle(transform.forward,flat)>=12||Vector3.Angle(forward,target-muzzle)>3)return;
            // Recheck after animation evaluation. No stale pre-animation LOS permission is reused.
            if(!CanSee(target,session.player.transform)||SecurityCombatBullet.Blocked(transform.position+Vector3.up*1.35f,muzzle,transform)||
                !SecurityCombatBullet.FirstHitIsTarget(muzzle,target,transform,session.player.transform))
            {session.ShotsBlockedByCover++;return;}
            // The evaluated barrel, not a hidden independently aimed hitscan, owns projectile direction.
            SecurityCombatBullet.Launch(muzzle,forward,transform,true);Shots++;recoil=1;PrototypeFeedback.Fire(muzzle,forward);
        }
        public void Damage(int value)
        {
            if(Health<=0||value<=0)return;Health=Mathf.Max(0,Health-value);motion.Hit(Health==0);
            MoveSpeed=0;if(footwork!=null)footwork.Advance(0,0,0,false);
            if(Health==0){State="Dead";controller.enabled=false;path.Clear();if(weapon!=null&&rightHand!=null)weapon.SetParent(rightHand,true);
                var session=SecurityCombatSession.Instance;if(session!=null&&session.options!=null)_=session.options.RequestCheckpoint("Guard cleared checkpoint");}
        }
        public void RestoreDefeated(){EnsureInitialized();Health=0;State="Dead";controller.enabled=false;path.Clear();gameObject.SetActive(false);}
        public void ResetActor()
        {
            EnsureInitialized();
            controller.enabled=false;transform.SetPositionAndRotation(spawn,heading);controller.enabled=true;
            Health=3;Shots=0;MoveSpeed=0;replan=recoil=0;nextShot=1.3f;waypoint=0;path.Clear();State="Idle";motion.ResetDemo();
            if(footwork!=null)footwork.Reset();
            if(weapon!=null){weapon.SetParent(weaponParent,true);ApplyWeaponPose();}
        }
    }
}
""";
    private const string SecurityBulletSource = """
using System;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    public sealed class SecurityCombatBullet : MonoBehaviour
    {
        private Transform owner;private bool hostile,spent;private float age;private Material material;
        public static SecurityCombatBullet Launch(Vector3 origin,Vector3 direction,Transform source,bool enemy)
        {
            var session=SecurityCombatSession.Instance;
            if(source==null||direction.sqrMagnitude<.5f||session!=null&&(session.Paused||session.Finished)||
                FindObjectsByType<SecurityCombatBullet>(FindObjectsSortMode.None).Length>=64)return null;
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=enemy?"Guard swept bullet":"Player swept bullet";
            go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction));go.transform.localScale=Vector3.one*.045f;
            var bullet=go.AddComponent<SecurityCombatBullet>();bullet.owner=source;bullet.hostile=enemy;
            bullet.material=PrototypeMuzzleFlash.CreateFeedbackMaterial(enemy?new Color(1,.5f,.06f):new Color(1,.8f,.1f),2);
            if(bullet.material==null){Destroy(go);return null;}
            go.GetComponent<Renderer>().sharedMaterial=bullet.material;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=bullet.material;trail.time=.06f;trail.startWidth=.035f;trail.endWidth=.003f;
            return bullet;
        }
        public static bool FirstHitIsTarget(Vector3 origin,Vector3 target,Transform source,Transform intended)
        {
            var delta=target-origin;var hits=Physics.RaycastAll(origin,delta.normalized,delta.magnitude+.1f,~(1<<9),QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var h in hits) {if(h.transform==source||h.transform.IsChildOf(source))continue;return h.transform==intended||h.transform.IsChildOf(intended);}
            return false;
        }
        public static bool Blocked(Vector3 origin,Vector3 end,Transform source)
        {
            var delta=end-origin;if(delta.sqrMagnitude<.00001f)return false;
            foreach(var h in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~(1<<9),QueryTriggerInteraction.Ignore))
                if(h.transform!=source&&!h.transform.IsChildOf(source))return true;
            return false;
        }
        private void Update()
        {
            var session=SecurityCombatSession.Instance;if(session!=null&&session.Finished){Destroy(gameObject);return;}
            if(session!=null&&session.Paused)return;Advance(Time.deltaTime);
        }
        public void Advance(float delta)
        {
            var session=SecurityCombatSession.Instance;if(session!=null&&(session.Paused||session.Finished))return;
            if(spent||delta<=0)return;age+=delta;if(age>3){spent=true;Destroy(gameObject);return;}
            var distance=(hostile?20:65)*delta;
            var hits=Physics.SphereCastAll(transform.position,.025f,transform.forward,distance,~(1<<9),QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(owner!=null&&(hit.transform==owner||hit.transform.IsChildOf(owner)))continue;
                spent=true;
                if(session!=null&&session.audio!=null)session.audio.Play("impact",hit.point);
                if(hostile){var p=hit.collider.GetComponentInParent<SecurityCombatPlayer>();if(p!=null)p.Damage(7);}
                else{var e=hit.collider.GetComponentInParent<SecurityCombatEnemy>();if(e!=null)e.Damage(1);}
                Destroy(gameObject);return;
            }
            transform.position+=transform.forward*distance;
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
""";
}
