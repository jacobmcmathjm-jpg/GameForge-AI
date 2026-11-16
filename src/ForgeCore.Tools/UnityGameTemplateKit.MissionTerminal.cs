namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndustrialMissionTerminalSource = """
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    public sealed class IndustrialMissionTerminal:MonoBehaviour
    {
        public string label="MANIFEST";
        public BoxCollider solidCollider;public Renderer statusLamp;public TextMesh statusPlate;
        public Material lockedMaterial,readyMaterial,usedMaterial;
        public bool Available {get;private set;}public bool Used {get;private set;}
        public bool WithinReach(Vector3 position)=>solidCollider!=null&&solidCollider.enabled&&gameObject.activeInHierarchy&&
            Vector3.Distance(position,solidCollider.ClosestPoint(position))<=2;
        public void SetState(bool available,bool used) {
            Available=available&&!used;Used=used;
            if(statusLamp!=null)statusLamp.sharedMaterial=used?usedMaterial:available?readyMaterial:lockedMaterial;
            if(statusPlate!=null)statusPlate.text=label+(used?" / COMPLETE":available?" / READY":" / LOCKED");
        }
    }
}
""";
}
