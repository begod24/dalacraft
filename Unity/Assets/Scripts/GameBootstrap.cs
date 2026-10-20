using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Entry point: builds the main menu (live biome panorama) on start; StartGame / ReturnToMenu swap between
    /// the menu session and a game session without reloading the scene. The scene itself needs no setup.
    /// </summary>
    public static class GameBootstrap
    {
        static GameObject sessionRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            GameSettings.Load();
            BuildMenu();
        }

        static void Teardown()
        {
            Time.timeScale = 1f;
            Session.Paused = false;
            foreach (var a in Animal.All.ToArray()) if (a != null) Object.Destroy(a.gameObject);
            foreach (var v in Villager.All.ToArray()) if (v != null) Object.Destroy(v.gameObject);
            Animal.All.Clear(); Villager.All.Clear();
            if (sessionRoot != null) Object.Destroy(sessionRoot);
            sessionRoot = null;
        }

        static Camera MakeCamera(Transform parent)
        {
            var camGo = new GameObject("Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(parent, false);
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 700f; cam.nearClipPlane = 0.05f; cam.fieldOfView = GameSettings.Fov;
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        public static void BuildMenu()
        {
            Teardown();
            Session.Current = null;
            sessionRoot = new GameObject("MenuSession");
            var cam = MakeCamera(sessionRoot.transform);

            var world = new GameObject("World").AddComponent<World>();   // Awake uses the default seed
            world.transform.SetParent(sessionRoot.transform);
            world.ViewRadius = Mathf.Min(GameSettings.RenderDistance, 6);
            world.MeshPerFrame = 4; world.DataPerFrame = 4;

            var vis = new GameObject("Visuals").AddComponent<WorldVisuals>();
            vis.transform.SetParent(sessionRoot.transform);
            vis.Init(cam);
            vis.DayLengthMinutes = 1000f;   // time stands still on each panorama shot

            var pano = new GameObject("Panorama").AddComponent<MenuPanorama>();
            pano.transform.SetParent(sessionRoot.transform);
            pano.Init(world, cam);

            var menu = new GameObject("MainMenuUI").AddComponent<MainMenu>();
            menu.transform.SetParent(sessionRoot.transform);
            menu.Init(pano);
            GameSettings.Apply();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public static void StartGame(WorldInfo info)
        {
            Teardown();
            Session.Current = info;
            sessionRoot = new GameObject("GameSession");
            var root = sessionRoot.transform;

            var player = new GameObject("Player");
            player.transform.SetParent(root);
            player.transform.position = new Vector3(8, 100, 8);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.6f;
            var cam = MakeCamera(player.transform);
            cam.transform.localPosition = new Vector3(0, 1.62f, 0);
            var pc = player.AddComponent<PlayerController>();
            pc.Cam = cam;
            pc.Avatar = player.AddComponent<PlayerAvatar>();

            var worldGo = new GameObject("World");
            worldGo.SetActive(false);                       // set the seed before Awake runs
            worldGo.transform.SetParent(root);
            var world = worldGo.AddComponent<World>();
            world.Seed = info.Seed;
            world.Target = player.transform;
            worldGo.SetActive(true);

            var vis = new GameObject("Visuals").AddComponent<WorldVisuals>();
            vis.transform.SetParent(root);
            vis.Init(cam);
            var weather = new GameObject("Weather").AddComponent<Weather>();
            weather.transform.SetParent(root);
            weather.Init(cam.transform);

            var ui = new GameObject("UI"); ui.transform.SetParent(root);
            ui.AddComponent<Hud>().Init(pc);
            ui.AddComponent<InventoryScreen>().Init(pc);
            ui.AddComponent<PauseMenu>().Init();
            GameSettings.Apply();
        }

        public static void ReturnToMenu() => BuildMenu();
    }
}
