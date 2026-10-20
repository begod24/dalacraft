Shader "Dala/Particle"
{
    // Pixel-art particles (weather, fire, smoke, sparks). Alpha-blended or additive via _SrcBlend/_DstBlend,
    // vertex colour from the particle system, optional HDR boost for bloom, distance fog.
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Boost ("HDR Boost", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Boost;
                float _SrcBlend, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; half fog : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.col = i.col;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.col;
                c.rgb *= _Boost;
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
