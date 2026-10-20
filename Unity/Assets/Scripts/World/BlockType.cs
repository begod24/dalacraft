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
        // --- block entities: occupy a cell, rendered as Blender models (see BlockEntities) ---
        Workbench, Furnace, Sandyq, Campfire, Torch,
    }

    public static class BlockInfo
    {
        /// <summary>T_BlockAtlas.png from Blender (Blender/tools/block_atlas.py) is 8x8 tiles of 16px.</summary>
        public const int AtlasTiles = 8;

        // Tile indices — must match NAMES order in Blender/tools/block_atlas.py
        public const int TileGrassTop = 0, TileGrassSide = 1, TileDirt = 2, TileStone = 3, TileSnow = 4, TileSand = 5, TileWater = 6,
            TileFelt = 7, TileWood = 8, TileDryTop = 9, TileDrySide = 10, TileSnowSide = 11, TileWoodTop = 12,
            TileIce = 13, TileGravel = 14, TileClay = 15, TileTakyr = 16, TileRedSand = 17, TileRedStone = 18,
            TileOreCoal = 19, TileOreIron = 20, TileOreCopper = 21, TileOreGold = 22,
            TileLeavesBroad = 23, TileLeavesNeedle = 24, TileLeavesBirch = 25,
            TilePlanks = 26, TileBirchSide = 27, TileBirchTop = 28, TileFeltRed = 29, TileFeltBlue = 30, TileTekemet = 31, TileShyrdak = 32,
            TileAdobe = 33, TileTurquoise = 34, TileMajolica = 35, TileGoldOrnament = 36, TileCobble = 37, TileStoneBrick = 38, TileSandstone = 39,
            TileHaySide = 40, TileHayTop = 41, TileThatch = 42, TileWoolWhite = 43, TileWoolBrown = 44, TileMossStone = 45,
            TileBedrock = 46, TileBasalt = 47, TileSalt = 48;

        public static bool IsSolid(BlockType b) => b != BlockType.Air && b != BlockType.Water && b != BlockType.Torch;

        /// <summary>Blocks drawn as a model instead of cube faces.</summary>
        public static bool IsEntity(BlockType b) => b >= BlockType.Workbench;

        /// <summary>Opaque blocks hide the faces of their neighbours. Leaves and ice are see-through.</summary>
        public static bool IsOpaque(BlockType b) =>
            b != BlockType.Air && b != BlockType.Water && b != BlockType.Ice && !IsLeaves(b) && !IsEntity(b);

        public static bool IsLeaves(BlockType b) =>
            b == BlockType.LeavesBroad || b == BlockType.LeavesNeedle || b == BlockType.LeavesBirch;

        // face: 0=+Y, 1=-Y, 2=+X, 3=-X, 4=+Z, 5=-Z
        public static int Tile(BlockType b, int face)
        {
            bool top = face == 0, bottom = face == 1;
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
                case BlockType.Wood: return top || bottom ? TileWoodTop : TileWood;
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
                case BlockType.Birch: return top || bottom ? TileBirchTop : TileBirchSide;
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
                case BlockType.Sandstone: return TileSandstone;
                case BlockType.Hay: return top || bottom ? TileHayTop : TileHaySide;
                case BlockType.Thatch: return TileThatch;
                case BlockType.WoolWhite: return TileWoolWhite;
                case BlockType.WoolBrown: return TileWoolBrown;
                case BlockType.MossStone: return TileMossStone;
                case BlockType.Bedrock: return TileBedrock;
                case BlockType.Basalt: return TileBasalt;
                case BlockType.Salt: return TileSalt;
                default: return 0;
            }
        }
    }
}
