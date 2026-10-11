using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Flare: a mounted light lantern that charges (glow + rising tone) and fires slow red light bolts,
    /// either at Umbra (when in range and visible) or along a fixed direction. Bolts travel at a finite
    /// speed, burst on solids, burn on hit and are absorbed by the resonance shield. A Pulse stuns the
    /// flare and dissolves any bolt its wavefront touches.
    /// Level: <c>flare x y [dir=deg] [period=2.4] [speed=6] [damage=20] [range=14] [phase=0]</c>
    /// </summary>
    public class Flare : MonoBehaviour, IPulseReceiver, IResettable
    {
        public float period = 2.4f;
        public float chargeTime = 0.8f;
        public float boltSpeed = 6f;
        public float damage = 20f;
        public float range = 14f;
        public float stunDuration = 3f;
        /// <summary>Fixed firing direction; zero = aim at Umbra.</summary>
        public Vector2 fixedDir;
        public float phase;

        public bool IsStunned => _stun > 0f;
        public float Timer => _t;

        readonly List<FlareBolt> _bolts = new List<FlareBolt>();
        float _t, _stun;
        bool _charging;
        Material _lensMat;
        Renderer _halo;
        Light _light;
        ParticleSystem _sparks;
        Transform _head;
        static readonly Color Col = new Color(1f, 0.12f, 0.08f);

        [LevelEntity("flare")]
        static GameObject Build(EntitySpec s, LevelContext ctx)
        {
            var go = Create(s.Position3);
            var f = go.GetComponent<Flare>();
            if (s.Has("dir")) f.fixedDir = s.Dir();
            f.period = s.Float("period", f.period);
            f.boltSpeed = s.Float("speed", f.boltSpeed);
            f.damage = s.Float("damage", f.damage);
            f.range = s.Float("range", f.range);
            f.phase = s.Float("phase", 0f);
            f._t = f.phase;
            if (ctx != null) go.transform.SetParent(ctx.entities, true);
            return go;
        }

        public static GameObject Create(Vector3 position)
        {
            var go = new GameObject("Flare");
            go.layer = Layers.Enemy;
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var f = go.AddComponent<Flare>();
            f.BuildVisuals();
            return go;
        }

        void BuildVisuals()
        {
            var dark = MaterialLibrary.Tinted("Lit", new Color(0.03f, 0.03f, 0.04f));
            EnemyUtil.Part(PrimitiveType.Cube, transform, new Vector3(0, 0, 0.1f), new Vector3(0.9f, 0.9f, 0.6f), dark, "Mount");
            _head = new GameObject("Head").transform;
            _head.SetParent(transform, false);
            EnemyUtil.Part(PrimitiveType.Cylinder, _head, new Vector3(0.35f, 0, 0), new Vector3(0.45f, 0.3f, 0.45f), dark, "Barrel")
                .transform.localRotation = Quaternion.Euler(0, 0, 90f);
            _lensMat = EnemyUtil.Emissive(Col, Col * 0.6f);
            EnemyUtil.Part(PrimitiveType.Sphere, _head, new Vector3(0.62f, 0, -0.05f), Vector3.one * 0.32f, _lensMat, "Lens");
            _halo = EnemyUtil.Halo(_head, new Vector3(0.62f, 0, -0.3f), 0.9f, Col * 0.5f);
            _light = EnemyUtil.PointLight(_head, new Vector3(0.62f, 0, -0.6f), Col, 3f, 0.6f);
            _sparks = EnemyUtil.Sparks(transform, new Color(1f, 0.5f, 0.3f), 5f, 0.12f, 0.6f);
        }

        Vector2 AimDirection(out bool hasTarget)
        {
            hasTarget = true;
            if (fixedDir.sqrMagnitude > 0.01f) return fixedDir.normalized;
            var p = EnemyUtil.FindPlayer();
            if (p == null) { hasTarget = false; return Vector2.right; }
            var c = EnemyUtil.Center(p);
            var d = (Vector2)(c - transform.position);
            if (d.magnitude > range || !EnemyUtil.LineOfSight(transform.position, c)) { hasTarget = false; return d.normalized; }
            return d.normalized;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_stun > 0f)
            {
                _stun -= dt;
                if (_stun <= 0f) { _t = 0f; SetGlow(0.15f); }
                else SetGlow(0.03f + 0.03f * Mathf.Sin(Time.time * 30f));
                return;
            }

            var dir = AimDirection(out bool hasTarget);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(0, 0, ang), 1f - Mathf.Exp(-8f * dt));

            if (!hasTarget) { _t = Mathf.Min(_t, period - chargeTime - 0.01f); _charging = false; SetGlow(0.15f); return; }

            _t += dt;
            float chargeStart = period - chargeTime;
            if (_t >= chargeStart)
            {
                if (!_charging)
                {
                    _charging = true;
                    // Telegraph: a rising two-note figure (just-intonation fifth → octave).
                    if (AudioDirector.Instance != null)
                    {
                        AudioDirector.Instance.PlayNote(Harmony.Degree(4), 0.18f, 0.25f);
                        AudioDirector.Instance.PlayNote(Harmony.Degree(7), 0.25f, 0.2f);
                    }
                }
                float k = Mathf.Clamp01((_t - chargeStart) / chargeTime);
                SetGlow(0.15f + 5f * k * k);
            }
            if (_t >= period)
            {
                _t = 0f;
                _charging = false;
                Fire(dir);
                SetGlow(0.15f);
            }
        }

        void SetGlow(float k)
        {
            EnemyUtil.SetEmission(_lensMat, Col * k);
            if (_light) _light.intensity = 0.4f + k * 0.8f;
            if (_halo) _halo.transform.localScale = Vector3.one * (0.7f + 0.15f * k);
        }

        public FlareBolt Fire(Vector2 dir)
        {
            var muzzle = transform.position + (Vector3)(dir.normalized * 0.75f);
            var b = FlareBolt.Spawn(muzzle, dir.normalized * boltSpeed, damage, this);
            // Bolts fade out a little beyond the flare's range so they never chase Umbra across the level.
            b.lifetime = (range + 4f) / Mathf.Max(0.5f, boltSpeed);
            _bolts.Add(b);
            if (_sparks) _sparks.Emit(6);
            return b;
        }

        internal void Forget(FlareBolt b) => _bolts.Remove(b);

        public void ClearBolts()
        {
            foreach (var b in _bolts.ToArray()) if (b != null) Destroy(b.gameObject);
            _bolts.Clear();
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            bool was = IsStunned;
            _stun = Mathf.Max(_stun, stunDuration * Mathf.Clamp(strength, 0.5f, 1.5f));
            _charging = false;
            if (_sparks) _sparks.Emit(20);
            if (!was) GameEvents.RaiseEnemyStunned(transform.position);
        }

        public void ResetState()
        {
            ClearBolts();
            _stun = 0f;
            _t = phase;
            _charging = false;
            SetGlow(0.15f);
        }

        void OnDestroy() => ClearBolts();
    }

    /// <summary>A slow glowing red light bolt. Burns on hit; the shield and Pulse dissolve it.</summary>
    public class FlareBolt : MonoBehaviour, IPulseReceiver
    {
        public Vector3 velocity;
        public float damage = 20f;
        public float lifetime = 6f;
        public float hitRadius = 0.45f;
        Flare _owner;
        float _age;
        bool _dead;
        static readonly Color Col = new Color(1f, 0.15f, 0.08f);

        /// <summary>Raised when a bolt is absorbed by the shield (tests / SFX).</summary>
        public static event System.Action<Vector3> Absorbed;

        public static FlareBolt Spawn(Vector3 pos, Vector3 velocity, float damage, Flare owner)
        {
            var go = new GameObject("FlareBolt");
            go.layer = Layers.Enemy;
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.3f;
            var b = go.AddComponent<FlareBolt>();
            b.velocity = new Vector3(velocity.x, velocity.y, 0f);
            b.damage = damage;
            b._owner = owner;
            if (owner != null && owner.transform.parent != null) go.transform.SetParent(owner.transform.parent, true);

            var core = EnemyUtil.Emissive(Col, Col * 6f);
            EnemyUtil.Part(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.26f, core, "Core");
            EnemyUtil.Halo(go.transform, new Vector3(0, 0, -0.2f), 1.1f, Col * 0.8f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.25f; tr.startWidth = 0.26f; tr.endWidth = 0f; tr.minVertexDistance = 0.05f;
            tr.sharedMaterial = EnemyUtil.GlowMaterial(Col * 0.8f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return b;
        }

        void Update()
        {
            if (_dead) return;
            float dt = Time.deltaTime;
            _age += dt;
            var from = transform.position;
            var to = from + velocity * dt;

            if (Physics.Linecast(from, to, out var hit, Layers.Mask(Layers.Ground, Layers.Crystal, Layers.Bridge, Layers.Mirror, Layers.Prism), QueryTriggerInteraction.Ignore))
            { Burst(hit.point, 10); return; }
            transform.position = to;

            if (ShieldState.Contains(to))
            {
                Absorbed?.Invoke(to);
                if (AudioDirector.Instance != null) AudioDirector.Instance.PlayNote(Harmony.Degree(9), 0.2f, 0.3f);
                Burst(to, 18);
                return;
            }

            var p = EnemyUtil.FindPlayer();
            if (p != null)
            {
                var c = EnemyUtil.Center(p);
                var d = c - to; d.z = 0f;
                if (d.sqrMagnitude < hitRadius * hitRadius * 2.2f)
                {
                    EnemyUtil.Hit(p, damage, to, 3f, LightColor.Red);
                    Burst(to, 14);
                    return;
                }
            }
            if (_age > lifetime) Burst(to, 4);
        }

        public void OnPulse(Vector3 origin, float strength) => Burst(transform.position, 12);

        void Burst(Vector3 at, int count)
        {
            if (_dead) return;
            _dead = true;
            var ps = EnemyUtil.Sparks(transform.parent, new Color(1f, 0.45f, 0.25f), 4f, 0.12f, 0.5f);
            ps.transform.position = at;
            ps.Emit(count);
            Destroy(ps.gameObject, 1.5f);
            if (_owner != null) _owner.Forget(this);
            Destroy(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Absorbed = null; }
    }
}
