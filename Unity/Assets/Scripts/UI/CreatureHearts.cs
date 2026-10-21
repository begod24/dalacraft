using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// A row of pixel hearts above the head of every animal, villager and night creature near the player (2 HP per heart,
    /// rows of 10): red hearts for animals and aul residents, dark cursed hearts for mobs. Far creatures show theirs for a
    /// few seconds after being hit. Sprites: Blender/tools/ui_assets.py (red) and mc_combat.py (mob).
    /// </summary>
    public class CreatureHearts : MonoBehaviour
    {
        const float ShowRadius = 16f, HitShowTime = 5f, HeartSize = 0.13f, Gap = 0.015f;

        class Row
        {
            public GameObject Go;
            public readonly List<SpriteRenderer> Hearts = new();
            public float Top, TopTimer;
            public bool Mob;
        }

        static CreatureHearts instance;
        static readonly Dictionary<Component, float> lastHit = new();
        readonly Dictionary<Component, Row> rows = new();
        readonly List<Component> creatures = new(), gone = new();

        public static CreatureHearts Create(Transform parent)
        {
            var go = new GameObject("CreatureHearts");
            go.transform.SetParent(parent, false);
            return instance = go.AddComponent<CreatureHearts>();
        }

        /// <summary>Called by Combat.Hit: a far creature that was hit shows its hearts for a while.</summary>
        public static void Hurt(Component c) { if (c != null) lastHit[c] = Time.time; }

        void LateUpdate()
        {
            var pc = PlayerController.Instance;
            if (pc != null && pc.Cam != null) Refresh(pc.Cam, pc.Riding);
        }

        /// <summary>Places / updates every row for this camera (public for editor previews).</summary>
        public void Refresh(Camera cam, Animal riding = null)
        {
            creatures.Clear();
            foreach (var a in Animal.All) if (a != null) creatures.Add(a);
            foreach (var v in Villager.All) if (v != null) creatures.Add(v);
            foreach (var m in Mob.All) if (m != null) creatures.Add(m);

            var eye = cam.transform.position;
            foreach (var c in creatures)
            {
                var h = (IHittable)c;
                float d = (c.transform.position - eye).magnitude;
                bool hurt = lastHit.TryGetValue(c, out float t) && Time.time - t < HitShowTime;
                bool show = h.Alive && (d < ShowRadius || hurt) && !(c is Animal an && an == riding);
                rows.TryGetValue(c, out var row);
                if (!show) { if (row != null) row.Go.SetActive(false); continue; }
                row ??= rows[c] = Build(c, h.Kind == CreatureKind.Mob);
                row.Go.SetActive(true);
                Layout(row, h);
                // head height above the creature's feet (measured now and then), so the row rides along when it jumps or climbs
                row.TopTimer -= Time.deltaTime;
                if (row.TopTimer <= 0) { row.TopTimer = 2f; row.Top = Mathf.Max(row.Top, TopOf(c) - c.transform.position.y); }
                row.Go.transform.position = c.transform.position + Vector3.up * (row.Top + 0.35f);
                row.Go.transform.rotation = Quaternion.LookRotation(row.Go.transform.position - eye);
            }
            // creatures that died or despawned
            gone.Clear();
            foreach (var kv in rows) if (kv.Key == null || !((IHittable)kv.Key).Alive) gone.Add(kv.Key);
            foreach (var k in gone) { if (rows[k].Go != null) Destroy(rows[k].Go); rows.Remove(k); lastHit.Remove(k); }
        }

        Row Build(Component c, bool mob)
        {
            var row = new Row { Go = new GameObject("Hearts_" + c.name), Mob = mob };
            row.Go.transform.SetParent(transform, false);
            row.Top = TopOf(c) - c.transform.position.y;
            return row;
        }

        void Layout(Row row, IHittable h)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(h.MaxHealth / 2f));
            string pre = row.Mob ? "UI_MobHeart" : "UI_Heart";
            while (row.Hearts.Count < count)
            {
                var go = new GameObject("Heart");
                go.transform.SetParent(row.Go.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                row.Hearts.Add(sr);
            }
            int perRow = Mathf.Min(10, count);
            float w = perRow * HeartSize + (perRow - 1) * Gap;
            for (int i = 0; i < row.Hearts.Count; i++)
            {
                var sr = row.Hearts[i];
                sr.enabled = i < count;
                if (i >= count) continue;
                float hp = h.Health - i * 2;
                sr.sprite = UiKit.Sprite(pre + (hp >= 2 ? "Full" : hp >= 1 ? "Half" : "Empty"));
                if (sr.sprite == null) continue;
                int col = i % 10, line = i / 10;
                // the row reads left → right as the player sees it (the parent faces away from the camera)
                sr.transform.localPosition = new Vector3(-w / 2 + col * (HeartSize + Gap) + HeartSize / 2, line * (HeartSize + Gap), 0);
                float k = HeartSize / sr.sprite.bounds.size.x;
                sr.transform.localScale = new Vector3(k, k, 1);
            }
        }

        static float TopOf(Component c)
        {
            var rs = c.GetComponentsInChildren<Renderer>();
            float top = c.transform.position.y + 1f;
            bool any = false;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                any = true;
            }
            return top;
        }

        void OnDestroy() { lastHit.Clear(); if (instance == this) instance = null; }
    }
}
