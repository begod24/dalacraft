using UnityEngine;

namespace Dala
{
    /// <summary>Minecraft-style HUD in Kazakh style: hotbar, hearts, baursak hunger, armor, air, xp, crosshair, death screen.</summary>
    public class Hud : MonoBehaviour
    {
        const float P = UiKit.Px;
        PlayerController pc;
        PlayerStats stats;
        RectTransform root, selectFrame, breakBar;
        readonly UnityEngine.UI.Image[] slotIcons = new UnityEngine.UI.Image[9];
        readonly UnityEngine.UI.Text[] slotCounts = new UnityEngine.UI.Text[9];
        readonly UnityEngine.UI.Image[] hearts = new UnityEngine.UI.Image[10], hunger = new UnityEngine.UI.Image[10], armor = new UnityEngine.UI.Image[10], air = new UnityEngine.UI.Image[10];
        UnityEngine.UI.Image xpFill, hurt, breakFill;
        UnityEngine.UI.Text xpLevel, itemName, debug, message;
        GameObject deathScreen;
        float nameTimer, msgTimer;
        int lastSelected = -1;
        bool showDebug;

        public static Hud Instance { get; private set; }

        public void Init(PlayerController player)
        {
            Instance = this;
            pc = player; stats = player.GetComponent<PlayerStats>();
            var canvas = UiKit.Canvas("HUD", 10, transform);
            root = (RectTransform)canvas.transform;

            // hurt vignette
            var hv = UiKit.Stretch("Hurt", root); hurt = UiKit.Image(hv, null, new Color(0.8f, 0, 0, 0));

            // crosshair + mining progress
            UiKit.Image(UiKit.Center("Crosshair", root, new Vector2(9 * P, 9 * P)), "UI_Crosshair");
            breakBar = UiKit.Center("Break", root, new Vector2(16 * P, 2 * P), new Vector2(0, -12 * P));
            UiKit.Image(breakBar, null, new Color(0, 0, 0, 0.6f));
            var bf = UiKit.Rect("Fill", breakBar, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            breakFill = UiKit.Image(bf, null, UiKit.Gold);
            breakBar.gameObject.SetActive(false);

            // hotbar
            var bar = UiKit.Rect("Hotbar", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2 * P), new Vector2(182 * P, 22 * P));
            UiKit.Image(bar, "UI_Hotbar");
            for (int i = 0; i < 9; i++)
            {
                var slot = UiKit.Rect("Slot" + i, bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((11 + 20 * i) * P, 0), new Vector2(16 * P, 16 * P));
                slotIcons[i] = UiKit.Image(slot, null);
                var ct = UiKit.Text(slot, "", 26, Color.white, TextAnchor.LowerRight);
                ct.rectTransform.offsetMax = new Vector2(4, -4);
                slotCounts[i] = ct;
            }
            selectFrame = UiKit.Rect("Selected", bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24 * P, 24 * P));
            UiKit.Image(selectFrame, "UI_SlotSelected");

            // xp
            float barTop = 2 * P + 22 * P;
            var xp = UiKit.Rect("Xp", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, barTop + 2 * P), new Vector2(182 * P, 5 * P));
            UiKit.Image(xp, "UI_XpBarBg");
            var xf = UiKit.Rect("Fill", xp, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            xpFill = UiKit.Image(xf, "UI_XpBarFill");
            xpFill.type = UnityEngine.UI.Image.Type.Filled; xpFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            xf.anchorMax = Vector2.one;
            var lv = UiKit.Center("Level", xp, new Vector2(120, 40), new Vector2(0, 14)); xpLevel = UiKit.Text(lv, "", 30, new Color(0.55f, 1f, 0.9f));

            // stat rows (hearts left, hunger right, armor above hearts, air above hunger)
            float rowY = barTop + 9 * P;
            for (int i = 0; i < 10; i++)
            {
                hearts[i] = Icon("Heart" + i, -91 * P + i * 8 * P, rowY, 0f);
                hunger[i] = Icon("Hunger" + i, 91 * P - 9 * P - i * 8 * P, rowY, 0f);
                armor[i] = Icon("Armor" + i, -91 * P + i * 8 * P, rowY + 10 * P, 0f);
                air[i] = Icon("Air" + i, 91 * P - 9 * P - i * 8 * P, rowY + 10 * P, 0f);
                air[i].sprite = UiKit.Sprite("UI_Bubble");
            }

            // item name, messages, debug
            var nm = UiKit.Rect("ItemName", root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, rowY + 22 * P), new Vector2(900, 40));
            itemName = UiKit.Text(nm, "", 30, UiKit.Cream);
            var ms = UiKit.Rect("Message", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1400, 60));
            message = UiKit.Text(ms, "", 34, UiKit.Gold);
            var dbg = UiKit.Rect("Debug", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(900, 400));
            debug = UiKit.Text(dbg, "", 24, Color.white, TextAnchor.UpperLeft);

            BuildDeathScreen();
            stats.Died += () => deathScreen.SetActive(true);
        }

        UnityEngine.UI.Image Icon(string name, float x, float y, float _)
        {
            var rt = UiKit.Rect(name, root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(x, y), new Vector2(9 * P, 9 * P));
            return UiKit.Image(rt, null);
        }

        void BuildDeathScreen()
        {
            var ds = UiKit.Stretch("Death", root);
            UiKit.Image(ds, null, new Color(0.45f, 0.02f, 0.02f, 0.55f), false, true);
            var t = UiKit.Center("Title", ds, new Vector2(1200, 120), new Vector2(0, 200));
            UiKit.Text(t, "Вы погибли!", 72, Color.white);
            UiKit.Button(ds, "Возродиться", new Vector2(600, 72), new Vector2(0, 20), () => { pc.Respawn(); deathScreen.SetActive(false); });
            UiKit.Button(ds, "В главное меню", new Vector2(600, 72), new Vector2(0, -70), () => GameBootstrap.ReturnToMenu());
            deathScreen = ds.gameObject;
            deathScreen.SetActive(false);
        }

        public void ShowMessage(string text, float time = 3f) { message.text = text; msgTimer = time; }
        public bool DeathShown => deathScreen != null && deathScreen.activeSelf;

        void Update()
        {
            if (pc == null) return;
            var inv = pc.Inventory;
            bool creative = Session.Creative;

            for (int i = 0; i < 9; i++)
            {
                var s = inv[i];
                slotIcons[i].enabled = !s.IsEmpty;
                if (!s.IsEmpty) slotIcons[i].sprite = s.Def?.Icon;
                slotCounts[i].text = !s.IsEmpty && s.Count > 1 && !creative ? s.Count.ToString() : "";
            }
            selectFrame.anchoredPosition = new Vector2((11 + 20 * pc.Selected) * P, 0);
            if (pc.Selected != lastSelected)
            {
                lastSelected = pc.Selected; nameTimer = 2.2f;
                var s = inv[pc.Selected];
                itemName.text = s.IsEmpty ? "" : s.Def?.Name;
            }
            nameTimer -= Time.deltaTime;
            itemName.color = new Color(1, 0.95f, 0.85f, Mathf.Clamp01(nameTimer));
            msgTimer -= Time.deltaTime;
            message.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, Mathf.Clamp01(msgTimer));

            // stats rows (hidden in creative, like Minecraft)
            for (int i = 0; i < 10; i++)
            {
                hearts[i].enabled = hunger[i].enabled = !creative;
                float h = stats.Health - i * 2, f = stats.Hunger - i * 2, a = stats.Armor - i * 2, b = stats.Air - i;
                hearts[i].sprite = UiKit.Sprite(h >= 2 ? "UI_HeartFull" : h >= 1 ? "UI_HeartHalf" : "UI_HeartEmpty");
                hunger[i].sprite = UiKit.Sprite(f >= 2 ? "UI_HungerFull" : f >= 1 ? "UI_HungerHalf" : "UI_HungerEmpty");
                armor[i].enabled = !creative && stats.Armor > 0;
                armor[i].sprite = UiKit.Sprite(a >= 2 ? "UI_ArmorFull" : a >= 1 ? "UI_ArmorHalf" : "UI_ArmorEmpty");
                air[i].enabled = !creative && stats.Air < PlayerStats.MaxAir - 0.01f && b > 0;
                // low health wobble
                if (stats.Health <= 4) hearts[i].rectTransform.anchoredPosition = new Vector2(hearts[i].rectTransform.anchoredPosition.x, (2 + 22 + 9) * P + Mathf.Sin(Time.time * 20 + i) * 2);
            }
            xpFill.fillAmount = stats.Xp;
            xpLevel.text = stats.Level > 0 ? stats.Level.ToString() : "";
            hurt.color = new Color(0.8f, 0, 0, stats.HurtFlash * 0.6f);

            // mining progress
            breakBar.gameObject.SetActive(pc.BreakProgress > 0.01f);
            ((RectTransform)breakFill.transform).anchorMax = new Vector2(pc.BreakProgress, 1);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) showDebug = !showDebug;
            if (showDebug)
            {
                var p = pc.transform.position; var w = World.Instance;
                int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
                var biome = w.Terrain.GetBiome(x, z, w.Terrain.Height(x, z, out _));
                debug.text = $"Kazakh Mine — {Session.Current?.Name}  сид {w.Seed}\nXYZ: {p.x:0.0} / {p.y:0.0} / {p.z:0.0}\nБиом: {biome}\n" +
                             $"FPS: {1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f):0}\nЖивотных: {Animal.All.Count}  Жителей: {Villager.All.Count}\n" +
                             $"Время суток: {(WorldVisuals.Instance != null ? WorldVisuals.Instance.TimeOfDay * 24f : 0):0.0} ч";
            }
            else debug.text = "";
        }
    }
}
