namespace Dala
{
    public enum BlockType : byte
    {
        Air = 0,
        Grass,
        DryGrass,
        Dirt,
        Stone,
        Snow,
        Sand,
        Water,
        Felt,
        Wood,
        // --- added with the 8x8 Blender atlas ---
        Ice, Gravel, Clay, Takyr, RedSand, RedStone,
        OreCoal, OreIron, OreCopper, OreGold,
        LeavesBroad, LeavesNeedle, LeavesBirch,
        Planks, Birch, FeltRed, FeltBlue, Tekemet, Shyrdak, Adobe, TileTurquoise, TileMajolica, GoldOrnament,
        Cobble, StoneBrick, Sandstone, Hay, Thatch, WoolWhite, WoolBrown, MossStone, Bedrock, Basalt, Salt,
        // --- trees from blocks: one log per tree family, seasonal / regional leaves ---
        LogSpruce, LogPine, LogSaxaul, LogTuranga,
        LeavesAutumn, LeavesBlossom, LeavesApple, LeavesSnow, LeavesTugai, LeavesSaxaul,
        Glass,
        // --- shaped blocks: meshed by the chunk with their own boxes (see BlockShapes) ---
        SlabCobble, SlabPlanks, SlabStoneBrick, SlabSandstone, SlabAdobe,
        StairsCobble, StairsPlanks, StairsStoneBrick, StairsSandstone, StairsAdobe,
        Ladder, Door, Trapdoor,
        Fence, FenceGate,
        SaplingBroad, SaplingBirch, SaplingSpruce, SaplingPine, SaplingApple, SaplingSaxaul, SaplingTugai, SaplingBlossom,
        MushroomRed, MushroomBrown, GlowShroom, CrystalTurquoise, CrystalAmethyst, CrystalRuby, Dripstone,
        LilyPad,
        // --- block entities: occupy a cell, rendered as Blender models (see BlockEntities) ---
        Workbench, Furnace, Sandyq, Campfire, Torch, Bed, Kazan, Kebeje, Tandyr,
    }

    /// <summary>How a block is drawn and collided.</summary>
    public enum Shape { None, Cube, Liquid, Cross, Flat, Slab, Stairs, Ladder, Door, Trapdoor, Fence, Gate, Entity }

    public static class BlockInfo
    {
        /// <summary>Every atlas page (Blender/tools/block_atlas.py) is 8x8 tiles of 16px; tile index = page * 64 + slot.</summary>
        public const int AtlasTiles = 8, TilesPerPage = 64;

        // Tile indices — must match NAMES / PAGES in Blender/tools/block_atlas.py
        public const int TileGrassTop = 0, TileGrassSide = 1, TileDirt = 2, TileStone = 3, TileSnow = 4, TileSand = 5, TileWater = 6,
            TileFelt = 7, TileWood = 8, TileDryTop = 9, TileDrySide = 10, TileSnowSide = 11, TileWoodTop = 12,
            TileIce = 13, TileGravel = 14, TileClay = 15, TileTakyr = 16, TileRedSand = 17, TileRedStone = 18,
            TileOreCoal = 19, TileOreIron = 20, TileOreCopper = 21, TileOreGold = 22,
            TileLeavesBroad = 23, TileLeavesNeedle = 24, TileLeavesBirch = 25,
            TilePlanks = 26, TileBirchSide = 27, TileBirchTop = 28, TileFeltRed = 29, TileFeltBlue = 30, TileTekemet = 31, TileShyrdak = 32,
            TileAdobe = 33, TileTurquoise = 34, TileMajolica = 35, TileGoldOrnament = 36, TileCobble = 37, TileStoneBrick = 38, TileSandstone = 39,
            TileHaySide = 40, TileHayTop = 41, TileThatch = 42, TileWoolWhite = 43, TileWoolBrown = 44, TileMossStone = 45,
            TileBedrock = 46, TileBasalt = 47, TileSalt = 48,
            TileSpruceSide = 49, TileSpruceTop = 50, TilePineSide = 51, TilePineTop = 52, TileSaxaulSide = 53, TileSaxaulTop = 54,
            TileLeavesAutumn = 55, TileLeavesBlossom = 56, TileLeavesApple = 57, TileLeavesSnow = 58,
            TileCraftTop = 59, TileCraftSide = 60, TileFurnaceFront = 61, TileFurnaceFrontLit = 62, TileSandstoneTop = 63,
            // page 1: trees
            TileTurangaSide = 64, TileTurangaTop = 65, TileLeavesTugai = 66, TileLeavesSaxaul = 67,
            TileSaplingBroad = 68, TileSaplingBirch = 69, TileSaplingSpruce = 70, TileSaplingPine = 71, TileSaplingApple = 72,
            TileSaplingSaxaul = 73, TileSaplingTugai = 74, TileSaplingBlossom = 75,
            TileMushroomRed = 76, TileMushroomBrown = 77, TileGlowShroom = 78, TileCrystalTurquoise = 79, TileCrystalAmethyst = 80, TileCrystalRuby = 81,
            TileDripBase = 82, TileDripMid = 83, TileDripTip = 84, TileLilyPad = 85,
            // page 2: building
            TileGlass = 128, TileLadder = 129, TileDoorBottom = 130, TileDoorTop = 131, TileTrapdoor = 132;

        public static Shape ShapeOf(BlockType b)
        {
            if (b == BlockType.Air) return Shape.None;
            if (b == BlockType.Water) return Shape.Liquid;
            if (b >= BlockType.Workbench) return Shape.Entity;
            if (b == BlockType.LilyPad) return Shape.Flat;
            if (b >= BlockType.SaplingBroad) return Shape.Cross;
            return b switch
            {
                >= BlockType.SlabCobble and <= BlockType.SlabAdobe => Shape.Slab,
                >= BlockType.StairsCobble and <= BlockType.StairsAdobe => Shape.Stairs,
                BlockType.Ladder => Shape.Ladder,
                BlockType.Door => Shape.Door,
                BlockType.Trapdoor => Shape.Trapdoor,
                BlockType.Fence => Shape.Fence,
                BlockType.FenceGate => Shape.Gate,
                _ => Shape.Cube,
            };
        }

        /// <summary>Blocks movement (feet, drops, mobs). Plants, ladders, torches and water do not.</summary>
        public static bool IsSolid(BlockType b)
        {
            var s = ShapeOf(b);
            return s != Shape.None && s != Shape.Liquid && s != Shape.Cross && s != Shape.Flat && s != Shape.Ladder && b != BlockType.Torch;
        }

        /// <summary>Blocks drawn as a model instead of cube faces.</summary>
        public static bool IsEntity(BlockType b) => b >= BlockType.Workbench;

        /// <summary>Full opaque cubes hide the faces of their neighbours. Leaves, ice, glass and shaped blocks are see-through.</summary>
        public static bool IsOpaque(BlockType b) =>
            ShapeOf(b) == Shape.Cube && b != BlockType.Ice && b != BlockType.Glass && !IsLeaves(b);

        /// <summary>Fences and gates: thin posts and rails that connect to neighbouring fences and solid blocks.</summary>
        public static bool IsFence(BlockType b) => b == BlockType.Fence || b == BlockType.FenceGate;

        public static bool IsLeaves(BlockType b) =>
            b == BlockType.LeavesBroad || b == BlockType.LeavesNeedle || b == BlockType.LeavesBirch ||
            (b >= BlockType.LeavesAutumn && b <= BlockType.LeavesSaxaul);

        public static bool IsLog(BlockType b) =>
            b == BlockType.Wood || b == BlockType.Birch || (b >= BlockType.LogSpruce && b <= BlockType.LogTuranga);

        public static bool IsSapling(BlockType b) => b >= BlockType.SaplingBroad && b <= BlockType.SaplingBlossom;

        /// <summary>Glowing blocks (cave crystals, glow mushrooms) get a small light when they are near the player.</summary>
        public static bool Emits(BlockType b, out UnityEngine.Color c)
        {
            c = b switch
            {
                BlockType.CrystalTurquoise => new UnityEngine.Color(0.3f, 0.9f, 0.9f),
                BlockType.CrystalAmethyst => new UnityEngine.Color(0.7f, 0.4f, 1f),
                BlockType.CrystalRuby => new UnityEngine.Color(1f, 0.3f, 0.3f),
                BlockType.GlowShroom => new UnityEngine.Color(0.3f, 0.7f, 1f),
                _ => default,
            };
            return c.a > 0;
        }

        /// <summary>Logs lie along an axis (meta 0 = up, 1 = east-west, 2 = north-south), like Minecraft's placed logs.</summary>
        public static int LogTile(BlockType b, int face, byte meta)
        {
            int axis = meta & 3;
            bool end = axis switch { 1 => face == 2 || face == 3, 2 => face == 4 || face == 5, _ => face == 0 || face == 1 };
            return Tile(b, end ? 0 : 2);
        }

        /// <summary>Cells a placed block may simply overwrite (air, water).</summary>
        public static bool IsReplaceable(BlockType b) => b == BlockType.Air || b == BlockType.Water;

        /// <summary>Full block a slab or stairs is made of (its texture).</summary>
        public static BlockType BaseOf(BlockType b) => b switch
        {
            BlockType.SlabCobble or BlockType.StairsCobble => BlockType.Cobble,
            BlockType.SlabPlanks or BlockType.StairsPlanks => BlockType.Planks,
            BlockType.SlabStoneBrick or BlockType.StairsStoneBrick => BlockType.StoneBrick,
            BlockType.SlabSandstone or BlockType.StairsSandstone => BlockType.Sandstone,
            BlockType.SlabAdobe or BlockType.StairsAdobe => BlockType.Adobe,
            _ => b,
        };

        // face: 0=+Y, 1=-Y, 2=+X, 3=-X, 4=+Z, 5=-Z
        public static int Tile(BlockType b, int face)
        {
            bool top = face == 0, bottom = face == 1, end = top || bottom;
            switch (b)
            {
                case BlockType.Grass: return top ? TileGrassTop : bottom ? TileDirt : TileGrassSide;
                case BlockType.DryGrass: return top ? TileDryTop : bottom ? TileDirt : TileDrySide;
                case BlockType.Dirt: return TileDirt;
                case BlockType.Stone: return TileStone;
                case BlockType.Snow: return top ? TileSnow : bottom ? TileDirt : TileSnowSide;
                case BlockType.Sand: return TileSand;
                case BlockType.Water: return TileWater;
                case BlockType.Felt: return TileFelt;
                case BlockType.Wood: return end ? TileWoodTop : TileWood;
                case BlockType.Ice: return TileIce;
                case BlockType.Gravel: return TileGravel;
                case BlockType.Clay: return TileClay;
                case BlockType.Takyr: return top ? TileTakyr : TileClay;
                case BlockType.RedSand: return TileRedSand;
                case BlockType.RedStone: return TileRedStone;
                case BlockType.OreCoal: return TileOreCoal;
                case BlockType.OreIron: return TileOreIron;
                case BlockType.OreCopper: return TileOreCopper;
                case BlockType.OreGold: return TileOreGold;
                case BlockType.LeavesBroad: return TileLeavesBroad;
                case BlockType.LeavesNeedle: return TileLeavesNeedle;
                case BlockType.LeavesBirch: return TileLeavesBirch;
                case BlockType.Planks: return TilePlanks;
                case BlockType.Birch: return end ? TileBirchTop : TileBirchSide;
                case BlockType.FeltRed: return TileFeltRed;
                case BlockType.FeltBlue: return TileFeltBlue;
                case BlockType.Tekemet: return TileTekemet;
                case BlockType.Shyrdak: return TileShyrdak;
                case BlockType.Adobe: return TileAdobe;
                case BlockType.TileTurquoise: return TileTurquoise;
                case BlockType.TileMajolica: return TileMajolica;
                case BlockType.GoldOrnament: return TileGoldOrnament;
                case BlockType.Cobble: return TileCobble;
                case BlockType.StoneBrick: return TileStoneBrick;
                case BlockType.Sandstone: return end ? TileSandstoneTop : TileSandstone;
                case BlockType.Hay: return end ? TileHayTop : TileHaySide;
                case BlockType.Thatch: return TileThatch;
                case BlockType.WoolWhite: return TileWoolWhite;
                case BlockType.WoolBrown: return TileWoolBrown;
                case BlockType.MossStone: return TileMossStone;
                case BlockType.Bedrock: return TileBedrock;
                case BlockType.Basalt: return TileBasalt;
                case BlockType.Salt: return TileSalt;
                case BlockType.LogSpruce: return end ? TileSpruceTop : TileSpruceSide;
                case BlockType.LogPine: return end ? TilePineTop : TilePineSide;
                case BlockType.LogSaxaul: return end ? TileSaxaulTop : TileSaxaulSide;
                case BlockType.LogTuranga: return end ? TileTurangaTop : TileTurangaSide;
                case BlockType.LeavesAutumn: return TileLeavesAutumn;
                case BlockType.LeavesBlossom: return TileLeavesBlossom;
                case BlockType.LeavesApple: return TileLeavesApple;
                case BlockType.LeavesSnow: return TileLeavesSnow;
                case BlockType.LeavesTugai: return TileLeavesTugai;
                case BlockType.LeavesSaxaul: return TileLeavesSaxaul;
                case BlockType.Glass: return TileGlass;
                case BlockType.Ladder: return TileLadder;
                case BlockType.Door: return TileDoorBottom;
                case BlockType.Trapdoor: return TileTrapdoor;
                case BlockType.Fence: case BlockType.FenceGate: return TilePlanks;
                case BlockType.SaplingBroad: return TileSaplingBroad;
                case BlockType.SaplingBirch: return TileSaplingBirch;
                case BlockType.SaplingSpruce: return TileSaplingSpruce;
                case BlockType.SaplingPine: return TileSaplingPine;
                case BlockType.SaplingApple: return TileSaplingApple;
                case BlockType.SaplingSaxaul: return TileSaplingSaxaul;
                case BlockType.SaplingTugai: return TileSaplingTugai;
                case BlockType.SaplingBlossom: return TileSaplingBlossom;
                case BlockType.MushroomRed: return TileMushroomRed;
                case BlockType.MushroomBrown: return TileMushroomBrown;
                case BlockType.GlowShroom: return TileGlowShroom;
                case BlockType.CrystalTurquoise: return TileCrystalTurquoise;
                case BlockType.CrystalAmethyst: return TileCrystalAmethyst;
                case BlockType.CrystalRuby: return TileCrystalRuby;
                case BlockType.Dripstone: return TileDripTip;
                case BlockType.LilyPad: return TileLilyPad;
                case BlockType.Workbench: return top ? TileCraftTop : TileCraftSide;
                case BlockType.Furnace: return TileFurnaceFront;
                default:
                    var basis = BaseOf(b);
                    return basis != b ? Tile(basis, face) : 0;
            }
        }
    }

    /// <summary>Meta bits stored per cell next to the block type (orientation and state).</summary>
    public static class Meta
    {
        // facing: 0 = +Z (north), 1 = +X (east), 2 = -Z (south), 3 = -X (west)
        public const byte FacingMask = 3, Upper = 4, Open = 8, Second = 16, Double = 32, HingeRight = 64;
        public static int Facing(byte m) => m & FacingMask;
        public static bool Has(byte m, byte flag) => (m & flag) != 0;

        public static readonly UnityEngine.Vector3Int[] Dir = { new(0, 0, 1), new(1, 0, 0), new(0, 0, -1), new(-1, 0, 0) };

        /// <summary>Horizontal facing closest to a direction (e.g. where the player looks).</summary>
        public static int FacingOf(UnityEngine.Vector3 d)
        {
            if (UnityEngine.Mathf.Abs(d.x) > UnityEngine.Mathf.Abs(d.z)) return d.x > 0 ? 1 : 3;
            return d.z > 0 ? 0 : 2;
        }

        public static int FacingOf(UnityEngine.Vector3Int n) => n.x > 0 ? 1 : n.x < 0 ? 3 : n.z > 0 ? 0 : 2;
    }
}
