using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Side-scroller camera: smooth follow with look-ahead in the facing / running direction,
    /// a vertical dead zone (the camera settles on the ground height instead of bobbing with every jump),
    /// clamped to the level bounds, plus trauma-based shake (GameEvents.CameraShake).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("What to follow (Umbra).")]
        [SerializeField] Transform target;
        [Tooltip("Orthographic size: half the visible height in units.")]
        [SerializeField] float orthographicSize = 7.5f;

        [Header("Follow")]
        [SerializeField] float horizontalSmoothTime = 0.18f;
        [SerializeField] float verticalSmoothTime = 0.28f;
        [Tooltip("How far ahead of Umbra the camera looks, in the running direction.")]
        [SerializeField] float lookAheadDistance = 3f;
        [SerializeField] float lookAheadSmoothTime = 0.6f;
        [Tooltip("The target sits this far above the screen centre (positive = see more below).")]
        [SerializeField] float verticalOffset = 1.6f;
        [Tooltip("Half-height of the vertical dead zone. Inside it, the camera only moves to the ground height on landing.")]
        [SerializeField] float verticalDeadZone = 2.2f;

        [Header("Shake")]
        [SerializeField] float maxShakeOffset = 0.5f;
        [SerializeField] float maxShakeAngle = 2f;
        [SerializeField] float shakeFrequency = 22f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField] float traumaDecay = 1.6f;

        Camera cam;
        PlayerMovement targetMovement;
        Rect bounds;
        bool hasBounds;

        Vector2 focus;
        float lookAhead, lookAheadVelocity;
        float focusY;
        float velocityX, velocityY;
        float trauma;
        float noiseSeed;

        public Camera Camera => cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            noiseSeed = Random.value * 100f;
        }

        void OnEnable()
        {
            GameEvents.CameraShake += AddTrauma;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRespawned += OnPlayerRespawned;
        }

        void OnDisable()
        {
            GameEvents.CameraShake -= AddTrauma;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRespawned -= OnPlayerRespawned;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            targetMovement = newTarget != null ? newTarget.GetComponent<PlayerMovement>() : null;
        }

        /// <summary>Level area the view must stay inside.</summary>
        public void SetBounds(Rect levelBounds)
        {
            bounds = levelBounds;
            hasBounds = true;
        }

        /// <summary>Jump straight to the target (level start, respawn).</summary>
        public void Snap()
        {
            if (target == null) return;
            lookAhead = 0f;
            lookAheadVelocity = 0f;
            velocityX = velocityY = 0f;
            focusY = target.position.y;
            focus = new Vector2(target.position.x, focusY + verticalOffset);
            transform.position = ClampToBounds(focus).ToVector3(transform.position.z);
        }

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        void OnPlayerDied(Vector2 position, DeathCause cause) => AddTrauma(0.45f);

        void OnPlayerRespawned(Vector2 position) => Snap();

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (target != null && Time.timeScale > 0f)
                Follow(Time.deltaTime);

            Vector2 position = ClampToBounds(focus);

            // Shake: trauma² feels better than linear; Perlin noise keeps it smooth.
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            float shake = trauma * trauma;
            float t = Time.unscaledTime * shakeFrequency;
            Vector2 shakeOffset = new Vector2(Mathf.PerlinNoise(noiseSeed, t) * 2f - 1f, Mathf.PerlinNoise(noiseSeed + 10f, t) * 2f - 1f) * (maxShakeOffset * shake);
            float angle = (Mathf.PerlinNoise(noiseSeed + 20f, t) * 2f - 1f) * maxShakeAngle * shake;

            transform.position = (position + shakeOffset).ToVector3(transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void Follow(float dt)
        {
            if (dt <= 0f) return;
            Vector2 p = target.position;

            // Look-ahead follows the facing direction, scaled by how fast we run.
            float facing = targetMovement != null ? targetMovement.FacingDirection : 1f;
            float speed = targetMovement != null ? Mathf.Clamp01(Mathf.Abs(targetMovement.Velocity.x) / targetMovement.MoveSpeed) : 0f;
            float desiredLookAhead = facing * lookAheadDistance * Mathf.Lerp(0.35f, 1f, speed);
            lookAhead = Mathf.SmoothDamp(lookAhead, desiredLookAhead, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);

            // Vertical: settle on ground height when grounded; only chase while airborne if leaving the dead zone.
            bool grounded = targetMovement == null || targetMovement.IsGrounded;
            if (grounded)
                focusY = p.y;
            else if (p.y > focusY + verticalDeadZone)
                focusY = p.y - verticalDeadZone;
            else if (p.y < focusY - verticalDeadZone * 0.5f)
                focusY = p.y + verticalDeadZone * 0.5f; // falling: follow sooner so you see where you land

            focus.x = Mathf.SmoothDamp(focus.x, p.x + lookAhead, ref velocityX, horizontalSmoothTime, Mathf.Infinity, dt);
            focus.y = Mathf.SmoothDamp(focus.y, focusY + verticalOffset, ref velocityY, verticalSmoothTime, Mathf.Infinity, dt);
        }

        Vector2 ClampToBounds(Vector2 position)
        {
            if (!hasBounds) return position;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            position.x = bounds.width <= halfWidth * 2f
                ? bounds.center.x
                : Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            position.y = bounds.height <= halfHeight * 2f
                ? bounds.yMin + halfHeight
                : Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
            return position;
        }
    }

    static class VectorExtensions
    {
        public static Vector3 ToVector3(this Vector2 v, float z) => new Vector3(v.x, v.y, z);
    }
}
