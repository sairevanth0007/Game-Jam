using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Smooth side-scrolling follow camera: close framing (Umbra is ~15 % of the screen height), a slight
    /// downward pitch for depth, look-ahead biased forward so hazards ahead stay visible, vertical dead zone,
    /// downward peek while falling, clamped to level bounds, plus trauma-based screen shake.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public const float DefaultDistance = 21f;
        public const float FieldOfView = 30f;
        /// <summary>Downward pitch in degrees (small: the playfield stays side-on, platform tops just show).</summary>
        public const float Pitch = 3f;
        public static CameraRig Instance { get; private set; }

        public float distance = DefaultDistance;
        public float lookAhead = 3.4f;
        public float velocityLead = 0.22f;
        public float verticalBias = 3.4f;   // with the pitch, the ground sits ~28% from the bottom of the screen
        public float deadZoneUp = 1.4f;
        public float deadZoneDown = 0.6f;
        public float smoothTimeX = 0.28f;
        public float smoothTimeY = 0.4f;
        public float maxShakeOffset = 0.45f;
        public float maxShakeAngle = 1.2f;

        public Transform Target { get; private set; }
        public Rect Bounds { get; private set; } = new Rect(-1000, -1000, 2000, 2000);
        /// <summary>Extra framing offset that cutscenes (e.g. finale) can set.</summary>
        public Vector2 extraOffset;
        /// <summary>Extra distance (zoom out) that cutscenes can set.</summary>
        public float extraDistance;

        IPlayerBody _body;
        Camera _cam;
        float _lookX, _focusY, _velX, _velY, _trauma, _seed;
        Vector3 _base;

        void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _seed = Random.value * 100f;
            _base = transform.position;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetTarget(Transform t)
        {
            Target = t;
            _body = t != null ? t.GetComponent<IPlayerBody>() : null;
            if (t != null) _focusY = t.position.y;
        }

        public void SetBounds(Rect r) => Bounds = r;

        /// <summary>Add screen shake (0..1 trauma). Ignored when disabled in settings.</summary>
        public static void Shake(float amount)
        {
            if (Instance == null || !Settings.ScreenShake) return;
            Instance._trauma = Mathf.Clamp01(Instance._trauma + amount);
        }

        public void SnapToTarget()
        {
            if (Target == null) TryFindTarget();
            if (Target == null) return;
            _focusY = Target.position.y;
            _lookX = (_body != null ? _body.Facing : 1) * lookAhead;
            _base = Clamp(Goal());
            _velX = _velY = 0f;
            transform.position = _base;
        }

        /// <summary>Frames a world point (used when there is no target, e.g. screenshots / cutscenes).</summary>
        public void FramePoint(Vector3 p)
        {
            _base = Clamp(new Vector3(p.x, p.y + verticalBias, p.z - distance - extraDistance));
            _velX = _velY = 0f;
            transform.position = _base;
        }

        void TryFindTarget()
        {
            var rt = LevelRuntime.Current;
            if (rt != null && rt.Player != null) SetTarget(rt.Player.transform);
        }

        Vector3 Goal()
        {
            var p = Target.position;
            float y = _focusY + verticalBias;
            if (_body != null && _body.Velocity.y < -9f) y -= 3.2f;   // peek below when falling
            return new Vector3(p.x + _lookX + extraOffset.x, y + extraOffset.y, p.z - distance - extraDistance);
        }

        Vector3 Clamp(Vector3 pos)
        {
            if (_cam == null) return pos;
            float dist = Mathf.Abs(pos.z);
            float halfH = dist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;
            var b = Bounds;
            // The pitch lowers the view centre on the play plane; clamp that centre, not the camera height.
            float pitchDrop = dist * Mathf.Tan(Pitch * Mathf.Deg2Rad);
            pos.y -= pitchDrop;
            pos.x = b.width > halfW * 2f ? Mathf.Clamp(pos.x, b.xMin + halfW, b.xMax - halfW) : b.center.x;
            float minY = b.yMin + halfH, maxY = Mathf.Max(minY, b.yMax - halfH);
            pos.y = Mathf.Clamp(pos.y, minY, maxY) + pitchDrop;
            return pos;
        }

        void LateUpdate()
        {
            if (Target == null) TryFindTarget();
            float dt = Time.unscaledDeltaTime > 0 ? Mathf.Min(Time.deltaTime, 0.05f) : 0f;
            if (Target != null && dt > 0f)
            {
                int facing = _body != null ? _body.Facing : 1;
                float vx = _body != null ? _body.Velocity.x : 0f;
                float desiredLook = facing * lookAhead + Mathf.Clamp(vx * velocityLead, -2.2f, 2.2f);
                _lookX = Mathf.Lerp(_lookX, desiredLook, 1f - Mathf.Exp(-2.2f * dt));

                float ty = Target.position.y;
                bool grounded = _body != null && _body.Grounded;
                if (ty > _focusY + deadZoneUp) _focusY = ty - deadZoneUp;
                else if (ty < _focusY - deadZoneDown) _focusY = ty + deadZoneDown;
                if (grounded) _focusY = Mathf.Lerp(_focusY, ty, 1f - Mathf.Exp(-3f * dt));

                var goal = Clamp(Goal());
                _base.x = Mathf.SmoothDamp(_base.x, goal.x, ref _velX, smoothTimeX, Mathf.Infinity, dt);
                _base.y = Mathf.SmoothDamp(_base.y, goal.y, ref _velY, smoothTimeY, Mathf.Infinity, dt);
                _base.z = Mathf.Lerp(_base.z, goal.z, 1f - Mathf.Exp(-3f * dt));
            }

            // Trauma shake (squared for a nicer falloff), Perlin noise so it is smooth.
            _trauma = Mathf.Max(0f, _trauma - Time.unscaledDeltaTime * 1.6f);
            float s = _trauma * _trauma;
            float t = Time.unscaledTime * 22f;
            var off = new Vector3(Mathf.PerlinNoise(_seed, t) - 0.5f, Mathf.PerlinNoise(_seed + 7f, t) - 0.5f, 0f) * 2f * maxShakeOffset * s;
            float ang = (Mathf.PerlinNoise(_seed + 13f, t) - 0.5f) * 2f * maxShakeAngle * s;
            transform.SetPositionAndRotation(_base + off, Quaternion.Euler(Pitch, 0f, ang));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }
}
