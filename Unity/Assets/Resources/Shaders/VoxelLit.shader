Shader "Dala/VoxelLit"
{
    // Lit voxel terrain: sun (N·L + soft shadows), sky/ground ambient, per-vertex AO (vertex colour), distance fog.
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _Tint ("Season Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint;
        CBUFFER_END
        // Set globally by WorldVisuals
        half4 _AmbientSky, _AmbientGround;
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct Varyings
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR;
                float3 posWS : TEXCOORD1; half3 normalWS : TEXCOORD2; half fog : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(i.normal);
                o.uv = i.uv;
                o.col = i.col;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 texc = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(texc.a - 0.5);                                   // see-through gaps in leaves
                half3 albedo = texc.rgb * _Tint.rgb;
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                half ndl = saturate(dot(n, sun.direction));
                half3 ambient = lerp(_AmbientGround.rgb, _AmbientSky.rgb, n.y * 0.5 + 0.5);
                half ao = i.col.r;
                half3 c = albedo * (ambient * ao + sun.color * ndl * sun.shadowAttenuation * lerp(0.6, 1, ao));
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.uv = i.uv;
                float3 posWS = TransformObjectToWorld(i.pos.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normal);
                o.pos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.pos.z = min(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.pos.z = max(o.pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 frag(Varyings i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - 0.5); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; };
            Varyings vert(Attributes i) { Varyings o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
