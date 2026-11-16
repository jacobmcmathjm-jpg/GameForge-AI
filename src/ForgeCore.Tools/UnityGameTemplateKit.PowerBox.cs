namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string IndustrialPowerBoxSource = """
using UnityEngine;
namespace ForgeCoreGameTemplates
{
    // Physical mission prop, separate from the legacy disposable objective marker.
    public sealed class IndustrialPowerBox:MonoBehaviour
    {
        public BoxCollider solidCollider;
        public Renderer statusLamp;
        public Material standbyMaterial,poweredMaterial;
        public TextMesh statusPlate;
        public bool IsPowered {get;private set;}
        public bool WithinReach(Vector3 playerPosition)=>solidCollider!=null&&solidCollider.enabled&&gameObject.activeInHierarchy&&
            Vector3.Distance(playerPosition,solidCollider.ClosestPoint(playerPosition))<=2;
        public void SetPowered(bool value)
        {
            IsPowered=value;
            if(statusLamp!=null)statusLamp.sharedMaterial=value?poweredMaterial:standbyMaterial;
            if(statusPlate!=null)statusPlate.text=value?"POWER / ONLINE":"POWER / OFFLINE";
        }
    }
}
""";
}
