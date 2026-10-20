Shader "Dala/UIInvert"
{
    // Minecraft-style crosshair: inverts whatever is behind it, so it reads on sky, snow, water or stone.
    Properties { _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend OneMinusDstColor OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = UnityObjectToClipPos(i.pos); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a;
                return half4(a, a, a, a);   // a=1 → 1 - dst (inverted), a=0 → dst unchanged
            }
            ENDCG
        }
    }
}
