using UnityEngine;
using UnityEngine.Rendering;

namespace Dala
{
    /// <summary>The player's body: Blender model Resources/Models/KazakhMan.fbx with Idle/Walk/Run/Mine/Ride takes.</summary>
    public class PlayerAvatar : MonoBehaviour
    {
        ClipPlayer anim;
        Renderer[] renderers;
        bool firstPerson = true;

        void Awake()
        {
            var model = ClipPlayer.Spawn("Models/KazakhMan", transform, out anim);
            renderers = model != null ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
            anim?.Play("Idle", 0f);
            SetFirstPerson(true);
        }

        /// <summary>In first person the body only casts shadows, so you see your shadow but not the inside of your head.</summary>
        public void SetFirstPerson(bool fp)
        {
            firstPerson = fp;
            foreach (var r in renderers) r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        public void SetMotion(float horizontalSpeed, bool riding)
        {
            if (anim == null) return;
            if (riding) anim.Play("Ride", 0.3f);
            else if (horizontalSpeed > 6.5f) anim.Play("Run", 0.2f, Mathf.Clamp(horizontalSpeed / 8.5f, 0.8f, 1.3f));
            else if (horizontalSpeed > 0.4f) anim.Play("Walk", 0.2f, Mathf.Clamp(horizontalSpeed / 4f, 0.6f, 1.4f));
            else anim.Play("Idle", 0.35f);
        }

        public void Swing() => anim?.PlayOnce("Mine", 1.6f);

        void LateUpdate() => anim?.Tick(Time.deltaTime);
        void OnDestroy() => anim?.Destroy();
    }
}
