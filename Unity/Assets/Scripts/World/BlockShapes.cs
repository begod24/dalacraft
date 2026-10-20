using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Boxes (in cell space, 0..1) that make up a non-cube block. The same boxes are used for the chunk mesh and collider,
    /// the precise aim ray and the black selection outline, so what you see is what you hit.
    /// </summary>
    public static class BlockShapes
    {
        public const float P = 1f / 16f;

        static Bounds B(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var b = new Bounds(); b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1)); return b;
        }

        /// <summary>A plate of thickness <paramref name="t"/> against the cell side in direction <paramref name="facing"/>.</summary>
        public static Bounds Edge(int facing, float t, float y0 = 0, float y1 = 1) => facing switch
        {
            0 => B(0, y0, 1 - t, 1, y1, 1),
            1 => B(1 - t, y0, 0, 1, y1, 1),
            2 => B(0, y0, 0, 1, y1, t),
            _ => B(0, y0, 0, t, y1, 1),
        };

        /// <summary>Half of the cell on the <paramref name="facing"/> side, between y0 and y1.</summary>
        static Bounds HalfOn(int facing, float y0, float y1) => Edge(facing, 0.5f, y0, y1);

        /// <summary>The closed door panel sits on the side toward the player who placed it; open, it swings to the hinge side.</summary>
        public static int DoorSide(byte m)
        {
            int f = Meta.Facing(m), toward = (f + 2) % 4;
            if (!Meta.Has(m, Meta.Open)) return toward;
            return Meta.Has(m, Meta.HingeRight) ? (f + 3) % 4 : (f + 1) % 4;
        }

        /// <summary>Solid (rendered + collided) boxes of a shaped block. Fences and gates are meshed by the chunk itself.</summary>
        public static void Solid(BlockType b, byte m, List<Bounds> list)
        {
            list.Clear();
            switch (BlockInfo.ShapeOf(b))
            {
                case Shape.Slab:
                    if (Meta.Has(m, Meta.Double)) list.Add(B(0, 0, 0, 1, 1, 1));
                    else list.Add(Meta.Has(m, Meta.Upper) ? B(0, 0.5f, 0, 1, 1, 1) : B(0, 0, 0, 1, 0.5f, 1));
                    break;
                case Shape.Stairs:
                {
                    bool up = Meta.Has(m, Meta.Upper);
                    list.Add(up ? B(0, 0.5f, 0, 1, 1, 1) : B(0, 0, 0, 1, 0.5f, 1));
                    list.Add(up ? HalfOn(Meta.Facing(m), 0, 0.5f) : HalfOn(Meta.Facing(m), 0.5f, 1));
                    break;
                }
                case Shape.Door:
                    list.Add(Edge(DoorSide(m), 3 * P));
                    break;
                case Shape.Trapdoor:
                    if (Meta.Has(m, Meta.Open)) list.Add(Edge(Meta.Facing(m), 3 * P));
                    else list.Add(Meta.Has(m, Meta.Upper) ? B(0, 13 * P, 0, 1, 1, 1) : B(0, 0, 0, 1, 3 * P, 1));
                    break;
                case Shape.Ladder:
                    list.Add(Edge(Meta.Facing(m), P));
                    break;
            }
        }

        /// <summary>
        /// Boxes the aim ray tests and the outline draws. Returns false for a plain full cube (the fast path).
        /// </summary>
        public static bool Selection(World w, Vector3Int p, BlockType b, byte m, List<Bounds> list)
        {
            list.Clear();
            switch (BlockInfo.ShapeOf(b))
            {
                case Shape.Cube: return false;
                case Shape.Slab: case Shape.Stairs: case Shape.Door: case Shape.Trapdoor: case Shape.Ladder:
                    Solid(b, m, list); return true;
                case Shape.Cross: list.Add(B(2 * P, 0, 2 * P, 14 * P, 13 * P, 14 * P)); return true;
                case Shape.Flat: list.Add(B(P, 0, P, 15 * P, P, 15 * P)); return true;
                case Shape.Fence:
                {
                    list.Add(B(6 * P, 0, 6 * P, 10 * P, 1, 10 * P));
                    for (int f = 0; f < 4; f++)
                    {
                        var n = p + Meta.Dir[f];
                        var nb = w.GetBlock(n.x, n.y, n.z);
                        if (!BlockInfo.IsFence(nb) && !BlockInfo.IsOpaque(nb)) continue;
                        list.Add(f switch
                        {
                            0 => B(6 * P, 0, 10 * P, 10 * P, 1, 1),
                            1 => B(10 * P, 0, 6 * P, 1, 1, 10 * P),
                            2 => B(6 * P, 0, 0, 10 * P, 1, 6 * P),
                            _ => B(0, 0, 6 * P, 6 * P, 1, 10 * P),
                        });
                    }
                    return true;
                }
                case Shape.Gate:
                {
                    bool alongX = GateAlongX(w, p);
                    list.Add(alongX ? B(0, 5 * P, 7 * P, 1, 1, 9 * P) : B(7 * P, 5 * P, 0, 9 * P, 1, 1));
                    return true;
                }
                case Shape.Entity:
                    list.Add(EntityBox(b, m)); return true;
            }
            return false;
        }

        public static bool GateAlongX(World w, Vector3Int p)
        {
            bool Links(int dx, int dz) { var nb = w.GetBlock(p.x + dx, p.y, p.z + dz); return BlockInfo.IsFence(nb) || BlockInfo.IsOpaque(nb); }
            return Links(1, 0) || Links(-1, 0) || !(Links(0, 1) || Links(0, -1));
        }

        public static Bounds EntityBox(BlockType b, byte m) => b switch
        {
            BlockType.Torch when Meta.Has(m, Meta.Second) => TorchOnWall(Meta.Facing(m)),
            BlockType.Torch => B(6 * P, 0, 6 * P, 10 * P, 11 * P, 10 * P),
            BlockType.Campfire => B(0, 0, 0, 1, 7 * P, 1),
            BlockType.Bed => B(0, 0, 0, 1, 9 * P, 1),
            BlockType.Kazan => B(P, 0, P, 15 * P, 14 * P, 15 * P),
            _ => B(0, 0, 0, 1, 1, 1),
        };

        static Bounds TorchOnWall(int f)
        {
            var c = new Vector3(0.5f, 7 * P, 0.5f) + (Vector3)Meta.Dir[f] * 0.32f;
            return new Bounds(c, new Vector3(5 * P, 11 * P, 5 * P));
        }

        /// <summary>Ray vs. box (cell space). Returns entry distance and the face normal.</summary>
        public static bool RayBox(Vector3 o, Vector3 d, Bounds b, out float t, out Vector3Int normal)
        {
            t = 0; normal = default;
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity; int axis = 0;
            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(d[i]) < 1e-8f) { if (o[i] < b.min[i] || o[i] > b.max[i]) return false; continue; }
                float t1 = (b.min[i] - o[i]) / d[i], t2 = (b.max[i] - o[i]) / d[i];
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tmin) { tmin = t1; axis = i; }
                tmax = Mathf.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
            if (tmax < 0) return false;
            t = Mathf.Max(0, tmin);
            var n = Vector3Int.zero; n[axis] = d[axis] > 0 ? -1 : 1;
            normal = n;
            return true;
        }
    }
}
