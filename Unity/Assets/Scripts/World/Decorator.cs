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
            var taken = new bool[Chunk.Size, Chunk.Size];   // cells already holding a tree / rock / prop
            // block trees from the generator: their trunks (and the cells around them) are taken first
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    int t = c.Top[x, z];
                    if (t + 1 < Chunk.Height && BlockInfo.IsLog(c.Blocks[x, t + 1, z])) Take(taken, x, z, 1);
                }
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
                        // reeds only fringe the bank; lily pads float on calm water a little further out
                        if (depth <= 1 && r1 < 0.3f && NearShore(world, c, wx, wz, 2)) Foliage(c, lib, "Reed", x, top, z, new Vector3(pos.x, top + 1f, pos.z), r2);
                        else if (c.Blocks[x, top + 1, z] == BlockType.Water) LakeBed(c, lib, x, top, z, new Vector3(pos.x, surf, pos.z), depth, R(wx, wz, 9), r2, r3);
                        continue;
                    }

                    // keep aul grounds clear of trees and rocks
                    bool inAul = false;
                    foreach (var v in villages) if ((new Vector2(wx, wz) - v.Center).sqrMagnitude < 52 * 52) { inAul = true; break; }
                    if (inAul)
                    {
                        if (r1 < 0.35f && (ground == BlockType.Grass || ground == BlockType.DryGrass) && c.Blocks[x, top + 1, z] == BlockType.Air)
                            Foliage(c, lib, r2 < 0.8f ? "Grass_Short" : "Chamomile", x, top, z, pos, r3);
                        continue;
                    }

                    // ---- trees are real blocks now (Trees.Populate): keep props and plants off trunks and out of low leaves ----
                    if (top + 1 < Chunk.Height && c.Blocks[x, top + 1, z] != BlockType.Air) continue;

                    // ---- props & landmarks ----
                    if (Free(taken, x, z, 1) && Slope(world, c, wx, wz, 1, out float low) <= 1f
                        && PlaceProp(parent, biome, ground, new Vector3(pos.x, low, pos.z), r1, r2, r3, wx, wz, world))
                    {
                        Take(taken, x, z, 1);
                        continue;
                    }

                    // ---- plants (GPU instanced) ----
                    if (taken[x, z]) continue;
                    string plant = PlantFor(biome, ground, top, r1, r2, wx, wz, world);
                    if (plant != null) Foliage(c, lib, plant, x, top, z, pos, r3);
                }

            foreach (var v in villages) PlaceVillage(world, c, v);
            DecorateCaves(world, c);
            if (Session.InGame) SpawnFauna(world, c);
        }

        /// <summary>Lake and river beds: swaying weeds (instanced foliage, like grass) and algae-covered stones; weeds stay below the surface.</summary>
        static void LakeBed(Chunk c, FoliageLibrary lib, int x, int top, int z, Vector3 at, int depth, float r, float r2, float r3)
        {
            if (r < 0.10f && depth >= 3) Foliage(c, lib, "Seaweed_Tall", x, top, z, at, r2);
            else if (r < 0.22f && depth >= 2) Foliage(c, lib, r2 < 0.5f ? "Seaweed_Short" : "Eelgrass", x, top, z, at, r3);
            else if (r < 0.27f && depth >= 2) Foliage(c, lib, "Hornwort", x, top, z, at, r2);
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
                    Place(c.Go.transform, pr.path, new Vector3(pr.pos.x, LowestGround(w, pr.pos, pr.radius) - 0.05f, pr.pos.y), pr.yaw, pr.scale, pr.collide, pr.fire);
            if (!Session.InGame || !InChunk(c, v.Center)) return;
            foreach (var p in v.People) Villager.Spawn(p.npc, new Vector3(p.pos.x, GroundY(w, p.pos) + 0.5f, p.pos.y));
            foreach (var a in v.Livestock) Animal.Spawn(a.animal, new Vector3(a.pos.x, GroundY(w, a.pos) + 0.5f, a.pos.y), Random.Range(0f, 360f));
        }

        // ---------------------------------------------------------------- caves
        /// <summary>Cave crystals, glow mushrooms and dripstone are blocks now (Features) and glow through light levels; here: bones and treasure.</summary>
        static void DecorateCaves(World w, Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            var parent = c.Go.transform;
            for (int x = 1; x < Chunk.Size - 1; x++)
                for (int z = 1; z < Chunk.Size - 1; z++)
                {
                    int top = c.Top[x, z];
                    for (int y = 4; y < top - 4; y++)
                    {
                        var b = c.Blocks[x, y, z];
                        int wx = ox + x, wz = oz + z;
                        var at = new Vector3(wx + 0.5f, y, wz + 0.5f);
                        if (b != BlockType.Air || !BlockInfo.IsOpaque(c.Blocks[x, y - 1, z])) continue;
                        float r = TerrainGenerator.Hash(wx, y, wz, salt + 17);
                        if (r < 0.003f) Place(parent, "Dungeon/SM_Bones", at, r * 70000f, 1f, false);
                        else if (r < 0.0038f && y < 24) Treasure(parent, at, r);
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
            t.Legendary = r < 0.0032f;
            if (t.Legendary) Place(parent, "Dungeon/SM_AltynAdamCrown", at + new Vector3(-1.1f, 0, 0.3f), 0, 1f, false);
            CaveLight(chest.transform, new Color(1f, 0.8f, 0.4f), 5f);
        }

        // ---------------------------------------------------------------- ground helpers
        /// <summary>Top surface (block top, half-blocks included) of a column — from the chunk when it is inside, else from the generator.</summary>
        static float Surface(World w, Chunk c, int wx, int wz)
        {
            int lx = wx - c.Coord.x * Chunk.Size, lz = wz - c.Coord.y * Chunk.Size;
            if (lx >= 0 && lx < Chunk.Size && lz >= 0 && lz < Chunk.Size)
            {
                int t = c.Top[lx, lz];
                return t + (c.Half[lx, t, lz] ? 0.5f : 1f);
            }
            int h = w.Terrain.Height(wx, wz, out bool half);
            return h + (half ? 0.5f : 1f);
        }

        /// <summary>Height difference across a (2r+1)² footprint; <paramref name="low"/> is its lowest surface.</summary>
        static float Slope(World w, Chunk c, int wx, int wz, int r, out float low)
        {
            low = float.MaxValue; float high = float.MinValue;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    float s = Surface(w, c, wx + dx, wz + dz);
                    low = Mathf.Min(low, s); high = Mathf.Max(high, s);
                }
            return high - low;
        }

        /// <summary>Lowest surface under a round footprint, so wide models sink into slopes instead of hanging off them.</summary>
        static float LowestGround(World w, Vector2 p, float radius)
        {
            float low = GroundY(w, p);
            if (radius <= 0.01f) return low;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                low = Mathf.Min(low, GroundY(w, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.8f));
            }
            return low;
        }

        /// <summary>True if dry land (surface above the water line) is within <paramref name="r"/> blocks.</summary>
        static bool NearShore(World w, Chunk c, int wx, int wz, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                    if ((dx != 0 || dz != 0) && Surface(w, c, wx + dx, wz + dz) > TerrainGenerator.SeaLevel + 0.9f) return true;
            return false;
        }

        static bool Free(bool[,] taken, int x, int z, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int ax = x + dx, az = z + dz;
                    if (ax >= 0 && ax < Chunk.Size && az >= 0 && az < Chunk.Size && taken[ax, az]) return false;
                }
            return true;
        }

        static void Take(bool[,] taken, int x, int z, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int ax = x + dx, az = z + dz;
                    if (ax >= 0 && ax < Chunk.Size && az >= 0 && az < Chunk.Size) taken[ax, az] = true;
                }
        }

        static void Foliage(Chunk c, FoliageLibrary lib, string name, int x, int y, int z, Vector3 pos, float r)
        {
            int id = lib.Get(name);
            if (id < 0) return;
            // a cell the player changed (or picked the grass from) stays as they left it
            var w = World.Instance;
            if (w != null && w.Edits.ContainsKey(new Vector3Int(c.Coord.x * Chunk.Size + x, y + 1, c.Coord.y * Chunk.Size + z))) return;
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
            if (fire) go.AddComponent<FireVfx>().FitToModel = true;
            return go;
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
                    if (rp < 0.0013f) { Place(parent, "Environment/SM_Balbal", pos, yaw, 1f, true); return true; }
                    if (rp < 0.0016f) { Place(parent, "Environment/SM_Oba", pos, yaw, 1f, true); return true; }
                    break;
                case Biome.BetpakDala:
                    if (rp < 0.0052f) { Place(parent, "Environment/SM_RamSkull", pos, yaw, 1f, false); return true; }
                    break;
                case Biome.Taiga:
                    break;
                case Biome.BirchGrove:
                    break;
                case Biome.Snowy:
                    break;
                case Biome.Altai:
                case Biome.AppleForest:
                    if (rp < 0.0012f) { Place(parent, "Environment/SM_PetroglyphRock", pos, yaw, 1f, true); return true; }
                    break;
                case Biome.Balkhash:
                case Biome.Tugai:
                    break;
            }
            return false;
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
            [Biome.Tugai] = new[] { ("Duck", 2, 4, 2f), ("Kulan", 2, 3, 1f), ("Snake", 1, 1, 0.5f) },
            [Biome.Balkhash] = new[] { ("Duck", 2, 5, 3f) },
        };
        public const int MaxAnimals = 60;

        static void SpawnFauna(World world, Chunk c)
        {
            int ox = c.Coord.x * Chunk.Size, oz = c.Coord.y * Chunk.Size;
            if (Animal.All.Count >= MaxAnimals) return;
            FishSchool(c, ox, oz);
            if (R(ox, oz, 31) > 0.11f) return;
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

        /// <summary>A school of 3–6 fish in water at least 2 blocks deep (about every third lake chunk).</summary>
        static void FishSchool(Chunk c, int ox, int oz)
        {
            if (R(ox, oz, 41) > 0.35f) return;
            int lx = 2 + (int)(R(ox, oz, 42) * 12), lz = 2 + (int)(R(ox, oz, 43) * 12);
            int top = c.Top[lx, lz];
            if (TerrainGenerator.SeaLevel - top < 2 || c.Biomes[lx, lz] == Biome.Snowy) return;
            int n = 3 + (int)(R(ox, oz, 44) * 3.99f);
            for (int i = 0; i < n; i++)
            {
                float fx = ox + lx + 0.5f + (R(ox + i, oz, 45) - 0.5f) * 3f, fz = oz + lz + 0.5f + (R(ox, oz + i, 46) - 0.5f) * 3f;
                Animal.Spawn("Fish", new Vector3(fx, (top + 1 + TerrainGenerator.SeaLevel + 1) * 0.5f, fz), R(ox + i, oz + i, 47) * 360f);
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
