using UnityEngine;
using UnityEngine.InputSystem;

namespace Dala
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public float WalkSpeed = 4.6f, RunSpeed = 7.2f, JumpHeight = 1.3f, Gravity = -22f;
        public float Reach = 5.5f, LookSens = 0.12f;
        public Camera Cam;
        public PlayerAvatar Avatar;
        public PlayerStats Stats { get; private set; }
        public readonly Inventory Inventory = new(36);
        public Animal Riding { get; private set; }
        public int Selected { get; private set; }
        public float BreakProgress { get; private set; }

        CharacterController cc;
        float pitch, vy, lastSpacePress, attackCooldown, swingTimer;
        bool frozen = true, thirdPerson, flying;
        Vector3 spawnPoint;
        Vector3Int breakTarget = new(int.MinValue, 0, 0);
        readonly System.Random rng = new();

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>(); cc.enabled = false;
            Stats = gameObject.AddComponent<PlayerStats>();
        }

        Vector3 Eye => transform.position + Vector3.up * 1.62f;
        bool UiOpen => (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) || (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) ||
                       (Hud.Instance != null && Hud.Instance.DeathShown);

        void Update()
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            if (kb == null || ms == null) return;
            var w = World.Instance;

            // Wait for spawn chunk collider before enabling physics
            if (frozen)
            {
                if (!w.IsReady(transform.position)) return;
                int x = Mathf.FloorToInt(transform.position.x), z = Mathf.FloorToInt(transform.position.z);
                transform.position = new Vector3(x + 0.5f, w.SurfaceY(x, z) + 2.5f, z + 0.5f);
                spawnPoint = transform.position;
                cc.enabled = true; frozen = false;
                StarterKit();
                Decorator.StarterHorses(transform.position);
            }

            // ---- windows ----
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (InventoryScreen.Instance.IsOpen) InventoryScreen.Instance.Close();
                else if (PauseMenu.Instance.IsOpen) PauseMenu.Instance.Close();
                else if (!Hud.Instance.DeathShown) PauseMenu.Instance.Open();
            }
            if (kb.eKey.wasPressedThisFrame && !PauseMenu.Instance.IsOpen && !Hud.Instance.DeathShown)
            {
                if (InventoryScreen.Instance.IsOpen) InventoryScreen.Instance.Close();
                else InventoryScreen.Instance.Open(InventoryScreen.Mode.Player);
            }
            bool ui = UiOpen;
            Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ui;
            if (ui || Stats.Dead) { BreakProgress = 0; Avatar.SetMotion(0, Riding != null); return; }

            // ---- look ----
            var d = ms.delta.ReadValue() * LookSens;
            if (Riding == null) transform.Rotate(0, d.x, 0);
            pitch = Mathf.Clamp(pitch - d.y, -89f, 89f);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (kb.f5Key.wasPressedThisFrame) { thirdPerson = !thirdPerson; Avatar.SetFirstPerson(!thirdPerson); }
            float back = Riding != null ? -5.5f : -3.5f;
            Cam.transform.localPosition = new Vector3(0, 1.62f, 0) + (thirdPerson ? Quaternion.Euler(pitch, 0, 0) * new Vector3(0.4f, 0.3f, back) : Vector3.zero);

            float ix = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
            float iz = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);

            // ---- F: mount / dismount ----
            if (kb.fKey.wasPressedThisFrame) { if (Riding != null) Dismount(); else TryMount(); }

            if (Riding != null)
            {
                Riding.SetRiderInput(new Vector2(ix, iz), d.x, kb.leftShiftKey.isPressed, kb.spaceKey.wasPressedThisFrame);
                Avatar.SetMotion(0, true);
            }
            else Move(kb, ix, iz);

            // ---- hotbar ----
            for (int i = 0; i < 9; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Selected = i;
            float scroll = ms.scroll.ReadValue().y;
            if (scroll != 0) Selected = (Selected - (int)Mathf.Sign(scroll) + 9) % 9;
            if (kb.qKey.wasPressedThisFrame && !Inventory[Selected].IsEmpty) Inventory.TakeFrom(Selected, kb.leftCtrlKey.isPressed ? Inventory[Selected].Count : 1);

            Interact(ms, w);
        }

        void Move(Keyboard kb, float ix, float iz)
        {
            bool creative = Session.Creative;
            if (kb.spaceKey.wasPressedThisFrame && creative)
            {
                if (Time.time - lastSpacePress < 0.3f) { flying = !flying; vy = 0; }
                lastSpacePress = Time.time;
            }
            if (!creative) flying = false;
            bool sprint = kb.leftShiftKey.isPressed && !flying && (creative || Stats.Hunger > 6);
            var move = transform.right * ix + transform.forward * iz;
            if (move.sqrMagnitude > 1) move.Normalize();
            move *= flying ? 11f : (sprint ? RunSpeed : WalkSpeed);
            if (flying)
            {
                vy = (kb.spaceKey.isPressed ? 8f : 0f) - (kb.leftShiftKey.isPressed ? 8f : 0f);
            }
            else
            {
                if (cc.isGrounded && vy < 0) vy = -2f;
                if (cc.isGrounded && kb.spaceKey.wasPressedThisFrame) { vy = Mathf.Sqrt(JumpHeight * -2f * Gravity); Stats.AddExhaustion(sprint ? 0.2f : 0.05f); }
                // swimming: float up gently in water
                var w = World.Instance; var p = transform.position + Vector3.up * 0.9f;
                bool inWater = w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == BlockType.Water;
                if (inWater) { vy = Mathf.Lerp(vy, kb.spaceKey.isPressed ? 3f : -1f, Time.deltaTime * 4f); move *= 0.6f; }
                else vy += Gravity * Time.deltaTime;
            }
            move.y = vy;
            cc.Move(move * Time.deltaTime);
            var hv = cc.velocity; hv.y = 0;
            if (sprint) Stats.AddExhaustion(hv.magnitude * Time.deltaTime * 0.02f);
            Avatar.SetMotion(hv.magnitude, false);
        }

        // ------------------------------------------------------------------ mining, fighting, using
        void Interact(Mouse ms, World w)
        {
            attackCooldown -= Time.deltaTime;
            swingTimer -= Time.deltaTime;
            bool blockHit = w.Raycast(Eye, Cam.transform.forward, Reach, out var hit, out var prev);
            float blockDist = blockHit ? Vector3.Distance(Eye, hit + Vector3.one * 0.5f) : float.MaxValue;
            Component target = null;
            if (Physics.Raycast(Eye, Cam.transform.forward, out var ph, Reach, ~0, QueryTriggerInteraction.Ignore) && ph.distance < blockDist)
                target = (Component)ph.collider.GetComponentInParent<Animal>() ?? (Component)ph.collider.GetComponentInParent<Villager>() ?? ph.collider.GetComponentInParent<TreasureMarker>();
            if (target is Animal ridden && ridden == Riding) target = null;
            var held = Inventory[Selected];

            // LMB: attack creature or mine block
            if (ms.leftButton.isPressed)
            {
                if (swingTimer <= 0) { Avatar.Swing(); swingTimer = 0.35f; }
                if (target is Animal a && ms.leftButton.wasPressedThisFrame && attackCooldown <= 0)
                {
                    float dmg = held.IsEmpty ? 1f : held.Def?.Damage ?? 1f;
                    a.Hurt(dmg, transform.position); attackCooldown = 0.4f; Stats.AddExhaustion(0.1f);
                    BreakProgress = 0; return;
                }
                if (blockHit && target == null) Mine(hit, held, w);
                else BreakProgress = 0;
            }
            else { BreakProgress = 0; breakTarget = new Vector3Int(int.MinValue, 0, 0); }

            // RMB: talk / open / eat / place
            if (!ms.rightButton.wasPressedThisFrame) return;
            if (target is Villager v) { v.Talk(this); return; }
            if (target is TreasureMarker t) { t.Open(this); return; }
            if (blockHit && w.TryGetEntity(hit, out var ent))
            {
                switch (ent.Type)
                {
                    case BlockType.Workbench: InventoryScreen.Instance.Open(InventoryScreen.Mode.Workbench, ent); return;
                    case BlockType.Furnace: InventoryScreen.Instance.Open(InventoryScreen.Mode.Furnace, ent); return;
                    case BlockType.Sandyq: InventoryScreen.Instance.Open(InventoryScreen.Mode.Chest, ent); return;
                }
            }
            var def = held.Def;
            if (def != null && def.Food > 0 && (Stats.Hunger < PlayerStats.MaxHunger || Session.Creative))
            {
                if (Stats.Eat(def) && !Session.Creative) Inventory.TakeFrom(Selected);
                Hud.Instance?.ShowMessage($"Ням! {def.Name}", 1.5f);
                return;
            }
            if (blockHit && def != null && def.IsBlock && !PlayerOverlaps(prev))
            {
                if (w.SetBlock(prev.x, prev.y, prev.z, def.Block) && !Session.Creative) Inventory.TakeFrom(Selected);
                Avatar.Swing();
            }
        }

        void Mine(Vector3Int hit, Stack held, World w)
        {
            var b = w.GetBlock(hit.x, hit.y, hit.z);
            float hard = Items.Hardness(b);
            if (hard < 0) { BreakProgress = 0; return; }
            if (hit != breakTarget) { breakTarget = hit; BreakProgress = 0; }
            if (Session.Creative) BreakProgress = 1f;
            else
            {
                var best = Items.BestTool(b);
                bool right = best != ToolKind.None && !held.IsEmpty && held.Def?.Tool == best;
                float time = hard * (right ? 0.45f : (best == ToolKind.Pickaxe ? 3.2f : 1.4f));
                BreakProgress += Time.deltaTime / Mathf.Max(0.05f, time);
            }
            if (BreakProgress < 1f) return;
            BreakProgress = 0; breakTarget = new Vector3Int(int.MinValue, 0, 0);
            w.SetBlock(hit.x, hit.y, hit.z, BlockType.Air, out var spilled);
            if (Session.Creative) return;
            foreach (var s in Items.Drops(b, rng)) Inventory.Add(s.Id, s.Count);
            if (spilled != null) foreach (var s in spilled) Inventory.Add(s.Id, s.Count);
            if (b == BlockType.OreGold || b == BlockType.OreCoal || b == BlockType.OreIron || b == BlockType.OreCopper) Stats.AddXp(1.5f);
            Stats.AddExhaustion(0.05f);
        }

        void StarterKit()
        {
            Inventory.Clear();
            if (Session.Creative)
            {
                int[] kit = { (int)BlockType.FeltRed, (int)BlockType.FeltBlue, (int)BlockType.Tekemet, (int)BlockType.Shyrdak, (int)BlockType.TileTurquoise,
                              (int)BlockType.TileMajolica, (int)BlockType.GoldOrnament, (int)BlockType.Adobe, (int)BlockType.Planks };
                for (int i = 0; i < kit.Length; i++) Inventory[i] = new Stack(kit[i], 64);
            }
            else
            {
                Inventory.Add((int)ItemType.Baursak, 6);
                Inventory.Add((int)ItemType.Torsyk, 1);
                Inventory.Add((int)BlockType.Torch, 4);
            }
        }

        public void Respawn()
        {
            if (Riding != null) Dismount();
            cc.enabled = false;
            transform.position = spawnPoint + Vector3.up;
            cc.enabled = true;
            vy = 0;
            Stats.Respawn();
        }

        void LateUpdate()
        {
            if (Riding == null) return;
            transform.SetPositionAndRotation(Riding.SeatPosition, Quaternion.Euler(0, Riding.transform.eulerAngles.y, 0));
        }

        void TryMount()
        {
            Animal best = null; float bestD = 3.2f;
            foreach (var a in Animal.All)
            {
                if (a.Sp == null || !a.Sp.Rideable || a.HasRider) continue;
                float dist = Vector3.Distance(a.transform.position, transform.position);
                if (dist < bestD) { bestD = dist; best = a; }
            }
            if (best == null) { Hud.Instance?.ShowMessage("Рядом нет коня или верблюда", 1.5f); return; }
            Riding = best;
            cc.enabled = false;
            best.Mount();
            Hud.Instance?.ShowMessage("WASD/мышь — править, Shift — галоп, Space — прыжок, F — слезть", 3f);
        }

        void Dismount()
        {
            var a = Riding;
            Riding = null;
            if (a != null)
            {
                a.Dismount();
                transform.position = a.transform.position + a.transform.right * 1.4f + Vector3.up * 0.6f;
            }
            vy = 0;
            cc.enabled = true;
        }

        public void ForceDismount() { if (Riding != null) Dismount(); }

        bool PlayerOverlaps(Vector3Int b)
        {
            var bounds = cc.bounds;
            var block = new Bounds(new Vector3(b.x + 0.5f, b.y + 0.5f, b.z + 0.5f), Vector3.one);
            return bounds.Intersects(block);
        }
    }
}
