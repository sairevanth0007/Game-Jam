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
        Material _bodyMat;
        Renderer _halo;
        ParticleSystem _dust;
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
            _bodyMat = EnemyUtil.Emissive(new Color(0.1f, 0.1f, 0.1f), Calm * 0.8f);
            var wingMat = EnemyUtil.GlowMaterial(Calm * 0.55f);
            EnemyUtil.Part(PrimitiveType.Capsule, _bodyT, Vector3.zero, new Vector3(0.12f, 0.18f, 0.12f), _bodyMat, "Thorax");
            _wingL = new GameObject("WingL").transform; _wingL.SetParent(_bodyT, false);
            _wingR = new GameObject("WingR").transform; _wingR.SetParent(_bodyT, false);
            EnemyUtil.Part(PrimitiveType.Quad, _wingL, new Vector3(-0.22f, 0.05f, 0), new Vector3(0.42f, 0.32f, 1f), wingMat, "Wing");
            EnemyUtil.Part(PrimitiveType.Quad, _wingR, new Vector3(0.22f, 0.05f, 0), new Vector3(0.42f, 0.32f, 1f), wingMat, "Wing");
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
            if (_wingL) _wingL.localScale = new Vector3(0.25f + 0.75f * w, 1f, 1f);
            if (_wingR) _wingR.localScale = new Vector3(0.25f + 0.75f * w, 1f, 1f);
            if (_bodyT) _bodyT.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(-_vel.x * 4f, -30f, 30f) + (Current == State.Stunned ? t * 400f : 0f));
            var c = Current == State.Hunt ? Hungry : Calm;
            EnemyUtil.SetEmission(_bodyMat, c * (Current == State.Hunt ? 3f : 0.8f));
            if (_halo) EnemyUtil.SetGlow(_halo.sharedMaterial, c * (Current == State.Hunt ? 0.8f : 0.3f));
            if (Current == State.Flutter && Random.value < dt * 2f && _dust) _dust.Emit(1);
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            Current = State.Stunned;
            _timer = 2.5f;
            var away = transform.position - origin; away.z = 0f;
            _vel = (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up) * 6f;
            if (_dust) _dust.Emit(10);
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
