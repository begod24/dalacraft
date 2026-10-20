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

        static Recipes()
        {
            Shapeless(new Stack(B(BlockType.Planks), 4), B(BlockType.Wood));
            Shapeless(new Stack(B(BlockType.Planks), 4), B(BlockType.Birch));
            Shaped(new Stack(I(ItemType.Stick), 4), new[] { "P", "P" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Workbench), 1), new[] { "PP", "PP" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Furnace), 1), new[] { "CCC", "C C", "CCC" }, ('C', B(BlockType.Cobble)));
            Shaped(new Stack(B(BlockType.Sandyq), 1), new[] { "PPP", "PFP", "PPP" }, ('P', B(BlockType.Planks)), ('F', B(BlockType.FeltRed)));
            Shaped(new Stack(B(BlockType.Sandyq), 1), new[] { "PPP", "P P", "PPP" }, ('P', B(BlockType.Planks)));
            Shaped(new Stack(B(BlockType.Torch), 4), new[] { "C", "S" }, ('C', I(ItemType.Coal)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(B(BlockType.Campfire), 1), new[] { " S ", "SCS", "WWW" }, ('S', I(ItemType.Stick)), ('C', I(ItemType.Coal)), ('W', B(BlockType.Wood)));
            // tools: heads of cobble or iron
            foreach (var head in new[] { B(BlockType.Cobble), I(ItemType.IngotIron), B(BlockType.Planks) })
            {
                Shaped(new Stack(I(ItemType.Pickaxe), 1), new[] { "HHH", " S ", " S " }, ('H', head), ('S', I(ItemType.Stick)));
                Shaped(new Stack(I(ItemType.Axe), 1), new[] { "HH", "HS", " S" }, ('H', head), ('S', I(ItemType.Stick)));
                Shaped(new Stack(I(ItemType.Shovel), 1), new[] { "H", "S", "S" }, ('H', head), ('S', I(ItemType.Stick)));
            }
            Shaped(new Stack(I(ItemType.Kylysh), 1), new[] { "I", "I", "G" }, ('I', I(ItemType.IngotIron)), ('G', I(ItemType.IngotGold)));
            Shaped(new Stack(I(ItemType.Kylysh), 1), new[] { "I", "I", "S" }, ('I', I(ItemType.IngotIron)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(I(ItemType.Spear), 1), new[] { "  I", " S ", "S  " }, ('I', I(ItemType.IngotIron)), ('S', I(ItemType.Stick)));
            Shaped(new Stack(I(ItemType.Bow), 1), new[] { " SW", "S W", " SW" }, ('S', I(ItemType.Stick)), ('W', I(ItemType.Wool)));
            Shaped(new Stack(I(ItemType.Arrow), 4), new[] { "I", "S", "W" }, ('I', I(ItemType.IngotIron)), ('S', I(ItemType.Stick)), ('W', I(ItemType.Wool)));
            Shapeless(new Stack(I(ItemType.Kamcha), 1), I(ItemType.Leather), I(ItemType.Leather), I(ItemType.Stick));
            Shaped(new Stack(I(ItemType.Torsyk), 1), new[] { "L L", " L " }, ('L', I(ItemType.Leather)));
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

    /// <summary>Furnace (пеш) recipes and fuel values (seconds of burning).</summary>
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
            [(int)BlockType.Wood] = new Stack((int)ItemType.Coal, 1),
            [(int)BlockType.Sand] = new Stack((int)BlockType.Sandstone, 1),
        };

        public static float Fuel(int id) => id switch
        {
            (int)ItemType.Coal => 40f,
            (int)BlockType.Wood or (int)BlockType.Birch => 15f,
            (int)BlockType.Planks => 8f,
            (int)ItemType.Stick => 3f,
            (int)BlockType.Hay => 6f,
            _ => 0f,
        };
        public const float SmeltTime = 6f;
    }
}
