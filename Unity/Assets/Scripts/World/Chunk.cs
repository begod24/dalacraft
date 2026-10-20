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
        /// <summary>True = block is a half-height slab (occupies the lower 0.5 of its cell).</summary>
        public readonly bool[,,] Half = new bool[Size, Height, Size];
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
            if (mc.sharedMesh != null) Object.Destroy(mc.sharedMesh);
            Object.Destroy(Go);
        }

        // ---------------------------------------------------------------- foliage
        public void AddFoliage(int type, int x, int y, int z, Matrix4x4 m)
        {
            foliageCells.Add((type, x, y, z, m));
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
                if (c.x == x && c.z == z && (c.y == y || c.y == y + 1)) { foliageCells.RemoveAt(i); changed = true; }
            }
            if (!changed) return;
            foreach (var l in Foliage.Values) l.Clear();
            foreach (var c in foliageCells) Foliage[c.type].Add(c.m);
        }

        // ---------------------------------------------------------------- meshing
        BlockType Get(World w, int ox, int oz, int x, int y, int z)
        {
            if (y < 0) return BlockType.Stone;
            if (y >= Height) return BlockType.Air;
            if (x >= 0 && x < Size && z >= 0 && z < Size) return Blocks[x, y, z];
            return w.GetBlock(ox + x, y, oz + z);
        }

        bool HalfAt(World w, int ox, int oz, int x, int y, int z)
        {
            if (y < 0 || y >= Height) return false;
            if (x >= 0 && x < Size && z >= 0 && z < Size) return Half[x, y, z];
            return w.IsHalf(ox + x, y, oz + z);
        }

        bool Occludes(World w, int ox, int oz, int x, int y, int z, bool fullOnly)
        {
            var b = Get(w, ox, oz, x, y, z);
            if (!BlockInfo.IsOpaque(b)) return false;
            return !fullOnly || !HalfAt(w, ox, oz, x, y, z);
        }

        int CornerAo(World w, int ox, int oz, Vector3Int layer, int ax1, int s1, int ax2, int s2, bool fullOnly)
        {
            var a = layer; a[ax1] += s1;
            var b = layer; b[ax2] += s2;
            var c = layer; c[ax1] += s1; c[ax2] += s2;
            bool o1 = Occludes(w, ox, oz, a.x, a.y, a.z, fullOnly);
            bool o2 = Occludes(w, ox, oz, b.x, b.y, b.z, fullOnly);
            if (o1 && o2) return 0;
            bool oc = Occludes(w, ox, oz, c.x, c.y, c.z, fullOnly);
            return 3 - ((o1 ? 1 : 0) + (o2 ? 1 : 0) + (oc ? 1 : 0));
        }

        public void BuildMesh(World world)
        {
            var verts = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var uvs = new List<Vector2>(4096);
            var cols = new List<Color32>(4096);
            var solidTris = new List<int>(8192);
            var waterTris = new List<int>(1024);
            const float tileUv = 1f / BlockInfo.AtlasTiles;
            const float inset = 0.001f;
            int ox = Coord.x * Size, oz = Coord.y * Size;
            var ao = new int[4];

            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    for (int y = 0; y < Height; y++)
                    {
                        var b = Blocks[x, y, z];
                        if (b == BlockType.Air || BlockInfo.IsEntity(b)) continue;
                        bool water = b == BlockType.Water;
                        bool half = Half[x, y, z];
                        bool seeThrough = !BlockInfo.IsOpaque(b);
                        float hy = half ? 0.5f : 1f;
                        for (int f = 0; f < 6; f++)
                        {
                            var n = Normals[f];
                            int nx = x + n.x, ny = y + n.y, nz = z + n.z;
                            var nb = Get(world, ox, oz, nx, ny, nz);
                            bool show;
                            if (water) show = nb == BlockType.Air;
                            else if (seeThrough) show = nb != b && !BlockInfo.IsOpaque(nb);   // ice / leaves
                            else if (!BlockInfo.IsOpaque(nb)) show = true;
                            else
                            {
                                bool nbHalf = HalfAt(world, ox, oz, nx, ny, nz);
                                if (f == 0) show = half;                  // top hidden only by a block directly above a full block
                                else if (f == 1) show = nbHalf;           // bottom hidden unless the block below is a slab
                                else show = nbHalf && !half;              // side peeks out over a lower neighbour slab
                            }
                            if (!show) continue;

                            // corner AO in the layer in front of the face (slab tops look at their own layer)
                            int a1 = n.x != 0 ? 1 : 0, a2 = n.z != 0 ? 1 : 2;
                            if (n.y != 0) { a1 = 0; a2 = 2; }
                            var layer = new Vector3Int(nx, ny, nz);
                            bool slabTop = half && f == 0;
                            if (slabTop) layer = new Vector3Int(x, y, z);
                            for (int i = 0; i < 4; i++)
                            {
                                if (water) { ao[i] = 3; continue; }
                                var v = Verts[f][i];
                                int s1 = v[a1] > 0.5f ? 1 : -1, s2 = v[a2] > 0.5f ? 1 : -1;
                                ao[i] = CornerAo(world, ox, oz, layer, a1, s1, a2, s2, slabTop);
                            }

                            int tile = BlockInfo.Tile(b, f);
                            float u0 = (tile % BlockInfo.AtlasTiles) * tileUv + inset;
                            float v0 = (tile / BlockInfo.AtlasTiles) * tileUv + inset;
                            float span = tileUv - inset * 2f;
                            int start = verts.Count;
                            float dy = water ? -0.1f : 0f;
                            for (int i = 0; i < 4; i++)
                            {
                                var v = Verts[f][i];
                                verts.Add(new Vector3(x + v.x, y + v.y * hy + (water && v.y > 0 ? dy : 0), z + v.z));
                                normals.Add(n);
                                // slab sides show the upper half of the tile (keeps grass edges visible)
                                float uvy = f >= 2 ? 1f - hy + UVs[f][i].y * hy : UVs[f][i].y;
                                uvs.Add(new Vector2(u0 + UVs[f][i].x * span, v0 + uvy * span));
                                byte s = (byte)(Shade[f] * AoCurve[ao[i]] * 255f);
                                cols.Add(new Color32(s, s, s, 255));
                            }
                            var tris = water ? waterTris : solidTris;
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
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(solidTris, 0);
            mesh.SetTriangles(waterTris, 1);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // Collider: solid submesh only so the player can wade through water
            var colMesh = new Mesh { name = "ChunkCol", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            colMesh.SetVertices(verts);
            colMesh.SetTriangles(solidTris, 0);
            if (mc.sharedMesh != null) Object.Destroy(mc.sharedMesh);
            mc.sharedMesh = colMesh;

            MeshBuilt = true;
            Dirty = false;
        }
    }
}
