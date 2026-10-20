using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Living fire for campfires / torches: animated pixel flames (8-frame flipbook from Blender), rising embers
    /// with turbulence, wind-blown smoke, a soft heat glow and a flickering warm point light.
    /// </summary>
    public class FireVfx : MonoBehaviour
    {
        public float Scale = 1f;
        Light glow;
        float seed;

        static Material flameMat;

        void Start()
        {
            seed = Random.value * 100f;
            flameMat ??= Vfx.Material(Vfx.FireTexture, false, 2.6f);

            Flames(); Embers(); Smoke(); HeatGlow();

            glow = new GameObject("FireLight").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0, 0.6f * Scale, 0);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.58f, 0.24f);
            glow.range = 8f * Scale;
            glow.intensity = 3f;
            glow.shadows = LightShadows.None;
        }

        void Flames()
        {
            var ps = Vfx.Create("Flames", transform, flameMat, 64);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.45f * Scale, 0.65f * Scale);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.9f * Scale, 1.3f * Scale);
            main.startSizeZ = 1f;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.16f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            var ts = ps.textureSheetAnimation;
            ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid; ts.numTilesX = 8; ts.numTilesY = 1;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
            ts.cycleCount = 2;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(Color.white, 0.12f, 0.6f);
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.35f)));
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.VerticalBillboard; r.sortingFudge = -1;
            ps.Play();
        }

        void Embers()
        {
            var ps = Vfx.Create("Embers", transform, Vfx.Tile(Vfx.Ember, true, 5f), 80);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * Scale, 0.12f * Scale);
            main.gravityModifier = -0.08f;
            var em = ps.emission; em.rateOverTime = 7f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.5f, 3, 6, 0, 2.7f) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18; sh.radius = 0.15f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            var n = ps.noise; n.enabled = true; n.strength = 0.9f; n.frequency = 1.4f; n.scrollSpeed = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
        }

        void Smoke()
        {
            var ps = Vfx.Create("Smoke", transform, Vfx.Tile(Vfx.Smoke, false), 60);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * Scale, 0.6f * Scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = ps.emission; em.rateOverTime = 3.5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.12f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            sh.position = new Vector3(0, 0.7f * Scale, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 3.5f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(new Color(0.55f, 0.53f, 0.52f), 0.15f, 0.5f, 0.38f);
            ps.Play();
        }

        void HeatGlow()
        {
            var ps = Vfx.Create("HeatGlow", transform, Vfx.Tile(Vfx.Glow, true, 1.6f), 4);
            var main = ps.main;
            main.startLifetime = 1.2f; main.startSpeed = 0f; main.startSize = 1.6f * Scale;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 2.5f;
            var sh = ps.shape; sh.enabled = false;
            ps.transform.localPosition = new Vector3(0, 0.35f * Scale, 0);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(new Color(1f, 0.6f, 0.25f), 0.3f, 0.7f, 0.6f);
            ps.Play();
        }

        void Update()
        {
            if (glow == null) return;
            float t = Time.time * 7f + seed;
            float flicker = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 5f) * 0.3f;
            glow.intensity = (2.2f + flicker * 1.6f) * Scale;
            glow.transform.localPosition = new Vector3((flicker - 0.5f) * 0.08f, 0.6f * Scale + flicker * 0.1f, 0);
        }
    }
}
