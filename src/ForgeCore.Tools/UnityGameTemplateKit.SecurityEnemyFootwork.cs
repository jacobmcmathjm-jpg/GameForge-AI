namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string SecurityEnemyFootworkSource = """
using System;
using System.Linq;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    // Post-Animator, distance/turn-driven placement for the bounded flat security lab only.
    // The controller owns the root; this pass owns leg contacts and corpse visual grounding.
    // No navigation, ragdoll, arbitrary terrain, stairs or motion-capture quality is implied.
    internal sealed class SecurityEnemyFootwork : IDisposable
    {
        private sealed class Leg
        {
            public Transform upper,lower,foot;
            public Vector3 rest,anchor,swingOrigin,target;
            public Quaternion restRotation,anchorRotation,swingRotation,rotation;
            public float ankle;
            public bool planted=true;
        }
        private sealed class Arm
        {
            public Transform upper,lower,hand;public Quaternion restRotation;
        }
        private readonly Transform root,visual,hips,chest;
        private readonly SkinnedMeshRenderer skin;
        private readonly Leg[] legs;
        private readonly Arm[] arms;
        private readonly bool[] torsoVertices;private readonly byte[] handVertices;
        private readonly Vector3 visualRest;
        private Mesh corpseMesh;
        private float phase,delta,lead;
        private float lastCorpseTime;
        private bool stepping,corpseSettled;
        private int sequence,lastTargets=-1;
        public float MaximumPlantError { get; private set; }
        public float CorpseGroundError { get; private set; }
        public int PlantedFeet => legs.Count(l=>l.planted);
        public bool IsPlanted(int side)=>legs[side].planted;
        public Vector3 Target(int side)=>legs[side].target;
        public float Phase => phase;
        public SecurityEnemyFootwork(Transform owner,PrototypeSkinnedAnimator motion)
        {
            root=owner;visual=motion.transform;visualRest=visual.localPosition;
            var bones=visual.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name,t=>t);
            hips=bones["Hips"];chest=bones["Chest"];skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var weights=skin.sharedMesh.boneWeights;torsoVertices=new bool[weights.Length];handVertices=new byte[weights.Length];
            for(var i=0;i<weights.Length;i++)
            {
                var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};var torso=0f;
                for(var j=0;j<4;j++)
                {
                    var name=skin.bones[ids[j]].name;
                    if(name=="Hips"||name=="Spine"||name=="Chest")torso+=amounts[j];
                    if(amounts[j]>.5f){if(name=="Hand.L")handVertices[i]=1;if(name=="Hand.R")handVertices[i]=2;}
                }
                torsoVertices[i]=torso>.5f;
            }
            legs=new[]{MakeLeg(bones,"L"),MakeLeg(bones,"R")};
            arms=new[]{MakeArm(bones,"L"),MakeArm(bones,"R")};Reset();
        }
        private Arm MakeArm(System.Collections.Generic.Dictionary<string,Transform> bones,string side)
        {
            var hand=bones["Hand."+side];
            return new Arm{upper=bones["UpperArm."+side],lower=bones["LowerArm."+side],hand=hand,restRotation=Quaternion.Inverse(root.rotation)*hand.rotation};
        }
        private Leg MakeLeg(System.Collections.Generic.Dictionary<string,Transform> bones,string side)
        {
            var foot=bones["Foot."+side];var rest=root.InverseTransformPoint(foot.position);
            return new Leg{upper=bones["UpperLeg."+side],lower=bones["LowerLeg."+side],foot=foot,
                rest=rest,ankle=Mathf.Max(.02f,rest.y),restRotation=Quaternion.Inverse(root.rotation)*foot.rotation};
        }
        private float Ground(Vector3 point)
        {
            RaycastHit hit;
            if(Physics.Raycast(new Vector3(point.x,root.position.y+.7f,point.z),Vector3.down,out hit,1.5f,1<<8,QueryTriggerInteraction.Ignore)&&
                Vector3.Dot(hit.normal,Vector3.up)>.9f&&Mathf.Abs(hit.point.y-root.position.y)<.3f)return hit.point.y;
            return root.position.y-.02f; // Flat-lab fallback; never step up onto a cover block.
        }
        private Vector3 OnFloor(Vector3 point,Leg leg){point.y=Ground(point)+leg.ankle;return point;}
        public void Advance(float travelled,float turned,float dt,bool allowSteps)
        {
            delta=dt;sequence++;
            stepping=allowSteps&&(travelled>.0001f||Mathf.Abs(turned)>.05f);
            var recovery=allowSteps&&!stepping&&legs.Any(l=>!l.planted||
                Vector3.ProjectOnPlane(l.anchor-root.TransformPoint(l.rest),Vector3.up).magnitude>.16f||
                Quaternion.Angle(l.anchorRotation,root.rotation*l.restRotation)>20);
            stepping|=recovery;
            // Complete a small alternating shuffle after a stop/large turn, rather than freeze crossed feet.
            if(stepping)phase=Mathf.Repeat(phase+travelled/.9f+Mathf.Abs(turned)/180f+(recovery?dt*.8f:0),1);
            lead=travelled/Mathf.Max(dt,.001f)>.08f?.18f:0;
        }
        private void Targets()
        {
            if(lastTargets==sequence)return;lastTargets=sequence;
            for(var i=0;i<2;i++)
            {
                var leg=legs[i];var p=Mathf.Repeat(phase+i*.5f,1);
                if(!stepping)
                {
                    // Finish a lifted foot's descent over time rather than snapping it to the floor.
                    if(!leg.planted) {
                        var floor=OnFloor(leg.target,leg);leg.target=Vector3.MoveTowards(leg.target,floor,.6f*delta);
                        if(Vector3.Distance(leg.target,floor)<.001f){leg.planted=true;leg.anchor=floor;leg.anchorRotation=leg.rotation;}
                    }
                    else{leg.target=leg.anchor;leg.rotation=leg.anchorRotation;}
                    continue;
                }
                if(p<.6f)
                {
                    if(!leg.planted){leg.planted=true;leg.anchor=OnFloor(leg.target,leg);leg.anchorRotation=leg.rotation;}
                    leg.target=leg.anchor;leg.rotation=leg.anchorRotation;
                }
                else
                {
                    if(leg.planted){leg.swingOrigin=leg.anchor;leg.swingRotation=leg.anchorRotation;leg.planted=false;}
                    var t=(p-.6f)/.4f;var u=ForgeCore.Motion.GuardMotionProfile.Ease(t);
                    var goal=OnFloor(root.TransformPoint(leg.rest)+root.forward*lead,leg);
                    leg.target=Vector3.Lerp(leg.swingOrigin,goal,u)+Vector3.up*(.09f*ForgeCore.Motion.GuardMotionProfile.SwingLift(t));
                    leg.rotation=Quaternion.Slerp(leg.swingRotation,root.rotation*leg.restRotation,u);
                }
            }
        }
        public void Apply(bool dead,float actionTime)
        {
            if(dead){SettleCorpsePose(actionTime);GroundCorpse(actionTime);return;}
            Targets();var lower=0f;
            foreach(var leg in legs)
            {
                var reach=(Vector3.Distance(leg.upper.position,leg.lower.position)+Vector3.Distance(leg.lower.position,leg.foot.position))*.985f;
                var offset=leg.upper.position-leg.target;var horizontal=Vector3.ProjectOnPlane(offset,Vector3.up).sqrMagnitude;
                if(horizontal<reach*reach)lower=Mathf.Max(lower,offset.y-Mathf.Sqrt(reach*reach-horizontal));
            }
            // Small pelvis compensation preserves reach as idle/walk clips blend; no root/controller teleport.
            hips.position-=Vector3.up*Mathf.Clamp(lower,0,.14f);MaximumPlantError=0;
            foreach(var leg in legs)
            {
                Solve(leg.upper,leg.lower,leg.foot,leg.target,root.forward);leg.foot.rotation=leg.rotation;
                if(leg.planted)MaximumPlantError=Mathf.Max(MaximumPlantError,Vector3.Distance(leg.foot.position,leg.target));
            }
        }
        private static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 bend)
        {
            var a=Vector3.Distance(upper.position,lower.position);var b=Vector3.Distance(lower.position,end.position);
            var offset=target-upper.position;if(offset.sqrMagnitude<.000001f)return;
            var d=Mathf.Clamp(offset.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);var axis=offset.normalized;
            var along=(a*a-b*b+d*d)/(2*d);var across=Vector3.ProjectOnPlane(bend,axis).normalized;
            var knee=upper.position+axis*along+across*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,knee-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
        }
        private void SettleCorpsePose(float actionTime)
        {
            // Death-only post-animation support pose. A toe touching the ground is not
            // enough: extended legs previously propped the whole torso 23cm in the air.
            var weight=ForgeCore.Motion.GuardMotionProfile.Ease((actionTime-.65f)/.65f);
            if(weight<=0)return;
            var axis=chest.position-hips.position;var forward=Vector3.ProjectOnPlane(axis,Vector3.up);
            if(axis.sqrMagnitude<.001f||forward.sqrMagnitude<.001f)return;
            forward.Normalize();var align=Quaternion.FromToRotation(axis.normalized,forward);
            hips.rotation=Quaternion.Slerp(Quaternion.identity,align,weight)*hips.rotation;
            for(var i=0;i<legs.Length;i++)
            {
                var leg=legs[i];var side=i==0?-1f:1f;
                // Flex the knees sideways/back rather than letting a downward-pointing
                // boot act as a rigid support. Bone lengths and gameplay root stay fixed.
                var target=leg.upper.position-forward*.65f+Vector3.up*.18f+root.right*(side*.12f);
                var footRotation=Quaternion.FromToRotation(Vector3.up,forward)*(root.rotation*leg.restRotation);
                Solve(leg.upper,leg.lower,leg.foot,Vector3.Lerp(leg.foot.position,target,weight),root.right*side-Vector3.up*.08f);
                leg.foot.rotation=Quaternion.Slerp(leg.foot.rotation,footRotation,weight);
            }
            for(var i=0;i<arms.Length;i++)
            {
                var arm=arms[i];var side=i==0?-1f:1f;
                var target=hips.position+forward*.28f+root.right*(side*.32f)-Vector3.up*.17f;
                var rotation=Quaternion.FromToRotation(Vector3.up,forward)*(root.rotation*arm.restRotation);
                // Relax the arms outwards; a downward elbow brace must not hold the chest up.
                Solve(arm.upper,arm.lower,arm.hand,Vector3.Lerp(arm.hand.position,target,weight),root.right*side+Vector3.up*.08f);
                arm.hand.rotation=Quaternion.Slerp(arm.hand.rotation,rotation,weight);
            }
            RelaxHandSupport(weight);
        }
        private void RelaxHandSupport(float weight)
        {
            using(var measured=DemoRuntimeTelemetry.Measure(ForgeCore.Demo.DemoWorkKind.CorpseSupport))RelaxHandSupportCore(weight);
        }
        private void RelaxHandSupportCore(float weight)
        {
            if(corpseMesh==null)corpseMesh=new Mesh{name="Owned security corpse grounding sample"};
            skin.BakeMesh(corpseMesh);var vertices=corpseMesh.vertices;
            var torso=float.PositiveInfinity;var left=float.PositiveInfinity;var right=float.PositiveInfinity;
            for(var i=0;i<vertices.Length;i++)
            {
                var y=skin.transform.TransformPoint(vertices[i]).y;
                if(torsoVertices[i])torso=Mathf.Min(torso,y);
                if(handVertices[i]==1)left=Mathf.Min(left,y);if(handVertices[i]==2)right=Mathf.Min(right,y);
            }
            if(float.IsInfinity(torso))return;
            for(var i=0;i<arms.Length;i++)
            {
                var min=i==0?left:right;if(float.IsInfinity(min))continue;
                var lift=Mathf.Clamp(torso-min+.012f,0,.25f)*weight;if(lift<=0)continue;
                var arm=arms[i];var rotation=arm.hand.rotation;var side=i==0?-1f:1f;
                Solve(arm.upper,arm.lower,arm.hand,arm.hand.position+Vector3.up*lift,root.right*side+Vector3.up*.08f);
                arm.hand.rotation=rotation;
            }
        }
        private void GroundCorpse(float actionTime)
        {
            using(var measured=DemoRuntimeTelemetry.Measure(ForgeCore.Demo.DemoWorkKind.CorpseSupport))GroundCorpseCore(actionTime);
        }
        private void GroundCorpseCore(float actionTime)
        {
            if(corpseSettled||skin==null)return;
            if(corpseMesh==null)corpseMesh=new Mesh{name="Owned security corpse grounding sample"};
            skin.BakeMesh(corpseMesh);var min=float.PositiveInfinity;
            foreach(var vertex in corpseMesh.vertices)min=Mathf.Min(min,skin.transform.TransformPoint(vertex).y);
            var floor=Ground(root.position)+.005f;
            var correction=Mathf.Clamp(floor-min,-.5f,.5f);
            var elapsed=Mathf.Max(0,actionTime-lastCorpseTime);lastCorpseTime=actionTime;
            // Prevent penetration immediately, but do not snap an airborne collapsing
            // body down to the floor. Controlled descent does not move the gameplay root.
            if(correction<0&&actionTime<1.7f)correction=Mathf.Max(correction,-1.2f*elapsed);
            visual.position+=Vector3.up*correction;
            CorpseGroundError=Mathf.Abs(min+correction-floor);
            if(actionTime>=1.7f&&CorpseGroundError<.002f)corpseSettled=true;
        }
        public void Reset()
        {
            visual.localPosition=visualRest;phase=delta=lead=lastCorpseTime=0;stepping=corpseSettled=false;sequence++;lastTargets=-1;
            MaximumPlantError=CorpseGroundError=0;
            foreach(var leg in legs){leg.anchor=leg.target=leg.swingOrigin=OnFloor(root.TransformPoint(leg.rest),leg);
                leg.rotation=leg.anchorRotation=leg.swingRotation=root.rotation*leg.restRotation;leg.planted=true;}
        }
        public void Dispose(){if(corpseMesh!=null)UnityEngine.Object.Destroy(corpseMesh);}
    }
}
""";
}
