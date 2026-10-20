using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Instanced plant meshes from Blender (Resources/Models/Foliage/Foliage.fbx) + their wind-swaying material.</summary>
    public class FoliageLibrary
    {
        public readonly List<Mesh> Meshes = new();
        public readonly Material Material;
        readonly Dictionary<string, int> index = new();

        public FoliageLibrary()
        {
            foreach (var m in Resources.LoadAll<Mesh>("Models/Foliage/Foliage"))
            {
                index[m.name.Replace("SM_", "")] = Meshes.Count;
                Meshes.Add(m);
            }
            Material = new Material(Shader.Find("Dala/Foliage")) { name = "Foliage", enableInstancing = true };
            Material.SetTexture("_BaseMap", Resources.Load<Texture2D>("Textures/T_FoliageAtlas"));
        }

        public int Get(string name) => index.TryGetValue(name, out var i) ? i : -1;
    }

    /// <summary>Places plants, trees, rocks, landmarks, auls and animal herds on a freshly generated chunk.</summary>
    public static class Decorator
    {
        static readonly Dictionary<string, GameObject> prefabs = new();
        static int salt;

        static GameObject Prefab(string path)
        {
            if (!prefabs.TryGetValue(path, out var p)) prefabs[path] = p = Resources.Load<GameObject>("Models/" + path);
            return p;
        }

        static float R(int wx, int wz, int s) => TerrainGenerator.Hash(wx, s, wz, salt);

        // ---------------------------------------------------------------- tables
        static readonly (string name, float w)[] SteppeGrass =
            { ("Grass_Short", 30), ("Grass_Mid", 22), ("Grass_Tall", 10), ("Kovyl", 18), ("Wormwood", 6), ("Grass_Dry", 6) };
        static readonly (string name, float w)[] SteppeFlowers =
            { ("Tulip_Red", 30), ("Tulip_Yellow", 18), ("Poppy", 24), ("Flower_Blue", 12), ("Chamomile", 16) };
        static readonly (string name, float w)[] ForestFloor =
            { ("Grass_Short", 30), ("Grass_Mid", 20), ("Fern", 20), ("BerryBush", 6), ("Chamomile", 6), ("Flower_Blue", 5) };

        static string Pick((string name, float w)[] table, float r)
        {
            float sum = 0; foreach (var t in table) sum += t.w;
            float x = r * sum;
            foreach (var t in table) { if ((x -= t.w) <= 0) return t.name; }
            return table[^1].name;
        }

        public static void Decorate(World world, Chunk c)
        {
            salt = world.Seed;
            c.Decorated = true;
            var lib = world.Foliage;
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            var trees = new List<Vector2Int>();
            var parent = c.Go.transform;
            var villages = NearbyVillages(world, c);

            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    int wx = ox + x, wz = oz + z;
                    int top = c.Top[x, z];
                    var biome = c.Biomes[x, z];
                    var ground = c.Blocks[x, top, z];
                    bool half = c.Half[x, top, z];
                    float surf = top + (half ? 0.5f : 1f);
                    bool underwater = top < TerrainGenerator.SeaLevel;
                    float r1 = R(wx, wz, 1), r2 = R(wx, wz, 2), r3 = R(wx, wz, 3);
                    var pos = new Vector3(wx + 0.5f + (r2 - 0.5f) * 0.5f, surf, wz + 0.5f + (r3 - 0.5f) * 0.5f);

                    if (underwater)
                    {
                        // lily pads and reeds in shallow water
                        int depth = TerrainGenerator.SeaLevel - top;
                        if (biome == Biome.Snowy) continue;
                        if (depth <= 1 && r1 < 0.25f) Foliage(c, lib, "Reed", x, top, z, new Vector3(pos.x, top + 1f, pos.z), r2);
                        else if (depth <= 3 && r1 < 0.035f) Place(parent, "Environment/SM_LilyPad", new Vector3(pos.x, TerrainGenerator.SeaLevel + 0.86f, pos.z), r2 * 360f, 1f, false);
                        continue;
                    }

                    // keep aul grounds clear of trees and rocks
                    bool inAul = false;
                    foreach (var v in villages) if ((new Vector2(wx, wz) - v.Center).sqrMagnitude < 34 * 34) { inAul = true; break; }
                    if (inAul)
                    {
                        if (r1 < 0.35f) Foliage(c, lib, r2 < 0.8f ? "Grass_Short" : "Chamomile", x, top, z, pos, r3);
                        continue;
                    }

                    // ---- trees (spaced at least 3 blocks apart) ----
                    string tree = TreeFor(biome, ground, top, r1, r2, wx, wz, world);
                    if (tree != null && x > 0 && x < Chunk.Size - 1 && z > 0 && z < Chunk.Size - 1 && FarFrom(trees, x, z, biome == Biome.Taiga ? 3 : 4))
                    {
                        trees.Add(new Vector2Int(x, z));
                        Place(parent, "Trees/SM_" + tree, new Vector3(wx + 0.5f, surf - 0.05f, wz + 0.5f), r3 * 360f, 0.85f + r2 * 0.35f, true);
                        continue;
                    }

                    // ---- props & landmarks ----
                    if (PlaceProp(parent, biome, ground, pos, r1, r2, r3, wx, wz, world)) continue;

                    // ---- plants (GPU instanced) ----
                    string plant = PlantFor(biome, ground, top, r1, r2, wx, wz, world);
                    if (plant != null) Foliage(c, lib, plant, x, top, z, pos, r3);
                }

            foreach (var v in villages) PlaceVillage(world, c, v);
            DecorateCaves(world, c);
            if (Session.InGame) SpawnFauna(world, c);
        }

        // ---------------------------------------------------------------- auls
        static List<Village> NearbyVillages(World w, Chunk c)
        {
            var list = new List<Village>();
            if (Session.Current != null && !Session.Current.Villages) return list;
            int cx = c.Coord.x * Chunk.Size + 8, cz = c.Coord.y * Chunk.Size + 8;
            var reg = new Vector2Int(Mathf.FloorToInt(cx / (float)Village.Region), Mathf.FloorToInt(cz / (float)Village.Region));
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var v = Village.At(reg + new Vector2Int(dx, dz), w.Terrain, w.Seed);
                    if (v != null && (new Vector2(cx, cz) - v.Center).sqrMagnitude < 70 * 70) list.Add(v);
                }
            return list;
        }

        static bool InChunk(Chunk c, Vector2 p) =>
            p.x >= c.Coord.x * Chunk.Size && p.x < (c.Coord.x + 1) * Chunk.Size && p.y >= c.Coord.y * Chunk.Size && p.y < (c.Coord.y + 1) * Chunk.Size;

        static float GroundY(World w, Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.y);
            int h = w.Terrain.Height(x, z, out bool half);
            return h + (half ? 0.5f : 1f);
        }

        static void PlaceVillage(World w, Chunk c, Village v)
        {
            foreach (var pr in v.Props)
                if (InChunk(c, pr.pos))
                    Place(c.Go.transform, pr.path, new Vector3(pr.pos.x, GroundY(w, pr.pos) - 0.05f, pr.pos.y), pr.yaw, pr.scale, pr.collide, pr.fire);
            if (!Session.InGame || !InChunk(c, v.Center)) return;
            foreach (var p in v.People) Villager.Spawn(p.npc, new Vector3(p.pos.x, GroundY(w, p.pos) + 0.5f, p.pos.y));
            foreach (var a in v.Livestock) Animal.Spawn(a.animal, new Vector3(a.pos.x, GroundY(w, a.pos) + 0.5f, a.pos.y), Random.Range(0f, 360f));
        }

        // ---------------------------------------------------------------- caves
        static void DecorateCaves(World w, Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            var parent = c.Go.transform;
            int lights = 0;
            for (int x = 1; x < Chunk.Size - 1; x++)
                for (int z = 1; z < Chunk.Size - 1; z++)
                {
                    int top = c.Top[x, z];
                    for (int y = 4; y < top - 4; y++)
                    {
                        if (c.Blocks[x, y, z] != BlockType.Air) continue;
                        int wx = ox + x, wz = oz + z;
                        float r = TerrainGenerator.Hash(wx, y, wz, salt + 11);
                        bool floor = BlockInfo.IsOpaque(c.Blocks[x, y - 1, z]);
                        bool ceil = BlockInfo.IsOpaque(c.Blocks[x, y + 1, z]);
                        var at = new Vector3(wx + 0.5f, y, wz + 0.5f);
                        if (floor)
                        {
                            if (r < 0.012f) Place(parent, "Dungeon/SM_Stalagmite_" + (r < 0.006f ? 1 : 2), at, r * 30000f, 1f, true);
                            else if (r < 0.020f && y < 34)
                            {
                                string[] cr = { "Crystal_Turquoise", "Crystal_Amethyst", "Crystal_Ruby" };
                                var col = new[] { new Color(0.3f, 0.9f, 0.9f), new Color(0.7f, 0.4f, 1f), new Color(1f, 0.3f, 0.3f) };
                                int k = (int)(r * 100000) % 3;
                                var g = Place(parent, "Dungeon/SM_" + cr[k], at, r * 50000f, 1f, false);
                                if (g != null && lights < 3) { CaveLight(g.transform, col[k], 4f); lights++; }
                            }
                            else if (r < 0.028f)
                            {
                                var g = Place(parent, "Dungeon/SM_GlowMushrooms", at, r * 40000f, 1f, false);
                                if (g != null && lights < 3) { CaveLight(g.transform, new Color(0.3f, 0.7f, 1f), 3f); lights++; }
                            }
                            else if (r < 0.031f) Place(parent, "Dungeon/SM_Bones", at, r * 70000f, 1f, false);
                            else if (r < 0.0318f && y < 24) Treasure(parent, at, r);
                        }
                        else if (ceil && r > 0.985f)
                            Place(parent, "Dungeon/SM_Stalactite_" + (r > 0.993f ? 1 : 2), at + Vector3.up * 1f, r * 30000f, 1f, true);
                    }
                }
        }

        static void CaveLight(Transform t, Color c, float range)
        {
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(0, 0.4f, 0);
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 1.6f; l.shadows = LightShadows.None;
        }

        static void Treasure(Transform parent, Vector3 at, float r)
        {
            var chest = Place(parent, "Dungeon/SM_TreasureChest", at, r * 90000f, 1f, true);
            Place(parent, "Dungeon/SM_GoldPile", at + new Vector3(1.2f, 0, 0.4f), 0, 1f, false);
            if (chest == null) return;
            var t = chest.AddComponent<TreasureMarker>();
            t.Legendary = r < 0.0312f;
            if (t.Legendary) Place(parent, "Dungeon/SM_AltynAdamCrown", at + new Vector3(-1.1f, 0, 0.3f), 0, 1f, false);
            CaveLight(chest.transform, new Color(1f, 0.8f, 0.4f), 5f);
        }

        static bool FarFrom(List<Vector2Int> list, int x, int z, int d)
        {
            foreach (var p in list) if (Mathf.Abs(p.x - x) < d && Mathf.Abs(p.y - z) < d) return false;
            return true;
        }

        static void Foliage(Chunk c, FoliageLibrary lib, string name, int x, int y, int z, Vector3 pos, float r)
        {
            int id = lib.Get(name);
            if (id < 0) return;
            float s = 0.8f + r * 0.45f;
            c.AddFoliage(id, x, y + 1, z, Matrix4x4.TRS(pos, Quaternion.Euler(0, r * 360f, 0), new Vector3(s, s * (0.85f + r * 0.3f), s)));
        }

        static GameObject Place(Transform parent, string path, Vector3 pos, float yaw, float scale, bool collide, bool fire = false)
        {
            var prefab = Prefab(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0), parent);
            go.transform.localScale = Vector3.one * scale;
            if (collide)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            if (fire) go.AddComponent<FireVfx>();
            return go;
        }

        static string TreeFor(Biome b, BlockType ground, int top, float r1, float r2, int wx, int wz, World w)
        {
            if (ground != BlockType.Grass && ground != BlockType.DryGrass && ground != BlockType.Snow && ground != BlockType.Sand && ground != BlockType.MossStone) return null;
            float forest = w.Terrain.Forest(wx, wz);
            switch (b)
            {
                case Biome.Taiga:
                    if (r1 > 0.09f) return null;
                    return r2 < 0.35f ? "Spruce" : r2 < 0.6f ? "Fir" : r2 < 0.82f ? "Cedar" : "Pine";
                case Biome.Snowy:
                    if (top > 92 || r1 > 0.02f) return null;
                    return r2 < 0.5f ? "Spruce_Snow" : r2 < 0.8f ? "Fir_Snow" : "Pine_Snow";
                case Biome.BirchGrove:
                    if (r1 > 0.07f) return null;
                    return r2 < 0.6f ? "Birch" : r2 < 0.75f ? "Birch_Autumn" : r2 < 0.92f ? "Aspen" : "Rosehip";
                case Biome.Tugai:
                    if (r1 > 0.06f) return null;
                    return r2 < 0.4f ? "Turanga" : r2 < 0.7f ? "Willow" : "Tamarisk";
                case Biome.AppleForest:
                    if (r1 > 0.07f) return null;
                    return r2 < 0.5f ? "WildApple" : r2 < 0.72f ? "Apricot_Blossom" : r2 < 0.9f ? "Hawthorn" : "Aspen";
                case Biome.Altai:
                    if (r1 > 0.012f) return null;
                    return r2 < 0.5f ? "Pine" : "Birch";
                case Biome.BetpakDala:
                    if (ground != BlockType.Sand || r1 > 0.006f) return null;
                    return r2 < 0.8f ? null : "DeadTree";
                case Biome.Steppe:
                    // lone birches / rosehip bushes at the edge of groves
                    if (forest > 0.55f && r1 < 0.01f) return r2 < 0.6f ? "Birch" : "Rosehip";
                    return null;
            }
            return null;
        }

        static string PlantFor(Biome b, BlockType ground, int top, float r1, float r2, int wx, int wz, World w)
        {
            switch (b)
            {
                case Biome.Steppe:
                {
                    if (ground != BlockType.Grass && ground != BlockType.DryGrass) return null;
                    // flower meadows come in patches
                    float meadow = Mathf.PerlinNoise(wx * 0.06f + 40f, wz * 0.06f - 12f);
                    if (meadow > 0.62f && r1 < 0.45f) return Pick(SteppeFlowers, r2);
                    if (ground == BlockType.DryGrass) return r1 < 0.5f ? (r2 < 0.45f ? "Grass_Dry" : r2 < 0.8f ? "Kovyl" : "Wormwood") : null;
                    return r1 < 0.55f ? Pick(SteppeGrass, r2) : null;
                }
                case Biome.BirchGrove:
                case Biome.AppleForest:
                case Biome.Taiga:
                    if (ground != BlockType.Grass && ground != BlockType.MossStone) return null;
                    return r1 < 0.5f ? Pick(ForestFloor, r2) : null;
                case Biome.Altai:
                    if (ground != BlockType.Grass) return null;
                    return r1 < 0.45f ? (r2 < 0.12f ? Pick(SteppeFlowers, r2 * 8f) : Pick(SteppeGrass, r2)) : null;
                case Biome.Tugai:
                    if (ground == BlockType.Sand) return r1 < 0.3f ? "Reed" : null;
                    return r1 < 0.55f ? (r2 < 0.3f ? "Reed" : r2 < 0.6f ? "Grass_Tall" : "Grass_Mid") : null;
                case Biome.Balkhash:
                    return r1 < 0.12f ? (r2 < 0.5f ? "Reed" : "Grass_Dry") : null;
                case Biome.BetpakDala:
                    if (ground == BlockType.Takyr) return null;
                    return r1 < 0.06f ? (r2 < 0.6f ? "Camelthorn" : "Grass_Dry") : null;
                case Biome.Snowy:
                    return ground == BlockType.Snow && r1 < 0.05f ? "Grass_Snow" : null;
            }
            return null;
        }

        static bool PlaceProp(Transform parent, Biome b, BlockType ground, Vector3 pos, float r1, float r2, float r3, int wx, int wz, World w)
        {
            float rp = R(wx, wz, 7), yaw = r3 * 360f;
            switch (b)
            {
                case Biome.Steppe:
                    if (rp < 0.0006f && Flat(w, wx, wz, 4)) { Aul(parent, pos, yaw); return true; }
                    if (rp < 0.0013f) { Place(parent, "Environment/SM_Balbal", pos, yaw, 1f, true); return true; }
                    if (rp < 0.0016f) { Place(parent, "Environment/SM_Oba", pos, yaw, 1f, true); return true; }
                    if (rp < 0.0040f) { Place(parent, "Props/SM_Rock_" + (1 + (int)(r2 * 2.99f)), pos, yaw, 0.7f + r2 * 0.6f, true); return true; }
                    break;
                case Biome.BetpakDala:
                    if (rp < 0.004f) { Place(parent, "Props/SM_Saxaul", pos, yaw, 0.8f + r2 * 0.4f, true); return true; }
                    if (rp < 0.0052f) { Place(parent, "Environment/SM_RamSkull", pos, yaw, 1f, false); return true; }
                    if (rp < 0.0062f) { Place(parent, "Environment/SM_Tumbleweed", pos, yaw, 1f, false); return true; }
                    if (rp < 0.0068f && ground == BlockType.RedSand) { Place(parent, "Environment/SM_RedRock_Pillar_" + (r2 < 0.5f ? 1 : 2), pos - Vector3.up * 0.4f, yaw, 1f, true); return true; }
                    if (rp < 0.0075f && ground == BlockType.Takyr) { Place(parent, "Environment/SM_TakyrSlab", pos, yaw, 1f, false); return true; }
                    break;
                case Biome.Taiga:
                    if (rp < 0.004f) { Place(parent, "Environment/SM_" + (r2 < 0.4f ? "Log_Pine" : r2 < 0.7f ? "Stump" : "MossyRock"), pos, yaw, 1f, true); return true; }
                    if (rp < 0.008f) { Place(parent, "Environment/SM_" + (r2 < 0.5f ? "Mushrooms_FlyAgaric" : "Mushrooms_Boletus"), pos, yaw, 1f, false); return true; }
                    break;
                case Biome.BirchGrove:
                    if (rp < 0.003f) { Place(parent, "Environment/SM_" + (r2 < 0.5f ? "Log_Birch" : "Stump"), pos, yaw, 1f, true); return true; }
                    if (rp < 0.007f) { Place(parent, "Environment/SM_Mushrooms_Boletus", pos, yaw, 1f, false); return true; }
                    break;
                case Biome.Snowy:
                    if (rp < 0.004f) { Place(parent, "Environment/SM_" + (r2 < 0.4f ? "Snowdrift" : r2 < 0.75f ? "SnowRock" : "IceChunk"), pos, yaw, 1f, true); return true; }
                    break;
                case Biome.Altai:
                case Biome.AppleForest:
                    if (rp < 0.003f) { Place(parent, "Environment/SM_" + (r2 < 0.5f ? "MossyRock" : "PetroglyphRock"), pos, yaw, 1f, true); return true; }
                    break;
                case Biome.Balkhash:
                case Biome.Tugai:
                    if (rp < 0.006f) { Place(parent, "Environment/SM_" + (r2 < 0.5f ? "ShorePebbles" : "Driftwood"), pos, yaw, 1f, false); return true; }
                    break;
            }
            return false;
        }

        static bool Flat(World w, int wx, int wz, int r)
        {
            int h = w.Terrain.Height(wx, wz, out _);
            for (int dx = -r; dx <= r; dx += r)
                for (int dz = -r; dz <= r; dz += r)
                    if (Mathf.Abs(w.Terrain.Height(wx + dx, wz + dz, out _) - h) > 1) return false;
            return h > TerrainGenerator.SeaLevel + 1;
        }

        /// <summary>A small nomad camp: yurt, campfire with living fire, kazan, hitching post.</summary>
        static void Aul(Transform parent, Vector3 pos, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Place(parent, "Props/SM_Yurt", pos, yaw, 1f, true);
            var fire = Place(parent, "Household/SM_Campfire_Base", pos + rot * new Vector3(0, 0, 6f), 0, 1f, false, true);
            Place(parent, "Household/SM_Kazan", pos + rot * new Vector3(2.2f, 0, 6.5f), yaw, 1f, true);
            Place(parent, "Household/SM_HitchingPost", pos + rot * new Vector3(-5f, 0, 3f), yaw + 90f, 1f, true);
            Place(parent, "Household/SM_Sandyq", pos + rot * new Vector3(1.5f, 0, -1.2f), yaw, 1f, true);
        }

        // ---------------------------------------------------------------- fauna
        static readonly Dictionary<Biome, (string sp, int min, int max, float w)[]> Fauna = new()
        {
            [Biome.Steppe] = new[] { ("Horse", 2, 4, 3f), ("Sheep", 3, 6, 3f), ("Cow", 2, 3, 2f), ("Saiga", 3, 6, 2f), ("Kulan", 2, 3, 1f),
                                     ("Marmot", 1, 3, 2f), ("Hare", 1, 2, 2f), ("Wolf", 2, 3, 0.7f), ("Fox", 1, 1, 1f), ("Berkut", 1, 1, 0.6f) },
            [Biome.BetpakDala] = new[] { ("Camel", 2, 3, 3f), ("Lizard", 1, 2, 2f), ("Tortoise", 1, 2, 1.5f), ("Jerboa", 1, 3, 2f), ("Snake", 1, 1, 1f), ("Saiga", 2, 4, 1f) },
            [Biome.Altai] = new[] { ("Horse", 2, 4, 2f), ("Goat", 2, 4, 2f), ("Cow", 1, 3, 1f), ("Marmot", 1, 3, 2f), ("Berkut", 1, 1, 1f), ("Fox", 1, 1, 1f) },
            [Biome.AppleForest] = new[] { ("Goat", 1, 3, 1f), ("Hare", 1, 2, 2f), ("Fox", 1, 1, 1f), ("Berkut", 1, 1, 0.5f) },
            [Biome.Taiga] = new[] { ("Wolf", 2, 4, 2f), ("Fox", 1, 1, 1f), ("Hare", 1, 2, 2f) },
            [Biome.Snowy] = new[] { ("SnowLeopard", 1, 1, 1f), ("Goat", 1, 3, 1.5f) },
            [Biome.BirchGrove] = new[] { ("Hare", 1, 2, 2f), ("Fox", 1, 1, 1f), ("Cow", 1, 2, 1f), ("Horse", 1, 3, 1f) },
            [Biome.Tugai] = new[] { ("Duck", 2, 4, 2f), ("Fish", 2, 4, 2f), ("Kulan", 2, 3, 1f), ("Snake", 1, 1, 0.5f) },
            [Biome.Balkhash] = new[] { ("Duck", 2, 5, 3f), ("Fish", 2, 5, 3f) },
        };
        public const int MaxAnimals = 60;

        static void SpawnFauna(World world, Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            if (R(ox, oz, 31) > 0.11f || Animal.All.Count >= MaxAnimals) return;
            int lx = 2 + (int)(R(ox, oz, 32) * 12), lz = 2 + (int)(R(ox, oz, 33) * 12);
            var biome = c.Biomes[lx, lz];
            if (!Fauna.TryGetValue(biome, out var table)) return;
            float sum = 0; foreach (var t in table) sum += t.w;
            float pick = R(ox, oz, 34) * sum;
            var choice = table[0];
            foreach (var t in table) { if ((pick -= t.w) <= 0) { choice = t; break; } }
            int n = choice.min + (int)(R(ox, oz, 35) * (choice.max - choice.min + 0.999f));
            for (int i = 0; i < n; i++)
            {
                float ax = ox + lx + 0.5f + (R(ox + i, oz, 36) - 0.5f) * 6f, az = oz + lz + 0.5f + (R(ox, oz + i, 37) - 0.5f) * 6f;
                int top = c.Top[lx, lz];
                bool aquatic = choice.sp == "Duck" || choice.sp == "Fish";
                if (aquatic != (top < TerrainGenerator.SeaLevel)) return;
                float y = choice.sp == "Fish" ? TerrainGenerator.SeaLevel - 1f : top + 1.2f;
                Animal.Spawn(choice.sp, new Vector3(ax, y, az), R(ox + i, oz + i, 38) * 360f);
            }
        }

        /// <summary>Spawns a couple of rideable horses next to the player at the start.</summary>
        public static void StarterHorses(Vector3 near)
        {
            Animal.Spawn("Horse", near + new Vector3(4f, 1f, 3f), 200f);
            Animal.Spawn("Horse", near + new Vector3(-3f, 1f, 5f), 160f);
            Animal.Spawn("Camel", near + new Vector3(6f, 1f, -4f), 90f);
        }
    }
}
