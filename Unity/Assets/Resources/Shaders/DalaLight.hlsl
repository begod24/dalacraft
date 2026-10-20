// Minecraft light levels for Dala shaders. Chunk meshes carry sky / block light per vertex (0..1 = level 0..15);
// the held torch and (for instanced plants) nearby fires come from globals set by Lighting.cs, the day factor from WorldVisuals.
#ifndef DALA_LIGHT_INCLUDED
#define DALA_LIGHT_INCLUDED

float4 _HandLight;              // xyz = position, w = level 0..15 (0 = no torch in hand)
float4 _PointLights[16];        // xyz = centre, w = level
int _PointLightCount;
half _DayLight;                 // 0 night .. 1 day
half3 _BlockLightColor;         // warm torch colour

// Minecraft's brightness table: level 15 -> 1, 8 -> ~0.21, 0 -> 0
half LightCurve(half l) { return l / (4.0 - 3.0 * l); }

half HandLevel(float3 posWS)
{
    float d = distance(posWS, _HandLight.xyz);
    return saturate((_HandLight.w - d) / 15.0);
}

half PointLevel(float3 posWS)
{
    half l = 0;
    for (int i = 0; i < _PointLightCount; i++)
    {
        float3 d = abs(posWS - _PointLights[i].xyz);
        l = max(l, saturate((_PointLights[i].w - (d.x + d.y + d.z)) / 15.0));   // Manhattan steps, like the flood fill
    }
    return l;
}

// Light reaching a surface (multiply with albedo). ambient/sun are the sky terms already shaded by the caller.
half3 BlockLightTerm(half blockLevel)
{
    // torches matter less in daylight, like Minecraft where light = max(sky, block)
    return _BlockLightColor * LightCurve(blockLevel) * (1.0 - 0.75 * _DayLight);
}

#endif
