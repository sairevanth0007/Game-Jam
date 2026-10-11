using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// ECHO PATH: a sound wave leaves Umbra at a finite speed and freezes into a solid shadow bridge as
    /// its front passes. The underside and the glowing rim follow a sine whose wavelength is the real
    /// wavelength of the sung note (λ = v / f with v = 343 m/s, 1 unit = 1 m). Fades after a few seconds.
    /// </summary>
    public class EchoBridge : MonoBehaviour, IResettable
    {
        public const float SpeedOfSound = 343f;  // m/s — sets the wave shape (λ)
        public const float FrontSpeed = 18f;     // u/s — slowed travel of the visible wavefront (readability)
        public const float MaxLength = 7f;
        public const float Thickness = 0.32f;
        public const float Depth = 1.4f;
        public const float Lifetime = 4.5f;
        public const float FadeTime = 0.9f;

        public float Length { get; private set; }
        public float Built { get; private set; }
        public int Facing { get; private set; } = 1;
        public float Wavelength { get; private set; }
        public float Frequency { get; private set; }
        public bool Solid => _col != null && _col.enabled && Built > 0.05f;

        BoxCollider _col;
        Mesh _mesh;
        MeshFilter _mf;
        Material _mat;
        LineRenderer _rim, _front, _glow;
        float _moteAcc;
        bool _formed;
        static Texture2D _softAcross;
        float _age;
        float _amp = 0.18f;
        Color _glow0 = new Color(0.45f, 0.75f, 1.6f, 1f);

        public static EchoBridge Spawn(Vector3 feet, int facing, float frequency)
        {
            var go = new GameObject("EchoBridge");
            go.layer = Layers.Bridge;
            var parent = SoundPowers.SpawnParent;
            if (parent != null) go.transform.SetParent(parent, true);
            facing = facing >= 0 ? 1 : -1;
            var start = new Vector3(feet.x + facing * 0.3f, feet.y, 0f);
            go.transform.position = start;
            var b = go.AddComponent<EchoBridge>();
            b.Init(facing, frequency);
            return b;
        }

        void Init(int facing, float frequency)
        {
            Facing = facing;
            Frequency = frequency;
            Wavelength = Harmony.Wavelength(frequency, SpeedOfSound);

            // The wave stops at walls: raycast along the path a little above foot height.
            Length = MaxLength;
            var origin = transform.position + Vector3.up * 0.35f - Vector3.right * Facing * 0.3f;
            int mask = Layers.Mask(Layers.Ground, Layers.Crystal, Layers.Default);
            if (Physics.Raycast(origin, Vector3.right * Facing, out var hit, MaxLength + 0.3f, mask, QueryTriggerInteraction.Ignore))
                Length = Mathf.Clamp(hit.distance - 0.3f, 0.5f, MaxLength);

            _col = gameObject.AddComponent<BoxCollider>();
            _col.size = new Vector3(0.01f, Thickness, Depth);
            _col.center = new Vector3(0f, -Thickness * 0.5f, 0f);
            _col.enabled = false;

            _mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mat = MaterialLibrary.Create("EchoBridge");
            SetMat("_Color", _glow0);
            SetMat("_BaseColor", new Color(0.02f, 0.02f, 0.04f, 1f));
            // Shader ripple is spatial (cycles per world unit) = 1/λ, so the glow ripples at the note's true wavelength.
            if (_mat.HasProperty("_Frequency")) _mat.SetFloat("_Frequency", 1f / Mathf.Max(0.05f, Wavelength));
            if (_mat.HasProperty("_Dir")) _mat.SetFloat("_Dir", Facing);
            SetFade(0f);
            mr.sharedMaterial = _mat;
            _mesh = new Mesh { name = "EchoBridgeMesh" };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;

            _glow = MakeLine("RimGlow", 0.55f, 48);
            if (_glow.sharedMaterial.HasProperty("_BaseMap")) _glow.sharedMaterial.SetTexture("_BaseMap", SoftAcross);
            if (_glow.sharedMaterial.HasProperty("_BaseColor")) _glow.sharedMaterial.SetColor("_BaseColor", _glow.sharedMaterial.GetColor("_BaseColor") * 0.45f);
            _glow.textureMode = LineTextureMode.Stretch;
            _rim = MakeLine("Rim", 0.06f, 96);
            _front = MakeLine("Front", 0.11f, 24);
        }

        void SetMat(string prop, Color c) { if (_mat != null && _mat.HasProperty(prop)) _mat.SetColor(prop, c); }

        void SetFade(float f)
        {
            if (_mat == null) return;
            if (_mat.HasProperty("_Fade")) _mat.SetFloat("_Fade", f);
        }

        LineRenderer MakeLine(string name, float width, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = LineMaterial(_glow0);
            lr.startColor = lr.endColor = Color.white;
            return lr;
        }

        /// <summary>Additive line material (HDR tint in _BaseColor; line vertex alpha fades it).</summary>
        public static Material LineMaterial(Color hdr)
        {
            var m = MaterialLibrary.Create("ParticleAdditive");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            return m;
        }

        /// <summary>Soft falloff across a line's width (u along, v across) for the rim glow.</summary>
        static Texture2D SoftAcross
        {
            get
            {
                if (_softAcross != null) return _softAcross;
                _softAcross = new Texture2D(4, 32, TextureFormat.RGBA32, false) { name = "SoftAcross", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[4 * 32];
                for (int y = 0; y < 32; y++)
                {
                    float v = Mathf.Sin((y + 0.5f) / 32f * Mathf.PI);
                    byte a = (byte)(v * v * v * 255f);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, a);
                }
                _softAcross.SetPixels32(px);
                _softAcross.Apply();
                return _softAcross;
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            float prevBuilt = Built;
            Built = Mathf.Min(Length, _age * FrontSpeed);

            if (!Mathf.Approximately(prevBuilt, Built) || _age < 0.05f)
            {
                _col.enabled = Built > 0.05f;
                _col.size = new Vector3(Built, Thickness, Depth);
                _col.center = new Vector3(Facing * Built * 0.5f, -Thickness * 0.5f, 0f);
                RebuildMesh();
            }

            float t = Time.time;
            // Standing-wave ripple on the rim: y = A·sin(kx)·cos(ωt), decaying as the echo dies.
            float life01 = Mathf.Clamp01(_age / Lifetime);
            float k = 2f * Mathf.PI / Mathf.Max(0.05f, Wavelength);
            float ripple = 0.07f * (1f - life01 * 0.7f) * Mathf.Cos(t * 9f);
            // A wave packet runs along the rim from the singer to the tip, over and over (visual only).
            float packetX = Mathf.Repeat(_age * 9f, Mathf.Max(0.5f, Length) + 1.5f) - 0.75f;
            float packetAmp = 0.09f * (1f - life01 * 0.6f);
            int n = _rim.positionCount;
            for (int i = 0; i < n; i++)
            {
                float x = Built * i / (n - 1);
                float dx = x - packetX;
                float packet = packetAmp * Mathf.Exp(-dx * dx * 2.5f) * Mathf.Sin(x * 14f - _age * 30f);
                _rim.SetPosition(i, new Vector3(Facing * x, 0.02f + ripple * Mathf.Sin(k * x) + packet, -Depth * 0.5f - 0.02f));
            }
            int g = _glow.positionCount;
            for (int i = 0; i < g; i++)
            {
                float x = Built * i / (g - 1);
                _glow.SetPosition(i, new Vector3(Facing * x, 0.0f, -Depth * 0.5f - 0.01f));
            }

            // Glow motes drift up from the rim while the bridge holds; a few sparks fly off the forming front.
            if (_age < Lifetime - FadeTime && Built > 0.2f)
            {
                _moteAcc += Time.deltaTime * (6f + 3f * Built);
                while (_moteAcc >= 1f)
                {
                    _moteAcc -= 1f;
                    float x = Built * Hash(_age * 37.1f + _moteAcc);
                    var p = transform.position + new Vector3(Facing * x, 0.05f, 0f);
                    ImpactFx.Mote(p, new Vector3(0f, 0.5f + Hash(x * 3.3f) * 0.8f, 0f), _glow0 * (0.5f + Hash(x * 7.7f)), 0.07f + 0.08f * Hash(x * 1.9f), 0.9f + 0.6f * Hash(x));
                }
            }
            if (Built < Length - 0.001f)
            {
                var tip = transform.position + new Vector3(Facing * Built, 0.1f, 0f);
                ImpactFx.Mote(tip, new Vector3(Facing * 2.5f, 1.2f * (Hash(_age * 91f) - 0.5f), 0f), _glow0 * 1.4f, 0.12f, 0.35f);
            }
            else if (!_formed)
            {
                _formed = true;
                ImpactFx.Ring(transform.position + new Vector3(Facing * Built, 0f, 0f), _glow0, 0.8f, 0.3f);
            }

            // Travelling wavefront: a bright vertical arc at the leading edge while the bridge forms.
            bool growing = Built < Length - 0.001f;
            _front.enabled = growing;
            if (growing)
            {
                int m = _front.positionCount;
                for (int i = 0; i < m; i++)
                {
                    float v = i / (float)(m - 1);
                    float y = Mathf.Lerp(-0.6f, 1.6f, v);
                    float bulge = 0.25f * Mathf.Sin(v * Mathf.PI);
                    _front.SetPosition(i, new Vector3(Facing * (Built + bulge), y, -Depth * 0.5f - 0.05f));
                }
            }

            // Fade out at the end of its life (flicker so the player sees it's about to vanish).
            float fadeStart = Lifetime - FadeTime;
            if (_age > fadeStart)
            {
                float f = Mathf.Clamp01((_age - fadeStart) / FadeTime);
                float flicker = (Mathf.Sin(_age * 40f) > 0f) ? 0.25f : 0f;
                SetFade(Mathf.Clamp01(f + flicker * (1f - f)));
                _rim.startColor = _rim.endColor = new Color(1f, 1f, 1f, 1f - f);
                _glow.startColor = _glow.endColor = new Color(1f, 1f, 1f, 1f - f);
            }
            if (_age >= Lifetime) Destroy(gameObject);
        }

        /// <summary>Builds the bridge mesh: flat walkable top, sine-shaped underside (wavelength λ).</summary>
        void RebuildMesh()
        {
            int seg = Mathf.Max(2, Mathf.CeilToInt(Built / 0.08f));
            var verts = new Vector3[(seg + 1) * 4];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 12];
            float k = 2f * Mathf.PI / Mathf.Max(0.05f, Wavelength);
            float zf = -Depth * 0.5f, zb = Depth * 0.5f;
            for (int i = 0; i <= seg; i++)
            {
                float x = Built * i / seg;
                float taper = Mathf.Clamp01(x / 0.6f) * Mathf.Clamp01((Built - x) / 0.6f + 0.3f);
                float bottom = -Thickness - _amp * taper * (0.5f + 0.5f * Mathf.Sin(k * x));
                float u = x / MaxLength;
                // front face: top/bottom; top face: front/back
                verts[i * 4 + 0] = new Vector3(Facing * x, 0f, zf);
                verts[i * 4 + 1] = new Vector3(Facing * x, bottom, zf);
                verts[i * 4 + 2] = new Vector3(Facing * x, 0f, zf);
                verts[i * 4 + 3] = new Vector3(Facing * x, 0f, zb);
                uvs[i * 4 + 0] = new Vector2(u, 1f);
                uvs[i * 4 + 1] = new Vector2(u, 0f);
                uvs[i * 4 + 2] = new Vector2(u, 1f);
                uvs[i * 4 + 3] = new Vector2(u, 0.8f);
            }
            int ti = 0;
            for (int i = 0; i < seg; i++)
            {
                int a = i * 4, b = (i + 1) * 4;
                // Winding depends on facing so faces point at the camera (−Z) / up.
                if (Facing > 0)
                {
                    tris[ti++] = a + 0; tris[ti++] = b + 0; tris[ti++] = a + 1;
                    tris[ti++] = a + 1; tris[ti++] = b + 0; tris[ti++] = b + 1;
                    tris[ti++] = a + 2; tris[ti++] = a + 3; tris[ti++] = b + 2;
                    tris[ti++] = b + 2; tris[ti++] = a + 3; tris[ti++] = b + 3;
                }
                else
                {
                    tris[ti++] = a + 0; tris[ti++] = a + 1; tris[ti++] = b + 0;
                    tris[ti++] = a + 1; tris[ti++] = b + 1; tris[ti++] = b + 0;
                    tris[ti++] = a + 2; tris[ti++] = b + 2; tris[ti++] = a + 3;
                    tris[ti++] = b + 2; tris[ti++] = b + 3; tris[ti++] = a + 3;
                }
            }
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        static float Hash(float x) { x = Mathf.Sin(x * 12.9898f) * 43758.5453f; return x - Mathf.Floor(x); }

        /// <summary>Speeds up the fade (when a newer bridge replaces this one).</summary>
        public void Dissolve()
        {
            _age = Mathf.Max(_age, Lifetime - FadeTime * 0.5f);
        }

        public void ResetState()
        {
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_mat != null) Destroy(_mat);
            if (_rim != null) Destroy(_rim.sharedMaterial);
            if (_front != null) Destroy(_front.sharedMaterial);
            if (_glow != null) Destroy(_glow.sharedMaterial);
        }
    }
}
