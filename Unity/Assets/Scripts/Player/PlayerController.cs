using UnityEngine;
using UnityEngine.InputSystem;

namespace Dala
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        /// <summary>Set by the mob system: true if a hostile creature is within the radius (no sleeping then).</summary>
        public static System.Func<Vector3, float, bool> EnemiesNear;

        public float WalkSpeed = 4.6f, RunSpeed = 7.2f, JumpHeight = 1.3f, Gravity = -22f;
        public float Reach = 5.5f, LookSens = 0.12f;
        public Camera Cam;
        public PlayerAvatar Avatar;
        public PlayerStats Stats { get; private set; }
        public readonly Inventory Inventory = new(36);
        public Animal Riding { get; private set; }
        public int Selected { get; private set; }
        public float BreakProgress { get; private set; }
        public FirstPersonHands Hands { get; private set; }
        /// <summary>The block under the crosshair (for the outline and the cracks), if any.</summary>
        public World.RayHit? Aim { get; private set; }
        public Vector3Int BreakTarget => breakTarget;
        const float EatTime = 1.6f;
        float eatTimer, bowDraw, plantCd;

        CharacterController cc;
        float pitch, vy, lastSpacePress, attackCooldown, swingTimer;
        bool frozen = true, thirdPerson, flying;
        Vector3 spawnPoint;
        Vector3? bedSpawn;
        Vector3 knock;
        Vector3Int breakTarget = new(int.MinValue, 0, 0);
        readonly System.Random rng = new();

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>(); cc.enabled = false;
            Stats = gameObject.AddComponent<PlayerStats>();
            Stats.Died += OnDied;
        }

        Vector3 Eye => transform.position + Vector3.up * 1.62f;
        bool UiOpen => (InventoryScreen.Instance != null && InventoryScreen.Instance.IsOpen) || (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) ||
                       (Hud.Instance != null && (Hud.Instance.DeathShown || Hud.Instance.Sleeping));

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

            if (Hands == null && Cam != null) { Hands = FirstPersonHands.Create(Cam); Hands.SetVisible(!thirdPerson); }
            var heldNow = Inventory[Selected];
            Hands?.SetItem(heldNow.IsEmpty ? 0 : heldNow.Id);
            UpdateHandLight(heldNow);

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
            Cursor.visible = ui && !(Hud.Instance != null && Hud.Instance.Sleeping);
            if (ui || Stats.Dead) { BreakProgress = 0; Aim = null; Avatar.SetMotion(0, Riding != null); return; }

            // ---- look ----
            var d = ms.delta.ReadValue() * LookSens;
            if (Riding == null) transform.Rotate(0, d.x, 0);
            pitch = Mathf.Clamp(pitch - d.y, -89f, 89f);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0, Stats.HurtTilt);
            if (kb.f5Key.wasPressedThisFrame) { thirdPerson = !thirdPerson; Avatar.SetFirstPerson(!thirdPerson); Hands?.SetVisible(!thirdPerson); }
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
                Hands?.SetWalk(0, false);
            }
            else Move(kb, ix, iz);

            // ---- hotbar ----
            for (int i = 0; i < 9; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Selected = i;
            float scroll = ms.scroll.ReadValue().y;
            if (scroll != 0) Selected = (Selected - (int)Mathf.Sign(scroll) + 9) % 9;
            if (kb.qKey.wasPressedThisFrame && !Inventory[Selected].IsEmpty) DropSelected(kb.leftCtrlKey.isPressed);

            Interact(ms, w);
        }

        /// <summary>Holding a torch lights the surroundings (like the dynamic-light mods for Minecraft): a moving light level in the shaders.</summary>
        void UpdateHandLight(Stack held)
        {
            bool torch = !held.IsEmpty && (held.Id == (int)BlockType.Torch || held.Id == (int)ItemType.FireEssence);
            Lighting.SetHandLight(Eye, torch && !Stats.Dead ? 13f + Mathf.PerlinNoise(Time.time * 6f, 0.3f) * 0.8f : 0f);
        }

        // ------------------------------------------------------------------ movement
        void Move(Keyboard kb, float ix, float iz)
        {
            bool creative = Session.Creative;
            if (kb.spaceKey.wasPressedThisFrame && creative)
            {
                if (Time.time - lastSpacePress < 0.3f) { flying = !flying; vy = 0; }
                lastSpacePress = Time.time;
            }
            if (!creative) flying = false;
            bool sneak = kb.leftCtrlKey.isPressed && !flying;
            bool sprint = kb.leftShiftKey.isPressed && !flying && !sneak && (creative || Stats.Hunger > 6);
            var move = transform.right * ix + transform.forward * iz;
            if (move.sqrMagnitude > 1) move.Normalize();
            move *= flying ? 11f : (sprint ? RunSpeed : sneak ? WalkSpeed * 0.35f : WalkSpeed);
            var w = World.Instance;
            if (flying)
            {
                vy = (kb.spaceKey.isPressed ? 8f : 0f) - (kb.leftShiftKey.isPressed ? 8f : 0f);
            }
            else if (OnLadder(w))
            {
                // ladders: walk into it or hold space to climb, sneak to hold on, otherwise slide down slowly
                vy = kb.spaceKey.isPressed || iz > 0 ? 3f : sneak ? 0f : -2f;
            }
            else
            {
                if (cc.isGrounded && vy < 0) vy = -2f;
                bool jump = kb.spaceKey.wasPressedThisFrame || (GameSettings.AutoJump && !sneak && move.sqrMagnitude > 0.01f && LedgeAhead(move));
                if (cc.isGrounded && jump) { vy = Mathf.Sqrt(JumpHeight * -2f * Gravity); Stats.AddExhaustion(sprint ? 0.2f : 0.05f); }
                // swimming: float up gently in water
                var p = transform.position + Vector3.up * 0.9f;
                bool inWater = w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == BlockType.Water;
                if (inWater) { vy = Mathf.Lerp(vy, kb.spaceKey.isPressed ? 3f : -1f, Time.deltaTime * 4f); move *= 0.6f; }
                else vy += Gravity * Time.deltaTime;
            }
            // sneaking at an edge: don't walk off it (Minecraft's crouch)
            if (sneak && cc.isGrounded && move.sqrMagnitude > 0.001f && !GroundAt(w, transform.position + move * Time.deltaTime * 3f)) move = Vector3.zero;
            move += knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, Time.deltaTime * 18f);
            move.y = vy;
            cc.Move(move * Time.deltaTime);
            var hv = cc.velocity; hv.y = 0;
            if (sprint) Stats.AddExhaustion(hv.magnitude * Time.deltaTime * 0.02f);
            Avatar.SetMotion(hv.magnitude, false);
            Hands?.SetWalk(hv.magnitude, cc.isGrounded);
        }

        bool OnLadder(World w)
        {
            foreach (float h in new[] { 0.1f, 0.9f })
            {
                var p = transform.position + Vector3.up * h;
                if (w.GetBlock(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z)) == BlockType.Ladder) return true;
            }
            return false;
        }

        static bool GroundAt(World w, Vector3 p)
        {
            for (float dx = -0.25f; dx <= 0.25f; dx += 0.5f)
                for (float dz = -0.25f; dz <= 0.25f; dz += 0.5f)
                    if (BlockInfo.IsSolid(w.GetBlock(Mathf.FloorToInt(p.x + dx), Mathf.FloorToInt(p.y - 0.05f), Mathf.FloorToInt(p.z + dz)))) return true;
            return false;
        }

        /// <summary>Auto-jump: a 1-block step right in front of the feet with room above it (like Minecraft's option).</summary>
        bool LedgeAhead(Vector3 move)
        {
            var w = World.Instance;
            var dir = new Vector3(move.x, 0, move.z).normalized;
            var p = transform.position + dir * (cc.radius + 0.25f);
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), y = Mathf.FloorToInt(transform.position.y + 0.05f);
            var step = w.GetBlock(x, y, z);
            // bottom slabs and stairs are climbed by the step offset, no jump needed
            var sh = BlockInfo.ShapeOf(step);
            if ((sh == Shape.Slab && !Meta.Has(w.GetMeta(x, y, z), Meta.Double) && !Meta.Has(w.GetMeta(x, y, z), Meta.Upper)) || sh == Shape.Stairs) return false;
            return BlockInfo.IsSolid(step) && !BlockInfo.IsFence(step) && step != BlockType.Door
                && !BlockInfo.IsSolid(w.GetBlock(x, y + 1, z)) && !BlockInfo.IsSolid(w.GetBlock(x, y + 2, z))
                && !BlockInfo.IsSolid(w.GetBlock(Mathf.FloorToInt(transform.position.x), y + 2, Mathf.FloorToInt(transform.position.z)));
        }

        /// <summary>Pushes the player (being hit).</summary>
        public void Knockback(Vector3 from, float strength = 6f)
        {
            var d = transform.position - from; d.y = 0;
            knock = d.normalized * strength;
            if (cc.isGrounded) vy = 4.5f;
        }

        // ------------------------------------------------------------------ mining, fighting, using
        void Interact(Mouse ms, World w)
        {
            attackCooldown -= Time.deltaTime;
            swingTimer -= Time.deltaTime;
            plantCd -= Time.deltaTime;
            bool blockHit = w.Raycast(Eye, Cam.transform.forward, Reach, out World.RayHit rh, true);
            var hit = rh.Cell;
            float blockDist = blockHit ? rh.Distance : float.MaxValue;
            Component target = null;
            if (Physics.Raycast(Eye, Cam.transform.forward, out var ph, Reach, ~0, QueryTriggerInteraction.Ignore) && ph.distance < blockDist)
                target = Combat.TargetOf(ph.collider);
            if (target is Animal ridden && ridden == Riding) target = null;
            Aim = blockHit && target == null ? rh : null;
            var held = Inventory[Selected];
            var def = held.Def;

            // LMB: attack creature or mine block
            if (ms.leftButton.isPressed)
            {
                if (swingTimer <= 0) { Avatar.Swing(); Hands?.Swing(); swingTimer = 0.3f; }
                if (target != null && Combat.IsHittable(target) && ms.leftButton.wasPressedThisFrame && attackCooldown <= 0)
                {
                    Attack(target, held);
                    BreakProgress = 0; return;
                }
                if (blockHit && target == null) Mine(hit, held, w);
                else BreakProgress = 0;
            }
            else { BreakProgress = 0; breakTarget = new Vector3Int(int.MinValue, 0, 0); }

            // RMB held on food: eat over 1.6 s with the hand at the mouth (not while aiming at something usable)
            var aimed = blockHit ? w.GetBlock(hit) : BlockType.Air;
            bool usable = aimed == BlockType.FenceGate || aimed == BlockType.Door || aimed == BlockType.Trapdoor || (blockHit && w.TryGetEntity(hit, out _));
            bool aimsAtUse = target is Villager || target is TreasureMarker || usable;
            if (def != null && def.Food > 0 && !aimsAtUse && (Stats.Hunger < PlayerStats.MaxHunger || Session.Creative) && ms.rightButton.isPressed)
            {
                eatTimer += Time.deltaTime;
                Hands?.SetEating(eatTimer / EatTime);
                if (eatTimer >= EatTime)
                {
                    eatTimer = 0;
                    if (Stats.Eat(def) && !Session.Creative)
                    {
                        Inventory.TakeFrom(Selected);
                        if (def.Returns != 0) Give(new Stack(def.Returns, 1));
                    }
                    Hud.Instance?.ShowMessage($"Ням! {def.Name}", 1.5f);
                }
                return;
            }
            eatTimer = 0; Hands?.SetEating(0);

            // садақ: hold RMB to draw (up to 1 s), release to shoot an arrow
            if (held.Id == (int)ItemType.Bow && !aimsAtUse)
            {
                bool hasArrow = Session.Creative || Inventory.CountOf((int)ItemType.Arrow) > 0;
                if (ms.rightButton.isPressed && hasArrow) { bowDraw = Mathf.Min(1f, bowDraw + Time.deltaTime); Hands?.SetEating(0.2f + bowDraw * 0.3f); }
                else if (bowDraw > 0)
                {
                    if (bowDraw > 0.15f && hasArrow)
                    {
                        var dir = Cam.transform.forward;
                        Projectile.Fire(Eye + dir * 0.6f, dir * (12f + 26f * bowDraw), 2f + 7f * bowDraw * bowDraw, this, true);
                        if (!Session.Creative) { Inventory.Remove((int)ItemType.Arrow, 1); Inventory.Wear(Selected); }
                        Hands?.Swing();
                    }
                    bowDraw = 0; Hands?.SetEating(0);
                }
                return;
            }
            bowDraw = 0;

            // RMB: talk / open / use / place
            if (!ms.rightButton.wasPressedThisFrame) return;
            if (target is Villager v) { v.Talk(this); return; }
            if (target is TreasureMarker t) { t.Open(this); return; }
            if (target is Animal an && UseOnAnimal(an, held)) return;
            if (blockHit && Use(w, rh, held)) { Hands?.Swing(); return; }
            if (UseItem(w, blockHit, rh, held)) { Hands?.Swing(); return; }
            if (blockHit && def != null && def.IsBlock && Place(w, rh, def.Block))
            {
                if (!Session.Creative) Inventory.TakeFrom(Selected);
                Avatar.Swing(); Hands?.Swing();
            }
        }

        void Attack(Component target, Stack held)
        {
            var def = held.Def;
            float dmg = held.IsEmpty ? 1f : def?.Damage ?? 1f;
            // a falling hit is a critical one, like Minecraft
            bool crit = !cc.isGrounded && vy < -1f;
            if (crit) dmg *= 1.5f;
            Combat.Hit(target, dmg, transform.position, crit);
            attackCooldown = 0.4f; Stats.AddExhaustion(0.1f); Hands?.Swing();
            if (!Session.Creative && def != null && def.Durability > 0 && Inventory.Wear(Selected, def.Tool == ToolKind.Weapon ? 1 : 2))
                Hud.Instance?.ShowMessage($"{def.Name} сломался!", 2f);
        }

        void Mine(Vector3Int hit, Stack held, World w)
        {
            var b = w.GetBlock(hit);
            var tool = held.IsEmpty ? null : held.Def;
            // grass, flowers and bushes break at once, like Minecraft's tall grass
            if (b == BlockType.Air)
            {
                if (plantCd <= 0 && w.BreakPlant(hit))
                {
                    Vfx.Puff(new Vector3(hit.x + 0.5f, hit.y + 0.3f, hit.z + 0.5f), new Color(0.45f, 0.7f, 0.25f, 0.9f), 8, 0.12f, 0.8f);
                    plantCd = 0.15f;
                }
                BreakProgress = 0; return;
            }
            if (Items.Hardness(b) < 0) { BreakProgress = 0; return; }
            if (hit != breakTarget) { breakTarget = hit; BreakProgress = 0; }
            if (Session.Creative) BreakProgress = 1f;
            else
            {
                var head = Eye;
                bool underwater = w.GetBlock(Mathf.FloorToInt(head.x), Mathf.FloorToInt(head.y), Mathf.FloorToInt(head.z)) == BlockType.Water;
                BreakProgress += Time.deltaTime / Mathf.Max(0.05f, Items.BreakTime(b, tool, underwater));
            }
            if (BreakProgress < 1f) return;
            BreakProgress = 0; breakTarget = new Vector3Int(int.MinValue, 0, 0);
            var drops = w.BreakBlock(hit, tool, rng);
            Vfx.BlockBreak(hit, b);
            if (Session.Creative) return;
            var at = new Vector3(hit.x + 0.5f, hit.y + 0.3f, hit.z + 0.5f);
            ItemDrop.SpawnAll(drops, at);
            if (b == BlockType.OreGold || b == BlockType.OreCoal || b == BlockType.OreIron || b == BlockType.OreCopper) Stats.AddXp(1.5f);
            if (Items.Hardness(b) > 0 && !Items.CanHarvest(b, tool)) Hud.Instance?.ShowMessage(NeedToolHint(b), 2.5f);
            Stats.AddExhaustion(0.05f);
            if (tool != null && tool.Durability > 0 && Items.Hardness(b) > 0 && Inventory.Wear(Selected, tool.Tool == ToolKind.Weapon ? 2 : 1))
                Hud.Instance?.ShowMessage($"{tool.Name} сломался!", 2f);
        }

        static string NeedToolHint(BlockType b) => Items.HarvestTier(b) switch
        {
            Tier.Iron => "Нужна железная кирка или лучше — иначе ничего не выпадет",
            Tier.Stone => "Нужна каменная кирка или лучше — иначе ничего не выпадет",
            _ => "Нужна кирка — руками камень не добыть",
        };

        // ------------------------------------------------------------------ placing
        /// <summary>Places a block against the face that was hit, oriented like Minecraft (stairs/doors face away, torches on walls…).</summary>
        bool Place(World w, World.RayHit rh, BlockType b)
        {
            var shape = BlockInfo.ShapeOf(b);
            var hitB = w.GetBlock(rh.Cell); byte hitM = w.GetMeta(rh.Cell);
            var n = rh.Normal;
            float fracY = rh.Point.y - rh.Cell.y;
            bool upperHalf = n.y < 0 || (n.y == 0 && fracY > 0.5f);
            int look = Meta.FacingOf(Cam.transform.forward);

            // slab onto a matching slab → a double slab
            if (shape == Shape.Slab)
            {
                if (hitB == b && !Meta.Has(hitM, Meta.Double) && ((n.y > 0 && !Meta.Has(hitM, Meta.Upper)) || (n.y < 0 && Meta.Has(hitM, Meta.Upper))))
                {
                    w.SetMeta(rh.Cell, (byte)(hitM | Meta.Double)); return true;
                }
                var into = rh.Prev;
                if (w.GetBlock(into) == b && !Meta.Has(w.GetMeta(into), Meta.Double))
                {
                    w.SetMeta(into, (byte)(w.GetMeta(into) | Meta.Double)); return true;
                }
            }

            // aiming at grass: the new block replaces it, like Minecraft
            var at = hitB == BlockType.Air && w.PlantAt(rh.Cell) ? rh.Cell : rh.Prev;
            if (!BlockInfo.IsReplaceable(w.GetBlock(at)) || PlayerOverlaps(at, shape, b)) return false;
            byte meta = 0;
            var below = w.GetBlock(at + Vector3Int.down);
            switch (shape)
            {
                case Shape.Stairs:
                    meta = (byte)(look | (upperHalf ? Meta.Upper : 0)); break;
                case Shape.Slab:
                    meta = upperHalf ? Meta.Upper : (byte)0; break;
                case Shape.Trapdoor:
                    meta = (byte)((n.y == 0 ? Meta.FacingOf(-n) : look) | (upperHalf ? Meta.Upper : 0)); break;
                case Shape.Ladder:
                    if (n.y != 0 || !BlockInfo.IsOpaque(hitB)) return false;
                    meta = (byte)Meta.FacingOf(-n); break;
                case Shape.Cross when BlockInfo.IsSapling(b):
                    if (below != BlockType.Grass && below != BlockType.DryGrass && below != BlockType.Dirt && below != BlockType.Snow &&
                        !(b == BlockType.SaplingSaxaul && (below == BlockType.Sand || below == BlockType.RedSand))) return false;
                    break;
                case Shape.Cross:
                    if (b == BlockType.Dripstone && n.y < 0) { meta = Meta.Upper; break; }        // hangs from a ceiling
                    if (!BlockInfo.IsSolid(below)) return false;
                    break;
                case Shape.Flat:
                    if (below != BlockType.Water || w.GetBlock(at) != BlockType.Air) return false;
                    break;
                case Shape.Cube when BlockInfo.IsLog(b):
                    meta = (byte)(n.x != 0 ? 1 : n.z != 0 ? 2 : 0);                                // logs lie along the clicked face
                    break;
                case Shape.Door:
                {
                    var top = at + Vector3Int.up;
                    if (!BlockInfo.IsSolid(below) || !BlockInfo.IsReplaceable(w.GetBlock(top)) || PlayerOverlaps(top, shape, b)) return false;
                    // hinge on the side away from a neighbouring door, so double doors open outward like a pair
                    var left = at + Meta.Dir[(look + 3) % 4];
                    byte hinge = w.GetBlock(left) == BlockType.Door ? Meta.HingeRight : (byte)0;
                    meta = (byte)(look | hinge);
                    w.SetBlock(at.x, at.y, at.z, b, meta);
                    w.SetBlock(top.x, top.y, top.z, b, (byte)(meta | Meta.Upper));
                    return true;
                }
                case Shape.Entity:
                    if (b == BlockType.Torch)
                    {
                        if (n.y < 0) return false;
                        if (n.y == 0) { if (!BlockInfo.IsOpaque(hitB)) return false; meta = (byte)(Meta.FacingOf(-n) | Meta.Second); }
                        else if (!BlockInfo.IsSolid(below)) return false;
                        break;
                    }
                    if (b == BlockType.Bed)
                    {
                        var head = at + Meta.Dir[look];
                        if (!BlockInfo.IsReplaceable(w.GetBlock(head)) || !BlockInfo.IsSolid(below) || !BlockInfo.IsSolid(w.GetBlock(head + Vector3Int.down))) return false;
                        w.SetBlock(at.x, at.y, at.z, b, (byte)look);
                        w.SetBlock(head.x, head.y, head.z, b, (byte)(look | Meta.Second));
                        return true;
                    }
                    meta = (byte)((look + 2) % 4);      // the front looks at the player
                    break;
            }
            if (!w.SetBlock(at.x, at.y, at.z, b, meta)) return false;
            if (BlockInfo.IsLeaves(b)) Trees.PlayerPlaced.Add(at);
            return true;
        }

        // ------------------------------------------------------------------ using blocks and items
        bool Use(World w, World.RayHit rh, Stack held)
        {
            var p = rh.Cell;
            var b = w.GetBlock(p);
            if (b == BlockType.FenceGate || b == BlockType.Door || b == BlockType.Trapdoor) { w.ToggleOpen(p); return true; }
            if (BlockInfo.IsSapling(b) && held.Id == (int)ItemType.Bone)
            {
                if (!Session.Creative) Inventory.TakeFrom(Selected);
                if (Random.value < 0.45f) Trees.Grow(w, p, b);
                return true;
            }
            if (!w.TryGetEntity(p, out var e)) return false;
            if (e.Type == BlockType.Bed && Meta.Has(e.Meta, Meta.Second) && w.TryGetEntity(p - Meta.Dir[Meta.Facing(e.Meta)], out var foot)) e = foot;
            var def = held.Def;
            switch (e.Type)
            {
                case BlockType.Workbench: InventoryScreen.Instance.Open(InventoryScreen.Mode.Workbench, e); return true;
                case BlockType.Furnace: case BlockType.Tandyr: InventoryScreen.Instance.Open(InventoryScreen.Mode.Furnace, e); return true;
                case BlockType.Sandyq: case BlockType.Kebeje: InventoryScreen.Instance.Open(InventoryScreen.Mode.Chest, e); return true;
                case BlockType.Kazan:
                    if (held.Id == (int)ItemType.BucketWater)
                    {
                        if (e.Water >= BlockEntity.MaxWater) { Hud.Instance?.ShowMessage("Қазан уже полон воды", 1.5f); return true; }
                        e.Water = BlockEntity.MaxWater;
                        if (!Session.Creative) Inventory[Selected] = new Stack((int)ItemType.Bucket, 1);
                        Hud.Instance?.ShowMessage("Залили воду в қазан", 1.5f);
                        return true;
                    }
                    InventoryScreen.Instance.Open(InventoryScreen.Mode.Kazan, e); return true;
                case BlockType.Campfire: return UseCampfire(e, held, def);
                case BlockType.Bed: Sleep(e); return true;
            }
            return false;
        }

        bool UseCampfire(BlockEntity e, Stack held, ItemDef def)
        {
            if (def != null && def.Tool == ToolKind.Shovel && e.Lit)
            {
                e.Lit = false; Inventory.Wear(Selected); Hud.Instance?.ShowMessage("Костёр потушен", 1.5f); return true;
            }
            bool igniter = held.Id == (int)ItemType.FlintSteel || held.Id == (int)BlockType.Torch || held.Id == (int)ItemType.FireEssence;
            if (!e.Lit && igniter)
            {
                if (e.Fuel == 0) e.Fuel = 60f;
                e.Lit = true;
                if (held.Id == (int)ItemType.FlintSteel) Inventory.Wear(Selected);
                Hud.Instance?.ShowMessage("Костёр горит", 1.5f);
                return true;
            }
            if (held.Id == (int)ItemType.MeatRaw || held.Id == (int)ItemType.FishRaw)
            {
                for (int i = 0; i < 4; i++)
                    if (e.Grill[i].IsEmpty)
                    {
                        e.Grill[i] = new Stack(held.Id, 1); e.GrillTime[i] = 0; e.RefreshGrill();
                        if (!Session.Creative) Inventory.TakeFrom(Selected);
                        if (!e.Lit) Hud.Instance?.ShowMessage("Разожги костёр огнивом или факелом", 2f);
                        return true;
                    }
                Hud.Instance?.ShowMessage("На костре нет места", 1.5f);
                return true;
            }
            float fuel = held.IsEmpty ? 0 : Smelting.Fuel(held.Id);
            if (fuel > 0 && e.Fuel >= 0)
            {
                e.Fuel = Mathf.Min(e.Fuel + fuel * 4f, 900f);
                if (!Session.Creative) Inventory.TakeFrom(Selected);
                Hud.Instance?.ShowMessage($"Топливо: {Mathf.CeilToInt(e.Fuel)} с", 1.5f);
                return true;
            }
            Hud.Instance?.ShowMessage(e.Lit ? (e.Fuel < 0 ? "Очаг аула горит всегда" : $"Горит ещё {Mathf.CeilToInt(e.Fuel)} с — подкинь дров") : "Костёр погас — нужно огниво или факел", 2f);
            return true;
        }

        /// <summary>Items used on the world: buckets scoop and pour water.</summary>
        bool UseItem(World w, bool blockHit, World.RayHit rh, Stack held)
        {
            if (held.Id == (int)BlockType.LilyPad)
            {
                // lily pads go on the water surface the player looks at
                if (!WaterAlongAim(w, out var cell)) return false;
                while (w.GetBlock(cell + Vector3Int.up) == BlockType.Water) cell += Vector3Int.up;
                var top = cell + Vector3Int.up;
                if (w.GetBlock(top) != BlockType.Air) return false;
                w.SetBlock(top.x, top.y, top.z, BlockType.LilyPad);
                if (!Session.Creative) Inventory.TakeFrom(Selected);
                return true;
            }
            if (held.Id == (int)ItemType.Bucket)
            {
                if (!WaterAlongAim(w, out _)) return false;
                TakeOne(new Stack((int)ItemType.BucketWater, 1));
                Hud.Instance?.ShowMessage("Набрали воды", 1.2f);
                return true;
            }
            if (held.Id == (int)ItemType.BucketWater && blockHit)
            {
                var at = rh.Prev;
                if (!BlockInfo.IsReplaceable(w.GetBlock(at))) return false;
                w.SetBlock(at.x, at.y, at.z, BlockType.Water);
                if (!Session.Creative) Inventory[Selected] = new Stack((int)ItemType.Bucket, 1);
                return true;
            }
            return false;
        }

        bool UseOnAnimal(Animal a, Stack held)
        {
            if (held.Id == (int)ItemType.Bucket && (a.Sp.Name == "Cow" || a.Sp.Name == "Goat" || a.Sp.Name == "Camel" || a.Sp.Name == "Horse"))
            {
                TakeOne(new Stack((int)ItemType.BucketMilk, 1));
                Hud.Instance?.ShowMessage(a.Sp.Name == "Horse" ? "Надоили кобылье молоко — для кумыса!" : "Надоили молока", 1.6f);
                Hands?.Swing();
                return true;
            }
            return false;
        }

        bool WaterAlongAim(World w, out Vector3Int cell)
        {
            var dir = Cam.transform.forward;
            for (float t = 0.5f; t < Reach; t += 0.25f)
            {
                var p = Eye + dir * t;
                cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
                var b = w.GetBlock(cell);
                if (b == BlockType.Water) return true;
                if (BlockInfo.IsSolid(b)) break;
            }
            cell = default;
            return false;
        }

        /// <summary>Uses up one of the selected stack and gives <paramref name="result"/> (bucket → filled bucket).</summary>
        void TakeOne(Stack result)
        {
            if (Session.Creative) { Give(result); return; }
            if (Inventory[Selected].Count == 1) Inventory[Selected] = result;
            else { Inventory.TakeFrom(Selected); Give(result); }
        }

        void Give(Stack s)
        {
            int left = Inventory.Add(s);
            if (left > 0) ItemDrop.Spawn(s.WithCount(left), Eye, Cam.transform.forward * 2f);
        }

        // ------------------------------------------------------------------ sleeping, death, respawn
        void Sleep(BlockEntity bed)
        {
            var vis = WorldVisuals.Instance;
            bedSpawn = new Vector3(bed.Pos.x + 0.5f, bed.Pos.y + 0.6f, bed.Pos.z + 0.5f);
            if (vis == null || vis.SunElevation > 0.02f) { Hud.Instance?.ShowMessage("Точка возрождения — у корпе. Спать можно только ночью.", 3f); return; }
            if (EnemiesNear != null && EnemiesNear(transform.position, 10f)) { Hud.Instance?.ShowMessage("Нельзя спать — рядом враги!", 2.5f); return; }
            Hud.Instance?.Sleep(() =>
            {
                vis.TimeOfDay = 0.26f;            // sunrise
                Stats.AddExhaustion(2f);
            });
        }

        void OnDied()
        {
            if (Riding != null) Dismount();
            if (Session.Creative) return;
            // everything spills on the spot, like Minecraft
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = Inventory[i];
                if (s.IsEmpty) continue;
                ItemDrop.Spawn(s, transform.position + Vector3.up * 0.8f, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(2f, 4f), Random.Range(-2.5f, 2.5f)), 2f);
            }
            Inventory.Clear();
            Stats.Level = 0; Stats.Xp = 0;
        }

        /// <summary>Q throws one item (Ctrl+Q the whole stack) forward, like Minecraft.</summary>
        void DropSelected(bool all)
        {
            var s = Inventory[Selected];
            int n = all ? s.Count : 1;
            Inventory.TakeFrom(Selected, n);
            ItemDrop.Spawn(s.WithCount(n), Eye - Vector3.up * 0.3f + Cam.transform.forward * 0.4f, Cam.transform.forward * 5f + Vector3.up * 1.5f, 1.5f);
            Hands?.Swing();
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
            var w = World.Instance;
            bool bedOk = bedSpawn.HasValue && w.GetBlock(Vector3Int.FloorToInt(bedSpawn.Value)) == BlockType.Bed;
            if (bedSpawn.HasValue && !bedOk) { Hud.Instance?.ShowMessage("Твоя корпе пропала — возрождение в начале", 3f); bedSpawn = null; }
            transform.position = bedOk ? bedSpawn.Value + Vector3.up * 0.6f : spawnPoint + Vector3.up;
            cc.enabled = true;
            vy = 0; knock = Vector3.zero;
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

        bool PlayerOverlaps(Vector3Int b, Shape shape, BlockType type)
        {
            if (shape == Shape.Cross || shape == Shape.Flat || shape == Shape.Ladder || type == BlockType.Torch) return false;
            var bounds = cc.bounds;
            bounds.Expand(-0.02f);
            var block = new Bounds(new Vector3(b.x + 0.5f, b.y + 0.5f, b.z + 0.5f), Vector3.one);
            return bounds.Intersects(block);
        }
    }
}
