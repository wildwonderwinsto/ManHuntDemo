using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Professional-grade player movement with industry-standard jump mechanics.
    /// Includes: Coyote Time, Jump Buffering, Enhanced Fall Gravity, Head Collision Detection.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(PlayerInput), typeof(CapsuleCollider))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Base walking speed.")]
        public float speed = 8f;

        [Tooltip("Sprint speed (when holding sprint button).")]
        public float sprintSpeed = 12f;

        [Header("Jump Settings")]
        [Tooltip("Jump force applied when jumping.")]
        [Range(5f, 20f)]
        public float jumpForce = 12f;

        [Tooltip("Gravity multiplier when falling (makes fall faster than rise for better game feel).")]
        [Range(1f, 5f)]
        public float fallGravityMultiplier = 2.5f;

        [Header("Coyote Time & Jump Buffering")]
        [Tooltip("Grace period to jump after leaving a ledge (seconds).")]
        [Range(0f, 0.3f)]
        public float coyoteTime = 0.15f;

        [Tooltip("How early can player press jump before landing and still jump (seconds).")]
        [Range(0f, 0.3f)]
        public float jumpBufferTime = 0.15f;

        [Header("Head Collision Settings")]
        [Tooltip("Downforce applied when head hits ceiling (prevents jump spam abuse).")]
        [Range(0f, 20f)]
        public float headBonkDownforce = 12f;

        [Tooltip("Distance above player to check for ceiling.")]
        [Range(0.1f, 0.5f)]
        public float ceilingCheckDistance = 0.2f;

        [Tooltip("Layer mask for ceiling detection.")]
        public LayerMask ceilingMask;

        [Header("Physics Settings")]
        [Tooltip("Drag when grounded (prevents sliding).")]
        public float groundDrag = 6f;

        [Tooltip("Drag when in air (slight air resistance).")]
        public float airDrag = 1f;

        [Header("Ground Detection")]
        [Tooltip("Transform at player's feet for ground detection.")]
        public Transform groundCheck;

        [Tooltip("Radius of ground detection sphere.")]
        public float groundDistance = 0.4f;

        [Tooltip("What layers count as ground.")]
        public LayerMask groundMask;

        [Header("Debug")]
        [Tooltip("Show jump debug info in console.")]
        public bool showDebugInfo = false;

        // Components
        private Rigidbody _rb;
        private PlayerInput _playerInput;
        private CapsuleCollider _capsuleCollider;

        // Input Actions
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        // Ground State
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private float _lastGroundedTime;

        // Jump State
        private float _lastJumpPressedTime;
        private bool _hasUsedCoyoteJump;

        // Head Collision
        private bool _headHitCeiling;

        private void Awake()
        {
            // Get components
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();
            _capsuleCollider = GetComponent<CapsuleCollider>();

            // Validation
            if (groundCheck == null)
            {
                Debug.LogError("PlayerMovement: No groundCheck assigned! Please assign the GroundCheck transform.");
                enabled = false;
                return;
            }

            // Get input actions
            _moveAction = _playerInput.actions["Move"];
            _jumpAction = _playerInput.actions["Jump"];
            _sprintAction = _playerInput.actions["Sprint"];

            if (_moveAction == null || _jumpAction == null || _sprintAction == null)
            {
                Debug.LogError("PlayerMovement: Could not find required input actions!");
                enabled = false;
                return;
            }

            // Configure Rigidbody
            _rb.freezeRotation = true; // Prevent player from tipping over
        }

        private void OnEnable()
        {
            // Subscribe to jump input
            _jumpAction.performed += OnJumpPressed;
        }

        private void OnDisable()
        {
            // Unsubscribe from jump input
            _jumpAction.performed -= OnJumpPressed;
        }

        private void Update()
        {
            // Ground detection
            DetectGroundState();

            // Ceiling detection
            DetectCeilingCollision();

            // Update drag based on ground state
            _rb.linearDamping = _isGrounded ? groundDrag : airDrag;

            // Handle jump buffering and coyote time
            HandleJumpLogic();
        }

        private void FixedUpdate()
        {
            // Handle horizontal movement
            HandleMovement();

            // Apply enhanced fall gravity
            ApplyFallGravity();
        }

        /// <summary>
        /// Detects if player is grounded and tracks state changes.
        /// </summary>
        private void DetectGroundState()
        {
            _wasGroundedLastFrame = _isGrounded;
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            // Track when we were last grounded (for coyote time)
            if (_isGrounded)
            {
                _lastGroundedTime = Time.time;
                _hasUsedCoyoteJump = false; // Reset coyote jump flag
            }
        }

        /// <summary>
        /// Detects ceiling collision above player's head.
        /// </summary>
        private void DetectCeilingCollision()
        {
            // Raycast upward from top of capsule
            Vector3 rayStart = transform.position + Vector3.up * (_capsuleCollider.height * 0.5f);
            _headHitCeiling = Physics.Raycast(
                rayStart,
                Vector3.up,
                ceilingCheckDistance,
                ceilingMask
            );

            // If jumping and hit ceiling, cancel upward velocity
            if (_headHitCeiling && _rb.linearVelocity.y > 0f)
            {
                // Cancel upward velocity
                Vector3 vel = _rb.linearVelocity;
                vel.y = 0f;
                _rb.linearVelocity = vel;

                // Apply downforce to prevent jump spam cheese
                _rb.AddForce(Vector3.down * headBonkDownforce, ForceMode.Impulse);

                if (showDebugInfo)
                {
                    Debug.Log("HEAD BONK! Applied downforce to prevent jump spam.");
                }
            }
        }

        /// <summary>
        /// Handles horizontal movement (WASD).
        /// </summary>
        private void HandleMovement()
        {
            Vector2 input = _moveAction.ReadValue<Vector2>();

            // Calculate movement direction (normalized to prevent faster diagonal movement)
            Vector3 rawDirection = transform.right * input.x + transform.forward * input.y;
            Vector3 direction = rawDirection.magnitude > 0 ? rawDirection.normalized : Vector3.zero;

            // Determine target speed
            float targetSpeed = _sprintAction.IsPressed() ? sprintSpeed : speed;

            // Apply horizontal velocity (preserve vertical velocity)
            Vector3 targetVelocity = direction * targetSpeed;
            Vector3 currentVel = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(targetVelocity.x, currentVel.y, targetVelocity.z);
        }

        /// <summary>
        /// Handles jump buffering and coyote time.
        /// </summary>
        private void HandleJumpLogic()
        {
            // Check if we can jump (grounded OR within coyote time)
            bool canCoyoteJump = (Time.time - _lastGroundedTime) <= coyoteTime && !_hasUsedCoyoteJump;
            bool canJump = _isGrounded || canCoyoteJump;

            // Check if jump was buffered (pressed recently)
            bool jumpBuffered = (Time.time - _lastJumpPressedTime) <= jumpBufferTime;

            // Execute jump if buffered and able
            if (jumpBuffered && canJump)
            {
                ExecuteJump();

                // Mark coyote jump as used
                if (canCoyoteJump && !_isGrounded)
                {
                    _hasUsedCoyoteJump = true;
                    if (showDebugInfo)
                    {
                        Debug.Log("COYOTE TIME JUMP!");
                    }
                }
            }
        }

        /// <summary>
        /// Called when jump button is pressed.
        /// </summary>
        private void OnJumpPressed(InputAction.CallbackContext context)
        {
            _lastJumpPressedTime = Time.time;
        }

        /// <summary>
        /// Executes the jump.
        /// </summary>
        private void ExecuteJump()
        {
            // Reset vertical velocity for consistent jump height
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _rb.linearVelocity = vel;

            // Apply jump force
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // Clear jump buffer
            _lastJumpPressedTime = -1f;

            if (showDebugInfo)
            {
                Debug.Log($"JUMP! Force: {jumpForce}");
            }
        }

        /// <summary>
        /// Applies enhanced gravity when falling for better game feel.
        /// </summary>
        private void ApplyFallGravity()
        {
            // Don't apply custom gravity when grounded
            if (_isGrounded) return;

            // Apply enhanced gravity when falling
            if (_rb.linearVelocity.y < 0f)
            {
                float additionalGravity = Physics.gravity.y * (fallGravityMultiplier - 1f);
                _rb.AddForce(Vector3.up * additionalGravity, ForceMode.Acceleration);
            }
        }

        #region Debug Visualization
        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                // Draw ground check
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }

            if (_capsuleCollider != null)
            {
                // Draw ceiling check
                Vector3 rayStart = transform.position + Vector3.up * (_capsuleCollider.height * 0.5f);
                Gizmos.color = _headHitCeiling ? Color.yellow : Color.blue;
                Gizmos.DrawRay(rayStart, Vector3.up * ceilingCheckDistance);
            }
        }
        #endregion
    }
}