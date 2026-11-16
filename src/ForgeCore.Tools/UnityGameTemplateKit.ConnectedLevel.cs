namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string ConnectedLevelSource="""
using System;
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    public sealed class ConnectedIndustrialLevel:MonoBehaviour
    {
        public SecurityCombatSession session;
        public BoxCollider[] doors;
        public Transform[] leaves;
        public Vector3[] closedPositions;
        public string Zone(Vector3 p)=>p.z<16?"SERVICE YARD":p.x<16?"WAREHOUSE":"CONTROL ROOM";
        public void Synchronize(int stage) {
            if(doors==null||doors.Length!=2||leaves==null||leaves.Length!=2)throw new InvalidOperationException("Two bounded mission gates required.");
            for(var i=0;i<2;i++){var open=stage>=i+1;doors[i].enabled=!open;leaves[i].localPosition=closedPositions[i]+(open?Vector3.up*4.2f:Vector3.zero);}
            Physics.SyncTransforms();if(session.Navigation!=null)session.Navigation.Refresh();
        }
        public bool CheckCheckpointClear(Vector3 point,int stage) {
            // Synchronous hypothetical-collision check; failed loads retain current mission/door state.
            if(stage<0||stage>2)return false;
            var prior=new[]{doors[0].enabled,doors[1].enabled};var positions=new[]{leaves[0].localPosition,leaves[1].localPosition};
            try{for(var i=0;i<2;i++){var open=stage>=i+1;doors[i].enabled=!open;leaves[i].localPosition=closedPositions[i]+(open?Vector3.up*4.2f:Vector3.zero);}Physics.SyncTransforms();
                return Physics.Raycast(point+Vector3.up*.2f,Vector3.down,4,1<<8,QueryTriggerInteraction.Ignore)&&!Physics.CheckCapsule(point+Vector3.up*.35f,point+Vector3.up*1.45f,.25f,1<<8,QueryTriggerInteraction.Ignore);
            }finally{for(var i=0;i<2;i++){leaves[i].localPosition=positions[i];doors[i].enabled=prior[i];}Physics.SyncTransforms();}
        }
    }
}
""";
}

