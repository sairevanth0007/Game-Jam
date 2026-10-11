using System;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Receiver for the hero clips' animation events (added by EditorTools.ArtImport on the UAL clips):
    /// <c>OnFootstep</c> on walk/jog/sprint foot contacts and <c>OnLand</c> on Jump_Land's impact frame.
    /// Raises a little footstep dust and the <see cref="Footstep"/> / <see cref="Landed"/> hooks (audio can
    /// subscribe). Sits on the model next to the Animator.
    /// </summary>
    public class UmbraAnimEvents : MonoBehaviour
    {
        /// <summary>World position of the foot contact (feet level). Optional sound hook.</summary>
        public static event Action<Vector3> Footstep;
        /// <summary>World position of the landing impact. Optional sound hook.</summary>
        public static event Action<Vector3> Landed;

        UmbraController _ctl;
        float _lastStep;

        void Awake() { _ctl = GetComponentInParent<UmbraController>(); }

        public void OnFootstep(AnimationEvent e)
        {
            // Blend trees fire the events of every clip; only the dominant clip counts.
            if (e != null && e.animatorClipInfo.clip != null && e.animatorClipInfo.weight < 0.5f) return;
            if (_ctl == null || !_ctl.Grounded || Time.time - _lastStep < 0.12f) return;
            _lastStep = Time.time;
            var p = _ctl.transform.position;
            float speed = Mathf.Abs(_ctl.Velocity.x) / Mathf.Max(0.01f, _ctl.maxSpeed);
            if (speed > 0.45f) UmbraFx.Dust(p, 0.08f + 0.12f * speed);
            Footstep?.Invoke(p);
        }

        public void OnLand()
        {
            if (_ctl == null) return;
            var p = _ctl.transform.position;
            UmbraFx.Dust(p, Mathf.Clamp01(_ctl.LastLandingSpeed / 18f) * 0.6f + 0.15f);
            Landed?.Invoke(p);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Footstep = null; Landed = null; }
    }
}
