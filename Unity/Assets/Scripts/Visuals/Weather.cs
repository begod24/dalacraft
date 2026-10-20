using UnityEngine;

namespace Dala
{
    /// <summary>
    /// Weather around the camera: clear / cloudy / rain / storm / snow (mountains) / dust (desert), plus ambient
    /// particles — leaves blown from groves now and then, kovyl fluff on windy steppe, apricot petals, fireflies at night.
    /// Changes every few minutes depending on the biome under the player. F6 cycles weather manually.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        public enum Kind { Clear, Cloudy, Rain, Storm, Snow, Dust }
        public Kind Current = Kind.Clear;

        Transform follow;
        ParticleSystem rain, snow, dust, leavesGreen, leavesYellow, fluff, petals, fireflies;
        float timer, intensity, targetIntensity;
        float gustPhase;

        public void Init(Transform target)
        {
            follow = target;
            rain = MakeFalling("Rain", Vfx.Tile(Vfx.Rain, false, 1.2f), 3000, 0.9f, 1.3f, 16f, 0.05f, 0.08f, true);
            var rr = rain.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.06f; rr.lengthScale = 1f;
            snow = MakeFalling("Snow", Vfx.Tile(Vfx.Snow, false, 1.3f), 2500, 6f, 9f, 1.4f, 0.08f, 0.14f, false);
            var sn = snow.noise; sn.enabled = true; sn.strength = 0.8f; sn.frequency = 0.4f;
            dust = MakeAmbient("Dust", Vfx.Tile(Vfx.Dust, false), 300, 5f, 8f, 1.6f, 3.5f, 0.28f);
            leavesGreen = MakeAmbient("LeavesGreen", Vfx.Tile(Vfx.LeafGreen, false), 120, 6f, 10f, 0.18f, 0.28f, 1f, true);
            leavesYellow = MakeAmbient("LeavesYellow", Vfx.Tile(Vfx.LeafYellow, false), 120, 6f, 10f, 0.18f, 0.28f, 1f, true);
            fluff = MakeAmbient("KovylFluff", Vfx.Tile(Vfx.Fluff, false, 1.3f), 120, 6f, 10f, 0.12f, 0.2f, 0.9f);
            petals = MakeAmbient("Petals", Vfx.Tile(Vfx.Petal, false, 1.2f), 150, 5f, 9f, 0.12f, 0.2f, 1f, true);
            fireflies = MakeAmbient("Fireflies", Vfx.Tile(Vfx.Firefly, true, 6f), 80, 3f, 6f, 0.08f, 0.12f, 1f);
            var fn = fireflies.noise; fn.enabled = true; fn.strength = 0.6f; fn.frequency = 0.5f;
            timer = 90f;
        }

        ParticleSystem MakeFalling(string name, Material mat, int max, float lifeMin, float lifeMax, float speed, float sizeMin, float sizeMax, bool collide)
        {
            var ps = Vfx.Create(name, transform, mat, max);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(44, 1, 44);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(-speed * 1.1f, -speed * 0.9f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var em = ps.emission; em.rateOverTime = 0;
            if (collide)
            {
                var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.mode = ParticleSystemCollisionMode.Collision3D;
                c.quality = ParticleSystemCollisionQuality.Low; c.lifetimeLoss = 1f; c.bounce = 0f;
            }
            ps.Play();
            return ps;
        }

        ParticleSystem MakeAmbient(string name, Material mat, int max, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float alpha, bool spin = false)
        {
            var ps = Vfx.Create(name, transform, mat, max);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = 0.01f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36, 8, 36);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.1f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var n = ps.noise; n.enabled = true; n.strength = 0.7f; n.frequency = 0.35f; n.scrollSpeed = 0.3f;
            if (spin) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f); }
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(Color.white, 0.15f, 0.8f, alpha);
            var em = ps.emission; em.rateOverTime = 0;
            ps.Play();
            return ps;
        }

        void SetWind(ParticleSystem ps, Vector2 wind, float scale)
        {
            var vel = ps.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x * scale * 0.7f, wind.x * scale * 1.3f);
            vel.z = new ParticleSystem.MinMaxCurve(wind.y * scale * 0.7f, wind.y * scale * 1.3f);
        }

        static void Rate(ParticleSystem ps, float r) { var em = ps.emission; em.rateOverTime = r; }

        Kind Pick(Biome b, float height)
        {
            float r = Random.value;
            switch (b)
            {
                case Biome.Snowy: return r < 0.55f ? Kind.Snow : (r < 0.8f ? Kind.Cloudy : Kind.Clear);
                case Biome.Taiga: return height > 70 && r < 0.35f ? Kind.Snow : (r < 0.35f ? Kind.Rain : (r < 0.65f ? Kind.Cloudy : Kind.Clear));
                case Biome.BetpakDala: return r < 0.3f ? Kind.Dust : (r < 0.4f ? Kind.Cloudy : Kind.Clear);
                default: return r < 0.15f ? Kind.Rain : (r < 0.22f ? Kind.Storm : (r < 0.5f ? Kind.Cloudy : Kind.Clear));
            }
        }

        void Update()
        {
            if (follow == null || WorldVisuals.Instance == null || World.Instance == null) return;
            var vis = WorldVisuals.Instance;
            var pos = follow.position;
            int wx = Mathf.FloorToInt(pos.x), wz = Mathf.FloorToInt(pos.z);
            var terrain = World.Instance.Terrain;
            int h = terrain.Height(wx, wz, out _);
            var biome = terrain.GetBiome(wx, wz, h);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f6Key.wasPressedThisFrame) { Current = (Kind)(((int)Current + 1) % 6); timer = 240f; }
            timer -= Time.deltaTime;
            if (timer <= 0) { Current = Pick(biome, pos.y); timer = Random.Range(150f, 360f); }
            // snow turns into rain in warm lowlands and vice versa
            Kind k = Current;
            if (k == Kind.Snow && biome != Biome.Snowy && biome != Biome.Taiga) k = Kind.Rain;
            if ((k == Kind.Rain || k == Kind.Storm) && (biome == Biome.Snowy || pos.y > 82)) k = Kind.Snow;
            if (k == Kind.Dust && biome != Biome.BetpakDala) k = Kind.Cloudy;

            targetIntensity = k == Kind.Clear ? 0f : 1f;
            intensity = Mathf.MoveTowards(intensity, targetIntensity, Time.deltaTime * 0.05f);

            // Wind gusts
            gustPhase += Time.deltaTime;
            float gust = Mathf.Clamp01(Mathf.PerlinNoise(gustPhase * 0.07f, 3.3f) * 1.6f - 0.3f);
            float baseWind = k == Kind.Storm ? 1.6f : k == Kind.Dust ? 1.4f : k == Kind.Rain ? 0.9f : 0.45f;
            vis.Wind = Mathf.Lerp(vis.Wind, baseWind + gust * 0.6f, Time.deltaTime * 0.5f);
            float ang = Mathf.PerlinNoise(gustPhase * 0.01f, 9.1f) * Mathf.PI * 2f;
            vis.WindDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 wind = vis.WindDir * vis.Wind;

            float cover = k switch { Kind.Clear => 0.3f, Kind.Cloudy => 0.65f, Kind.Rain => 0.85f, Kind.Storm => 1f, Kind.Snow => 0.85f, _ => 0.5f };
            float overcast = k switch { Kind.Rain => 0.6f, Kind.Storm => 0.9f, Kind.Snow => 0.55f, Kind.Dust => 0.35f, Kind.Cloudy => 0.15f, _ => 0f };
            vis.CloudCover = Mathf.Lerp(vis.CloudCover, cover, Time.deltaTime * 0.03f);
            vis.Overcast = Mathf.Lerp(vis.Overcast, overcast * intensity, Time.deltaTime * 0.05f);
            vis.FogDensityBoost = Mathf.Lerp(vis.FogDensityBoost, k == Kind.Dust ? 0.6f * intensity : (k == Kind.Snow ? 0.3f * intensity : 0f), Time.deltaTime * 0.1f);

            // Weather particles follow the camera
            transform.position = pos;
            rain.transform.position = pos + Vector3.up * 16f;
            snow.transform.position = pos + Vector3.up * 12f;
            dust.transform.position = pos + Vector3.up * 2f;
            float wet = (k == Kind.Rain ? 1f : k == Kind.Storm ? 1.8f : 0f) * intensity;
            Rate(rain, 1600f * wet);
            Rate(snow, (k == Kind.Snow ? 600f : 0f) * intensity);
            Rate(dust, (k == Kind.Dust ? 40f : 0f) * intensity);
            SetWind(rain, wind, 1.5f); SetWind(snow, wind, 1.2f); SetWind(dust, wind, 4f);

            // Ambient life: leaves only "иногда" — on gusts near groves / forests
            bool grove = biome == Biome.BirchGrove || biome == Biome.AppleForest || biome == Biome.Tugai || biome == Biome.Taiga;
            float leafRate = grove ? 2f + gust * 14f : gust * 2.5f;
            if (k == Kind.Snow || k == Kind.Dust) leafRate = 0;
            bool autumnish = biome == Biome.BirchGrove || biome == Biome.Tugai;
            foreach (var ps in new[] { leavesGreen, leavesYellow, fluff, petals, fireflies }) ps.transform.position = pos + Vector3.up * 3f;
            Rate(leavesGreen, leafRate * (autumnish ? 0.4f : 1f));
            Rate(leavesYellow, leafRate * (autumnish ? 0.8f : 0.15f));
            SetWind(leavesGreen, wind, 2.2f); SetWind(leavesYellow, wind, 2.2f);
            Rate(fluff, biome == Biome.Steppe ? gust * 10f * (k == Kind.Clear || k == Kind.Cloudy ? 1f : 0f) : 0f);
            SetWind(fluff, wind, 2.6f);
            Rate(petals, biome == Biome.AppleForest ? 4f + gust * 10f : 0f);
            SetWind(petals, wind, 1.8f);
            bool night = vis.SunElevation < -0.05f;
            Rate(fireflies, night && k != Kind.Rain && k != Kind.Storm && k != Kind.Snow && biome != Biome.Snowy ? 6f : 0f);
        }
    }
}
