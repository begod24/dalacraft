using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Main menu over a live, slowly rotating panorama that tours the biomes of a showcase world.</summary>
    public class MainMenu : MonoBehaviour
    {
        RectTransform root, screen;
        UnityEngine.UI.Image fade;
        UnityEngine.UI.Text splash;
        MenuPanorama pano;

        static readonly string[] Splashes =
        {
            "Сәлем, Дала!", "Жеті ата!", "Кумыс на столе!", "Беркут в небе!", "Ковыль шумит!", "Бауырсаки готовы!",
            "Алатау зовёт!", "Домбыра играет!", "Тюльпаны цветут!", "Ат — ер қанаты!", "Ешқашан берілме!", "Алтын адам ждёт!",
        };

        public void Init(MenuPanorama panorama)
        {
            pano = panorama;
            var c = UiKit.Canvas("MainMenu", 30, transform);
            root = (RectTransform)c.transform;
            // soft vignette so text stays readable over the panorama
            var vg = UiKit.Stretch("Shade", root); UiKit.Image(vg, null, new Color(0.02f, 0.02f, 0.04f, 0.25f));
            var bottom = UiKit.Rect("Bottom", root, Vector2.zero, new Vector2(1, 0.35f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            UiKit.Image(bottom, null, new Color(0, 0, 0, 0.25f));
            screen = UiKit.Stretch("Screen", root);
            var fd = UiKit.Stretch("Fade", root); fade = UiKit.Image(fd, null, Color.black);
            pano.Fade = fade;

            var ver = UiKit.Rect("Version", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(16, 10), new Vector2(700, 40));
            UiKit.Text(ver, "Kazakh Mine — альфа 0.1", 24, Color.white, TextAnchor.LowerLeft);
            var cr = UiKit.Rect("Credits", root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 10), new Vector2(700, 40));
            UiKit.Text(cr, "Сделано в Казахстане", 24, Color.white, TextAnchor.LowerRight);
            Home();
        }

        void Clear() { foreach (Transform t in screen) Destroy(t.gameObject); splash = null; }

        void Logo(float y = 300)
        {
            var lg = UiKit.Center("Logo", screen, new Vector2(256 * 3.4f, 64 * 3.4f), new Vector2(0, y));
            UiKit.Image(lg, "UI_Logo");
            var sp = UiKit.Center("Splash", screen, new Vector2(600, 60), new Vector2(330, y - 70));
            sp.localRotation = Quaternion.Euler(0, 0, 18);
            splash = UiKit.Text(sp, Splashes[Random.Range(0, Splashes.Length)], 34, new Color(1f, 0.95f, 0.2f));
        }

        // ------------------------------------------------------------------ screens
        void Home()
        {
            Clear(); Logo();
            UiKit.Button(screen, "Одиночная игра", new Vector2(640, 76), new Vector2(0, 40), Worlds);
            UiKit.Button(screen, "По сети (скоро)", new Vector2(640, 76), new Vector2(0, -50), null, false);
            UiKit.Button(screen, "Настройки", new Vector2(310, 76), new Vector2(-165, -140), () => Settings(Home));
            UiKit.Button(screen, "Выход", new Vector2(310, 76), new Vector2(165, -140), Quit, true, "UI_ButtonBlue");
        }

        void Worlds()
        {
            Clear();
            var panel = UiKit.Panel(screen, new Vector2(900, 760)).rectTransform;
            var t = UiKit.Center("Title", panel, new Vector2(800, 60), new Vector2(0, 330)); UiKit.Text(t, "Выбор мира", 40, UiKit.Ink, TextAnchor.MiddleCenter, false);
            var list = WorldList.Load();
            if (list.Count == 0)
            {
                var e = UiKit.Center("Empty", panel, new Vector2(800, 60), new Vector2(0, 120));
                UiKit.Text(e, "Пока нет миров — создайте первый!", 30, UiKit.Ink, TextAnchor.MiddleCenter, false);
            }
            for (int i = 0; i < Mathf.Min(list.Count, 6); i++)
            {
                var w = list[i];
                float y = 250 - i * 82;
                string mode = w.Mode == GameMode.Creative ? "Творческий" : "Выживание";
                UiKit.Button(panel, $"{w.Name}  ·  {mode}  ·  сид {w.Seed}", new Vector2(640, 70), new Vector2(-80, y), () => GameBootstrap.StartGame(w));
                UiKit.Button(panel, "X", new Vector2(80, 70), new Vector2(330, y), () => { WorldList.Remove(w.Name); Worlds(); }, true, "UI_ButtonBlue");
            }
            UiKit.Button(panel, "Создать новый мир", new Vector2(520, 72), new Vector2(-140, -300), CreateWorld);
            UiKit.Button(panel, "Назад", new Vector2(260, 72), new Vector2(270, -300), Home, true, "UI_ButtonBlue");
        }

        void CreateWorld()
        {
            Clear();
            var info = new WorldInfo { Name = "Мой мир " + (WorldList.Load().Count + 1), Seed = Random.Range(1, 999999) };
            var panel = UiKit.Panel(screen, new Vector2(900, 760)).rectTransform;
            var t = UiKit.Center("Title", panel, new Vector2(800, 60), new Vector2(0, 330)); UiKit.Text(t, "Создание мира", 40, UiKit.Ink, TextAnchor.MiddleCenter, false);
            Label(panel, "Название мира", 250);
            var name = UiKit.Input(panel, "Название", info.Name, new Vector2(0, 200), new Vector2(700, 64));
            Label(panel, "Сид (пусто — случайный)", 130);
            var seed = UiKit.Input(panel, "Случайный", "", new Vector2(0, 80), new Vector2(700, 64));
            UnityEngine.UI.Button modeBtn = null;
            System.Action refresh = null;
            modeBtn = UiKit.Button(panel, "", new Vector2(700, 70), new Vector2(0, -20), () => { info.Mode = info.Mode == GameMode.Survival ? GameMode.Creative : GameMode.Survival; refresh(); });
            refresh = () => modeBtn.GetComponentInChildren<UnityEngine.UI.Text>().text = "Режим: " + (info.Mode == GameMode.Survival ? "Выживание" : "Творческий");
            refresh();
            UiKit.Toggle(panel, "Аулы и жители", info.Villages, new Vector2(0, -110), v => info.Villages = v);
            UiKit.Button(panel, "Создать мир", new Vector2(420, 76), new Vector2(-150, -280), () =>
            {
                info.Name = string.IsNullOrWhiteSpace(name.text) ? info.Name : name.text.Trim();
                if (!string.IsNullOrWhiteSpace(seed.text))
                    info.Seed = int.TryParse(seed.text.Trim(), out int s) ? s : seed.text.Trim().GetHashCode();
                info.Created = System.DateTime.Now.Ticks;
                WorldList.Add(info);
                GameBootstrap.StartGame(info);
            });
            UiKit.Button(panel, "Отмена", new Vector2(260, 76), new Vector2(240, -280), Worlds, true, "UI_ButtonBlue");
        }

        static void Label(RectTransform p, string text, float y)
        {
            var r = UiKit.Center("Label", p, new Vector2(700, 40), new Vector2(0, y));
            UiKit.Text(r, text, 28, UiKit.Ink, TextAnchor.MiddleLeft, false);
        }

        void Settings(System.Action back)
        {
            Clear();
            SettingsPanel.Build(screen, () => back());
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (splash != null)
            {
                float s = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 0.08f;
                splash.rectTransform.localScale = new Vector3(s, s, 1);
            }
        }
    }

    /// <summary>Settings UI shared by the main menu and the pause menu.</summary>
    public static class SettingsPanel
    {
        public static RectTransform Build(Transform parent, System.Action onDone)
        {
            var panel = UiKit.Panel(parent, new Vector2(1400, 820)).rectTransform;
            var t = UiKit.Center("Title", panel, new Vector2(800, 60), new Vector2(0, 360)); UiKit.Text(t, "Настройки", 40, UiKit.Ink, TextAnchor.MiddleCenter, false);
            float lx = -340, rx = 340;
            UiKit.Slider(panel, "Дальность прорисовки", 3, 12, GameSettings.RenderDistance, new Vector2(lx, 260), v => $"{v:0} чанков", v => GameSettings.RenderDistance = Mathf.RoundToInt(v), true);
            UiKit.Slider(panel, "Поле зрения", 50, 110, GameSettings.Fov, new Vector2(lx, 170), v => $"{v:0}°", v => GameSettings.Fov = v, true);
            UiKit.Slider(panel, "Чувствительность мыши", 0.03f, 0.4f, GameSettings.MouseSensitivity, new Vector2(lx, 80), v => $"{v * 100:0}%", v => GameSettings.MouseSensitivity = v);
            UiKit.Slider(panel, "Длина суток", 5, 60, GameSettings.DayLengthMinutes, new Vector2(lx, -10), v => $"{v:0} мин", v => GameSettings.DayLengthMinutes = v, true);
            UiKit.Slider(panel, "Громкость", 0, 1, GameSettings.MasterVolume, new Vector2(lx, -100), v => $"{v * 100:0}%", v => GameSettings.MasterVolume = v);

            UnityEngine.UI.Button sh = null;
            string ShadowName() => GameSettings.ShadowQuality switch { 0 => "выкл", 1 => "низкие", _ => "высокие" };
            sh = UiKit.Button(panel, "Тени: " + ShadowName(), new Vector2(620, 60), new Vector2(rx, 260), () =>
            {
                GameSettings.ShadowQuality = (GameSettings.ShadowQuality + 1) % 3;
                sh.GetComponentInChildren<UnityEngine.UI.Text>().text = "Тени: " + ShadowName();
            });
            UiKit.Toggle(panel, "Пост-обработка (шейдеры)", GameSettings.PostFx, new Vector2(rx, 175), v => GameSettings.PostFx = v);
            UiKit.Toggle(panel, "Трава качается на ветру", GameSettings.GrassWind, new Vector2(rx, 105), v => GameSettings.GrassWind = v);
            UiKit.Toggle(panel, "Полный экран", GameSettings.Fullscreen, new Vector2(rx, 35), v => GameSettings.Fullscreen = v);
            UiKit.Toggle(panel, "Вертикальная синхронизация", GameSettings.VSync, new Vector2(rx, -35), v => GameSettings.VSync = v);
            var hint = UiKit.Center("Controls", panel, new Vector2(1300, 140), new Vector2(0, -215));
            UiKit.Text(hint, "WASD — ходьба · Space — прыжок · Shift — бег · E — инвентарь · F — сесть на коня/верблюда · ПКМ — использовать/поставить\n" +
                             "ЛКМ (держать) — ломать/бить · 1-9/колесо — слот · Q — выбросить · F5 — вид · F6 — погода · F3 — отладка · Esc — пауза", 24, UiKit.Ink, TextAnchor.MiddleCenter, false);
            UiKit.Button(panel, "Готово", new Vector2(420, 72), new Vector2(0, -340), () => { GameSettings.Save(); GameSettings.Apply(); onDone(); });
            return panel;
        }
    }

    /// <summary>In-game pause (Esc): resume, settings, save & quit to menu.</summary>
    public class PauseMenu : MonoBehaviour
    {
        RectTransform root, screen;
        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public static PauseMenu Instance { get; private set; }

        public void Init()
        {
            Instance = this;
            var c = UiKit.Canvas("PauseMenu", 40, transform);
            root = UiKit.Stretch("Root", c.transform);
            UiKit.Image(root, null, new Color(0, 0, 0, 0.55f), false, true);
            screen = UiKit.Stretch("Screen", root);
            root.gameObject.SetActive(false);
        }

        public void Open() { Session.Paused = true; Time.timeScale = 0f; root.gameObject.SetActive(true); Main(); }
        public void Close() { Session.Paused = false; Time.timeScale = 1f; root.gameObject.SetActive(false); }

        void Main()
        {
            foreach (Transform t in screen) Destroy(t.gameObject);
            var tl = UiKit.Center("Title", screen, new Vector2(800, 80), new Vector2(0, 260)); UiKit.Text(tl, "Пауза", 54, Color.white);
            UiKit.Button(screen, "Вернуться в игру", new Vector2(640, 76), new Vector2(0, 110), Close);
            UiKit.Button(screen, "Настройки", new Vector2(640, 76), new Vector2(0, 20), () =>
            {
                foreach (Transform t in screen) Destroy(t.gameObject);
                SettingsPanel.Build(screen, Main);
            });
            UiKit.Button(screen, "По сети (скоро)", new Vector2(640, 76), new Vector2(0, -70), null, false);
            UiKit.Button(screen, "Сохранить и выйти в меню", new Vector2(640, 76), new Vector2(0, -160), () => { Close(); GameBootstrap.ReturnToMenu(); }, true, "UI_ButtonBlue");
        }
    }

    /// <summary>Menu background: a showcase world toured biome by biome with a slowly turning camera.</summary>
    public class MenuPanorama : MonoBehaviour
    {
        public UnityEngine.UI.Image Fade;
        public Transform Pivot;
        Camera cam;
        World world;
        readonly List<(Vector3 pos, float time, Biome biome)> spots = new();
        int index = -1;
        float timer, yaw, fadeAlpha = 1f;
        bool waiting;

        public void Init(World w, Camera camera)
        {
            world = w; cam = camera;
            Pivot = new GameObject("PanoramaPivot").transform;
            Pivot.SetParent(transform, false);
            cam.transform.SetParent(Pivot, false);
            cam.transform.localPosition = Vector3.zero;
            world.Target = Pivot;
            FindSpots();
            Next();
        }

        void FindSpots()
        {
            var t = world.Terrain;
            var want = new (Biome b, float time)[] { (Biome.Steppe, 0.27f), (Biome.Taiga, 0.36f), (Biome.Snowy, 0.42f), (Biome.BetpakDala, 0.70f),
                                                     (Biome.BirchGrove, 0.31f), (Biome.AppleForest, 0.45f), (Biome.Balkhash, 0.72f), (Biome.Altai, 0.3f) };
            var found = new HashSet<Biome>();
            for (int r = 0; r < 2600 && found.Count < want.Length; r += 40)
            {
                int n = Mathf.Max(1, r / 24);
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    int x = Mathf.RoundToInt(Mathf.Cos(a) * r), z = Mathf.RoundToInt(Mathf.Sin(a) * r);
                    int h = t.Height(x, z, out _);
                    var b = t.GetBiome(x, z, h);
                    if (found.Contains(b)) continue;
                    if (b == Biome.Balkhash && h > TerrainGenerator.SeaLevel - 1) continue;
                    foreach (var w in want)
                        if (w.b == b)
                        {
                            found.Add(b);
                            float y = Mathf.Max(h, TerrainGenerator.SeaLevel) + (b == Biome.Snowy || b == Biome.Taiga ? 8f : 12f);
                            spots.Add((new Vector3(x + 0.5f, y, z + 0.5f), w.time, b));
                        }
                }
            }
            if (spots.Count == 0) spots.Add((new Vector3(8, 60, 8), 0.3f, Biome.Steppe));
        }

        void Next()
        {
            index = (index + 1) % spots.Count;
            Pivot.position = spots[index].pos;
            yaw = Random.Range(0f, 360f);
            if (WorldVisuals.Instance != null) WorldVisuals.Instance.TimeOfDay = spots[index].time;
            waiting = true; timer = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            yaw += dt * 3.2f;
            Pivot.rotation = Quaternion.Euler(-6f + Mathf.Sin(Time.unscaledTime * 0.1f) * 3f, yaw, 0);
            if (waiting)
            {
                timer += dt;
                if (world.IsReady(Pivot.position) && timer > 2.5f) { waiting = false; timer = 0f; }
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, 1f, dt * 2f);
            }
            else
            {
                timer += dt;
                fadeAlpha = Mathf.MoveTowards(fadeAlpha, timer > 17f ? 1f : 0f, dt * (timer > 17f ? 1.2f : 0.7f));
                if (timer > 18f) Next();
            }
            if (Fade != null) Fade.color = new Color(0, 0, 0, fadeAlpha);
        }
    }
}
