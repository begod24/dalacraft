using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dala
{
    /// <summary>
    /// Minecraft-style first-person view: the right arm (Blender model Models/Player/SM_FP_Arm) with the held item,
    /// drawn by an overlay camera on its own layer so it never clips into walls. Procedural motion: swing (mine / hit /
    /// place), equip when the slot changes, walk bob, look sway and eating at the mouth.
    /// </summary>
    public class FirstPersonHands : MonoBehaviour
    {
        public static int Layer => LayerMask.NameToLayer("ViewModel") is var l && l >= 0 ? l : 8;

        Camera overlay;
        Transform pivot, arm, itemSocket;
        GameObject itemModel;
        int heldId = -1, shownId = -1;
        float swing = 1f, equip = 1f, bobDist, bobAmount, eat;
        Vector2 sway;
        Quaternion lastLook;

        // Poses are given by where the FIST is in camera space; the shoulder stays below the screen edge (arm length 0.75 m).
        const float ArmLength = 0.75f;
        static readonly Vector3 FistRest = new(0.40f, -0.25f, 0.56f);
        static readonly Quaternion RotRest = Quaternion.Euler(-36f, -18f, 8f);       // pointing up-forward, slightly inwards
        static readonly Vector3 FistEat = new(0.14f, -0.21f, 0.50f);
        static readonly Quaternion RotEat = Quaternion.Euler(-48f, -42f, 12f);

        public static FirstPersonHands Create(Camera main)
        {
            var go = new GameObject("FirstPersonHands");
            go.transform.SetParent(main.transform, false);
            var h = go.AddComponent<FirstPersonHands>();
            h.Build(main);
            return h;
        }

        void Build(Camera main)
        {
            int layer = Layer;
            main.cullingMask &= ~(1 << layer);
            overlay = new GameObject("HandsCamera").AddComponent<Camera>();
            overlay.transform.SetParent(transform, false);
            overlay.cullingMask = 1 << layer;
            overlay.fieldOfView = 70f;                         // fixed, so hands look the same at any FOV setting
            overlay.nearClipPlane = 0.01f; overlay.farClipPlane = 5f;
            var od = overlay.GetUniversalAdditionalCameraData();
            od.renderType = CameraRenderType.Overlay;
            od.renderShadows = false;
            od.renderPostProcessing = true;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            arm = new GameObject("Arm").transform;
            arm.SetParent(pivot, false);
            var prefab = Resources.Load<GameObject>("Models/Player/SM_FP_Arm");
            if (prefab != null)
            {
                var a = Instantiate(prefab, arm, false);
                // Blender -Y (along the arm) = Unity +Z; the hand is the far end
                a.transform.localRotation = Quaternion.identity;
                foreach (var r in a.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            }
            itemSocket = new GameObject("ItemSocket").transform;
            itemSocket.SetParent(arm, false);
            itemSocket.localPosition = new Vector3(0, 0.02f, ArmLength + 0.02f);   // just beyond the knuckles
            ItemVisuals.SetLayer(gameObject, layer);
            lastLook = transform.rotation;
        }

        // ---------------------------------------------------------------- API used by PlayerController
        public void Swing() { if (swing > 0.5f) swing = 0f; }
        public void SetItem(int id) => heldId = id;
        public void SetWalk(float speed, bool grounded)
        {
            bobAmount = Mathf.MoveTowards(bobAmount, grounded ? Mathf.Clamp01(speed / 4.5f) : 0f, Time.deltaTime * 4f);
            bobDist += speed * Time.deltaTime * 0.55f;
        }
        /// <summary>0 = not eating, (0..1] eating progress.</summary>
        public void SetEating(float progress) => eat = progress;
        public void SetVisible(bool v) { pivot.gameObject.SetActive(v); overlay.enabled = v; }

        void LateUpdate() => Animate(Time.deltaTime);

        /// <summary>Advances the hand motion by <paramref name="dt"/> and poses the arm (public for editor previews).</summary>
        public void Animate(float dt)
        {
            if (heldId != shownId && equip >= 1f) { equip = 0f; }
            equip = Mathf.Min(1f, equip + dt / 0.25f);
            // halfway through the equip dip the new item appears
            if (equip >= 0.5f && heldId != shownId) RebuildItem();
            swing = Mathf.Min(1f, swing + dt / 0.28f);

            // look sway: the arm lags slightly behind camera rotation
            var d = Quaternion.Inverse(lastLook) * transform.rotation;
            lastLook = transform.rotation;
            var e = d.eulerAngles;
            float dx = Mathf.DeltaAngle(0, e.y), dy = Mathf.DeltaAngle(0, e.x);
            sway = Vector2.Lerp(sway, new Vector2(Mathf.Clamp(-dx * 0.012f, -0.06f, 0.06f), Mathf.Clamp(dy * 0.012f, -0.06f, 0.06f)), dt * 10f);

            var fist = FistRest; var rot = RotRest;

            // walk bob (Minecraft: a figure-eight of the hand)
            float b = bobDist * Mathf.PI;
            fist += new Vector3(Mathf.Sin(b) * 0.035f, -Mathf.Abs(Mathf.Cos(b)) * 0.045f, 0) * bobAmount;
            rot = Quaternion.Euler(Mathf.Abs(Mathf.Cos(b)) * 3f * bobAmount, 0, Mathf.Sin(b) * 2.5f * bobAmount) * rot;
            fist += new Vector3(sway.x, sway.y, 0);

            // equip: dip down out of view and back up
            fist.y -= Mathf.Sin(equip * Mathf.PI) * 0.5f;

            // eating: bring the item to the mouth and shake it
            if (eat > 0f)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Min(1f, eat * 4f));
                fist = Vector3.Lerp(fist, FistEat, k);
                rot = Quaternion.Slerp(rot, RotEat, k);
                fist.y += Mathf.Abs(Mathf.Sin(Time.time * 22f)) * 0.02f * k;
            }

            // swing — Minecraft's attack curve: the fist darts in towards the crosshair and the hand turns about the
            // wrist so the tool chops forward and down onto the block, then comes back; the arm stays in view
            var wrist = Quaternion.identity;
            if (swing < 1f)
            {
                float sq = Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI), sp = Mathf.Sin(swing * swing * Mathf.PI);
                fist += new Vector3(-0.20f * sq, 0.08f * Mathf.Sin(Mathf.Sqrt(swing) * Mathf.PI * 2f) - 0.04f * sq, -0.06f * Mathf.Sin(swing * Mathf.PI));
                rot = Quaternion.Euler(12f * sq, -12f * sp, 0) * rot;                    // the arm only nods a little
                wrist = Quaternion.Euler(60f * sq, -15f * sp, 20f * sq);                // the hand chops (camera space)
            }
            var shoulder = fist - rot * Vector3.forward * ArmLength;
            itemSocket.localRotation = Quaternion.Inverse(rot) * wrist * rot;

            pivot.localPosition = shoulder;
            pivot.localRotation = rot;
        }

        void RebuildItem()
        {
            shownId = heldId;
            ItemVisuals.Kill(itemModel);
            itemModel = null;
            var def = shownId > 0 ? Items.Get(shownId) : null;
            if (def == null) return;
            bool cube = def.IsBlock && !BlockInfo.IsEntity(def.Block);
            itemModel = ItemVisuals.Create(shownId, itemSocket, cube ? 0.24f : def.Tool != ToolKind.None ? 0.42f : 0.26f, false);
            if (itemModel == null) return;
            // orientations are chosen in camera space (as the player sees them) and converted into the arm's frame
            Quaternion seen;
            Vector3 offset;
            if (cube) { seen = Quaternion.Euler(-15f, 40f, 0f); offset = new Vector3(-0.02f, 0.08f, 0.02f); }      // a block showing top and two sides
            else if (def.Tool != ToolKind.None)
            {
                // tools are extruded icons (handle at the icon's bottom-left = model +X/-Y, head towards -X/+Y):
                // the handle end sits in the fist, the head leans up, away and a little left, the flat side turned to the eye
                const float size = 0.42f;
                var headM = new Vector3(-1, 1, 0).normalized;
                var head = new Vector3(-0.35f, 0.8f, 0.5f).normalized;
                var face = Vector3.ProjectOnPlane(new Vector3(-0.75f, 0.1f, -0.65f), head).normalized;
                seen = Quaternion.LookRotation(face, head) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, headM));
                var grip = -headM * size * 0.62f;                     // the handle end, from the model centre
                offset = -(seen * grip) + new Vector3(0.01f, -0.03f, -0.02f);
            }
            else { seen = Quaternion.Euler(-10f, 60f, 15f); offset = new Vector3(-0.02f, 0.07f, 0.02f); }
            itemModel.transform.localRotation = Quaternion.Inverse(RotRest) * seen;
            itemModel.transform.localPosition = Quaternion.Inverse(RotRest) * offset;
            ItemVisuals.SetLayer(itemModel, Layer);
        }
    }
}
