using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Umbra's trailing scarf: a verlet chain. Each point keeps its previous position, so velocity is
    /// implicit (x' = x + (x − x_prev)·damping + a·dt²), then distance constraints are relaxed a few times.
    /// Rendered as a tapered line ribbon whose tip glows faintly.
    /// </summary>
    public class UmbraScarf : MonoBehaviour
    {
        public int points = 13;
        public float segment = 0.15f;
        public float damping = 0.93f;
        public float gravity = 4.5f;
        public float wind = 1.6f;
        public int iterations = 5;

        Transform _anchor;
        UmbraController _ctl;
        Vector3[] _p, _prev;
        LineRenderer _line;
        TrailRenderer _trail;
        UmbraRig _rig;
        bool _init;

        public void Init(Transform anchor, UmbraController ctl)
        {
            _anchor = anchor;
            _ctl = ctl;
            _p = new Vector3[points];
            _prev = new Vector3[points];
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = points;
            _line.useWorldSpace = true;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _line.receiveShadows = false;
            _line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.35f, 0.17f), new Keyframe(0.75f, 0.1f), new Keyframe(1f, 0.025f));
            _line.sharedMaterial = MaterialLibrary.Tinted("ParticleAlpha", Color.white);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.01f, 0.01f, 0.02f), 0f), new GradientColorKey(new Color(0.02f, 0.03f, 0.05f), 0.75f), new GradientColorKey(new Color(0.3f, 0.75f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0.85f, 1f) });
            _line.colorGradient = g;

            // Faint glowing trail off the scarf tip.
            var tip = new GameObject("ScarfTrail");
            tip.layer = gameObject.layer;
            tip.transform.SetParent(transform, false);
            _trail = tip.AddComponent<TrailRenderer>();
            _trail.time = 0.22f;
            _trail.minVertexDistance = 0.04f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.alignment = LineAlignment.View;
            _trail.sharedMaterial = MaterialLibrary.Tinted("ParticleAdditive", Color.white);
            var tg = new Gradient();
            tg.SetKeys(new[] { new GradientColorKey(new Color(0.35f, 0.8f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.5f, 1f), 1f) },
                       new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = tg;
            Reset();
        }

        public void Reset()
        {
            if (_anchor == null || _p == null) return;
            var a = AnchorPos();
            int f = _ctl != null ? _ctl.Facing : 1;
            for (int i = 0; i < points; i++)
            {
                _p[i] = a + new Vector3(-f * segment * i, -0.03f * i, 0f);
                _prev[i] = _p[i];
            }
            if (_trail != null) { _trail.transform.position = _p[points - 1]; _trail.Clear(); }
            _init = true;
        }

        Vector3 AnchorPos()
        {
            var a = _anchor.position;
            a.z = 0.02f;
            return a;
        }

        void LateUpdate()
        {
            if (_anchor == null || _p == null) return;
            if (!_init) Reset();
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;

            // Teleport detection (respawn).
            if ((AnchorPos() - _p[0]).sqrMagnitude > 9f) Reset();

            int f = _ctl != null ? _ctl.Facing : 1;
            float t = Time.time;
            _p[0] = AnchorPos();
            for (int i = 1; i < points; i++)
            {
                var cur = _p[i];
                var vel = (cur - _prev[i]) * damping;
                float k = i / (float)(points - 1);
                // Gentle wind flutter + a constant trailing breeze behind Umbra.
                float flutter = (Mathf.PerlinNoise(t * 1.7f, i * 0.31f) - 0.5f) * 2f;
                var acc = new Vector3(-f * wind * (0.6f + 0.4f * k), -gravity + flutter * wind * 1.6f * k, 0f);
                _prev[i] = cur;
                _p[i] = cur + vel + acc * dt * dt;
                _p[i].z = 0.02f + k * 0.04f;
            }
            for (int it = 0; it < iterations; it++)
            {
                _p[0] = AnchorPos();
                for (int i = 0; i < points - 1; i++)
                {
                    var d = _p[i + 1] - _p[i];
                    float len = d.magnitude;
                    if (len < 1e-5f) continue;
                    float diff = (len - segment) / len;
                    if (i == 0) _p[i + 1] -= d * diff;
                    else { _p[i] += d * diff * 0.5f; _p[i + 1] -= d * diff * 0.5f; }
                }
            }
            _line.SetPositions(_p);
            bool visible = _anchor.lossyScale.y > 0.05f;
            if (_rig == null && _ctl != null) _rig = _ctl.GetComponent<UmbraRig>();
            if (_rig != null && _rig.Hero != null && _rig.Hero.Dissolve > 0.35f) visible = false;
            _line.enabled = visible;
            if (_trail != null)
            {
                _trail.transform.position = _p[points - 1];
                _trail.emitting = visible;
            }
        }
    }
}
