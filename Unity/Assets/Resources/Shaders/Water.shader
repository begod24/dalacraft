Shader "Dala/Water"
{
    // Shader-pack style lake / river water:
    //  * gentle layered waves (vertex + normals), calmer than open sea
    //  * soft swell lines that roll towards the shore (phase driven by water depth) and break into foam on the beach
    //  * see-through: refracted bottom with caustic light, depth-tinted turquoise → deep blue
    //  * screen-space reflections of shores and trees over the sky, big HDR sun glare + ripple sparkle (bloom),
    //    subsurface glow on wave crests, distance fog
    // Sky colours come from WorldVisuals globals.
    Properties
    {
        _Shallow ("Shallow", Color) = (0.40, 0.76, 0.78, 1)
        _Deep ("Deep", Color) = (0.14, 0.40, 0.55, 1)
        _DepthRange ("Depth Range (m)", Float) = 7
        _WaveHeight ("Wave Height", Float) = 0.035
        _WaveScale ("Wave Scale", Float) = 1
        _Refraction ("Refraction", Float) = 0.035
        _Foam ("Foam", Float) = 0.7
        _ShoreWaves ("Shore Waves", Range(0,1)) = 0.6
        _Caustics ("Caustics", Range(0,1)) = 0.5
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
                float _DepthRange, _WaveHeight, _WaveScale, _Refraction, _Foam, _ShoreWaves, _Caustics, _PixelSnap;
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
                    float4( 1.0,  0.3, 0.55, 0.9),  // dir.xy, frequency, speed
                    float4(-0.4,  1.0, 0.80, 1.1),
                    float4( 0.7, -0.7, 1.35, 1.5),
                    float4(-0.9, -0.2, 2.10, 2.0),
                    float4( 0.2,  0.9, 3.30, 2.6) };
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

            // bright web of light on the bottom, like sunlight through ripples
            half Caustics(float2 p, float t)
            {
                float a = sin(dot(p, float2(2.3, 1.1)) + t * 1.1 + sin(p.y * 1.7 + t * 0.7) * 1.4);
                float b = sin(dot(p, float2(-1.3, 2.1)) - t * 0.9 + sin(p.x * 1.9 - t * 0.6) * 1.4);
                return pow(saturate(1 - abs(a + b) * 0.6), 4);
            }

            // Screen-space reflection: march the reflected ray through the depth buffer and pick up the shore,
            // trees and hills from the opaque colour texture. Falls back to the sky where nothing is hit.
            half3 ScreenReflection(float3 posWS, half3 r, out half hit)
            {
                hit = 0;
                float3 pos = posWS + r * 0.05;
                float stepLen = 0.3;
                [loop] for (int s = 0; s < 40; s++)
                {
                    pos += r * stepLen;
                    stepLen *= 1.1;
                    float4 cp = TransformWorldToHClip(pos);
                    if (cp.w <= 0.01) break;
                    float4 sp = ComputeScreenPos(cp);
                    float2 uv = sp.xy / sp.w;
                    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) break;
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                    float diff = cp.w - sceneZ;
                    if (diff > 0 && diff < stepLen * 2.0 + 0.25)
                    {
                        float2 edge = smoothstep(0, 0.12, uv) * smoothstep(1, 0.88, uv);
                        hit = edge.x * edge.y * saturate(1.2 - s / 40.0);
                        return SampleSceneColor(uv);
                    }
                }
                return 0;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.posWS.xz;
                p = lerp(p, floor(p * 16) / 16, _PixelSnap);
                float3 w = Waves(p, t);
                float3 w2 = Waves(p * 2.7 + 13.1, t * 1.3);
                float3 w3 = Waves(p * 6.3 + 41.7, t * 1.9);          // fine ripples: sparkle on the sun path

                half3 v = normalize(GetWorldSpaceViewDir(i.posWS));
                float2 suv = i.screen.xy / i.screen.w;

                // water depth under this pixel from the scene depth buffer
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfZ = i.screen.w;
                float depth = max(0, sceneZ - surfZ);

                // ---- swell rolling to the shore: crests travel towards shallower water, fade out in deep water ----
                float shore = saturate(1 - depth / 4.0) * _ShoreWaves;
                float swellPh = depth * 2.6 + t * 1.25 + w.x * 0.35;
                float swell = sin(swellPh);
                float crest = pow(saturate(swell * 0.5 + 0.5), 5) * shore;

                // normals: calm waves + ripples + the swell's slope (screen-space derivative of its height)
                float amp = 0.07 + _WindStrength * 0.06;
                half3 n = normalize(half3(-(w.y + w2.y * 0.45 + w3.y * 0.18) * amp, 1, -(w.z + w2.z * 0.45 + w3.z * 0.18) * amp));
                float sh = swell * shore * 0.04;
                float2 dP = float2(length(ddx(p)), length(ddy(p))) + 1e-4;
                n = normalize(n + half3(-ddx(sh) / dP.x, 0, -ddy(sh) / dP.y) * 0.5);
                if (i.normalWS.y < 0.5) n = normalize(i.normalWS);
                bool fromBelow = v.y < 0;

                float2 ruv = suv + n.xz * _Refraction * saturate(depth);
                float rZ = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rZ < surfZ) ruv = suv;                          // don't refract things in front of the water
                half3 bottom = SampleSceneColor(ruv);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                float k = saturate(depth / _DepthRange);

                // caustic light on the bottom, strongest in the shallows and in sunlight
                float2 bottomP = p + v.xz * depth * 0.5;
                half caus = Caustics(bottomP * 1.3, t) * _Caustics * (1 - k) * sun.shadowAttenuation;
                bottom *= 1 + caus * 0.9 * (fromBelow ? 0 : 1);

                half3 waterCol = lerp(_Shallow.rgb, _Deep.rgb, k) * (0.35 + 0.65 * saturate(sun.color.g));
                half3 refracted = lerp(bottom * lerp(_Shallow.rgb * 1.5, _Deep.rgb * 2.0, k), waterCol, saturate(k * 0.75 + 0.2));

                // subsurface glow: light shining through the thin tops of waves
                half sss = saturate(w.x * 0.35 + 0.35 + crest) * saturate(dot(-v, sun.direction) * 0.5 + 0.5);
                refracted += _Shallow.rgb * sss * 0.18 * sun.color;

                // reflection: the surroundings (SSR) over the sky
                half3 r = reflect(-v, n);
                half3 refl = SkyColor(r);
                if (!fromBelow)
                {
                    half hit;
                    half3 ssr = ScreenReflection(i.posWS, r, hit);
                    refl = lerp(refl, ssr * 0.9, hit);
                }
                half fres = 0.06 + 0.94 * pow(1 - saturate(abs(dot(n, v))), 4);
                half3 c = lerp(refracted, refl, saturate(fres * 1.25));

                // sun: a bright core, a wide glare and sparkles on the ripples (HDR so bloom makes the halo)
                half3 hdir = normalize(sun.direction + v);
                half nh = saturate(dot(n, hdir));
                half spec = pow(nh, 1400) * 70 + pow(nh, 160) * 2.2 + pow(nh, 24) * 0.18;
                c += sun.color * spec * sun.shadowAttenuation * (fromBelow ? 0 : 1);

                // foam: thin lapping edge at the beach + the swell breaking as it reaches the sand
                half edge = saturate(1 - depth / 0.35);
                half lap = step(0.5, frac(w.x * 0.4 + w2.x * 0.25 + t * 0.12) * 0.5 + 0.5 - depth * 1.5);
                half breakFoam = crest * saturate(1 - depth / 1.5) * step(0.3, frac(w2.x * 0.7 + p.x * 0.9 + p.y * 0.7));
                half foam = saturate(edge * lap + breakFoam) * _Foam;
                // seen from below there is no shore and no foam: anything floating on top (a lily pad, a boat, ice)
                // only looks "shallow" from the depth buffer and would glow white
                if (fromBelow) { foam = 0; crest = 0; }
                c += half3(0.9, 0.95, 1.0) * crest * 0.3;                    // brighter crest line
                c = lerp(c, half3(0.94, 0.97, 1.0) * (0.6 + 0.4 * sun.shadowAttenuation), foam);

                // the bottom is already blended in from the opaque texture, so the surface itself is solid;
                // only the very edge fades for a soft shoreline
                half alpha = saturate(depth * 6 + foam);
                // the underside: a darker sheet tinted by the day, the sky only through Snell's window straight up
                if (fromBelow)
                {
                    half window = smoothstep(0.55, 0.85, -v.y);
                    c = lerp(_Deep.rgb * (0.25 + 0.6 * saturate(sun.color.g)), c, window);
                    alpha = 1;
                }
                return half4(MixFog(c, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
