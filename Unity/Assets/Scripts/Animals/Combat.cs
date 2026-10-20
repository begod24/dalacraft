using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>Who a creature is, shown on its health bar (green = animal, blue = aul resident, red = enemy).</summary>
    public enum CreatureKind { Animal, Villager, Mob }

    /// <summary>Anything the player can hit: animals, villagers, night mobs.</summary>
    public interface IHittable
    {
        CreatureKind Kind { get; }
        string DisplayName { get; }
        float Health { get; }
        float MaxHealth { get; }
        bool Alive { get; }
        /// <summary>Applies a hit (knockback, flash, reaction). Ignored while invulnerable.</summary>
        void TakeHit(float damage, Vector3 from, bool crit);
    }

    /// <summary>Unified hits: damage, red flash, knockback, floating numbers and the target's health bar on the HUD.</summary>
    public static class Combat
    {
        public static Component TargetOf(Collider c)
        {
            if (c == null) return null;
            var h = c.GetComponentInParent<IHittable>();
            if (h is Component comp) return comp;
            return c.GetComponentInParent<TreasureMarker>();
        }

        public static bool IsHittable(Component c) => c is IHittable h && h.Alive;

        public static void Hit(Component target, float damage, Vector3 from, bool crit)
        {
            if (target is not IHittable h || !h.Alive) return;
            float before = h.Health;
            h.TakeHit(damage, from, crit);
            float dealt = Mathf.Max(0, before - h.Health);
            CreatureHearts.Hurt(target);
            if (dealt <= 0 && h.Alive) return;
            Hud.Instance?.ShowTarget(h, dealt, crit, target.transform.position + Vector3.up * Height(target));
            Vfx.Puff(target.transform.position + Vector3.up * Height(target) * 0.6f, crit ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.1f, 0.08f), crit ? 14 : 8, 0.18f, 2f);
        }

        static float Height(Component c)
        {
            var cc = c.GetComponent<CharacterController>();
            return cc != null ? cc.height : 1f;
        }
    }

    /// <summary>
    /// Briefly tints every renderer of a creature red when it is hurt (Minecraft's damage flash), using property blocks so
    /// shared materials stay untouched.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        float t;

        public static void On(GameObject go)
        {
            var f = go.GetComponent<HitFlash>() ?? go.AddComponent<HitFlash>();
            f.t = 0.35f;
            f.renderers = go.GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            if (t <= 0) return;
            t -= Time.deltaTime;
            mpb ??= new MaterialPropertyBlock();
            float k = Mathf.Clamp01(t / 0.35f);
            var c = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), k);
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
