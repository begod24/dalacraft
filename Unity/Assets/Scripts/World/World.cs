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
        /// <summary>Player edits, re-applied when a chunk is regenerated after unloading.</summary>
        public readonly Dictionary<Vector3Int, BlockType> Edits = new();
        readonly List<Vector2Int> wanted = new();
        Vector2Int lastCenter = new(int.MinValue, int.MinValue);

        void Awake()
        {
            Instance = this;
            Terrain = new TerrainGenerator(Seed);
            VoxelMaterial = new Material(Shader.Find("Dala/VoxelLit")) { name = "VoxelRuntime" };
            VoxelMaterial.SetTexture("_BaseMap", Resources.Load<Texture2D>("Textures/T_BlockAtlas"));
            WaterMaterial = new Material(Shader.Find("Dala/Water")) { name = "WaterRuntime" };
            Foliage = new FoliageLibrary();
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

        public bool IsHalf(int wx, int y, int wz)
        {
            if (y < 0 || y >= Chunk.Height) return false;
            return chunks.TryGetValue(ChunkOf(wx, wz), out var c) && c.Half[Mod(wx), y, Mod(wz)];
        }

        /// <summary>Sets a block. Returns contents of a removed chest/furnace (if any) via <paramref name="spilled"/>.</summary>
        public bool SetBlock(int wx, int y, int wz, BlockType b) => SetBlock(wx, y, wz, b, out _);

        public bool SetBlock(int wx, int y, int wz, BlockType b, out List<Stack> spilled)
        {
            spilled = null;
            if (y < 0 || y >= Chunk.Height) return false;
            var cc = ChunkOf(wx, wz);
            if (!chunks.TryGetValue(cc, out var c)) return false;
            int lx = Mod(wx), lz = Mod(wz);
            var pos = new Vector3Int(wx, y, wz);
            if (BlockInfo.IsEntity(c.Blocks[lx, y, lz])) spilled = BlockEntities.Remove(pos);
            c.Blocks[lx, y, lz] = b;
            Edits[pos] = b;
            if (BlockInfo.IsEntity(b))
            {
                var e = BlockEntities.Create(pos, b);
                if (c.Go != null) BlockEntities.Spawn(e, c.Go.transform);
            }
            c.Half[lx, y, lz] = false;
            c.RemoveFoliageAt(lx, y, lz);
            c.Dirty = true;
            if (lx == 0) MarkDirty(cc + Vector2Int.left);
            if (lx == Chunk.Size - 1) MarkDirty(cc + Vector2Int.right);
            if (lz == 0) MarkDirty(cc + Vector2Int.down);
            if (lz == Chunk.Size - 1) MarkDirty(cc + Vector2Int.up);
            return true;
        }

        void MarkDirty(Vector2Int cc) { if (chunks.TryGetValue(cc, out var c)) c.Dirty = true; }
        static int Mod(int v) => ((v % Chunk.Size) + Chunk.Size) % Chunk.Size;

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

        void ApplyEdits(Chunk c)
        {
            if (Edits.Count == 0) return;
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            foreach (var kv in Edits)
            {
                var p = kv.Key;
                if (p.x < ox || p.x >= ox + Chunk.Size || p.z < oz || p.z >= oz + Chunk.Size) continue;
                c.Blocks[p.x - ox, p.y, p.z - oz] = kv.Value;
                c.Half[p.x - ox, p.y, p.z - oz] = false;
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

        bool NeighboursLoaded(Vector2Int cc) =>
            chunks.ContainsKey(cc + Vector2Int.left) && chunks.ContainsKey(cc + Vector2Int.right) &&
            chunks.ContainsKey(cc + Vector2Int.up) && chunks.ContainsKey(cc + Vector2Int.down);

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
            hit = prev = default;
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
            var last = p;
            float t = 0;
            while (t <= maxDist)
            {
                var hb = GetBlock(p.x, p.y, p.z);
                if (BlockInfo.IsSolid(hb) || BlockInfo.IsEntity(hb)) { hit = p; prev = last; return true; }
                last = p;
                if (tMax.x < tMax.y && tMax.x < tMax.z) { p.x += step.x; t = tMax.x; tMax.x += tDelta.x; }
                else if (tMax.y < tMax.z) { p.y += step.y; t = tMax.y; tMax.y += tDelta.y; }
                else { p.z += step.z; t = tMax.z; tMax.z += tDelta.z; }
            }
            return false;
        }
    }
}
