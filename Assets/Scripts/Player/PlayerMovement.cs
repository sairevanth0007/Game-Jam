using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Rigidbody2D platformer movement tuned for game feel: separate acceleration / deceleration / turn speeds,
    /// jump defined by height + time to apex, coyote time, jump buffer, variable jump height, apex hang,
    /// faster falling, fast fall (hold down), drop through one-way ledges (down + jump) and riding moving platforms.
    /// Input comes only from InputActions (Inspector reference, or LumenControls via LumenInput), or from an
    /// adapter via SetExternalMove / ExternalJump. Use SetControlEnabled(false) to block the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Input (drag from LumenControls > Player; empty = use LumenInput's actions)")]
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference jumpAction;

        [Header("Run")]
        [Tooltip("Top running speed in units/s.")]
        [SerializeField] float moveSpeed = 8.5f;
        [Tooltip("How fast you reach top speed on the ground (units/s²).")]
        [SerializeField] float groundAcceleration = 90f;
        [Tooltip("How fast you stop on the ground when no direction is held.")]
        [SerializeField] float groundDeceleration = 120f;
        [Tooltip("Acceleration when pushing against your current direction (snappy turns).")]
        [SerializeField] float turnAcceleration = 170f;
        [Tooltip("Air control: acceleration while airborne.")]
        [SerializeField] float airAcceleration = 55f;
        [Tooltip("Air drag when no direction is held while airborne.")]
        [SerializeField] float airDeceleration = 25f;
        [SerializeField, Range(0f, 1f)] float inputDeadZone = 0.2f;

        [Header("Jump")]
        [Tooltip("Height in world units of a full (held) jump.")]
        [SerializeField] float jumpHeight = 3.3f;
        [Tooltip("Seconds to reach the top of a full jump. Lower = snappier, heavier.")]
        [SerializeField] float timeToApex = 0.38f;
        [Tooltip("Seconds after walking off a ledge during which a jump still works.")]
        [SerializeField] float coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        [SerializeField] float jumpBufferTime = 0.13f;
        [Tooltip("Upward speed is multiplied by this when jump is released early.")]
        [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.45f;
        [Tooltip("Gravity multiplier near the top of a held jump, for a little hang time.")]
        [SerializeField, Range(0.1f, 1f)] float apexGravityMultiplier = 0.55f;
        [Tooltip("Vertical speed below which we count as 'near the apex'.")]
        [SerializeField] float apexThreshold = 2.2f;

        [Header("Fall")]
        [SerializeField] float fallGravityMultiplier = 1.6f;
        [SerializeField] float maxFallSpeed = 20f;
        [Tooltip("Holding down while falling: extra gravity and a higher speed cap.")]
        [SerializeField] float fastFallGravityMultiplier = 2.4f;
        [SerializeField] float fastFallMaxSpeed = 28f;

        [Header("Ground Check")]
        [Tooltip("Layers the player can stand on (e.g. Ground, ShadowPath).")]
        [SerializeField] LayerMask groundLayers = ~0;
        [Tooltip("Optional. If empty, the check sits at the bottom of the player's collider.")]
        [SerializeField] Transform groundCheck;
        [SerializeField] Vector2 groundCheckSize = new Vector2(0.55f, 0.08f);

        [Header("Physics")]
        [Tooltip("Gives the collider a frictionless material so the player doesn't stick to walls.")]
        [SerializeField] bool useFrictionlessMaterial = true;
        [Tooltip("Seconds collisions with a one-way ledge are ignored after dropping through it.")]
        [SerializeField] float dropThroughTime = 0.3f;

        /// <summary>Fires on every jump (also on this component, for visuals attached to the player).</summary>
        public event Action Jumped;
        /// <summary>Fires on touching ground after being airborne; argument = impact speed (positive).</summary>
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; }
        public float FacingDirection { get; private set; } = 1f;
        public Vector2 Velocity => body.linearVelocity;
        public bool ControlEnabled { get; private set; } = true;
        public bool IsFrozen => !body.simulated;
        public float MoveSpeed => moveSpeed;
        /// <summary>Horizontal input this physics step (-1..1), after dead zone.</summary>
        public float MoveInput { get; private set; }
        public Collider2D BodyCollider => bodyCollider;

        Rigidbody2D body;
        Collider2D bodyCollider;
        float baseGravityScale;
        float jumpVelocity;

        InputAction move, jump;
        Vector2? externalMove;
        bool actionJumpHeld;
        bool externalJumpHeld;
        bool JumpHeld => actionJumpHeld || externalJumpHeld;

        float coyoteTimer;
        float jumpBufferTimer;
        float groundIgnoreTimer;
        bool jumpCutPending;
        float peakFallSpeed;

        Collider2D groundCollider;
        Rigidbody2D groundBody;
        Collider2D droppedThrough;
        float dropTimer;

        readonly List<Collider2D> groundHits = new List<Collider2D>();
        ContactFilter2D groundFilter;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            RecalculateJump();

            if (useFrictionlessMaterial && bodyCollider != null && bodyCollider.sharedMaterial == null)
                bodyCollider.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };

            if (bodyCollider == null)
                Debug.LogWarning("[PlayerMovement] No Collider2D on the player; add a CapsuleCollider2D or BoxCollider2D.", this);
        }

        void OnValidate()
        {
            if (body != null) RecalculateJump();
        }

        // gravity = 2h / t², v0 = 2h / t. Gravity is applied through gravityScale so other physics stays normal.
        void RecalculateJump()
        {
            float apex = Mathf.Max(0.05f, timeToApex);
            float gravity = 2f * jumpHeight / (apex * apex);
            baseGravityScale = gravity / Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y));
            jumpVelocity = 2f * jumpHeight / apex;
            body.gravityScale = baseGravityScale;
        }

        void OnEnable()
        {
            move = LumenInput.Resolve(moveAction, "Player/Move");
            jump = LumenInput.Resolve(jumpAction, "Player/Jump");
            if (jump != null)
            {
                jump.performed += OnJumpPerformed;
                jump.canceled += OnJumpCanceled;
            }
        }

        void OnDisable()
        {
            if (jump != null)
            {
                jump.performed -= OnJumpPerformed;
                jump.canceled -= OnJumpCanceled;
            }

            actionJumpHeld = false;
        }

        /// <summary>True if the collider belongs to the player (works without layers or tags).</summary>
        public static bool IsPlayer(Collider2D collider, out PlayerMovement player)
        {
            player = null;
            if (collider == null) return false;
            Rigidbody2D rb = collider.attachedRigidbody;
            player = rb != null ? rb.GetComponent<PlayerMovement>() : collider.GetComponentInParent<PlayerMovement>();
            return player != null;
        }

        public static bool IsPlayer(Collider2D collider) => IsPlayer(collider, out _);

        // ---------- Control ----------

        /// <summary>false = ignore all input (actions and adapters). Physics keeps running.</summary>
        public void SetControlEnabled(bool enabled)
        {
            ControlEnabled = enabled;
            if (!enabled)
                jumpBufferTimer = 0f;
        }

        /// <summary>true = stop simulating (dissolved, between levels). The body stays where it is.</summary>
        public void SetFrozen(bool frozen)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = !frozen;
        }

        /// <summary>Moves the player to a point and clears velocity and jump state (respawn, checkpoints).</summary>
        public void Teleport(Vector2 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpCutPending = false;
            peakFallSpeed = 0f;
            IsGrounded = false;
            Physics2D.SyncTransforms();
        }

        // ---------- External input (adapters, see InputSourceTemplate) ----------

        /// <summary>Overrides the move action until ClearExternalMove is called.</summary>
        public void SetExternalMove(Vector2 move) => externalMove = move;

        /// <summary>Go back to reading the move action.</summary>
        public void ClearExternalMove() => externalMove = null;

        /// <summary>true = jump button down, false = jump button up. Works alongside the jump action.</summary>
        public void ExternalJump(bool pressed)
        {
            if (pressed && !externalJumpHeld)
                QueueJump();
            externalJumpHeld = pressed;
        }

        // ---------- Input callbacks ----------

        void OnJumpPerformed(InputAction.CallbackContext _)
        {
            actionJumpHeld = true;
            QueueJump();
        }

        void OnJumpCanceled(InputAction.CallbackContext _) => actionJumpHeld = false;

        void QueueJump()
        {
            if (ControlEnabled)
                jumpBufferTimer = jumpBufferTime;
        }

        // ---------- Movement ----------

        Vector2 ReadMove()
        {
            if (!ControlEnabled)
                return Vector2.zero;

            Vector2 value = Vector2.zero;
            if (externalMove.HasValue)
                value = externalMove.Value;
            else if (move != null)
                value = move.ReadValue<Vector2>();

            value.x = Mathf.Abs(value.x) < inputDeadZone ? 0f : Mathf.Clamp(value.x, -1f, 1f);
            value.y = Mathf.Abs(value.y) < 0.5f ? 0f : Mathf.Sign(value.y);
            return value;
        }

        void Update()
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            if (!body.simulated)
                return;

            float dt = Time.fixedDeltaTime;
            groundIgnoreTimer -= dt;
            UpdateDropThrough(dt);

            Vector2 velocity = body.linearVelocity;

            bool wasGrounded = IsGrounded;
            bool touchingGround = groundIgnoreTimer <= 0f && CheckGround();
            Vector2 platformVelocity = touchingGround && groundBody != null ? groundBody.linearVelocity : Vector2.zero;
            // Moving up faster than what we touch = we're jumping through it, not standing on it.
            IsGrounded = touchingGround && velocity.y - platformVelocity.y <= 0.5f;
            if (!IsGrounded)
                platformVelocity = Vector2.zero;

            if (!IsGrounded)
                peakFallSpeed = Mathf.Max(peakFallSpeed, -velocity.y);
            if (IsGrounded && !wasGrounded)
            {
                Landed?.Invoke(peakFallSpeed);
                GameEvents.RaisePlayerLanded(peakFallSpeed);
                peakFallSpeed = 0f;
            }

            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - dt;

            // Run (relative to whatever we stand on, so moving platforms carry us)
            Vector2 input = ReadMove();
            MoveInput = input.x;
            if (input.x != 0f)
                FacingDirection = Mathf.Sign(input.x);

            float carryX = IsGrounded ? platformVelocity.x : 0f;
            float relativeX = velocity.x - carryX;
            float targetX = input.x * moveSpeed;
            float accel;
            if (IsGrounded)
            {
                if (input.x == 0f) accel = groundDeceleration;
                else if (relativeX != 0f && Mathf.Sign(relativeX) != Mathf.Sign(input.x)) accel = turnAcceleration;
                else accel = groundAcceleration;
            }
            else
            {
                accel = input.x == 0f ? airDeceleration : airAcceleration;
            }
            velocity.x = carryX + Mathf.MoveTowards(relativeX, targetX, accel * dt);

            // Drop through a one-way ledge: down + jump while standing on one.
            if (jumpBufferTimer > 0f && IsGrounded && input.y < 0f && IsOneWay(groundCollider))
            {
                StartDropThrough(groundCollider);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
            }

            // Jump (buffered press + coyote time)
            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                velocity.y = jumpVelocity + Mathf.Max(0f, platformVelocity.y);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                groundIgnoreTimer = 0.1f;
                IsGrounded = false;
                jumpCutPending = true;
                peakFallSpeed = 0f;
                Jumped?.Invoke();
                GameEvents.RaisePlayerJumped();
            }

            // Variable jump height: cut once when the button is released while rising.
            if (jumpCutPending && (!JumpHeld || !ControlEnabled))
            {
                if (velocity.y > 0f)
                    velocity.y *= jumpCutMultiplier;
                jumpCutPending = false;
            }
            if (velocity.y <= 0f)
                jumpCutPending = false;

            // Gravity shaping: apex hang, faster fall, fast fall.
            float gravity = baseGravityScale;
            float fallCap = maxFallSpeed;
            if (!IsGrounded)
            {
                bool fastFall = input.y < 0f && velocity.y < apexThreshold;
                if (fastFall)
                {
                    gravity *= fastFallGravityMultiplier;
                    fallCap = fastFallMaxSpeed;
                }
                else if (velocity.y < -apexThreshold)
                {
                    gravity *= fallGravityMultiplier;
                }
                else if (JumpHeld && Mathf.Abs(velocity.y) < apexThreshold)
                {
                    gravity *= apexGravityMultiplier;
                }
                else if (velocity.y < 0f)
                {
                    gravity *= Mathf.Lerp(1f, fallGravityMultiplier, 0.5f);
                }
            }
            body.gravityScale = gravity;
            velocity.y = Mathf.Max(velocity.y, -fallCap);

            body.linearVelocity = velocity;
        }

        // ---------- Ground ----------

        bool CheckGround()
        {
            GetGroundBox(out Vector2 center, out Vector2 size);

            groundFilter.useLayerMask = true;
            groundFilter.layerMask = groundLayers;
            groundFilter.useTriggers = false;

            groundCollider = null;
            groundBody = null;
            int count = Physics2D.OverlapBox(center, size, 0f, groundFilter, groundHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = groundHits[i];
                if (hit.attachedRigidbody == body || hit == droppedThrough)
                    continue;
                // One-way ledges only count when our feet rest on their top.
                if (IsOneWay(hit) && !IsOnTopOfOneWayTile(hit))
                    continue;

                groundCollider = hit;
                groundBody = hit.attachedRigidbody != null && hit.attachedRigidbody.bodyType != RigidbodyType2D.Static ? hit.attachedRigidbody : null;
                return true;
            }
            return false;
        }

        // A one-way tilemap is one big collider, so compare against the tile row under the feet instead of its bounds.
        bool IsOnTopOfOneWayTile(Collider2D hit)
        {
            if (bodyCollider == null) return false;
            float feet = bodyCollider.bounds.min.y;
            float cellTop = Mathf.Round(feet);
            return Mathf.Abs(feet - cellTop) < 0.15f && hit.OverlapPoint(new Vector2(bodyCollider.bounds.center.x, cellTop - 0.05f));
        }

        static bool IsOneWay(Collider2D collider)
        {
            return collider != null && collider.usedByEffector && collider.GetComponent<PlatformEffector2D>() != null;
        }

        void StartDropThrough(Collider2D ledge)
        {
            if (bodyCollider == null || ledge == null) return;
            if (droppedThrough != null) Physics2D.IgnoreCollision(bodyCollider, droppedThrough, false);
            droppedThrough = ledge;
            dropTimer = dropThroughTime;
            Physics2D.IgnoreCollision(bodyCollider, ledge, true);
        }

        void UpdateDropThrough(float dt)
        {
            if (droppedThrough == null) return;
            dropTimer -= dt;
            if (dropTimer <= 0f)
            {
                if (bodyCollider != null) Physics2D.IgnoreCollision(bodyCollider, droppedThrough, false);
                droppedThrough = null;
            }
        }

        void GetGroundBox(out Vector2 center, out Vector2 size)
        {
            size = groundCheckSize;
            if (groundCheck != null)
            {
                center = groundCheck.position;
            }
            else if (bodyCollider != null)
            {
                Bounds b = bodyCollider.bounds;
                center = new Vector2(b.center.x, b.min.y);
            }
            else
            {
                center = transform.position;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider2D>();

            GetGroundBox(out Vector2 center, out Vector2 size);
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
