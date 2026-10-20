using UnityEngine;

namespace Dala
{
    public enum Biome
    {
        Steppe,       // ковыльная степь с тюльпанами и маками
        Altai,        // травянистые предгорья
        Balkhash,     // песчаные берега озёр
        BetpakDala,   // пустыня: песок, такыры, саксаул
        Taiga,        // хвойная тайга Алтая
        Snowy,        // заснеженные вершины
        BirchGrove,   // берёзовые колки
        Tugai,        // тугайные леса вдоль рек
        AppleForest,  // яблоневые леса Алатау
    }

    /// <summary>Deterministic heightmap + biome generation from a seed.</summary>
    public class TerrainGenerator
    {
        public const int SeaLevel = 30;

        readonly Vector2 oHeight, oMount, oTemp, oWet, oDetail, oRiver, oForest, oFoot;
        readonly int seed;

        public int Seed => seed;

        public TerrainGenerator(int seed)
        {
            this.seed = seed;
            var r = new System.Random(seed);
            Vector2 Off() => new Vector2(r.Next(-10000, 10000), r.Next(-10000, 10000));
            oHeight = Off(); oMount = Off(); oTemp = Off(); oWet = Off(); oDetail = Off();
            oRiver = Off(); oForest = Off(); oFoot = Off();
        }

        static float Fbm(float x, float z, Vector2 o, int oct, float lac = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, freq = 1, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                sum += Mathf.PerlinNoise((x + o.x) * freq, (z + o.y) * freq) * amp;
                norm += amp; amp *= gain; freq *= lac;
            }
            return sum / norm; // 0..1
        }

        /// <summary>Cheap deterministic per-position hash in [0,1).</summary>
        public static float Hash(int x, int y, int z, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ salt * 2654435;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Smooth 3D value noise in [0,1].</summary>
        public float Noise3(float x, float y, float z, int salt)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float fx = x - xi, fy = y - yi, fz = z - zi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float H(int a, int b, int c) => Hash(xi + a, yi + b, zi + c, seed + salt);
            float x00 = Mathf.Lerp(H(0, 0, 0), H(1, 0, 0), fx), x10 = Mathf.Lerp(H(0, 1, 0), H(1, 1, 0), fx);
            float x01 = Mathf.Lerp(H(0, 0, 1), H(1, 0, 1), fx), x11 = Mathf.Lerp(H(0, 1, 1), H(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        /// <summary>Caves: winding "spaghetti" tunnels everywhere plus large caverns deep down.</summary>
        public bool IsCave(int x, int y, int z, int surface)
        {
            if (y < 4 || y > surface - 4) return false;
            if (surface < SeaLevel + 3 && y > surface - 9) return false;   // never open caves under lakes and rivers
            float a = Noise3(x * 0.045f, y * 0.075f, z * 0.045f, 101) + Noise3(x * 0.11f, y * 0.16f, z * 0.11f, 103) * 0.25f;
            float b = Noise3(x * 0.045f, y * 0.075f, z * 0.045f, 102);
            if (Mathf.Abs(a - 0.62f) < 0.035f && Mathf.Abs(b - 0.5f) < 0.09f) return true;
            if (y < 40 && Noise3(x * 0.022f, y * 0.04f, z * 0.022f, 104) > 0.74f) return true;
            return false;
        }

        public float Mountainness(int wx, int wz) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.72f, Fbm(wx * 0.0035f, wz * 0.0035f, oMount, 3)));

        public float Dryness(int wx, int wz) =>
            Mathf.InverseLerp(0.55f, 0.72f, Fbm(wx * 0.0028f, wz * 0.0028f, oWet, 2));

        /// <summary>1 at a river's centre line, falling to 0 at its banks (rivers follow a noise contour).</summary>
        public float Riverness(int wx, int wz)
        {
            float n = Mathf.Abs(Fbm(wx * 0.0022f, wz * 0.0022f, oRiver, 3) - 0.5f);
            const float width = 0.006f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 2.6f, width, n));
        }

        public float Forest(int wx, int wz) => Fbm(wx * 0.009f, wz * 0.009f, oForest, 3);

        /// <summary>Surface block Y for a column (<paramref name="half"/> is kept for slab support but the generator never sets it).</summary>
        public int Height(int wx, int wz, out bool half)
        {
            float mount = Mountainness(wx, wz);
            float h = RawHeight(wx, wz, mount);
            if (Villages) h = FlattenForVillage(wx, wz, h);
            return Snap(h, mount, out half);
        }

        /// <summary>Height before aul plateaus are levelled (used to choose aul sites without recursion).</summary>
        public int BaseHeight(int wx, int wz)
        {
            float mount = Mountainness(wx, wz);
            return Snap(RawHeight(wx, wz, mount), mount, out _);
        }

        /// <summary>When true, the ground under every aul is levelled to one plateau with soft ramps at its edge.</summary>
        public bool Villages;
        public const float AulFlatRadius = 40f, AulRamp = 12f;

        float FlattenForVillage(int wx, int wz, float h)
        {
            int rx = Mathf.FloorToInt(wx / (float)Village.Region), rz = Mathf.FloorToInt(wz / (float)Village.Region);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var v = Village.At(new Vector2Int(rx + dx, rz + dz), this, seed);
                    if (v == null) continue;
                    float d = Vector2.Distance(new Vector2(wx, wz), v.Center);
                    if (d >= AulFlatRadius + AulRamp) continue;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AulFlatRadius + AulRamp, AulFlatRadius, d));
                    h = Mathf.Lerp(h, v.Ground, k);
                }
            return h;
        }

        float RawHeight(int wx, int wz, float mount)
        {
            float rolling = Fbm(wx * 0.012f, wz * 0.012f, oHeight, 3);
            float h = 32f + (rolling - 0.5f) * 14f;                       // steppe: gentle hills
            float ridge = 1f - Mathf.Abs(Fbm(wx * 0.01f, wz * 0.01f, oDetail, 4) * 2f - 1f);
            h += mount * (ridge * ridge * 62f + 6f);                          // Altai peaks
            // Balkhash-like basin: carve low-lying lake areas
            float lake = Fbm(wx * 0.0045f, wz * 0.0045f, oTemp, 2);
            float basin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.30f, lake)) * (1f - mount);
            h -= basin * 10f;
            // Rivers: carve a channel below sea level so it fills with water; fade out in the mountains
            float river = Riverness(wx, wz) * Mathf.Clamp01(1f - mount * 2.5f);
            h = Mathf.Lerp(h, Mathf.Min(h, SeaLevel - 2.6f), river);
            return Mathf.Clamp(h, 2f, Chunk.Height - 4);
        }

        /// <summary>Whole blocks only, like Minecraft: no slab steps in the terrain (1-block steps are climbed with auto-jump).</summary>
        static int Snap(float h, float mount, out bool half)
        {
            half = false;
            return Mathf.RoundToInt(h);
        }

        public Biome GetBiome(int wx, int wz, int height)
        {
            float mount = Mountainness(wx, wz);
            if (mount > 0.35f)
            {
                if (height > 78) return Biome.Snowy;
                if (height > 56) return Biome.Taiga;
                return Fbm(wx * 0.006f, wz * 0.006f, oFoot, 2) > 0.5f ? Biome.AppleForest : Biome.Altai;
            }
            if (height < SeaLevel + 2) return Biome.Balkhash;
            if (Riverness(wx + 9, wz - 7) > 0.05f && Dryness(wx, wz) < 0.6f) return Biome.Tugai;
            if (Dryness(wx, wz) > 0.5f) return Biome.BetpakDala;
            if (Forest(wx, wz) > 0.6f) return Biome.BirchGrove;
            return Biome.Steppe;
        }

        public BlockType BlockAt(int wx, int y, int wz, int height, Biome biome)
        {
            if (y > height) return y <= SeaLevel ? BlockType.Water : BlockType.Air;
            if (y == 0) return BlockType.Bedrock;
            int depth = height - y;
            if (depth > 4)
            {
                // ores in stone
                float o = Hash(wx, y, wz, seed);
                if (y < 20 && o < 0.003f) return BlockType.OreGold;
                if (y < 40 && o < 0.011f) return BlockType.OreCopper;
                if (y < 48 && o < 0.023f) return BlockType.OreIron;
                if (y < 64 && o < 0.043f) return BlockType.OreCoal;
                if (y < 6) return BlockType.Basalt;
            }
            switch (biome)
            {
                case Biome.Snowy:
                    if (depth == 0) return BlockType.Snow;
                    return depth < 2 && Hash(wx, y, wz, 5) < 0.3f ? BlockType.Ice : BlockType.Stone;
                case Biome.Taiga:
                    if (depth == 0) return Hash(wx, 0, wz, 6) < 0.12f ? BlockType.MossStone : BlockType.Grass;
                    return depth < 3 ? BlockType.Dirt : BlockType.Stone;
                case Biome.Altai:
                case Biome.AppleForest:
                    if (depth == 0) return height < 64 ? BlockType.Grass : BlockType.Stone;
                    return depth < 3 ? BlockType.Dirt : BlockType.Stone;
                case Biome.Balkhash:
                    if (depth == 0 && Hash(wx, 1, wz, 7) < 0.15f) return BlockType.Gravel;
                    return depth < 3 ? BlockType.Sand : (depth < 5 ? BlockType.Clay : BlockType.Stone);
                case Biome.BetpakDala:
                {
                    float takyr = Mathf.PerlinNoise(wx * 0.04f + 300f, wz * 0.04f);
                    float red = Mathf.PerlinNoise(wx * 0.02f - 500f, wz * 0.02f);
                    if (depth == 0 && takyr > 0.68f) return BlockType.Takyr;
                    if (depth < 4) return red > 0.66f ? BlockType.RedSand : BlockType.Sand;
                    return depth < 7 ? BlockType.Sandstone : (red > 0.6f ? BlockType.RedStone : BlockType.Stone);
                }
                case Biome.Tugai:
                    if (depth == 0) return height <= SeaLevel + 1 ? BlockType.Sand : BlockType.Grass;
                    return depth < 4 ? BlockType.Dirt : BlockType.Stone;
                default: // Steppe, BirchGrove
                    if (depth == 0) return height <= SeaLevel + 1 ? BlockType.Sand
                        : (biome == Biome.Steppe && Mathf.PerlinNoise(wx * 0.05f + 99f, wz * 0.05f) > 0.55f ? BlockType.DryGrass : BlockType.Grass);
                    return depth < 4 ? BlockType.Dirt : BlockType.Stone;
            }
        }

        public void Fill(Chunk chunk)
        {
            for (int x = 0; x < Chunk.Size; x++)
                for (int z = 0; z < Chunk.Size; z++)
                {
                    int wx = chunk.Coord.x * Chunk.Size + x, wz = chunk.Coord.y * Chunk.Size + z;
                    int h = Height(wx, wz, out bool half);
                    var biome = GetBiome(wx, wz, h);
                    chunk.Biomes[x, z] = biome;
                    chunk.Top[x, z] = (byte)h;
                    int top = Mathf.Max(h, SeaLevel);
                    for (int y = 0; y <= top; y++)
                        chunk.Blocks[x, y, z] = IsCave(wx, y, wz, h) ? BlockType.Air : BlockAt(wx, y, wz, h, biome);
                    if (half) chunk.Half[x, h, z] = true;
                    // frozen lakes in the snow
                    if (biome == Biome.Snowy && h < SeaLevel) chunk.Blocks[x, SeaLevel, z] = BlockType.Ice;
                }
            Trees.Populate(this, chunk, seed);
            Features.Populate(this, chunk, seed);
            if (Villages) PlaceVillageBlocks(chunk);
        }

        /// <summary>True if the column lies within <paramref name="radius"/> of an aul centre.</summary>
        public bool InVillage(int wx, int wz, float radius)
        {
            if (!Villages) return false;
            int rx = Mathf.FloorToInt(wx / (float)Village.Region), rz = Mathf.FloorToInt(wz / (float)Village.Region);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var v = Village.At(new Vector2Int(rx + dx, rz + dz), this, seed);
                    if (v != null && (new Vector2(wx, wz) - v.Center).sqrMagnitude < radius * radius) return true;
                }
            return false;
        }

        /// <summary>Builds the auls' block structures (corral fences) that fall into this chunk, standing on the surface.</summary>
        void PlaceVillageBlocks(Chunk chunk)
        {
            int ox = chunk.Coord.x * Chunk.Size, oz = chunk.Coord.y * Chunk.Size;
            int rx = Mathf.FloorToInt((ox + 8) / (float)Village.Region), rz = Mathf.FloorToInt((oz + 8) / (float)Village.Region);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var v = Village.At(new Vector2Int(rx + dx, rz + dz), this, seed);
                    if (v == null) continue;
                    foreach (var (p, type, meta) in v.Blocks)
                    {
                        int lx = p.x - ox, lz = p.z - oz;
                        if (lx < 0 || lx >= Chunk.Size || lz < 0 || lz >= Chunk.Size) continue;
                        int y = v.Ground + p.y;
                        if (y <= 0 || y >= Chunk.Height) continue;
                        chunk.Blocks[lx, y, lz] = type; chunk.Metas[lx, y, lz] = meta;
                        // structures never float: fill any dip under their floor
                        if (p.y <= 1 && type != BlockType.Air)
                            for (int yy = y - 1; yy > 0 && !BlockInfo.IsSolid(chunk.Blocks[lx, yy, lz]); yy--) chunk.Blocks[lx, yy, lz] = BlockType.Dirt;
                    }
                }
        }
    }
}
