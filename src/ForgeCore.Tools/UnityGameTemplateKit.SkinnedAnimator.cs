namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SkinnedAnimatorSource = """
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForgeCoreGameTemplates
{
    // Owns gameplay time and root translation. Animator owns only the skeleton; no root-motion mixing.
    public sealed class PrototypeSkinnedAnimator : MonoBehaviour
    {
        public Animator animator;
        public Transform muzzle;
        public bool automaticDemo = true;
        public bool showControls = true;
        public bool driveRootTranslation = true;
        public bool emitTrainingProjectiles = true;
        public event System.Action<Vector3,Vector3> ShotEmitted;
        public event System.Action PoseEvaluated;
        public bool Dead { get; private set; }
        public bool Paused { get; private set; }
        public int Ammo { get; private set; } = 6;
        public int Shots { get; private set; }
        public int AvailableReloadRounds = 6;
        private int reloadTarget;
        public string Action { get; private set; } = "Locomotion";
        public float ActionTime { get; private set; }
        public Vector3 LastMuzzlePosition { get; private set; }
        public Vector3 LastShotDirection { get; private set; }
        public float Speed { get; private set; }
        private float turn, duration, demoTime, nextDemoShot;
        private bool aiming, shotPending;
        private Vector3 origin;
        private Quaternion originRotation;
        private readonly List<GameObject> bullets = new List<GameObject>();
        private readonly List<float> bulletAges = new List<float>();
        private void Awake() { origin=transform.position; originRotation=transform.rotation; }
        private void Start() { if(animator!=null) { animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; } }
        private void Update() { Tick(Time.deltaTime); }
        private void LateUpdate()
        {
            // Contacts/weapon sockets must be solved after animation and BEFORE the muzzle marker,
            // irrespective of the two components' relative LateUpdate order.
            PoseEvaluated?.Invoke();
            // Marker is emitted after Animator has evaluated this frame, at the evaluated muzzle.
            if(shotPending && !Paused && !Dead && Action!="Hit" && Action!="Reload") { shotPending=false; Emit(); }
        }
        public void SetLocomotion(float speed,float degreesPerSecond,bool aim)
        {
            if(float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(degreesPerSecond)||float.IsInfinity(degreesPerSecond))
                throw new ArgumentException("Motion inputs must be finite.");
            Speed=Mathf.Clamp(speed,0,2); turn=Mathf.Clamp(degreesPerSecond,-120,120); aiming=aim;
        }
        public bool TryFire()
        {
            if(Paused||Dead||Ammo<=0||Action!="Locomotion")return false;
            Ammo--; Begin("Fire",.25f);return true;
        }
        public bool TryReload()
        {
            if(Paused||Dead||Ammo==6||AvailableReloadRounds<=0||Action!="Locomotion")return false;
            reloadTarget=Mathf.Min(6,Ammo+AvailableReloadRounds);
            Begin("Reload",1.6f);return true;
        }
        public void Hit(bool lethal=false)
        {
            if(Paused||Dead)return;
            shotPending=false;
            if(lethal) { Dead=true;Begin("Death",float.PositiveInfinity); }
            else Begin("Hit",.4f); // Interrupted reload never grants ammunition.
        }
        public void SetPaused(bool paused)
        { Paused=paused;if(animator!=null)animator.speed=paused?0:1; }
        public void ResetDemo()
        {
            foreach(var b in bullets)if(b!=null)Destroy(b);bullets.Clear();bulletAges.Clear();
            Dead=false;Paused=false;Ammo=6;Shots=0;Speed=0;turn=0;aiming=false;shotPending=false;
            demoTime=0;nextDemoShot=0;ActionTime=0;duration=0;Action="Locomotion";
            transform.SetPositionAndRotation(origin,originRotation);
            if(animator!=null) { animator.speed=1;animator.Rebind();animator.Update(0);ApplyAnimator(); }
        }
        public void RestoreMagazine(int rounds){if(rounds<0||rounds>6)throw new ArgumentOutOfRangeException(nameof(rounds));Ammo=rounds;}
        private void Begin(string action,float seconds) { shotPending=false;Action=action;ActionTime=0;duration=seconds;ApplyAnimator(); }
        public void Tick(float delta)
        {
            if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0||delta>1)throw new ArgumentException("Invalid simulation delta.");
            if(Paused)return;
            if(automaticDemo)Demo(delta);
            if(Action!="Locomotion")
            {
                var previous=ActionTime;ActionTime+=delta;
                if(Action=="Fire"&&previous<.08f&&ActionTime>=.08f)shotPending=true;
                if(!Dead&&ActionTime>=duration)
                { if(Action=="Reload")Ammo=reloadTarget;Action="Locomotion";ActionTime=0; }
            }
            else if(!Dead)
            {
                if(driveRootTranslation) {
                    transform.Rotate(0,turn*delta,0,Space.World);
                    if(!aiming)transform.position+=transform.forward*(Speed*delta);
                }
            }
            for(var i=bullets.Count-1;i>=0;i--)
            {
                bulletAges[i]+=delta;
                if(bullets[i]==null||bulletAges[i]>.8f)
                {if(bullets[i]!=null)Destroy(bullets[i]);bullets.RemoveAt(i);bulletAges.RemoveAt(i);}
                else bullets[i].transform.position+=bullets[i].transform.forward*(18*delta);
            }
            ApplyAnimator();
        }
        public void FlushShotMarkerForTest() { LateUpdate(); }
        private void Emit()
        {
            if(muzzle==null)return;
            LastMuzzlePosition=muzzle.position;LastShotDirection=muzzle.forward;Shots++;
            ShotEmitted?.Invoke(LastMuzzlePosition,LastShotDirection);
            if(!emitTrainingProjectiles)return;
            var bullet=GameObject.CreatePrimitive(PrimitiveType.Sphere);bullet.name="S03 visible training projectile";
            var collider=bullet.GetComponent<Collider>();if(collider!=null)Destroy(collider);
            bullet.transform.SetPositionAndRotation(muzzle.position,muzzle.rotation);bullet.transform.localScale=new Vector3(.035f,.035f,.12f);
            bullets.Add(bullet);bulletAges.Add(0);
        }
        private void ApplyAnimator()
        {
            if(animator==null||animator.runtimeAnimatorController==null)return;
            animator.applyRootMotion=false;
            animator.SetFloat("Speed",Action=="Locomotion"&&!aiming&&!Dead?Speed:0);
            var mode=Dead?5:Action=="Fire"?2:Action=="Reload"?3:Action=="Hit"?4:aiming?1:0;
            animator.SetInteger("Mode",mode);
        }
        private void Demo(float dt)
        {
            demoTime+=dt;
            if(demoTime<2)SetLocomotion(0,0,false);
            else if(demoTime<4)SetLocomotion(1,0,false);
            else if(demoTime<5.2f)SetLocomotion(2,0,false);
            else if(demoTime<6.2f)SetLocomotion(0,0,true);
            else if(demoTime<7.6f) { if(demoTime>=nextDemoShot&&TryFire())nextDemoShot=demoTime+.5f; }
            else if(demoTime<9.4f) { if(Action=="Locomotion"&&Ammo<6)TryReload(); }
            else if(demoTime<9.8f) { if(Action=="Locomotion")Hit(); }
            else if(demoTime<12.8f) { if(!Dead)Hit(true); }
            else ResetDemo();
        }
        private void OnGUI()
        {
            if(!showControls)return;
            GUI.Box(new Rect(10,10,720,110),"S03 ANIMATION LAB — original parameterized motion / technical mannequin");
            GUI.Label(new Rect(25,35,690,25),"State: "+Action+" | ammo "+Ammo+" | shots "+Shots+" | speed "+Speed.ToString("0.0")+" | root motion OFF");
            if(GUI.Button(new Rect(25,65,100,30),Paused?"Resume":"Pause"))SetPaused(!Paused);
            if(GUI.Button(new Rect(130,65,100,30),"Manual"))automaticDemo=false;
            if(GUI.Button(new Rect(235,65,80,30),"Fire"))TryFire();
            if(GUI.Button(new Rect(320,65,80,30),"Reload"))TryReload();
            if(GUI.Button(new Rect(405,65,80,30),"Hit"))Hit();
            if(GUI.Button(new Rect(490,65,80,30),"Death"))Hit(true);
            if(GUI.Button(new Rect(575,65,100,30),"Restart demo")){ResetDemo();automaticDemo=true;}
        }
        private void OnDestroy() {foreach(var b in bullets)if(b!=null)Destroy(b);}
    }
}
""";
}
