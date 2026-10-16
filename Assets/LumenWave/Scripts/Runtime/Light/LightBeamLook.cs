using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Danger readability for beams (visual only, gameplay numbers untouched). Every colour/effect has one look,
    /// kept through mirrors and prisms because it is chosen per segment colour:
    ///  - red BURN: thin, soft, semi-transparent, gentle flicker, a few sparks;
    ///  - red LETHAL: thick, white-hot core with red-orange edges, heat shimmer, pulsing glow, crackling sparks and
    ///    embers where it lands, and a warning triangle on its emitter;
    ///  - yellow beat: faint guide, nervous charge-up flicker before the beat, hard flash on it;
    ///  - white: thin, clean, steady; green heal: soft rising motes; blue low-gravity: slow floating specks.
    /// All beams get brighter/wider near the source (inverse square in the shader) and a floor glow sized by the
    /// gameplay intensity where they land.
    /// </summary>
    public partial class LightBeam
    {
        public struct Look
        {
            public float coreW, haloW, coreI, haloI, flicker, hot, wobble, pulse, dust;
        }

        static readonly Look Default = new Look { coreW = 0.45f, haloW = 2.6f, coreI = 1.7f, haloI = 0.45f, flicker = 0.15f, dust = 0.6f };
        static readonly Look BurnLook = new Look { coreW = 0.28f, haloW = 1.7f, coreI = 0.85f, haloI = 0.26f, flicker = 0.35f, dust = 0.7f };
        static readonly Look LethalLook = new Look { coreW = 0.42f, haloW = 3.4f, coreI = 1.5f, haloI = 1.0f, flicker = 0.04f, hot = 1f, wobble = 0.12f, pulse = 1f, dust = 0.35f };
        static readonly Look WhiteLook = new Look { coreW = 0.26f, haloW = 1.3f, coreI = 1.4f, haloI = 0.2f, flicker = 0f, dust = 0.15f };
        static readonly Look YellowLook = new Look { coreW = 0.5f, haloW = 2.4f, coreI = 1.9f, haloI = 0.5f, flicker = 0f, dust = 0.5f };

        /// <summary>The look for a segment of the given colour (red uses this beam's <see cref="Danger"/>).</summary>
        public Look LookFor(LightColor c)
        {
            if (c == LightColor.Yellow) return YellowLook;
            if (c == LightColor.White) return WhiteLook;
            if ((c & LightColor.Red) != 0)
            {
                var d = Danger;
                return d == BeamDanger.Lethal ? LethalLook : BurnLook;
            }
            return Default;
        }

        MeshRenderer _floorGlow, _warning, _floorWarning;
        ParticleSystem _embers, _motes;
        LightColor _motesColor;

        /// <summary>Yellow: charge-up flicker before the beat, a hard flash on it.</summary>
        float YellowBoost(out float flicker)
        {
            flicker = 0f;
            if (!IsBeatGated) return 1f;
            float ph = BeatClock.CurrentPhase;
            if (IsLit) return 1f + 1.4f * Mathf.Clamp01(1f - ph / 0.1f);
            if (ph > 0.7f) flicker = 0.9f;
            return 1f;
        }

        void ApplyLook(MeshRenderer r, Look L, bool core, float flicker)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Flicker", Mathf.Max(flicker, L.flicker));
            _mpb.SetFloat("_HotCore", core ? L.hot : 0f);   // the halo stays red-orange
            _mpb.SetFloat("_Wobble", L.wobble);
            _mpb.SetFloat("_Pulse", L.pulse);
            _mpb.SetFloat("_Dust", L.dust);
            r.SetPropertyBlock(_mpb);
        }

        void SetPathStart(MeshRenderer r, float pathStart)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_PathStart", pathStart);
            _mpb.SetFloat("_NearRadius", 3f + width * 2f);
            r.SetPropertyBlock(_mpb);
        }

        void UpdateDangerExtras(bool anyHit, Vector3 hit, Vector3 normal, LightColor hitColor, float hitPath, float level)
        {
            var danger = (color & LightColor.Red) != 0 && color != LightColor.White && !IsBeatGated ? Danger : BeamDanger.Safe;
            bool lethal = danger == BeamDanger.Lethal;

            // Floor glow sized/brightened by the gameplay intensity where the beam lands.
            if (_floorGlow == null) _floorGlow = LightVisuals.Glow(_visualRoot, Vector3.zero, 1f, Color.black, "FloorGlow");
            bool showFloor = anyHit && level > 0.01f;
            _floorGlow.gameObject.SetActive(showFloor);
            if (showFloor)
            {
                float I = LightField.BeamIntensityAt(intensity, hitPath, LightField.FogSigma);
                float s = Mathf.Clamp(0.7f + 1.1f * Mathf.Sqrt(I), 0.6f, 4.5f) * (lethal ? 1.3f : 1f);
                _floorGlow.transform.position = hit + normal * 0.08f + new Vector3(0f, 0f, -0.25f);
                _floorGlow.transform.rotation = Quaternion.identity;
                _floorGlow.transform.localScale = new Vector3(s * 1.6f, s * 0.7f, 1f);
                var c = LightMath.DisplayColor(hitColor);
                if (lethal && (hitColor & LightColor.Red) != 0) c = Color.Lerp(c, new Color(1f, 0.6f, 0.25f), 0.5f);
                float b = Mathf.Clamp(0.25f + 0.35f * Mathf.Sqrt(I), 0.2f, 1.6f) * level * (lethal ? 1f + 0.25f * Mathf.Sin(Time.time * 9f) : 1f);
                LightVisuals.SetGlowColor(_floorGlow, _mpb, c * b);
            }

            // Lethal: embers + strong sparks at the hit, warning glyph on the emitter.
            if (lethal && _embers == null) _embers = LightVisuals.Embers(_visualRoot);
            if (_embers != null)
            {
                var em = _embers.emission;
                em.enabled = lethal && anyHit && IsLit;
                if (anyHit) { _embers.transform.position = hit + normal * 0.05f; _embers.transform.rotation = Quaternion.LookRotation(normal); }
            }
            if (_sparks != null)
            {
                var em = _sparks.emission;
                em.rateOverTime = lethal ? 75f : danger == BeamDanger.Burn ? 10f : 20f;
                var main = _sparks.main;
                main.startSize = lethal ? new ParticleSystem.MinMaxCurve(0.06f, 0.2f) : new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.startSpeed = lethal ? new ParticleSystem.MinMaxCurve(2f, 5f) : new ParticleSystem.MinMaxCurve(1f, 3f);
            }
            if (lethal && showFixture && _warning == null) BuildWarning();
            if (_warning != null)
            {
                _warning.gameObject.SetActive(lethal && on);
                float p = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
                _warning.transform.localScale = new Vector3(0.9f, 0.9f, 1f) * p;
            }
            // A second mark beside where it lands (the emitter is often above the frame).
            if (lethal && showFixture && _floorWarning == null) _floorWarning = WarningQuad(_visualRoot, "LethalFloorWarning");
            if (_floorWarning != null)
            {
                bool show = lethal && on && anyHit && normal.y > 0.5f;
                _floorWarning.gameObject.SetActive(show);
                if (show)
                {
                    float p = 1f + 0.1f * Mathf.Sin(Time.time * 6f);
                    _floorWarning.transform.position = hit + new Vector3(-1.5f, 1.1f, 0.6f);   // behind Umbra's plane: never covers her
                    _floorWarning.transform.rotation = Quaternion.identity;
                    _floorWarning.transform.localScale = new Vector3(0.7f, 0.7f, 1f) * p;
                }
            }

            // Green / blue effect motes along the first segment.
            var effect = color == LightColor.Green || color == LightColor.Blue ? color : LightColor.None;
            if (effect != LightColor.None && Segments.Count > 0 && level > 0.01f)
            {
                if (_motes == null || _motesColor != effect)
                {
                    if (_motes != null) Destroy(_motes.gameObject);
                    _motes = LightVisuals.EffectMotes(_visualRoot, effect, "BeamMotes");
                    _motesColor = effect;
                }
                var s = Segments[0];
                float len = s.Length;
                var dir = s.Direction;
                _motes.transform.position = (s.start + s.end) * 0.5f;
                _motes.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(dir.x, dir.y, 0f));
                var sh = _motes.shape; sh.scale = new Vector3(width * 1.2f, len, 0.4f);
                var em = _motes.emission; em.rateOverTime = Mathf.Clamp(len * 1.6f, 4f, 30f);
            }
            else if (_motes != null)
            {
                var em = _motes.emission; em.rateOverTime = 0f;
            }
        }

        static MeshRenderer WarningQuad(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            var m = MaterialLibrary.Create("ParticleAdditive");
            m.name = "LW_LethalWarning";
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", LightVisuals.WarningTexture);
            m.SetColor("_BaseColor", new Color(2.4f, 1.1f, 0.25f, 1f));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void BuildWarning()
        {
            var r0 = WarningQuad(transform, "LethalWarning");
            var go = r0.gameObject;
            // Beside the emitter, on the side away from the beam, facing the camera.
            var d = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.down;
            var side = new Vector2(-d.y, d.x);
            if (side.y < 0f) side = -side;
            var off = -d * 0.9f + side * 0.9f;
            go.transform.position = transform.position + new Vector3(off.x, off.y, -0.4f);
            go.transform.rotation = Quaternion.identity;
            _warning = r0;
        }
    }
}
