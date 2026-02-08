using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerInput), typeof(CapsuleCollider))]
    public class PlayerMovement : NetworkBehaviour // Changed from MonoBehaviour
    {
        [Header("Movement Settings")]
        public float speed = 8f;
        public float sprintSpeed = 12f;

        [Header("Jump Settings")]
        public float jumpForce = 12f;
        public float fallGravityMultiplier = 2.5f;
        public float coyoteTime = 0.15f;

        [Header("Ground Detection")]
        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask;

        // Components
        private Rigidbody _rb;
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        // State
        private bool _isGrounded;
        private float _lastGroundedTime;

        // SERVER AUTHORITY INPUTS
        // These variables hold the input sent from the Client
        private Vector2 _currentInputVector;
        private bool _isSprinting;
        private bool _jumpRequested;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();

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
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Server controls physics
                _rb.isKinematic = false;
            }
            else
            {
                // Client does NOT control physics (NetworkTransform handles position)
                _rb.isKinematic = true;
            }
        }

        private void Update()
        {
            // 1. Client Side: Read Input and Send to Server
            if (IsOwner)
            {
                HandleClientInput();
            }

            // 2. Server Side: Logic that doesn't need physics (like timers)
            if (IsServer)
            {
                DetectGroundState();
            }
        }

        private void FixedUpdate()
        {
            // 3. Server Side: Apply Physics
            if (IsServer)
            {
                ApplyServerMovement();
                ApplyFallGravity();
            }
        }

        // --- CLIENT LOGIC ---

        private void HandleClientInput()
        {
            // Read raw input
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            bool sprintInput = _sprintAction.IsPressed();

            // Send to server
            // We verify inputs haven't changed drastically to save bandwidth (optional optimization omitted for simplicity)
            SubmitInputServerRpc(moveInput, sprintInput, transform.rotation);

            // Handle Jump separately (trigger)
            if (_jumpAction.WasPerformedThisFrame())
            {
                SubmitJumpServerRpc();
            }
        }

        // --- SERVER RPCs ---

        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 input, bool sprint, Quaternion rotation)
        {
            _currentInputVector = input;
            _isSprinting = sprint;

            // Server updates the rotation based on client look
            // (You might want to smooth this on the server side)
            transform.rotation = rotation;
        }

        [ServerRpc]
        private void SubmitJumpServerRpc()
        {
            // Validate jump on server
            if (_isGrounded || (Time.time - _lastGroundedTime <= coyoteTime))
            {
                ExecuteJump();
            }
        }

        // --- SERVER PHYSICS LOGIC ---

        private void DetectGroundState()
        {
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            if (_isGrounded) _lastGroundedTime = Time.time;
        }

        private void ApplyServerMovement()
        {
            // Calculate direction based on current rotation
            Vector3 direction = transform.right * _currentInputVector.x + transform.forward * _currentInputVector.y;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            float targetSpeed = _isSprinting ? sprintSpeed : speed;
            Vector3 targetVelocity = direction * targetSpeed;

            // Apply velocity to Rigidbody
            Vector3 currentVel = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(targetVelocity.x, currentVel.y, targetVelocity.z);
        }

        private void ExecuteJump()
        {
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _rb.linearVelocity = vel;
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        private void ApplyFallGravity()
        {
            if (_isGrounded) return;
            if (_rb.linearVelocity.y < 0f)
            {
                float additionalGravity = Physics.gravity.y * (fallGravityMultiplier - 1f);
                _rb.AddForce(Vector3.up * additionalGravity, ForceMode.Acceleration);
            }
        }
    }
}