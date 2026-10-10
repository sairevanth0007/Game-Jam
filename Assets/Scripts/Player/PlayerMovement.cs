using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Rigidbody2D platformer movement: run, jump, ground check, coyote time, jump buffer,
    /// variable jump height (release early = lower jump) and faster falling.
    /// Input comes only from InputActionReferences, or from an adapter via SetExternalMove / ExternalJump.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Input (drag from LumenControls > Player)")]
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference jumpAction;

        [Header("Run")]
        [SerializeField] float moveSpeed = 7f;
        [SerializeField] float groundAcceleration = 80f;
        [SerializeField] float airAcceleration = 50f;
        [SerializeField, Range(0f, 1f)] float inputDeadZone = 0.2f;

        [Header("Jump")]
        [Tooltip("Height in world units of a full (held) jump.")]
        [SerializeField] float jumpHeight = 3f;
        [Tooltip("Seconds after walking off a ledge during which a jump still works.")]
        [SerializeField] float coyoteTime = 0.1f;
        [Tooltip("Seconds a jump press is remembered before landing.")]
        [SerializeField] float jumpBufferTime = 0.12f;
        [Tooltip("Upward speed is multiplied by this when jump is released early.")]
        [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.5f;
        [SerializeField] float fallGravityMultiplier = 2f;
        [SerializeField] float maxFallSpeed = 18f;

        [Header("Ground Check")]
        [Tooltip("Layers the player can stand on (e.g. Ground, ShadowPath).")]
        [SerializeField] LayerMask groundLayers = ~0;
        [Tooltip("Optional. If empty, the check sits at the bottom of the player's collider.")]
        [SerializeField] Transform groundCheck;
        [SerializeField] Vector2 groundCheckSize = new Vector2(0.4f, 0.08f);

        [Header("Physics")]
        [Tooltip("Gives the collider a frictionless material so the player doesn't stick to walls.")]
        [SerializeField] bool useFrictionlessMaterial = true;

        public bool IsGrounded { get; private set; }
        public float FacingDirection { get; private set; } = 1f;
        public Vector2 Velocity => body.linearVelocity;

        Rigidbody2D body;
        Collider2D bodyCollider;
        float baseGravityScale;

        Vector2? externalMove;
        bool actionJumpHeld;
        bool externalJumpHeld;
        bool JumpHeld => actionJumpHeld || externalJumpHeld;

        float coyoteTimer;
        float jumpBufferTimer;
        float groundIgnoreTimer;
        bool jumpCutPending;

        readonly List<Collider2D> groundHits = new List<Collider2D>();
        ContactFilter2D groundFilter;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            baseGravityScale = body.gravityScale;

            if (useFrictionlessMaterial && bodyCollider != null && bodyCollider.sharedMaterial == null)
                bodyCollider.sharedMaterial = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };

            if (bodyCollider == null)
                Debug.LogWarning("[PlayerMovement] No Collider2D on the player; add a CapsuleCollider2D or BoxCollider2D.", this);
        }

        void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
                moveAction.action.Enable();

            if (jumpAction != null && jumpAction.action != null)
            {
                jumpAction.action.performed += OnJumpPerformed;
                jumpAction.action.canceled += OnJumpCanceled;
                jumpAction.action.Enable();
            }
        }

        void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
                moveAction.action.Disable();

            if (jumpAction != null && jumpAction.action != null)
            {
                jumpAction.action.Disable();
                jumpAction.action.performed -= OnJumpPerformed;
                jumpAction.action.canceled -= OnJumpCanceled;
            }

            actionJumpHeld = false;
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

        void QueueJump() => jumpBufferTimer = jumpBufferTime;

        // ---------- Movement ----------

        float ReadMoveX()
        {
            Vector2 move = Vector2.zero;
            if (externalMove.HasValue)
                move = externalMove.Value;
            else if (moveAction != null && moveAction.action != null)
                move = moveAction.action.ReadValue<Vector2>();

            return Mathf.Abs(move.x) < inputDeadZone ? 0f : Mathf.Clamp(move.x, -1f, 1f);
        }

        void Update()
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            groundIgnoreTimer -= dt;

            IsGrounded = groundIgnoreTimer <= 0f && CheckGround();
            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - dt;

            Vector2 velocity = body.linearVelocity;

            // Run
            float inputX = ReadMoveX();
            if (inputX != 0f)
                FacingDirection = Mathf.Sign(inputX);
            float accel = IsGrounded ? groundAcceleration : airAcceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, inputX * moveSpeed, accel * dt);

            // Jump (buffered press + coyote time)
            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                velocity.y = JumpVelocity();
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                groundIgnoreTimer = 0.1f;
                IsGrounded = false;
                jumpCutPending = true;
            }

            // Variable jump height: cut once when the button is released while rising.
            if (jumpCutPending && !JumpHeld)
            {
                if (velocity.y > 0f)
                    velocity.y *= jumpCutMultiplier;
                jumpCutPending = false;
            }
            if (velocity.y <= 0f)
                jumpCutPending = false;

            // Faster fall, capped
            body.gravityScale = velocity.y < 0f ? baseGravityScale * fallGravityMultiplier : baseGravityScale;
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

            body.linearVelocity = velocity;
        }

        float JumpVelocity()
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * baseGravityScale);
            return Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        bool CheckGround()
        {
            GetGroundBox(out Vector2 center, out Vector2 size);

            groundFilter.useLayerMask = true;
            groundFilter.layerMask = groundLayers;
            groundFilter.useTriggers = false;

            int count = Physics2D.OverlapBox(center, size, 0f, groundFilter, groundHits);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].attachedRigidbody != body)
                    return true;
            }
            return false;
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
