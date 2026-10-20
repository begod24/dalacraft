using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Non-block items. Icons/models come from Blender (Resources/Textures/Icons, Resources/Models/Items).</summary>
    public enum ItemType
    {
        None = 0,
        Pickaxe = 1000, Axe, Shovel, Kylysh, Spear, Kamcha, Bow, Arrow,
        Torsyk, KumysBowl, Baursak, Nan, Kurt, Apple, MeatRaw, MeatCooked, Kazy, FishRaw,
        IngotIron, IngotGold, IngotCopper, Coal, Wool, Leather, Bone, Stick,
    }

    public enum ToolKind { None, Pickaxe, Axe, Shovel, Weapon }

    public class ItemDef
    {
        public int Id;
        public string Name;
        public int MaxStack = 64;
        public int Food;            // hunger points restored (half-baursaks)
        public ToolKind Tool;
        public float Damage = 1f;
        public BlockType Block;     // for block items
        public bool IsBlock => Id < 1000;
        Sprite icon;
        public Sprite Icon => icon ??= Items.MakeIcon(this);
    }

    /// <summary>Item ids: values below 1000 are <see cref="BlockType"/>s, 1000+ are <see cref="ItemType"/>s.</summary>
    public struct Stack
    {
        public int Id, Count;
        public Stack(int id, int count) { Id = id; Count = count; }
        public static Stack Empty => new(0, 0);
        public bool IsEmpty => Id == 0 || Count <= 0;
        public ItemDef Def => Items.Get(Id);
        public static int Of(BlockType b) => (int)b;
        public static int Of(ItemType t) => (int)t;
    }

    public static class Items
    {
        static readonly Dictionary<int, ItemDef> defs = new();
        static readonly Dictionary<BlockType, string> blockNames = new()
        {
            [BlockType.Grass] = "Дёрн", [BlockType.DryGrass] = "Сухой дёрн", [BlockType.Dirt] = "Земля", [BlockType.Stone] = "Камень", [BlockType.Snow] = "Снег",
            [BlockType.Sand] = "Песок", [BlockType.Water] = "Вода", [BlockType.Felt] = "Киіз (белый войлок)", [BlockType.Wood] = "Бревно", [BlockType.Ice] = "Лёд",
            [BlockType.Gravel] = "Гравий", [BlockType.Clay] = "Глина", [BlockType.Takyr] = "Такыр", [BlockType.RedSand] = "Красный песок", [BlockType.RedStone] = "Чарынский камень",
            [BlockType.OreCoal] = "Угольная руда", [BlockType.OreIron] = "Железная руда", [BlockType.OreCopper] = "Медная руда", [BlockType.OreGold] = "Золотая руда",
            [BlockType.LeavesBroad] = "Листва", [BlockType.LeavesNeedle] = "Хвоя", [BlockType.LeavesBirch] = "Листва берёзы", [BlockType.Planks] = "Резные доски",
            [BlockType.Birch] = "Берёзовое бревно", [BlockType.FeltRed] = "Красный войлок", [BlockType.FeltBlue] = "Синий войлок", [BlockType.Tekemet] = "Текемет",
            [BlockType.Shyrdak] = "Шырдак", [BlockType.Adobe] = "Саман", [BlockType.TileTurquoise] = "Бирюзовый изразец", [BlockType.TileMajolica] = "Майолика",
            [BlockType.GoldOrnament] = "Золотой орнамент", [BlockType.Cobble] = "Булыжник", [BlockType.StoneBrick] = "Каменный кирпич", [BlockType.Sandstone] = "Песчаник",
            [BlockType.Hay] = "Сено", [BlockType.Thatch] = "Камышовая кровля", [BlockType.WoolWhite] = "Белая шерсть", [BlockType.WoolBrown] = "Бурая шерсть",
            [BlockType.MossStone] = "Мшистый камень", [BlockType.Bedrock] = "Коренная порода", [BlockType.Basalt] = "Базальт", [BlockType.Salt] = "Соль",
            [BlockType.Workbench] = "Верстак", [BlockType.Furnace] = "Печь", [BlockType.Sandyq] = "Сандык", [BlockType.Campfire] = "Костёр", [BlockType.Torch] = "Факел",
        };

        static Items()
        {
            foreach (BlockType b in System.Enum.GetValues(typeof(BlockType)))
            {
                if (b == BlockType.Air) continue;
                defs[(int)b] = new ItemDef { Id = (int)b, Block = b, Name = blockNames.TryGetValue(b, out var n) ? n : b.ToString() };
            }
            void I(ItemType t, string name, int stack = 64, int food = 0, ToolKind tool = ToolKind.None, float dmg = 1f) =>
                defs[(int)t] = new ItemDef { Id = (int)t, Name = name, MaxStack = stack, Food = food, Tool = tool, Damage = dmg };
            I(ItemType.Pickaxe, "Кирка", 1, tool: ToolKind.Pickaxe, dmg: 3); I(ItemType.Axe, "Балта (топор)", 1, tool: ToolKind.Axe, dmg: 5);
            I(ItemType.Shovel, "Күрек (лопата)", 1, tool: ToolKind.Shovel, dmg: 2); I(ItemType.Kylysh, "Қылыш (сабля)", 1, tool: ToolKind.Weapon, dmg: 7);
            I(ItemType.Spear, "Найза (копьё)", 1, tool: ToolKind.Weapon, dmg: 6); I(ItemType.Kamcha, "Қамшы (камча)", 1, tool: ToolKind.Weapon, dmg: 2);
            I(ItemType.Bow, "Садақ (лук)", 1, tool: ToolKind.Weapon, dmg: 2); I(ItemType.Arrow, "Стрела");
            I(ItemType.Torsyk, "Торсық с кумысом", 16, food: 6); I(ItemType.KumysBowl, "Кесе кумыса", 16, food: 4);
            I(ItemType.Baursak, "Бауырсак", 64, food: 5); I(ItemType.Nan, "Нан", 64, food: 5); I(ItemType.Kurt, "Құрт", 64, food: 2);
            I(ItemType.Apple, "Алма (яблоко)", 64, food: 4); I(ItemType.MeatRaw, "Сырое мясо", 64, food: 2); I(ItemType.MeatCooked, "Жареное мясо", 64, food: 8);
            I(ItemType.Kazy, "Қазы", 64, food: 9); I(ItemType.FishRaw, "Сазан", 64, food: 2);
            I(ItemType.IngotIron, "Железный слиток"); I(ItemType.IngotGold, "Золотой слиток"); I(ItemType.IngotCopper, "Медный слиток");
            I(ItemType.Coal, "Уголь"); I(ItemType.Wool, "Шерсть"); I(ItemType.Leather, "Кожа"); I(ItemType.Bone, "Кость"); I(ItemType.Stick, "Палка");
        }

        public static ItemDef Get(int id) => defs.TryGetValue(id, out var d) ? d : null;
        public static IEnumerable<ItemDef> All => defs.Values;

        static Texture2D atlas;
        public static Sprite MakeIcon(ItemDef d)
        {
            if (!d.IsBlock)
            {
                var tex = Resources.Load<Texture2D>("Textures/Icons/Icon_" + (ItemType)d.Id);
                if (tex == null) return null;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16);
            }
            // block icons: the side tile from the Blender atlas (entities use a representative tile)
            atlas ??= Resources.Load<Texture2D>("Textures/T_BlockAtlas");
            int tile = d.Block switch
            {
                BlockType.Workbench => BlockInfo.TilePlanks, BlockType.Furnace => BlockInfo.TileCobble, BlockType.Sandyq => BlockInfo.TileFeltRed,
                BlockType.Campfire => BlockInfo.TileWoodTop, BlockType.Torch => BlockInfo.TileWood,
                _ => BlockInfo.Tile(d.Block, 2),
            };
            const int ts = 16;
            var r = new Rect((tile % BlockInfo.AtlasTiles) * ts, (tile / BlockInfo.AtlasTiles) * ts, ts, ts);
            return Sprite.Create(atlas, r, new Vector2(0.5f, 0.5f), 16);
        }

        // ------------------------------------------------------------------ mining
        public static float Hardness(BlockType b) => b switch
        {
            BlockType.Bedrock => -1f, BlockType.Water => -1f,
            BlockType.LeavesBroad or BlockType.LeavesNeedle or BlockType.LeavesBirch or BlockType.Torch => 0.2f,
            BlockType.Snow or BlockType.Hay or BlockType.Thatch or BlockType.WoolWhite or BlockType.WoolBrown or BlockType.Felt or BlockType.FeltRed or BlockType.FeltBlue
                or BlockType.Tekemet or BlockType.Shyrdak => 0.6f,
            BlockType.Dirt or BlockType.Grass or BlockType.DryGrass or BlockType.Sand or BlockType.RedSand or BlockType.Gravel or BlockType.Clay or BlockType.Takyr => 0.7f,
            BlockType.Wood or BlockType.Birch or BlockType.Planks or BlockType.Workbench or BlockType.Sandyq or BlockType.Campfire => 1.6f,
            BlockType.Ice or BlockType.Salt => 0.9f,
            BlockType.OreGold or BlockType.OreIron or BlockType.OreCopper => 3.5f,
            _ => 2.5f,
        };

        public static ToolKind BestTool(BlockType b) => b switch
        {
            BlockType.Dirt or BlockType.Grass or BlockType.DryGrass or BlockType.Sand or BlockType.RedSand or BlockType.Gravel or BlockType.Clay or BlockType.Snow or BlockType.Takyr => ToolKind.Shovel,
            BlockType.Wood or BlockType.Birch or BlockType.Planks or BlockType.Workbench or BlockType.Sandyq => ToolKind.Axe,
            BlockType.LeavesBroad or BlockType.LeavesNeedle or BlockType.LeavesBirch or BlockType.Hay or BlockType.Felt or BlockType.FeltRed or BlockType.FeltBlue
                or BlockType.Tekemet or BlockType.Shyrdak or BlockType.WoolWhite or BlockType.WoolBrown or BlockType.Thatch or BlockType.Torch or BlockType.Campfire => ToolKind.None,
            _ => ToolKind.Pickaxe,
        };

        /// <summary>What you get for breaking a block (survival).</summary>
        public static IEnumerable<Stack> Drops(BlockType b, System.Random rng)
        {
            switch (b)
            {
                case BlockType.Grass: case BlockType.DryGrass:
                    yield return new Stack((int)BlockType.Dirt, 1);
                    if (b == BlockType.DryGrass && rng.NextDouble() < 0.3) yield return new Stack((int)BlockType.Hay, 1);
                    break;
                case BlockType.Stone: yield return new Stack((int)BlockType.Cobble, 1); break;
                case BlockType.OreCoal: yield return new Stack((int)ItemType.Coal, 1 + rng.Next(2)); break;
                case BlockType.LeavesBroad: case BlockType.LeavesBirch: case BlockType.LeavesNeedle:
                    if (rng.NextDouble() < 0.12) yield return new Stack((int)ItemType.Stick, 1);
                    if (b == BlockType.LeavesBroad && rng.NextDouble() < 0.08) yield return new Stack((int)ItemType.Apple, 1);
                    break;
                case BlockType.Ice: case BlockType.Water: case BlockType.Bedrock: break;
                case BlockType.Snow: yield return new Stack((int)BlockType.Snow, 1); break;
                default: yield return new Stack((int)b, 1); break;
            }
        }
    }
}
