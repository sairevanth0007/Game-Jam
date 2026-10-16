using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Echo Moth: harmless while the world is quiet. Whenever Umbra sings a power nearby, every moth that
    /// hears it flies at the sound and feeds on it — contact drains resonance. Silence (or the shield) is safety;
    /// a Pulse knocks them out of the air.
    /// Level: <c>moth x y [hearing=12] [drain=30]</c>
    /// </summary>
    public class EchoMoth : MonoBehaviour, IPulseReceiver, IResettable
    {
        public enum State { Flutter, Hunt, Flee, Stunned }

        public float hearing = 12f;
        public float huntTime = 3.5f;
        public float speed = 6.2f;
        public float drain = 30f;
        public float damage = 6f;
        public float radius = 0.25f;

        public State Current { get; private set; } = State.Flutter;

        Vector3 _home, _vel, _target;
        float _timer, _seed;
        Transform _wingL, _wingR, _bodyT;
        Material _bodyMat, _patternMat;
        Renderer _halo;
        ParticleSystem _dust;
        float _hitFlash;   // visual only
        static readonly Color Calm = new Color(0.85f, 0.8f, 0.65f);
        static readonly Color Hungry = new Color(0.4f, 1f, 0.95f);

        [LevelEntity("moth")]
        static GameObject Build(EntitySpec s, LevelContext ctx)
        {
            var go = Create(s.Position3 + Vector3.up * 0.5f);
            var m = go.GetComponent<EchoMoth>();
            m.hearing = s.Float("hearing", m.hearing);
            m.drain = s.Float("drain", m.drain);
            if (ctx != null) go.transform.SetParent(ctx.entities, true);
            return go;
        }

        public static GameObject Create(Vector3 position)
        {
            var go = new GameObject("EchoMoth");
            go.layer = Layers.Enemy;
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.35f;
            var m = go.AddComponent<EchoMoth>();
            m.BuildVisuals();
            return go;
        }

        void BuildVisuals()
        {
            _home = transform.position;
            _seed = Random.value * 20f;
            _bodyT = new GameObject("Body").transform;
            _bodyT.SetParent(transform, false);
            _bodyT.localScale = Vector3.one * 1.3f;
            // Dark moth: silhouette body (thorax, abdomen, head, antennae) and two pairs of black wings, each
            // with a faint emissive eye-spot pattern that brightens when it hunts. Wings hinge on the body axis.
            var dark = EnemyUtil.Silhouette(new Color(0.55f, 0.85f, 1.1f) * 1.8f);
            _bodyMat = EnemyUtil.Emissive(new Color(0.02f, 0.02f, 0.025f), Calm * 0.25f);
            _patternMat = EnemyUtil.GlowMaterial(Calm * 0.3f);
            EnemyUtil.Part(PrimitiveType.Capsule, _bodyT, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.12f, 0.1f), dark, "Thorax");
            EnemyUtil.Part(PrimitiveType.Capsule, _bodyT, new Vector3(0, -0.13f, 0.01f), new Vector3(0.085f, 0.13f, 0.085f), _bodyMat, "Abdomen");
            EnemyUtil.Part(PrimitiveType.Sphere, _bodyT, new Vector3(0, 0.14f, 0), Vector3.one * 0.085f, dark, "Head");
            EnemyUtil.Part(PrimitiveType.Cube, _bodyT, new Vector3(-0.05f, 0.24f, -0.01f), new Vector3(0.012f, 0.16f, 0.012f), dark, "AntennaL").transform.localRotation = Quaternion.Euler(0, 0, 28);
            EnemyUtil.Part(PrimitiveType.Cube, _bodyT, new Vector3(0.05f, 0.24f, -0.01f), new Vector3(0.012f, 0.16f, 0.012f), dark, "AntennaR").transform.localRotation = Quaternion.Euler(0, 0, -28);

            var fore = EnemyUtil.CachedMesh("MothFore", () => EnemyUtil.Polygon(new[]
            {
                new Vector2(0.0f, 0.04f), new Vector2(0.08f, 0.12f), new Vector2(0.22f, 0.2f), new Vector2(0.38f, 0.24f),
                new Vector2(0.46f, 0.18f), new Vector2(0.42f, 0.06f), new Vector2(0.28f, -0.01f), new Vector2(0.1f, -0.03f),
            }, "MothFore"));
            var hind = EnemyUtil.CachedMesh("MothHind", () => EnemyUtil.Polygon(new[]
            {
                new Vector2(0.0f, -0.02f), new Vector2(0.14f, -0.02f), new Vector2(0.28f, -0.07f), new Vector2(0.33f, -0.17f),
                new Vector2(0.26f, -0.27f), new Vector2(0.13f, -0.26f), new Vector2(0.03f, -0.14f),
            }, "MothHind"));
            _wingL = new GameObject("WingL").transform; _wingL.SetParent(_bodyT, false);
            _wingR = new GameObject("WingR").transform; _wingR.SetParent(_bodyT, false);
            foreach (var (hinge, side) in new[] { (_wingL, -1f), (_wingR, 1f) })
            {
                var sc = new Vector3(side, 1f, 1f);
                EnemyUtil.MeshPart(fore, hinge, new Vector3(side * 0.03f, 0.02f, 0f), Quaternion.identity, sc, dark, "ForeWing");
                EnemyUtil.MeshPart(hind, hinge, new Vector3(side * 0.03f, 0.0f, 0.005f), Quaternion.identity, sc, dark, "HindWing");
                // Emissive patterns: an eye-spot on each forewing and a smaller one on each hindwing.
                EnemyUtil.Part(PrimitiveType.Quad, hinge, new Vector3(side * 0.3f, 0.13f, -0.012f), new Vector3(0.17f, 0.12f, 1f), _patternMat, "SpotFore");
                EnemyUtil.Part(PrimitiveType.Quad, hinge, new Vector3(side * 0.2f, -0.15f, -0.012f), new Vector3(0.11f, 0.1f, 1f), _patternMat, "SpotHind");
                EnemyUtil.Part(PrimitiveType.Quad, hinge, new Vector3(side * 0.12f, 0.06f, -0.011f), new Vector3(0.22f, 0.035f, 1f), _patternMat, "Vein");
            }
            _halo = EnemyUtil.Halo(_bodyT, new Vector3(0, 0, -0.15f), 0.9f, Calm * 0.35f);
            _dust = EnemyUtil.Sparks(transform, Calm, 1.5f, 0.07f, 0.3f);
        }

        void OnEnable() { GameEvents.PowerUsed += OnPowerUsed; }
        void OnDisable() { GameEvents.PowerUsed -= OnPowerUsed; }

        void OnPowerUsed(PowerType p)
        {
            if (p == PowerType.Dash || Current == State.Stunned) return;
            var player = EnemyUtil.FindPlayer();
            if (player == null) return;
            var pc = EnemyUtil.Center(player);
            if (Vector2.Distance(transform.position, pc) > hearing) return;
            Current = State.Hunt;
            _timer = huntTime;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var pos = transform.position;
            var player = EnemyUtil.FindPlayer();
            Vector3 pc = player != null ? EnemyUtil.Center(player) : pos;
            float t = Time.time + _seed;

            Vector3 want;
            switch (Current)
            {
                case State.Hunt:
                    _timer -= dt;
                    // Erratic moth flight toward the singer.
                    var jitter = new Vector3(Mathf.Sin(t * 11f), Mathf.Cos(t * 13f), 0f) * 0.8f;
                    want = ((pc + jitter) - pos).normalized * speed;
                    if (_timer <= 0f) Current = State.Flee;
                    break;
                case State.Flee:
                    want = (_home - pos);
                    want = want.sqrMagnitude > 0.01f ? want.normalized * speed * 0.6f : Vector3.zero;
                    if (Vector3.Distance(pos, _home) < 0.4f) Current = State.Flutter;
                    break;
                case State.Stunned:
                    _timer -= dt;
                    want = new Vector3(0f, -2.5f, 0f); // tumbles down
                    if (_timer <= 0f) Current = State.Flee;
                    break;
                default:
                    _target = _home + new Vector3(Mathf.Sin(t * 1.3f) * 0.9f + Mathf.Sin(t * 3.1f) * 0.2f, Mathf.Sin(t * 2.2f) * 0.5f, 0f);
                    want = (_target - pos) * 2.5f;
                    break;
            }
            want.z = 0f;
            _vel = Vector3.MoveTowards(_vel, want, 20f * dt);
            pos = EnemyUtil.FlyMove(pos, _vel * dt, radius);
            if (EnemyUtil.RepelFromShield(ref pos, ref _vel, radius) && Current == State.Hunt)
            {
                ShieldDome.Ripple(pos);
                Current = State.Flee; // the shield's standing wave confuses it
            }
            transform.position = pos;

            // Feed on the song.
            if (Current == State.Hunt && player != null && Vector2.Distance(pos, pc) < 0.7f && !ShieldState.Contains(pos))
            {
                var res = player.GetComponent<Resonance>();
                if (res != null) res.Drain(drain);
                EnemyUtil.Hit(player, damage, pos, 3f, LightColor.None);
                if (_dust) _dust.Emit(14);
                Current = State.Flee;
            }

            // Wing flap + colour.
            float flap = Current == State.Hunt ? 34f : 16f;
            float w = Mathf.Abs(Mathf.Sin(t * flap));
            // Wings rotate about the body axis (Y): open (facing camera) → nearly edge-on, like a real flap.
            float open = 72f * w * w;   // mostly spread toward the camera, quick snaps closed
            if (_wingL) _wingL.localRotation = Quaternion.Euler(0f, -open, 0f);
            if (_wingR) _wingR.localRotation = Quaternion.Euler(0f, open, 0f);
            if (_bodyT) _bodyT.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(-_vel.x * 4f, -30f, 30f) + (Current == State.Stunned ? t * 400f : 0f));
            var c = Current == State.Hunt ? Hungry : Calm;
            _hitFlash = Mathf.Max(0f, _hitFlash - dt * 4f);
            EnemyUtil.SetEmission(_bodyMat, c * (Current == State.Hunt ? 2f : 0.3f) + Color.white * 10f * _hitFlash * _hitFlash);
            if (_halo) EnemyUtil.SetGlow(_halo.sharedMaterial, c * (Current == State.Hunt ? 0.8f : 0.3f));
            float patt = Current == State.Hunt ? 1.0f + 0.3f * Mathf.Sin(t * 20f) : 0.55f + 0.1f * Mathf.Sin(t * 2f);
            EnemyUtil.SetGlow(_patternMat, c * patt + Color.white * 3f * _hitFlash * _hitFlash);
            if (Current == State.Flutter && Random.value < dt * 2f && _dust) _dust.Emit(1);
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            Current = State.Stunned;
            _timer = 2.5f;
            var away = transform.position - origin; away.z = 0f;
            _vel = (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up) * 6f;
            if (_dust) _dust.Emit(10);
            _hitFlash = 1f;
            ImpactFx.Sparks(transform.position, Calm * 1.6f, 16, 5f, 0.09f, 0.8f);
            GameEvents.RaiseEnemyStunned(transform.position);
        }

        public void ResetState()
        {
            transform.position = _home;
            _vel = Vector3.zero;
            Current = State.Flutter;
        }
    }
}
