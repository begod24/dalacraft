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

        static readonly System.Collections.Generic.Dictionary<int, Material> blockMats = new();

        /// <summary>Material showing one block tile of its atlas page (for break fragments).</summary>
        static Material BlockTile(int tile)
        {
            if (blockMats.TryGetValue(tile, out var m) && m != null) return m;
            m = Material(BlockAtlas.PageOf(tile), false, 1f);
            int slot = tile % BlockInfo.TilesPerPage;
            const float k = 1f / BlockInfo.AtlasTiles;
            m.SetTextureScale("_BaseMap", new Vector2(k, k));
            m.SetTextureOffset("_BaseMap", new Vector2((slot % BlockInfo.AtlasTiles) * k, (slot / BlockInfo.AtlasTiles) * k));
            blockMats[tile] = m;
            return m;
        }

        /// <summary>Minecraft-like burst of small pieces of the block's own texture when it breaks.</summary>
        public static void BlockBreak(Vector3Int cell, BlockType b)
        {
            if (!Application.isPlaying || b == BlockType.Air) return;
            int tile = BlockInfo.IsEntity(b) ? BlockInfo.TilePlanks : BlockInfo.Tile(b, 2);
            if (b == BlockType.Furnace || b == BlockType.Kazan || b == BlockType.Tandyr) tile = BlockInfo.TileCobble;
            if (b == BlockType.Torch || b == BlockType.Campfire) tile = BlockInfo.TileWood;
            var ps = Create("BreakBits", null, BlockTile(tile), 40);
            ps.transform.position = cell + new Vector3(0.5f, 0.5f, 0.5f);
            var main = ps.main;
            main.loop = false; main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.gravityModifier = 1.6f;
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = Vector3.one * 0.8f;
            // each fragment shows a random 4x4-pixel piece of the 16px tile
            var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 4; ts.numTilesY = 4;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f); ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.dampen = 0.5f; col.bounce = 0.2f; col.radiusScale = 0.5f;
            ps.Play();
        }

        /// <summary>A puff of tinted particles (hits, deaths, spirits).</summary>
        public static void Puff(Vector3 at, Color c, int count = 14, float size = 0.25f, float speed = 1.6f, int tile = Smoke)
        {
            if (!Application.isPlaying) return;
            var ps = Create("Puff", null, Tile(tile, false), count + 4);
            ps.transform.position = at;
            var main = ps.main;
            main.loop = false; main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = c;
            main.gravityModifier = -0.15f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(c, 0.05f, 0.5f);
            ps.Play();
        }
    }
}
