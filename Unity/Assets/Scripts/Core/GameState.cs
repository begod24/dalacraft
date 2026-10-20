using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public enum GameMode { Survival, Creative }

    /// <summary>A world entry from the "create world" screen.</summary>
    [System.Serializable]
    public class WorldInfo
    {
        public string Name = "Новый мир";
        public int Seed;
        public GameMode Mode = GameMode.Survival;
        public bool Villages = true;
        public long Created;
    }

    /// <summary>Player settings, persisted in PlayerPrefs.</summary>
    public static class GameSettings
    {
        public static int RenderDistance = 6;      // chunks
        public static float Fov = 72f;
        public static float MouseSensitivity = 0.12f;
        public static int ShadowQuality = 2;        // 0 off, 1 low, 2 high
        public static bool PostFx = true;
        public static bool GrassWind = true;
        public static bool Fullscreen = true;
        public static bool VSync = true;
        public static float DayLengthMinutes = 24f;
        public static float MasterVolume = 0.8f;

        public static void Load()
        {
            RenderDistance = PlayerPrefs.GetInt("rd", RenderDistance);
            Fov = PlayerPrefs.GetFloat("fov", Fov);
            MouseSensitivity = PlayerPrefs.GetFloat("sens", MouseSensitivity);
            ShadowQuality = PlayerPrefs.GetInt("shadows", ShadowQuality);
            PostFx = PlayerPrefs.GetInt("postfx", 1) == 1;
            GrassWind = PlayerPrefs.GetInt("wind", 1) == 1;
            Fullscreen = PlayerPrefs.GetInt("fullscreen", 1) == 1;
            VSync = PlayerPrefs.GetInt("vsync", 1) == 1;
            DayLengthMinutes = PlayerPrefs.GetFloat("day", DayLengthMinutes);
            MasterVolume = PlayerPrefs.GetFloat("vol", MasterVolume);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("rd", RenderDistance); PlayerPrefs.SetFloat("fov", Fov); PlayerPrefs.SetFloat("sens", MouseSensitivity);
            PlayerPrefs.SetInt("shadows", ShadowQuality); PlayerPrefs.SetInt("postfx", PostFx ? 1 : 0); PlayerPrefs.SetInt("wind", GrassWind ? 1 : 0);
            PlayerPrefs.SetInt("fullscreen", Fullscreen ? 1 : 0); PlayerPrefs.SetInt("vsync", VSync ? 1 : 0);
            PlayerPrefs.SetFloat("day", DayLengthMinutes); PlayerPrefs.SetFloat("vol", MasterVolume);
            PlayerPrefs.Save();
        }

        /// <summary>Applies settings to the running game (camera, world, quality).</summary>
        public static void Apply()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Screen.fullScreen = Fullscreen;
            AudioListener.volume = MasterVolume;
            if (World.Instance != null && Session.InGame) World.Instance.ViewRadius = RenderDistance;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.LookSens = MouseSensitivity;
                if (PlayerController.Instance.Cam != null) PlayerController.Instance.Cam.fieldOfView = Fov;
            }
            if (WorldVisuals.Instance != null) WorldVisuals.Instance.ApplySettings();
        }
    }

    /// <summary>Saved list of created worlds (seed + settings; block edits are not persisted yet).</summary>
    public static class WorldList
    {
        [System.Serializable] class Wrapper { public List<WorldInfo> Worlds = new(); }

        public static List<WorldInfo> Load()
        {
            var json = PlayerPrefs.GetString("worlds", "");
            if (string.IsNullOrEmpty(json)) return new List<WorldInfo>();
            return JsonUtility.FromJson<Wrapper>(json)?.Worlds ?? new List<WorldInfo>();
        }

        public static void Add(WorldInfo w)
        {
            var list = Load();
            list.RemoveAll(x => x.Name == w.Name);
            list.Insert(0, w);
            PlayerPrefs.SetString("worlds", JsonUtility.ToJson(new Wrapper { Worlds = list }));
            PlayerPrefs.Save();
        }

        public static void Remove(string name)
        {
            var list = Load();
            list.RemoveAll(x => x.Name == name);
            PlayerPrefs.SetString("worlds", JsonUtility.ToJson(new Wrapper { Worlds = list }));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Global session state (menu vs. in-game).</summary>
    public static class Session
    {
        public static WorldInfo Current;
        public static bool InGame => Current != null;
        public static bool Creative => Current != null && Current.Mode == GameMode.Creative;
        public static bool Paused;
    }
}
