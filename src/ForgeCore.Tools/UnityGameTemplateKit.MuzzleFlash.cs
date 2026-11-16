namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string MuzzleFlashSource = """
using UnityEngine;
using UnityEngine.Rendering;
namespace ForgeCoreGameTemplates
{
    // Original soft additive gas/glow geometry. A Resources shader is explicitly bundled
    // so the player does not depend on runtime-only Standard emission variants.
    public sealed class PrototypeMuzzleFlash:MonoBehaviour
    {
        public const float Lifetime=.055f;
        public static int ActiveCount {get;private set;}
        private Material[] owned;private Mesh mesh;private Light glow;
        private bool timed;private float age;private static int sequence;
        public static Shader FeedbackShader=>Resources.Load<Shader>("ForgeCoreSoftWarmFeedback");
        public static Material CreateFeedbackMaterial(Color color,float intensity=1)
        {
            var shader=FeedbackShader;
            if(shader==null||SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null&&!shader.isSupported)
            {Debug.LogError("ForgeCore bundled feedback shader is missing or unsupported; refusing error-material rendering.");return null;}
            return MakeMaterial(shader,color,intensity);
        }
        public static PrototypeMuzzleFlash Spawn(Vector3 position,Vector3 direction)
        {
            if(!Application.isPlaying||ActiveCount>=32||!Finite(position)||!Finite(direction)||direction.sqrMagnitude<.001f)return null;
            var shader=FeedbackShader;
            if(shader==null||SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null&&!shader.isSupported)return null;
            var go=new GameObject("Original soft warm muzzle flash");
            go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,(sequence++*137.5f)%360));
            if(SecurityCombatSession.Instance!=null)go.transform.SetParent(SecurityCombatSession.Instance.transform,true);
            else if(PrototypeGame.Instance!=null)go.transform.SetParent(PrototypeGame.Instance.transform,true);
            var effect=go.AddComponent<PrototypeMuzzleFlash>();effect.timed=true;ActiveCount++;
            effect.owned=new[]{MakeMaterial(shader,new Color(1,.38f,.045f),1.7f),MakeMaterial(shader,new Color(1,.87f,.52f),2.4f)};
            effect.mesh=BuildMesh();go.AddComponent<MeshFilter>().sharedMesh=effect.mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=effect.owned;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            effect.glow=go.AddComponent<Light>();effect.glow.type=LightType.Point;effect.glow.color=new Color(1,.65f,.25f);
            effect.glow.range=2;effect.glow.intensity=2;effect.glow.shadows=LightShadows.None;
            return effect;
        }
        public static Material AssignParticleMaterial(GameObject owner,Color color)
        {
            var material=CreateFeedbackMaterial(color,1.5f);if(material==null)return null;
            var lifetime=owner.AddComponent<PrototypeMuzzleFlash>();lifetime.owned=new[]{material};
            owner.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            return material;
        }
        private static Material MakeMaterial(Shader shader,Color color,float intensity)
        {
            color.r*=intensity;color.g*=intensity;color.b*=intensity;
            return new Material(shader){name="Owned bundled soft warm feedback",color=color};
        }
        private static Mesh BuildMesh()
        {
            var vertices=new System.Collections.Generic.List<Vector3>();
            var uv=new System.Collections.Generic.List<Vector2>();var colors=new System.Collections.Generic.List<Color>();
            var outer=new System.Collections.Generic.List<int>();var core=new System.Collections.Generic.List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,System.Collections.Generic.List<int> indices)
            {
                var n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
                for(var i=0;i<4;i++)colors.Add(Color.white);
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            // Crossed tapered gas plumes read from the side, plus soft axial flare layers seen from the eye.
            for(var i=0;i<3;i++) {
                var angle=i*Mathf.PI/3;var axis=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                Quad(-axis*.045f,axis*.045f,axis*.013f+Vector3.forward*.19f,-axis*.013f+Vector3.forward*.19f,outer);
            }
            // Uneven soft gas lobes break up the circular halo when looking along the barrel.
            for(var i=0;i<5;i++) {
                var angle=i*Mathf.PI*.4f;var axis=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                var tangent=new Vector3(-axis.y,axis.x,0);var length=i%2==0?.075f:.060f;var width=.017f;
                var near=axis*.01f+Vector3.forward*.055f;var far=axis*length+Vector3.forward*.055f;
                Quad(near-tangent*width,near+tangent*width,far+tangent*width*.6f,far-tangent*width*.6f,outer);
            }
            for(var i=0;i<3;i++) {
                var radius=i==0?.049f:i==1?.032f:.018f;var z=.025f+i*.045f;
                Quad(new Vector3(-radius,-radius,z),new Vector3(radius,-radius,z),new Vector3(radius,radius,z),new Vector3(-radius,radius,z),i==0?core:outer);
            }
            var value=new Mesh{name="Owned original soft gas plume"};value.SetVertices(vertices);value.SetUVs(0,uv);value.SetColors(colors);value.subMeshCount=2;
            value.SetTriangles(outer,0);value.SetTriangles(core,1);value.RecalculateNormals();value.RecalculateBounds();return value;
        }
        private void Update()
        {
            if(!timed)return;
            var combat=SecurityCombatSession.Instance;var game=PrototypeGame.Instance;
            if(combat!=null&&combat.Paused||game!=null&&game.MenuOpen)return;
            age+=Time.deltaTime;if(age>=Lifetime){Destroy(gameObject);return;}
            var remaining=1-age/Lifetime;transform.localScale=Vector3.one*Mathf.Lerp(.7f,1,remaining);
            foreach(var material in owned){var color=material.color;color.a=remaining;material.color=color;}
            if(glow!=null)glow.intensity=2*(1-age/Lifetime);
        }
        private void OnDestroy()
        {
            if(timed)ActiveCount=Mathf.Max(0,ActiveCount-1);
            if(owned!=null)foreach(var material in owned)if(material!=null)Destroy(material);
            if(mesh!=null)Destroy(mesh);
        }
        private static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    }
}
""";
}
