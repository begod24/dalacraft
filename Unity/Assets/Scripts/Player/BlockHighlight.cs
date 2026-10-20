using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Minecraft's block selection: thin dark lines around the block (or the exact shape of a slab, stairs, fence, torch…)
    /// under the crosshair, and the breaking cracks drawn on the block itself while it is being mined.
    /// </summary>
    public class BlockHighlight : MonoBehaviour
    {
        const float Line = 0.008f, Grow = 0.003f;
        MeshFilter outlineMf, cracksMf;
        Material cracksMat;
        Vector3Int lastCell = new(int.MinValue, 0, 0);
        BlockType lastType;
        byte lastMeta;
        readonly List<Bounds> boxes = new();
        Mesh outline, cracks;

        public static BlockHighlight Create(Transform parent)
        {
            var go = new GameObject("BlockHighlight");
            go.transform.SetParent(parent, false);
            return go.AddComponent<BlockHighlight>();
        }

        void Awake()
        {
            outline = new Mesh { name = "Outline" };
            cracks = new Mesh { name = "Cracks" };
            outlineMf = Part("Outline", new Material(Shader.Find("Dala/Outline")), outline);
            cracksMat = new Material(Shader.Find("Dala/Cracks"));
            cracksMat.SetTexture("_MainTex", Resources.Load<Texture2D>("Textures/T_Cracks"));
            cracksMf = Part("Cracks", cracksMat, cracks);
        }

        MeshFilter Part(string name, Material m, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.SetActive(false);
            return mf;
        }

        void LateUpdate()
        {
            var pc = PlayerController.Instance;
            var w = World.Instance;
            var aim = pc != null ? pc.Aim : null;
            if (aim == null || w == null)
            {
                outlineMf.gameObject.SetActive(false); cracksMf.gameObject.SetActive(false);
                lastCell = new Vector3Int(int.MinValue, 0, 0);
                return;
            }
            var cell = aim.Value.Cell;
            var b = w.GetBlock(cell); byte m = w.GetMeta(cell);
            if (cell != lastCell || b != lastType || m != lastMeta || BlockInfo.IsFence(b))
            {
                lastCell = cell; lastType = b; lastMeta = m;
                if (b == BlockType.Air && w.PlantAt(cell)) { boxes.Clear(); boxes.Add(World.PlantBox); }
                else if (!BlockShapes.Selection(w, cell, b, m, boxes)) { boxes.Clear(); boxes.Add(new Bounds(Vector3.one * 0.5f, Vector3.one)); }
                BuildOutline();
                BuildCracks();
                outlineMf.transform.position = cracksMf.transform.position = cell;
            }
            outlineMf.gameObject.SetActive(true);
            bool breaking = pc.BreakProgress > 0.01f && pc.BreakTarget == cell;
            cracksMf.gameObject.SetActive(breaking);
            if (breaking) cracksMat.SetFloat("_Stage", Mathf.Min(9, Mathf.FloorToInt(pc.BreakProgress * 10f)));
        }

        /// <summary>The 12 edges of every box as thin bars (constant thickness in world space, like Minecraft's lines).</summary>
        void BuildOutline()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (var bx in boxes)
            {
                var lo = bx.min - Vector3.one * Grow; var hi = bx.max + Vector3.one * Grow;
                for (int axis = 0; axis < 3; axis++)
                    for (int i = 0; i < 4; i++)
                    {
                        var a = lo; var c = hi;
                        int u = (axis + 1) % 3, w = (axis + 2) % 3;
                        a[u] = c[u] = (i & 1) == 0 ? lo[u] : hi[u];
                        a[w] = c[w] = (i & 2) == 0 ? lo[w] : hi[w];
                        a[axis] = lo[axis]; c[axis] = hi[axis];
                        var mn = a - Vector3.one * Line; var mx = c + Vector3.one * Line;
                        mn[axis] = lo[axis] - Line; mx[axis] = hi[axis] + Line;
                        Cube(v, t, mn, mx, null);
                    }
            }
            outline.Clear(); outline.SetVertices(v); outline.SetTriangles(t, 0); outline.RecalculateBounds();
        }

        void BuildCracks()
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            foreach (var bx in boxes) Cube(v, t, bx.min - Vector3.one * Grow, bx.max + Vector3.one * Grow, uv);
            cracks.Clear(); cracks.SetVertices(v); cracks.SetUVs(0, uv); cracks.SetTriangles(t, 0); cracks.RecalculateBounds();
        }

        static readonly int[][] Faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 },
        };

        /// <summary>Box with outward faces; UVs (if asked) are projected so cracks keep their texel size on thin shapes.</summary>
        static void Cube(List<Vector3> v, List<int> t, Vector3 mn, Vector3 mx, List<Vector2> uv)
        {
            Vector3[] c =
            {
                new(mn.x, mn.y, mn.z), new(mx.x, mn.y, mn.z), new(mx.x, mx.y, mn.z), new(mn.x, mx.y, mn.z),
                new(mn.x, mn.y, mx.z), new(mx.x, mn.y, mx.z), new(mx.x, mx.y, mx.z), new(mn.x, mx.y, mx.z),
            };
            for (int f = 0; f < 6; f++)
            {
                int s = v.Count;
                var n = Vector3.Cross(c[Faces[f][1]] - c[Faces[f][0]], c[Faces[f][3]] - c[Faces[f][0]]);
                foreach (int k in Faces[f])
                {
                    v.Add(c[k]);
                    if (uv == null) continue;
                    var p = c[k];
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y);
                    uv.Add(ax > 0.5f * n.magnitude ? new Vector2(p.z, p.y) : ay > 0.5f * n.magnitude ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
                }
                // winding: keep faces pointing out of the box
                var center = (mn + mx) * 0.5f;
                bool outward = Vector3.Dot(n, c[Faces[f][0]] - center) > 0;
                if (outward) { t.Add(s); t.Add(s + 3); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 1); }
                else { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            }
        }
    }
}
