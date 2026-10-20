using UnityEngine;

namespace Dala
{
    /// <summary>Health (hearts), hunger (baursaks), armor, air and experience — Minecraft-style survival stats.</summary>
    public class PlayerStats : MonoBehaviour
    {
        public const int MaxHealth = 20, MaxHunger = 20, MaxAir = 10;
        public float Health = MaxHealth, Hunger = MaxHunger, Saturation = 5f, Air = MaxAir;
        public int Armor;
        public int Level; public float Xp;    // Xp: 0..1 progress to next level
        public bool Dead { get; private set; }
        public float HurtFlash { get; private set; }

        public event System.Action Died;

        float exhaustion, regenTimer, starveTimer, drownTimer, fallStartY;
        bool wasGrounded = true;
        CharacterController cc;

        void Awake() { cc = GetComponent<CharacterController>(); }

        public void AddExhaustion(float e) { if (!Session.Creative) exhaustion += e; }

        float invuln;
        float tiltSign = 1f;
        /// <summary>Camera roll right after a hit (Minecraft's hurt shake).</summary>
        public float HurtTilt => Mathf.Sin(Mathf.Clamp01(HurtFlash / 0.35f) * Mathf.PI) * 6f * tiltSign;

        /// <summary>Hurts the player; <paramref name="from"/> (attacker position) also knocks them back.</summary>
        public void Damage(float amount, Vector3? from = null)
        {
            if (Dead || Session.Creative || amount <= 0 || invuln > 0) return;
            invuln = 0.5f;
            amount *= 1f - Mathf.Min(Armor, 20) * 0.04f;
            Health = Mathf.Max(0, Health - amount);
            HurtFlash = 0.35f;
            tiltSign = Random.value < 0.5f ? -1f : 1f;
            if (from.HasValue) GetComponent<PlayerController>()?.Knockback(from.Value);
            if (Health <= 0) { Dead = true; Died?.Invoke(); }
        }

        public void Heal(float amount) { if (!Dead) Health = Mathf.Min(MaxHealth, Health + amount); }

        /// <summary>Seconds left burning (a shaitan's touch): 1 damage per second, water puts it out.</summary>
        public float Burning { get; private set; }
        float burnTick;
        public void SetOnFire(float seconds) { if (!Session.Creative) Burning = Mathf.Max(Burning, seconds); }

        public bool Eat(ItemDef food)
        {
            if (food == null || food.Food <= 0 || Hunger >= MaxHunger) return false;
            Hunger = Mathf.Min(MaxHunger, Hunger + food.Food);
            Saturation = Mathf.Min(Hunger, Saturation + food.Food * 0.6f + food.Saturation * 0.5f);
            return true;
        }

        public void AddXp(float amount)
        {
            Xp += amount / (7 + Level * 2);
            while (Xp >= 1f) { Xp -= 1f; Level++; }
        }

        public void Respawn()
        {
            Dead = false; Health = MaxHealth; Hunger = MaxHunger; Saturation = 5; Air = MaxAir; exhaustion = 0;
        }

        void Update()
        {
            HurtFlash = Mathf.Max(0, HurtFlash - Time.deltaTime);
            invuln -= Time.deltaTime;
            if (Dead || Session.Creative || cc == null) { Air = MaxAir; return; }
            float dt = Time.deltaTime;

            // hunger: exhaustion drains saturation first, then baursaks
            while (exhaustion >= 4f)
            {
                exhaustion -= 4f;
                if (Saturation > 0) Saturation = Mathf.Max(0, Saturation - 1); else Hunger = Mathf.Max(0, Hunger - 1);
            }
            AddExhaustion(dt * 0.02f);
            // natural regen when well fed, starving damage when empty
            if (Hunger >= 18 && Health < MaxHealth) { regenTimer += dt; if (regenTimer > 4f) { regenTimer = 0; Health = Mathf.Min(MaxHealth, Health + 1); AddExhaustion(3f); } }
            if (Hunger <= 0) { starveTimer += dt; if (starveTimer > 4f) { starveTimer = 0; if (Health > 1) Damage(1); } }

            // fall damage
            if (cc.enabled)
            {
                if (!cc.isGrounded && wasGrounded) fallStartY = transform.position.y;
                if (cc.isGrounded && !wasGrounded)
                {
                    float fall = fallStartY - transform.position.y;
                    if (fall > 3.5f && !InWater(0.2f)) Damage(Mathf.Floor(fall - 3f));
                }
                if (!cc.isGrounded && transform.position.y > fallStartY) fallStartY = transform.position.y;
                wasGrounded = cc.isGrounded;
            }

            // burning
            if (Burning > 0)
            {
                Burning -= dt; burnTick -= dt;
                if (InWater(0.5f)) Burning = 0;
                else if (burnTick <= 0) { burnTick = 1f; invuln = 0; Damage(1f); }
            }

            // air / drowning
            if (InWater(1.55f))
            {
                Air = Mathf.Max(0, Air - dt * 0.65f);
                if (Air <= 0) { drownTimer += dt; if (drownTimer > 1f) { drownTimer = 0; Damage(2); } }
            }
            else Air = Mathf.Min(MaxAir, Air + dt * 4f);
        }

        bool InWater(float height)
        {
            var w = World.Instance;
            if (w == null) return false;
            var p = transform.position + Vector3.up * height;
            return w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == BlockType.Water;
        }
    }
}
