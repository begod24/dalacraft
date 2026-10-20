using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public enum TreeKind
    {
        Spruce, Fir, Cedar, Pine, SpruceSnow, FirSnow, PineSnow,
        Birch, BirchAutumn, Aspen, WildApple, Apricot, Hawthorn, Rosehip,
        Turanga, Willow, Tamarisk, Saxaul, DeadTree,
    }

    /// <summary>
    /// Trees made of real blocks, like Minecraft: a log per tree family and leaves per species/region. The world generator
    /// places them deterministically on a jittered grid (so trees cross chunk borders seamlessly), saplings grow into them,
    /// and leaves no longer held by a log decay after the trunk is cut.
    /// </summary>
    public class Trees : MonoBehaviour
    {
        public delegate void Put(int x, int y, int z, BlockType b, bool log);

        // ------------------------------------------------------------------ shapes
        /// <summary>Builds a tree whose trunk starts at (x, y0, z). <paramref name="put"/> decides what may be overwritten.</summary>
        public static void Build(TreeKind kind, int x, int y0, int z, int seed, Put put)
        {
            float R(int i) => TerrainGenerator.Hash(x, i, z, seed + 977);
            int Ri(int i, int min, int max) => min + (int)(R(i) * (max - min + 0.999f));

            void Trunk(BlockType log, int h) { for (int i = 0; i < h; i++) put(x, y0 + i, z, log, true); }
            void Disc(int cy, int r, BlockType leaf, bool trimCorners, int salt)
            {
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        bool corner = Mathf.Abs(dx) == r && Mathf.Abs(dz) == r;
                        if (corner && (trimCorners || TerrainGenerator.Hash(x + dx, cy, z + dz, seed + salt) < 0.5f)) continue;
                        if (dx * dx + dz * dz > r * r + r) continue;
                        put(x + dx, cy, z + dz, leaf, false);
                    }
            }
            void Ball(int cx, int cy, int cz, float r, BlockType leaf, float ragged = 0.35f)
            {
                int ir = Mathf.CeilToInt(r);
                for (int dx = -ir; dx <= ir; dx++)
                    for (int dy = -ir; dy <= ir; dy++)
                        for (int dz = -ir; dz <= ir; dz++)
                        {
                            float d = (dx * dx + dz * dz) / (r * r) + dy * dy / (r * r * 0.7f);
                            if (d + TerrainGenerator.Hash(cx + dx, cy + dy, cz + dz, seed + 31) * ragged > 1f) continue;
                            put(cx + dx, cy + dy, cz + dz, leaf, false);
                        }
            }

            switch (kind)
            {
                case TreeKind.Spruce: case TreeKind.SpruceSnow: case TreeKind.Fir: case TreeKind.FirSnow:
                {
                    // Minecraft spruce: a cone of alternating rings, bare at the bottom
                    bool fir = kind == TreeKind.Fir || kind == TreeKind.FirSnow;
                    var leaf = kind == TreeKind.SpruceSnow || kind == TreeKind.FirSnow ? BlockType.LeavesSnow : BlockType.LeavesNeedle;
                    int h = Ri(1, fir ? 8 : 7, fir ? 11 : 10);
                    int bare = Ri(2, 1, 2);
                    Trunk(BlockType.LogSpruce, h - 1);
                    int maxR = fir ? 2 : 3;
                    int r = 0;
                    for (int y = y0 + h; y >= y0 + bare; y--)
                    {
                        int fromTop = y0 + h - y;
                        r = fromTop == 0 ? 0 : (fromTop % 2 == 1 ? Mathf.Min(maxR, 1 + fromTop / 3) : Mathf.Max(1, Mathf.Min(maxR, fromTop / 3)));
                        if (r == 0) put(x, y, z, leaf, false);
                        else Disc(y, r, leaf, r > 1, fromTop);
                    }
                    break;
                }
                case TreeKind.Cedar:
                {
                    // Siberian cedar: tall, dense tiers with a broad rounded crown
                    int h = Ri(1, 10, 13);
                    Trunk(BlockType.LogSpruce, h);
                    for (int y = y0 + 3; y <= y0 + h + 1; y++)
                    {
                        int fromTop = y0 + h + 1 - y;
                        int r = fromTop < 2 ? 1 : (fromTop % 3 == 0 ? 2 : 3);
                        if (y > y0 + h - 1) r = 1;
                        Disc(y, Mathf.Min(r, 1 + fromTop), BlockType.LeavesNeedle, false, 40 + y);
                    }
                    put(x, y0 + h + 2, z, BlockType.LeavesNeedle, false);
                    break;
                }
                case TreeKind.Pine: case TreeKind.PineSnow:
                {
                    // pine: tall bare trunk, a flat umbrella crown
                    var leaf = kind == TreeKind.PineSnow ? BlockType.LeavesSnow : BlockType.LeavesNeedle;
                    int h = Ri(1, 9, 13);
                    Trunk(BlockType.LogPine, h);
                    int top = y0 + h;
                    Disc(top - 2, 2, leaf, true, 1); Disc(top - 1, 3, leaf, false, 2); Disc(top, 2, leaf, false, 3); Disc(top + 1, 1, leaf, true, 4);
                    // a side branch with its own tuft
                    int f = Ri(5, 0, 3);
                    var d = Meta.Dir[f];
                    put(x + d.x, top - 4, z + d.z, BlockType.LogPine, true);
                    put(x + d.x * 2, top - 4, z + d.z * 2, leaf, false);
                    Disc(top - 3, 1, leaf, true, 6);
                    break;
                }
                case TreeKind.Birch: case TreeKind.BirchAutumn: case TreeKind.Aspen:
                {
                    var log = kind == TreeKind.Aspen ? BlockType.Wood : BlockType.Birch;
                    var leaf = kind == TreeKind.BirchAutumn ? BlockType.LeavesAutumn : kind == TreeKind.Aspen ? BlockType.LeavesBroad : BlockType.LeavesBirch;
                    int h = Ri(1, 5, kind == TreeKind.Aspen ? 9 : 8);
                    Trunk(log, h);
                    int top = y0 + h;
                    Disc(top - 3, 2, leaf, false, 1); Disc(top - 2, 2, leaf, false, 2);
                    Disc(top - 1, 1, leaf, false, 3); Disc(top, 1, leaf, true, 4);
                    break;
                }
                case TreeKind.WildApple: case TreeKind.Apricot: case TreeKind.Hawthorn:
                {
                    // Alatau fruit trees: short trunk that forks, a round crown
                    var leaf = kind == TreeKind.WildApple ? BlockType.LeavesApple : kind == TreeKind.Apricot ? BlockType.LeavesBlossom : BlockType.LeavesBroad;
                    int h = kind == TreeKind.Hawthorn ? Ri(1, 2, 3) : Ri(1, 3, 5);
                    Trunk(BlockType.Wood, h);
                    float r = kind == TreeKind.Hawthorn ? 1.8f : 2.6f;
                    Ball(x, y0 + h + 1, z, r, leaf);
                    if (kind != TreeKind.Hawthorn)
                    {
                        var d = Meta.Dir[Ri(2, 0, 3)];
                        put(x + d.x, y0 + h, z + d.z, BlockType.Wood, true);
                        Ball(x + d.x * 2, y0 + h + 1, z + d.z * 2, 1.7f, leaf);
                    }
                    put(x, y0 + h, z, BlockType.Wood, true);
                    break;
                }
                case TreeKind.Rosehip:
                    Ball(x, y0, z, 1.3f, BlockType.LeavesApple, 0.5f);
                    break;
                case TreeKind.Turanga: case TreeKind.Willow:
                {
                    // tugai poplar (turanga) / willow: thick trunk, wide uneven crown; willow strands hang down
                    var leaf = kind == TreeKind.Willow ? BlockType.LeavesBroad : BlockType.LeavesTugai;
                    int h = Ri(1, 5, 8);
                    Trunk(BlockType.LogTuranga, h);
                    int top = y0 + h;
                    Ball(x, top, z, 3f, leaf, 0.6f);
                    for (int i = 0; i < 3; i++)
                    {
                        var d = Meta.Dir[(Ri(2, 0, 3) + i) % 4];
                        int bx = x + d.x * 2, bz = z + d.z * 2;
                        put(x + d.x, top - 2 + i % 2, z + d.z, BlockType.LogTuranga, true);
                        Ball(bx, top - 1 + i % 2, bz, 2f, leaf, 0.6f);
                    }
                    if (kind == TreeKind.Willow)
                        for (int dx = -3; dx <= 3; dx++)
                            for (int dz = -3; dz <= 3; dz++)
                            {
                                if (Mathf.Abs(dx) < 3 && Mathf.Abs(dz) < 3) continue;
                                if (TerrainGenerator.Hash(x + dx, 5, z + dz, seed) > 0.45f) continue;
                                int len = 1 + (int)(TerrainGenerator.Hash(x + dx, 6, z + dz, seed) * 3);
                                for (int k = 0; k < len; k++) put(x + dx, top - 1 - k, z + dz, leaf, false);
                            }
                    break;
                }
                case TreeKind.Tamarisk:
                {
                    // tamarisk (жыңғыл): a pink feathery shrub on short stems
                    Trunk(BlockType.LogTuranga, 1);
                    Ball(x, y0 + 2, z, 1.8f, BlockType.LeavesBlossom, 0.6f);
                    break;
                }
                case TreeKind.Saxaul: case TreeKind.DeadTree:
                {
                    // saxaul: low gnarled trunk with crooked branches and sparse tufts; a dead tree has no tufts
                    bool dead = kind == TreeKind.DeadTree;
                    int h = dead ? Ri(1, 3, 5) : Ri(1, 2, 3);
                    Trunk(BlockType.LogSaxaul, h);
                    for (int i = 0; i < (dead ? 3 : 4); i++)
                    {
                        var d = Meta.Dir[(Ri(3, 0, 3) + i) % 4];
                        int bx = x, by = y0 + h - 1 - (i % 2), bz = z;
                        int len = Ri(4 + i, 2, 3);
                        for (int k = 0; k < len; k++)
                        {
                            bx += d.x; bz += d.z; if (k % 2 == 1) by++;
                            put(bx, by, bz, BlockType.LogSaxaul, true);
                        }
                        if (!dead) Ball(bx, by + 1, bz, 1.4f, BlockType.LeavesSaxaul, 0.7f);
                    }
                    if (!dead) Ball(x, y0 + h + 1, z, 1.5f, BlockType.LeavesSaxaul, 0.7f);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ world generation
        const int Margin = 4;

        /// <summary>Which tree (if any) grows on a column, from biome, ground and a jittered grid — identical for every chunk asking.</summary>
        public static bool TreeAt(TerrainGenerator t, int seed, int wx, int wz, out TreeKind kind, out int ground)
        {
            kind = default; ground = 0;
            // one candidate per grid cell; density per biome below
            const int cell = 4;
            int gx = Mathf.FloorToInt(wx / (float)cell), gz = Mathf.FloorToInt(wz / (float)cell);
            int cx = gx * cell + (int)(TerrainGenerator.Hash(gx, 401, gz, seed) * cell);
            int cz = gz * cell + (int)(TerrainGenerator.Hash(gx, 402, gz, seed) * cell);
            if (cx != wx || cz != wz) return false;
            ground = t.Height(wx, wz, out _);
            if (ground <= TerrainGenerator.SeaLevel) return false;
            var biome = t.GetBiome(wx, wz, ground);
            var top = t.BlockAt(wx, ground, wz, ground, biome);
            if (top != BlockType.Grass && top != BlockType.DryGrass && top != BlockType.Snow && top != BlockType.Sand && top != BlockType.MossStone && top != BlockType.RedSand) return false;
            for (int f = 0; f < 4; f++)
                if (Mathf.Abs(t.Height(wx + Meta.Dir[f].x, wz + Meta.Dir[f].z, out _) - ground) > 1) return false;
            if (t.InVillage(wx, wz, 50f)) return false;
            float r1 = TerrainGenerator.Hash(gx, 403, gz, seed), r2 = TerrainGenerator.Hash(gx, 404, gz, seed);
            float forest = t.Forest(wx, wz);
            switch (biome)
            {
                case Biome.Taiga:
                    if (r1 > 0.85f) return false;
                    kind = r2 < 0.35f ? TreeKind.Spruce : r2 < 0.6f ? TreeKind.Fir : r2 < 0.82f ? TreeKind.Cedar : TreeKind.Pine; return true;
                case Biome.Snowy:
                    if (ground > 92 || r1 > 0.3f) return false;
                    kind = r2 < 0.5f ? TreeKind.SpruceSnow : r2 < 0.8f ? TreeKind.FirSnow : TreeKind.PineSnow; return true;
                case Biome.BirchGrove:
                    if (r1 > 0.7f) return false;
                    kind = r2 < 0.6f ? TreeKind.Birch : r2 < 0.75f ? TreeKind.BirchAutumn : r2 < 0.92f ? TreeKind.Aspen : TreeKind.Rosehip; return true;
                case Biome.Tugai:
                    if (r1 > 0.55f || top == BlockType.Sand && r2 < 0.5f) return false;
                    kind = r2 < 0.45f ? TreeKind.Turanga : r2 < 0.75f ? TreeKind.Willow : TreeKind.Tamarisk; return true;
                case Biome.AppleForest:
                    if (r1 > 0.65f) return false;
                    kind = r2 < 0.5f ? TreeKind.WildApple : r2 < 0.72f ? TreeKind.Apricot : r2 < 0.9f ? TreeKind.Hawthorn : TreeKind.Aspen; return true;
                case Biome.Altai:
                    if (r1 > 0.12f) return false;
                    kind = r2 < 0.5f ? TreeKind.Pine : TreeKind.Birch; return true;
                case Biome.BetpakDala:
                    if (r1 > 0.09f || (top != BlockType.Sand && top != BlockType.RedSand)) return false;
                    kind = r2 < 0.85f ? TreeKind.Saxaul : TreeKind.DeadTree; return true;
                case Biome.Steppe:
                    // lone birches / rosehip bushes at the edge of groves
                    if (forest > 0.55f && r1 < 0.12f) { kind = r2 < 0.6f ? TreeKind.Birch : TreeKind.Rosehip; return true; }
                    return false;
            }
            return false;
        }

        /// <summary>Writes every tree overlapping the chunk into it (called by TerrainGenerator.Fill).</summary>
        public static void Populate(TerrainGenerator t, Chunk c, int seed)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            void PutInChunk(int x, int y, int z, BlockType b, bool log)
            {
                int lx = x - ox, lz = z - oz;
                if (lx < 0 || lx >= Chunk.Size || lz < 0 || lz >= Chunk.Size || y <= 0 || y >= Chunk.Height) return;
                var cur = c.Blocks[lx, y, lz];
                if (cur == BlockType.Air || (log && BlockInfo.IsLeaves(cur))) c.Blocks[lx, y, lz] = b;
            }
            for (int wx = ox - Margin; wx < ox + Chunk.Size + Margin; wx++)
                for (int wz = oz - Margin; wz < oz + Chunk.Size + Margin; wz++)
                    if (TreeAt(t, seed, wx, wz, out var kind, out int ground))
                        Build(kind, wx, ground + 1, wz, seed, PutInChunk);
        }

        // ------------------------------------------------------------------ saplings & leaf decay (runtime)
        World world;
        /// <summary>Leaf blocks placed by the player (exempt from decay, like Minecraft's persistent leaves).</summary>
        public static readonly HashSet<Vector3Int> PlayerPlaced = new();
        readonly HashSet<Vector3Int> saplings = new();
        readonly List<(Vector3Int p, float at)> decay = new();
        readonly HashSet<Vector3Int> queued = new();
        readonly System.Random rng = new();
        float tick;

        public static Trees Attach(World w)
        {
            var t = w.gameObject.GetComponent<Trees>() ?? w.gameObject.AddComponent<Trees>();
            t.world = w;
            w.Changed -= t.OnChanged;
            w.Changed += t.OnChanged;
            return t;
        }

        void OnChanged(Vector3Int p, BlockType b)
        {
            if (BlockInfo.IsSapling(b)) { saplings.Add(p); return; }
            saplings.Remove(p);
            if (b != BlockType.Air) return;
            // a log (or anything) vanished: leaves around it may have lost their tree
            for (int dx = -4; dx <= 4; dx++)
                for (int dy = -4; dy <= 4; dy++)
                    for (int dz = -4; dz <= 4; dz++)
                    {
                        var q = p + new Vector3Int(dx, dy, dz);
                        if (!BlockInfo.IsLeaves(world.GetBlock(q)) || queued.Contains(q)) continue;
                        if (PlayerPlaced.Contains(q)) continue;   // leaves the player built with never decay
                        queued.Add(q);
                        decay.Add((q, Time.time + 0.5f + (float)rng.NextDouble() * 6f));
                    }
        }

        /// <summary>True if a log is reachable from the leaf within 4 steps through leaves (Minecraft's rule).</summary>
        bool Supported(Vector3Int start)
        {
            var seen = new HashSet<Vector3Int> { start };
            var frontier = new List<Vector3Int> { start };
            for (int d = 0; d < 4; d++)
            {
                var next = new List<Vector3Int>();
                foreach (var p in frontier)
                    foreach (var n in Neighbours)
                    {
                        var q = p + n;
                        if (!seen.Add(q)) continue;
                        var b = world.GetBlock(q);
                        if (BlockInfo.IsLog(b)) return true;
                        if (BlockInfo.IsLeaves(b)) next.Add(q);
                    }
                frontier = next;
            }
            return false;
        }

        static readonly Vector3Int[] Neighbours = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, new(0, 0, 1), new(0, 0, -1) };

        void Update()
        {
            if (world == null) return;
            for (int i = decay.Count - 1; i >= 0; i--)
            {
                var (p, at) = decay[i];
                if (Time.time < at) continue;
                decay.RemoveAt(i); queued.Remove(p);
                var b = world.GetBlock(p);
                if (!BlockInfo.IsLeaves(b) || Supported(p)) continue;
                world.SetBlock(p.x, p.y, p.z, BlockType.Air);
                if (Session.InGame && !Session.Creative)
                    ItemDrop.SpawnAll(Items.LeafDrops(b, rng, 1f), p + new Vector3(0.5f, 0.3f, 0.5f));
            }
            // saplings: roughly one to three minutes to grow, if there is room above
            tick += Time.deltaTime;
            if (tick < 1f) return;
            tick = 0;
            var grown = new List<Vector3Int>();
            foreach (var s in saplings)
                if (rng.NextDouble() < 1.0 / 100.0 && Grow(world, s, world.GetBlock(s))) grown.Add(s);
            foreach (var g in grown) saplings.Remove(g);
        }

        public static TreeKind KindOf(BlockType sapling) => sapling switch
        {
            BlockType.SaplingBirch => TreeKind.Birch,
            BlockType.SaplingSpruce => TreeKind.Spruce,
            BlockType.SaplingPine => TreeKind.Pine,
            BlockType.SaplingApple => TreeKind.WildApple,
            BlockType.SaplingSaxaul => TreeKind.Saxaul,
            BlockType.SaplingTugai => TreeKind.Turanga,
            BlockType.SaplingBlossom => TreeKind.Apricot,
            _ => TreeKind.Aspen,
        };

        /// <summary>Turns a sapling into a full tree (only where its trunk has room).</summary>
        public static bool Grow(World w, Vector3Int p, BlockType sapling)
        {
            if (!BlockInfo.IsSapling(sapling)) return false;
            for (int i = 1; i <= 4; i++) if (BlockInfo.IsSolid(w.GetBlock(p + Vector3Int.up * i))) return false;
            w.SetBlock(p.x, p.y, p.z, BlockType.Air);
            int seed = Random.Range(0, 1 << 20);
            Build(KindOf(sapling), p.x, p.y, p.z, seed, (x, y, z, b, log) =>
            {
                var cur = w.GetBlock(x, y, z);
                if (cur == BlockType.Air || (log && BlockInfo.IsLeaves(cur))) w.SetBlock(x, y, z, b);
            });
            Vfx.Puff(p + new Vector3(0.5f, 1f, 0.5f), new Color(0.6f, 1f, 0.5f), 16, 0.2f, 1.5f, Vfx.Glow);
            return true;
        }
    }
}
