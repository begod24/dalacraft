Shader "Dala/Cracks"
{
    // Block-breaking cracks drawn on the block itself: T_Cracks has 10 stages side by side; multiplied 2x over the scene
    // (Blend DstColor SrcColor) so bare pixels leave the block unchanged and crack pixels darken it, like Minecraft.
    Properties { _MainTex ("Cracks", 2D) = "white" {} _Stage ("Stage", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend DstColor SrcColor
            ZWrite Off ZTest LEqual Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float _Stage; float4 _MainTex_ST; CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float2 uv = float2((saturate(i.uv.x) * 0.999 + floor(_Stage)) / 10.0, i.uv.y);
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                return half4(lerp(half3(0.5, 0.5, 0.5), half3(0.16, 0.15, 0.14), a), 1);
            }
            ENDHLSL
        }
    }
}
