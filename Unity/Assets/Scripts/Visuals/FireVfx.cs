using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Living fire for campfires / torches / furnaces: two layers of pixel flames (bright core + outer tongues from the
    /// 8-frame Blender flipbook) that stay on the logs, glowing coals, rising embers, thin smoke and a flickering light.
    /// </summary>
    public class FireVfx : MonoBehaviour
    {
        public float Scale = 1f;
        /// <summary>Sit the fire on the model's log pile: centred on its bounds, base at 60% of its height.</summary>
        public bool FitToModel;
        /// <summary>Light radius in metres (default 8 × Scale); torches light a wider area than their tiny flame.</summary>
        public float LightRange = -1f;

        Light glow;
        float seed;
        Transform anchor;

        static Material flameMat, coreMat;

        void Start() => Build();

        /// <summary>Creates the particle systems and light (also usable from editor previews).</summary>
        public void Build()
        {
            if (glow != null) return;
            seed = Random.value * 100f;
            flameMat ??= Vfx.Material(Vfx.FireTexture, false, 1.9f);
            coreMat ??= Vfx.Material(Vfx.FireTexture, true, 1.6f);

            anchor = new GameObject("FireAnchor").transform;
            anchor.SetParent(transform, false);
            if (FitToModel) PlaceOnModel();

            Flames("Flames", flameMat, 0.11f, new Vector2(0.26f, 0.38f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.75f), 14f, Color.white);
            Flames("Core", coreMat, 0.05f, new Vector2(0.16f, 0.24f), new Vector2(0.3f, 0.45f), new Vector2(0.35f, 0.55f), 12f, new Color(1f, 0.85f, 0.55f));
            if (FitToModel) Coals();
            Embers(); Smoke();

            glow = new GameObject("FireLight").AddComponent<Light>();
            glow.transform.SetParent(anchor, false);
            glow.transform.localPosition = new Vector3(0, 0.45f * Scale, 0);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.58f, 0.24f);
            glow.range = LightRange > 0 ? LightRange : 8f * Scale;
            glow.intensity = 1.5f;            // only models see it; blocks get light levels
            glow.shadows = LightShadows.None;
        }

        /// <summary>The model is either on this object (world decor) or a sibling under the parent (block entities).</summary>
        void PlaceOnModel()
        {
            var rs = GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0 && transform.parent != null) rs = transform.parent.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            anchor.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.6f, b.center.z);
            // keep the flames inside the stone ring: scale by the pile's footprint (campfire model ≈ 0.88 m)
            Scale *= Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) / 0.88f, 0.4f, 2f);
        }

        /// <summary>One flame layer. Quads are pivoted at their centre, so the emitter is lifted by half the height to put the base on the logs.</summary>
        void Flames(string name, Material mat, float radius, Vector2 width, Vector2 height, Vector2 life, float rate, Color tint)
        {
            var ps = Vfx.Create(name, anchor, mat, 48);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f * Scale, 0.15f * Scale);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(width.x * Scale, width.y * Scale);
            main.startSizeY = new ParticleSystem.MinMaxCurve(height.x * Scale, height.y * Scale);
            main.startSizeZ = 1f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * Scale; sh.rotation = new Vector3(-90, 0, 0);
            sh.position = new Vector3(0, height.x * 0.5f * Scale, 0);
            var ts = ps.textureSheetAnimation;
            ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid; ts.numTilesX = 8; ts.numTilesY = 1;
            ts.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
            ts.cycleCount = 2;
            ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 7f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(tint, 0.15f, 0.55f);
            // grow in, then narrow to a tip — never wider than at birth, so tongues stay over the logs
            var size = ps.sizeOverLifetime; size.enabled = true; size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(0.25f, 1f), new Keyframe(1, 0.3f)));
            size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.6f)));
            size.z = new ParticleSystem.MinMaxCurve(1f, 1f);
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.VerticalBillboard; r.sortingFudge = -1;
            ps.Play();
        }

        void Coals()
        {
            var ps = Vfx.Create("Coals", anchor, Vfx.Tile(Vfx.Ember, true, 3f), 24);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * Scale, 0.08f * Scale);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            sh.position = new Vector3(0, -0.06f * Scale, 0);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(new Color(1f, 0.45f, 0.12f), 0.3f, 0.6f);
            ps.Play();
        }

        void Embers()
        {
            var ps = Vfx.Create("Embers", anchor, Vfx.Tile(Vfx.Ember, true, 5f), 60);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * Scale, 1.4f * Scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * Scale, 0.08f * Scale);
            main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 5f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.5f, 2, 5, 0, 2.7f) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 0.1f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            sh.position = new Vector3(0, 0.2f * Scale, 0);
            var n = ps.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 1.4f; n.scrollSpeed = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.6f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
        }

        void Smoke()
        {
            var ps = Vfx.Create("Smoke", anchor, Vfx.Tile(Vfx.Smoke, false), 60);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * Scale, 0.4f * Scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = ps.emission; em.rateOverTime = 3.5f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.08f * Scale; sh.rotation = new Vector3(-90, 0, 0);
            sh.position = new Vector3(0, 0.75f * Scale, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 2.2f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(new Color(0.55f, 0.53f, 0.52f), 0.15f, 0.45f, 0.22f);
            ps.Play();
        }

        void Update()
        {
            if (glow == null) return;
            float t = Time.time * 7f + seed;
            float flicker = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 5f) * 0.3f;
            glow.intensity = (2.2f + flicker * 1.6f) * Scale;
            glow.transform.localPosition = new Vector3((flicker - 0.5f) * 0.06f, 0.45f * Scale + flicker * 0.08f, 0);
        }
    }
}
