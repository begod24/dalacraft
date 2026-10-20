using UnityEngine;
using UnityEngine.Rendering;

namespace Dala
{
    /// <summary>Shared particle helpers for the pixel-art VFX textures painted in Blender (T_VFX, T_FireFlipbook).</summary>
    public static class Vfx
    {
        // Tiles of T_VFX (4x4) as numbered in Blender (row 0 = bottom).
        public const int Smoke = 0, SmokeDark = 1, Ember = 2, Glow = 3, Rain = 4, Snow = 5, LeafGreen = 6, LeafYellow = 7,
            LeafOrange = 8, Dust = 9, Splash = 10, Petal = 11, Fluff = 12, Firefly = 13, Mist = 14, SmokeBlack = 15;

        static Texture2D vfxTex, fireTex;
        public static Texture2D VfxTexture => vfxTex ??= Resources.Load<Texture2D>("Textures/T_VFX");
        public static Texture2D FireTexture => fireTex ??= Resources.Load<Texture2D>("Textures/T_FireFlipbook");

        public static Material Material(Texture tex, bool additive, float boost = 1f)
        {
            var m = new Material(Shader.Find("Dala/Particle")) { name = additive ? "VfxAdd" : "VfxAlpha" };
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Boost", boost);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Creates a stopped particle system with sane defaults; caller configures modules, then Play().</summary>
        public static ParticleSystem Create(string name, Transform parent, Material mat, int maxParticles = 1000)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            return ps;
        }

        static readonly System.Collections.Generic.Dictionary<(int, bool, float), Material> tileMats = new();

        /// <summary>Material showing one tile of the 4x4 T_VFX sheet (Blender numbering, row 0 = bottom) via tiling/offset.</summary>
        public static Material Tile(int tile, bool additive, float boost = 1f)
        {
            if (tileMats.TryGetValue((tile, additive, boost), out var m) && m != null) return m;
            m = Material(VfxTexture, additive, boost);
            m.SetTextureScale("_BaseMap", new Vector2(0.25f, 0.25f));
            m.SetTextureOffset("_BaseMap", new Vector2((tile % 4) * 0.25f, (tile / 4) * 0.25f));
            tileMats[(tile, additive, boost)] = m;
            return m;
        }

        public static ParticleSystem.MinMaxGradient Fade(Color c, float inTime = 0.1f, float outTime = 0.8f, float peak = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inTime), new GradientAlphaKey(peak, outTime), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }
}
