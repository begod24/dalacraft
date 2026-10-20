using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Aul resident (Blender model Resources/Models/NPC/*.fbx): wanders near home, works, greets and talks to the player.</summary>
    public class Villager : MonoBehaviour, IHittable
    {
        public static readonly List<Villager> All = new();
        public string Kind;
        public Vector3 Home;

        CharacterController cc;
        ClipPlayer anim;
        float heading, vy, timer, speed, talkTimer;
        enum Mode { Idle, Walk, Work, Talk }
        Mode mode;
        bool ready;
        int lineIndex;

        static readonly Dictionary<string, string[]> Lines = new()
        {
            ["Aksakal"] = new[] { "Сәлеметсіз бе, балам! (Здравствуй, сынок!)", "Жеті атаңды біл — знай семь поколений своих предков.", "Степь широка, но дорога домой одна.", "Береги коня — конь крылья джигита." },
            ["Apa"] = new[] { "Келіңіз, шай ішіңіз! (Проходи, выпей чаю!)", "Бауырсаки только из казана, бери!", "Дастархан всегда открыт для гостя." },
            ["Kyz"] = new[] { "Сәлем! Ты видел беркута над горами?", "Апа учит меня ткать текемет!" },
            ["Bala"] = new[] { "Ура, гость! Давай наперегонки до колодца!", "Я сам поймал сазана в реке!" },
            ["Merchant"] = new[] { "Сауда! Принеси золотой слиток — дам торсык кумыса и бауырсаки.", "Лучшие ковры от Каспия до Алтая!", "Караван ушёл к Шёлковому пути…" },
            ["Blacksmith"] = new[] { "Темірді қызған кезде соқ — куй железо, пока горячо!", "Принеси железные слитки — выкую тебе қылыш.", "Печь любит уголь, а не сырое бревно." },
            ["Batyr"] = new[] { "Ел қорғаны — батыр! Защищаю аул от волков.", "В горах бродит ирбис — будь осторожен.", "Ешқашан берілме! Никогда не сдавайся!" },
            ["Shepherd"] = new[] { "Отара цела, волки не подходили.", "Ночью волки воют на луну — не ходи один в степь." },
        };

        // ---- IHittable: residents can be hurt; the batyr defends the aul against whoever hits its people
        float health = 20f, invuln, angry;
        Vector3 push;
        CreatureKind IHittable.Kind => CreatureKind.Villager;
        public string DisplayName => Kind switch
        {
            "Aksakal" => "Ақсақал", "Apa" => "Апа", "Kyz" => "Қыз", "Bala" => "Бала", "Merchant" => "Саудагер", "Blacksmith" => "Ұста",
            "Batyr" => "Батыр", "Shepherd" => "Шопан", _ => Kind,
        };
        public float Health => health;
        public float MaxHealth => 20f;
        public bool Alive => this != null && health > 0;

        public void TakeHit(float damage, Vector3 from, bool crit)
        {
            if (invuln > 0 || health <= 0) return;
            invuln = 0.5f;
            health -= damage;
            HitFlash.On(gameObject);
            var away = transform.position - from; away.y = 0;
            push = away.normalized * 6f; vy = 4f;
            Hud.Instance?.ShowMessage($"{DisplayName}: Ай! Неге ұрасың?! (За что?!)", 2f);
            foreach (var v in All) if (v.Kind == "Batyr" && (v.transform.position - transform.position).sqrMagnitude < 40 * 40) v.angry = 40f;
            if (Kind == "Batyr") angry = 40f;
            if (health > 0) { mode = Mode.Walk; timer = 3f; heading = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg; return; }
            Vfx.Puff(transform.position + Vector3.up * 0.9f, new Color(0.92f, 0.92f, 0.92f, 0.9f), 18, 0.4f, 1.4f);
            Destroy(gameObject);
        }

        public static Villager Spawn(string kind, Vector3 pos)
        {
            var go = new GameObject("NPC_" + kind);
            go.transform.position = pos;
            var v = go.AddComponent<Villager>();
            v.Kind = kind; v.Home = pos;
            return v;
        }

        void Start()
        {
            ClipPlayer.Spawn("Models/NPC/" + Kind, transform, out anim);
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = Kind == "Bala" || Kind == "Kyz" ? 1.4f : 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, cc.height / 2, 0); cc.stepOffset = 0.6f;
            cc.enabled = false;
            heading = Random.Range(0, 360f);
            All.Add(this);
        }

        void OnDestroy() { All.Remove(this); anim?.Destroy(); }

        public void Talk(PlayerController player)
        {
            mode = Mode.Talk; timer = 4f;
            Vector3 to = player.transform.position - transform.position; to.y = 0;
            heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            // merchant trade: one gold ingot → kumys + baursaks
            if (Kind == "Merchant" && player.Inventory.CountOf((int)ItemType.IngotGold) > 0)
            {
                for (int i = 0; i < player.Inventory.Size; i++)
                    if (player.Inventory[i].Id == (int)ItemType.IngotGold) { player.Inventory.TakeFrom(i); break; }
                player.Inventory.Add((int)ItemType.Torsyk, 1); player.Inventory.Add((int)ItemType.Baursak, 6);
                Hud.Instance?.ShowMessage("Саудагер: Рахмет! Вот торсык кумыса и бауырсаки.", 4f);
                return;
            }
            if (Kind == "Blacksmith" && player.Inventory.CountOf((int)ItemType.IngotIron) >= 3)
            {
                int need = 3;
                for (int i = 0; i < player.Inventory.Size && need > 0; i++)
                    while (player.Inventory[i].Id == (int)ItemType.IngotIron && need > 0) { player.Inventory.TakeFrom(i); need--; }
                player.Inventory.Add((int)ItemType.Kylysh, 1);
                Hud.Instance?.ShowMessage("Ұста: Держи қылыш — острый, как ветер степи!", 4f);
                return;
            }
            var lines = Lines.TryGetValue(Kind, out var l) ? l : new[] { "Сәлем!" };
            Hud.Instance?.ShowMessage(lines[lineIndex++ % lines.Length], 4.5f);
        }

        void Update()
        {
            var w = World.Instance;
            if (w == null) return;
            float dt = Time.deltaTime;
            var player = PlayerController.Instance;
            if (player != null && (transform.position - player.transform.position).sqrMagnitude > Mathf.Pow((w.ViewRadius + 2) * Chunk.Size, 2)) { Destroy(gameObject); return; }
            if (!ready)
            {
                if (!w.IsReady(transform.position)) return;
                ready = true;
                transform.position = new Vector3(transform.position.x, w.SurfaceY(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.z)) + 1.2f, transform.position.z);
                cc.enabled = true;
            }

            timer -= dt;
            invuln -= dt;
            if (angry > 0 && player != null && !player.Stats.Dead && Kind == "Batyr")
            {
                // an angry batyr runs at the player and strikes with his spear
                angry -= dt;
                Vector3 to = player.transform.position - transform.position; to.y = 0;
                heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                mode = Mode.Walk; timer = 0.5f;
                if (to.magnitude < 2.2f && talkTimer <= 0) { player.Stats.Damage(4f, transform.position); talkTimer = 1.2f; anim?.PlayOnce("Work"); }
                talkTimer -= dt;
            }
            else if (mode != Mode.Talk && player != null)
            {
                float d = Vector3.Distance(player.transform.position, transform.position);
                talkTimer -= dt;
                if (d < 3f && talkTimer <= 0 && mode != Mode.Work) { mode = Mode.Idle; timer = 2f; Vector3 to = player.transform.position - transform.position; heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; }
            }
            if (timer <= 0)
            {
                float r = Random.value;
                bool worker = Kind == "Blacksmith" || Kind == "Apa" || Kind == "Shepherd";
                if (worker && r < 0.45f) { mode = Mode.Work; timer = Random.Range(5f, 10f); }
                else if (r < 0.7f) { mode = Mode.Walk; timer = Random.Range(3f, 7f); Vector3 toHome = Home - transform.position; toHome.y = 0;
                    heading = toHome.magnitude > 10f ? Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg : heading + Random.Range(-120f, 120f); }
                else { mode = Mode.Idle; timer = Random.Range(2f, 5f); }
            }
            float target = mode == Mode.Walk ? (angry > 0 ? 4.5f : 1.3f) : 0f;
            speed = Mathf.MoveTowards(speed, target, dt * 4f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, heading, 0), dt * 5f);
            var move = transform.forward * speed + push;
            push = Vector3.MoveTowards(push, Vector3.zero, dt * 20f);
            if (cc.isGrounded) { if (vy < 0) vy = -2; if ((cc.collisionFlags & CollisionFlags.Sides) != 0 && speed > 0.5f) heading += 120f; }
            vy -= 20f * dt; move.y = vy;
            cc.Move(move * dt);

            if (anim == null) return;
            if (mode == Mode.Talk) anim.Play("Talk", 0.3f);
            else if (mode == Mode.Work) anim.Play("Work", 0.3f);
            else if (speed > 0.2f) anim.Play("Walk", 0.25f, Mathf.Min(speed / 1.3f, 2.5f));
            else anim.Play("Idle", 0.4f);
            anim.Tick(dt);
        }
    }
}
