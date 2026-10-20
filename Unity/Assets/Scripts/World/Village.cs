using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Deterministic aul (nomad village) layouts — one possible aul per 160×160 region on flat steppe/foothills.</summary>
    public class Village
    {
        public const int Region = 160;
        public Vector2Int Center;
        public readonly List<(string path, Vector2 pos, float yaw, float scale, bool collide, bool fire)> Props = new();
        public readonly List<(string npc, Vector2 pos)> People = new();
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

        static Village Generate(Vector2Int region, TerrainGenerator t, int seed)
        {
            if (TerrainGenerator.Hash(region.x, 77, region.y, seed) > 0.42f) return null;
            int cx = region.x * Region + 40 + (int)(TerrainGenerator.Hash(region.x, 78, region.y, seed) * (Region - 80));
            int cz = region.y * Region + 40 + (int)(TerrainGenerator.Hash(region.x, 79, region.y, seed) * (Region - 80));
            int h = t.Height(cx, cz, out _);
            var biome = t.GetBiome(cx, cz, h);
            if (biome != Biome.Steppe && biome != Biome.Altai && biome != Biome.BirchGrove) return null;
            if (h <= TerrainGenerator.SeaLevel + 1) return null;
            for (int dx = -18; dx <= 18; dx += 9)
                for (int dz = -18; dz <= 18; dz += 9)
                    if (Mathf.Abs(t.Height(cx + dx, cz + dz, out _) - h) > 2) return null;

            var v = new Village { Center = new Vector2Int(cx, cz) };
            var rng = new System.Random(seed ^ (region.x * 73856093) ^ (region.y * 19349663));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector2 C = new(cx + 0.5f, cz + 0.5f);
            Vector2 Polar(float ang, float r) => C + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * r;
            float Face(Vector2 p) => Mathf.Atan2(C.x - p.x, C.y - p.y) * Mathf.Rad2Deg;   // door towards the centre

            v.Props.Add(("Buildings/SM_Yurt_Khan", C + new Vector2(0, 9), 180f, 1f, true, false));
            v.Props.Add(("Household/SM_Campfire_Base", C, 0, 1f, false, true));
            v.Props.Add(("Household/SM_Kazan", C + new Vector2(1.6f, 0.4f), 0, 1f, true, false));
            v.Props.Add(("Household/SM_Dastarkhan", C + new Vector2(-2.6f, 1.2f), 0, 1f, false, false));
            v.Props.Add(("Buildings/SM_Tug", C + new Vector2(3.5f, 5f), 0, 1f, true, false));
            v.Props.Add(("Buildings/SM_Well", C + new Vector2(-6f, -3f), 0, 1f, true, false));
            string[] yurts = { "Buildings/SM_Yurt_White", "Buildings/SM_Yurt_Blue", "Buildings/SM_Yurt_Grey", "Props/SM_Yurt" };
            int n = rng.Next(3, 7);
            for (int i = 0; i < n; i++)
            {
                float ang = -40 + i * (260f / n) + R(-10, 10);
                var p = Polar(ang + 180, R(15, 21));
                v.Props.Add((yurts[rng.Next(yurts.Length)], p, Face(p), 1f, true, false));
            }
            // corral with sheep
            var corral = Polar(R(20, 60), 22);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f;
                var p = corral + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 5f;
                v.Props.Add((k == 0 ? "Buildings/SM_CorralGate" : "Buildings/SM_Fence", p, -a + 90, 1f, true, false));
            }
            for (int k = 0; k < rng.Next(3, 6); k++) v.Livestock.Add(("Sheep", corral + new Vector2(R(-2.5f, 2.5f), R(-2.5f, 2.5f))));
            // craft quarter
            var smith = Polar(R(130, 170), 12);
            v.Props.Add(("Buildings/SM_Anvil", smith, R(0, 360), 1f, true, false));
            v.Props.Add(("Buildings/SM_Furnace", smith + new Vector2(1.6f, 0.5f), Face(smith), 1f, true, false));
            v.Props.Add(("Buildings/SM_Workbench", smith + new Vector2(-1.4f, 0.8f), Face(smith), 1f, true, false));
            v.Props.Add(("Buildings/SM_MarketStall", Polar(R(190, 230), 11), Face(Polar(210, 11)), 1f, true, false));
            v.Props.Add(("Buildings/SM_Tandyr", Polar(R(250, 280), 7), 0, 1f, true, false));
            v.Props.Add(("Buildings/SM_Loom", Polar(R(300, 330), 9), Face(Polar(315, 9)), 1f, true, false));
            v.Props.Add(("Household/SM_HitchingPost", Polar(R(80, 110), 13), Face(Polar(95, 13)) + 90, 1f, true, false));
            for (int k = 0; k < 3; k++) v.Props.Add(("Buildings/SM_Haystack", Polar(R(0, 360), R(25, 30)), R(0, 360), 1f, true, false));
            if (rng.NextDouble() < 0.35) v.Props.Add(("Buildings/SM_Mausoleum", Polar(R(0, 360), 38), R(0, 360), 1f, true, false));
            if (rng.NextDouble() < 0.25) v.Props.Add(("Buildings/SM_CaravanseraiGate", Polar(R(0, 360), 30), R(0, 360), 1f, true, false));

            var hitch = Polar(95, 13);
            for (int k = 0; k < rng.Next(2, 4); k++) v.Livestock.Add(("Horse", hitch + new Vector2(R(-3, 3), R(1.5f, 3))));
            if (rng.NextDouble() < 0.5) v.Livestock.Add(("Camel", Polar(R(0, 360), 16)));
            v.Livestock.Add(("Cow", Polar(R(0, 360), 14)));

            v.People.Add(("Aksakal", C + new Vector2(-2, -1.5f)));
            v.People.Add(("Apa", Polar(R(0, 360), 6)));
            v.People.Add(("Blacksmith", smith + new Vector2(0.8f, -1.2f)));
            v.People.Add(("Merchant", Polar(210, 9)));
            v.People.Add(("Shepherd", corral + new Vector2(5.5f, 1)));
            v.People.Add(("Batyr", Polar(R(0, 360), 17)));
            for (int k = 0; k < rng.Next(1, 4); k++) v.People.Add((rng.NextDouble() < 0.5 ? "Kyz" : "Bala", Polar(R(0, 360), R(3, 10))));
            return v;
        }
    }
}
