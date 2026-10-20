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
        // tool tiers (Pickaxe / Axe / Shovel / Kylysh above are the iron ones)
        PickaxeWood, PickaxeStone, PickaxeGold, AxeWood, AxeStone, AxeGold, ShovelWood, ShovelStone, ShovelGold,
        SwordWood, SwordStone, KylyshGold,
        // survival
        Flint, FlintSteel, Bucket, BucketWater, BucketMilk, Kese, Sorpa, Beshbarmak, Kuyrdak,
        String, RottenFlesh, SpiderEye, Ectoplasm, FireEssence,
    }

    public enum ToolKind { None, Pickaxe, Axe, Shovel, Weapon }

    /// <summary>Material tier of a tool, like Minecraft's wood → stone → iron; gold is the best here (Алтын).</summary>
    public enum Tier { None = 0, Wood = 1, Stone = 2, Iron = 3, Gold = 4 }

    public class ItemDef
    {
        public int Id;
        public string Name;
        public int MaxStack = 64;
        public int Food;            // hunger points restored (half-baursaks)
        public float Saturation;    // extra saturation (hidden hunger bar)
        public ToolKind Tool;
        public Tier Tier;
        public int Durability;      // uses before it breaks (0 = unbreakable / not a tool)
        public float Damage = 1f;
        public int Returns;         // item left after eating (an empty kese / bucket)
        public BlockType Block;     // for block items
        public bool IsBlock => Id < 1000;
        public bool Stackable => MaxStack > 1;
        Sprite icon;
        public Sprite Icon => icon ??= Items.MakeIcon(this);
    }

    /// <summary>Item ids: values below 1000 are <see cref="BlockType"/>s, 1000+ are <see cref="ItemType"/>s.</summary>
    public struct Stack
    {
        public int Id, Count;
        /// <summary>Durability used up (tools); 0 = new.</summary>
        public int Wear;
        public Stack(int id, int count) { Id = id; Count = count; Wear = 0; }
        public Stack(int id, int count, int wear) { Id = id; Count = count; Wear = wear; }
        public static Stack Empty => new(0, 0);
        public bool IsEmpty => Id == 0 || Count <= 0;
        public ItemDef Def => Items.Get(Id);
        /// <summary>0..1 durability left (1 for items without durability).</summary>
        public float Health => Def != null && Def.Durability > 0 ? 1f - Wear / (float)Def.Durability : 1f;
        public Stack WithCount(int n) => new(Id, n, Wear);
        public static int Of(BlockType b) => (int)b;
        public static int Of(ItemType t) => (int)t;
    }

    public static class Items
    {
        static readonly Dictionary<int, ItemDef> defs = new();
        static readonly Dictionary<BlockType, string> blockNames = new()
        {
            [BlockType.Grass] = "Дёрн", [BlockType.DryGrass] = "Сухой дёрн", [BlockType.Dirt] = "Земля", [BlockType.Stone] = "Камень", [BlockType.Snow] = "Снег",
            [BlockType.Sand] = "Песок", [BlockType.Water] = "Вода", [BlockType.Felt] = "Киіз (белый войлок)", [BlockType.Wood] = "Бревно (лиственное)", [BlockType.Ice] = "Лёд",
            [BlockType.Gravel] = "Гравий", [BlockType.Clay] = "Глина", [BlockType.Takyr] = "Такыр", [BlockType.RedSand] = "Красный песок", [BlockType.RedStone] = "Чарынский камень",
            [BlockType.OreCoal] = "Угольная руда", [BlockType.OreIron] = "Железная руда", [BlockType.OreCopper] = "Медная руда", [BlockType.OreGold] = "Золотая руда",
            [BlockType.LeavesBroad] = "Листва", [BlockType.LeavesNeedle] = "Хвоя", [BlockType.LeavesBirch] = "Листва берёзы", [BlockType.Planks] = "Резные доски",
            [BlockType.Birch] = "Берёзовое бревно", [BlockType.FeltRed] = "Красный войлок", [BlockType.FeltBlue] = "Синий войлок", [BlockType.Tekemet] = "Текемет",
            [BlockType.Shyrdak] = "Шырдак", [BlockType.Adobe] = "Саман", [BlockType.TileTurquoise] = "Бирюзовый изразец", [BlockType.TileMajolica] = "Майолика",
            [BlockType.GoldOrnament] = "Золотой орнамент", [BlockType.Cobble] = "Булыжник", [BlockType.StoneBrick] = "Каменный кирпич", [BlockType.Sandstone] = "Песчаник",
            [BlockType.Hay] = "Сено", [BlockType.Thatch] = "Камышовая кровля", [BlockType.WoolWhite] = "Белая шерсть", [BlockType.WoolBrown] = "Бурая шерсть",
            [BlockType.MossStone] = "Мшистый камень", [BlockType.Bedrock] = "Коренная порода", [BlockType.Basalt] = "Базальт", [BlockType.Salt] = "Соль",
            [BlockType.LogSpruce] = "Еловое бревно", [BlockType.LogPine] = "Сосновое бревно", [BlockType.LogSaxaul] = "Саксаул (бревно)", [BlockType.LogTuranga] = "Бревно туранги",
            [BlockType.LeavesAutumn] = "Осенняя листва", [BlockType.LeavesBlossom] = "Цветущий урюк", [BlockType.LeavesApple] = "Листва яблони", [BlockType.LeavesSnow] = "Заснеженная хвоя",
            [BlockType.LeavesTugai] = "Листва туранги", [BlockType.LeavesSaxaul] = "Ветви саксаула", [BlockType.Glass] = "Стекло",
            [BlockType.SlabCobble] = "Плита из булыжника", [BlockType.SlabPlanks] = "Плита из досок", [BlockType.SlabStoneBrick] = "Плита из кирпича",
            [BlockType.SlabSandstone] = "Плита из песчаника", [BlockType.SlabAdobe] = "Плита из самана",
            [BlockType.StairsCobble] = "Ступени из булыжника", [BlockType.StairsPlanks] = "Ступени из досок", [BlockType.StairsStoneBrick] = "Ступени из кирпича",
            [BlockType.StairsSandstone] = "Ступени из песчаника", [BlockType.StairsAdobe] = "Ступени из самана",
            [BlockType.Ladder] = "Лестница", [BlockType.Door] = "Резная дверь (есік)", [BlockType.Trapdoor] = "Люк",
            [BlockType.Fence] = "Забор", [BlockType.FenceGate] = "Калитка",
            [BlockType.SaplingBroad] = "Саженец осины", [BlockType.SaplingBirch] = "Саженец берёзы", [BlockType.SaplingSpruce] = "Саженец ели",
            [BlockType.SaplingPine] = "Саженец сосны", [BlockType.SaplingApple] = "Саженец яблони (алма)", [BlockType.SaplingSaxaul] = "Саженец саксаула",
            [BlockType.SaplingTugai] = "Саженец туранги", [BlockType.SaplingBlossom] = "Саженец урюка",
            [BlockType.MushroomRed] = "Мухомор", [BlockType.MushroomBrown] = "Боровик", [BlockType.GlowShroom] = "Светящийся гриб",
            [BlockType.CrystalTurquoise] = "Бирюзовый кристалл", [BlockType.CrystalAmethyst] = "Аметист", [BlockType.CrystalRuby] = "Рубин",
            [BlockType.Dripstone] = "Сталактит", [BlockType.LilyPad] = "Кувшинка",
            [BlockType.Workbench] = "Верстак", [BlockType.Furnace] = "Печь", [BlockType.Sandyq] = "Сандык", [BlockType.Campfire] = "Костёр", [BlockType.Torch] = "Факел",
            [BlockType.Bed] = "Постель-корпе", [BlockType.Kazan] = "Қазан на ошаке", [BlockType.Kebeje] = "Кебеже (сундук для еды)", [BlockType.Tandyr] = "Тандыр",
        };

        static Items()
        {
            foreach (BlockType b in System.Enum.GetValues(typeof(BlockType)))
            {
                if (b == BlockType.Air) continue;
                int stack = b == BlockType.Door || b == BlockType.Bed ? 16 : 64;
                defs[(int)b] = new ItemDef { Id = (int)b, Block = b, MaxStack = stack, Name = blockNames.TryGetValue(b, out var n) ? n : b.ToString() };
            }
            ItemDef I(ItemType t, string name, int stack = 64, int food = 0, ToolKind tool = ToolKind.None, float dmg = 1f) =>
                defs[(int)t] = new ItemDef { Id = (int)t, Name = name, MaxStack = stack, Food = food, Tool = tool, Damage = dmg };
            void ToolSet(ItemType t, string name, ToolKind kind, Tier tier, float dmg)
            {
                var d = I(t, name, 1, tool: kind, dmg: dmg);
                d.Tier = tier;
                d.Durability = tier switch { Tier.Wood => 60, Tier.Stone => 132, Tier.Iron => 250, _ => 520 };
            }
            ToolSet(ItemType.PickaxeWood, "Деревянная кирка", ToolKind.Pickaxe, Tier.Wood, 2); ToolSet(ItemType.PickaxeStone, "Каменная кирка", ToolKind.Pickaxe, Tier.Stone, 3);
            ToolSet(ItemType.Pickaxe, "Железная кирка", ToolKind.Pickaxe, Tier.Iron, 4); ToolSet(ItemType.PickaxeGold, "Золотая кирка (алтын)", ToolKind.Pickaxe, Tier.Gold, 5);
            ToolSet(ItemType.AxeWood, "Деревянный топор", ToolKind.Axe, Tier.Wood, 4); ToolSet(ItemType.AxeStone, "Каменный топор (балта)", ToolKind.Axe, Tier.Stone, 5);
            ToolSet(ItemType.Axe, "Железный топор (балта)", ToolKind.Axe, Tier.Iron, 6); ToolSet(ItemType.AxeGold, "Золотой топор", ToolKind.Axe, Tier.Gold, 7);
            ToolSet(ItemType.ShovelWood, "Деревянная лопата", ToolKind.Shovel, Tier.Wood, 1.5f); ToolSet(ItemType.ShovelStone, "Каменная лопата (күрек)", ToolKind.Shovel, Tier.Stone, 2.5f);
            ToolSet(ItemType.Shovel, "Железная лопата (күрек)", ToolKind.Shovel, Tier.Iron, 3.5f); ToolSet(ItemType.ShovelGold, "Золотая лопата", ToolKind.Shovel, Tier.Gold, 4.5f);
            ToolSet(ItemType.SwordWood, "Деревянный меч", ToolKind.Weapon, Tier.Wood, 4); ToolSet(ItemType.SwordStone, "Каменный меч", ToolKind.Weapon, Tier.Stone, 5);
            ToolSet(ItemType.Kylysh, "Қылыш (железная сабля)", ToolKind.Weapon, Tier.Iron, 7); ToolSet(ItemType.KylyshGold, "Алтын қылыш (золотая сабля)", ToolKind.Weapon, Tier.Gold, 8);
            ToolSet(ItemType.Spear, "Найза (копьё)", ToolKind.Weapon, Tier.Iron, 6);
            ToolSet(ItemType.Kamcha, "Қамшы (камча)", ToolKind.Weapon, Tier.Wood, 2);
            ToolSet(ItemType.Bow, "Садақ (лук)", ToolKind.Weapon, Tier.Wood, 2);
            I(ItemType.Arrow, "Стрела");
            I(ItemType.Torsyk, "Торсық с кумысом", 16, food: 6); I(ItemType.KumysBowl, "Кесе кумыса", 16, food: 4).Returns = (int)ItemType.Kese;
            I(ItemType.Baursak, "Бауырсак", 64, food: 5); I(ItemType.Nan, "Нан", 64, food: 5); I(ItemType.Kurt, "Құрт", 64, food: 2);
            I(ItemType.Apple, "Алма (яблоко)", 64, food: 4); I(ItemType.MeatRaw, "Сырое мясо", 64, food: 2); I(ItemType.MeatCooked, "Жареное мясо", 64, food: 8).Saturation = 6;
            I(ItemType.Kazy, "Қазы", 64, food: 9).Saturation = 8; I(ItemType.FishRaw, "Сазан", 64, food: 2);
            I(ItemType.IngotIron, "Железный слиток"); I(ItemType.IngotGold, "Золотой слиток"); I(ItemType.IngotCopper, "Медный слиток");
            I(ItemType.Coal, "Уголь"); I(ItemType.Wool, "Шерсть"); I(ItemType.Leather, "Кожа"); I(ItemType.Bone, "Кость"); I(ItemType.Stick, "Палка");
            I(ItemType.Flint, "Кремень (шақпақ тас)");
            var fs = I(ItemType.FlintSteel, "Огниво (шақпақ)", 1); fs.Durability = 64;
            I(ItemType.Bucket, "Шелек (ведро)", 16); I(ItemType.BucketWater, "Шелек с водой", 1); I(ItemType.BucketMilk, "Шелек молока (сүт)", 1, food: 3).Returns = (int)ItemType.Bucket;
            I(ItemType.Kese, "Кесе (пустая чаша)");
            var sorpa = I(ItemType.Sorpa, "Сорпа (бульон)", 16, food: 8); sorpa.Saturation = 8; sorpa.Returns = (int)ItemType.Kese;
            var besh = I(ItemType.Beshbarmak, "Бешбармак", 16, food: 14); besh.Saturation = 14; besh.Returns = (int)ItemType.Kese;
            var kuyr = I(ItemType.Kuyrdak, "Қуырдақ", 16, food: 10); kuyr.Saturation = 10; kuyr.Returns = (int)ItemType.Kese;
            I(ItemType.String, "Жіп (паутинная нить)"); I(ItemType.RottenFlesh, "Гнилое мясо", 64, food: 3); I(ItemType.SpiderEye, "Глаз қарақұрта", 64, food: 2);
            I(ItemType.Ectoplasm, "Рух тозаңы (пыль духа)"); I(ItemType.FireEssence, "Шайтан оты (огонь шайтана)");
        }

        public static ItemDef Get(int id) => defs.TryGetValue(id, out var d) ? d : null;
        public static IEnumerable<ItemDef> All => defs.Values;

        public static Sprite MakeIcon(ItemDef d)
        {
            if (!d.IsBlock)
            {
                var tex = Resources.Load<Texture2D>("Textures/Icons/Icon_" + (ItemType)d.Id);
                if (tex == null) return null;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16);
            }
            // block icons: isometric cube / model renders from Blender (Blender/tools/icons.py)
            var iso = Resources.Load<Texture2D>("Textures/Icons/Block_" + d.Block);
            if (iso != null) return Sprite.Create(iso, new Rect(0, 0, iso.width, iso.height), new Vector2(0.5f, 0.5f), 16);
            // fallback: the side tile from its atlas page
            int tile = BlockInfo.Tile(d.Block, 2);
            var page = BlockAtlas.PageOf(tile);
            if (page == null) return null;
            const int ts = 16;
            int slot = tile % BlockInfo.TilesPerPage;
            var r = new Rect((slot % BlockInfo.AtlasTiles) * ts, (slot / BlockInfo.AtlasTiles) * ts, ts, ts);
            return Sprite.Create(page, r, new Vector2(0.5f, 0.5f), 16);
        }

        // ------------------------------------------------------------------ mining (Minecraft rules)
        /// <summary>Seconds-scale hardness like Minecraft's (stone 1.5, dirt 0.5, logs 2, ores 3). -1 = unbreakable.</summary>
        public static float Hardness(BlockType b)
        {
            if (b == BlockType.Bedrock || b == BlockType.Water || b == BlockType.Air) return -1f;
            if (BlockInfo.IsSapling(b) || b == BlockType.Torch || b == BlockType.LilyPad || b == BlockType.MushroomRed || b == BlockType.MushroomBrown || b == BlockType.GlowShroom) return 0f;
            if (BlockInfo.IsLeaves(b)) return 0.2f;
            if (BlockInfo.IsLog(b)) return 2f;
            return b switch
            {
                BlockType.Snow => 0.2f,
                BlockType.Glass or BlockType.Ice => 0.4f,
                BlockType.Ladder or BlockType.Bed => 0.4f,
                BlockType.Dirt or BlockType.Sand or BlockType.RedSand or BlockType.Hay or BlockType.Thatch => 0.5f,
                BlockType.Grass or BlockType.DryGrass or BlockType.Gravel or BlockType.Clay or BlockType.Takyr or BlockType.Salt => 0.6f,
                BlockType.Felt or BlockType.FeltRed or BlockType.FeltBlue or BlockType.Tekemet or BlockType.Shyrdak or BlockType.WoolWhite or BlockType.WoolBrown => 0.8f,
                BlockType.Sandstone or BlockType.SlabSandstone or BlockType.StairsSandstone => 0.8f,
                BlockType.Adobe or BlockType.SlabAdobe or BlockType.StairsAdobe or BlockType.Basalt => 1.25f,
                BlockType.TileTurquoise or BlockType.TileMajolica => 1.4f,
                BlockType.Stone or BlockType.StoneBrick or BlockType.SlabStoneBrick or BlockType.StairsStoneBrick or BlockType.MossStone or BlockType.RedStone => 1.5f,
                BlockType.Cobble or BlockType.SlabCobble or BlockType.StairsCobble => 2f,
                BlockType.Planks or BlockType.SlabPlanks or BlockType.StairsPlanks or BlockType.Fence or BlockType.FenceGate or BlockType.Campfire => 2f,
                BlockType.Workbench or BlockType.Sandyq or BlockType.Kebeje => 2.5f,
                BlockType.Door or BlockType.Trapdoor => 3f,
                BlockType.OreCoal or BlockType.OreIron or BlockType.OreCopper or BlockType.OreGold or BlockType.GoldOrnament => 3f,
                BlockType.Furnace or BlockType.Tandyr => 3.5f,
                BlockType.Kazan => 4f,
                _ => 1.5f,
            };
        }

        public static ToolKind BestTool(BlockType b)
        {
            if (BlockInfo.IsLog(b)) return ToolKind.Axe;
            if (BlockInfo.IsLeaves(b) || BlockInfo.IsSapling(b) || BlockInfo.ShapeOf(b) == Shape.Flat || b == BlockType.MushroomRed || b == BlockType.MushroomBrown || b == BlockType.GlowShroom) return ToolKind.None;
            return b switch
            {
                BlockType.Dirt or BlockType.Grass or BlockType.DryGrass or BlockType.Sand or BlockType.RedSand or BlockType.Gravel or BlockType.Clay
                    or BlockType.Snow or BlockType.Takyr or BlockType.Salt => ToolKind.Shovel,
                BlockType.Planks or BlockType.SlabPlanks or BlockType.StairsPlanks or BlockType.Workbench or BlockType.Sandyq or BlockType.Kebeje
                    or BlockType.Fence or BlockType.FenceGate or BlockType.Ladder or BlockType.Door or BlockType.Trapdoor or BlockType.Campfire => ToolKind.Axe,
                BlockType.Hay or BlockType.Felt or BlockType.FeltRed or BlockType.FeltBlue or BlockType.Tekemet or BlockType.Shyrdak or BlockType.WoolWhite
                    or BlockType.WoolBrown or BlockType.Thatch or BlockType.Torch or BlockType.Bed or BlockType.Glass => ToolKind.None,
                _ => ToolKind.Pickaxe,
            };
        }

        /// <summary>Minimum tool tier needed to get a drop (0 = by hand). Stone-like blocks need any pickaxe.</summary>
        public static Tier HarvestTier(BlockType b) => b switch
        {
            BlockType.OreGold or BlockType.GoldOrnament => Tier.Iron,
            BlockType.OreIron or BlockType.OreCopper or BlockType.TileMajolica => Tier.Stone,
            _ => BestTool(b) == ToolKind.Pickaxe ? Tier.Wood : Tier.None,
        };

        public static bool CanHarvest(BlockType b, ItemDef tool)
        {
            var need = HarvestTier(b);
            if (need == Tier.None) return true;
            return tool != null && tool.Tool == BestTool(b) && tool.Tier >= need;
        }

        static float TierSpeed(Tier t) => t switch { Tier.Wood => 2f, Tier.Stone => 4f, Tier.Iron => 6f, Tier.Gold => 9f, _ => 1f };

        /// <summary>Seconds to break <paramref name="b"/> with <paramref name="tool"/> (null = hand), Minecraft's formula.</summary>
        public static float BreakTime(BlockType b, ItemDef tool, bool inWater)
        {
            float hard = Hardness(b);
            if (hard < 0) return float.PositiveInfinity;
            if (hard == 0) return 0.05f;
            float speed = 1f;
            if (tool != null && tool.Tool != ToolKind.None && tool.Tool == BestTool(b)) speed = TierSpeed(tool.Tier);
            if (tool != null && tool.Tool == ToolKind.Weapon && BlockInfo.IsLeaves(b)) speed = 1.5f;
            float t = hard * (CanHarvest(b, tool) ? 1.5f : 5f) / speed;
            if (inWater) t *= 5f;
            return t;
        }

        static readonly Dictionary<BlockType, BlockType> saplingOf = new()
        {
            [BlockType.LeavesBroad] = BlockType.SaplingBroad, [BlockType.LeavesAutumn] = BlockType.SaplingBroad, [BlockType.LeavesBirch] = BlockType.SaplingBirch,
            [BlockType.LeavesNeedle] = BlockType.SaplingSpruce, [BlockType.LeavesSnow] = BlockType.SaplingSpruce, [BlockType.LeavesApple] = BlockType.SaplingApple,
            [BlockType.LeavesBlossom] = BlockType.SaplingBlossom, [BlockType.LeavesTugai] = BlockType.SaplingTugai, [BlockType.LeavesSaxaul] = BlockType.SaplingSaxaul,
        };

        /// <summary>What you get for breaking a block (survival). Nothing if the tool can't harvest it.</summary>
        public static IEnumerable<Stack> Drops(BlockType b, byte meta, ItemDef tool, System.Random rng)
        {
            if (!CanHarvest(b, tool)) yield break;
            if (BlockInfo.IsLeaves(b))
            {
                foreach (var s in LeafDrops(b, rng, 1f)) yield return s;
                yield break;
            }
            switch (b)
            {
                case BlockType.Grass: case BlockType.DryGrass:
                    yield return new Stack((int)BlockType.Dirt, 1);
                    if (b == BlockType.DryGrass && rng.NextDouble() < 0.3) yield return new Stack((int)BlockType.Hay, 1);
                    break;
                case BlockType.Stone: yield return new Stack((int)BlockType.Cobble, 1); break;
                case BlockType.OreCoal: yield return new Stack((int)ItemType.Coal, 1 + rng.Next(2)); break;
                case BlockType.Gravel:
                    yield return rng.NextDouble() < 0.12 ? new Stack((int)ItemType.Flint, 1) : new Stack((int)BlockType.Gravel, 1); break;
                case BlockType.Ice: case BlockType.Water: case BlockType.Bedrock: case BlockType.Glass: break;
                case BlockType.Door: if (!Meta.Has(meta, Meta.Upper)) yield return new Stack((int)b, 1); break;
                case BlockType.Bed: if (!Meta.Has(meta, Meta.Second)) yield return new Stack((int)b, 1); break;
                default:
                    if (BlockInfo.ShapeOf(b) == Shape.Slab && Meta.Has(meta, Meta.Double)) yield return new Stack((int)b, 2);
                    else yield return new Stack((int)b, 1);
                    break;
            }
        }

        /// <summary>Leaves: a sapling of their tree now and then, sticks, apples from apple trees (also used by leaf decay).</summary>
        public static IEnumerable<Stack> LeafDrops(BlockType b, System.Random rng, float chance)
        {
            if (saplingOf.TryGetValue(b, out var sap) && rng.NextDouble() < 0.06 * chance) yield return new Stack((int)sap, 1);
            if (rng.NextDouble() < 0.03 * chance) yield return new Stack((int)ItemType.Stick, 1 + rng.Next(2));
            if (b == BlockType.LeavesApple && rng.NextDouble() < 0.06 * chance) yield return new Stack((int)ItemType.Apple, 1);
            if (b == BlockType.LeavesBroad && rng.NextDouble() < 0.005 * chance) yield return new Stack((int)ItemType.Apple, 1);
        }
    }
}
