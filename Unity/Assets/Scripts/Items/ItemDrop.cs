using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// An item lying in the world, like in Minecraft: a small spinning, bobbing 3D model that falls, floats in water,
    /// merges with identical drops nearby and flies into the player's inventory when they walk close.
    /// </summary>
    public class ItemDrop : MonoBehaviour
    {
        public static readonly List<ItemDrop> All = new();
        const float Size = 0.28f, Half = 0.14f, PickupRadius = 1.6f, Lifetime = 300f;

        public Stack Stack;
        Vector3 vel;
        float age, pickupDelay, spin, mergeTimer;
        Transform visual;
        bool grounded;

        /// <summary>Drops a stack at <paramref name="pos"/> with a little random pop (or the given velocity).</summary>
        public static ItemDrop Spawn(Stack s, Vector3 pos, Vector3? velocity = null, float pickupDelay = 0.4f)
        {
            if (s.IsEmpty || Items.Get(s.Id) == null) return null;
            var go = new GameObject("Drop_" + s.Def.Name);
            go.transform.position = pos;
            var d = go.AddComponent<ItemDrop>();
            d.Stack = s;
            d.vel = velocity ?? new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.5f, 3.5f), Random.Range(-1.2f, 1.2f));
            d.pickupDelay = pickupDelay;
            d.spin = Random.Range(0f, 360f);
            return d;
        }

        /// <summary>Drops several stacks from one spot (block break, chest contents, animal loot).</summary>
        public static void SpawnAll(IEnumerable<Stack> stacks, Vector3 pos)
        {
            foreach (var s in stacks) Spawn(s, pos);
        }

        void Start()
        {
            All.Add(this);
            var holder = new GameObject("Visual").transform;
            holder.SetParent(transform, false);
            visual = holder;
            // a stack of several shows 2–3 copies, like Minecraft
            int copies = Stack.Count >= 16 ? 3 : Stack.Count > 1 ? 2 : 1;
            for (int i = 0; i < copies; i++)
            {
                var m = ItemVisuals.Create(Stack.Id, holder, Size);
                if (m != null) m.transform.localPosition = new Vector3(i * 0.05f, i * 0.04f, -i * 0.05f);
            }
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > Lifetime) { Destroy(gameObject); return; }
            var w = World.Instance;
            if (w == null) return;

            var p = transform.position;
            var player = PlayerController.Instance;
            bool magnet = false;
            if (player != null && age > pickupDelay && !player.Stats.Dead)
            {
                var target = player.transform.position + Vector3.up * 0.9f;
                float d = Vector3.Distance(p, target);
                if (d < 0.6f) { TryPickup(player); if (this == null) return; }
                else if (d < PickupRadius && CanFit(player)) { magnet = true; vel = (target - p).normalized * 8f; }
            }

            if (!magnet)
            {
                bool inWater = w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == BlockType.Water;
                vel.y = inWater ? Mathf.MoveTowards(vel.y, 0.6f, dt * 6f) : vel.y - 18f * dt;
                vel.x *= grounded || inWater ? 1f - Mathf.Min(1f, dt * 8f) : 1f - dt * 0.5f;
                vel.z *= grounded || inWater ? 1f - Mathf.Min(1f, dt * 8f) : 1f - dt * 0.5f;
            }
            p = Collide(w, p, vel * dt, magnet);
            transform.position = p;

            spin += dt * 70f;
            visual.localRotation = Quaternion.Euler(0, spin, 0);
            visual.localPosition = new Vector3(0, Half + Mathf.Sin(age * 2.5f) * 0.06f + 0.04f, 0);

            mergeTimer -= dt;
            if (mergeTimer <= 0) { mergeTimer = 0.5f; MergeNearby(); }
        }

        /// <summary>Moves axis by axis against solid voxels (the item's bottom rests on block tops).</summary>
        Vector3 Collide(World w, Vector3 p, Vector3 delta, bool ghost)
        {
            if (ghost) { grounded = false; return p + delta; }
            var n = p;
            n.x += delta.x; if (Solid(w, n)) { n.x = p.x; vel.x = 0; }
            n.z += delta.z; if (Solid(w, n)) { n.z = p.z; vel.z = 0; }
            n.y += delta.y;
            grounded = false;
            if (Solid(w, n))
            {
                if (delta.y < 0) { n.y = Mathf.Floor(n.y) + 1f; grounded = true; }
                else n.y = p.y;
                vel.y = 0;
            }
            return n;
        }

        static bool Solid(World w, Vector3 p) => BlockInfo.IsSolid(w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)))
                                                 && !BlockInfo.IsEntity(w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)));

        bool CanFit(PlayerController pc)
        {
            int max = Stack.Def?.MaxStack ?? 64;
            foreach (var s in pc.Inventory.Slots) if (s.IsEmpty || (s.Id == Stack.Id && s.Count < max)) return true;
            return false;
        }

        void TryPickup(PlayerController pc)
        {
            int left = pc.Inventory.Add(Stack);
            if (left == Stack.Count) return;
            Hud.Instance?.Pickup(Stack.Def, Stack.Count - left);
            if (left <= 0) { Destroy(gameObject); return; }
            Stack.Count = left;
        }

        void MergeNearby()
        {
            int max = Stack.Def?.MaxStack ?? 64;
            foreach (var o in All)
            {
                if (o == this || o == null || o.Stack.Id != Stack.Id || Stack.Count >= max || max <= 1) continue;
                if ((o.transform.position - transform.position).sqrMagnitude > 1.2f) continue;
                if (o.GetInstanceID() < GetInstanceID()) continue;       // the older one absorbs, so a pair merges once
                int n = Mathf.Min(max - Stack.Count, o.Stack.Count);
                Stack.Count += n; o.Stack.Count -= n;
                if (o.Stack.Count <= 0) Destroy(o.gameObject);
                return;
            }
        }
    }
}
