using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Deterministic aul (nomad village) layouts — one possible aul per 160×160 region on flat steppe/foothills; the terrain under it is levelled (TerrainGenerator.FlattenForVillage).</summary>
    public class Village
    {
        public const int Region = 160;
        public Vector2Int Center;
        public readonly List<(string path, Vector2 pos, float yaw, float scale, bool collide, bool fire, float radius)> Props = new();
        /// <summary>Plateau height the terrain is levelled to under the aul.</summary>
        public int Ground;
        public readonly List<(string npc, Vector2 pos)> People = new();
        /// <summary>Real blocks (yurts, fences, hearths, workshops): world x/z, y relative to the plateau (1 = standing on it), placed by TerrainGenerator.Fill.</summary>
        public readonly List<(Vector3Int pos, BlockType type, byte meta)> Blocks = new();
        public readonly List<(string animal, Vector2 pos)> Livestock = new();

        static readonly Dictionary<Vector2Int, Village> cache = new();

        public static void ClearCache() => cache.Clear();

        public static Village At(Vector2Int region, TerrainGenerator t, int seed)
        {
            if (cache.TryGetValue(region, out var v)) return v;
            v = Generate(region, t, seed);
            cache[region] = v;
            return v;
        }

        /// <summary>Steppe / foothills / birch groves above the water, gentle enough to level, with no river through it.</summary>
        static bool GoodSite(TerrainGenerator t, int cx, int cz, out int h)
        {
            h = t.BaseHeight(cx, cz);
            var biome = t.GetBiome(cx, cz, h);
            if (biome != Biome.Steppe && biome != Biome.Altai && biome != Biome.BirchGrove) return false;
            if (h <= TerrainGenerator.SeaLevel + 1) return false;
            for (int dx = -24; dx <= 24; dx += 12)
                for (int dz = -24; dz <= 24; dz += 12)
                {
                    if (Mathf.Abs(t.BaseHeight(cx + dx, cz + dz) - h) > 5) return false;
                    if (t.Riverness(cx + dx, cz + dz) > 0.01f) return false;
                }
            return true;
        }

        static Village Generate(Vector2Int region, TerrainGenerator t, int seed)
        {
            if (TerrainGenerator.Hash(region.x, 77, region.y, seed) > 0.5f) return null;
            // a few candidate sites per region; the first good one wins
            int cx = 0, cz = 0, h = 0; bool ok = false;
            for (int k = 0; k < 4 && !ok; k++)
            {
                cx = region.x * Region + 44 + (int)(TerrainGenerator.Hash(region.x, 78 + k * 7, region.y, seed) * (Region - 88));
                cz = region.y * Region + 44 + (int)(TerrainGenerator.Hash(region.x, 79 + k * 7, region.y, seed) * (Region - 88));
                ok = GoodSite(t, cx, cz, out h);
            }
            if (!ok) return null;

            var v = new Village { Center = new Vector2Int(cx, cz), Ground = h };
            var rng = new System.Random(seed ^ (region.x * 73856093) ^ (region.y * 19349663));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector2 C = new(cx + 0.5f, cz + 0.5f);
            Vector2 Polar(float ang, float r) => C + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * r;
            float Face(Vector2 p) => Mathf.Atan2(C.x - p.x, C.y - p.y) * Mathf.Rad2Deg;   // door towards the centre

            // footprints already taken, so nothing is built inside something else
            var taken = new List<(Vector2 p, float r)>();
            bool Free(Vector2 p, float r)
            {
                foreach (var o in taken) if ((o.p - p).sqrMagnitude < (o.r + r) * (o.r + r)) return false;
                return true;
            }
            void Add(string path, Vector2 p, float yaw, float r, bool collide = true, bool fire = false)
            {
                v.Props.Add((path, p, yaw, 1f, collide, fire, r));
                if (r > 0) taken.Add((p, r));
            }
            // tries a few random spots from pick(); skips the prop if there is no room
            bool TryAdd(string path, System.Func<Vector2> pick, System.Func<Vector2, float> yaw, float r, out Vector2 at)
            {
                for (int k = 0; k < 10; k++)
                {
                    at = pick();
                    if (Free(at, r)) { Add(path, at, yaw(at), r); return true; }
                }
                at = default; return false;
            }

            var cc = new Vector2Int(cx, cz);
            void Block(Vector2Int p, int dy, BlockType b, byte meta = 0) => v.Blocks.Add((new Vector3Int(p.x, dy, p.y), b, meta));
            var lamps = new List<Vector2Int>();
            // a lamp post: two fence posts with a torch on top, like Minecraft village lamps — auls are lit at night
            void Lamp(Vector2Int p)
            {
                var at = new Vector2(p.x + 0.5f, p.y + 0.5f);
                if (!Free(at, 0.6f)) return;
                taken.Add((at, 0.6f));
                Block(p, 1, BlockType.Fence); Block(p, 2, BlockType.Fence); Block(p, 3, BlockType.Torch);
            }
            // a yurt of real blocks; its door faces the aul centre, with a lamp beside the door
            void YurtAt(Vector2 p, YurtStyle style)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                int door = Meta.FacingOf(new Vector3(C.x - p.x, 0, C.y - p.y));
                foreach (var c in Yurt.Build(style, door)) Block(cell + new Vector2Int(c.Off.x, c.Off.z), c.Off.y, c.Type, c.Meta);
                var d = Meta.Dir[door]; var side = Meta.Dir[(door + 1) % 4];
                int r = style == YurtStyle.Khan ? 4 : 3;
                lamps.Add(cell + new Vector2Int(d.x * (r + 2) + side.x * 2, d.z * (r + 2) + side.z * 2));
            }

            // centre: khan's yurt, open hearth with a kazan, dastarkhan, tug and well
            YurtAt(C + new Vector2(0, 10), YurtStyle.Khan); taken.Add((C + new Vector2(0, 10), 5.5f));
            Block(cc, 1, BlockType.Campfire); taken.Add((C, 1.2f));
            Block(cc + new Vector2Int(2, 0), 1, BlockType.Kazan, 3);
            Add("Household/SM_Dastarkhan", C + new Vector2(-2.6f, 1.2f), 0, 1.2f, false);
            Add("Buildings/SM_Tug", C + new Vector2(3.5f, 5f), 0, 0.6f);
            Add("Buildings/SM_Well", C + new Vector2(-6f, -3f), 0, 1.5f);

            // corral first (largest), on the open side of the ring of yurts
            float corralAng = R(55, 80);
            var corral = Polar(corralAng, 27);
            taken.Add((corral, 6.5f));
            // a square fence of real blocks (9x9) with a gate on the side facing the aul
            var cell = new Vector2Int(Mathf.FloorToInt(corral.x), Mathf.FloorToInt(corral.y));
            const int half = 4;
            var toC = (C - corral).normalized;
            var gate = Mathf.Abs(toC.x) > Mathf.Abs(toC.y) ? new Vector2Int(cell.x + (toC.x > 0 ? half : -half), cell.y)
                                                           : new Vector2Int(cell.x, cell.y + (toC.y > 0 ? half : -half));
            for (int dx = -half; dx <= half; dx++)
                for (int dz = -half; dz <= half; dz++)
                {
                    if (Mathf.Abs(dx) != half && Mathf.Abs(dz) != half) continue;
                    var p = cell + new Vector2Int(dx, dz);
                    v.Blocks.Add((new Vector3Int(p.x, 1, p.y), p == gate ? BlockType.FenceGate : BlockType.Fence, 0));
                }
            for (int k = 0; k < rng.Next(3, 6); k++) v.Livestock.Add(("Sheep", corral + new Vector2(R(-2.5f, 2.5f), R(-2.5f, 2.5f))));

            // ring of family yurts around the centre
            var styles = new[] { YurtStyle.White, YurtStyle.Blue, YurtStyle.Red, YurtStyle.White };
            int n = rng.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                float ang = 120 + i * (280f / n);
                for (int tries = 0; tries < 10; tries++)
                {
                    var p = Polar(ang + R(-10, 10), R(15, 21));
                    if (!Free(p, 4.5f)) continue;
                    taken.Add((p, 4.5f));
                    YurtAt(p, styles[rng.Next(styles.Length)]);
                    break;
                }
            }

            // craft quarter: the furnace and workbench are real, usable blocks
            if (TryAdd("Buildings/SM_Anvil", () => Polar(R(130, 170), 12), _ => R(0, 360), 0.8f, out var smith))
            {
                var sc = new Vector2Int(Mathf.FloorToInt(smith.x), Mathf.FloorToInt(smith.y));
                int faceC = Meta.FacingOf(new Vector3(C.x - smith.x, 0, C.y - smith.y));
                Block(sc + new Vector2Int(2, 0), 1, BlockType.Furnace, (byte)faceC);
                Block(sc + new Vector2Int(-2, 0), 1, BlockType.Workbench, (byte)faceC);
                v.People.Add(("Blacksmith", smith + new Vector2(0.8f, -1.2f)));
            }
            if (TryAdd("Buildings/SM_MarketStall", () => Polar(R(190, 230), 11), Face, 2f, out var market))
                v.People.Add(("Merchant", Vector2.MoveTowards(market, C, 2.2f)));
            for (int k = 0; k < 10; k++)
            {
                var p = Polar(R(250, 280), 7);
                if (!Free(p, 1f)) continue;
                taken.Add((p, 1f));
                Block(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)), 1, BlockType.Tandyr, (byte)Meta.FacingOf(new Vector3(C.x - p.x, 0, C.y - p.y)));
                break;
            }
            TryAdd("Buildings/SM_Loom", () => Polar(R(300, 330), 9), Face, 1.4f, out _);
            if (TryAdd("Household/SM_HitchingPost", () => Polar(R(90, 115), 12), p => Face(p) + 90, 1.2f, out var hitch))
                for (int k = 0; k < rng.Next(2, 4); k++) v.Livestock.Add(("Horse", hitch + new Vector2(R(-3, 3), R(1.5f, 3))));
            for (int k = 0; k < 3; k++) TryAdd("Buildings/SM_Haystack", () => Polar(R(0, 360), R(25, 32)), _ => R(0, 360), 1.5f, out _);
            if (rng.NextDouble() < 0.35) TryAdd("Buildings/SM_Mausoleum", () => Polar(R(0, 360), R(34, 38)), Face, 3.5f, out _);

            if (rng.NextDouble() < 0.5) v.Livestock.Add(("Camel", Polar(R(0, 360), 16)));
            v.Livestock.Add(("Cow", Polar(R(0, 360), 14)));

            // lamps: beside every yurt door, around the hearth, at the corral gate
            foreach (var l in lamps) Lamp(l);
            for (int k = 0; k < 4; k++) Lamp(cc + new Vector2Int(Mathf.RoundToInt(Mathf.Cos((45 + k * 90) * Mathf.Deg2Rad) * 6), Mathf.RoundToInt(Mathf.Sin((45 + k * 90) * Mathf.Deg2Rad) * 6)));
            Lamp(gate + new Vector2Int(Mathf.RoundToInt(toC.x * 4), Mathf.RoundToInt(toC.y * 4)) + (Mathf.Abs(toC.x) > Mathf.Abs(toC.y) ? new Vector2Int(0, 2) : new Vector2Int(2, 0)));

            v.People.Add(("Aksakal", C + new Vector2(-2, -1.5f)));
            v.People.Add(("Apa", Polar(R(0, 360), 6)));
            v.People.Add(("Shepherd", corral + new Vector2(5.8f, 1)));
            v.People.Add(("Batyr", Polar(R(0, 360), 17)));
            for (int k = 0; k < rng.Next(1, 4); k++) v.People.Add((rng.NextDouble() < 0.5 ? "Kyz" : "Bala", Polar(R(0, 360), R(3, 10))));
            return v;
        }
    }
}
