using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dala
{
    /// <summary>
    /// Inventory / crafting / furnace / chest windows. LMB picks up or drops a stack, RMB places one or takes half,
    /// Shift+click moves between the container and the player inventory.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        public enum Mode { Player, Workbench, Furnace, Chest }
        public static InventoryScreen Instance { get; private set; }
        public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;

        PlayerController pc;
        RectTransform canvasRt, panelRoot, content, cursorRt;
        UnityEngine.UI.Image cursorIcon; UnityEngine.UI.Text cursorCount, tooltip;
        Stack cursor;
        Mode mode;
        BlockEntity entity;
        Inventory craft = new(4);
        int craftW = 2;
        Inventory output = new(1);
        readonly List<SlotView> views = new();
        UnityEngine.UI.Image furnaceArrow, furnaceFlame;
        int creativePage;

        class SlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public InventoryScreen Owner; public Inventory Inv; public int Index; public bool TakeOnly, IsCraftOutput, IsCreative; public int CreativeId;
            public UnityEngine.UI.Image Icon; public UnityEngine.UI.Text Count;
            public void OnPointerClick(PointerEventData e) => Owner.Click(this, e.button == PointerEventData.InputButton.Right);
            public void OnPointerEnter(PointerEventData e) => Owner.Hover(this);
            public void OnPointerExit(PointerEventData e) => Owner.Hover(null);
        }

        public void Init(PlayerController player)
        {
            Instance = this; pc = player;
            var c = UiKit.Canvas("InventoryUI", 20, transform);
            canvasRt = (RectTransform)c.transform;
            panelRoot = UiKit.Stretch("Root", canvasRt);
            UiKit.Image(panelRoot, null, new Color(0, 0, 0, 0.45f), false, true);
            content = UiKit.Stretch("Content", panelRoot);
            cursorRt = UiKit.Center("Cursor", canvasRt, new Vector2(48, 48));
            cursorIcon = UiKit.Image(cursorRt, null);
            cursorCount = UiKit.Text(cursorRt, "", 26, Color.white, TextAnchor.LowerRight);
            var tt = UiKit.Center("Tooltip", canvasRt, new Vector2(500, 40));
            tooltip = UiKit.Text(tt, "", 26, UiKit.Gold, TextAnchor.MiddleLeft);
            panelRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ open / close
        public void Open(Mode m, BlockEntity e = null)
        {
            mode = m; entity = e;
            craftW = m == Mode.Workbench ? 3 : 2;
            craft = new Inventory(craftW * craftW);
            craft.Changed += UpdateCraftOutput;
            Build();
            panelRoot.gameObject.SetActive(true);
            UpdateCraftOutput();
        }

        public void Close()
        {
            if (!IsOpen) return;
            // return crafting grid and cursor to the player
            foreach (var s in craft.Slots) if (!s.IsEmpty) Give(s);
            if (!cursor.IsEmpty) Give(cursor);
            cursor = Stack.Empty;
            panelRoot.gameObject.SetActive(false);
            tooltip.text = "";
        }

        void Give(Stack s)
        {
            int left = pc.Inventory.Add(s.Id, s.Count);
            if (left > 0) Hud.Instance?.ShowMessage("Инвентарь полон");
        }

        // ------------------------------------------------------------------ layout
        void Build()
        {
            foreach (Transform t in content) Destroy(t.gameObject);
            views.Clear();
            bool creative = Session.Creative && mode == Mode.Player;
            var panel = UiKit.Panel(content, new Vector2(620, 720), new Vector2(creative ? 330 : 0, 0)).rectTransform;
            string title = mode switch { Mode.Workbench => "Верстак", Mode.Furnace => "Печь", Mode.Chest => "Сандык", _ => "Инвентарь" };
            var tr = UiKit.Center("Title", panel, new Vector2(560, 50), new Vector2(0, 318)); UiKit.Text(tr, title, 34, UiKit.Ink, TextAnchor.MiddleCenter, false);

            // player inventory: rows of 9 (slots 9..35), hotbar (0..8)
            for (int r = 0; r < 3; r++)
                for (int col = 0; col < 9; col++)
                    Slot(panel, pc.Inventory, 9 + r * 9 + col, new Vector2(-240 + col * 60, -30 - r * 60));
            for (int col = 0; col < 9; col++) Slot(panel, pc.Inventory, col, new Vector2(-240 + col * 60, -232));

            switch (mode)
            {
                case Mode.Player:
                case Mode.Workbench:
                {
                    float x0 = craftW == 3 ? -200 : -120, y0 = craftW == 3 ? 250 : 220;
                    for (int r = 0; r < craftW; r++)
                        for (int col = 0; col < craftW; col++) Slot(panel, craft, r * craftW + col, new Vector2(x0 + col * 60, y0 - r * 60));
                    var arrow = UiKit.Center("Arrow", panel, new Vector2(72, 51), new Vector2(craftW == 3 ? 40 : 30, 190)); UiKit.Image(arrow, "UI_CraftArrow");
                    var o = Slot(panel, output, 0, new Vector2(craftW == 3 ? 150 : 140, 190)); o.TakeOnly = true; o.IsCraftOutput = true;
                    ((RectTransform)o.transform).sizeDelta = new Vector2(72, 72);
                    break;
                }
                case Mode.Furnace:
                {
                    Slot(panel, entity.Storage, 0, new Vector2(-100, 250));
                    Slot(panel, entity.Storage, 1, new Vector2(-100, 130));
                    var fl = UiKit.Center("Flame", panel, new Vector2(36, 36), new Vector2(-100, 190)); furnaceFlame = UiKit.Image(fl, null, new Color(1f, 0.55f, 0.1f));
                    furnaceFlame.sprite = UiKit.Sprite("UI_HungerFull");
                    var ar = UiKit.Center("Arrow", panel, new Vector2(72, 51), new Vector2(10, 190)); UiKit.Image(ar, "UI_CraftArrow", new Color(1, 1, 1, 0.3f));
                    var af = UiKit.Center("ArrowFill", panel, new Vector2(72, 51), new Vector2(10, 190)); furnaceArrow = UiKit.Image(af, "UI_CraftArrow");
                    furnaceArrow.type = UnityEngine.UI.Image.Type.Filled; furnaceArrow.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                    var o = Slot(panel, entity.Storage, 2, new Vector2(120, 190)); o.TakeOnly = true;
                    break;
                }
                case Mode.Chest:
                    for (int r = 0; r < 3; r++)
                        for (int col = 0; col < 9; col++) Slot(panel, entity.Storage, r * 9 + col, new Vector2(-240 + col * 60, 250 - r * 60));
                    break;
            }
            if (creative) BuildCreative();
        }

        void BuildCreative()
        {
            var list = new List<ItemDef>(Items.All);
            list.RemoveAll(d => d.Id == (int)BlockType.Water);
            int perPage = 9 * 9, pages = (list.Count + perPage - 1) / perPage;
            creativePage = Mathf.Clamp(creativePage, 0, pages - 1);
            var panel = UiKit.Panel(content, new Vector2(620, 720), new Vector2(-330, 0)).rectTransform;
            var tr = UiKit.Center("Title", panel, new Vector2(560, 50), new Vector2(0, 318)); UiKit.Text(tr, $"Все предметы  {creativePage + 1}/{pages}", 32, UiKit.Ink, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < perPage; i++)
            {
                int k = creativePage * perPage + i;
                if (k >= list.Count) break;
                var v = Slot(panel, null, 0, new Vector2(-240 + (i % 9) * 60, 250 - (i / 9) * 56));
                v.IsCreative = true; v.CreativeId = list[k].Id;
            }
            UiKit.Button(panel, "<", new Vector2(90, 50), new Vector2(-200, -300), () => { creativePage--; Build(); });
            UiKit.Button(panel, ">", new Vector2(90, 50), new Vector2(200, -300), () => { creativePage++; Build(); });
        }

        SlotView Slot(RectTransform parent, Inventory inv, int index, Vector2 pos)
        {
            var rt = UiKit.Center("Slot", parent, new Vector2(60, 60), pos);
            UiKit.Image(rt, "UI_Slot", null, true, true);
            var v = rt.gameObject.AddComponent<SlotView>();
            v.Owner = this; v.Inv = inv; v.Index = index;
            var ic = UiKit.Center("Icon", rt, new Vector2(42, 42)); v.Icon = UiKit.Image(ic, null);
            v.Count = UiKit.Text(rt, "", 24, Color.white, TextAnchor.LowerRight); v.Count.rectTransform.offsetMax = new Vector2(-4, 2);
            views.Add(v);
            return v;
        }

        // ------------------------------------------------------------------ interaction
        void Click(SlotView v, bool right)
        {
            bool shift = UnityEngine.InputSystem.Keyboard.current?.leftShiftKey.isPressed ?? false;
            if (v.IsCreative)
            {
                var d = Items.Get(v.CreativeId);
                if (shift) pc.Inventory.Add(d.Id, d.MaxStack);
                else cursor = new Stack(d.Id, right ? 1 : d.MaxStack);
                return;
            }
            if (v.IsCraftOutput) { TakeCraft(shift); return; }
            var inv = v.Inv; int i = v.Index;
            var slot = inv[i];

            if (shift && !slot.IsEmpty)
            {
                // quick move: container <-> player, hotbar <-> main
                if (inv == pc.Inventory)
                {
                    int left = slot.Count;
                    if (entity != null && entity.Storage != null && mode == Mode.Chest) left = entity.Storage.Add(slot.Id, slot.Count);
                    else if (mode == Mode.Furnace && Smelting.Results.ContainsKey(slot.Id)) left = entity.Storage.Add(slot.Id, slot.Count, 0, 1);
                    else if (mode == Mode.Furnace && Smelting.Fuel(slot.Id) > 0) left = entity.Storage.Add(slot.Id, slot.Count, 1, 2);
                    else left = i < 9 ? pc.Inventory.Add(slot.Id, slot.Count, 9, 36) : pc.Inventory.Add(slot.Id, slot.Count, 0, 9);
                    if (left == slot.Count) return;
                    inv[i] = new Stack(slot.Id, left);
                }
                else { int left = pc.Inventory.Add(slot.Id, slot.Count); inv[i] = new Stack(slot.Id, left); }
                return;
            }

            if (v.TakeOnly)
            {
                if (slot.IsEmpty) return;
                if (cursor.IsEmpty) { cursor = slot; inv[i] = Stack.Empty; }
                else if (cursor.Id == slot.Id && cursor.Count + slot.Count <= 64) { cursor.Count += slot.Count; inv[i] = Stack.Empty; }
                return;
            }

            int max = Items.Get(cursor.IsEmpty ? slot.Id : cursor.Id)?.MaxStack ?? 64;
            if (!right)
            {
                if (!cursor.IsEmpty && !slot.IsEmpty && cursor.Id == slot.Id)
                {
                    int n = Mathf.Min(max - slot.Count, cursor.Count);
                    inv[i] = new Stack(slot.Id, slot.Count + n); cursor.Count -= n; if (cursor.Count <= 0) cursor = Stack.Empty;
                }
                else { inv[i] = cursor; cursor = slot; }
            }
            else
            {
                if (cursor.IsEmpty && !slot.IsEmpty)
                {
                    int half = (slot.Count + 1) / 2;
                    cursor = new Stack(slot.Id, half); inv[i] = new Stack(slot.Id, slot.Count - half);
                }
                else if (!cursor.IsEmpty && (slot.IsEmpty || (slot.Id == cursor.Id && slot.Count < max)))
                {
                    inv[i] = new Stack(cursor.Id, (slot.IsEmpty ? 0 : slot.Count) + 1);
                    cursor.Count--; if (cursor.Count <= 0) cursor = Stack.Empty;
                }
            }
        }

        void TakeCraft(bool all)
        {
            int guard = 64;
            do
            {
                var res = output[0];
                if (res.IsEmpty) return;
                if (all)
                {
                    if (pc.Inventory.Add(res.Id, res.Count) > 0) return;
                }
                else
                {
                    if (!cursor.IsEmpty && (cursor.Id != res.Id || cursor.Count + res.Count > (res.Def?.MaxStack ?? 64))) return;
                    cursor = cursor.IsEmpty ? res : new Stack(res.Id, cursor.Count + res.Count);
                }
                for (int i = 0; i < craft.Size; i++) if (!craft[i].IsEmpty) craft.Slots[i] = craft[i].Count > 1 ? new Stack(craft[i].Id, craft[i].Count - 1) : Stack.Empty;
                craft.Notify();
                pc.Stats.AddXp(0.2f);
            } while (all && --guard > 0);
        }

        void UpdateCraftOutput()
        {
            var r = Recipes.Match(craft, craftW, craftW);
            output.Slots[0] = r != null ? r.Result : Stack.Empty;
        }

        void Hover(SlotView v)
        {
            if (v == null) { tooltip.text = ""; return; }
            var s = v.IsCreative ? new Stack(v.CreativeId, 1) : v.Inv[v.Index];
            tooltip.text = s.IsEmpty ? "" : s.Def?.Name;
        }

        void Update()
        {
            if (!IsOpen) return;
            foreach (var v in views)
            {
                if (v == null) continue;
                var s = v.IsCreative ? new Stack(v.CreativeId, 1) : v.Inv[v.Index];
                v.Icon.enabled = !s.IsEmpty;
                if (!s.IsEmpty) v.Icon.sprite = s.Def?.Icon;
                v.Count.text = !s.IsEmpty && s.Count > 1 && !v.IsCreative ? s.Count.ToString() : "";
            }
            var mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, mouse, null, out var local);
            cursorRt.anchoredPosition = local;
            cursorIcon.enabled = !cursor.IsEmpty;
            if (!cursor.IsEmpty) cursorIcon.sprite = cursor.Def?.Icon;
            cursorCount.text = cursor.Count > 1 ? cursor.Count.ToString() : "";
            ((RectTransform)tooltip.transform).anchoredPosition = local + new Vector2(270, 40);
            if (mode == Mode.Furnace && entity != null && furnaceArrow != null)
            {
                furnaceArrow.fillAmount = entity.Progress / Smelting.SmeltTime;
                furnaceFlame.color = new Color(1f, 0.55f, 0.1f, entity.BurnMax > 0 ? Mathf.Clamp01(entity.Burn / entity.BurnMax) : 0.15f);
            }
        }
    }
}
