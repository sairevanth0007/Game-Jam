using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// RESONANCE SHIELD: a standing-wave dome around Umbra. Its surface shows Chladni figures
    /// (nodal lines of a vibrating plate, cos(nπx)cos(mπy) − cos(mπx)cos(nπy) = 0), stepping through
    /// modes on the beat. The collider (layer Shield) stops light beams; projectiles test ShieldState.
    /// </summary>
    public class ShieldDome : MonoBehaviour
    {
        public float radius = 1.6f;
        public bool IsActive { get; private set; }

        static readonly Vector2Int[] Modes =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 3), new Vector2Int(3, 4), new Vector2Int(2, 5),
            new Vector2Int(3, 5), new Vector2Int(1, 4), new Vector2Int(4, 5), new Vector2Int(3, 7),
        };

        SoundPowers _owner;
        SphereCollider _col;
        MeshRenderer _mr;
        Material _mat;
        float _scale;   // pop-in animation 0..1
        float _phase;
        int _mode;
        float _modeTimer;

        // ---- Visual-only: hit ripples (shader slots), beam contact sparks.
        static ShieldDome _active;
        /// <summary>Last unscaled time a lethal beam was stopped by the shield (read by the HUD for near-miss feedback).</summary>
        public static float LethalContactTime = -10f;
        static readonly int[] HitIds =
        {
            Shader.PropertyToID("_Hit0"), Shader.PropertyToID("_Hit1"), Shader.PropertyToID("_Hit2"), Shader.PropertyToID("_Hit3"),
        };
        readonly Vector4[] _hits = { new Vector4(0, 0, -1, -1), new Vector4(0, 0, -1, -1), new Vector4(0, 0, -1, -1), new Vector4(0, 0, -1, -1) };
        float _lastHitTime = -10f;
        Vector3 _lastHitDir;
        LightBeam[] _beams = System.Array.Empty<LightBeam>();
        float _beamScan, _beamSparkT, _beamRippleT = -10f;
        static readonly Color RippleColor = new Color(0.7f, 0.92f, 1.5f);

        /// <summary>Shows an impact ripple on the active shield at a world position (no-op without an active shield).</summary>
        public static void Ripple(Vector3 worldPos)
        {
            if (_active != null && _active.IsActive) _active.AddHit(worldPos, true);
        }

        void AddHit(Vector3 worldPos, bool sparks)
        {
            var local = worldPos - transform.position;
            local.z = Mathf.Min(local.z, -0.05f);   // impacts read on the camera-facing side
            if (local.sqrMagnitude < 1e-6f) local = Vector3.up;
            var dir = local.normalized;
            if (Time.time - _lastHitTime < 0.12f && Vector3.Dot(dir, _lastHitDir) > 0.85f) return;
            _lastHitTime = Time.time;
            _lastHitDir = dir;
            int slot = 0;
            float oldest = -1f;
            for (int i = 0; i < _hits.Length; i++)
            {
                float age = _hits[i].w < 0f ? 99f : _hits[i].w;
                if (age > oldest) { oldest = age; slot = i; }
            }
            var od = transform.InverseTransformDirection(dir).normalized;   // shader works in object space
            _hits[slot] = new Vector4(od.x, od.y, od.z, 0f);
            if (sparks)
            {
                var surface = transform.position + new Vector3(dir.x, dir.y, 0f).normalized * radius;
                ImpactFx.Sparks(surface, RippleColor, 8, 4f, 0.1f, 0.45f);
                ImpactFx.Ring(surface, RippleColor, 0.7f, 0.25f);
            }
        }

        void OnAbsorbed(Vector3 at) { if (IsActive) AddHit(at, true); }

        void OnEnable() { FlareBolt.Absorbed += OnAbsorbed; }

        void TickHits(float dt)
        {
            if (_mat == null) return;
            for (int i = 0; i < _hits.Length; i++)
            {
                if (_hits[i].w >= 0f)
                {
                    _hits[i].w += dt;
                    if (_hits[i].w > 0.9f) _hits[i].w = -1f;
                }
                _mat.SetVector(HitIds[i], _hits[i]);
            }
        }

        /// <summary>Beams that end on the dome make it ripple and spark at the contact point.</summary>
        void TickBeamContacts(float dt)
        {
            _beamScan -= dt;
            if (_beamScan <= 0f) { _beamScan = 0.5f; _beams = FindObjectsByType<LightBeam>(FindObjectsSortMode.None); }
            _beamSparkT -= dt;
            for (int b = 0; b < _beams.Length; b++)
            {
                var beam = _beams[b];
                if (beam == null || !beam.on) continue;
                var segs = beam.Segments;
                for (int i = 0; i < segs.Count; i++)
                {
                    if (segs[i].hitCollider != _col) continue;
                    if (beam.Danger == LightBeam.BeamDanger.Lethal) LethalContactTime = Time.unscaledTime;
                    if (Time.time - _beamRippleT > 0.4f) { _beamRippleT = Time.time; AddHit(segs[i].end, false); }
                    if (_beamSparkT <= 0f) ImpactFx.Sparks(segs[i].end, LightMath.DisplayColor(segs[i].color) * 2f, 3, 4f, 0.09f, 0.35f);
                }
            }
            if (_beamSparkT <= 0f) _beamSparkT = 0.05f;
        }

        public void Init(SoundPowers owner)
        {
            _owner = owner;
            gameObject.layer = Layers.Shield;
            transform.localPosition = Vector3.up * owner.centerHeight;

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp.GetComponent<Collider>());
            Destroy(tmp);

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mat = MaterialLibrary.Create("Shield");
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", new Color(0.55f, 0.85f, 1.4f, 1f));
            _mr.sharedMaterial = _mat;

            _col = gameObject.AddComponent<SphereCollider>();
            _col.radius = 0.5f;   // mesh is unit-diameter; scaled below
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // The dome must never push Umbra or anything else around: it only stops light rays.
            for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(Layers.Shield, l, true);

            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            _mr.enabled = v;
            _col.enabled = v;
        }

        public void SetActive(bool on)
        {
            if (on == IsActive) return;
            IsActive = on;
            if (on) { _scale = 0.2f; SetVisible(true); _active = this; }
            if (!on) ShieldState.Active = false;
            // Pop-in / collapse shock ring (visual only).
            ImpactFx.Ring(transform.position, RippleColor, radius * (on ? 1.35f : 0.9f), on ? 0.3f : 0.22f);
            if (on) ImpactFx.Flash(transform.position, new Color(0.45f, 0.7f, 1f), radius * 2.4f, 0.18f);
        }

        void Update()
        {
            if (IsActive) _scale = Mathf.MoveTowards(_scale, 1f, Time.deltaTime * 8f);
            else
            {
                _scale = Mathf.MoveTowards(_scale, 0f, Time.deltaTime * 6f);
                if (_scale <= 0f && _mr.enabled) SetVisible(false);
            }
            // Colliders are only active at full strength (no half-formed blocking).
            _col.enabled = IsActive;

            float wobble = 1f + 0.03f * Mathf.Sin(Time.time * 18f);
            transform.localScale = Vector3.one * (radius * 2f * EaseOutBack(_scale) * wobble);

            // Step through Chladni modes on the beat (or every 0.6 s without a clock).
            int beat = BeatClock.Instance != null ? BeatClock.Instance.Beat : Mathf.FloorToInt(Time.time / Harmony.SecondsPerBeat);
            int mode = ((beat % Modes.Length) + Modes.Length) % Modes.Length;
            if (mode != _mode) { _mode = mode; _modeTimer = 0f; }
            _modeTimer += Time.deltaTime;
            _phase += Time.deltaTime * 2f * Mathf.PI * 2.5f;
            if (_mat != null)
            {
                var m = Modes[_mode];
                if (_mat.HasProperty("_ModeN")) _mat.SetFloat("_ModeN", m.x);
                if (_mat.HasProperty("_ModeM")) _mat.SetFloat("_ModeM", m.y);
                if (_mat.HasProperty("_Phase")) _mat.SetFloat("_Phase", _phase);
                if (_mat.HasProperty("_Alpha")) _mat.SetFloat("_Alpha", Mathf.Clamp01(_scale) * (0.85f + 0.15f * Mathf.Exp(-_modeTimer * 6f)));
            }

            TickHits(Time.deltaTime);
            if (IsActive && _mr.enabled) TickBeamContacts(Time.deltaTime);

            if (IsActive)
            {
                ShieldState.Active = true;
                ShieldState.Center = transform.position;
                ShieldState.Radius = radius;
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        void OnDisable()
        {
            FlareBolt.Absorbed -= OnAbsorbed;
            if (IsActive) ShieldState.Active = false;
        }

        void OnDestroy()
        {
            if (_active == this) _active = null;
            if (IsActive) ShieldState.Active = false;
            if (_mat != null) Destroy(_mat);
        }
    }
}
