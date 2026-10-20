Shader "Dala/Water"
{
    // Shader-pack style water: layered moving waves, sky + sun reflection (fresnel), refraction of the bottom,
    // depth-based colour / transparency, shoreline foam, distance fog. Sky colours come from WorldVisuals globals.
    Properties
    {
        _Shallow ("Shallow", Color) = (0.30, 0.68, 0.72, 1)
        _Deep ("Deep", Color) = (0.08, 0.30, 0.45, 1)
        _DepthRange ("Depth Range (m)", Float) = 7
        _WaveHeight ("Wave Height", Float) = 0.06
        _WaveScale ("Wave Scale", Float) = 1
        _Refraction ("Refraction", Float) = 0.04
        _Foam ("Foam", Float) = 0.6
        _PixelSnap ("Pixel Snap (0..1)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep;
                float _DepthRange, _WaveHeight, _WaveScale, _Refraction, _Foam, _PixelSnap;
            CBUFFER_END
            half4 _SkyZenith, _SkyHorizon;
            float _WindStrength;

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Varyings
            {
                float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; half3 normalWS : TEXCOORD1;
                float4 screen : TEXCOORD2; half fog : TEXCOORD3;
            };

            // sum of directional sine waves -> height and analytic slope
            float3 Waves(float2 p, float t)
            {
                float h = 0; float2 d = 0;
                const float4 W[5] = {
                    float4( 1.0,  0.3, 0.55, 1.1),  // dir.xy, frequency, speed
                    float4(-0.4,  1.0, 0.80, 1.4),
                    float4( 0.7, -0.7, 1.35, 1.9),
                    float4(-0.9, -0.2, 2.10, 2.6),
                    float4( 0.2,  0.9, 3.30, 3.4) };
                const float A[5] = { 1.0, 0.6, 0.35, 0.22, 0.12 };
                [unroll] for (int i = 0; i < 5; i++)
                {
                    float2 dir = normalize(W[i].xy);
                    float ph = dot(dir, p) * W[i].z * _WaveScale + t * W[i].w;
                    h += sin(ph) * A[i];
                    d += cos(ph) * A[i] * W[i].z * _WaveScale * dir;
                }
                return float3(h, d);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                float amp = _WaveHeight * (0.6 + _WindStrength * 0.6);
                if (i.normal.y > 0.5) ws.y += Waves(ws.xz, _Time.y).x * amp * 0.5 - amp;
                o.posWS = ws;
                o.pos = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normal);
                o.screen = ComputeScreenPos(o.pos);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half3 SkyColor(half3 dir)
            {
                half up = saturate(dir.y);
                return lerp(_SkyHorizon.rgb, _SkyZenith.rgb, pow(up, 0.5));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.posWS.xz;
                p = lerp(p, floor(p * 16) / 16, _PixelSnap);
                float3 w = Waves(p, t);
                float3 w2 = Waves(p * 2.7 + 13.1, t * 1.3);
                float amp = 0.10 + _WindStrength * 0.08;
                half3 n = normalize(half3(-(w.y + w2.y * 0.4) * amp, 1, -(w.z + w2.z * 0.4) * amp));
                if (i.normalWS.y < 0.5) n = normalize(i.normalWS);

                half3 v = normalize(GetWorldSpaceViewDir(i.posWS));
                float2 suv = i.screen.xy / i.screen.w;

                // water depth from the scene depth buffer
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfZ = i.screen.w;
                float depth = max(0, sceneZ - surfZ);
                float2 ruv = suv + n.xz * _Refraction * saturate(depth);
                float rZ = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rZ < surfZ) ruv = suv;                          // don't refract things in front of the water
                half3 bottom = SampleSceneColor(ruv);

                float k = saturate(depth / _DepthRange);
                half3 waterCol = lerp(_Shallow.rgb, _Deep.rgb, k);
                half3 refracted = lerp(bottom * lerp(_Shallow.rgb * 1.5, _Deep.rgb * 2.0, k), waterCol, saturate(k * 0.75 + 0.08));

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                half3 r = reflect(-v, n);
                half3 refl = SkyColor(r);
                half fres = 0.04 + 0.96 * pow(1 - saturate(dot(n, v)), 5);
                half3 c = lerp(refracted, refl, saturate(fres * 1.15));

                // sun glitter (HDR so bloom picks it up)
                half3 hdir = normalize(sun.direction + v);
                half spec = pow(saturate(dot(n, hdir)), 600) * 40 + pow(saturate(dot(n, hdir)), 60) * 0.6;
                c += sun.color * spec * sun.shadowAttenuation;

                // shoreline foam
                half foam = saturate(1 - depth / 0.35) * _Foam;
                foam *= step(0.45, frac(w.x * 0.5 + w2.x * 0.3 + t * 0.15) * 0.5 + 0.5 - depth);
                c = lerp(c, half3(0.92, 0.95, 1.0) * (0.6 + 0.4 * sun.shadowAttenuation), foam);

                // stays see-through: the bottom, fish and plants remain visible even in deep water
                half alpha = saturate(min(0.38 + k * 0.4 + fres * 0.3, 0.86) + foam);
                return half4(MixFog(c, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
