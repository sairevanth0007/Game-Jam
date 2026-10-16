using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Lives on Umbra. Every frame samples the light field at feet / chest / head, applies the light
    /// rules (LightRules.Evaluate) and feeds the results into health, resonance and gravity.
    /// Added automatically when the player spawns (GameEvents.PlayerSpawned).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class LightReceiver : MonoBehaviour
    {
        public static LightReceiver Current { get; private set; }

        public LightExposure LastExposure { get; private set; }
        public LightEffect LastEffect { get; private set; }
        /// <summary>Seconds since the player was last burned (for HUD vignette / audio).</summary>
        public float TimeSinceDamage { get; private set; } = 99f;

        PlayerHealth _health;
        Resonance _resonance;
        IPlayerBody _body;
        Collider _collider;
        float _healAccum;
        readonly Vector3[] _points = new Vector3[3];

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _resonance = GetComponent<Resonance>();
            _body = GetComponent<IPlayerBody>();
            _collider = GetComponent<Collider>();
            if (_collider == null) _collider = GetComponentInChildren<Collider>();
        }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        void LateUpdate() { Tick(Time.deltaTime); }

        /// <summary>Sample points (feet, chest, head) in world space.</summary>
        public Vector3[] SamplePoints()
        {
            if (_collider != null && _collider.enabled)
            {
                var b = _collider.bounds;
                _points[0] = new Vector3(b.center.x, b.min.y + 0.15f, 0f);
                _points[1] = new Vector3(b.center.x, b.center.y, 0f);
                _points[2] = new Vector3(b.center.x, b.max.y - 0.15f, 0f);
            }
            else
            {
                var p = transform.position;
                _points[0] = new Vector3(p.x, p.y + 0.2f, 0f);
                _points[1] = new Vector3(p.x, p.y + 0.9f, 0f);
                _points[2] = new Vector3(p.x, p.y + 1.6f, 0f);
            }
            return _points;
        }

        public void Tick(float dt)
        {
            if (_health != null && _health.IsDead)
            {
                if (_body != null) _body.GravityScale = 1f;
                return;
            }
            var exposure = LightField.SampleMax(SamplePoints());
            var fx = LightRules.Evaluate(exposure, LightField.BeatHot);
            LastExposure = exposure;
            LastEffect = fx;
            TimeSinceDamage += dt;

            if (_body != null) _body.GravityScale = fx.gravityScale;
            if (_resonance != null && fx.resonancePerSecond > 0f) _resonance.Add(fx.resonancePerSecond * dt);
            if (_health == null) return;
            if (fx.lethal)
            {
                GameEvents.RaisePlayerDamaged(_health.current, LightColor.Red);
                _health.Kill();
                return;
            }
            if (fx.damagePerSecond > 0f)
            {
                _health.Damage(fx.damagePerSecond * dt, fx.harmfulColor);
                TimeSinceDamage = 0f;
            }
            if (fx.healPerSecond > 0f)
            {
                // Apply healing in 1 HP chunks so the heal event isn't raised every frame.
                _healAccum += fx.healPerSecond * dt;
                if (_healAccum >= 1f) { _health.Heal(_healAccum); _healAccum = 0f; }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameEvents.PlayerSpawned -= Attach;
            GameEvents.PlayerSpawned += Attach;
        }

        static void Attach(GameObject player)
        {
            if (player != null && player.GetComponent<LightReceiver>() == null) player.AddComponent<LightReceiver>();
        }
    }
}
