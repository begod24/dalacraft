using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public enum MobKind { Kanka, Olik, Albasty, Ayu, Karakurt, Shaitan }

    /// <summary>Per-kind tuning of the night creatures (models: Resources/Models/Mobs/*.fbx from Blender/tools/mobs.py).</summary>
    public class MobInfo
    {
        public MobKind Kind;
        public string Name;
        public float Health, Damage, Speed, Radius, Height;
        public bool Ranged, Flying, Undead, Neutral, Fire;
        public (int id, int min, int max)[] Drops;

        public static readonly Dictionary<MobKind, MobInfo> All = new()
        {
            [MobKind.Kanka] = new MobInfo { Kind = MobKind.Kanka, Name = "Қаңқа (скелет-лучник)", Health = 20, Damage = 3, Speed = 3.2f, Radius = 0.3f, Height = 1.95f,
                                            Ranged = true, Undead = true, Drops = new[] { ((int)ItemType.Bone, 0, 2), ((int)ItemType.Arrow, 0, 2) } },
            [MobKind.Olik] = new MobInfo { Kind = MobKind.Olik, Name = "Өлік (мертвец)", Health = 20, Damage = 3, Speed = 2.6f, Radius = 0.3f, Height = 1.95f,
                                           Undead = true, Drops = new[] { ((int)ItemType.RottenFlesh, 0, 2), ((int)ItemType.IngotIron, 0, 0) } },
            [MobKind.Albasty] = new MobInfo { Kind = MobKind.Albasty, Name = "Албасты (дух)", Health = 16, Damage = 4, Speed = 3.6f, Radius = 0.3f, Height = 1.8f,
                                              Flying = true, Undead = true, Drops = new[] { ((int)ItemType.Ectoplasm, 1, 2) } },
            [MobKind.Ayu] = new MobInfo { Kind = MobKind.Ayu, Name = "Аю (медведь)", Health = 30, Damage = 6, Speed = 5.0f, Radius = 0.6f, Height = 1.4f,
                                          Neutral = true, Drops = new[] { ((int)ItemType.MeatRaw, 2, 4), ((int)ItemType.Leather, 1, 3) } },
            [MobKind.Karakurt] = new MobInfo { Kind = MobKind.Karakurt, Name = "Қарақұрт (паук)", Health = 16, Damage = 2.5f, Speed = 4.6f, Radius = 0.6f, Height = 0.9f,
                                               Drops = new[] { ((int)ItemType.String, 0, 2), ((int)ItemType.SpiderEye, 0, 1) } },
            [MobKind.Shaitan] = new MobInfo { Kind = MobKind.Shaitan, Name = "Шайтан", Health = 24, Damage = 4, Speed = 4.2f, Radius = 0.3f, Height = 1.8f,
                                              Fire = true, Drops = new[] { ((int)ItemType.FireEssence, 0, 2), ((int)ItemType.Bone, 0, 1) } },
        };
    }

    /// <summary>
    /// Night creature AI: wanders, notices the player, chases and hits (or keeps distance and shoots), jumps over steps,
    /// flies (spirit), pounces (spider). Undead burn in daylight; the bear is neutral until hurt; spiders are calm by day.
    /// </summary>
    public class Mob : MonoBehaviour, IHittable
    {
        public static readonly List<Mob> All = new();
        public MobInfo Info;

        CharacterController cc;
        ClipPlayer anim;
        float health, vy, invuln, attackCd, think, burnTick, provoked, airTime;
        Vector3 push, wander;
        bool ready, dying;

        public CreatureKind Kind => Info.Neutral && provoked <= 0 ? CreatureKind.Animal : CreatureKind.Mob;
        public string DisplayName => Info.Name;
        public float Health => health;
        public float MaxHealth => Info.Health;
        public bool Alive => this != null && health > 0;
        public bool Hostile => !Info.Neutral || provoked > 0;

        public static Mob Spawn(MobKind kind, Vector3 pos)
        {
            var go = new GameObject("Mob_" + kind);
            go.transform.position = pos;
            var m = go.AddComponent<Mob>();
            m.Info = MobInfo.All[kind];
            m.health = m.Info.Health;
            return m;
        }

        void Start()
        {
            ClipPlayer.Spawn("Models/Mobs/" + Info.Kind, transform, out anim);
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = Info.Radius; cc.height = Mathf.Max(Info.Height, Info.Radius * 2 + 0.01f);
            cc.center = new Vector3(0, cc.height / 2, 0); cc.stepOffset = 0.55f; cc.slopeLimit = 60f;
            cc.enabled = false;
            transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            All.Add(this);
        }

        void OnDestroy() { All.Remove(this); anim?.Destroy(); }

        // ---------------------------------------------------------------- IHittable
        public void TakeHit(float damage, Vector3 from, bool crit)
        {
            if (invuln > 0 || health <= 0) return;
            invuln = 0.5f;
            health -= damage;
            provoked = 30f;
            HitFlash.On(gameObject);
            var away = transform.position - from; away.y = 0;
            push = away.normalized * (Info.Kind == MobKind.Ayu ? 4f : 7f);
            vy = Info.Flying ? 2f : 5f;
            if (health <= 0) Die(true);
        }

        void Die(bool byPlayer)
        {
            if (dying) return;
            dying = true; health = 0;
            Vfx.Puff(transform.position + Vector3.up * Info.Height * 0.5f, Info.Undead ? new Color(0.8f, 0.8f, 0.85f, 0.9f) : new Color(0.9f, 0.9f, 0.9f, 0.9f), 18, 0.35f, 1.6f);
            if (byPlayer)
            {
                var rng = new System.Random();
                foreach (var d in Info.Drops)
                {
                    int n = rng.Next(d.min, d.max + 1);
                    if (n > 0) ItemDrop.Spawn(new Stack(d.id, n), transform.position + Vector3.up * 0.6f);
                }
                if (Info.Kind == MobKind.Olik && rng.NextDouble() < 0.03) ItemDrop.Spawn(new Stack((int)ItemType.IngotIron, 1), transform.position + Vector3.up * 0.6f);
                PlayerController.Instance?.Stats.AddXp(5);
            }
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            var w = World.Instance;
            var pc = PlayerController.Instance;
            if (w == null || pc == null) return;
            float dt = Time.deltaTime;
            invuln -= dt; attackCd -= dt; think -= dt; provoked -= dt;

            if (!ready)
            {
                if (!w.IsReady(transform.position)) return;
                ready = true; cc.enabled = true;
            }
            var player = pc.transform.position;
            var to = player - transform.position;
            float dist = to.magnitude;
            if (dist > 90f) { Destroy(gameObject); return; }

            // daylight: the undead burn when the sky is above them, spirits fade
            var vis = WorldVisuals.Instance;
            bool day = vis != null && vis.SunElevation > 0.08f;
            if (Info.Undead && day && SkyAbove(w))
            {
                burnTick -= dt;
                if (burnTick <= 0)
                {
                    burnTick = 1f;
                    Vfx.Puff(transform.position + Vector3.up * Info.Height * 0.6f, Info.Flying ? new Color(0.8f, 0.95f, 1f, 0.7f) : new Color(0.25f, 0.22f, 0.2f, 0.8f), 8, 0.3f, 1f);
                    health -= Info.Flying ? 6f : 3f;
                    HitFlash.On(gameObject);
                    if (health <= 0) { Die(false); return; }
                }
            }

            bool calm = (Info.Neutral && provoked <= 0) || (Info.Kind == MobKind.Karakurt && day && provoked <= 0);
            bool chasePlayer = !calm && !pc.Stats.Dead && !Session.Creative && dist < (Info.Ranged ? 22f : 18f) && SeesPlayer(w, pc);
            // aul residents are prey too (like zombies and villagers): the nearest one, if closer than the player
            preyCheck -= dt;
            if (preyCheck <= 0) { preyCheck = 1f; prey = calm || Info.Neutral ? null : NearestVillager(16f); }
            if (prey != null && !prey.Alive) prey = null;
            bool chasePrey = prey != null && (!chasePlayer || (prey.transform.position - transform.position).magnitude < dist);
            bool chase = chasePlayer || chasePrey;
            if (chasePrey) { to = prey.transform.position - transform.position; dist = to.magnitude; }
            Vector3 dir;
            float speed;
            if (chase)
            {
                dir = new Vector3(to.x, 0, to.z).normalized;
                speed = Info.Speed;
                if (Info.Ranged)
                {
                    // the archer keeps 6–12 m away and shoots
                    if (dist < 6f) dir = -dir;
                    else if (dist < 12f) speed = 0f;
                    if (attackCd <= 0 && dist < 20f) { attackCd = 2.2f; anim?.PlayOnce("Attack", 1.2f); Shoot(chasePrey ? prey.transform.position : pc.transform.position); }
                }
                else if (dist < Info.Radius + 1.3f + (Info.Kind == MobKind.Ayu ? 0.5f : 0f) && attackCd <= 0)
                {
                    attackCd = Info.Kind == MobKind.Karakurt ? 0.9f : 1.2f;
                    anim?.PlayOnce("Attack");
                    if (chasePrey) prey.HurtBy(this, Info.Damage);
                    else
                    {
                        pc.Stats.Damage(Info.Damage, transform.position);
                        if (Info.Fire) pc.Stats.SetOnFire(3f);
                    }
                }
                // the karakurt pounces from a few metres away
                if (Info.Kind == MobKind.Karakurt && cc.isGrounded && dist > 2.5f && dist < 5f && attackCd <= 0.2f && Random.value < dt * 2f)
                { vy = 6f; push = dir * 7f; }
            }
            else
            {
                if (think <= 0) { think = Random.Range(3f, 7f); wander = Random.value < 0.4f ? Vector3.zero : Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward; }
                dir = wander; speed = Info.Speed * 0.35f;
            }

            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(chase ? new Vector3(to.x, 0, to.z) : dir), dt * 8f);
            var move = dir * speed + push;
            push = Vector3.MoveTowards(push, Vector3.zero, dt * 20f);

            if (Info.Flying)
            {
                // spirits float about 1.5 m above the ground and drift to the player's head height when hunting
                int ground = w.SurfaceY(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.z)) + 1;
                float targetY = chase ? transform.position.y + to.y + 0.8f : ground + 1.5f + Mathf.Sin(Time.time + transform.position.x) * 0.3f;
                vy = Mathf.MoveTowards(vy, Mathf.Clamp(targetY - transform.position.y, -2f, 2f) * 2f, dt * 6f);
            }
            else if (cc.isGrounded)
            {
                if (vy < 0) vy = -2f;
                bool blocked = (cc.collisionFlags & CollisionFlags.Sides) != 0 && move.sqrMagnitude > 0.3f;
                if (blocked) vy = Info.Kind == MobKind.Karakurt ? 6.5f : 7.2f;   // hop up a block (spiders climb higher)
                vy -= 25f * dt;
            }
            else
            {
                // spiders climb walls like Minecraft's
                bool wall = (cc.collisionFlags & CollisionFlags.Sides) != 0;
                vy = Info.Kind == MobKind.Karakurt && wall && chase ? 3f : vy - 25f * dt;
            }
            airTime = cc.isGrounded ? 0 : airTime + dt;
            move.y = vy;
            cc.Move(move * dt);

            if (anim != null)
            {
                float hs = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
                if (hs > Info.Speed * 0.7f) anim.Play("Run", 0.2f, Mathf.Clamp(hs / Info.Speed, 0.6f, 1.6f));
                else if (hs > 0.2f) anim.Play("Walk", 0.2f, Mathf.Clamp(hs / (Info.Speed * 0.4f), 0.5f, 2f));
                else anim.Play("Idle", 0.3f);
                anim.Tick(dt);
            }
        }

        bool SkyAbove(World w)
        {
            var p = transform.position;
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
            for (int y = Mathf.FloorToInt(p.y + Info.Height); y < Chunk.Height; y++)
                if (BlockInfo.IsOpaque(w.GetBlock(x, y, z)) || BlockInfo.IsLeaves(w.GetBlock(x, y, z))) return false;
            return true;
        }

        /// <summary>Line of sight through air (blocks hide the player).</summary>
        bool SeesPlayer(World w, PlayerController pc)
        {
            var eye = transform.position + Vector3.up * Info.Height * 0.85f;
            var target = pc.transform.position + Vector3.up * 1.5f;
            var d = target - eye;
            if (!w.Raycast(eye, d, d.magnitude, out World.RayHit h)) return true;
            return h.Distance > d.magnitude - 0.5f;
        }

        Villager prey;
        float preyCheck;

        Villager NearestVillager(float range)
        {
            Villager best = null; float bd = range * range;
            foreach (var v in Villager.All)
            {
                if (v == null || !v.Alive) continue;
                float d = (v.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        void Shoot(Vector3 at)
        {
            // turn to the target first, so the arrow leaves the bow and not the archer's side
            var look = at - transform.position; look.y = 0;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            var from = transform.position + Vector3.up * Info.Height * 0.8f + transform.forward * 0.5f;
            var target = at + Vector3.up * 1.1f;
            // ballistic aim: horizontal speed fixed, vertical speed chosen so gravity brings the arrow onto the target
            var d = target - from;
            var flat = new Vector3(d.x, 0, d.z);
            const float speed = 24f;
            float t = Mathf.Max(0.05f, flat.magnitude / speed);
            var v = flat.normalized * speed + Vector3.up * (d.y / t + 0.5f * Projectile.Gravity * t);
            v += Random.insideUnitSphere * 0.5f;                                 // skeletons are good shots, not perfect
            Projectile.Fire(from, v, Info.Damage, this, false);
        }

        // ---------------------------------------------------------------- spawning
        public const int Cap = 14;

        /// <summary>Called by the spawner each few seconds: picks a dark, empty spot 24–44 m from the player.</summary>
        public static void TrySpawn(World w, Vector3 player)
        {
            if (All.Count >= Cap || Session.Creative) return;
            var vis = WorldVisuals.Instance;
            bool night = vis != null && vis.SunElevation < -0.04f;
            float ang = Random.Range(0, Mathf.PI * 2), r = Random.Range(24f, 44f);
            int x = Mathf.FloorToInt(player.x + Mathf.Cos(ang) * r), z = Mathf.FloorToInt(player.z + Mathf.Sin(ang) * r);
            int surface = w.SurfaceY(x, z);
            int h = w.Terrain.Height(x, z, out _);
            var biome = w.Terrain.GetBiome(x, z, h);
            // caves: below the surface the dark is always deep enough
            bool cave = player.y < surface - 8;
            int y;
            if (cave)
            {
                y = -1;
                for (int yy = Mathf.FloorToInt(player.y) - 8; yy < player.y + 8; yy++)
                    if (BlockInfo.IsSolid(w.GetBlock(x, yy - 1, z)) && w.GetBlock(x, yy, z) == BlockType.Air && w.GetBlock(x, yy + 1, z) == BlockType.Air) { y = yy; break; }
                if (y < 0) return;
            }
            else
            {
                y = surface + 1;
                if (w.GetBlock(x, y, z) != BlockType.Air || w.GetBlock(x, y + 1, z) != BlockType.Air || w.GetBlock(x, surface, z) == BlockType.Water) return;
                if (w.Terrain.InVillage(x, z, 56f)) return;
            }
            var pos = new Vector3(x + 0.5f, y, z + 0.5f);
            // like Minecraft: only where no torch, fire or glowing block lights the spot (block light 0)
            Lighting.At(w, new Vector3Int(x, y, z), out _, out int blockL);
            if (blockL > 0) return;
            MobKind? kind = Pick(biome, night, cave, y);
            if (kind == null) return;
            int group = kind == MobKind.Ayu || kind == MobKind.Albasty ? 1 : Random.Range(1, 3);
            for (int i = 0; i < group; i++) Spawn(kind.Value, pos + new Vector3(Random.Range(-1.5f, 1.5f), 0.1f, Random.Range(-1.5f, 1.5f)));
        }

        static MobKind? Pick(Biome b, bool night, bool cave, int y)
        {
            float r = Random.value;
            if (cave) return y < 24 && r < 0.2f ? MobKind.Shaitan : r < 0.45f ? MobKind.Kanka : r < 0.75f ? MobKind.Olik : MobKind.Karakurt;
            if (b == Biome.Taiga && r < 0.12f) return MobKind.Ayu;          // bears roam the taiga day and night
            if (!night) return null;
            return b switch
            {
                Biome.BetpakDala => r < 0.4f ? MobKind.Shaitan : r < 0.75f ? MobKind.Karakurt : MobKind.Kanka,
                Biome.Snowy => r < 0.5f ? MobKind.Albasty : MobKind.Kanka,
                Biome.Taiga => r < 0.35f ? MobKind.Albasty : r < 0.7f ? MobKind.Olik : MobKind.Kanka,
                Biome.Balkhash or Biome.Tugai => r < 0.6f ? MobKind.Olik : MobKind.Karakurt,
                _ => r < 0.35f ? MobKind.Olik : r < 0.65f ? MobKind.Kanka : r < 0.88f ? MobKind.Karakurt : MobKind.Albasty,
            };
        }
    }

    /// <summary>Spawns night creatures around the player and answers "are enemies near?" for sleeping.</summary>
    public class MobSpawner : MonoBehaviour
    {
        float timer;

        void Awake()
        {
            PlayerController.EnemiesNear = (p, r) =>
            {
                foreach (var m in Mob.All) if (m != null && m.Hostile && (m.transform.position - p).sqrMagnitude < r * r) return true;
                return false;
            };
        }

        void Update()
        {
            var w = World.Instance; var pc = PlayerController.Instance;
            if (w == null || pc == null || Session.Paused) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 2.5f;
            Mob.TrySpawn(w, pc.transform.position);
        }

        void OnDestroy()
        {
            foreach (var m in Mob.All.ToArray()) if (m != null) Destroy(m.gameObject);
            Mob.All.Clear();
        }
    }
}
