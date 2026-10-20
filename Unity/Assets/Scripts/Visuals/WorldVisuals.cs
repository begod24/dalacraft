using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dala
{
    /// <summary>
    /// "Minecraft with shaders" look: day/night cycle, sun with soft shadows, procedural sky with clouds,
    /// atmospheric fog, wind, and a URP post-processing stack. Drives the shader globals used by
    /// Dala/VoxelLit, Dala/Foliage, Dala/Water and Dala/Sky.
    /// </summary>
    public class WorldVisuals : MonoBehaviour
    {
        public static WorldVisuals Instance { get; private set; }

        [Range(0, 1)] public float TimeOfDay = 0.32f;      // 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
        public float DayLengthMinutes = 24f;
        public float SunAzimuth = 35f;

        // Set by Weather
        [Range(0, 1)] public float CloudCover = 0.4f;
        [Range(0, 1)] public float Overcast;               // darkens and greys everything (rain / storm)
        [Range(0, 2)] public float Wind = 0.6f;
        public Vector2 WindDir = new(0.8f, 0.6f);
        public float FogDensityBoost;

        public Light Sun { get; private set; }
        Camera cam;
        Material sky;
        ColorAdjustments colorAdj;
        Bloom bloom;
        float cloudTime;

        static readonly int SunDirId = Shader.PropertyToID("_SunDir"), ZenithId = Shader.PropertyToID("_SkyZenith"),
            HorizonId = Shader.PropertyToID("_SkyHorizon"), SunColorId = Shader.PropertyToID("_SunColor"),
            CloudLitId = Shader.PropertyToID("_CloudLit"), CloudShadeId = Shader.PropertyToID("_CloudShade"),
            CloudCoverId = Shader.PropertyToID("_CloudCover"), NightId = Shader.PropertyToID("_Night"), CloudTimeId = Shader.PropertyToID("_CloudTime"),
            AmbSkyId = Shader.PropertyToID("_AmbientSky"), AmbGroundId = Shader.PropertyToID("_AmbientGround"),
            WindDirId = Shader.PropertyToID("_WindDir"), WindStrengthId = Shader.PropertyToID("_WindStrength");

        // Palette keyed by sun elevation
        static readonly Color DayZenith = new(0.24f, 0.47f, 0.86f), DayHorizon = new(0.68f, 0.81f, 0.95f);
        static readonly Color SetZenith = new(0.26f, 0.30f, 0.55f), SetHorizon = new(1.00f, 0.56f, 0.30f);
        static readonly Color NightZenith = new(0.01f, 0.02f, 0.06f), NightHorizon = new(0.05f, 0.07f, 0.14f);
        static readonly Color SunDay = new(1.00f, 0.95f, 0.86f), SunSet = new(1.00f, 0.52f, 0.25f);

        public void Init(Camera camera)
        {
            Instance = this;
            cam = camera;

            // Sun: reuse the scene's directional light if there is one
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { Sun = l; break; }
            if (Sun == null) Sun = new GameObject("Sun").AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.9f;
            Sun.shadowBias = 0.05f;
            Sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = Sun;

            sky = new Material(Shader.Find("Dala/Sky")) { name = "DalaSky" };
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthTexture = true;    // water depth
            data.requiresColorTexture = true;    // water refraction
            data.renderShadows = true;

            BuildPostFx();
            Apply(0f);
        }

        Volume postVolume;
        bool windOn = true;

        /// <summary>Applies GameSettings (shadows, post-processing, wind, day length).</summary>
        public void ApplySettings()
        {
            if (Session.InGame) DayLengthMinutes = GameSettings.DayLengthMinutes;
            if (postVolume != null) postVolume.enabled = GameSettings.PostFx;
            windOn = GameSettings.GrassWind;
            if (Sun != null)
            {
                Sun.shadows = GameSettings.ShadowQuality == 0 ? LightShadows.None : GameSettings.ShadowQuality == 1 ? LightShadows.Hard : LightShadows.Soft;
                Sun.shadowResolution = GameSettings.ShadowQuality == 1 ? UnityEngine.Rendering.LightShadowResolution.Low : UnityEngine.Rendering.LightShadowResolution.VeryHigh;
            }
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = GameSettings.PostFx;
        }

        void BuildPostFx()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            var vol = go.AddComponent<Volume>();
            postVolume = vol;
            vol.isGlobal = true;
            vol.priority = 50;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.82f)); bloom.highQualityFiltering.Override(true);
            colorAdj = p.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.45f); colorAdj.contrast.Override(14f); colorAdj.saturation.Override(12f);
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(6f); wb.tint.Override(2f);
            var st = p.Add<SplitToning>(true);
            st.shadows.Override(new Color(0.42f, 0.50f, 0.62f)); st.highlights.Override(new Color(0.70f, 0.60f, 0.45f)); st.balance.Override(-15f);
            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.98f, 0.99f, 1.02f, -0.02f));
            lgg.gain.Override(new Vector4(1.02f, 1.0f, 0.97f, 0.03f));
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.24f); vig.smoothness.Override(0.45f);
            var ca = p.Add<ChromaticAberration>(true); ca.intensity.Override(0.04f);
            var grain = p.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.08f);
            vol.profile = p;
        }

        void Update()
        {
            TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime / (DayLengthMinutes * 60f), 1f);
            cloudTime += Time.deltaTime * (0.6f + Wind);
            Apply(Time.deltaTime);
        }

        public float SunElevation { get; private set; }

        void Apply(float dt)
        {
            float ang = (TimeOfDay - 0.25f) * Mathf.PI * 2f;
            Vector3 sunDir = Quaternion.AngleAxis(SunAzimuth, Vector3.up) * new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0.28f).normalized;
            float elev = sunDir.y;
            SunElevation = elev;

            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.25f, elev));
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(elev - 0.03f) / 0.22f);
            float night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, -0.02f, elev));

            Color zenith = Color.Lerp(NightZenith, DayZenith, day);
            zenith = Color.Lerp(zenith, SetZenith, sunset * 0.45f);
            Color horizon = Color.Lerp(NightHorizon, DayHorizon, day);
            horizon = Color.Lerp(horizon, SetHorizon, sunset * 0.75f);
            Color sunCol = Color.Lerp(SunDay, SunSet, sunset);

            // weather greys the sky
            Color grey = new(0.55f, 0.58f, 0.62f);
            zenith = Color.Lerp(zenith, grey * (0.25f + 0.75f * day), Overcast * 0.75f);
            horizon = Color.Lerp(horizon, grey * (0.3f + 0.7f * day), Overcast * 0.7f);

            // Sun or moon drives the directional light
            bool moon = elev < -0.04f;
            Vector3 lightDir = moon ? -sunDir : sunDir;
            Sun.transform.rotation = Quaternion.LookRotation(-lightDir);
            float sunI = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, 0.18f, elev)) * 1.9f;
            float moonI = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, -0.25f, elev)) * 0.22f;
            Sun.intensity = (moon ? moonI : sunI) * (1f - Overcast * 0.65f);
            Sun.color = moon ? new Color(0.62f, 0.72f, 1f) : sunCol;
            Sun.shadowStrength = Mathf.Lerp(0.9f, 0.35f, Overcast);

            // Ambient: sky light from above, bounce from the ground
            Color ambSky = Color.Lerp(zenith, horizon, 0.45f) * (0.55f + 0.25f * day) + new Color(0.03f, 0.035f, 0.05f);
            Color ambGround = Color.Lerp(new Color(0.06f, 0.06f, 0.08f), new Color(0.36f, 0.31f, 0.24f), day);
            Shader.SetGlobalColor(AmbSkyId, ambSky);
            Shader.SetGlobalColor(AmbGroundId, ambGround);
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = horizon * 0.6f;
            RenderSettings.ambientGroundColor = ambGround;

            // Sky shader
            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalColor(ZenithId, zenith);
            Shader.SetGlobalColor(HorizonId, horizon);
            Shader.SetGlobalColor(SunColorId, sunCol * Mathf.Lerp(1f, 0.35f, Overcast));
            Shader.SetGlobalColor(CloudLitId, Color.Lerp(new Color(0.12f, 0.13f, 0.18f), Color.Lerp(new Color(1f, 0.98f, 0.95f), new Color(1f, 0.75f, 0.6f), sunset), day) * (1f - Overcast * 0.45f));
            Shader.SetGlobalColor(CloudShadeId, Color.Lerp(new Color(0.05f, 0.06f, 0.09f), new Color(0.48f, 0.50f, 0.58f), day) * (1f - Overcast * 0.35f));
            Shader.SetGlobalFloat(CloudCoverId, Mathf.Clamp01(CloudCover + Overcast * 0.6f));
            Shader.SetGlobalFloat(NightId, night * (1f - Overcast));
            Shader.SetGlobalFloat(CloudTimeId, cloudTime);

            // Wind
            Vector2 wd = WindDir.normalized;
            Shader.SetGlobalVector(WindDirId, new Vector4(wd.x, 0f, wd.y, 0f));
            Shader.SetGlobalFloat(WindStrengthId, windOn ? Wind : 0f);

            // Fog matches the horizon so terrain melts into the sky
            RenderSettings.fogColor = horizon;
            float view = World.Instance != null ? World.Instance.ViewRadius * Chunk.Size : 96f;
            RenderSettings.fogStartDistance = Mathf.Lerp(view * 0.45f, 8f, Mathf.Clamp01(Overcast * 0.6f + FogDensityBoost));
            RenderSettings.fogEndDistance = Mathf.Lerp(view * 1.05f, 60f, Mathf.Clamp01(Overcast * 0.5f + FogDensityBoost));
            if (cam != null) cam.backgroundColor = horizon;

            if (colorAdj != null)
            {
                colorAdj.postExposure.value = Mathf.Lerp(0.9f, 0.45f, day) - Overcast * 0.1f;
                colorAdj.saturation.value = Mathf.Lerp(-10f, 12f, day) - Overcast * 18f;
                colorAdj.colorFilter.value = Color.Lerp(new Color(0.75f, 0.82f, 1f), Color.Lerp(Color.white, new Color(1f, 0.9f, 0.8f), sunset * 0.6f), day);
            }
            if (bloom != null) bloom.intensity.value = Mathf.Lerp(0.6f, 0.95f, day) + sunset * 0.4f;
        }
    }
}
