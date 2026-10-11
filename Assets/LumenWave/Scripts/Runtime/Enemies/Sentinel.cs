using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Sentinel: a floating lamp that sweeps a burning red beam back and forth below it.
    /// A Pulse wavefront stuns it (beam off, lamp droops and sparks) for a few seconds.
    /// Level: <c>sentinel x y range=10 sweep=60 speed=1 [patrol=0] [intensity=3] [color=red] [phase=0]</c>
    /// </summary>
    public class Sentinel : MonoBehaviour, IPulseReceiver, IResettable
    {
        public float sweep = 60f;          // ± degrees around straight down
        public float speed = 1f;           // sweep cycles per ~6 s (radians/s of the sine)
        public float patrol = 0f;          // horizontal drift amplitude
        public float stunDuration = 3.5f;
        public float phase;

        public LightBeam Beam { get; private set; }
        public bool IsStunned => _stun > 0f;
        public float BeamAngle { get; private set; } = -90f;

        Vector3 _home;
        float _t, _stun, _initialPhase;
        Transform _body, _robe;
        Material _lensMat, _cageMat;
        Renderer _halo;
        Light _light;
        ParticleSystem _sparks;
        Color _color;
        float _hitFlash;   // visual only: white flash when a pulse stuns it

        [LevelEntity("sentinel")]
        static GameObject Build(EntitySpec s, LevelContext ctx)
        {
            var go = Create(s.Position3, s.Color("color", LightColor.Red), s.Float("range", 10f), s.Float("intensity", 3f));
            var sen = go.GetComponent<Sentinel>();
            sen.sweep = s.Float("sweep", 60f);
            sen.speed = s.Float("speed", 1f);
            sen.patrol = s.Float("patrol", 0f);
            sen.phase = s.Float("phase", 0f);
            sen._initialPhase = sen.phase;
            if (ctx != null) go.transform.SetParent(ctx.entities, true);
            return go;
        }

        /// <summary>Builds a sentinel procedurally (also used by tests).</summary>
        public static GameObject Create(Vector3 position, LightColor color, float range, float intensity)
        {
            var go = new GameObject("Sentinel");
            go.layer = Layers.Enemy;
            go.transform.position = position;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.7f;
            var s = go.AddComponent<Sentinel>();
            s.BuildVisuals(color, range, intensity);
            return go;
        }

        void BuildVisuals(LightColor color, float range, float intensity)
        {
            _home = transform.position;
            _color = LightMath.DisplayColor(color);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // Hooded lantern-warden: a black robed figure (procedural lathe meshes, tinted Schlick rim) that
            // holds its red lantern out below it. The lantern lens stays where the beam starts.
            var dark = EnemyUtil.Silhouette(new Color(1.0f, 0.42f, 0.36f) * 1.3f);
            _cageMat = EnemyUtil.Emissive(new Color(0.08f, 0.07f, 0.07f), _color * 0.6f);
            _lensMat = EnemyUtil.Emissive(_color, _color * 6f);

            var hood = EnemyUtil.CachedMesh("SentinelHood", () => EnemyUtil.Lathe(new[]
            {
                new Vector2(0.0f, 1.16f), new Vector2(0.07f, 1.12f), new Vector2(0.15f, 1.03f), new Vector2(0.22f, 0.9f),
                new Vector2(0.25f, 0.76f), new Vector2(0.24f, 0.66f), new Vector2(0.3f, 0.6f),
            }, 18, 0.25f, 3, "SentinelHood"));
            var robe = EnemyUtil.CachedMesh("SentinelRobe", () => EnemyUtil.Lathe(new[]
            {
                new Vector2(0.17f, 0f), new Vector2(0.3f, -0.1f), new Vector2(0.34f, -0.28f), new Vector2(0.37f, -0.48f),
                new Vector2(0.42f, -0.64f), new Vector2(0.47f, -0.72f), new Vector2(0.4f, -0.74f),
            }, 22, 0.9f, 7, "SentinelRobe"));

            // Robe hangs from the shoulders on its own pivot so the hem can sway.
            _robe = new GameObject("Robe").transform;
            _robe.SetParent(_body, false);
            _robe.localPosition = new Vector3(0f, 0.66f, 0f);
            EnemyUtil.MeshPart(robe, _robe, Vector3.zero, Quaternion.identity, Vector3.one, dark, "RobeMesh");
            EnemyUtil.MeshPart(hood, _body, Vector3.zero, Quaternion.Euler(0f, 0f, 6f), Vector3.one, dark, "Hood");
            // Eyes in the hood's shadow: two red embers.
            EnemyUtil.Halo(_body, new Vector3(-0.07f, 0.8f, -0.27f), 0.16f, _color * 2.2f);
            EnemyUtil.Halo(_body, new Vector3(0.07f, 0.8f, -0.27f), 0.16f, _color * 2.2f);
            // Sleeves reaching down to the lantern ring.
            EnemyUtil.Part(PrimitiveType.Capsule, _body, new Vector3(-0.17f, 0.22f, -0.16f), new Vector3(0.11f, 0.27f, 0.11f), dark, "SleeveL").transform.localRotation = Quaternion.Euler(0, 0, -24);
            EnemyUtil.Part(PrimitiveType.Capsule, _body, new Vector3(0.17f, 0.22f, -0.16f), new Vector3(0.11f, 0.27f, 0.11f), dark, "SleeveR").transform.localRotation = Quaternion.Euler(0, 0, 24);

            // Lantern: ring + chain, dark cap, glowing cage with bars, glowing lens at the bottom.
            EnemyUtil.Part(PrimitiveType.Cylinder, _body, new Vector3(0, -0.04f, -0.08f), new Vector3(0.025f, 0.09f, 0.025f), dark, "Chain");
            EnemyUtil.Part(PrimitiveType.Cylinder, _body, new Vector3(0, -0.14f, 0), new Vector3(0.36f, 0.04f, 0.36f), dark, "Cap");
            EnemyUtil.Part(PrimitiveType.Cylinder, _body, new Vector3(0, -0.24f, 0), new Vector3(0.28f, 0.08f, 0.28f), _cageMat, "Cage");
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                EnemyUtil.Part(PrimitiveType.Cube, _body, new Vector3(Mathf.Cos(a) * 0.15f, -0.24f, Mathf.Sin(a) * 0.15f), new Vector3(0.03f, 0.2f, 0.03f), dark, "Bar");
            }
            var lens = EnemyUtil.Part(PrimitiveType.Sphere, _body, new Vector3(0, -0.38f, 0), new Vector3(0.24f, 0.24f, 0.24f), _lensMat, "Lens");

            _halo = EnemyUtil.Halo(_body, new Vector3(0, -0.38f, -0.3f), 1.6f, _color * 0.55f);
            _light = EnemyUtil.PointLight(_body, new Vector3(0, -0.5f, -0.4f), _color, 4f, 2.5f);

            var beamGo = new GameObject("Beam");
            beamGo.layer = Layers.Enemy;
            beamGo.transform.SetParent(lens.transform.parent, false);
            beamGo.transform.localPosition = new Vector3(0, -0.45f, 0);
            Beam = beamGo.AddComponent<LightBeam>();
            Beam.color = color;
            Beam.range = range;
            Beam.intensity = intensity;
            Beam.width = 0.5f;
            Beam.direction = Vector2.down;
            Beam.on = true;

            _sparks = EnemyUtil.Sparks(transform, _color, 5f, 0.12f, 1.5f);
            _sparks.transform.localPosition = new Vector3(0, -0.4f, 0);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_stun > 0f)
            {
                _stun -= dt;
                // Droop and sway, lens flickers like a dying bulb.
                float droop = Mathf.Min(1f, (stunDuration - _stun) * 3f);
                _body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 7f) * 6f + 25f * droop);
                _body.localPosition = new Vector3(0, -0.35f * droop, 0);
                bool flick = Random.value < 0.15f;
                EnemyUtil.SetEmission(_lensMat, flick ? _color * 2f : _color * 0.05f);
                if (_halo) _halo.enabled = flick;
                if (_light) _light.intensity = flick ? 1.2f : 0f;
                if (_hitFlash > 0f)
                {
                    // Hit flash: the whole lantern blazes white for a moment, then gutters out.
                    _hitFlash = Mathf.Max(0f, _hitFlash - dt * 4f);
                    float f = _hitFlash * _hitFlash;
                    EnemyUtil.SetEmission(_lensMat, Color.Lerp(_color * 0.05f, Color.white * 12f, f));
                    EnemyUtil.SetEmission(_cageMat, Color.Lerp(_color * 0.05f, Color.white * 5f, f));
                    if (_halo) _halo.enabled = true;
                    if (_light) _light.intensity = 1.2f + 4f * f;
                }
                if (_stun <= 0f) Recover();
                return;
            }

            _t += dt;
            // Smooth sine sweep around "straight down".
            float a = -90f + Mathf.Sin(_t * speed + phase) * sweep;
            BeamAngle = a;
            float rad = a * Mathf.Deg2Rad;
            if (Beam != null) Beam.direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            _body.localRotation = Quaternion.Euler(0, 0, a + 90f);

            // Gentle bob + optional patrol.
            var p = _home;
            p.y += Mathf.Sin(_t * 1.7f) * 0.15f;
            if (patrol > 0f) p.x += Mathf.Sin(_t * 0.5f) * patrol;
            transform.position = p;

            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 6f);
            EnemyUtil.SetEmission(_lensMat, _color * 6f * pulse);
            // Idle cloth sway: the robe hem lags the body a little.
            if (_robe) _robe.localRotation = Quaternion.Euler(Mathf.Sin(_t * 1.1f + phase) * 3f, 0f, Mathf.Sin(_t * 1.3f + 0.7f) * 5f);
            if (_halo) _halo.transform.localScale = Vector3.one * 1.6f * (0.92f + 0.08f * pulse);
        }

        void Recover()
        {
            _stun = 0f;
            if (Beam != null) Beam.on = true;
            _body.localPosition = Vector3.zero;
            if (_halo) _halo.enabled = true;
            if (_light) _light.intensity = 2.5f;
            EnemyUtil.SetEmission(_cageMat, _color * 0.6f);
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            Stun(stunDuration * Mathf.Clamp(strength, 0.5f, 1.5f));
        }

        public void Stun(float seconds)
        {
            bool wasStunned = IsStunned;
            _stun = Mathf.Max(_stun, seconds);
            if (Beam != null) Beam.on = false;
            EnemyUtil.SetEmission(_cageMat, _color * 0.05f);
            if (_sparks) _sparks.Emit(24);
            if (!wasStunned)
            {
                _hitFlash = 1f;
                ImpactFx.Sparks(transform.position + Vector3.down * 0.3f, _color * 2f, 22, 7f, 0.13f, 0.7f);
            }
            if (!wasStunned) GameEvents.RaiseEnemyStunned(transform.position);
        }

        public void ResetState()
        {
            _t = 0f;
            phase = _initialPhase;
            transform.position = _home;
            Recover();
        }
    }
}
