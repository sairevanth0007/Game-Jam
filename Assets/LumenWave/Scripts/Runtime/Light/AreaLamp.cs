using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// A hanging lantern that floods a circular zone with coloured light. Exposure follows the
    /// inverse-square law from the lamp (with fog), blocked by solid geometry and the shield.
    /// Green lamps = healing sanctuaries, blue lamps = low-gravity zones.
    /// </summary>
    public class AreaLamp : MonoBehaviour
    {
        public LightColor color = LightColor.Green;
        public float radius = 5f;
        public float intensity = 1f;
        public bool beatGated;
        public bool on = true;

        Light _light;
        MeshRenderer _glow, _zone, _core;
        MaterialPropertyBlock _mpb;
        float _flicker;

        public bool IsBeatGated => beatGated || color == LightColor.Yellow;

        void OnEnable() { if (!LightField.Lamps.Contains(this)) LightField.Lamps.Add(this); }
        void OnDisable() { LightField.Lamps.Remove(this); }

        public void BuildVisuals(bool hanging)
        {
            _mpb = new MaterialPropertyBlock();
            var c = LightMath.DisplayColor(color);
            var dark = MaterialLibrary.Tinted("Lit", new Color(0.05f, 0.05f, 0.07f));
            if (hanging)
            {
                // Chain up to the ceiling if there is one within reach; otherwise a floating lantern.
                Physics.SyncTransforms();
                if (Physics.Raycast(transform.position, Vector3.up, out var hit, 8f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                {
                    float chain = hit.distance - 0.3f;
                    if (chain > 0.05f)
                        LightVisuals.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0.3f + chain * 0.5f, 0), new Vector3(0.05f, chain * 0.5f, 0.05f), dark, "Chain");
                }
                LightVisuals.Part(PrimitiveType.Cube, transform, new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.1f, 0.5f), dark, "Cap");
            }
            var lensMat = MaterialLibrary.Tinted("LitEmissive", c * 0.5f, c * 4f);
            var core = LightVisuals.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.45f, lensMat, "Bulb");
            _core = core.GetComponent<MeshRenderer>();
            _glow = LightVisuals.Glow(transform, new Vector3(0, 0, -0.3f), 2.2f, c * 1.5f, "Glow");
            // Faint disc showing the zone of effect — readability over realism.
            _zone = LightVisuals.Glow(transform, new Vector3(0, 0, 0.6f), radius * 2.2f, c * 0.22f, "Zone");
            _light = LightVisuals.AddLight(transform, LightType.Point, c, 3f * intensity, radius * 1.6f, "LampLight");
            _light.transform.localPosition = new Vector3(0, 0, -1.5f);
            _flicker = Random.value * 10f;
        }

        void Update()
        {
            if (_light == null) return;
            float level = Level();
            float wobble = 0.93f + 0.07f * Mathf.Sin((Time.time + _flicker) * 3.1f) * Mathf.Sin((Time.time + _flicker) * 1.7f);
            var c = LightMath.DisplayColor(color);
            _light.enabled = level > 0.01f;
            _light.intensity = 3f * intensity * level * wobble;
            if (_glow) LightVisuals.SetGlowColor(_glow, _mpb, c * 1.5f * Mathf.Max(0.1f, level) * wobble);
            if (_zone) LightVisuals.SetGlowColor(_zone, _mpb, c * 0.22f * level);
        }

        float Level()
        {
            if (!on) return 0f;
            if (!IsBeatGated) return 1f;
            if (LightField.BeatHot) return 1f;
            float ph = BeatClock.CurrentPhase;
            return ph > 0.7f ? Mathf.Lerp(0.1f, 0.4f, (ph - 0.7f) / 0.3f) : 0.08f;
        }

        public void AddExposure(Vector3 p, bool hot, ref LightExposure e)
        {
            if (!on) return;
            if (IsBeatGated && !hot) return;
            var lp = transform.position;
            var to = new Vector3(p.x - lp.x, p.y - lp.y, 0f);
            float d = to.magnitude;
            if (d > radius) return;
            float I = LightField.LampIntensityAt(intensity, d, radius, LightField.FogSigma);
            if (I <= 0f) return;
            // Occlusion: solids, crystals, bridges and the shield cast shadows.
            if (d > 0.3f)
            {
                LightField.SyncPhysics();
                int mask = Layers.Mask(Layers.Ground, Layers.Crystal, Layers.Bridge, Layers.Shield);
                if (Physics.Raycast(new Vector3(lp.x, lp.y, 0f), to / d, d - 0.05f, mask, QueryTriggerInteraction.Ignore)) return;
            }
            if (color == LightColor.Yellow) e.yellowPulse += I;
            else e.Add(color, I);
        }
    }
}
