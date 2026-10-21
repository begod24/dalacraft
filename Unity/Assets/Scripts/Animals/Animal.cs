using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    public enum Behaviour { Grazer, Skittish, Predator, Bird, Swimmer, Fish }

    /// <summary>Per-species tuning. Models/animations come from Blender (Resources/Models/Animals/*.fbx).</summary>
    public class Species
    {
        public string Name;
        public Behaviour Kind;
        public float Walk = 1.5f, Run = 6f, Radius = 0.4f, Height = 1f;
        public float AnimWalk = 1.5f, AnimRun = 6f;   // m/s at clip speed 1
        public bool Rideable;
        public float Trot = 6f, Gallop = 12f, SeatDrop = 0.5f;
        public string IdleClip = "Idle", WalkClip = "Walk", RunClip = "Run", EatClip = "Eat";
        public float Health = 10f, AttackDamage = 0f;
        public (int id, int min, int max)[] Drops = new (int, int, int)[0];

        static (int, int, int) D(ItemType t, int min, int max) => ((int)t, min, max);
        static Species()
        {
            void Set(string n, float hp, float atk, params (int, int, int)[] drops) { All[n].Health = hp; All[n].AttackDamage = atk; All[n].Drops = drops; }
            Set("Horse", 24, 0, D(ItemType.Leather, 1, 2));
            Set("Camel", 26, 0, D(ItemType.Leather, 1, 3), D(ItemType.Wool, 1, 2));
            Set("Cow", 20, 0, D(ItemType.MeatRaw, 1, 3), D(ItemType.Leather, 0, 2), D(ItemType.Kurt, 0, 2));
            Set("Sheep", 10, 0, D(ItemType.MeatRaw, 1, 2), D(ItemType.Wool, 1, 3));
            Set("Goat", 10, 0, D(ItemType.MeatRaw, 1, 2), D(ItemType.Kurt, 0, 1));
            Set("Kulan", 18, 0, D(ItemType.MeatRaw, 1, 3), D(ItemType.Leather, 0, 1));
            Set("Saiga", 12, 0, D(ItemType.MeatRaw, 1, 2), D(ItemType.Bone, 0, 1));
            Set("Hare", 4, 0, D(ItemType.MeatRaw, 1, 1));
            Set("Marmot", 5, 0, D(ItemType.MeatRaw, 1, 1));
            Set("Jerboa", 2, 0); Set("Lizard", 2, 0); Set("Tortoise", 8, 0); Set("Snake", 4, 2, D(ItemType.Bone, 0, 1));
            Set("Wolf", 14, 3, D(ItemType.Bone, 1, 2), D(ItemType.Leather, 0, 1));
            Set("Fox", 8, 1, D(ItemType.Leather, 0, 1));
            Set("SnowLeopard", 24, 5, D(ItemType.Leather, 1, 2), D(ItemType.Bone, 1, 2));
            Set("Berkut", 8, 0); Set("Duck", 4, 0, D(ItemType.MeatRaw, 1, 1)); Set("Fish", 3, 0, D(ItemType.FishRaw, 1, 1));
        }

        public static readonly Dictionary<string, Species> All = new()
        {
            ["Horse"] = new Species { Name = "Horse", Kind = Behaviour.Grazer, Walk = 1.8f, Run = 9f, Radius = 0.55f, Height = 1.7f, AnimWalk = 1.8f, AnimRun = 11f,
                                      Rideable = true, Trot = 6.5f, Gallop = 13f, SeatDrop = 0.55f, RunClip = "Gallop", EatClip = "Graze" },
            ["Camel"] = new Species { Name = "Camel", Kind = Behaviour.Grazer, Walk = 1.6f, Run = 7f, Radius = 0.6f, Height = 2.2f, AnimWalk = 1.8f, AnimRun = 8f,
                                      Rideable = true, Trot = 5f, Gallop = 9.5f, SeatDrop = 0.45f },
            ["Cow"] = new Species { Name = "Cow", Kind = Behaviour.Grazer, Walk = 1.2f, Run = 4.5f, Radius = 0.55f, Height = 1.5f, AnimWalk = 1.3f, AnimRun = 5f },
            ["Sheep"] = new Species { Name = "Sheep", Kind = Behaviour.Grazer, Walk = 1.1f, Run = 4.5f, Radius = 0.4f, Height = 1f, AnimWalk = 1.1f, AnimRun = 5f },
            ["Goat"] = new Species { Name = "Goat", Kind = Behaviour.Grazer, Walk = 1.3f, Run = 5.5f, Radius = 0.35f, Height = 1f, AnimWalk = 1.2f, AnimRun = 5.5f },
            ["Kulan"] = new Species { Name = "Kulan", Kind = Behaviour.Skittish, Walk = 1.7f, Run = 11f, Radius = 0.5f, Height = 1.5f, AnimWalk = 1.8f, AnimRun = 10f },
            ["Saiga"] = new Species { Name = "Saiga", Kind = Behaviour.Skittish, Walk = 1.4f, Run = 12f, Radius = 0.35f, Height = 1.1f, AnimWalk = 1.4f, AnimRun = 11f },
            ["Hare"] = new Species { Name = "Hare", Kind = Behaviour.Skittish, Walk = 1.0f, Run = 9f, Radius = 0.15f, Height = 0.4f, AnimWalk = 0.8f, AnimRun = 8f },
            ["Marmot"] = new Species { Name = "Marmot", Kind = Behaviour.Skittish, Walk = 0.8f, Run = 5f, Radius = 0.2f, Height = 0.4f, AnimWalk = 0.8f, AnimRun = 4f, EatClip = "Stand" },
            ["Jerboa"] = new Species { Name = "Jerboa", Kind = Behaviour.Skittish, Walk = 0.8f, Run = 7f, Radius = 0.08f, Height = 0.25f, AnimWalk = 0.7f, AnimRun = 6f, EatClip = "Idle" },
            ["Lizard"] = new Species { Name = "Lizard", Kind = Behaviour.Skittish, Walk = 0.8f, Run = 5f, Radius = 0.1f, Height = 0.15f, AnimWalk = 0.8f, AnimRun = 4f, EatClip = "Idle" },
            ["Tortoise"] = new Species { Name = "Tortoise", Kind = Behaviour.Grazer, Walk = 0.25f, Run = 0.4f, Radius = 0.2f, Height = 0.3f, AnimWalk = 0.3f, AnimRun = 0.4f },
            ["Snake"] = new Species { Name = "Snake", Kind = Behaviour.Skittish, Walk = 0.6f, Run = 2.5f, Radius = 0.1f, Height = 0.12f, AnimWalk = 0.6f, AnimRun = 2.5f, EatClip = "Idle" },
            ["Wolf"] = new Species { Name = "Wolf", Kind = Behaviour.Predator, Walk = 1.6f, Run = 9f, Radius = 0.35f, Height = 0.9f, AnimWalk = 1.6f, AnimRun = 9f },
            ["Fox"] = new Species { Name = "Fox", Kind = Behaviour.Skittish, Walk = 1.3f, Run = 8f, Radius = 0.25f, Height = 0.5f, AnimWalk = 1.2f, AnimRun = 7f },
            ["SnowLeopard"] = new Species { Name = "SnowLeopard", Kind = Behaviour.Predator, Walk = 1.4f, Run = 10f, Radius = 0.35f, Height = 0.8f, AnimWalk = 1.4f, AnimRun = 10f },
            ["Berkut"] = new Species { Name = "Berkut", Kind = Behaviour.Bird, Walk = 0.6f, Run = 9f, Radius = 0.3f, Height = 0.5f, AnimRun = 9f },
            ["Duck"] = new Species { Name = "Duck", Kind = Behaviour.Swimmer, Walk = 0.6f, Run = 3f, Radius = 0.2f, Height = 0.4f, AnimWalk = 0.6f },
            ["Fish"] = new Species { Name = "Fish", Kind = Behaviour.Fish, Walk = 0.8f, Run = 3f, Radius = 0.15f, Height = 0.3f, AnimWalk = 0.8f },
        };
    }

    /// <summary>Wandering / grazing / fleeing / hunting animal AI. Horses and camels can be ridden (E).</summary>
    public class Animal : MonoBehaviour, IHittable
    {
        public static readonly List<Animal> All = new();
        public Species Sp;

        CharacterController cc;
        ClipPlayer anim;
        Transform saddle;
        float heading, vy, modeTimer, speed, attackCooldown;
        enum Mode { Idle, Eat, Walk, Flee, Chase, Attack }
        Mode mode = Mode.Idle;
        bool ready;
        Vector3 home;

        // Rider
        public bool HasRider { get; private set; }
        float health = -1f;

        float invuln;
        Vector3 push;

        // ---- IHittable
        public CreatureKind Kind => CreatureKind.Animal;
        public string DisplayName => Names.TryGetValue(Sp.Name, out var n) ? n : Sp.Name;
        public float Health => health < 0 ? Sp.Health : health;
        public float MaxHealth => Sp.Health;
        public bool Alive => this != null && Health > 0;
        public void TakeHit(float damage, Vector3 from, bool crit) => Hurt(damage, from);

        static readonly Dictionary<string, string> Names = new()
        {
            ["Horse"] = "Ат (конь)", ["Camel"] = "Түйе (верблюд)", ["Cow"] = "Сиыр (корова)", ["Sheep"] = "Қой (овца)", ["Goat"] = "Ешкі (коза)",
            ["Kulan"] = "Құлан", ["Saiga"] = "Ақбөкен (сайгак)", ["Hare"] = "Қоян (заяц)", ["Marmot"] = "Суыр (сурок)", ["Jerboa"] = "Қосаяқ (тушканчик)",
            ["Lizard"] = "Кесіртке (ящерица)", ["Tortoise"] = "Тасбақа (черепаха)", ["Snake"] = "Сұр жылан (гадюка)", ["Wolf"] = "Қасқыр (волк)",
            ["Fox"] = "Түлкі (лиса)", ["SnowLeopard"] = "Ілбіс (ирбис)", ["Berkut"] = "Бүркіт", ["Duck"] = "Үйрек (утка)", ["Fish"] = "Сазан",
        };

        /// <summary>Player hits the animal. Returns true if it died (loot drops on the ground).</summary>
        public bool Hurt(float dmg, Vector3 from)
        {
            if (invuln > 0) return false;
            invuln = 0.5f;
            if (health < 0) health = Sp.Health;
            health -= dmg;
            HitFlash.On(gameObject);
            Vector3 away = transform.position - from; away.y = 0;
            push = away.normalized * 7f;
            heading = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            if (Sp.Kind != Behaviour.Predator) { mode = Mode.Flee; modeTimer = 5f; }
            else { mode = Mode.Chase; modeTimer = 8f; }
            vy = 5f;
            if (health > 0) return false;
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var rng = new System.Random();
                foreach (var d in Sp.Drops) { int n = rng.Next(d.min, d.max + 1); if (n > 0) ItemDrop.Spawn(new Stack(d.id, n), transform.position + Vector3.up * 0.5f); }
                pc.Stats.AddXp(Sp.AttackDamage > 0 ? 5 : 2);
            }
            Vfx.Puff(transform.position + Vector3.up * Sp.Height * 0.5f, new Color(0.92f, 0.92f, 0.92f, 0.9f), 16, Mathf.Clamp(Sp.Height * 0.4f, 0.2f, 0.6f), 1.5f);
            if (HasRider && pc != null && pc.Riding == this) pc.ForceDismount();
            Destroy(gameObject);
            return true;
        }
        Vector2 riderMove; bool riderGallop, riderJump;

        public static Animal Spawn(string species, Vector3 pos, float yaw)
        {
            if (!Species.All.TryGetValue(species, out var sp)) return null;
            var go = new GameObject(species);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var a = go.AddComponent<Animal>();
            a.Sp = sp;
            a.heading = yaw;
            a.home = pos;
            return a;
        }

        void Start()
        {
            ClipPlayer.Spawn("Models/Animals/" + Sp.Name, transform, out anim);
            saddle = FindDeep(transform, "Saddle");
            if (Sp.Kind != Behaviour.Bird && Sp.Kind != Behaviour.Swimmer && Sp.Kind != Behaviour.Fish)
            {
                cc = gameObject.AddComponent<CharacterController>();
                cc.radius = Sp.Radius;
                cc.height = Mathf.Max(Sp.Height, Sp.Radius * 2f + 0.01f);
                cc.center = new Vector3(0, cc.height * 0.5f, 0);
                cc.stepOffset = Mathf.Min(0.6f, cc.height * 0.5f);
                cc.slopeLimit = 55f;
                cc.enabled = false;
            }
            modeTimer = Random.Range(1f, 4f);
            All.Add(this);
        }

        void OnDestroy() { All.Remove(this); anim?.Destroy(); }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public Vector3 SeatPosition => (saddle != null ? saddle.position : transform.position + Vector3.up * Sp.Height) + Vector3.down * Sp.SeatDrop;

        public void Mount() { HasRider = true; mode = Mode.Idle; }
        public void Dismount() { HasRider = false; riderMove = Vector2.zero; }
        public void SetRiderInput(Vector2 move, float yawDelta, bool gallop, bool jump)
        {
            riderMove = move; heading += yawDelta; riderGallop = gallop; riderJump |= jump;
        }

        void Update()
        {
            var w = World.Instance;
            if (w == null) return;
            float dt = Time.deltaTime;
            invuln -= dt;
            var player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;

            // despawn far away (never the ridden one)
            if (!HasRider && player != null && (transform.position - player.position).sqrMagnitude > Mathf.Pow((w.ViewRadius + 2) * Chunk.Size, 2))
            { Destroy(gameObject); return; }

            if (!ready)
            {
                if (!w.IsReady(transform.position)) return;
                ready = true;
                if (cc != null)
                {
                    int x = Mathf.FloorToInt(transform.position.x), z = Mathf.FloorToInt(transform.position.z);
                    transform.position = new Vector3(transform.position.x, w.SurfaceY(x, z) + 1.2f, transform.position.z);
                    cc.enabled = true;
                }
            }

            switch (Sp.Kind)
            {
                case Behaviour.Bird: UpdateBird(dt, w); break;
                case Behaviour.Swimmer: UpdateSwimmer(dt, w); break;
                case Behaviour.Fish: UpdateFish(dt, w); break;
                default: UpdateGround(dt, w, player); break;
            }
            anim?.Tick(dt);
        }

        // ------------------------------------------------------------------ ground animals
        void UpdateGround(float dt, World w, Transform player)
        {
            float target = 0f;
            bool jump = false;
            if (HasRider)
            {
                float turn = riderMove.x * 110f * dt;
                heading += turn;
                target = riderMove.y > 0 ? (riderGallop ? Sp.Gallop : Sp.Trot) : (riderMove.y < 0 ? -Sp.Walk : 0f);
                jump = riderJump; riderJump = false;
            }
            else
            {
                Think(dt, player);
                target = mode switch { Mode.Walk => Sp.Walk, Mode.Flee => Sp.Run, Mode.Chase => Sp.Run * 0.85f, _ => 0f };
                // avoid walking into water / off cliffs
                if (target > 0 && AheadIsBad(w)) { heading += 150f + Random.Range(-30f, 30f); if (mode == Mode.Walk) mode = Mode.Idle; }
            }
            speed = Mathf.MoveTowards(speed, target, (HasRider ? 9f : 6f) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, heading, 0), dt * 6f);
            Vector3 move = transform.forward * speed + push;
            push = Vector3.MoveTowards(push, Vector3.zero, dt * 20f);
            if (cc.isGrounded)
            {
                if (vy < 0) vy = -2f;
                // walking into a 1-block step hops onto it — with a rider too (Minecraft horses step up by themselves)
                bool blocked = ((cc.collisionFlags & CollisionFlags.Sides) != 0 || StepAhead(w)) && Mathf.Abs(speed) > 0.5f;
                if (jump || blocked) vy = Mathf.Sqrt(1.4f * 2f * 25f);
            }
            vy -= 25f * dt;
            move.y = vy;
            cc.Move(move * dt);

            // animation
            float a = Mathf.Abs(speed);
            if (anim == null) return;
            if (mode == Mode.Attack && !HasRider) anim.Play("Attack", 0.1f);
            else if (a > Sp.Walk * 1.8f) anim.Play(Sp.RunClip, 0.25f, Mathf.Clamp(a / Sp.AnimRun, 0.5f, 1.6f));
            else if (a > 0.15f) anim.Play(Sp.WalkClip, 0.25f, Mathf.Clamp(a / Sp.AnimWalk, 0.4f, 2.5f));
            else if (mode == Mode.Eat) anim.Play(anim.Has(Sp.EatClip) ? Sp.EatClip : Sp.IdleClip, 0.5f);
            else anim.Play(Sp.IdleClip, 0.4f);
        }

        void Think(float dt, Transform player)
        {
            modeTimer -= dt;
            attackCooldown -= dt;
            float dist = player != null ? Vector3.Distance(player.position, transform.position) : 999f;
            bool playerRiding = PlayerController.Instance != null && PlayerController.Instance.Riding != null;

            if (Sp.Kind == Behaviour.Skittish && dist < 9f && !playerRiding)
            {
                mode = Mode.Flee; modeTimer = 3f;
                Vector3 away = transform.position - player.position; away.y = 0;
                heading = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                return;
            }
            const bool hunting = true;          // wolves and snow leopards are hostile day and night
            if (Sp.Kind == Behaviour.Predator && dist < 16f && player != null && hunting && !Session.Creative)
            {
                Vector3 to = player.position - transform.position; to.y = 0;
                heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                if (dist < 2.2f)
                {
                    mode = attackCooldown <= 0 ? Mode.Attack : Mode.Idle;
                    if (mode == Mode.Attack)
                    {
                        attackCooldown = 1.6f; modeTimer = 0.7f;
                        PlayerController.Instance?.Stats.Damage(Sp.AttackDamage, transform.position);
                    }
                }
                else if (mode != Mode.Attack || modeTimer <= 0) mode = Mode.Chase;
                return;
            }
            if (mode == Mode.Flee || mode == Mode.Chase || mode == Mode.Attack) { if (modeTimer > 0) return; mode = Mode.Idle; }
            if (modeTimer > 0) return;

            float r = Random.value;
            if (r < 0.35f) { mode = Mode.Idle; modeTimer = Random.Range(2f, 6f); }
            else if (r < 0.65f) { mode = Mode.Eat; modeTimer = Random.Range(4f, 10f); }
            else
            {
                mode = Mode.Walk; modeTimer = Random.Range(3f, 8f);
                // drift back towards home so herds stay together
                Vector3 toHome = home - transform.position; toHome.y = 0;
                float homeYaw = Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg;
                heading = toHome.magnitude > 18f ? homeYaw + Random.Range(-30f, 30f) : heading + Random.Range(-110f, 110f);
            }
            // wolves howl at night
            if (Sp.Name == "Wolf" && WorldVisuals.Instance != null && WorldVisuals.Instance.SunElevation < -0.1f && Random.value < 0.15f)
            { mode = Mode.Idle; modeTimer = 4f; anim?.PlayOnce("Howl"); }
        }

        /// <summary>A single solid block right in front of the feet with room above it.</summary>
        bool StepAhead(World w)
        {
            Vector3 p = transform.position + transform.forward * Mathf.Sign(speed) * (Sp.Radius + 0.45f);
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), y = Mathf.FloorToInt(transform.position.y + 0.1f);
            var b = w.GetBlock(x, y, z);
            return BlockInfo.IsSolid(b) && BlockInfo.ShapeOf(b) != Shape.Slab && !BlockInfo.IsFence(b)
                && !BlockInfo.IsSolid(w.GetBlock(x, y + 1, z)) && !BlockInfo.IsSolid(w.GetBlock(x, y + 2, z));
        }

        bool AheadIsBad(World w)
        {
            Vector3 p = transform.position + transform.forward * (Sp.Radius + 0.8f);
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), y = Mathf.FloorToInt(transform.position.y + 0.1f);
            if (w.GetBlock(x, y, z) == BlockType.Water || w.GetBlock(x, y - 1, z) == BlockType.Water) return true;
            // drop of more than 3 blocks
            return !BlockInfo.IsSolid(w.GetBlock(x, y - 1, z)) && !BlockInfo.IsSolid(w.GetBlock(x, y - 2, z)) && !BlockInfo.IsSolid(w.GetBlock(x, y - 3, z));
        }

        // ------------------------------------------------------------------ berkut: soaring circles
        float orbit;
        void UpdateBird(float dt, World w)
        {
            orbit += dt * 0.25f;
            float r = 22f;
            Vector3 c = home;
            int ground = w.SurfaceY(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.z));
            float alt = Mathf.Max(ground + 18f, c.y + 20f) + Mathf.Sin(orbit * 0.7f) * 4f;
            Vector3 target = new(c.x + Mathf.Cos(orbit) * r, alt, c.z + Mathf.Sin(orbit) * r);
            Vector3 vel = (target - transform.position);
            transform.position += vel * Mathf.Min(1f, dt * 0.8f);
            Vector3 fwd = new(-Mathf.Sin(orbit), 0, Mathf.Cos(orbit));
            transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, -18f);
            anim?.Play(Mathf.Sin(orbit * 3f) > 0.85f ? "Fly" : "Glide", 0.5f);
        }

        // ------------------------------------------------------------------ duck: paddles on the water surface
        void UpdateSwimmer(float dt, World w)
        {
            modeTimer -= dt;
            if (modeTimer <= 0) { heading += Random.Range(-90f, 90f); modeTimer = Random.Range(3f, 7f); speed = Random.value < 0.3f ? 0f : Sp.Walk; }
            Vector3 next = transform.position + Quaternion.Euler(0, heading, 0) * Vector3.forward * (speed * dt + 0.6f);
            if (w.GetBlock(Mathf.FloorToInt(next.x), TerrainGenerator.SeaLevel, Mathf.FloorToInt(next.z)) != BlockType.Water) heading += 160f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, heading, 0), dt * 3f);
            var p = transform.position + transform.forward * speed * dt;
            p.y = TerrainGenerator.SeaLevel + 0.82f + Mathf.Sin(Time.time * 1.7f + home.x) * 0.02f;
            transform.position = p;
            anim?.Play("Swim", 0.3f, speed > 0.1f ? 1f : 0.4f);
        }

        // ------------------------------------------------------------------ fish: swims below the surface
        void UpdateFish(float dt, World w)
        {
            modeTimer -= dt;
            if (modeTimer <= 0) { heading += Random.Range(-120f, 120f); modeTimer = Random.Range(2f, 5f); speed = Random.Range(0.3f, Sp.Walk); }
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var away = transform.position - pc.transform.position; away.y = 0;
                if (away.sqrMagnitude < 16f)   // the whole school scatters when someone wades in
                {
                    heading = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg + Random.Range(-25f, 25f);
                    speed = Sp.Run; modeTimer = 1.2f;
                }
            }
            Vector3 next = transform.position + Quaternion.Euler(0, heading, 0) * Vector3.forward * 0.8f;
            if (w.GetBlock(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y), Mathf.FloorToInt(next.z)) != BlockType.Water) { heading += 170f; speed *= 0.5f; }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, heading, 0), dt * 4f);
            var p = transform.position + transform.forward * speed * dt;
            // swim mid-water: between the bed and half a block under the surface
            int bed = w.SurfaceY(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)) + 1;
            float mid = Mathf.Min((bed + TerrainGenerator.SeaLevel + 1f) * 0.5f, TerrainGenerator.SeaLevel + 0.4f);
            p.y = Mathf.Lerp(p.y, Mathf.Max(bed + 0.25f, mid + Mathf.Sin(Time.time * 0.6f + home.z) * 0.3f), dt);
            transform.position = p;
            anim?.Play("Swim", 0.2f, Mathf.Clamp(speed / Sp.AnimWalk, 0.5f, 2f));
        }
    }
}
