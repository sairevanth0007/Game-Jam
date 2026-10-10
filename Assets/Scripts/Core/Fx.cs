using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Shared particle effects. One world-space system per kind; effects are Emit() calls, so they're cheap.
    /// Listens to GameEvents for the world events; the player's dust is requested directly by PlayerVisual.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        [Tooltip("Colour of Umbra's dissolve / reform particles.")]
        [SerializeField] Color umbraColor = new Color(0.62f, 0.36f, 1f, 1f);
        [Tooltip("Colour of dust kicked up by landing and running.")]
        [SerializeField] Color dustColor = new Color(0.45f, 0.4f, 0.6f, 0.6f);
        [Tooltip("Colour of echo pickup sparkles.")]
        [SerializeField] Color echoColor = new Color(0.75f, 0.9f, 1f, 1f);

        ParticleSystem sparkles;
        ParticleSystem dust;

        void Awake()
        {
            Instance = this;
            sparkles = RuntimeArt.CreateParticles("Sparkles", transform, 600, 0f, RuntimeArt.SortFx);
            dust = RuntimeArt.CreateParticles("Dust", transform, 300, -0.05f, RuntimeArt.SortPlayer - 1);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.EchoCollected += OnEchoCollected;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRespawned += OnPlayerRespawned;
        }

        void OnDisable()
        {
            GameEvents.EchoCollected -= OnEchoCollected;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRespawned -= OnPlayerRespawned;
        }

        void OnEchoCollected(Vector2 position, int collected, int total) => Sparkle(position, 24, echoColor, 1.5f, 5f);
        void OnCheckpoint(Vector2 position) => Sparkle(position + Vector2.up * 0.8f, 30, umbraColor, 1f, 4f);
        void OnPlayerDied(Vector2 position, DeathCause cause) => Sparkle(position + Vector2.up * 0.35f, 40, umbraColor, 1f, 6f, 0.25f);
        void OnPlayerRespawned(Vector2 position) => Implode(position + Vector2.up * 0.35f, 24, umbraColor);

        // ---------- Public effects ----------

        /// <summary>Burst of glowing points flying out in every direction.</summary>
        public void Sparkle(Vector2 position, int count, Color color, float minSpeed, float maxSpeed, float size = 0.18f)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = position;
                p.velocity = dir * Random.Range(minSpeed, maxSpeed);
                p.startColor = color;
                p.startSize = size * Random.Range(0.6f, 1.3f);
                p.startLifetime = Random.Range(0.35f, 0.8f);
                sparkles.Emit(p, 1);
            }
        }

        /// <summary>Particles that start on a ring and fly into the centre (used when Umbra reforms).</summary>
        public void Implode(Vector2 position, int count, Color color)
        {
            var p = new ParticleSystem.EmitParams();
            const float lifetime = 0.35f;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float radius = Random.Range(1f, 1.6f);
                p.position = position + dir * radius;
                p.velocity = -dir * radius / lifetime;
                p.startColor = color;
                p.startSize = Random.Range(0.1f, 0.2f);
                p.startLifetime = lifetime;
                sparkles.Emit(p, 1);
            }
        }

        /// <summary>Dust puff at the feet. direction.x pushes it sideways (e.g. away from a turn).</summary>
        public void Dust(Vector2 position, int count, float spread = 1f, Vector2 direction = default)
        {
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float side = Random.Range(-1f, 1f);
                p.position = position + new Vector2(side * 0.25f, 0.05f);
                p.velocity = new Vector2(side * 2.2f * spread + direction.x, Random.Range(0.3f, 1.4f) + direction.y);
                p.startColor = dustColor;
                p.startSize = Random.Range(0.12f, 0.28f);
                p.startLifetime = Random.Range(0.25f, 0.5f);
                dust.Emit(p, 1);
            }
        }
    }
}
