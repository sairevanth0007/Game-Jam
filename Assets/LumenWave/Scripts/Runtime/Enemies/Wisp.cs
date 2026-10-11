using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Wisp: a hungry mote of light that chases Umbra when it can see it. Contact burns and knocks back.
    /// The resonance shield is a wall it cannot cross; a Pulse scatters and stuns it.
    /// Level: <c>wisp x y [sight=8] [speed=5.2] [damage=18]</c>
    /// </summary>
    public class Wisp : MonoBehaviour, IPulseReceiver, IResettable
    {
        public enum State { Idle, Chase, Return, Stunned, Recoil }

        public float sight = 8f;
        public float loseSight = 12f;
        public float maxSpeed = 5.2f;
        public float accel = 14f;
        public float damage = 18f;
        public float radius = 0.3f;
        public float contactRadius = 0.75f;
        public float hitCooldown = 1f;

        public State Current { get; private set; } = State.Idle;
        public Vector3 Velocity => _vel;

        Vector3 _home, _vel;
        float _stateTimer, _hitTimer, _dim, _seed;
        Material _coreMat;
        Renderer _halo;
        Light _light;
        TrailRenderer _trail;
        ParticleSystem _sparks;
        float _hitFlash;   // visual only
        static readonly Color Col = new Color(0.75f, 0.9f, 1f);

        [LevelEntity("wisp")]
        static GameObject Build(EntitySpec s, LevelContext ctx)
        {
            var go = Create(s.Position3 + Vector3.up * 0.5f);
            var w = go.GetComponent<Wisp>();
            w.sight = s.Float("sight", w.sight);
            w.maxSpeed = s.Float("speed", w.maxSpeed);
            w.damage = s.Float("damage", w.damage);
            if (ctx != null) go.transform.SetParent(ctx.entities, true);
            return go;
        }

        public static GameObject Create(Vector3 position)
        {
            var go = new GameObject("Wisp");
            go.layer = Layers.Enemy;
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.35f;
            var w = go.AddComponent<Wisp>();
            w.BuildVisuals();
            return go;
        }

        void BuildVisuals()
        {
            _home = transform.position;
            _seed = Random.value * 10f;
            _coreMat = EnemyUtil.Emissive(Col, Col * 5f);
            EnemyUtil.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.32f, _coreMat, "Core");
            _halo = EnemyUtil.Halo(transform, new Vector3(0, 0, -0.2f), 1.5f, Col * 0.9f);
            _light = EnemyUtil.PointLight(transform, new Vector3(0, 0, -0.5f), Col, 3.5f, 2f);

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.35f;
            _trail.startWidth = 0.28f;
            _trail.endWidth = 0f;
            _trail.minVertexDistance = 0.05f;
            _trail.sharedMaterial = EnemyUtil.GlowMaterial(Col * 0.7f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.6f, 1f), 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;

            _sparks = EnemyUtil.Sparks(transform, Col, 4f, 0.1f, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _hitTimer -= dt;
            _dim = Mathf.Max(0f, _dim - dt * 2f);
            var pos = transform.position;
            var player = EnemyUtil.FindPlayer();
            Vector3 target = _home;
            Vector3 pc = player != null ? EnemyUtil.Center(player) : Vector3.zero;
            float dist = player != null ? Vector2.Distance(pos, pc) : float.MaxValue;
            var hp = player != null ? player.GetComponent<PlayerHealth>() : null;
            bool playerAlive = hp == null || !hp.IsDead;

            switch (Current)
            {
                case State.Idle:
                case State.Return:
                    if (playerAlive && dist < sight && EnemyUtil.LineOfSight(pos, pc)) Current = State.Chase;
                    else if (Current == State.Return && Vector3.Distance(pos, _home) < 0.3f) Current = State.Idle;
                    break;
                case State.Chase:
                    if (!playerAlive || dist > loseSight) Current = State.Return;
                    break;
                case State.Stunned:
                case State.Recoil:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f) Current = State.Return;
                    break;
            }

            Vector3 desired = Vector3.zero;
            float speed = maxSpeed;
            switch (Current)
            {
                case State.Idle:
                    // Lazy figure-eight hover around home.
                    float t = Time.time + _seed;
                    target = _home + new Vector3(Mathf.Sin(t * 0.9f) * 0.6f, Mathf.Sin(t * 1.8f) * 0.3f, 0f);
                    desired = (target - pos); speed = 1.5f;
                    break;
                case State.Chase:
                    // Lead the target a little and wobble so the motion feels alive.
                    target = pc + new Vector3(0f, Mathf.Sin(Time.time * 4f + _seed) * 0.3f, 0f);
                    desired = (target - pos);
                    break;
                case State.Return:
                    desired = _home - pos; speed = maxSpeed * 0.6f;
                    break;
                case State.Stunned:
                    desired = Vector3.zero;
                    _vel *= Mathf.Exp(-3f * dt);
                    break;
                case State.Recoil:
                    desired = Vector3.zero;
                    break;
            }
            desired.z = 0f;
            if (Current != State.Stunned && Current != State.Recoil)
            {
                var want = desired.sqrMagnitude > 0.0004f ? desired.normalized * Mathf.Min(speed, desired.magnitude * 3f) : Vector3.zero;
                _vel = Vector3.MoveTowards(_vel, want, accel * dt);
            }

            pos = EnemyUtil.FlyMove(pos, _vel * dt, radius);
            if (EnemyUtil.RepelFromShield(ref pos, ref _vel, radius + 0.05f))
            {
                _dim = 1f;
                if (Current == State.Chase && Random.value < 0.05f && _sparks) _sparks.Emit(2);
                ShieldDome.Ripple(pos);
            }
            transform.position = pos;

            // Contact damage (shield blocks: we can never be inside it after the repel above).
            if (player != null && playerAlive && Current != State.Stunned && _hitTimer <= 0f)
            {
                float d = Vector2.Distance(pos, pc);
                if (d < contactRadius && !ShieldState.Contains(pos))
                {
                    EnemyUtil.Hit(player, damage, pos, 7f, LightColor.White);
                    _hitTimer = hitCooldown;
                    var away = (pos - pc); away.z = 0f;
                    _vel = (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up) * 6f;
                    Current = State.Recoil;
                    _stateTimer = 0.6f;
                    if (_sparks) _sparks.Emit(12);
                }
            }

            // Visual state.
            float flick = Current == State.Stunned ? (Random.value < 0.3f ? 0.6f : 0.1f) : 1f;
            float bright = Mathf.Lerp(1f, 0.25f, _dim) * flick;
            float chaseBoost = Current == State.Chase ? 1.3f : 1f;
            _hitFlash = Mathf.Max(0f, _hitFlash - dt * 4f);
            EnemyUtil.SetEmission(_coreMat, Col * 5f * bright * chaseBoost + Color.white * 14f * _hitFlash * _hitFlash);
            if (_halo) _halo.transform.localScale = Vector3.one * (1.3f + 0.2f * Mathf.Sin(Time.time * 9f + _seed)) * Mathf.Lerp(1f, 0.6f, _dim) * (1f + 1.2f * _hitFlash);
            if (_light) _light.intensity = 2f * bright * chaseBoost;
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            var away = transform.position - origin; away.z = 0f;
            _vel = (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up) * 9f * Mathf.Clamp(strength, 0.5f, 1.5f);
            Current = State.Stunned;
            _stateTimer = 2.5f;
            if (_sparks) _sparks.Emit(16);
            _hitFlash = 1f;
            ImpactFx.Sparks(transform.position, Col * 2.2f, 26, 8f, 0.12f, 0.6f);
            GameEvents.RaiseEnemyStunned(transform.position);
        }

        public void ResetState()
        {
            transform.position = _home;
            _vel = Vector3.zero;
            Current = State.Idle;
            _hitTimer = 0f;
            _dim = 0f;
            if (_trail) _trail.Clear();
        }
    }
}
