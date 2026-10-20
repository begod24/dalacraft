using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Natural features built from real blocks during generation, the way Minecraft does it (no rock / log / crystal models):
    /// mossy boulders, Charyn red-stone pillars, snowdrifts and ice spikes, fallen logs and stumps, mushrooms, lily pads,
    /// stones on river beds, and in caves glowing crystals, glow mushrooms and dripstone. Deterministic per world position,
    /// so features crossing chunk borders come out whole.
    /// </summary>
    public static class Features
    {
        const int Cell = 8, Margin = 7;

        delegate void Put(int x, int y, int z, BlockType b, byte meta, bool replaceSolid);

        public static void Populate(TerrainGenerator t, Chunk c, int seed)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            void Set(int x, int y, int z, BlockType b, byte meta, bool replaceSolid)
            {
                int lx = x - ox, lz = z - oz;
                if (lx < 0 || lx >= Chunk.Size || lz < 0 || lz >= Chunk.Size || y <= 0 || y >= Chunk.Height) return;
                var cur = c.Blocks[lx, y, lz];
                if (cur != BlockType.Air && !(replaceSolid && cur != BlockType.Water && !BlockInfo.IsLog(cur))) return;
                c.Blocks[lx, y, lz] = b; c.Metas[lx, y, lz] = meta;
            }

            // ---- larger features on a jittered 8x8 grid (may cross chunk borders)
            int g0x = Mathf.FloorToInt((ox - Margin) / (float)Cell), g1x = Mathf.FloorToInt((ox + Chunk.Size + Margin) / (float)Cell);
            int g0z = Mathf.FloorToInt((oz - Margin) / (float)Cell), g1z = Mathf.FloorToInt((oz + Chunk.Size + Margin) / (float)Cell);
            for (int gx = g0x; gx <= g1x; gx++)
                for (int gz = g0z; gz <= g1z; gz++)
                {
                    float r = TerrainGenerator.Hash(gx, 601, gz, seed);
                    if (r > 0.35f) continue;
                    int wx = gx * Cell + (int)(TerrainGenerator.Hash(gx, 602, gz, seed) * Cell);
                    int wz = gz * Cell + (int)(TerrainGenerator.Hash(gx, 603, gz, seed) * Cell);
                    int h = t.Height(wx, wz, out _);
                    if (h <= TerrainGenerator.SeaLevel || t.InVillage(wx, wz, 54f)) continue;
                    var biome = t.GetBiome(wx, wz, h);
                    var ground = t.BlockAt(wx, h, wz, h, biome);
                    float r2 = TerrainGenerator.Hash(gx, 604, gz, seed);
                    Large(biome, ground, wx, h, wz, r, r2, seed + gx * 31 + gz, Set);
                }

            // ---- small things per column, inside this chunk only
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    int wx = ox + x, wz = oz + z, top = c.Top[x, z];
                    var biome = c.Biomes[x, z];
                    float r = TerrainGenerator.Hash(wx, 611, wz, seed);
                    if (top < TerrainGenerator.SeaLevel)
                    {
                        int depth = TerrainGenerator.SeaLevel - top;
                        if (biome == Biome.Snowy) continue;
                        // lily pads on calm water a little away from the bank
                        if (depth >= 1 && depth <= 3 && r < 0.035f && !NearShore(t, wx, wz)) Set(wx, TerrainGenerator.SeaLevel + 1, wz, BlockType.LilyPad, 0, false);
                        // mossy stones on the river / lake bed
                        else if (depth >= 2 && r > 0.994f) c.Blocks[x, top + 1, z] = r > 0.997f ? BlockType.MossStone : BlockType.Cobble;
                        continue;
                    }
                    if (top + 1 >= Chunk.Height || c.Blocks[x, top + 1, z] != BlockType.Air) continue;
                    var ground = c.Blocks[x, top, z];
                    bool forest = biome == Biome.Taiga || biome == Biome.BirchGrove || biome == Biome.AppleForest;
                    if (forest && (ground == BlockType.Grass || ground == BlockType.MossStone) && r < 0.012f)
                        c.Blocks[x, top + 1, z] = TerrainGenerator.Hash(wx, 612, wz, seed) < 0.45f ? BlockType.MushroomRed : BlockType.MushroomBrown;
                }

            Caves(t, c, seed);
        }

        static bool NearShore(TerrainGenerator t, int wx, int wz)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (t.Height(wx + dx, wz + dz, out _) >= TerrainGenerator.SeaLevel) return true;
            return false;
        }

        static void Large(Biome biome, BlockType ground, int x, int h, int z, float r, float r2, int seed, Put put)
        {
            float H(int a, int b, int c) => TerrainGenerator.Hash(a, b, c, seed);
            switch (biome)
            {
                case Biome.Taiga:
                case Biome.BirchGrove:
                    if (r < 0.10f) FallenLog(x, h + 1, z, biome == Biome.Taiga ? BlockType.LogSpruce : BlockType.Birch, H, put);
                    else if (r < 0.16f) put(x, h + 1, z, biome == Biome.Taiga ? BlockType.LogSpruce : BlockType.Birch, 0, false);   // stump
                    else if (r < 0.24f && biome == Biome.Taiga) Boulder(x, h, z, 1.6f, BlockType.MossStone, BlockType.Cobble, H, put);
                    break;
                case Biome.Altai:
                case Biome.AppleForest:
                    if (r < 0.12f) Boulder(x, h, z, 1.3f + r2, BlockType.Stone, BlockType.MossStone, H, put);
                    break;
                case Biome.Steppe:
                    if (r < 0.025f) Boulder(x, h, z, 1.2f + r2 * 0.6f, BlockType.Cobble, BlockType.Stone, H, put);
                    break;
                case Biome.Snowy:
                    if (r < 0.12f) Drift(x, h, z, H, put);
                    else if (r < 0.16f) IceSpike(x, h + 1, z, 3 + (int)(r2 * 5), put);
                    else if (r < 0.22f) { Boulder(x, h, z, 1.4f, BlockType.Stone, BlockType.Cobble, H, put); put(x, h + 3, z, BlockType.Snow, 0, false); }
                    break;
                case Biome.BetpakDala:
                    if (ground == BlockType.RedSand && r < 0.16f) Pillar(x, h, z, 4 + (int)(r2 * 7), H, put);
                    else if (r < 0.05f) Boulder(x, h, z, 1.1f, BlockType.Sandstone, BlockType.RedStone, H, put);
                    break;
                case Biome.Tugai:
                    if (r < 0.06f) FallenLog(x, h + 1, z, BlockType.LogTuranga, H, put);
                    break;
            }
        }

        delegate float HashFn(int a, int b, int c);

        static void FallenLog(int x, int y, int z, BlockType log, HashFn H, Put put)
        {
            bool alongX = H(x, 1, z) < 0.5f;
            int len = 3 + (int)(H(x, 2, z) * 3);
            byte axis = (byte)(alongX ? 1 : 2);
            for (int i = 0; i < len; i++)
                put(x + (alongX ? i : 0), y, z + (alongX ? 0 : i), log, axis, false);
            if (H(x, 3, z) < 0.5f) put(x + (alongX ? 1 : 0), y + 1, z + (alongX ? 0 : 1), H(x, 4, z) < 0.5f ? BlockType.MushroomBrown : BlockType.MushroomRed, 0, false);
        }

        static void Boulder(int x, int h, int z, float r, BlockType a, BlockType b, HashFn H, Put put)
        {
            int ir = Mathf.CeilToInt(r);
            for (int dx = -ir; dx <= ir; dx++)
                for (int dy = -1; dy <= ir; dy++)
                    for (int dz = -ir; dz <= ir; dz++)
                    {
                        float d = (dx * dx + dz * dz) / (r * r) + (dy + 0.5f) * (dy + 0.5f) / (r * r * 0.8f);
                        if (d + H(x + dx, h + dy, z + dz) * 0.5f > 1f) continue;
                        put(x + dx, h + dy, z + dz, H(dx, dy, dz) < 0.35f ? b : a, 0, dy <= 0);
                    }
        }

        static void Drift(int x, int h, int z, HashFn H, Put put)
        {
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz) + H(x + dx, 5, z + dz) * 1.2f;
                    if (d > 3f) continue;
                    put(x + dx, h + 1, z + dz, BlockType.Snow, 0, false);
                    if (d < 1.4f) put(x + dx, h + 2, z + dz, BlockType.Snow, 0, false);
                }
        }

        static void IceSpike(int x, int y, int z, int height, Put put)
        {
            for (int i = 0; i < height; i++)
            {
                put(x, y + i, z, BlockType.Ice, 0, false);
                if (i < height / 2)
                    foreach (var d in Meta.Dir) put(x + d.x, y + i, z + d.z, BlockType.Ice, 0, false);
            }
        }

        /// <summary>Charyn canyon pillar: banded red stone, wider at the foot, a sandstone cap.</summary>
        static void Pillar(int x, int h, int z, int height, HashFn H, Put put)
        {
            for (int i = -1; i < height; i++)
            {
                float w = i < height / 3 ? 1.6f : i < height * 2 / 3 ? 1.2f : 0.9f;
                int iw = Mathf.CeilToInt(w);
                for (int dx = -iw; dx <= iw; dx++)
                    for (int dz = -iw; dz <= iw; dz++)
                    {
                        if (dx * dx + dz * dz > w * w + H(x + dx, i, z + dz) * 1.5f) continue;
                        var b = i == height - 1 ? BlockType.Sandstone : ((i + (int)(H(x, i, z) * 2)) % 4 == 0 ? BlockType.Sandstone : BlockType.RedStone);
                        put(x + dx, h + 1 + i, z + dz, b, 0, i < 0);
                    }
            }
        }

        // ---------------------------------------------------------------- caves
        static void Caves(TerrainGenerator t, Chunk c, int seed)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    int top = c.Top[x, z];
                    for (int y = 4; y < top - 4; y++)
                    {
                        if (c.Blocks[x, y, z] != BlockType.Air) continue;
                        int wx = ox + x, wz = oz + z;
                        float r = TerrainGenerator.Hash(wx, y, wz, seed + 11);
                        bool floor = BlockInfo.IsOpaque(c.Blocks[x, y - 1, z]);
                        bool ceil = BlockInfo.IsOpaque(c.Blocks[x, y + 1, z]);
                        if (floor)
                        {
                            if (r < 0.014f) Stalagmite(c, x, y, z, r);
                            else if (r < 0.022f && y < 34)
                                c.Blocks[x, y, z] = ((int)(r * 100000) % 3) switch { 0 => BlockType.CrystalTurquoise, 1 => BlockType.CrystalAmethyst, _ => BlockType.CrystalRuby };
                            else if (r < 0.030f) c.Blocks[x, y, z] = BlockType.GlowShroom;
                        }
                        else if (ceil && r > 0.975f) Stalactite(c, x, y, z, r);
                    }
                }
        }

        static void Stalactite(Chunk c, int x, int y, int z, float r)
        {
            int len = 1 + (int)((r - 0.975f) / 0.025f * 3f);                // 1..3 blocks hanging down
            for (int i = 0; i < len; i++)
            {
                int yy = y - i;
                if (yy < 1 || c.Blocks[x, yy, z] != BlockType.Air) break;
                byte part = (byte)(i == len - 1 ? 0 : i == 0 && len > 2 ? 2 : 1);
                c.Blocks[x, yy, z] = BlockType.Dripstone; c.Metas[x, yy, z] = (byte)(part | Meta.Upper);
            }
        }

        static void Stalagmite(Chunk c, int x, int y, int z, float r)
        {
            int len = 1 + (int)(r / 0.014f * 2.5f);
            for (int i = 0; i < len; i++)
            {
                int yy = y + i;
                if (yy >= Chunk.Height || c.Blocks[x, yy, z] != BlockType.Air) break;
                c.Blocks[x, yy, z] = BlockType.Dripstone; c.Metas[x, yy, z] = (byte)(i == len - 1 ? 0 : i == 0 && len > 2 ? 2 : 1);
            }
        }
    }
}
