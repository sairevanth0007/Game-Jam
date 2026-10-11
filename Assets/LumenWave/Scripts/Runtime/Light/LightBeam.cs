using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>One straight piece of a traced beam (between source/bounce and hit/end).</summary>
    public struct BeamSegment
    {
        public Vector3 start, end;
        public LightColor color;
        /// <summary>Optical path length from the emitter to <see cref="start"/> (for inverse-square + fog).</summary>
        public float pathStart;
        public bool endsOnSurface;
        public Vector3 hitNormal;
        public int depth;
        public Collider hitCollider;

        public float Length => Vector3.Distance(start, end);
        public Vector2 Direction => ((Vector2)(end - start)).normalized;
    }

    /// <summary>
    /// A raycast light beam on the X/Y plane. Occluded by solids and the shield, reflected by mirrors
    /// (law of reflection), dispersed by prisms (Newton). Visualised as soft additive quads with
    /// Beer–Lambert fog, real URP lights at the source and at hit points, and hit sparks.
    /// Public fields are the stable API used by level entities and enemies (e.g. the Sentinel sweeps one).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public partial class LightBeam : MonoBehaviour
    {
        public LightColor color = LightColor.White;
        /// <summary>Direction in the X/Y plane (normalised by the beam).</summary>
        public Vector2 direction = Vector2.down;
        public float range = 16f;
        public float intensity = 1f;
        public float width = 0.6f;
        /// <summary>Yellow-style: only lit during the BeatClock hot window.</summary>
        public bool beatGated;
        public bool on = true;

        /// <summary>Optional sweep: angle oscillates ±sweepDegrees around the base direction.</summary>
        public float sweepDegrees;
        public float sweepSpeed = 0.5f;
        /// <summary>Draws an emitter housing at the origin (level beams yes; enemies bring their own body).</summary>
        public bool showFixture;
        /// <summary>Real URP lights (source + hit points). Disable for cheap secondary beams.</summary>
        public bool castSceneLight = true;

        public const int MaxSegments = 24;
        public const int MaxDepth = 8;

        public readonly List<BeamSegment> Segments = new List<BeamSegment>();

        /// <summary>True when the beam currently delivers light (on, and hot if beat-gated).</summary>
        public bool IsLit { get; private set; }
        /// <summary>0..1 visual brightness (includes beat telegraph).</summary>
        public float VisualLevel { get; private set; }

        public bool IsBeatGated => beatGated || color == LightColor.Yellow;

        Vector2 _baseDirection;
        bool _baseSet;
        float _sweepT;

        // Visual pools
        readonly List<MeshRenderer> _cores = new List<MeshRenderer>();
        readonly List<MeshRenderer> _halos = new List<MeshRenderer>();
        readonly List<Light> _hitLights = new List<Light>();
        Light _sourceLight;
        MeshRenderer _sourceGlow;
        ParticleSystem _sparks;
        MaterialPropertyBlock _mpb;
        Transform _visualRoot;
        static Mesh _quadMesh, _haloMesh;

        void OnEnable() { if (!LightField.Beams.Contains(this)) LightField.Beams.Add(this); }
        void OnDisable() { LightField.Beams.Remove(this); HideVisuals(); }
        void OnDestroy() { if (_visualRoot != null) Destroy(_visualRoot.gameObject); }

        void Start()
        {
            if (!_baseSet) { _baseDirection = direction; _baseSet = true; }
            if (showFixture) BuildFixture();
        }

        /// <summary>Sets the resting direction used for sweeping.</summary>
        public void SetBaseDirection(Vector2 d) { _baseDirection = d.normalized; _baseSet = true; direction = _baseDirection; }

        void Update()
        {
            if (sweepDegrees > 0.01f)
            {
                if (!_baseSet) { _baseDirection = direction; _baseSet = true; }
                _sweepT += Time.deltaTime;
                direction = LightField.Rotate(_baseDirection, Mathf.Sin(_sweepT * sweepSpeed * Mathf.PI * 2f) * sweepDegrees);
            }
            UpdateLitState();
            Trace();
            UpdateVisuals();
        }

        void UpdateLitState()
        {
            if (!on) { IsLit = false; VisualLevel = 0f; return; }
            if (!IsBeatGated) { IsLit = true; VisualLevel = 1f; return; }
            bool hot = LightField.BeatHot;
            IsLit = hot;
            if (hot) VisualLevel = 1f;
            else
            {
                // Telegraph: a faint guide line, swelling in the last 30% of the beat before it fires.
                float ph = BeatClock.CurrentPhase;
                VisualLevel = ph > 0.7f ? Mathf.Lerp(0.08f, 0.4f, (ph - 0.7f) / 0.3f) : 0.06f;
            }
        }

        struct Ray2 { public Vector2 origin, dir; public LightColor color; public float path, remaining; public int depth; }
        static readonly Queue<Ray2> _queue = new Queue<Ray2>();
        static readonly LightColor[] Channels = { LightColor.Red, LightColor.Green, LightColor.Blue };

        /// <summary>Traces the beam through the scene. Public so tests can call it directly.</summary>
        public void Trace()
        {
            Segments.Clear();
            if (!on || color == LightColor.None) return;
            LightField.SyncPhysics();
            var d = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.down;
            var queue = _queue;
            queue.Clear();
            queue.Enqueue(new Ray2 { origin = transform.position, dir = d, color = color, path = 0f, remaining = range, depth = 0 });
            int mask = Layers.LightBlockers;
            while (queue.Count > 0 && Segments.Count < MaxSegments)
            {
                var r = queue.Dequeue();
                if (r.remaining <= 0.01f) continue;
                var o3 = new Vector3(r.origin.x, r.origin.y, 0f);
                var d3 = new Vector3(r.dir.x, r.dir.y, 0f);
                var seg = new BeamSegment { start = o3, color = r.color, pathStart = r.path, depth = r.depth };
                if (Physics.Raycast(o3, d3, out var hit, r.remaining, mask, QueryTriggerInteraction.Ignore))
                {
                    var hp = hit.point; hp.z = 0f;
                    seg.end = hp;
                    seg.endsOnSurface = true;
                    seg.hitCollider = hit.collider;
                    var n = hit.normal; n.z = 0f;
                    if (n.sqrMagnitude < 1e-6f) n = -d3;
                    seg.hitNormal = n.normalized;
                    Segments.Add(seg);
                    float travelled = hit.distance;
                    int layer = hit.collider.gameObject.layer;
                    if (r.depth >= MaxDepth) continue;
                    if (layer == Layers.Mirror)
                    {
                        var nd = LightMath.Reflect(r.dir, (Vector2)seg.hitNormal).normalized;
                        var mirror = hit.collider.GetComponentInParent<Mirror>();
                        if (mirror != null) mirror.NotifyHit(r.color);
                        queue.Enqueue(new Ray2 { origin = (Vector2)hp + nd * 0.02f, dir = nd, color = r.color, path = r.path + travelled, remaining = r.remaining - travelled, depth = r.depth + 1 });
                    }
                    else if (layer == Layers.Prism)
                    {
                        var prism = hit.collider.GetComponentInParent<Prism>();
                        if (prism == null) continue;
                        prism.NotifyHit(r.color);
                        foreach (var ch in Channels)
                        {
                            if ((r.color & ch) == 0) continue;
                            var ed = prism.ExitDirection(ch);
                            var eo = (Vector2)prism.transform.position + ed * (prism.radius + 0.06f);
                            queue.Enqueue(new Ray2 { origin = eo, dir = ed, color = ch, path = r.path + travelled + prism.radius, remaining = r.remaining - travelled, depth = r.depth + 1 });
                        }
                    }
                }
                else
                {
                    seg.end = o3 + d3 * r.remaining;
                    Segments.Add(seg);
                }
            }
        }

        /// <summary>Adds this beam's contribution at point p to the exposure.</summary>
        public void AddExposure(Vector3 p, float bodyRadius, bool hot, ref LightExposure e)
        {
            if (!on || Segments.Count == 0) return;
            bool gated = IsBeatGated;
            if (gated && !hot) return;
            float sigma = LightField.FogSigma;
            float reach = width * 0.5f + bodyRadius;
            // Per channel take the strongest segment (a body in a reflected beam isn't double counted).
            float r = 0, g = 0, b = 0, y = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                var s = Segments[i];
                float dist = LightField.DistanceToSegment(p, s.start, s.end, out float t);
                if (dist > reach) continue;
                float path = s.pathStart + t * s.Length;
                float I = LightField.BeamIntensityAt(intensity, path, sigma);
                if (s.color == LightColor.Yellow) { y = Mathf.Max(y, I); continue; }
                if ((s.color & LightColor.Red) != 0) r = Mathf.Max(r, I);
                if ((s.color & LightColor.Green) != 0) g = Mathf.Max(g, I);
                if ((s.color & LightColor.Blue) != 0) b = Mathf.Max(b, I);
            }
            e.r += r; e.g += g; e.b += b; e.yellowPulse += y;
        }

        // ---------------------------------------------------------------- danger (readability)

        /// <summary>What a red beam does to Umbra where she can actually stand in it.</summary>
        public enum BeamDanger { Safe, Burn, Lethal }

        /// <summary>Burn = damage over time; Lethal = reaches <see cref="LightRules.LethalRed"/> somewhere Umbra can be.
        /// Uses the same inverse-square + Beer–Lambert intensity as gameplay, sampled only where there is
        /// ground within jumping reach below (the part of a ceiling beam near its emitter is out of reach).</summary>
        public BeamDanger Danger { get { RefreshDanger(); return _danger; } }

        /// <summary>Strongest reachable red intensity (gameplay units, lethal at 4).</summary>
        public float PeakReachableRed { get { RefreshDanger(); return _peakRed; } }

        public const float ReachHeight = 4.5f;
        BeamDanger _danger;
        float _peakRed, _dangerAt = -1f;

        void RefreshDanger()
        {
            if (_dangerAt >= 0f && Time.time - _dangerAt < 0.25f) return;
            _dangerAt = Time.time;
            _peakRed = PeakRed(this);
            _danger = (color & LightColor.Red) == 0 || color == LightColor.White || IsBeatGated ? BeamDanger.Safe
                    : _peakRed >= LightRules.LethalRed ? BeamDanger.Lethal
                    : _peakRed > LightRules.Threshold ? BeamDanger.Burn : BeamDanger.Safe;
        }

        static float PeakRed(LightBeam b)
        {
            if (b.Segments.Count == 0) b.Trace();
            float sigma = LightField.FogSigma, peak = 0f;
            int mask = Layers.Mask(Layers.Ground, Layers.Bridge, Layers.Crystal);
            foreach (var s in b.Segments)
            {
                if ((s.color & LightColor.Red) == 0 || s.color == LightColor.Yellow) continue;
                float len = s.Length;
                for (float d = 0f; d <= len; d += 0.5f)
                {
                    var p = Vector3.Lerp(s.start, s.end, len > 0f ? d / len : 0f);
                    if (!Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, ReachHeight, mask, QueryTriggerInteraction.Ignore)) continue;
                    peak = Mathf.Max(peak, LightField.BeamIntensityAt(b.intensity, s.pathStart + d, sigma));
                }
            }
            return peak;
        }

        // ---------------------------------------------------------------- visuals

        void EnsureVisualRoot()
        {
            if (_visualRoot != null) return;
            _mpb = new MaterialPropertyBlock();
            _visualRoot = new GameObject("BeamVisuals").transform;
            // World-space visuals: not parented to a moving/rotating/scaled emitter body
            // (destroyed together with the beam in OnDestroy).
            if (castSceneLight)
            {
                _sourceLight = LightVisuals.AddLight(_visualRoot, LightType.Spot, LightMath.DisplayColor(color), 6f, 12f, "SourceLight");
                _sourceLight.spotAngle = 40f;
                _sourceLight.innerSpotAngle = 12f;
            }
            _sourceGlow = LightVisuals.Glow(_visualRoot, Vector3.zero, 1.4f, LightMath.DisplayColor(color), "SourceGlow");
            _sparks = LightVisuals.Sparks(_visualRoot, LightMath.DisplayColor(color));
        }

        static Mesh QuadMesh
        {
            get
            {
                if (_quadMesh == null) _quadMesh = BuildStrip(0.5f, 0.5f, "BeamCore");
                return _quadMesh;
            }
        }

        static Mesh HaloMesh
        {
            get
            {
                // Tapered: narrower at the source, wider at the far end, reads as a light cone.
                if (_haloMesh == null) _haloMesh = BuildStrip(0.32f, 0.5f, "BeamHalo");
                return _haloMesh;
            }
        }

        /// <summary>Strip along +Y from 0..1, x half-width h0 at y=0 and h1 at y=1. uv.x across, uv.y along.</summary>
        static Mesh BuildStrip(float h0, float h1, string name)
        {
            const int rows = 8;
            var v = new Vector3[(rows + 1) * 2];
            var uv = new Vector2[v.Length];
            var col = new Color[v.Length];
            var tri = new int[rows * 6];
            for (int i = 0; i <= rows; i++)
            {
                float y = i / (float)rows, h = Mathf.Lerp(h0, h1, y);
                v[i * 2] = new Vector3(-h, y, 0); v[i * 2 + 1] = new Vector3(h, y, 0);
                uv[i * 2] = new Vector2(0, y); uv[i * 2 + 1] = new Vector2(1, y);
                col[i * 2] = col[i * 2 + 1] = Color.white;
                if (i < rows)
                {
                    int k = i * 6, a = i * 2;
                    tri[k] = a; tri[k + 1] = a + 2; tri[k + 2] = a + 1;
                    tri[k + 3] = a + 1; tri[k + 4] = a + 2; tri[k + 5] = a + 3;
                }
            }
            var m = new Mesh { name = name, vertices = v, uv = uv, colors = col, triangles = tri };
            m.RecalculateNormals();
            m.bounds = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2f, 1.2f, 1f));
            return m;
        }

        MeshRenderer NewStrip(Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_visualRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = LightVisuals.BeamMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void UpdateVisuals()
        {
            EnsureVisualRoot();
            float level = VisualLevel;
            float sigma = LightField.FogSigma;
            int n = Segments.Count;
            while (_cores.Count < n) { _cores.Add(NewStrip(QuadMesh, "Core" + _cores.Count)); _halos.Add(NewStrip(HaloMesh, "Halo" + _halos.Count)); }
            int hitLightIdx = 0;
            Vector3 lastHit = Vector3.zero; bool anyHit = false; Vector3 lastHitNormal = Vector3.up; LightColor lastHitColor = color;
            float lastHitPath = 0f;
            float yBoost = YellowBoost(out float yFlicker);
            for (int i = 0; i < _cores.Count; i++)
            {
                bool show = i < n && level > 0.001f;
                _cores[i].gameObject.SetActive(show);
                _halos[i].gameObject.SetActive(show);
                if (!show) continue;
                var s = Segments[i];
                float len = Mathf.Max(0.01f, s.Length);
                var dir = s.Direction;
                var rot = Quaternion.FromToRotation(Vector3.up, new Vector3(dir.x, dir.y, 0f));
                // Fog already absorbed before this segment started (Beer–Lambert along the whole path).
                float fogBefore = Mathf.Exp(-sigma * s.pathStart);
                var c = LightMath.DisplayColor(s.color);
                var look = LookFor(s.color);
                float boost = s.color == LightColor.Yellow ? yBoost : 1f;

                var ct = _cores[i].transform;
                ct.position = s.start + new Vector3(0, 0, -0.05f);
                ct.rotation = rot;
                ct.localScale = new Vector3(width * look.coreW, len, 1f);
                LightVisuals.SetBeamProps(_cores[i], _mpb, c, look.coreI * level * fogBefore * boost, len, sigma);
                ApplyLook(_cores[i], look, true, s.color == LightColor.Yellow ? yFlicker : 0f);
                SetPathStart(_cores[i], s.pathStart);

                var ht = _halos[i].transform;
                ht.position = s.start + new Vector3(0, 0, 0.05f);
                ht.rotation = rot;
                ht.localScale = new Vector3(width * look.haloW, len, 1f);
                LightVisuals.SetBeamProps(_halos[i], _mpb, c, look.haloI * level * fogBefore * boost, len, sigma);
                ApplyLook(_halos[i], look, false, s.color == LightColor.Yellow ? yFlicker : 0f);
                SetPathStart(_halos[i], s.pathStart);

                if (s.endsOnSurface)
                {
                    anyHit = true; lastHit = s.end; lastHitNormal = s.hitNormal; lastHitColor = s.color; lastHitPath = s.pathStart + len;
                    if (castSceneLight && hitLightIdx < 2)
                    {
                        if (_hitLights.Count <= hitLightIdx) _hitLights.Add(LightVisuals.AddLight(_visualRoot, LightType.Point, c, 2f, 3.5f, "HitLight" + _hitLights.Count));
                        var hl = _hitLights[hitLightIdx++];
                        hl.gameObject.SetActive(true);
                        hl.color = c;
                        hl.intensity = 2.5f * level * Mathf.Exp(-sigma * (s.pathStart + len));
                        hl.transform.position = s.end + s.hitNormal * 0.35f + new Vector3(0, 0, -1.2f);
                    }
                }
            }
            for (int i = hitLightIdx; i < _hitLights.Count; i++) _hitLights[i].gameObject.SetActive(false);

            var col = LightMath.DisplayColor(color);
            if (_sourceLight != null)
            {
                _sourceLight.enabled = level > 0.01f;
                _sourceLight.color = col;
                _sourceLight.intensity = 7f * level;
                _sourceLight.range = Mathf.Min(range, 14f);
                _sourceLight.transform.position = transform.position + new Vector3(0, 0, -0.6f);
                _sourceLight.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, direction.y, 0.35f).normalized);
            }
            if (_sourceGlow != null)
            {
                _sourceGlow.transform.position = transform.position + new Vector3(0, 0, -0.1f);
                _sourceGlow.transform.rotation = Quaternion.identity;
                _sourceGlow.gameObject.SetActive(level > 0.001f);
                LightVisuals.SetGlowColor(_sourceGlow, _mpb, col * (0.6f + 1.4f * level));
            }
            if (_sparks != null)
            {
                bool spark = anyHit && IsLit;
                var em = _sparks.emission;
                em.enabled = spark;
                if (spark)
                {
                    _sparks.transform.position = lastHit + lastHitNormal * 0.05f;
                    _sparks.transform.rotation = Quaternion.LookRotation(lastHitNormal);
                    var main = _sparks.main;
                    main.startColor = LightMath.DisplayColor(lastHitColor);
                }
            }
            UpdateDangerExtras(anyHit, lastHit, lastHitNormal, lastHitColor, lastHitPath, level);
        }

        void HideVisuals()
        {
            if (_visualRoot == null) return;
            foreach (var r in _cores) if (r) r.gameObject.SetActive(false);
            foreach (var r in _halos) if (r) r.gameObject.SetActive(false);
            foreach (var l in _hitLights) if (l) l.gameObject.SetActive(false);
            if (_floorGlow) _floorGlow.gameObject.SetActive(false);
            if (_embers) { var em = _embers.emission; em.enabled = false; }
            if (_warning) _warning.gameObject.SetActive(false);
            if (_sourceLight) _sourceLight.enabled = false;
        }

        void BuildFixture()
        {
            var dark = MaterialLibrary.Tinted("Lit", new Color(0.05f, 0.05f, 0.07f));
            var fx = new GameObject("Fixture").transform;
            fx.SetParent(transform, false);
            float ang = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            fx.localRotation = Quaternion.Euler(0, 0, ang - 90f);   // local +Y = beam direction
            LightVisuals.Part(PrimitiveType.Cube, fx, new Vector3(0, -0.3f, 0), new Vector3(0.7f, 0.55f, 0.7f), dark, "Housing");
            LightVisuals.Part(PrimitiveType.Cube, fx, new Vector3(0, -0.62f, 0), new Vector3(0.9f, 0.12f, 0.9f), dark, "Cap");
            var c = LightMath.DisplayColor(color);
            var lens = MaterialLibrary.Tinted("LitEmissive", c * 0.4f, c * 5f);
            LightVisuals.Part(PrimitiveType.Cylinder, fx, new Vector3(0, -0.02f, 0), new Vector3(0.5f, 0.04f, 0.5f), lens, "Lens");
        }
    }
}
