using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public class Chunk
    {
        public const int Size = 16;
        public const int Height = 128;

        public readonly Vector2Int Coord;
        public readonly BlockType[,,] Blocks = new BlockType[Size, Height, Size];
        /// <summary>Per-cell orientation / state bits (see <see cref="Meta"/>).</summary>
        public readonly byte[,,] Metas = new byte[Size, Height, Size];
        /// <summary>True = block is a half-height slab (occupies the lower 0.5 of its cell). Kept for old terrain; unused.</summary>
        public readonly bool[,,] Half = new bool[Size, Height, Size];
        /// <summary>Light levels per cell (sky &lt;&lt; 4 | block), filled by <see cref="Lighting.Compute"/> when meshing.</summary>
        public readonly byte[,,] Light = new byte[Size, Height, Size];
        /// <summary>A block or light source changed here: neighbours must be re-lit if the light reaching them changed.</summary>
        public bool LightEdit;
        public readonly Biome[,] Biomes = new Biome[Size, Size];
        /// <summary>Generated surface height per column (before player edits).</summary>
        public readonly byte[,] Top = new byte[Size, Size];

        /// <summary>Instanced plants per foliage mesh index (drawn by World with GPU instancing).</summary>
        public readonly Dictionary<int, List<Matrix4x4>> Foliage = new();
        readonly List<(int type, int x, int y, int z, Matrix4x4 m)> foliageCells = new();
        public bool Decorated;
        public Bounds WorldBounds => new(new Vector3(Coord.x * Size + Size * 0.5f, Height * 0.5f, Coord.y * Size + Size * 0.5f),
                                         new Vector3(Size + 2, Height, Size + 2));

        public GameObject Go;
        public bool MeshBuilt, Dirty;
        MeshFilter mf;
        MeshCollider mc;

        public Chunk(Vector2Int coord) { Coord = coord; }

        // Face tables: 4 vertices per face (clockwise as seen from outside, Unity winding)
        static readonly Vector3Int[] Normals =
        {
            new(0,1,0), new(0,-1,0), new(1,0,0), new(-1,0,0), new(0,0,1), new(0,0,-1)
        };
        static readonly Vector3[][] Verts =
        {
            new[]{ new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            new[]{ new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
            new[]{ new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            new[]{ new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0) },
            new[]{ new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) },
            new[]{ new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };
        static readonly Vector2[][] UVs =
        {
            new[]{ new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) },
            new[]{ new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) },
            new[]{ new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) },
            new[]{ new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) },
            new[]{ new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) },
            new[]{ new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) },
        };
        // Mild directional tint on top of real lighting, so block edges read clearly
        static readonly float[] Shade = { 1f, 0.78f, 0.93f, 0.93f, 0.88f, 0.88f };
        // Corner ambient occlusion: 0 = fully occluded corner .. 3 = open
        static readonly float[] AoCurve = { 0.45f, 0.64f, 0.82f, 1f };

        public void Attach(Transform parent, Material voxel, Material water)
        {
            Go = new GameObject($"Chunk {Coord.x},{Coord.y}");
            Go.transform.SetParent(parent, false);
            Go.transform.position = new Vector3(Coord.x * Size, 0, Coord.y * Size);
            mf = Go.AddComponent<MeshFilter>();
            Go.AddComponent<MeshRenderer>().sharedMaterials = new[] { voxel, water };
            mc = Go.AddComponent<MeshCollider>();
        }

        public void Destroy()
        {
            if (Go == null) return;
            if (mf.sharedMesh != null) Object.Destroy(mf.sharedMesh);
            if (mc.sharedMesh != null) { if (Application.isPlaying) Object.Destroy(mc.sharedMesh); else Object.DestroyImmediate(mc.sharedMesh); }
            Object.Destroy(Go);
        }

        // ---------------------------------------------------------------- foliage
        readonly HashSet<int> plantCells = new();
        static int PlantKey(int x, int y, int z) => (x * Height + y) * Size + z;

        public void AddFoliage(int type, int x, int y, int z, Matrix4x4 m)
        {
            foliageCells.Add((type, x, y, z, m));
            plantCells.Add(PlantKey(x, y, z));
            if (!Foliage.TryGetValue(type, out var list)) Foliage[type] = list = new List<Matrix4x4>();
            list.Add(m);
        }

        /// <summary>Removes plants standing on / in the edited cell so nothing floats after digging or building.</summary>
        public void RemoveFoliageAt(int x, int y, int z)
        {
            bool changed = false;
            for (int i = foliageCells.Count - 1; i >= 0; i--)
            {
                var c = foliageCells[i];
                if (c.x == x && c.z == z && (c.y == y || c.y == y + 1)) { foliageCells.RemoveAt(i); plantCells.Remove(PlantKey(c.x, c.y, c.z)); changed = true; }
            }
            if (!changed) return;
            foreach (var l in Foliage.Values) l.Clear();
            foreach (var c in foliageCells) Foliage[c.type].Add(c.m);
        }

        /// <summary>True if a generated plant stands in the cell (decorator skips cells with trees in them).</summary>
        public bool HasFoliageAt(int x, int y, int z) => plantCells.Contains(PlantKey(x, y, z));

        /// <summary>Removes the plant growing in exactly this cell (broken by hand, like Minecraft's grass). Returns its mesh index or -1.</summary>
        public int RemovePlant(int x, int y, int z)
        {
            if (!plantCells.Remove(PlantKey(x, y, z))) return -1;
            int type = -1;
            for (int i = foliageCells.Count - 1; i >= 0; i--)
            {
                var c = foliageCells[i];
                if (c.x == x && c.y == y && c.z == z) { type = c.type; foliageCells.RemoveAt(i); }
            }
            foreach (var l in Foliage.Values) l.Clear();
            foreach (var c in foliageCells) Foliage[c.type].Add(c.m);
            return type;
        }

        // ---------------------------------------------------------------- meshing
        BlockType Get(World w, int ox, int oz, int x, int y, int z)
        {
            if (y < 0) return BlockType.Stone;
            if (y >= Height) return BlockType.Air;
            if (x >= 0 && x < Size && z >= 0 && z < Size) return Blocks[x, y, z];
            return w.GetBlock(ox + x, y, oz + z);
        }

        bool Occludes(World w, int ox, int oz, int x, int y, int z) => BlockInfo.IsOpaque(Get(w, ox, oz, x, y, z));

        int CornerAo(World w, int ox, int oz, Vector3Int layer, int ax1, int s1, int ax2, int s2)
        {
            var a = layer; a[ax1] += s1;
            var b = layer; b[ax2] += s2;
            var c = layer; c[ax1] += s1; c[ax2] += s2;
            bool o1 = Occludes(w, ox, oz, a.x, a.y, a.z);
            bool o2 = Occludes(w, ox, oz, b.x, b.y, b.z);
            if (o1 && o2) return 0;
            bool oc = Occludes(w, ox, oz, c.x, c.y, c.z);
            return 3 - ((o1 ? 1 : 0) + (o2 ? 1 : 0) + (oc ? 1 : 0));
        }

        // ---------------------------------------------------------------- light (from the region Lighting.Compute just filled)
        static byte L(int level) => (byte)(level * 17);

        /// <summary>Vertex colour: R = shade × AO, G = sky light, B = block light (0..255 = level 0..15).</summary>
        static Color32 LightCol(byte shade, int sky, int blk) => new(shade, L(sky), L(blk), 255);

        /// <summary>Brightest light of a cell and (if see-through) its neighbour along <paramref name="n"/> — for boxes and plants.</summary>
        Color32 CellLight(World w, int ox, int oz, Vector3Int cell, Vector3Int n, byte shade)
        {
            int sk = Lighting.Sky(cell.x, cell.y, cell.z), bl = Lighting.Block(cell.x, cell.y, cell.z);
            var nb = cell + n;
            if (n != Vector3Int.zero && !Occludes(w, ox, oz, nb.x, nb.y, nb.z))
            {
                sk = Mathf.Max(sk, Lighting.Sky(nb.x, nb.y, nb.z)); bl = Mathf.Max(bl, Lighting.Block(nb.x, nb.y, nb.z));
            }
            return LightCol(shade, sk, bl);
        }

        /// <summary>Minecraft smooth lighting: a face corner averages the light of the (up to) 4 open cells touching it.</summary>
        void CornerLight(World w, int ox, int oz, Vector3Int layer, int ax1, int s1, int ax2, int s2, out float sk, out float bl)
        {
            var a = layer; a[ax1] += s1;
            var b = layer; b[ax2] += s2;
            var c = layer; c[ax1] += s1; c[ax2] += s2;
            int n = 1;
            sk = Lighting.Sky(layer.x, layer.y, layer.z); bl = Lighting.Block(layer.x, layer.y, layer.z);
            bool oa = Occludes(w, ox, oz, a.x, a.y, a.z), ob = Occludes(w, ox, oz, b.x, b.y, b.z);
            if (!oa) { sk += Lighting.Sky(a.x, a.y, a.z); bl += Lighting.Block(a.x, a.y, a.z); n++; }
            if (!ob) { sk += Lighting.Sky(b.x, b.y, b.z); bl += Lighting.Block(b.x, b.y, b.z); n++; }
            if (!(oa && ob) && !Occludes(w, ox, oz, c.x, c.y, c.z)) { sk += Lighting.Sky(c.x, c.y, c.z); bl += Lighting.Block(c.x, c.y, c.z); n++; }
            sk /= n; bl /= n;
        }

        /// <summary>Vertex streams of one chunk mesh (UV1.x carries the atlas page of every vertex).</summary>
        class Builder
        {
            public readonly List<Vector3> Verts = new(4096), Normals = new(4096);
            public readonly List<Vector2> Uvs = new(4096), Pages = new(4096);
            public readonly List<Color32> Cols = new(4096);
            public readonly List<int> Solid = new(8192), Water = new(1024), Deco = new(256), ColOnly = new(256);
        }

        const float P = BlockShapes.P;

        static void TileRect(int tile, out float u0, out float v0, out float page)
        {
            const float tileUv = 1f / BlockInfo.AtlasTiles;
            int slot = tile % BlockInfo.TilesPerPage;
            page = tile / BlockInfo.TilesPerPage;
            u0 = (slot % BlockInfo.AtlasTiles) * tileUv; v0 = (slot / BlockInfo.AtlasTiles) * tileUv;
        }

        /// <summary>
        /// A box with per-face tiles; UVs are projected from the position so thin parts keep the 16px texel scale.
        /// Faces lying on the cell border against an opaque neighbour are skipped.
        /// </summary>
        void AddBox(World w, int ox, int oz, Vector3Int cell, Vector3 a, Vector3 c, System.Func<int, int> tileOf, Builder mb, List<int> tris, bool cull = true)
        {
            const float tileUv = 1f / BlockInfo.AtlasTiles;
            var size = c - a;
            for (int f = 0; f < 6; f++)
            {
                var n = Normals[f];
                if (cull)
                {
                    float edge = f switch { 0 => c.y, 1 => a.y, 2 => c.x, 3 => a.x, 4 => c.z, _ => a.z };
                    float border = f switch { 0 => cell.y + 1, 1 => cell.y, 2 => cell.x + 1, 3 => cell.x, 4 => cell.z + 1, _ => cell.z };
                    if (Mathf.Abs(edge - border) < 1e-4f && Occludes(w, ox, oz, cell.x + n.x, cell.y + n.y, cell.z + n.z)) continue;
                }
                TileRect(tileOf(f), out float u0, out float v0, out float page);
                var lc = CellLight(w, ox, oz, cell, n, (byte)(Shade[f] * 255f));
                int start = mb.Verts.Count;
                for (int i = 0; i < 4; i++)
                {
                    var v = Verts[f][i];
                    var p = new Vector3(a.x + v.x * size.x, a.y + v.y * size.y, a.z + v.z * size.z);
                    mb.Verts.Add(p);
                    mb.Normals.Add(n);
                    float pu = n.x != 0 ? p.z : p.x, pv = n.y != 0 ? p.z : p.y;
                    pu -= Mathf.Floor(pu + 1e-4f); pv -= Mathf.Floor(pv + 1e-4f);
                    if (pu < 1e-4f && (n.x != 0 ? v.z : v.x) > 0.5f) pu = 1f;
                    if (pv < 1e-4f && (n.y != 0 ? v.z : v.y) > 0.5f) pv = 1f;
                    mb.Uvs.Add(new Vector2(u0 + (0.001f + pu * (tileUv - 0.002f)), v0 + (0.001f + pv * (tileUv - 0.002f))));
                    mb.Pages.Add(new Vector2(page, 0));
                    mb.Cols.Add(lc);
                }
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }
        }

        /// <summary>Dripstone: meta bits 0-1 = part (0 tip, 1 middle, 2 base); Upper = hangs from the ceiling.</summary>
        static int CrossTile(BlockType b, byte meta) => b != BlockType.Dripstone ? BlockInfo.Tile(b, 2)
            : (meta & 3) switch { 2 => BlockInfo.TileDripBase, 1 => BlockInfo.TileDripMid, _ => BlockInfo.TileDripTip };

        /// <summary>A double-sided square lying on the water (lily pad).</summary>
        static void AddFlat(Vector3 o, int tile, Builder mb, Color32 col)
        {
            const float tileUv = 1f / BlockInfo.AtlasTiles, y = 0.015f;
            TileRect(tile, out float u0, out float v0, out float page);
            Vector3[] q = { new(0, y, 0), new(0, y, 1), new(1, y, 1), new(1, y, 0) };
            Vector2[] uv = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
            for (int side = 0; side < 2; side++)
            {
                int start = mb.Verts.Count;
                for (int i = 0; i < 4; i++)
                {
                    mb.Verts.Add(o + q[i]); mb.Normals.Add(Vector3.up);
                    mb.Uvs.Add(new Vector2(u0 + 0.001f + uv[i].x * (tileUv - 0.002f), v0 + 0.001f + uv[i].y * (tileUv - 0.002f)));
                    mb.Pages.Add(new Vector2(page, 0)); mb.Cols.Add(col);
                }
                if (side == 0) { mb.Deco.Add(start); mb.Deco.Add(start + 1); mb.Deco.Add(start + 2); mb.Deco.Add(start); mb.Deco.Add(start + 2); mb.Deco.Add(start + 3); }
                else { mb.Deco.Add(start); mb.Deco.Add(start + 2); mb.Deco.Add(start + 1); mb.Deco.Add(start); mb.Deco.Add(start + 3); mb.Deco.Add(start + 2); }
            }
        }

        /// <summary>Two crossed, double-sided quads (saplings, mushrooms, crystals), like Minecraft's plants. <paramref name="flipV"/> hangs it upside down.</summary>
        static void AddCross(Vector3 o, int tile, Builder mb, Color32 col, bool flipV = false)
        {
            const float tileUv = 1f / BlockInfo.AtlasTiles, k = 0.15f;
            TileRect(tile, out float u0, out float v0, out float page);
            var quads = new[]
            {
                (new Vector3(k, 0, k), new Vector3(1 - k, 0, 1 - k)),
                (new Vector3(1 - k, 0, k), new Vector3(k, 0, 1 - k)),
            };
            foreach (var (p0, p1) in quads)
                for (int side = 0; side < 2; side++)
                {
                    int start = mb.Verts.Count;
                    var a = side == 0 ? p0 : p1; var b = side == 0 ? p1 : p0;
                    var n = Vector3.Cross(b - a, Vector3.up).normalized;
                    Vector3[] q = { a, a + Vector3.up, b + Vector3.up, b };
                    Vector2[] uv = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
                    for (int i = 0; i < 4; i++)
                    {
                        mb.Verts.Add(o + q[i]); mb.Normals.Add(Vector3.up * 0.6f + n * 0.4f);
                        float u = side == 0 ? uv[i].x : 1 - uv[i].x;
                        float v = flipV ? 1 - uv[i].y : uv[i].y;
                        mb.Uvs.Add(new Vector2(u0 + 0.001f + u * (tileUv - 0.002f), v0 + 0.001f + v * (tileUv - 0.002f)));
                        mb.Pages.Add(new Vector2(page, 0));
                        mb.Cols.Add(col);
                    }
                    mb.Deco.Add(start); mb.Deco.Add(start + 1); mb.Deco.Add(start + 2);
                    mb.Deco.Add(start); mb.Deco.Add(start + 2); mb.Deco.Add(start + 3);
                }
        }

        // ---------------------------------------------------------------- fences & gates
        bool FenceLinks(World w, int ox, int oz, int x, int y, int z)
        {
            var n = Get(w, ox, oz, x, y, z);
            return BlockInfo.IsFence(n) || BlockInfo.IsOpaque(n);
        }

        /// <summary>
        /// Minecraft-style fence: a 4px post with two rails to every neighbouring fence/solid block, collision 1.5 high
        /// so it can't be jumped. A gate spans its cell along the fence line; open gates swing aside and let you through.
        /// </summary>
        void FenceMesh(World w, int ox, int oz, int x, int y, int z, BlockType b, byte meta, Builder mb)
        {
            int tile = BlockInfo.Tile(b, 2);
            int TileOf(int _) => tile;
            bool px = FenceLinks(w, ox, oz, x + 1, y, z), nx = FenceLinks(w, ox, oz, x - 1, y, z);
            bool pz = FenceLinks(w, ox, oz, x, y, z + 1), nz = FenceLinks(w, ox, oz, x, y, z - 1);
            var cell = new Vector3Int(x, y, z);
            var o = new Vector3(x, y, z);
            void Box(Vector3 a, Vector3 c, List<int> tris) => AddBox(w, ox, oz, cell, o + a, o + c, TileOf, mb, tris, false);

            if (b == BlockType.Fence)
            {
                Box(new Vector3(6 * P, 0, 6 * P), new Vector3(10 * P, 1, 10 * P), mb.Solid);
                foreach (var (on, dx, dz) in new[] { (px, 1, 0), (nx, -1, 0), (pz, 0, 1), (nz, 0, -1) })
                {
                    if (!on) continue;
                    for (int r = 0; r < 2; r++)
                    {
                        float y0 = r == 0 ? 6 * P : 12 * P, y1 = y0 + 3 * P;
                        Vector3 a, c;
                        if (dx != 0) { a = new Vector3(dx > 0 ? 10 * P : 0, y0, 7 * P); c = new Vector3(dx > 0 ? 1 : 6 * P, y1, 9 * P); }
                        else { a = new Vector3(7 * P, y0, dz > 0 ? 10 * P : 0); c = new Vector3(9 * P, y1, dz > 0 ? 1 : 6 * P); }
                        Box(a, c, mb.Solid);
                    }
                }
                // invisible collision up to 1.5 (post + arms toward neighbours)
                float x0 = nx ? 0 : 6 * P, x1 = px ? 1 : 10 * P, z0 = nz ? 0 : 6 * P, z1 = pz ? 1 : 10 * P;
                Box(new Vector3(x0, 1, 6 * P), new Vector3(x1, 1.5f, 10 * P), mb.ColOnly);
                Box(new Vector3(6 * P, 1, z0), new Vector3(10 * P, 1.5f, z1), mb.ColOnly);
                return;
            }

            // gate: along X if the fence line runs along X, else along Z
            bool alongX = px || nx || !(pz || nz);
            bool open = Meta.Has(meta, Meta.Open);
            Vector3 V(float a, float h, float c) => alongX ? new Vector3(a, h, c) : new Vector3(c, h, a);   // a = along the line, c = across
            Box(V(0, 5 * P, 7 * P), V(2 * P, 1, 9 * P), mb.Solid);
            Box(V(14 * P, 5 * P, 7 * P), V(1, 1, 9 * P), mb.Solid);
            if (!open)
            {
                foreach (float y0 in new[] { 6 * P, 12 * P }) Box(V(2 * P, y0, 7 * P), V(14 * P, y0 + 3 * P, 9 * P), mb.Solid);
                Box(V(6 * P, 9 * P, 7 * P), V(8 * P, 12 * P, 9 * P), mb.Solid);
                Box(V(8 * P, 9 * P, 7 * P), V(10 * P, 12 * P, 9 * P), mb.Solid);
                Box(V(0, 1, 7 * P), V(1, 1.5f, 9 * P), mb.ColOnly);
            }
            else
            {
                // the two leaves swing inwards and stand against the end posts
                foreach (float y0 in new[] { 6 * P, 12 * P })
                {
                    Box(V(0, y0, 9 * P), V(2 * P, y0 + 3 * P, 15 * P), mb.Deco);
                    Box(V(14 * P, y0, 9 * P), V(1, y0 + 3 * P, 15 * P), mb.Deco);
                }
            }
        }

        static readonly List<Bounds> shapeBoxes = new();

        /// <summary>Slabs, stairs, doors, trapdoors, ladders: the boxes from <see cref="BlockShapes.Solid"/>.</summary>
        void ShapedMesh(World w, int ox, int oz, int x, int y, int z, BlockType b, byte meta, Shape shape, Builder mb)
        {
            BlockShapes.Solid(b, meta, shapeBoxes);
            var cell = new Vector3Int(x, y, z);
            var o = new Vector3(x, y, z);
            int TileOf(int f)
            {
                if (shape == Shape.Door) return Meta.Has(meta, Meta.Upper) ? BlockInfo.TileDoorTop : BlockInfo.TileDoorBottom;
                return BlockInfo.Tile(b, f);
            }
            var tris = shape == Shape.Ladder ? mb.Deco : mb.Solid;
            foreach (var box in shapeBoxes) AddBox(w, ox, oz, cell, o + box.min, o + box.max, TileOf, mb, tris);
        }

        public void BuildMesh(World world)
        {
            var mb = new Builder();
            const float tileUv = 1f / BlockInfo.AtlasTiles;
            const float inset = 0.001f;
            int ox = Coord.x * Size, oz = Coord.y * Size;
            var ao = new int[4];
            var sk = new float[4]; var bl = new float[4];
            Lighting.Compute(world, this);

            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    for (int y = 0; y < Height; y++)
                    {
                        var b = Blocks[x, y, z];
                        var shape = BlockInfo.ShapeOf(b);
                        switch (shape)
                        {
                            case Shape.None: case Shape.Entity: continue;
                            case Shape.Fence: case Shape.Gate: FenceMesh(world, ox, oz, x, y, z, b, Metas[x, y, z], mb); continue;
                            case Shape.Cross:
                                AddCross(new Vector3(x, y, z), CrossTile(b, Metas[x, y, z]), mb, CellLight(world, ox, oz, new Vector3Int(x, y, z), Vector3Int.zero, 235),
                                         b == BlockType.Dripstone && !Meta.Has(Metas[x, y, z], Meta.Upper));
                                continue;
                            case Shape.Flat: AddFlat(new Vector3(x, y, z), BlockInfo.Tile(b, 0), mb, CellLight(world, ox, oz, new Vector3Int(x, y, z), Vector3Int.up, 240)); continue;
                            case Shape.Slab: case Shape.Stairs: case Shape.Door: case Shape.Trapdoor: case Shape.Ladder:
                                ShapedMesh(world, ox, oz, x, y, z, b, Metas[x, y, z], shape, mb); continue;
                        }
                        bool water = shape == Shape.Liquid;
                        bool seeThrough = !BlockInfo.IsOpaque(b);
                        for (int f = 0; f < 6; f++)
                        {
                            var n = Normals[f];
                            int nx = x + n.x, ny = y + n.y, nz = z + n.z;
                            var nb = Get(world, ox, oz, nx, ny, nz);
                            bool show;
                            if (water) show = nb == BlockType.Air || (BlockInfo.ShapeOf(nb) != Shape.Cube && nb != BlockType.Water && f == 0);
                            else if (seeThrough) show = nb != b && !BlockInfo.IsOpaque(nb);   // ice / leaves / glass
                            else show = !BlockInfo.IsOpaque(nb);
                            if (!show) continue;

                            int a1 = n.x != 0 ? 1 : 0, a2 = n.z != 0 ? 1 : 2;
                            if (n.y != 0) { a1 = 0; a2 = 2; }
                            var layer = new Vector3Int(nx, ny, nz);
                            for (int i = 0; i < 4; i++)
                            {
                                var v = Verts[f][i];
                                int s1 = v[a1] > 0.5f ? 1 : -1, s2 = v[a2] > 0.5f ? 1 : -1;
                                CornerLight(world, ox, oz, layer, a1, s1, a2, s2, out sk[i], out bl[i]);
                                ao[i] = water ? 3 : CornerAo(world, ox, oz, layer, a1, s1, a2, s2);
                            }

                            TileRect(BlockInfo.IsLog(b) ? BlockInfo.LogTile(b, f, Metas[x, y, z]) : BlockInfo.Tile(b, f), out float u0, out float v0, out float page);
                            u0 += inset; v0 += inset;
                            float span = tileUv - inset * 2f;
                            int start = mb.Verts.Count;
                            float dy = water ? -0.1f : 0f;
                            for (int i = 0; i < 4; i++)
                            {
                                var v = Verts[f][i];
                                mb.Verts.Add(new Vector3(x + v.x, y + v.y + (water && v.y > 0 ? dy : 0), z + v.z));
                                mb.Normals.Add(n);
                                mb.Uvs.Add(new Vector2(u0 + UVs[f][i].x * span, v0 + UVs[f][i].y * span));
                                mb.Pages.Add(new Vector2(page, 0));
                                byte s = (byte)(Shade[f] * AoCurve[ao[i]] * 255f);
                                mb.Cols.Add(new Color32(s, (byte)(sk[i] * 17f), (byte)(bl[i] * 17f), 255));
                            }
                            var tris = water ? mb.Water : mb.Solid;
                            // flip the quad diagonal so AO interpolates without a visible seam
                            if (ao[0] + ao[2] < ao[1] + ao[3])
                            {
                                tris.Add(start + 1); tris.Add(start + 2); tris.Add(start + 3);
                                tris.Add(start + 1); tris.Add(start + 3); tris.Add(start);
                            }
                            else
                            {
                                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                            }
                        }
                    }

            var mesh = mf.sharedMesh;
            if (mesh == null) mesh = new Mesh { name = "ChunkMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            else mesh.Clear();
            mesh.SetVertices(mb.Verts);
            mesh.SetNormals(mb.Normals);
            mesh.SetUVs(0, mb.Uvs);
            mesh.SetUVs(1, mb.Pages);
            mesh.SetColors(mb.Cols);
            mesh.subMeshCount = 2;
            var renderTris = mb.Solid;
            if (mb.Deco.Count > 0) { renderTris = new List<int>(mb.Solid); renderTris.AddRange(mb.Deco); }
            mesh.SetTriangles(renderTris, 0);
            mesh.SetTriangles(mb.Water, 1);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // Collider: solid boxes + invisible fence extensions; water, plants and ladders stay walk-through
            var colMesh = new Mesh { name = "ChunkCol", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            colMesh.SetVertices(mb.Verts);
            if (mb.ColOnly.Count > 0) mb.Solid.AddRange(mb.ColOnly);
            colMesh.SetTriangles(mb.Solid, 0);
            if (mc.sharedMesh != null) { if (Application.isPlaying) Object.Destroy(mc.sharedMesh); else Object.DestroyImmediate(mc.sharedMesh); }
            mc.sharedMesh = colMesh;

            MeshBuilt = true;
            Dirty = false;
        }
    }
}
