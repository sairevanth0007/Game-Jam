using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>
    /// Umbra's look and feel: squash &amp; stretch on jump/land (spring), run bob and lean, eyes that look
    /// where you're going and blink, dust puffs, and the dissolve / reform when burned or respawned.
    /// Purely cosmetic: it reads PlayerMovement and GameEvents, never changes gameplay.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Tooltip("The movement this visual follows. Empty = on this GameObject.")]
        [SerializeField] PlayerMovement movement;
        [Tooltip("Scaled for squash/stretch. Its pivot must be at the feet.")]
        [SerializeField] Transform squashRoot;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Transform eyes;
        [Tooltip("Umbra's soft purple glow.")]
        [SerializeField] Light2D glow;

        [Header("Squash & stretch")]
        [SerializeField] Vector2 jumpStretch = new Vector2(0.72f, 1.32f);
        [SerializeField] Vector2 maxLandSquash = new Vector2(1.4f, 0.62f);
        [Tooltip("Impact speed that gives the full landing squash.")]
        [SerializeField] float hardLandingSpeed = 22f;
        [SerializeField] float springStiffness = 260f;
        [SerializeField] float springDamping = 15f;

        [Header("Run")]
        [SerializeField] float runBobHeight = 0.05f;
        [SerializeField] float runBobRate = 16f;
        [SerializeField] float maxLeanDegrees = 7f;
        [SerializeField] float runDustInterval = 0.14f;

        [Header("Eyes")]
        [SerializeField] Vector2 eyesRestPosition = new Vector2(0f, 0.4f);
        [SerializeField] float eyesLookDistance = 0.08f;

        Vector2 scale = Vector2.one;
        Vector2 scaleVelocity;
        float presence = 1f;
        float presenceTarget = 1f;
        float baseGlowIntensity;
        float blinkTimer;
        float blinkAmount;
        float runDustTimer;
        float bobPhase;
        float lean;
        Vector2 eyeOffset;

        public void Setup(PlayerMovement movement, Transform squashRoot, SpriteRenderer body, Transform eyes, Light2D glow)
        {
            this.movement = movement;
            this.squashRoot = squashRoot;
            this.body = body;
            this.eyes = eyes;
            this.glow = glow;
            if (glow != null) baseGlowIntensity = glow.intensity;
        }

        void Awake()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (glow != null) baseGlowIntensity = glow.intensity;
            blinkTimer = Random.Range(2f, 4f);
        }

        void OnEnable()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.Jumped += OnJumped;
                movement.Landed += OnLanded;
            }
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerRespawned += OnRespawned;
        }

        void OnDisable()
        {
            if (movement != null)
            {
                movement.Jumped -= OnJumped;
                movement.Landed -= OnLanded;
            }
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerRespawned -= OnRespawned;
        }

        Vector2 Feet => transform.position;

        void OnJumped()
        {
            scale = jumpStretch;
            scaleVelocity = Vector2.zero;
            if (Fx.Instance != null) Fx.Instance.Dust(Feet, 5, 0.7f);
        }

        void OnLanded(float impactSpeed)
        {
            float t = Mathf.Clamp01(impactSpeed / hardLandingSpeed);
            if (t < 0.15f) return;
            scale = Vector2.Lerp(Vector2.one, maxLandSquash, t);
            scaleVelocity = Vector2.zero;
            if (Fx.Instance != null) Fx.Instance.Dust(Feet, Mathf.RoundToInt(3 + 10 * t), 0.6f + t);
            if (t > 0.75f) GameEvents.RaiseCameraShake(0.12f * t);
        }

        void OnDied(Vector2 position, DeathCause cause)
        {
            presenceTarget = 0f;
            scale = new Vector2(1.5f, 0.5f);
        }

        void OnRespawned(Vector2 position)
        {
            presence = 0f;
            presenceTarget = 1f;
            scale = new Vector2(0.5f, 1.5f);
            scaleVelocity = Vector2.zero;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Spring the scale back to 1 (critically-ish damped).
            scaleVelocity += (Vector2.one - scale) * (springStiffness * dt);
            scaleVelocity *= Mathf.Exp(-springDamping * dt);
            scale += scaleVelocity * dt;

            presence = Mathf.MoveTowards(presence, presenceTarget, dt * (presenceTarget > presence ? 5f : 7f));

            Vector2 velocity = movement != null ? movement.Velocity : Vector2.zero;
            bool grounded = movement != null && movement.IsGrounded;
            float speedRatio = movement != null ? Mathf.Clamp01(Mathf.Abs(velocity.x) / movement.MoveSpeed) : 0f;

            // Run bob + lean into the direction of travel.
            float bob = 0f;
            if (grounded && speedRatio > 0.1f)
            {
                bobPhase += dt * runBobRate * (0.5f + speedRatio * 0.5f);
                bob = Mathf.Abs(Mathf.Sin(bobPhase)) * runBobHeight * speedRatio;

                runDustTimer -= dt;
                if (runDustTimer <= 0f && speedRatio > 0.6f)
                {
                    runDustTimer = runDustInterval;
                    if (Fx.Instance != null) Fx.Instance.Dust(Feet, 1, 0.3f, new Vector2(-Mathf.Sign(velocity.x) * 0.8f, 0f));
                }
            }
            else
            {
                bobPhase = 0f;
            }
            float targetLean = movement != null && movement.MoveInput != 0f ? -movement.MoveInput * maxLeanDegrees * speedRatio : 0f;
            lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-12f * dt));

            // Airborne stretch follows vertical speed a little.
            Vector2 airStretch = Vector2.one;
            if (!grounded)
            {
                float s = Mathf.Clamp(Mathf.Abs(velocity.y) / 25f, 0f, 0.15f);
                airStretch = new Vector2(1f - s, 1f + s);
            }

            if (squashRoot != null)
            {
                Vector2 finalScale = Vector2.Scale(scale, airStretch) * presence;
                squashRoot.localScale = new Vector3(finalScale.x, finalScale.y, 1f);
                squashRoot.localPosition = new Vector3(0f, bob, 0f);
                squashRoot.localRotation = Quaternion.Euler(0f, 0f, lean);
            }

            // Eyes: look toward facing / travel, a little up or down with vertical speed, blink now and then.
            if (eyes != null)
            {
                float facing = movement != null ? movement.FacingDirection : 1f;
                Vector2 look = new Vector2(facing * eyesLookDistance, Mathf.Clamp(velocity.y * 0.006f, -0.05f, 0.05f));
                eyeOffset = Vector2.Lerp(eyeOffset, look, 1f - Mathf.Exp(-14f * dt));
                eyes.localPosition = eyesRestPosition + eyeOffset;

                blinkTimer -= dt;
                if (blinkTimer <= 0f)
                {
                    blinkAmount = 1f;
                    blinkTimer = Random.Range(2.2f, 5f);
                }
                blinkAmount = Mathf.MoveTowards(blinkAmount, 0f, dt * 9f);
                float lid = blinkAmount > 0.5f ? 0.1f : 1f;
                eyes.localScale = new Vector3(1f, lid, 1f);
            }

            if (glow != null)
                glow.intensity = baseGlowIntensity * presence;
        }
    }
}
