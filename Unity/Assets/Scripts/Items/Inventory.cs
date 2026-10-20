using System;
using System.Collections.Generic;

namespace Dala
{
    /// <summary>A fixed array of item stacks (player inventory, chest, crafting grid, furnace slots).</summary>
    public class Inventory
    {
        public readonly Stack[] Slots;
        public event Action Changed;

        public Inventory(int size) { Slots = new Stack[size]; }

        public int Size => Slots.Length;
        public Stack this[int i] { get => Slots[i]; set { Slots[i] = value.Count <= 0 ? Stack.Empty : value; Changed?.Invoke(); } }
        public void Notify() => Changed?.Invoke();

        /// <summary>Adds items, filling existing stacks first. Returns the amount that did not fit.</summary>
        public int Add(int id, int count, int from = 0, int to = -1)
        {
            if (to < 0) to = Slots.Length;
            var def = Items.Get(id);
            int max = def?.MaxStack ?? 64;
            for (int i = from; i < to && count > 0; i++)
                if (Slots[i].Id == id && Slots[i].Count < max)
                {
                    int n = Math.Min(max - Slots[i].Count, count);
                    Slots[i].Count += n; count -= n;
                }
            for (int i = from; i < to && count > 0; i++)
                if (Slots[i].IsEmpty)
                {
                    int n = Math.Min(max, count);
                    Slots[i] = new Stack(id, n); count -= n;
                }
            Changed?.Invoke();
            return count;
        }

        /// <summary>Adds a stack keeping its wear (tools). Returns the amount that did not fit.</summary>
        public int Add(Stack s, int from = 0, int to = -1)
        {
            if (s.IsEmpty) return 0;
            if (s.Def != null && s.Def.Stackable && s.Wear == 0) return Add(s.Id, s.Count, from, to);
            if (to < 0) to = Slots.Length;
            for (int i = from; i < to; i++)
                if (Slots[i].IsEmpty) { Slots[i] = s; Changed?.Invoke(); return 0; }
            return s.Count;
        }

        /// <summary>Removes <paramref name="n"/> items with this id from anywhere. Returns false (and takes nothing) if there are too few.</summary>
        public bool Remove(int id, int n)
        {
            if (CountOf(id) < n) return false;
            for (int i = 0; i < Slots.Length && n > 0; i++)
                if (Slots[i].Id == id)
                {
                    int k = Math.Min(n, Slots[i].Count);
                    TakeFrom(i, k); n -= k;
                }
            return true;
        }

        /// <summary>Uses up durability of the tool in a slot; returns true if it broke.</summary>
        public bool Wear(int slot, int amount = 1)
        {
            var s = Slots[slot];
            var d = s.Def;
            if (s.IsEmpty || d == null || d.Durability <= 0) return false;
            s.Wear += amount;
            bool broke = s.Wear >= d.Durability;
            Slots[slot] = broke ? Stack.Empty : s;
            Changed?.Invoke();
            return broke;
        }

        public int CountOf(int id)
        {
            int n = 0;
            foreach (var s in Slots) if (s.Id == id) n += s.Count;
            return n;
        }

        public void TakeFrom(int slot, int n = 1)
        {
            var s = Slots[slot];
            s.Count -= n;
            Slots[slot] = s.Count <= 0 ? Stack.Empty : s;
            Changed?.Invoke();
        }

        public void Clear() { for (int i = 0; i < Slots.Length; i++) Slots[i] = Stack.Empty; Changed?.Invoke(); }
    }

    /// <summary>Shaped (pattern) and shapeless crafting recipes, Kazakh flavoured.</summary>
    public static class Recipes
    {
        public class Recipe
        {
            public string[] Pattern;           // null for shapeless
            public Dictionary<char, int> Key;
            public int[] Shapeless;
            public Stack Result;
        }

        public static readonly List<Recipe> All = new();
        static int B(BlockType b) => (int)b;
        static int I(ItemType t) => (int)t;

        static void Shaped(Stack result, string[] pattern, params (char c, int id)[] key)
        {
            var d = new Dictionary<char, int>();
            foreach (var k in key) d[k.c] = k.id;
            All.Add(new Recipe { Pattern = pattern, Key = d, Result = result });
        }
        static void Shapeless(Stack result, params int[] ids) => All.Add(new Recipe { Shapeless = ids, Result = result });

        static readonly BlockType[] Logs = { BlockType.Wood, BlockType.Birch, BlockType.LogSpruce, BlockType.LogPine, BlockType.LogSaxaul, BlockType.LogTuranga };

        static Recipes()
        {
            foreach (var log in Logs)
            {
                Shapeless(new Stack(B(BlockType.Planks), 4), B(log));
                Shaped(new Stack(B(BlockType.Campfire), 1), new[] { " S ", "SCS", "LLL" }, ('S', I(ItemType.Stick)), ('C', I(ItemType.Coal)), ('L', B(log)));
            }
            Shaped(new Stack(I(ItemType.Stick), 4), new[] { "P", "P" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Workbench), 1), new[] { "PP", "PP" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Fence), 3), new[] { "PSP", "PSP" }, ('P', B(BlockType.Planks)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.FenceGate), 1), new[] { "SPS", "SPS" }, ('P', B(BlockType.Planks)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.Furnace), 1), new[] { "CCC", "C C", "CCC" }, ('C', B(BlockType.Cobble)));
            Shaped(new Stack(B(BlockType.Tandyr), 1), new[] { "CCC", "C C", "CCC" }, ('C', B(BlockType.Clay)));
            Shaped(new Stack(B(BlockType.Sandyq), 1), new[] { "PPP", "PFP", "PPP" }, ('P', B(BlockType.Planks)), ('F', B(BlockType.FeltRed)));
            Shaped(new Stack(B(BlockType.Sandyq), 1), new[] { "PPP", "P P", "PPP" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Kebeje), 1), new[] { "PPP", "PCP", "PPP" }, ('P', B(BlockType.Planks)), ('C', I(ItemType.IngotCopper)));
            Shaped(new Stack(B(BlockType.Torch), 4), new[] { "C", "S" }, ('C', I(ItemType.Coal)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.Torch), 8), new[] { "F", "S" }, ('F', I(ItemType.FireEssence)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.Ladder), 3), new[] { "S S", "SSS", "S S" }, ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.Door), 3), new[] { "PP", "PP", "PP" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Trapdoor), 2), new[] { "PPP", "PPP" }, ('P', B(BlockType.Planks)));
            foreach (var wool in new[] { B(BlockType.WoolWhite), B(BlockType.Felt), B(BlockType.FeltRed), B(BlockType.FeltBlue) })
                Shaped(new Stack(B(BlockType.Bed), 1), new[] { "WWW", "PPP" }, ('W', wool), ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Kazan), 1), new[] { "I I", "I I", "III" }, ('I', I(ItemType.IngotIron)));
            // slabs and stairs of every building stone / wood
            foreach (var (full, slab, stairs) in new[] {
                (BlockType.Cobble, BlockType.SlabCobble, BlockType.StairsCobble), (BlockType.Planks, BlockType.SlabPlanks, BlockType.StairsPlanks),
                (BlockType.StoneBrick, BlockType.SlabStoneBrick, BlockType.StairsStoneBrick), (BlockType.Sandstone, BlockType.SlabSandstone, BlockType.StairsSandstone),
                (BlockType.Adobe, BlockType.SlabAdobe, BlockType.StairsAdobe) })
            {
                Shaped(new Stack(B(slab), 6), new[] { "BBB" }, ('B', B(full)));
                Shaped(new Stack(B(stairs), 4), new[] { "B  ", "BB ", "BBB" }, ('B', B(full)));
            }
            // tools: wood → stone → iron → gold heads
            foreach (var (head, pick, axe, shovel, sword) in new[] {
                (B(BlockType.Planks), ItemType.PickaxeWood, ItemType.AxeWood, ItemType.ShovelWood, ItemType.SwordWood),
                (B(BlockType.Cobble), ItemType.PickaxeStone, ItemType.AxeStone, ItemType.ShovelStone, ItemType.SwordStone),
                (I(ItemType.IngotIron), ItemType.Pickaxe, ItemType.Axe, ItemType.Shovel, ItemType.Kylysh),
                (I(ItemType.IngotGold), ItemType.PickaxeGold, ItemType.AxeGold, ItemType.ShovelGold, ItemType.KylyshGold) })
            {
                Shaped(new Stack(I(pick), 1), new[] { "HHH", " S ", " S " }, ('H', head), ('S', I(ItemType.Stick)));
                Shaped(new Stack(I(axe), 1), new[] { "HH", "HS", " S" }, ('H', head), ('S', I(ItemType.Stick)));
                Shaped(new Stack(I(shovel), 1), new[] { "H", "S", "S" }, ('H', head), ('S', I(ItemType.Stick)));
                Shaped(new Stack(I(sword), 1), new[] { "H", "H", "S" }, ('H', head), ('S', I(ItemType.Stick)));
            }
            Shaped(new Stack(I(ItemType.Spear), 1), new[] { "  I", " S ", "S  " }, ('I', I(ItemType.IngotIron)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(I(ItemType.Bow), 1), new[] { " SW", "S W", " SW" }, ('S', I(ItemType.Stick)), ('W', I(ItemType.Wool)));
            Shaped(new Stack(I(ItemType.Bow), 1), new[] { " SW", "S W", " SW" }, ('S', I(ItemType.Stick)), ('W', I(ItemType.String)));
            Shaped(new Stack(I(ItemType.Arrow), 4), new[] { "F", "S", "W" }, ('F', I(ItemType.Flint)), ('S', I(ItemType.Stick)), ('W', I(ItemType.Wool)));
            Shaped(new Stack(I(ItemType.Arrow), 4), new[] { "I", "S", "W" }, ('I', I(ItemType.IngotIron)), ('S', I(ItemType.Stick)), ('W', I(ItemType.Wool)));
            Shapeless(new Stack(I(ItemType.Kamcha), 1), I(ItemType.Leather), I(ItemType.Leather), I(ItemType.Stick));
            Shaped(new Stack(I(ItemType.Torsyk), 1), new[] { "L L", " L " }, ('L', I(ItemType.Leather)));
            Shapeless(new Stack(I(ItemType.FlintSteel), 1), I(ItemType.IngotIron), I(ItemType.Flint));
            Shaped(new Stack(I(ItemType.Bucket), 1), new[] { "I I", " I " }, ('I', I(ItemType.IngotIron)));
            Shaped(new Stack(I(ItemType.Kese), 4), new[] { "P P", " P " }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.WoolWhite), 1), new[] { "SS", "SS" }, ('S', I(ItemType.String)));
            // felt & carpets
            Shaped(new Stack(B(BlockType.Felt), 1), new[] { "WW", "WW" }, ('W', I(ItemType.Wool)));
            Shaped(new Stack(B(BlockType.WoolWhite), 1), new[] { "WWW", "WWW", "WWW" }, ('W', I(ItemType.Wool)));
            Shapeless(new Stack(B(BlockType.FeltRed), 1), B(BlockType.Felt), I(ItemType.Apple));
            Shapeless(new Stack(B(BlockType.FeltBlue), 1), B(BlockType.Felt), I(ItemType.IngotCopper));
            Shapeless(new Stack(B(BlockType.Tekemet), 2), B(BlockType.FeltRed), B(BlockType.FeltBlue), B(BlockType.Felt));
            Shapeless(new Stack(B(BlockType.Shyrdak), 2), B(BlockType.FeltRed), B(BlockType.FeltBlue));
            // building
            Shaped(new Stack(B(BlockType.StoneBrick), 4), new[] { "CC", "CC" }, ('C', B(BlockType.Cobble)));
            Shaped(new Stack(B(BlockType.Adobe), 4), new[] { "CH", "HC" }, ('C', B(BlockType.Clay)), ('H', B(BlockType.Hay)));
            Shapeless(new Stack(B(BlockType.TileTurquoise), 4), B(BlockType.Clay), B(BlockType.Clay), I(ItemType.IngotCopper));
            Shapeless(new Stack(B(BlockType.TileMajolica), 4), B(BlockType.TileTurquoise), B(BlockType.TileTurquoise), I(ItemType.IngotGold));
            Shaped(new Stack(B(BlockType.GoldOrnament), 1), new[] { "GGG", "GGG", "GGG" }, ('G', I(ItemType.IngotGold)));
            Shaped(new Stack(B(BlockType.Sandstone), 1), new[] { "SS", "SS" }, ('S', B(BlockType.Sand)));
            Shaped(new Stack(B(BlockType.Thatch), 4), new[] { "HH", "HH" }, ('H', B(BlockType.Hay)));
            // food
            Shapeless(new Stack(I(ItemType.Baursak), 4), I(ItemType.Nan), I(ItemType.Kurt));
        }

        /// <summary>Every ingredient id of a recipe, one entry per grid cell it fills.</summary>
        public static List<int> Ingredients(Recipe r)
        {
            var list = new List<int>();
            if (r.Shapeless != null) { list.AddRange(r.Shapeless); return list; }
            foreach (var row in r.Pattern)
                foreach (char c in row)
                    if (c != ' ' && r.Key.TryGetValue(c, out int id)) list.Add(id);
            return list;
        }

        /// <summary>True if the recipe fits a w×w grid (2×2 inventory or 3×3 workbench).</summary>
        public static bool Fits(Recipe r, int w)
        {
            if (r.Shapeless != null) return r.Shapeless.Length <= w * w;
            if (r.Pattern.Length > w) return false;
            foreach (var row in r.Pattern) if (row.Length > w) return false;
            return true;
        }

        /// <summary>Ingredient for grid cell (x, y) of the recipe laid out from the top-left corner (0 = empty).</summary>
        public static int CellOf(Recipe r, int x, int y, int w)
        {
            if (r.Shapeless != null) { int i = y * w + x; return i < r.Shapeless.Length ? r.Shapeless[i] : 0; }
            if (y >= r.Pattern.Length || x >= r.Pattern[y].Length) return 0;
            char c = r.Pattern[y][x];
            return c != ' ' && r.Key.TryGetValue(c, out int id) ? id : 0;
        }

        /// <summary>Finds the recipe matching a w×h crafting grid (grid slots row-major, top row first).</summary>
        public static Recipe Match(Inventory grid, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            var ids = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var s = grid[y * w + x];
                    if (s.IsEmpty) continue;
                    ids.Add(s.Id);
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            if (ids.Count == 0) return null;
            int pw = maxX - minX + 1, ph = maxY - minY + 1;
            foreach (var r in All)
            {
                if (r.Shapeless != null)
                {
                    if (r.Shapeless.Length != ids.Count) continue;
                    var need = new List<int>(r.Shapeless);
                    bool ok = true;
                    foreach (var id in ids) if (!need.Remove(id)) { ok = false; break; }
                    if (ok) return r;
                    continue;
                }
                if (r.Pattern.Length != ph || r.Pattern[0].Length != pw) continue;
                bool match = true;
                for (int y = 0; y < ph && match; y++)
                    for (int x = 0; x < pw && match; x++)
                    {
                        char c = x < r.Pattern[y].Length ? r.Pattern[y][x] : ' ';
                        var s = grid[(minY + y) * w + minX + x];
                        if (c == ' ') match = s.IsEmpty;
                        else match = !s.IsEmpty && r.Key.TryGetValue(c, out int id) && s.Id == id;
                    }
                if (match) return r;
            }
            return null;
        }
    }

    /// <summary>Furnace (пеш) and tandyr recipes and fuel values (seconds of burning).</summary>
    public static class Smelting
    {
        public static readonly Dictionary<int, Stack> Results = new()
        {
            [(int)BlockType.OreIron] = new Stack((int)ItemType.IngotIron, 1),
            [(int)BlockType.OreGold] = new Stack((int)ItemType.IngotGold, 1),
            [(int)BlockType.OreCopper] = new Stack((int)ItemType.IngotCopper, 1),
            [(int)ItemType.MeatRaw] = new Stack((int)ItemType.MeatCooked, 1),
            [(int)ItemType.FishRaw] = new Stack((int)ItemType.MeatCooked, 1),
            [(int)BlockType.Cobble] = new Stack((int)BlockType.Stone, 1),
            [(int)BlockType.Clay] = new Stack((int)BlockType.Adobe, 1),
            [(int)BlockType.Hay] = new Stack((int)ItemType.Nan, 2),
            [(int)BlockType.Sand] = new Stack((int)BlockType.Glass, 1),
            [(int)BlockType.RedSand] = new Stack((int)BlockType.Glass, 1),
            [(int)BlockType.Wood] = new Stack((int)ItemType.Coal, 1), [(int)BlockType.Birch] = new Stack((int)ItemType.Coal, 1),
            [(int)BlockType.LogSpruce] = new Stack((int)ItemType.Coal, 1), [(int)BlockType.LogPine] = new Stack((int)ItemType.Coal, 1),
            [(int)BlockType.LogSaxaul] = new Stack((int)ItemType.Coal, 1), [(int)BlockType.LogTuranga] = new Stack((int)ItemType.Coal, 1),
        };

        /// <summary>The tandyr only bakes: bread from hay (grain), meat and fish — twice as fast as a furnace.</summary>
        public static readonly Dictionary<int, Stack> Baking = new()
        {
            [(int)BlockType.Hay] = new Stack((int)ItemType.Nan, 3),
            [(int)ItemType.MeatRaw] = new Stack((int)ItemType.MeatCooked, 1),
            [(int)ItemType.FishRaw] = new Stack((int)ItemType.MeatCooked, 1),
        };

        public static float Fuel(int id)
        {
            if (id < 1000 && BlockInfo.IsLog((BlockType)id)) return 15f;
            if (id < 1000 && BlockInfo.IsSapling((BlockType)id)) return 2.5f;
            return id switch
            {
                (int)ItemType.Coal => 40f,
                (int)BlockType.Planks or (int)BlockType.Fence or (int)BlockType.FenceGate or (int)BlockType.StairsPlanks or (int)BlockType.Ladder => 8f,
                (int)BlockType.SlabPlanks => 4f,
                (int)ItemType.Stick => 3f,
                (int)BlockType.Hay => 6f,
                (int)ItemType.FireEssence => 120f,
                _ => 0f,
            };
        }
        public const float SmeltTime = 6f;
    }
}
