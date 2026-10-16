using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Kinematic platformer controller for Umbra on the X/Y plane (Z locked at 0).
    /// Collision is resolved with box casts against <see cref="Layers.Solid"/>; the pivot is at the feet.
    /// Feel: acceleration curves, coyote time, jump buffer, variable jump height, apex hang,
    /// fall-speed cap and a horizontal dash with one air charge.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UmbraController : MonoBehaviour, IPlayerBody
    {
        [Header("Run")]
        public float maxSpeed = 7f;
        public float groundAccel = 70f;
        public float groundDecel = 85f;
        public float airAccel = 48f;
        public float airDecel = 30f;

        [Header("Jump")]
        public float jumpHeight = 3.2f;
        public float gravity = 38f;
        public float jumpCutMultiplier = 2.4f;
        public float fallMultiplier = 1.35f;
        public float apexHangThreshold = 1.6f;
        public float apexHangMultiplier = 0.55f;
        public float maxFallSpeed = 20f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;

        [Header("Dash")]
        public float dashSpeed = 17f;
        public float dashDuration = 0.18f;
        public float dashCooldown = 0.35f;

        [Header("Body")]
        public float width = 0.6f;
        public float height = 1.75f;
        public float skin = 0.02f;

        public Vector2 Velocity { get; set; }
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public float GravityScale { get; set; } = 1f;
        public ICommandSource Commands { get; set; } = new HumanCommandSource();

        /// <summary>The command snapshot used this frame (read by the rig for cast/shield poses).</summary>
        public PlayerCommands Current { get; private set; }
        public bool IsDashing => _dashTimer > 0f;
        public float TimeSinceLanded { get; private set; } = 10f;
        public float LastLandingSpeed { get; private set; }
        public bool InputLocked { get; set; }

        PlayerHealth _health;
        BoxCollider _box;
        float _coyote, _buffer, _dashTimer, _dashCooldownTimer;
        bool _airDashAvailable = true;
        bool _jumping;          // ascending from a jump (variable height applies)
        Transform _ground;
        Vector3 _groundLastPos;
        readonly Collider[] _overlaps = new Collider[8];

        Vector3 HalfExtents => new Vector3(width * 0.5f - skin, height * 0.5f - skin, 0.3f);
        Vector3 Center(Vector3 feet) => feet + new Vector3(0f, height * 0.5f, 0f);

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _box = GetComponent<BoxCollider>();
        }

        public void Teleport(Vector3 position)
        {
            position.z = 0f;
            transform.position = position;
            Velocity = Vector2.zero;
            _dashTimer = 0f;
            _jumping = false;
            _ground = null;
            _airDashAvailable = true;
            Physics.SyncTransforms();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool dead = _health != null && _health.IsDead;
            var cmd = (dead || InputLocked || Commands == null) ? default : Commands.Poll();
            Current = cmd;

            if (cmd.jumpPressed) _buffer = jumpBuffer;
            if (cmd.dashPressed && !dead) TryDash();
            if (Mathf.Abs(cmd.moveX) > 0.1f && !IsDashing) Facing = cmd.moveX > 0 ? 1 : -1;

            CarryWithGround();

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 120f)), 1, 6);
            float h = dt / steps;
            for (int i = 0; i < steps; i++) Step(h, cmd, dead);

            _buffer -= dt;
            _dashCooldownTimer -= dt;
            TimeSinceLanded += dt;

            var p = transform.position;
            if (p.z != 0f) { p.z = 0f; transform.position = p; }
            if (_ground != null) _groundLastPos = _ground.position;
        }

        void TryDash()
        {
            if (_dashCooldownTimer > 0f || IsDashing) return;
            if (!Grounded && !_airDashAvailable) return;
            if (!Grounded) _airDashAvailable = false;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown + dashDuration;
            _jumping = false;
            GameEvents.RaisePowerUsed(PowerType.Dash);
        }

        void Step(float dt, PlayerCommands cmd, bool dead)
        {
            var v = Velocity;
            float g = gravity * Mathf.Max(0.05f, GravityScale);

            if (IsDashing)
            {
                _dashTimer -= dt;
                v.x = Facing * dashSpeed;
                v.y = 0f;
                if (_dashTimer <= 0f) v.x = Facing * maxSpeed;
            }
            else
            {
                // Horizontal: accelerate toward target speed, decelerate faster than we accelerate.
                float target = dead ? 0f : cmd.moveX * maxSpeed;
                bool speeding = Mathf.Abs(target) > 0.01f && Mathf.Sign(target) == Mathf.Sign(v.x) && Mathf.Abs(target) >= Mathf.Abs(v.x);
                float rate = Grounded ? (speeding ? groundAccel : groundDecel) : (speeding ? airAccel : airDecel);
                v.x = Mathf.MoveTowards(v.x, target, rate * dt);

                // Jump (buffered + coyote).
                if (_buffer > 0f && _coyote > 0f && !dead)
                {
                    v.y = Mathf.Sqrt(2f * gravity * jumpHeight); // launch speed independent of blue light → blue = higher
                    _buffer = 0f; _coyote = 0f;
                    _jumping = true;
                    Grounded = false;
                    _ground = null;
                    GameEvents.RaisePlayerJumped();
                }

                // Gravity with variable height, apex hang and heavier fall.
                float gm = 1f;
                if (v.y > 0f && _jumping && !cmd.jumpHeld) gm = jumpCutMultiplier;
                else if (Mathf.Abs(v.y) < apexHangThreshold && cmd.jumpHeld && !Grounded) gm = apexHangMultiplier;
                else if (v.y < 0f) gm = fallMultiplier;
                v.y -= g * gm * dt;
                float cap = maxFallSpeed * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(GravityScale));
                if (v.y < -cap) v.y = -cap;
                if (v.y <= 0f) _jumping = false;
            }

            Velocity = v;
            Depenetrate();
            MoveHorizontal(v.x * dt);
            MoveVertical(Velocity.y * dt, dt);
        }

        void MoveHorizontal(float dx)
        {
            if (Mathf.Abs(dx) < 1e-6f) return;
            var pos = transform.position;
            var dir = new Vector3(Mathf.Sign(dx), 0f, 0f);
            var he = HalfExtents; he.y -= 0.03f; // don't snag on the floor we stand on
            if (Physics.BoxCast(Center(pos), he, dir, out var hit, Quaternion.identity, Mathf.Abs(dx) + skin, Layers.Solid, QueryTriggerInteraction.Ignore))
            {
                float move = Mathf.Max(0f, hit.distance - skin);
                pos.x += dir.x * move;
                Velocity = new Vector2(0f, Velocity.y);
            }
            else pos.x += dx;
            transform.position = pos;
        }

        void MoveVertical(float dy, float dt)
        {
            var pos = transform.position;
            bool wasGrounded = Grounded;
            float fallSpeed = -Velocity.y;
            if (dy > 0f)
            {
                if (Physics.BoxCast(Center(pos), HalfExtents, Vector3.up, out var hit, Quaternion.identity, dy + skin, Layers.Solid, QueryTriggerInteraction.Ignore))
                {
                    pos.y += Mathf.Max(0f, hit.distance - skin);
                    Velocity = new Vector2(Velocity.x, 0f);
                    _jumping = false;
                }
                else pos.y += dy;
                Grounded = false;
                _ground = null;
            }
            else
            {
                float probe = Mathf.Max(-dy, 0.04f);
                if (Physics.BoxCast(Center(pos), HalfExtents, Vector3.down, out var hit, Quaternion.identity, probe + skin, Layers.Solid, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > 0.5f)
                {
                    pos.y -= Mathf.Max(0f, hit.distance - skin);
                    if (Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);
                    Grounded = true;
                    if (_ground != hit.collider.transform) { _ground = hit.collider.transform; _groundLastPos = _ground.position; }
                }
                else
                {
                    pos.y += dy;
                    Grounded = false;
                    _ground = null;
                }
            }
            transform.position = pos;

            if (Grounded)
            {
                _coyote = coyoteTime;
                _airDashAvailable = true;
                if (!wasGrounded)
                {
                    LastLandingSpeed = fallSpeed;
                    TimeSinceLanded = 0f;
                    GameEvents.RaisePlayerLanded();
                }
            }
            else _coyote -= dt;
        }

        /// <summary>Move with the platform we stand on (moving platforms, sinking bridges).</summary>
        void CarryWithGround()
        {
            if (_ground == null || !Grounded) return;
            if (!_ground.gameObject.activeInHierarchy) { _ground = null; Grounded = false; return; }
            var delta = _ground.position - _groundLastPos;
            if (delta.sqrMagnitude > 1e-8f && delta.sqrMagnitude < 4f)
            {
                delta.z = 0f;
                transform.position += delta;
            }
        }

        /// <summary>Pushes out of anything solid that appeared inside us (e.g. a bridge, a revealed platform).</summary>
        void Depenetrate()
        {
            if (_box == null) return;
            var pos = transform.position;
            int n = Physics.OverlapBoxNonAlloc(Center(pos), HalfExtents, _overlaps, Quaternion.identity, Layers.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlaps[i];
                if (c == null || c == _box) continue;
                if (Physics.ComputePenetration(_box, pos, Quaternion.identity, c, c.transform.position, c.transform.rotation, out var dir, out var dist))
                {
                    dir.z = 0f;
                    pos += dir * (dist + 0.001f);
                    if (dir.y > 0.5f && Velocity.y < 0f) Velocity = new Vector2(Velocity.x, 0f);
                }
            }
            transform.position = pos;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Center(transform.position), HalfExtents * 2f);
        }
    }
}
