using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Minecraft light levels: every cell has a sky light and a block light 0–15. Sky light falls straight down open
    /// columns at 15 and spreads sideways under overhangs and into caves losing 1 per step; block light spreads from
    /// torches, fires, furnaces, glowing mushrooms and crystals the same way. Leaves and water dim it one more level,
    /// opaque blocks stop it. A chunk is lit over itself plus a 15-cell margin (light never travels farther), so the
    /// result is exact at its borders; the mesher bakes it into vertex colours (G = sky, B = block).
    /// </summary>
    public static class Lighting
    {
        public const int Max = 15, Margin = 15;
        const int R = Chunk.Size + 2 * Margin, H = Chunk.Height, N = R * R * H;
        const byte Opaque = 255;

        static readonly byte[] atten = new byte[N], sky = new byte[N], blk = new byte[N];
        static readonly int[] queue = new int[N];
        static readonly int[] lowOpen = new int[R * R];
        static int head, tail, count;

        static int Idx(int rx, int y, int rz) => (y * R + rz) * R + rx;

        /// <summary>Light a block gives off by itself (entities: torch, fires, ovens — see <see cref="BlockEntity.LightLevel"/>).</summary>
        public static int Emission(BlockType b) => b switch
        {
            BlockType.GlowShroom => 10,
            BlockType.CrystalTurquoise or BlockType.CrystalAmethyst or BlockType.CrystalRuby => 8,
            _ => 0,
        };

        static byte Attenuation(BlockType b)
        {
            if (BlockInfo.IsOpaque(b)) return Opaque;
            if (BlockInfo.IsLeaves(b) || b == BlockType.Water || b == BlockType.Ice) return 1;
            return 0;
        }

        // ---------------------------------------------------------------- queries on the last computed region
        /// <summary>Sky light of a cell given in the lit chunk's local coordinates (may lie up to 15 cells outside it).</summary>
        public static int Sky(int lx, int y, int lz)
        {
            if (y >= H) return Max;
            if (y < 0) return 0;
            return sky[Idx(lx + Margin, y, lz + Margin)];
        }

        public static int Block(int lx, int y, int lz)
        {
            if (y < 0 || y >= H) return 0;
            return blk[Idx(lx + Margin, y, lz + Margin)];
        }

        // ---------------------------------------------------------------- compute
        /// <summary>
        /// Lights <paramref name="c"/> (stores the packed result in <see cref="Chunk.Light"/>) and leaves the whole
        /// region in the static buffers for the mesher. After a block edit, neighbours whose light changed are re-meshed.
        /// </summary>
        public static void Compute(World w, Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size - Margin, oz = c.Coord.y * Chunk.Size - Margin;
            System.Array.Clear(blk, 0, N);
            head = tail = count = 0;

            // 1) attenuation from the 3x3 chunks around (missing ones count as open air)
            int top = 0;
            for (int dcx = -1; dcx <= 1; dcx++)
                for (int dcz = -1; dcz <= 1; dcz++)
                {
                    var nc = w.ChunkAt(c.Coord + new Vector2Int(dcx, dcz));
                    int cx0 = (c.Coord.x + dcx) * Chunk.Size, cz0 = (c.Coord.y + dcz) * Chunk.Size;
                    int rx0 = Mathf.Max(0, cx0 - ox), rx1 = Mathf.Min(R, cx0 + Chunk.Size - ox);
                    int rz0 = Mathf.Max(0, cz0 - oz), rz1 = Mathf.Min(R, cz0 + Chunk.Size - oz);
                    for (int rz = rz0; rz < rz1; rz++)
                        for (int rx = rx0; rx < rx1; rx++)
                        {
                            int lx = ox + rx - cx0, lz = oz + rz - cz0;
                            for (int y = 0; y < H; y++)
                            {
                                int i = Idx(rx, y, rz);
                                if (nc == null) { atten[i] = 0; continue; }
                                var b = nc.Blocks[lx, y, lz];
                                if (b == BlockType.Air) { atten[i] = 0; continue; }
                                if (y > top) top = y;
                                atten[i] = Attenuation(b);
                                int e = Emission(b);
                                if (BlockInfo.IsEntity(b) && BlockEntities.All.TryGetValue(new Vector3Int(ox + rx, y, oz + rz), out var be)) e = be.LightLevel;
                                if (e > 0) { blk[i] = (byte)e; Push(i); }
                            }
                        }
                }

            // 2) block light flood fill
            Flood(blk);

            // 3) sky light: straight down every column, then spread sideways
            for (int rz = 0; rz < R; rz++)
                for (int rx = 0; rx < R; rx++)
                {
                    int level = Max, low = H;
                    for (int y = H - 1; y >= 0; y--)
                    {
                        int i = Idx(rx, y, rz);
                        byte a = atten[i];
                        if (a == Opaque) level = 0;
                        else if (a > 0) level = Mathf.Max(0, level - a);
                        sky[i] = (byte)level;
                        if (level == Max) low = y;
                    }
                    lowOpen[rz * R + rx] = low;
                }
            for (int rz = 0; rz < R; rz++)
                for (int rx = 0; rx < R; rx++)
                {
                    // only cells beside a darker neighbour column can spread anything
                    int hi = 0;
                    if (rx > 0) hi = Mathf.Max(hi, lowOpen[rz * R + rx - 1]);
                    if (rx < R - 1) hi = Mathf.Max(hi, lowOpen[rz * R + rx + 1]);
                    if (rz > 0) hi = Mathf.Max(hi, lowOpen[(rz - 1) * R + rx]);
                    if (rz < R - 1) hi = Mathf.Max(hi, lowOpen[(rz + 1) * R + rx]);
                    int y0 = lowOpen[rz * R + rx];
                    // cells dimmed by leaves / water under the open part spread too
                    while (y0 > 0 && sky[Idx(rx, y0 - 1, rz)] > 1) y0--;
                    for (int y = y0; y < Mathf.Min(hi, H); y++)
                    {
                        int i = Idx(rx, y, rz);
                        if (sky[i] > 1) Push(i);
                    }
                }
            Flood(sky);

            // 4) store the chunk's own cells; after an edit, re-mesh neighbours whose light changed
            var light = c.Light;
            bool edit = c.LightEdit;
            c.LightEdit = false;
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                    for (int y = 0; y < H; y++)
                    {
                        int i = Idx(x + Margin, y, z + Margin);
                        light[x, y, z] = (byte)((sky[i] << 4) | blk[i]);
                    }
            if (!edit) return;
            for (int dcx = -1; dcx <= 1; dcx++)
                for (int dcz = -1; dcz <= 1; dcz++)
                {
                    if (dcx == 0 && dcz == 0) continue;
                    var nc = w.ChunkAt(c.Coord + new Vector2Int(dcx, dcz));
                    if (nc == null || !nc.MeshBuilt || nc.Dirty) continue;
                    if (Differs(nc, ox, oz, top)) nc.Dirty = true;
                }
        }

        static bool Differs(Chunk nc, int ox, int oz, int top)
        {
            int cx0 = nc.Coord.x * Chunk.Size, cz0 = nc.Coord.y * Chunk.Size;
            int rx0 = Mathf.Max(0, cx0 - ox), rx1 = Mathf.Min(R, cx0 + Chunk.Size - ox);
            int rz0 = Mathf.Max(0, cz0 - oz), rz1 = Mathf.Min(R, cz0 + Chunk.Size - oz);
            int yMax = Mathf.Min(H, top + Max + 1);
            for (int rz = rz0; rz < rz1; rz++)
                for (int rx = rx0; rx < rx1; rx++)
                {
                    int lx = ox + rx - cx0, lz = oz + rz - cz0;
                    for (int y = 0; y < yMax; y++)
                    {
                        int i = Idx(rx, y, rz);
                        if (nc.Light[lx, y, lz] != (byte)((sky[i] << 4) | blk[i])) return true;
                    }
                }
            return false;
        }

        static void Push(int i)
        {
            queue[tail] = i; tail = (tail + 1) % N; count++;
        }

        static void Flood(byte[] arr)
        {
            while (count > 0)
            {
                int i = queue[head]; head = (head + 1) % N; count--;
                int level = arr[i];
                if (level <= 1) continue;
                int rx = i % R, rz = (i / R) % R, y = i / (R * R);
                if (rx > 0) Spread(arr, i - 1, level);
                if (rx < R - 1) Spread(arr, i + 1, level);
                if (rz > 0) Spread(arr, i - R, level);
                if (rz < R - 1) Spread(arr, i + R, level);
                if (y > 0) Spread(arr, i - R * R, level);
                if (y < H - 1) Spread(arr, i + R * R, level);
            }
        }

        static void Spread(byte[] arr, int j, int level)
        {
            byte a = atten[j];
            if (a == Opaque) return;
            int nl = level - 1 - a;
            if (nl <= arr[j]) return;
            arr[j] = (byte)nl;
            if (count < N) Push(j);
        }

        // ---------------------------------------------------------------- world queries (spawning, models)
        /// <summary>Packed light of a loaded cell: sky in the high nibble, block in the low one.</summary>
        public static void At(World w, Vector3Int p, out int skyLevel, out int blockLevel)
        {
            skyLevel = Max; blockLevel = 0;
            if (p.y >= H) return;
            if (p.y < 0) { skyLevel = 0; return; }
            var c = w.ChunkAt(World.ChunkOf(p.x, p.z));
            if (c == null || !c.MeshBuilt) return;
            byte v = c.Light[World.Mod(p.x), p.y, World.Mod(p.z)];
            skyLevel = v >> 4; blockLevel = v & 15;
        }

        // ---------------------------------------------------------------- lights for instanced plants (no per-cell data)
        public const int ShaderLights = 16;
        static readonly Vector4[] points = new Vector4[ShaderLights];
        static readonly List<(float d, Vector4 v)> nearby = new();
        static readonly int PointsId = Shader.PropertyToID("_PointLights"), PointCountId = Shader.PropertyToID("_PointLightCount"),
            HandId = Shader.PropertyToID("_HandLight");

        /// <summary>Nearest burning torches / fires as shader point sources (xyz = centre, w = level) for plants.</summary>
        public static void UpdateShaderLights(Vector3 around)
        {
            nearby.Clear();
            foreach (var e in BlockEntities.All.Values)
            {
                int l = e.LightLevel;
                if (l <= 0) continue;
                var p = new Vector3(e.Pos.x + 0.5f, e.Pos.y + 0.5f, e.Pos.z + 0.5f);
                float d = (p - around).sqrMagnitude;
                if (d < 48f * 48f) nearby.Add((d, new Vector4(p.x, p.y, p.z, l)));
            }
            nearby.Sort((a, b) => a.d.CompareTo(b.d));
            int n = Mathf.Min(ShaderLights, nearby.Count);
            for (int i = 0; i < ShaderLights; i++) points[i] = i < n ? nearby[i].v : Vector4.zero;
            Shader.SetGlobalVectorArray(PointsId, points);
            Shader.SetGlobalInt(PointCountId, n);
        }

        /// <summary>Held torch: a moving light level around the player (like Minecraft's dynamic-lights mods).</summary>
        public static void SetHandLight(Vector3 pos, float level) => Shader.SetGlobalVector(HandId, new Vector4(pos.x, pos.y, pos.z, level));
    }
}
