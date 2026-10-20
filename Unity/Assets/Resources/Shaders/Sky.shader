Shader "Dala/Sky"
{
    // Procedural skybox: zenith/horizon gradient, HDR sun disk + halo, moon, stars, and soft fbm clouds
    // lit from the sun (dark bellies, silver lining). All parameters are globals set by WorldVisuals.
    Properties { }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SunDir;          // xyz: direction towards the sun
            half4 _SkyZenith, _SkyHorizon, _SunColor, _CloudLit, _CloudShade;
            float _CloudCover;       // 0..1
            float _Night;            // 0..1
            float _CloudTime;

            struct Attributes { float4 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.dir = i.pos.xyz;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 6; k++) { s += vnoise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sd = normalize(_SunDir.xyz);
                float up = d.y;

                // base gradient (below the horizon fades to a hazy ground colour)
                half3 sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, pow(saturate(up), 0.45));
                if (up < 0) sky = lerp(_SkyHorizon.rgb, _SkyHorizon.rgb * 0.55, saturate(-up * 4));

                float sdot = dot(d, sd);
                // horizon glow around the sun (sunrise / sunset)
                sky += _SunColor.rgb * pow(saturate(sdot), 6) * 0.35 * saturate(1 - abs(up) * 2);

                // stars
                if (_Night > 0.01 && up > 0)
                {
                    float2 sp = d.xz / (d.y + 0.3) * 160;
                    float st = step(0.997, hash21(floor(sp))) * hash21(floor(sp) + 3.1);
                    sky += st * _Night * 1.5;
                }

                // sun disk + halo (HDR for bloom), moon opposite
                float disk = smoothstep(0.9994, 0.9997, sdot);
                sky += _SunColor.rgb * (disk * 30 + pow(saturate(sdot), 400) * 3 + pow(saturate(sdot), 40) * 0.4) * saturate(sd.y * 8 + 0.6);
                float mdot = dot(d, -sd);
                sky += half3(0.85, 0.9, 1.0) * smoothstep(0.9990, 0.9994, mdot) * 2.5 * _Night;

                // clouds on a curved layer
                if (up > 0.0)
                {
                    float2 cuv = d.xz / (up + 0.12) * 1.2 + float2(_CloudTime * 0.012, _CloudTime * 0.006);
                    float base = fbm(cuv);
                    float cover = lerp(0.62, 0.28, _CloudCover);
                    float dens = saturate((base - cover) / 0.22);
                    // light march towards the sun: thicker = darker belly
                    float toSun = fbm(cuv + sd.xz * 0.08);
                    float lit = saturate(1.2 - (toSun - cover) * 3.0);
                    half3 cc = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                    // silver lining when looking towards the sun through thin edges
                    cc += _SunColor.rgb * pow(saturate(sdot), 8) * (1 - dens) * 2.2;
                    dens *= smoothstep(0.0, 0.18, up);
                    sky = lerp(sky, cc, dens * 0.95);
                }
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
