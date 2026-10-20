using UnityEngine;

namespace Dala
{
    /// <summary>
    /// An arrow in flight (skeleton archers and the player's садақ): gravity, sweeps blocks and creatures each frame,
    /// sticks in blocks; the player's arrows can be picked up again, like in Minecraft.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public const float Gravity = 20f;
        Vector3 vel;
        float damage, life;
        Component shooter;
        bool byPlayer, stuck;

        public static Projectile Fire(Vector3 from, Vector3 velocity, float damage, Component shooter, bool byPlayer)
        {
            var go = new GameObject("Arrow");
            go.transform.position = from;
            var p = go.AddComponent<Projectile>();
            p.vel = velocity; p.damage = damage; p.shooter = shooter; p.byPlayer = byPlayer;
            // the flight model (Blender) already points along +Z, so the arrow always flies tip first
            var prefab = Resources.Load<GameObject>("Models/Items/SM_ArrowFlight");
            if (prefab != null)
            {
                var m = Instantiate(prefab, go.transform, false);
                m.transform.localScale *= 0.55f;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            go.transform.rotation = Quaternion.LookRotation(velocity);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (stuck) { if (life > 20f) Destroy(gameObject); return; }
            if (life > 8f) { Destroy(gameObject); return; }
            var w = World.Instance;
            vel.y -= Gravity * dt;
            var from = transform.position;
            var step = vel * dt;
            float len = step.magnitude;
            if (len < 1e-4f) return;
            // creatures and the player
            if (Physics.Raycast(from, step / len, out var hit, len, ~0, QueryTriggerInteraction.Ignore))
            {
                var pc = hit.collider.GetComponentInParent<PlayerController>();
                var target = Combat.TargetOf(hit.collider);
                if (pc != null && !byPlayer) { pc.Stats.Damage(damage, from - vel.normalized); Destroy(gameObject); return; }
                if (target != null && target != shooter && Combat.IsHittable(target) && pc == null)
                {
                    Combat.Hit(target, damage, from - vel.normalized * 2f, false);
                    Destroy(gameObject); return;
                }
            }
            // blocks
            if (w != null && w.Raycast(from, step, len, out World.RayHit bh))
            {
                transform.position = bh.Point - step.normalized * 0.15f;
                stuck = true; life = 0;
                if (byPlayer && Session.InGame && !Session.Creative)
                {
                    ItemDrop.Spawn(new Stack((int)ItemType.Arrow, 1), transform.position, Vector3.zero, 0.5f);
                    Destroy(gameObject);
                }
                return;
            }
            transform.position = from + step;
            transform.rotation = Quaternion.LookRotation(vel);
        }
    }
}
