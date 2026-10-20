using System.Collections.Generic;
using UnityEngine;

namespace Dala
{
    /// <summary>
    /// State + model for a block that is drawn as a Blender model: workbench, furnace, tandyr, chest (sandyq), kebeje,
    /// campfire, torch, bed (корпе) and the kazan on its stand.
    /// </summary>
    public class BlockEntity
    {
        public Vector3Int Pos;
        public BlockType Type;
        public byte Meta;
        public GameObject Go;
        /// <summary>chest 27 / kebeje 9 slots; furnace & tandyr: 0 input, 1 fuel, 2 output; kazan: 0-3 ingredients, 4 fuel, 5 output.</summary>
        public Inventory Storage;
        public float Burn, BurnMax, Progress;     // furnace / tandyr / kazan fire

        // campfire
        public bool Lit = true;
        /// <summary>Seconds of fire left; negative = burns forever (aul hearths).</summary>
        public float Fuel = 180f;
        public readonly Stack[] Grill = new Stack[4];
        public readonly float[] GrillTime = new float[4];

        // kazan
        public int Water;                          // portions of water (one bucket = 3)
        public const int MaxWater = 3;

        // chest lid
        public int Viewers;
        float lid;
        Transform lidT;
        Quaternion lidRest;
        FireVfx fire;
        readonly GameObject[] grillModels = new GameObject[4];

        public bool IsFurnaceBurning => Burn > 0;
        public bool Burning => Type == BlockType.Campfire ? Lit : Burn > 0;

        /// <summary>Light level this block gives off now (0–15): torches always, fires and ovens only while burning.</summary>
        public int LightLevel => Type switch
        {
            BlockType.Torch => 14,
            BlockType.Campfire => Lit ? 15 : 0,
            BlockType.Furnace or BlockType.Tandyr => Burn > 0 ? 13 : 0,
            BlockType.Kazan => Burn > 0 ? 11 : 0,
            _ => 0,
        };
        int shownLight = -1;

        Dictionary<int, Stack> Recipes => Type == BlockType.Tandyr ? Smelting.Baking : Smelting.Results;

        public void Bind(GameObject go)
        {
            Go = go;
            fire = go.GetComponentInChildren<FireVfx>(true);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Lid")) { lidT = t; lidRest = t.localRotation; break; }
        }

        public void Tick(float dt)
        {
            switch (Type)
            {
                case BlockType.Furnace: case BlockType.Tandyr: TickOven(dt); break;
                case BlockType.Kazan: TickKazan(dt); break;
                case BlockType.Campfire: TickCampfire(dt); break;
                case BlockType.Sandyq: case BlockType.Kebeje: TickLid(dt); break;
            }
            // a fire was lit or went out: the light levels around it change
            int l = LightLevel;
            if (l != shownLight)
            {
                if (shownLight >= 0) World.Instance?.LightChanged(Pos);
                shownLight = l;
            }
        }

        void SetFire(bool on)
        {
            if (fire != null && fire.gameObject.activeSelf != on) fire.gameObject.SetActive(on);
        }

        void TickOven(float dt)
        {
            var inp = Storage[0];
            bool canSmelt = !inp.IsEmpty && Recipes.TryGetValue(inp.Id, out var res) &&
                            (Storage[2].IsEmpty || (Storage[2].Id == res.Id && Storage[2].Count + res.Count <= 64));
            if (Burn <= 0 && canSmelt)
            {
                float f = Smelting.Fuel(Storage[1].Id);
                if (f > 0) { Burn = BurnMax = f; Storage.TakeFrom(1); }
            }
            if (Burn > 0) Burn -= dt;
            float speed = Type == BlockType.Tandyr ? 2f : 1f;
            if (Burn > 0 && canSmelt)
            {
                Progress += dt * speed;
                if (Progress >= Smelting.SmeltTime)
                {
                    Progress = 0;
                    var r = Recipes[inp.Id];
                    Storage.TakeFrom(0);
                    Storage[2] = new Stack(r.Id, (Storage[2].IsEmpty ? 0 : Storage[2].Count) + r.Count);
                }
            }
            else Progress = Mathf.Max(0, Progress - dt * 2f);
            SetFire(Burn > 0);
        }

        void TickKazan(float dt)
        {
            var dish = Cooking.Match(Storage, Water);
            bool room = dish != null && (Storage[5].IsEmpty || (Storage[5].Id == dish.Result.Id && Storage[5].Count + dish.Result.Count <= 16));
            if (Burn <= 0 && room)
            {
                float f = Smelting.Fuel(Storage[4].Id);
                if (f > 0) { Burn = BurnMax = f; Storage.TakeFrom(4); }
            }
            if (Burn > 0) Burn -= dt;
            if (Burn > 0 && room)
            {
                Progress += dt;
                if (Progress >= Cooking.CookTime)
                {
                    Progress = 0;
                    for (int i = 0; i < 4; i++) if (!Storage[i].IsEmpty) Storage.TakeFrom(i);
                    if (dish.Water) Water--;
                    Storage[5] = new Stack(dish.Result.Id, (Storage[5].IsEmpty ? 0 : Storage[5].Count) + dish.Result.Count);
                }
            }
            else Progress = Mathf.Max(0, Progress - dt * 2f);
            SetFire(Burn > 0);
        }

        void TickCampfire(float dt)
        {
            if (Lit && Fuel >= 0)
            {
                Fuel -= dt;
                if (Fuel <= 0) { Fuel = 0; Lit = false; }
            }
            SetFire(Lit);
            for (int i = 0; i < 4; i++)
            {
                if (Grill[i].IsEmpty) continue;
                if (Lit) GrillTime[i] += dt;
                if (GrillTime[i] < 15f) continue;
                // cooked: pops off the grill like Minecraft's campfire
                var res = Smelting.Results.TryGetValue(Grill[i].Id, out var r) ? r : Grill[i];
                ItemDrop.Spawn(new Stack(res.Id, 1), new Vector3(Pos.x + 0.5f, Pos.y + 0.6f, Pos.z + 0.5f));
                Grill[i] = Stack.Empty; GrillTime[i] = 0;
                RefreshGrill();
            }
        }

        void TickLid(float dt)
        {
            if (lidT == null) return;
            float target = Viewers > 0 ? 1f : 0f;
            if (Mathf.Approximately(lid, target)) return;
            lid = Mathf.MoveTowards(lid, target, dt * 3.5f);
            float k = lid * lid * (3f - 2f * lid);
            lidT.localRotation = lidRest * Quaternion.Euler(-100f * k, 0, 0);   // hinge along the back edge
        }

        /// <summary>Raw food lying on the campfire grill, as small models.</summary>
        public void RefreshGrill()
        {
            if (Go == null) return;
            for (int i = 0; i < 4; i++)
            {
                if (grillModels[i] != null) ItemVisuals.Kill(grillModels[i]);
                grillModels[i] = null;
                if (Grill[i].IsEmpty) continue;
                var m = ItemVisuals.Create(Grill[i].Id, Go.transform, 0.28f);
                if (m == null) continue;
                float a = i * 90f + 45f;
                m.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.36f, 0.22f);
                m.transform.localRotation = Quaternion.Euler(90f, a, 0);
                grillModels[i] = m;
            }
        }
    }

    /// <summary>Kazan dishes: ingredients in the four pot slots (order doesn't matter), most need a portion of water.</summary>
    public static class Cooking
    {
        public class Dish { public int[] Ingredients; public bool Water; public Stack Result; }
        public const float CookTime = 8f;

        static int I(ItemType t) => (int)t;
        public static readonly List<Dish> All = new()
        {
            new Dish { Ingredients = new[] { I(ItemType.MeatRaw), I(ItemType.MeatRaw), I(ItemType.Nan), I(ItemType.Kese) }, Water = true, Result = new Stack(I(ItemType.Beshbarmak), 1) },
            new Dish { Ingredients = new[] { I(ItemType.MeatRaw), I(ItemType.Kese) }, Water = true, Result = new Stack(I(ItemType.Sorpa), 1) },
            new Dish { Ingredients = new[] { I(ItemType.FishRaw), I(ItemType.Kese) }, Water = true, Result = new Stack(I(ItemType.Sorpa), 1) },
            new Dish { Ingredients = new[] { I(ItemType.MeatRaw), I(ItemType.MeatRaw), I(ItemType.Kese) }, Water = false, Result = new Stack(I(ItemType.Kuyrdak), 1) },
            new Dish { Ingredients = new[] { I(ItemType.Nan), I(ItemType.Nan) }, Water = false, Result = new Stack(I(ItemType.Baursak), 6) },
        };

        public static Dish Match(Inventory pot, int water)
        {
            var have = new List<int>();
            for (int i = 0; i < 4; i++) if (!pot[i].IsEmpty) have.Add(pot[i].Id);
            if (have.Count == 0) return null;
            foreach (var d in All)
            {
                if (d.Ingredients.Length != have.Count || (d.Water && water <= 0)) continue;
                var need = new List<int>(d.Ingredients);
                bool ok = true;
                foreach (var id in have) if (!need.Remove(id)) { ok = false; break; }
                if (ok) return d;
            }
            return null;
        }
    }

    public static class BlockEntities
    {
        public static readonly Dictionary<Vector3Int, BlockEntity> All = new();

        public static string ModelPathOf(BlockType t) => ModelPath(t, 0);

        static string ModelPath(BlockType t, byte meta) => t switch
        {
            BlockType.Workbench => "Buildings/SM_Workbench",
            BlockType.Furnace => "Buildings/SM_Furnace",
            BlockType.Tandyr => "Buildings/SM_Tandyr",
            BlockType.Sandyq => "Household/SM_Sandyq",
            BlockType.Kebeje => "Household/SM_Kebeje",
            BlockType.Campfire => "Household/SM_Campfire_Base",
            BlockType.Torch => Meta.Has(meta, Dala.Meta.Second) ? "Dungeon/SM_WallTorch" : "Household/SM_Torch",
            BlockType.Bed => "Household/SM_Bed",
            BlockType.Kazan => "Household/SM_KazanBlock",
            _ => null,
        };

        public static BlockEntity Create(Vector3Int p, BlockType t, byte meta = 0)
        {
            var e = new BlockEntity { Pos = p, Type = t, Meta = meta };
            e.Storage = t switch
            {
                BlockType.Sandyq => new Inventory(27),
                BlockType.Kebeje => new Inventory(9),
                BlockType.Furnace or BlockType.Tandyr => new Inventory(3),
                BlockType.Kazan => new Inventory(6),
                _ => null,
            };
            All[p] = e;
            return e;
        }

        /// <summary>Removes the entity and returns its contents (and anything on the grill) so they can drop.</summary>
        public static List<Stack> Remove(Vector3Int p)
        {
            var drops = new List<Stack>();
            if (!All.TryGetValue(p, out var e)) return drops;
            if (e.Storage != null) foreach (var s in e.Storage.Slots) if (!s.IsEmpty) drops.Add(s);
            foreach (var s in e.Grill) if (!s.IsEmpty) drops.Add(s);
            if (e.Go != null) ItemVisuals.Kill(e.Go);
            All.Remove(p);
            return drops;
        }

        /// <summary>Yaw of a model whose front (Blender -Y = Unity +Z) looks along meta facing.</summary>
        public static float Yaw(byte meta) => Meta.Facing(meta) * 90f;

        public static void Spawn(BlockEntity e, Transform parent)
        {
            if (e.Go != null) return;
            var root = new GameObject(e.Type.ToString());
            root.transform.SetParent(parent, true);
            var center = new Vector3(e.Pos.x + 0.5f, e.Pos.y, e.Pos.z + 0.5f);
            root.transform.position = center;
            bool bedHead = e.Type == BlockType.Bed && Meta.Has(e.Meta, Dala.Meta.Second);
            bool wallTorch = e.Type == BlockType.Torch && Meta.Has(e.Meta, Dala.Meta.Second);
            var prefab = bedHead ? null : Resources.Load<GameObject>("Models/" + ModelPath(e.Type, e.Meta));
            if (prefab != null)
            {
                var m = Object.Instantiate(prefab, root.transform);
                if (wallTorch)
                {
                    // back plate against the wall the torch hangs on (meta facing points at the wall)
                    var toWall = (Vector3)Meta.Dir[Meta.Facing(e.Meta)];
                    m.transform.rotation = Quaternion.LookRotation(-toWall);
                    FitToCell(m.transform, center + toWall * 0.5f + Vector3.up * 0.18f);
                    var bnds = Bounds(m.transform);
                    float back = Vector3.Dot(bnds.extents, new Vector3(Mathf.Abs(toWall.x), 0, Mathf.Abs(toWall.z)));
                    m.transform.position -= toWall * back;
                }
                else
                {
                    m.transform.rotation = Quaternion.Euler(0, Yaw(e.Meta), 0);
                    // a bed spans its foot cell and the head cell in front of it
                    var at = e.Type == BlockType.Bed ? center + (Vector3)Meta.Dir[Meta.Facing(e.Meta)] * 0.5f : center;
                    FitToCell(m.transform, at);
                }
            }
            var col = root.AddComponent<BoxCollider>();
            var box = BlockShapes.EntityBox(e.Type, e.Meta);
            col.center = box.center - new Vector3(0.5f, 0, 0.5f);
            col.size = box.size * 0.98f;
            if (e.Type == BlockType.Torch) col.isTrigger = true;

            if (e.Type == BlockType.Campfire)
            {
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.FitToModel = true; f.transform.SetParent(root.transform, false);
                f.gameObject.SetActive(e.Lit);
            }
            if (e.Type == BlockType.Torch)
            {
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.Scale = 0.3f; f.LightRange = 9f;
                f.transform.SetParent(root.transform, false);
                var b = Bounds(root.transform);
                f.transform.position = new Vector3(b.center.x, b.max.y - 0.04f, b.center.z);
            }
            if (e.Type == BlockType.Furnace || e.Type == BlockType.Tandyr || e.Type == BlockType.Kazan)
            {
                var f = new GameObject("Fire").AddComponent<FireVfx>(); f.Scale = 0.35f; f.LightRange = 6f;
                f.transform.SetParent(root.transform, false);
                // furnace: in the mouth on the front; tandyr: in the opening on top; kazan: under the pot
                var front = Quaternion.Euler(0, Yaw(e.Meta), 0) * Vector3.forward;
                f.transform.localPosition = e.Type switch
                {
                    BlockType.Furnace => front * 0.45f + Vector3.up * 0.1f,
                    BlockType.Tandyr => Vector3.up * 0.7f,
                    _ => Vector3.up * 0.05f,
                };
                f.gameObject.SetActive(false);
            }
            e.Bind(root);
            e.RefreshGrill();
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
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
