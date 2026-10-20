using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>An achievement (жетістік): earned by obtaining an item or by an event; some open extra recipes.</summary>
    public class Achievement
    {
        public string Id, Title, Hint;
        /// <summary>Earned when any of these items is first obtained (or by <see cref="Progress.Event"/> with <see cref="Id"/>).</summary>
        public int[] Items = System.Array.Empty<int>();
    }

    /// <summary>
    /// What the player has discovered in this world, like Minecraft's recipe book and advancements: every item ever held
    /// (a recipe shows in the book once you have had one of its ingredients) and the achievements earned. A few
    /// recipes — gold ornaments, the kazan, carpets — only open after their achievement. Saved with the world.
    /// </summary>
    public static class Progress
    {
        public static readonly HashSet<int> Seen = new();
        public static readonly HashSet<string> Earned = new();
        public static event System.Action<Achievement> Unlocked;

        static int B(BlockType b) => (int)b;
        static int I(ItemType t) => (int)t;

        public static readonly List<Achievement> All = new()
        {
            new() { Id = "wood", Title = "Ағаш кесу — Первое бревно", Hint = "Добудь бревно любого дерева",
                    Items = new[] { B(BlockType.Wood), B(BlockType.Birch), B(BlockType.LogSpruce), B(BlockType.LogPine), B(BlockType.LogSaxaul), B(BlockType.LogTuranga) } },
            new() { Id = "bench", Title = "Шебер — Мастерская", Hint = "Сделай верстак", Items = new[] { B(BlockType.Workbench) } },
            new() { Id = "stone", Title = "Тас дәуірі — Каменный век", Hint = "Добудь булыжник", Items = new[] { B(BlockType.Cobble) } },
            new() { Id = "fire", Title = "От — Огонь", Hint = "Получи уголь или факел", Items = new[] { I(ItemType.Coal), B(BlockType.Torch) } },
            new() { Id = "iron", Title = "Темір — Железо", Hint = "Выплавь железный слиток", Items = new[] { I(ItemType.IngotIron) } },
            new() { Id = "copper", Title = "Мыс — Медь", Hint = "Выплавь медный слиток", Items = new[] { I(ItemType.IngotCopper) } },
            new() { Id = "gold", Title = "Алтын — Золото степи", Hint = "Выплавь золотой слиток", Items = new[] { I(ItemType.IngotGold) } },
            new() { Id = "felt", Title = "Киіз басу — Войлок", Hint = "Свалять войлок из шерсти", Items = new[] { B(BlockType.Felt) } },
            new() { Id = "meal", Title = "Дастарқан — Угощение", Hint = "Свари бешбармак, сорпу или қуырдақ в қазане",
                    Items = new[] { I(ItemType.Beshbarmak), I(ItemType.Sorpa), I(ItemType.Kuyrdak) } },
            new() { Id = "hunter", Title = "Батыр — Защитник", Hint = "Победи ночное существо" },
            new() { Id = "sleep", Title = "Тыныш түн — Спокойная ночь", Hint = "Поспи на корпе" },
            new() { Id = "bow", Title = "Мерген — Меткий стрелок", Hint = "Сделай садақ (лук)", Items = new[] { I(ItemType.Bow) } },
        };

        /// <summary>Recipes that need an achievement besides a known ingredient (result id → achievement id).</summary>
        static readonly Dictionary<int, string> Gated = new()
        {
            [B(BlockType.GoldOrnament)] = "gold", [B(BlockType.TileMajolica)] = "gold", [I(ItemType.KylyshGold)] = "gold",
            [B(BlockType.Kazan)] = "iron", [B(BlockType.Kebeje)] = "copper", [B(BlockType.TileTurquoise)] = "copper",
            [B(BlockType.Tekemet)] = "felt", [B(BlockType.Shyrdak)] = "felt", [B(BlockType.FeltRed)] = "felt", [B(BlockType.FeltBlue)] = "felt",
        };

        public static void Reset() { Seen.Clear(); Earned.Clear(); }

        /// <summary>Marks every item in the inventory as seen (called whenever it changes) and checks item achievements.</summary>
        public static void Saw(Inventory inv)
        {
            foreach (var s in inv.Slots) if (!s.IsEmpty) Saw(s.Id);
        }

        public static void Saw(int id)
        {
            if (!Seen.Add(id)) return;
            foreach (var a in All)
                if (!Earned.Contains(a.Id) && System.Array.IndexOf(a.Items, id) >= 0) Earn(a);
        }

        /// <summary>Non-item achievements: "hunter", "sleep".</summary>
        public static void Event(string id)
        {
            var a = All.Find(x => x.Id == id);
            if (a != null && !Earned.Contains(id)) Earn(a);
        }

        static void Earn(Achievement a)
        {
            Earned.Add(a.Id);
            if (!Session.Creative) Unlocked?.Invoke(a);
        }

        public static string GateOf(int resultId) => Gated.TryGetValue(resultId, out var g) ? g : null;

        /// <summary>A crafting recipe is in the book once one of its ingredients was obtained (and its achievement, if any, earned).</summary>
        public static bool Knows(Recipes.Recipe r)
        {
            if (Session.Creative) return true;
            var gate = GateOf(r.Result.Id);
            if (gate != null && !Earned.Contains(gate)) return false;
            foreach (var id in Recipes.Ingredients(r)) if (Seen.Contains(id)) return true;
            return false;
        }

        public static bool KnowsInput(int id) => Session.Creative || Seen.Contains(id);
    }
}
