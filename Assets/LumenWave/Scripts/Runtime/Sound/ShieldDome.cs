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
            if (on) { _scale = 0.2f; SetVisible(true); }
            if (!on) ShieldState.Active = false;
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
            if (IsActive) ShieldState.Active = false;
        }

        void OnDestroy()
        {
            if (IsActive) ShieldState.Active = false;
            if (_mat != null) Destroy(_mat);
        }
    }
}
