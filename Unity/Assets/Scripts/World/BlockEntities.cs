using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>State + model for a block that is drawn as a Blender model (workbench, furnace, chest, campfire, torch).</summary>
    public class BlockEntity
    {
        public Vector3Int Pos;
        public BlockType Type;
        public GameObject Go;
        public Inventory Storage;                 // chest: 27 slots, furnace: 0 input, 1 fuel, 2 output
        public float Burn, BurnMax, Progress;     // furnace state

        public bool IsFurnaceBurning => Burn > 0;

        public void Tick(float dt)
        {
            if (Type != BlockType.Furnace) return;
            var inp = Storage[0];
            bool canSmelt = !inp.IsEmpty && Smelting.Results.TryGetValue(inp.Id, out var res) &&
                            (Storage[2].IsEmpty || (Storage[2].Id == res.Id && Storage[2].Count + res.Count <= 64));
            if (Burn <= 0 && canSmelt)
            {
                float f = Smelting.Fuel(Storage[1].Id);
                if (f > 0) { Burn = BurnMax = f; Storage.TakeFrom(1); }
            }
            if (Burn > 0) Burn -= dt;
            if (Burn > 0 && canSmelt)
            {
                Progress += dt;
                if (Progress >= Smelting.SmeltTime)
                {
                    Progress = 0;
                    var r = Smelting.Results[inp.Id];
                    Storage.TakeFrom(0);
                    Storage[2] = new Stack(r.Id, (Storage[2].IsEmpty ? 0 : Storage[2].Count) + r.Count);
                }
            }
            else Progress = Mathf.Max(0, Progress - dt * 2f);
            if (Go != null)
            {
                var fire = Go.GetComponentInChildren<FireVfx>(true);
                if (fire != null) fire.gameObject.SetActive(Burn > 0);
            }
        }
    }

    public static class BlockEntities
    {
        public static readonly Dictionary<Vector3Int, BlockEntity> All = new();

        static string ModelPath(BlockType t) => t switch
        {
            BlockType.Workbench => "Buildings/SM_Workbench",
            BlockType.Furnace => "Buildings/SM_Furnace",
            BlockType.Sandyq => "Household/SM_Sandyq",
            BlockType.Campfire => "Household/SM_Campfire_Base",
            BlockType.Torch => "Dungeon/SM_WallTorch",
            _ => null,
        };

        public static BlockEntity Create(Vector3Int p, BlockType t)
        {
            var e = new BlockEntity { Pos = p, Type = t };
            if (t == BlockType.Sandyq) e.Storage = new Inventory(27);
            if (t == BlockType.Furnace) e.Storage = new Inventory(3);
            All[p] = e;
            return e;
        }

        /// <summary>Removes the entity and returns its contents so they can go to the player.</summary>
        public static List<Stack> Remove(Vector3Int p)
        {
            var drops = new List<Stack>();
            if (!All.TryGetValue(p, out var e)) return drops;
            if (e.Storage != null) foreach (var s in e.Storage.Slots) if (!s.IsEmpty) drops.Add(s);
            if (e.Go != null) Object.Destroy(e.Go);
            All.Remove(p);
            return drops;
        }

        public static void Spawn(BlockEntity e, Transform parent)
        {
            if (e.Go != null) return;
            var prefab = Resources.Load<GameObject>("Models/" + ModelPath(e.Type));
            var root = new GameObject(e.Type.ToString());
            root.transform.SetParent(parent, true);
            root.transform.position = new Vector3(e.Pos.x + 0.5f, e.Pos.y, e.Pos.z + 0.5f);
            if (prefab != null)
            {
                var m = Object.Instantiate(prefab, root.transform);
                float scale = e.Type == BlockType.Torch ? 1f : e.Type == BlockType.Sandyq ? 0.95f : 1f;
                m.transform.localScale = Vector3.one * scale;
                FitToCell(m.transform, root.transform.position);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = e.Type == BlockType.Torch ? new Vector3(0.2f, 0.7f, 0.2f) : new Vector3(0.98f, 0.98f, 0.98f);
            if (e.Type == BlockType.Torch) col.isTrigger = true;
            if (e.Type == BlockType.Campfire)
            {
                col.size = new Vector3(0.98f, 0.35f, 0.98f); col.center = new Vector3(0, 0.17f, 0);
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.transform.SetParent(root.transform, false); f.transform.localPosition = new Vector3(0, 0.12f, 0);
            }
            if (e.Type == BlockType.Torch)
            {
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.Scale = 0.3f;
                f.transform.SetParent(root.transform, false); f.transform.localPosition = new Vector3(0, 0.72f, 0);
            }
            if (e.Type == BlockType.Furnace)
            {
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.Scale = 0.35f;
                f.transform.SetParent(root.transform, false); f.transform.localPosition = new Vector3(0, 0.1f, 0.45f);   // Blender front (-Y) = Unity +Z
                f.gameObject.SetActive(false);
            }
            e.Go = root;
        }

        /// <summary>Centers a model's bounds on the cell and puts its base on the floor (independent of model pivot).</summary>
        public static void FitToCell(Transform model, Vector3 floorCenter)
        {
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            model.position += floorCenter - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        public static void Tick(float dt)
        {
            foreach (var e in All.Values) e.Tick(dt);
        }
    }
}
