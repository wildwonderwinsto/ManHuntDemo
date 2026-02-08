using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Game.Player
{
    /// <summary>
    /// Server-authoritative player movement with client-side input.
    /// 
    /// CRITICAL FIX v2:
    /// - Script runs on OWNER (for input reading)
    /// - Physics only applied on SERVER
    /// - Uses NetworkRigidbody for automatic kinematic handling
    /// 
    /// ARCHITECTURE:
    /// - Owner: Reads input, sends to server via ServerRpc
    /// - Server: Validates and applies physics
    /// - NetworkRigidbody: Automatically manages kinematic state
    /// - NetworkTransform: Syncs position/rotation to all clients
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(PlayerInput), typeof(CapsuleCollider))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Base walking speed")]
        public float speed = 8f;

        [Tooltip("Sprint speed (activated by Sprint key)")]
        public float sprintSpeed = 12f;

        [Tooltip("Maximum movement speed (prevents exploits)")]
        public float maxSpeed = 15f;

        [Header("Jump Settings")]
        [Tooltip("Upward force applied when jumping")]
        public float jumpForce = 12f;

        [Tooltip("Multiplier for downward gravity (makes falling feel better)")]
        public float fallGravityMultiplier = 2.5f;

        [Tooltip("Grace period after leaving ground where jump still works")]
        public float coyoteTime = 0.15f;

        [Header("Ground Detection")]
        [Tooltip("Transform at player's feet for ground detection")]
        public Transform groundCheck;

        [Tooltip("Radius of ground check sphere")]
        public float groundDistance = 0.4f;

        [Tooltip("What layers count as ground")]
        public LayerMask groundMask;

        [Header("Anti-Cheat Settings")]
        [Tooltip("Maximum velocity allowed (prevents speed hacks)")]
        public float maxVelocityMagnitude = 20f;

        [Tooltip("Maximum distance player can move per frame (prevents teleport hacks)")]
        public float maxMoveDistancePerFrame = 2f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Components
        private Rigidbody _rb;
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        // Ground state
        private bool _isGrounded;
        private float _lastGroundedTime;

        // Server-side movement state
        private Vector2 _currentInputVector;
        private bool _isSprinting;
        private Vector3 _lastValidPosition;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();

            // Cache input actions
            _moveAction = _playerInput.actions["Move"];
            _jumpAction = _playerInput.actions["Jump"];
            _sprintAction = _playerInput.actions["Sprint"];

            // Ensure we have a ground check
            if (groundCheck == null)
            {
                GameObject checkObj = new GameObject("GroundCheck_Auto");
                checkObj.transform.parent = transform;
                checkObj.transform.localPosition = new Vector3(0, -0.9f, 0);
                groundCheck = checkObj.transform;
                Debug.LogWarning("[PlayerMovement] No ground check assigned. Auto-created one.");
            }

            // Configure Rigidbody
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent physics rotation
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Better collision

            // NOTE: isKinematic is now handled by NetworkRigidbody component!
            // Do NOT manually set _rb.isKinematic here
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // CRITICAL FIX: Don't manually set isKinematic - NetworkRigidbody handles this!
            // Just log the state for debugging
            if (IsServer)
            {
                _lastValidPosition = transform.position;
                LogDebug($"Server: Physics authority for client {OwnerClientId}");
            }

            LogDebug($"OnNetworkSpawn - IsOwner: {IsOwner}, IsServer: {IsServer}, Kinematic: {_rb.isKinematic}");
        }

        private void Update()
        {
            // CRITICAL FIX: Run on OWNER for input (not just server!)
            // Owner reads input and sends to server
            if (IsOwner)
            {
                HandleClientInput();
            }

            // SERVER: Ground detection (for jump validation)
            if (IsServer)
            {
                DetectGroundState();
            }
        }

        private void FixedUpdate()
        {
            // SERVER ONLY: Apply physics
            if (IsServer)
            {
                ApplyServerMovement();
                ApplyFallGravity();
                ValidateMovement(); // Anti-cheat
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CLIENT INPUT HANDLING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Owner reads input and sends to server (runs on client)
        /// </summary>
        private void HandleClientInput()
        {
            // Read movement input
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            bool sprintInput = _sprintAction.IsPressed();

            // Send to server (with current rotation for movement direction)
            SubmitInputServerRpc(moveInput, sprintInput, transform.rotation);

            // Handle jump (separate RPC for instant response)
            if (_jumpAction.WasPerformedThisFrame())
            {
                SubmitJumpServerRpc();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SERVER RPCs (Client → Server)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Client sends movement input and rotation to server
        /// </summary>
        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 input, bool sprint, Quaternion rotation)
        {
            // Store input for FixedUpdate
            _currentInputVector = input;
            _isSprinting = sprint;

            // Apply rotation (horizontal look is handled client-side)
            transform.rotation = rotation;
        }

        /// <summary>
        /// Client requests a jump - Server validates and executes
        /// </summary>
        [ServerRpc]
        private void SubmitJumpServerRpc()
        {
            // Validate jump on server
            bool canJump = _isGrounded || (Time.time - _lastGroundedTime <= coyoteTime);

            if (canJump)
            {
                ExecuteJump();
                LogDebug($"Jump executed for client {OwnerClientId}");
            }
            else
            {
                LogDebug($"Jump denied for client {OwnerClientId} (not grounded)");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SERVER PHYSICS LOGIC
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Server detects ground state for jump validation
        /// </summary>
        private void DetectGroundState()
        {
            bool wasGrounded = _isGrounded;
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (_isGrounded)
            {
                _lastGroundedTime = Time.time;

                if (!wasGrounded)
                {
                    LogDebug($"Player {OwnerClientId} landed");
                }
            }
        }

        /// <summary>
        /// Server applies movement based on client input
        /// </summary>
        private void ApplyServerMovement()
        {
            // Calculate movement direction based on player rotation
            Vector3 direction = transform.right * _currentInputVector.x +
                                transform.forward * _currentInputVector.y;

            // Normalize diagonal movement
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            // Determine target speed
            float targetSpeed = _isSprinting ? sprintSpeed : speed;

            // Apply movement
            Vector3 targetVelocity = direction * targetSpeed;

            // Preserve vertical velocity (don't override gravity/jumps)
            Vector3 currentVel = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(targetVelocity.x, currentVel.y, targetVelocity.z);
        }

        /// <summary>
        /// Server executes jump
        /// </summary>
        private void ExecuteJump()
        {
            // Reset vertical velocity before applying jump
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _rb.linearVelocity = vel;

            // Apply jump force
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        /// <summary>
        /// Server applies stronger gravity when falling
        /// </summary>
        private void ApplyFallGravity()
        {
            if (_isGrounded) return;

            // Apply extra downward force when falling
            if (_rb.linearVelocity.y < 0f)
            {
                float additionalGravity = Physics.gravity.y * (fallGravityMultiplier - 1f);
                _rb.AddForce(Vector3.up * additionalGravity, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Server validates movement to prevent cheating
        /// </summary>
        private void ValidateMovement()
        {
            // Check max velocity
            if (_rb.linearVelocity.magnitude > maxVelocityMagnitude)
            {
                Debug.LogWarning($"[PlayerMovement] Client {OwnerClientId} exceeded max velocity! Clamping.");
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxVelocityMagnitude;
            }

            // Check teleport distance
            float moveDist = Vector3.Distance(transform.position, _lastValidPosition);
            if (moveDist > maxMoveDistancePerFrame)
            {
                Debug.LogWarning($"[PlayerMovement] Client {OwnerClientId} moved {moveDist}m in one frame! Resetting position.");
                transform.position = _lastValidPosition;
                _rb.linearVelocity = Vector3.zero;
            }

            _lastValidPosition = transform.position;
        }

        // ═══════════════════════════════════════════════════════════════
        // DEBUG & VISUALIZATION
        // ═══════════════════════════════════════════════════════════════

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerMovement] {message}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;

            // Visualize ground check
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        public bool IsGrounded() => _isGrounded;
        public bool IsSprinting() => _isSprinting;
        public Vector3 GetVelocity() => _rb.linearVelocity;
    }
}