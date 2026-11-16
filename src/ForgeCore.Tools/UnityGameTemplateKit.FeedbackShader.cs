namespace ForgeCore.Tools;
internal static partial class UnityGameTemplateKit
{
    private const string FeedbackShaderSource = """
Shader "ForgeCore/SoftWarmFeedback"
{
    Properties { _Color ("Tint", Color) = (1,0.8,0.3,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;
                float radial=saturate(1-dot(p,p));
                float soft=radial*radial;
                return fixed4(_Color.rgb*i.color.rgb,_Color.a*i.color.a*soft);
            }
            ENDCG
        }
    }
    Fallback Off
}
""";
}
