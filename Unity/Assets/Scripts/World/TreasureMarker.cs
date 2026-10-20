using UnityEngine;

namespace Dala
{
    /// <summary>Ancient treasure chest found in deep caves. Opened once with RMB; legendary ones hold the Golden Man's gold.</summary>
    public class TreasureMarker : MonoBehaviour
    {
        public bool Legendary;
        bool opened;

        public void Open(PlayerController p)
        {
            if (opened) { Hud.Instance?.ShowMessage("Сундук пуст."); return; }
            opened = true;
            var rng = new System.Random(GetInstanceID());
            p.Inventory.Add((int)ItemType.IngotGold, Legendary ? 12 : 3 + rng.Next(4));
            p.Inventory.Add((int)ItemType.IngotIron, 2 + rng.Next(5));
            p.Inventory.Add((int)ItemType.IngotCopper, 2 + rng.Next(5));
            if (rng.NextDouble() < 0.5) p.Inventory.Add((int)ItemType.Kylysh, 1);
            if (rng.NextDouble() < 0.5) p.Inventory.Add((int)ItemType.Torsyk, 1);
            if (Legendary) p.Inventory.Add((int)BlockType.GoldOrnament, 4);
            p.Stats.AddXp(Legendary ? 30 : 8);
            Hud.Instance?.ShowMessage(Legendary ? "Вы нашли сокровища Алтын Адама — Золотого человека!" : "Древний сундук: золото и железо!", 5f);
        }
    }
}
