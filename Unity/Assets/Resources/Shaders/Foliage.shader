Shader "Dala/Foliage"
{
    // Instanced cross-quad plants (meshes from Blender). Soft wind sway weighted by height, alpha cutout,
    // up-facing normals (even lighting like shader-pack grass), subtle translucency, fog.
    Properties
    {
        _BaseMap ("Foliage Atlas", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Sway ("Sway Amount", Float) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Cutoff;
            float _Sway;
        CBUFFER_END
        half4 _AmbientSky, _AmbientGround;
        float4 _WindDir;       // xz = direction, w unused
        float _WindStrength;   // 0..2

        float3 WindOffset(float3 posWS, float h)
        {
            float t = _Time.y;
            float phase = posWS.x * 0.37 + posWS.z * 0.29;
            float gust = sin(t * 0.35 + phase * 0.15) * 0.5 + 0.5;
            float sway = sin(t * 1.6 + phase) * 0.6 + sin(t * 2.9 + phase * 1.7) * 0.25;
            float s = (sway + gust * 0.8) * _Sway * (0.4 + _WindStrength) * h * h;
            float flutter = sin(t * 7.0 + phase * 3.0) * 0.015 * h * _WindStrength;
            return float3(_WindDir.x * s + flutter, -abs(s) * 0.15, _WindDir.z * s - flutter);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DalaLight.hlsl"

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 posWS : TEXCOORD1; half h : TEXCOORD2; half fog : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                float h = saturate(i.pos.y);                  // plant height in metres -> sway weight
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                ws += WindOffset(ws, h);
                o.posWS = ws;
                o.pos = TransformWorldToHClip(ws);
                o.uv = i.uv;
                o.h = h;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                half ao = lerp(0.55, 1, saturate(i.h * 1.6));
                half3 ambient = lerp(_AmbientGround.rgb, _AmbientSky.rgb, 0.75);
                half direct = (0.65 + 0.35 * saturate(sun.direction.y)) * sun.shadowAttenuation;
                half blockL = max(PointLevel(i.posWS), HandLevel(i.posWS));
                half3 c = tex.rgb * (ambient * ao + sun.color * direct * ao + BlockLightTerm(blockL) * ao * 2.0 + 0.03);
                c += tex.rgb * sun.color * 0.12 * i.h;      // light through the blades
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                ws += WindOffset(ws, saturate(i.pos.y));
                o.pos = TransformWorldToHClip(ApplyShadowBias(ws, float3(0,1,0), _LightDirection));
                o.uv = i.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
