using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dala.EditorTools
{
    /// <summary>
    /// Renders stills of a generated world in the editor — no Play Mode: builds chunks around a point, sets up the game's
    /// sky / light / post-processing, renders each shot to PNG, then removes everything and restores the scene's render settings.
    /// </summary>
    public static class WorldPreview
    {
        public struct Shot
        {
            public string File; public Vector3 Position; public Vector3 LookAt;
            public Shot(string file, Vector3 position, Vector3 lookAt) { File = file; Position = position; LookAt = lookAt; }
        }

        /// <param name="shotsFromSun">Optional: builds shots once the sun direction (towards the sun, flattened) is known.</param>
        /// <param name="beforeShot">Optional: called with the camera and shot index right before each render (e.g. to pose hands).</param>
        public static void Render(int seed, Vector3 center, int radius, float timeOfDay, string folder,
                                  IEnumerable<Shot> shots, System.Func<Vector3, IEnumerable<Shot>> shotsFromSun = null, int width = 1280, int height = 720,
                                  System.Action<Camera, int> beforeShot = null)
        {
            // remember everything the game visuals touch
            var skybox = RenderSettings.skybox; var amode = RenderSettings.ambientMode; var probe = RenderSettings.ambientProbe;
            var asky = RenderSettings.ambientSkyColor; var aeq = RenderSettings.ambientEquatorColor; var agr = RenderSettings.ambientGroundColor;
            bool fog = RenderSettings.fog; var fm = RenderSettings.fogMode; var fc = RenderSettings.fogColor;
            float fs = RenderSettings.fogStartDistance, fe = RenderSettings.fogEndDistance; var sun = RenderSettings.sun;
            var lights = new List<(Light l, bool on)>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { lights.Add((l, l.enabled)); l.enabled = false; }

            var root = new GameObject("__WorldPreview") { hideFlags = HideFlags.DontSave };
            try
            {
                Village.ClearCache();
                BlockAtlas.Reset();
                new GameObject("PreviewSun").AddComponent<Light>().transform.SetParent(root.transform);
                root.GetComponentInChildren<Light>().type = LightType.Directional;
                var world = new GameObject("World").AddComponent<World>(); world.transform.SetParent(root.transform);
                world.Seed = seed;
                world.GeneratePreview(center, radius);
                var cam = new GameObject("Cam").AddComponent<Camera>(); cam.transform.SetParent(root.transform);
                cam.farClipPlane = 600; cam.fieldOfView = 70;
                var vis = new GameObject("Visuals").AddComponent<WorldVisuals>(); vis.transform.SetParent(root.transform);
                vis.TimeOfDay = timeOfDay; vis.CloudCover = 0.3f;
                vis.Init(cam);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                var list = new List<Shot>();
                if (shots != null) list.AddRange(shots);
                if (shotsFromSun != null)
                {
                    var toSun = -vis.Sun.transform.forward;
                    list.AddRange(shotsFromSun(new Vector3(toSun.x, 0, toSun.z).normalized));
                }

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    cam.transform.position = s.Position;
                    cam.transform.LookAt(s.LookAt);
                    beforeShot?.Invoke(cam, i);
                    cam.targetTexture = rt;
                    world.DrawFoliage(cam);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, s.File), tex.EncodeToPNG());
                }
                rt.Release();
            }
            finally
            {
                Object.DestroyImmediate(root);
                RenderSettings.skybox = skybox; RenderSettings.ambientMode = amode; RenderSettings.ambientProbe = probe;
                RenderSettings.ambientSkyColor = asky; RenderSettings.ambientEquatorColor = aeq; RenderSettings.ambientGroundColor = agr;
                RenderSettings.fog = fog; RenderSettings.fogMode = fm; RenderSettings.fogColor = fc;
                RenderSettings.fogStartDistance = fs; RenderSettings.fogEndDistance = fe; RenderSettings.sun = sun;
                foreach (var (l, on) in lights) if (l) l.enabled = on;
            }
        }
    }
}
