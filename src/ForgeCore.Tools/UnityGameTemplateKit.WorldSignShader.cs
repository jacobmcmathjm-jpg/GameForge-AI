namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    // Separate from HUD text: physical route signs must be hidden by opaque world geometry.
    private const string WorldSignShaderSource="""
Shader "ForgeCore/WorldSignText"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i) : SV_Target { fixed4 c=i.color; c.a*=tex2D(_MainTex,i.uv).a; return c; }
            ENDCG
        }
    }
    Fallback Off
}
""";
}
