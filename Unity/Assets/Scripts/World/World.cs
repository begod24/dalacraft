using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dala
{
    public class World : MonoBehaviour
    {
        public static World Instance { get; private set; }

        public int Seed = 20240;
        public int ViewRadius = 6;
        public int FoliageRadius = 4;
        public int DataPerFrame = 2;
        public int MeshPerFrame = 2;

        public TerrainGenerator Terrain { get; private set; }
        public Material VoxelMaterial { get; private set; }
        public Material WaterMaterial { get; private set; }
        public FoliageLibrary Foliage { get; private set; }
        public Transform Target;

        readonly Dictionary<Vector2Int, Chunk> chunks = new();
        /// <summary>Player edits (block + meta), re-applied when a chunk is regenerated after unloading.</summary>
        public readonly Dictionary<Vector3Int, (BlockType type, byte meta)> Edits = new();
        readonly List<Vector2Int> wanted = new();
        Vector2Int lastCenter = new(int.MinValue, int.MinValue);

        void Awake()
        {
            Instance = this;
            Terrain = new TerrainGenerator(Seed) { Villages = Session.Current == null || Session.Current.Villages };
            VoxelMaterial = BlockAtlas.CreateMaterial("VoxelRuntime");
            WaterMaterial = new Material(Shader.Find("Dala/Water")) { name = "WaterRuntime" };
            Foliage = new FoliageLibrary();
            Trees.Attach(this);
            Trees.PlayerPlaced.Clear();
            BlockEntities.All.Clear();
            Village.ClearCache();
        }

        public static Vector2Int ChunkOf(int wx, int wz) =>
            new(Mathf.FloorToInt(wx / (float)Chunk.Size), Mathf.FloorToInt(wz / (float)Chunk.Size));

        public BlockType GetBlock(int wx, int y, int wz)
        {
            if (y < 0) return BlockType.Stone;
            if (y >= Chunk.Height) return BlockType.Air;
            if (!chunks.TryGetValue(ChunkOf(wx, wz), out var c)) return BlockType.Air;
            return c.Blocks[Mod(wx), y, Mod(wz)];
        }

        public byte GetMeta(int wx, int y, int wz)
        {
            if (y < 0 || y >= Chunk.Height) return 0;
            return chunks.TryGetValue(ChunkOf(wx, wz), out var c) ? c.Metas[Mod(wx), y, Mod(wz)] : (byte)0;
        }
        public byte GetMeta(Vector3Int p) => GetMeta(p.x, p.y, p.z);
        public BlockType GetBlock(Vector3Int p) => GetBlock(p.x, p.y, p.z);

        /// <summary>Changes only the state bits of a block (door / gate / trapdoor open) and remeshes.</summary>
        public void SetMeta(Vector3Int p, byte meta)
        {
            if (p.y < 0 || p.y >= Chunk.Height || !chunks.TryGetValue(ChunkOf(p.x, p.z), out var c)) return;
            int lx = Mod(p.x), lz = Mod(p.z);
            c.Metas[lx, p.y, lz] = meta;
            Edits[p] = (c.Blocks[lx, p.y, lz], meta);
            MarkAround(c, ChunkOf(p.x, p.z), lx, lz);
        }

        /// <summary>Sets a block. Returns contents of a removed chest/furnace (if any) via <paramref name="spilled"/>.</summary>
        public bool SetBlock(int wx, int y, int wz, BlockType b, byte meta = 0) => SetBlock(wx, y, wz, b, out _, meta);

        public bool SetBlock(int wx, int y, int wz, BlockType b, out List<Stack> spilled, byte meta = 0)
        {
            spilled = null;
            if (y < 0 || y >= Chunk.Height) return false;
            var cc = ChunkOf(wx, wz);
            if (!chunks.TryGetValue(cc, out var c)) return false;
            int lx = Mod(wx), lz = Mod(wz);
            var pos = new Vector3Int(wx, y, wz);
            if (BlockInfo.IsEntity(c.Blocks[lx, y, lz])) spilled = BlockEntities.Remove(pos);
            c.Blocks[lx, y, lz] = b;
            c.Metas[lx, y, lz] = meta;
            Edits[pos] = (b, meta);
            if (BlockInfo.IsEntity(b))
            {
                var e = BlockEntities.Create(pos, b, meta);
                if (c.Go != null) BlockEntities.Spawn(e, c.Go.transform);
            }
            c.Half[lx, y, lz] = false;
            c.RemoveFoliageAt(lx, y, lz);
            c.LightEdit = true;
            MarkAround(c, cc, lx, lz);
            if (b == BlockType.Air) Flood(wx, y, wz);
            Changed?.Invoke(pos, b);
            return true;
        }

        /// <summary>Raised after every block change (leaf decay, sapling tracking, entity reactions).</summary>
        public event System.Action<Vector3Int, BlockType> Changed;

        void MarkAround(Chunk c, Vector2Int cc, int lx, int lz)
        {
            c.Dirty = true;
            if (lx == 0) MarkDirty(cc + Vector2Int.left);
            if (lx == Chunk.Size - 1) MarkDirty(cc + Vector2Int.right);
            if (lz == 0) MarkDirty(cc + Vector2Int.down);
            if (lz == Chunk.Size - 1) MarkDirty(cc + Vector2Int.up);
        }

        /// <summary>
        /// Breaks a block like a player would: the other half of a door or bed goes with it, and plants, torches, ladders
        /// and doors that stood on / hung from it pop off as items. Returns everything that should drop (survival).
        /// </summary>
        public List<Stack> BreakBlock(Vector3Int p, ItemDef tool, System.Random rng)
        {
            var drops = new List<Stack>();
            var b = GetBlock(p); byte m = GetMeta(p);
            if (b == BlockType.Air) return drops;
            drops.AddRange(Items.Drops(b, m, tool, rng));
            SetBlock(p.x, p.y, p.z, BlockType.Air, out var spilled);
            if (spilled != null) drops.AddRange(spilled);
            var other = LinkedHalf(p, b, m);
            if (other.HasValue && GetBlock(other.Value) == b)
            {
                if (b == BlockType.Bed && Meta.Has(m, Meta.Second)) drops.AddRange(Items.Drops(b, GetMeta(other.Value), tool, rng));
                if (b == BlockType.Door && Meta.Has(m, Meta.Upper)) drops.AddRange(Items.Drops(b, GetMeta(other.Value), tool, rng));
                SetBlock(other.Value.x, other.Value.y, other.Value.z, BlockType.Air);
            }
            PopUnsupported(p, drops, rng);
            return drops;
        }

        /// <summary>The second cell of a two-cell block (door halves, bed foot/head).</summary>
        public static Vector3Int? LinkedHalf(Vector3Int p, BlockType b, byte m)
        {
            if (b == BlockType.Door) return p + (Meta.Has(m, Meta.Upper) ? Vector3Int.down : Vector3Int.up);
            if (b == BlockType.Bed) return p + Meta.Dir[Meta.Facing(m)] * (Meta.Has(m, Meta.Second) ? -1 : 1);
            return null;
        }

        /// <summary>Things that need the block at <paramref name="p"/> to stand on or hang from fall off when it goes.</summary>
        void PopUnsupported(Vector3Int p, List<Stack> drops, System.Random rng)
        {
            var up = p + Vector3Int.up;
            var ub = GetBlock(up); byte um = GetMeta(up);
            var ush = BlockInfo.ShapeOf(ub);
            bool needsFloor = (ush == Shape.Cross && !Meta.Has(um, Meta.Upper)) || (ub == BlockType.Door && !Meta.Has(um, Meta.Upper)) ||
                              (ub == BlockType.Torch && !Meta.Has(um, Meta.Second)) || ub == BlockType.Bed;
            if (needsFloor) drops.AddRange(BreakBlock(up, null, rng));
            var down = p + Vector3Int.down;
            if (GetBlock(down) == BlockType.Dripstone && Meta.Has(GetMeta(down), Meta.Upper)) drops.AddRange(BreakBlock(down, null, rng));
            if (GetBlock(up) == BlockType.LilyPad && GetBlock(p) != BlockType.Water) drops.AddRange(BreakBlock(up, null, rng));
            for (int f = 0; f < 4; f++)
            {
                var n = p - Meta.Dir[f];          // a neighbour whose wall (in direction f) is this block
                var nb = GetBlock(n); byte nm = GetMeta(n);
                bool hangs = (nb == BlockType.Ladder && Meta.Facing(nm) == f) ||
                             (nb == BlockType.Torch && Meta.Has(nm, Meta.Second) && Meta.Facing(nm) == f);
                if (hangs) drops.AddRange(BreakBlock(n, null, rng));
            }
        }

        /// <summary>Opens / closes a door (both halves), trapdoor or fence gate.</summary>
        public void ToggleOpen(Vector3Int p)
        {
            var b = GetBlock(p);
            byte m = (byte)(GetMeta(p) ^ Meta.Open);
            SetMeta(p, m);
            if (b == BlockType.Door)
            {
                var other = p + (Meta.Has(m, Meta.Upper) ? Vector3Int.down : Vector3Int.up);
                if (GetBlock(other) == BlockType.Door) SetMeta(other, (byte)(GetMeta(other) ^ Meta.Open));
            }
        }

        static readonly Vector3Int[] FlowDirs = { Vector3Int.down, Vector3Int.left, Vector3Int.right, new(0, 0, 1), new(0, 0, -1) };
        readonly Queue<Vector3Int> floodQueue = new();

        /// <summary>
        /// Water pours into a cell opened next to it (from above or the side), then keeps spreading down and sideways
        /// through connected empty cells at or below the water line — no air pockets left under lakes and rivers.
        /// </summary>
        void Flood(int wx, int y, int wz)
        {
            floodQueue.Clear();
            floodQueue.Enqueue(new Vector3Int(wx, y, wz));
            int budget = 512;
            while (floodQueue.Count > 0 && budget-- > 0)
            {
                var p = floodQueue.Dequeue();
                if (p.y > TerrainGenerator.SeaLevel || GetBlock(p.x, p.y, p.z) != BlockType.Air) continue;
                bool wet = GetBlock(p.x, p.y + 1, p.z) == BlockType.Water;
                for (int i = 1; i < FlowDirs.Length && !wet; i++)
                {
                    var n = p + FlowDirs[i];
                    wet = GetBlock(n.x, n.y, n.z) == BlockType.Water;
                }
                if (!wet) continue;
                SetBlock(p.x, p.y, p.z, BlockType.Water);
                foreach (var d in FlowDirs) floodQueue.Enqueue(p + d);
            }
        }

        void MarkDirty(Vector2Int cc) { if (chunks.TryGetValue(cc, out var c)) c.Dirty = true; }

        /// <summary>True if a grass tuft / flower / bush grows in this (air) cell.</summary>
        public bool PlantAt(Vector3Int p)
        {
            if (p.y < 0 || p.y >= Chunk.Height || !chunks.TryGetValue(ChunkOf(p.x, p.z), out var c)) return false;
            return c.HasFoliageAt(Mod(p.x), p.y, Mod(p.z));
        }

        /// <summary>Breaks the plant in a cell (instantly, like grass in Minecraft). Returns false if there was none.</summary>
        public bool BreakPlant(Vector3Int p)
        {
            if (p.y < 0 || p.y >= Chunk.Height || !chunks.TryGetValue(ChunkOf(p.x, p.z), out var c)) return false;
            if (c.RemovePlant(Mod(p.x), p.y, Mod(p.z)) < 0) return false;
            Edits.TryAdd(p, (GetBlock(p), GetMeta(p)));          // stays gone when the chunk is reloaded / the world is loaded
            return true;
        }

        /// <summary>Selection / hit box of a plant (Minecraft's grass box).</summary>
        public static readonly Bounds PlantBox = new(new Vector3(0.5f, 0.4f, 0.5f), new Vector3(0.75f, 0.8f, 0.75f));

        public Chunk ChunkAt(Vector2Int cc) => chunks.TryGetValue(cc, out var c) ? c : null;

        /// <summary>A light source at <paramref name="p"/> was lit or went out: relight its chunk (and neighbours it reaches).</summary>
        public void LightChanged(Vector3Int p)
        {
            if (!chunks.TryGetValue(ChunkOf(p.x, p.z), out var c)) return;
            c.Dirty = true; c.LightEdit = true;
        }

        public static int Mod(int v) => ((v % Chunk.Size) + Chunk.Size) % Chunk.Size;

        public bool IsReady(Vector3 pos)
        {
            var cc = ChunkOf(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z));
            return chunks.TryGetValue(cc, out var c) && c.MeshBuilt;
        }

        public int SurfaceY(int wx, int wz)
        {
            for (int y = Chunk.Height - 1; y >= 0; y--)
                if (BlockInfo.IsSolid(GetBlock(wx, y, wz))) return y;
            return 0;
        }

        void Update()
        {
            if (Target == null) return;
            var center = ChunkOf(Mathf.FloorToInt(Target.position.x), Mathf.FloorToInt(Target.position.z));
            if (center != lastCenter) { lastCenter = center; RebuildWanted(center); }

            // 1) generate block data (view radius + 1 so border faces have neighbours)
            int made = 0;
            foreach (var cc in wanted)
            {
                if (made >= DataPerFrame) break;
                if (chunks.ContainsKey(cc)) continue;
                var c = new Chunk(cc);
                Terrain.Fill(c);
                ApplyEdits(c);
                RegisterGeneratedEntities(c);
                chunks[cc] = c;
                made++;
            }

            // 2) build meshes for the inner radius, nearest first; decorate on first build
            int meshed = 0;
            foreach (var cc in wanted)
            {
                if (meshed >= MeshPerFrame) break;
                if (Mathf.Max(Mathf.Abs(cc.x - center.x), Mathf.Abs(cc.y - center.y)) > ViewRadius) continue;
                if (!chunks.TryGetValue(cc, out var c)) continue;
                if (c.MeshBuilt && !c.Dirty) continue;
                if (!NeighboursLoaded(cc)) continue;
                if (c.Go == null) c.Attach(transform, VoxelMaterial, WaterMaterial);
                bool first = !c.MeshBuilt;
                c.BuildMesh(this);
                if (!c.Decorated) Decorator.Decorate(this, c);
                if (first) SpawnEntities(c);
                meshed++;
            }

            // 3) unload far chunks
            if (made == 0 && meshed == 0) Unload(center);

            BlockEntities.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Editor preview without Play Mode: initialises the world and synchronously builds and decorates the chunks
        /// within <paramref name="radius"/> of <paramref name="center"/>. Plants are drawn by <see cref="DrawFoliage"/>.
        /// </summary>
        public void GeneratePreview(Vector3 center, int radius)
        {
            Awake();
            ViewRadius = radius;
            var cc = ChunkOf(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));
            lastCenter = cc;
            RebuildWanted(cc);
            foreach (var w in wanted)
            {
                var c = new Chunk(w);
                Terrain.Fill(c);
                RegisterGeneratedEntities(c);
                chunks[w] = c;
            }
            foreach (var w in wanted)
            {
                if (Mathf.Max(Mathf.Abs(w.x - cc.x), Mathf.Abs(w.y - cc.y)) > radius) continue;
                var c = chunks[w];
                c.Attach(transform, VoxelMaterial, WaterMaterial);
                c.BuildMesh(this);
                Decorator.Decorate(this, c);
                SpawnEntities(c);
            }
        }

        /// <summary>Remeshes every changed chunk right away (editor previews, where Update does not run).</summary>
        public void RebuildDirty()
        {
            foreach (var c in chunks.Values) if (c.Dirty && c.Go != null) c.BuildMesh(this);
        }

        /// <summary>Queues the instanced plants for one camera (used by editor previews; the game uses LateUpdate).</summary>
        public void DrawFoliage(Camera cam)
        {
            var rp = new RenderParams(Foliage.Material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, camera = cam };
            foreach (var c in chunks.Values)
            {
                if (!c.MeshBuilt) continue;
                rp.worldBounds = c.WorldBounds;
                foreach (var f in c.Foliage)
                    if (f.Value.Count > 0) Graphics.RenderMeshInstanced(rp, Foliage.Meshes[f.Key], 0, f.Value);
            }
        }

        void ApplyEdits(Chunk c)
        {
            if (Edits.Count == 0) return;
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            foreach (var kv in Edits)
            {
                var p = kv.Key;
                if (p.x < ox || p.x >= ox + Chunk.Size || p.z < oz || p.z >= oz + Chunk.Size) continue;
                c.Blocks[p.x - ox, p.y, p.z - oz] = kv.Value.type;
                c.Metas[p.x - ox, p.y, p.z - oz] = kv.Value.meta;
                c.Half[p.x - ox, p.y, p.z - oz] = false;
            }
        }

        /// <summary>Block entities placed by the generator (aul hearths, yurt furniture) get their state objects.</summary>
        void RegisterGeneratedEntities(Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                    for (int y = 1; y < Chunk.Height; y++)
                    {
                        var b = c.Blocks[x, y, z];
                        if (!BlockInfo.IsEntity(b)) continue;
                        var p = new Vector3Int(ox + x, y, oz + z);
                        if (BlockEntities.All.ContainsKey(p)) continue;
                        var e = BlockEntities.Create(p, b, c.Metas[x, y, z]);
                        if (b == BlockType.Campfire) e.Fuel = -1f;     // aul hearths never go out
                        if (b == BlockType.Kazan) { e.Water = BlockEntity.MaxWater; e.Storage[4] = new Stack((int)ItemType.Coal, 4); }   // the family kazan is ready to cook
                    }
        }

        void SpawnEntities(Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            foreach (var e in BlockEntities.All.Values)
                if (e.Go == null && e.Pos.x >= ox && e.Pos.x < ox + Chunk.Size && e.Pos.z >= oz && e.Pos.z < oz + Chunk.Size)
                    BlockEntities.Spawn(e, c.Go.transform);
        }

        public bool TryGetEntity(Vector3Int p, out BlockEntity e) => BlockEntities.All.TryGetValue(p, out e);

        // Plants are drawn with GPU instancing straight from the per-chunk matrix lists
        void LateUpdate()
        {
            if (Foliage == null || Foliage.Meshes.Count == 0 || lastCenter.x == int.MinValue) return;
            var rp = new RenderParams(Foliage.Material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = 0,
            };
            foreach (var kv in chunks)
            {
                var c = kv.Value;
                if (!c.MeshBuilt || c.Foliage.Count == 0) continue;
                if (Mathf.Max(Mathf.Abs(kv.Key.x - lastCenter.x), Mathf.Abs(kv.Key.y - lastCenter.y)) > FoliageRadius) continue;
                rp.worldBounds = c.WorldBounds;
                foreach (var f in c.Foliage)
                    if (f.Value.Count > 0) Graphics.RenderMeshInstanced(rp, Foliage.Meshes[f.Key], 0, f.Value);
            }
        }

        /// <summary>All 8 neighbours have block data (faces at the border and light from the 15-cell margin need them).</summary>
        bool NeighboursLoaded(Vector2Int cc)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (!chunks.ContainsKey(cc + new Vector2Int(dx, dz))) return false;
            return true;
        }

        void RebuildWanted(Vector2Int center)
        {
            wanted.Clear();
            int r = ViewRadius + 1;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    wanted.Add(new Vector2Int(center.x + dx, center.y + dz));
            wanted.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
        }

        readonly List<Vector2Int> toRemove = new();
        void Unload(Vector2Int center)
        {
            toRemove.Clear();
            int limit = ViewRadius + 3;
            foreach (var kv in chunks)
                if (Mathf.Max(Mathf.Abs(kv.Key.x - center.x), Mathf.Abs(kv.Key.y - center.y)) > limit)
                    toRemove.Add(kv.Key);
            foreach (var k in toRemove) { chunks[k].Destroy(); chunks.Remove(k); }
        }

        /// <summary>Voxel DDA raycast. Returns hit block and the empty block just before it.</summary>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out Vector3Int hit, out Vector3Int prev)
        {
            bool ok = Raycast(origin, dir, maxDist, out RayHit h);
            hit = h.Cell; prev = h.Prev;
            return ok;
        }

        public struct RayHit
        {
            public Vector3Int Cell, Normal;
            public Vector3 Point;
            public float Distance;
            public Vector3Int Prev => Cell + Normal;
        }

        static readonly List<Bounds> rayBoxes = new();

        /// <summary>
        /// Voxel DDA raycast that respects block shapes: it passes over the empty half of a slab, between fence rails and
        /// past a sapling's sides, and reports the exact point and face that was hit (for placing slabs, stairs, torches).
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out RayHit hit) => Raycast(origin, dir, maxDist, out hit, false);

        /// <param name="plants">The player's aim also hits grass and flowers (to break them); arrows and eyes see through.</param>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out RayHit hit, bool plants)
        {
            hit = default;
            dir.Normalize();
            var p = new Vector3Int(Mathf.FloorToInt(origin.x), Mathf.FloorToInt(origin.y), Mathf.FloorToInt(origin.z));
            var step = new Vector3Int(dir.x > 0 ? 1 : -1, dir.y > 0 ? 1 : -1, dir.z > 0 ? 1 : -1);
            Vector3 tDelta = new(Mathf.Abs(1f / (dir.x == 0 ? 1e-6f : dir.x)),
                                 Mathf.Abs(1f / (dir.y == 0 ? 1e-6f : dir.y)),
                                 Mathf.Abs(1f / (dir.z == 0 ? 1e-6f : dir.z)));
            Vector3 tMax = new(
                dir.x > 0 ? (p.x + 1 - origin.x) * tDelta.x : (origin.x - p.x) * tDelta.x,
                dir.y > 0 ? (p.y + 1 - origin.y) * tDelta.y : (origin.y - p.y) * tDelta.y,
                dir.z > 0 ? (p.z + 1 - origin.z) * tDelta.z : (origin.z - p.z) * tDelta.z);
            var enterNormal = Vector3Int.zero;
            float t = 0;
            while (t <= maxDist)
            {
                var hb = GetBlock(p.x, p.y, p.z);
                if (plants && hb == BlockType.Air && PlantAt(p) && BlockShapes.RayBox(origin - p, dir, PlantBox, out float pt, out var pn) && pt <= maxDist)
                {
                    hit = new RayHit { Cell = p, Normal = pn, Point = origin + dir * pt, Distance = pt };
                    return true;
                }
                if (hb != BlockType.Air && hb != BlockType.Water)
                {
                    if (!BlockShapes.Selection(this, p, hb, GetMeta(p), rayBoxes))
                    {
                        hit = new RayHit { Cell = p, Normal = enterNormal, Point = origin + dir * t, Distance = t };
                        if (hit.Normal == Vector3Int.zero) hit.Normal = -step.y * Vector3Int.up;
                        return true;
                    }
                    float best = float.MaxValue; Vector3Int bestN = default;
                    var local = origin - p;
                    foreach (var box in rayBoxes)
                        if (BlockShapes.RayBox(local, dir, box, out float bt, out var bn) && bt < best) { best = bt; bestN = bn; }
                    if (best <= maxDist)
                    {
                        hit = new RayHit { Cell = p, Normal = bestN, Point = origin + dir * best, Distance = best };
                        return true;
                    }
                }
                if (tMax.x < tMax.y && tMax.x < tMax.z) { p.x += step.x; t = tMax.x; tMax.x += tDelta.x; enterNormal = new Vector3Int(-step.x, 0, 0); }
                else if (tMax.y < tMax.z) { p.y += step.y; t = tMax.y; tMax.y += tDelta.y; enterNormal = new Vector3Int(0, -step.y, 0); }
                else { p.z += step.z; t = tMax.z; tMax.z += tDelta.z; enterNormal = new Vector3Int(0, 0, -step.z); }
            }
            return false;
        }
    }
}
