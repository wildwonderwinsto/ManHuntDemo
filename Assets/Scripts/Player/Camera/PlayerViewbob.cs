using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Dynamic viewbob system that responds to player movement, sprint state, turning, and jumping.
    /// Provides immersive camera feedback similar to modern FPS games.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// JITTER FIX: Uses LateUpdate() to apply bob as final camera adjustment.
    /// 
    /// ARCHITECTURE:
    /// Attach to the Camera object (child of CameraHolder).
    /// </summary>
    public class PlayerViewBob : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the player's Rigidbody to detect velocity.")]
        public Rigidbody playerRigidbody;

        [Tooltip("Reference to the PlayerInput component.")]
        public PlayerInput playerInput;

        [Tooltip("Reference to the ground check transform (to detect landing).")]
        public Transform groundCheck;

        [Tooltip("Ground detection layer mask.")]
        public LayerMask groundMask;

        [Tooltip("Ground detection distance.")]
        public float groundDistance = 0.4f;

        [Header("Walking Bob Settings")]
        [Tooltip("Vertical bob amount when walking (up/down motion).")]
        [Range(0f, 0.15f)]
        public float walkBobAmount = 0.05f;

        [Tooltip("Horizontal bob amount when walking (side-to-side sway).")]
        [Range(0f, 0.1f)]
        public float walkBobSwayAmount = 0.03f;

        [Tooltip("Bob frequency when walking (how fast the bob cycles).")]
        [Range(8f, 20f)]
        public float walkBobSpeed = 14f;

        [Header("Sprinting Bob Settings")]
        [Tooltip("Vertical bob amount when sprinting.")]
        [Range(0f, 0.25f)]
        public float sprintBobAmount = 0.08f;

        [Tooltip("Horizontal bob amount when sprinting.")]
        [Range(0f, 0.15f)]
        public float sprintBobSwayAmount = 0.05f;

        [Tooltip("Bob frequency when sprinting.")]
        [Range(15f, 25f)]
        public float sprintBobSpeed = 18f;

        [Header("Turning Bob Settings")]
        [Tooltip("Vertical bob amount when turning (up/down motion while looking around).")]
        [Range(0f, 0.15f)]
        public float turnBobAmount = 0.03f;

        [Tooltip("Horizontal sway amount when turning (side-to-side tilt during mouse look).")]
        [Range(0f, 0.15f)]
        public float turnSwayAmount = 0.04f;

        [Tooltip("Bob frequency when turning (how fast the bob cycles during turning).")]
        [Range(8f, 25f)]
        public float turnBobSpeed = 12f;

        [Tooltip("How quickly turn bob responds to mouse input (responsiveness).")]
        [Range(1f, 20f)]
        public float turnResponseSpeed = 10f;

        [Tooltip("Minimum mouse delta to trigger turn bob (prevents tiny movements from bobbing).")]
        [Range(0f, 2f)]
        public float minTurnThreshold = 0.5f;

        [Tooltip("Mouse delta at which turn bob reaches maximum intensity.")]
        [Range(3f, 15f)]
        public float maxTurnThreshold = 8f;

        [Header("Jump & Landing Settings")]
        [Tooltip("Landing impact intensity (camera dips down on landing).")]
        [Range(0f, 0.3f)]
        public float landingImpactAmount = 0.15f;

        [Tooltip("How quickly the camera recovers from landing impact.")]
        [Range(1f, 10f)]
        public float landingRecoverySpeed = 5f;

        [Tooltip("Minimum fall velocity to trigger landing impact (prevents tiny jumps).")]
        [Range(0f, 5f)]
        public float minFallVelocityForImpact = 2f;

        [Header("Smoothing")]
        [Tooltip("How quickly bob transitions between idle and moving.")]
        [Range(1f, 15f)]
        public float bobTransitionSpeed = 10f;

        [Header("Speed Thresholds")]
        [Tooltip("Minimum speed to start bobbing (prevents bob when barely moving).")]
        [Range(0f, 2f)]
        public float minSpeedThreshold = 1f;

        [Tooltip("Speed at which sprint bob kicks in.")]
        [Range(8f, 15f)]
        public float sprintSpeedThreshold = 11f;

        [Header("Debug")]
        [Tooltip("Show debug information in console.")]
        public bool showDebugInfo = false;

        // Private variables
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;

        private Vector3 _defaultCameraPosition;
        private float _bobTimer = 0f;
        private float _turnBobTimer = 0f;
        private float _currentBobIntensity = 0f;
        private float _currentTurnIntensity = 0f;
        private float _turnSwayOffset = 0f;
        private float _landingOffset = 0f;

        // Ground state tracking
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private float _fallVelocity = 0f;

        private void Awake()
        {
            // Validation
            if (playerRigidbody == null)
            {
                Debug.LogError("PlayerViewBob: No Rigidbody assigned!");
                enabled = false;
                return;
            }

            if (playerInput == null)
            {
                Debug.LogError("PlayerViewBob: No PlayerInput assigned!");
                enabled = false;
                return;
            }

            if (groundCheck == null)
            {
                Debug.LogWarning("PlayerViewBob: No groundCheck assigned. Landing detection will be disabled.");
            }

            // Get input actions
            _moveAction = playerInput.actions["Move"];
            _lookAction = playerInput.actions["Look"];
            _sprintAction = playerInput.actions["Sprint"];

            if (_moveAction == null || _lookAction == null || _sprintAction == null)
            {
                Debug.LogError("PlayerViewBob: Could not find required input actions!");
                enabled = false;
                return;
            }

            // Store default camera position
            _defaultCameraPosition = transform.localPosition;
        }

        // JITTER FIX: Changed from Update() to LateUpdate()
        // View bob should be the LAST thing to affect camera position each frame
        // This ensures it applies AFTER all rotation systems have completed
        private void LateUpdate()
        {
            DetectGroundState();
            CalculateViewBob();
            ApplyViewBob();
        }

        /// <summary>
        /// Detects if the player is grounded and handles landing impact.
        /// </summary>
        private void DetectGroundState()
        {
            if (groundCheck == null) return;

            _wasGroundedLastFrame = _isGrounded;
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            // Track vertical velocity for landing impact
            float currentVerticalVelocity = playerRigidbody.linearVelocity.y;

            // Detect landing (transition from air to ground)
            if (_isGrounded && !_wasGroundedLastFrame)
            {
                // Player just landed
                // Check if we were falling fast enough to trigger impact
                if (_fallVelocity < -minFallVelocityForImpact)
                {
                    // Apply landing impact (proportional to fall speed)
                    float impactStrength = Mathf.Clamp01(Mathf.Abs(_fallVelocity) / 10f);
                    _landingOffset = -landingImpactAmount * impactStrength;

                    if (showDebugInfo)
                    {
                        Debug.Log($"Landing Impact: {_landingOffset:F3} (Fall Velocity: {_fallVelocity:F2})");
                    }
                }
            }

            // Store fall velocity when in air
            if (!_isGrounded)
            {
                _fallVelocity = currentVerticalVelocity;
            }
            else
            {
                _fallVelocity = 0f;
            }
        }

        /// <summary>
        /// Calculates the viewbob based on player movement, sprint state, and turning.
        /// </summary>
        private void CalculateViewBob()
        {
            // Get current speed (horizontal velocity only)
            Vector3 horizontalVelocity = new Vector3(
                playerRigidbody.linearVelocity.x,
                0f,
                playerRigidbody.linearVelocity.z
            );
            float currentSpeed = horizontalVelocity.magnitude;

            // Get movement input for directional awareness
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();

            // Get look input for turn bob
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();

            // Determine if sprinting
            bool isSprinting = _sprintAction.IsPressed() && currentSpeed >= sprintSpeedThreshold;

            // --- CALCULATE MOVEMENT BOB INTENSITY ---
            float targetBobIntensity = 0f;

            if (_isGrounded && currentSpeed > minSpeedThreshold)
            {
                // Player is moving on ground
                targetBobIntensity = 1f;
            }

            // Smoothly transition movement bob intensity
            _currentBobIntensity = Mathf.Lerp(
                _currentBobIntensity,
                targetBobIntensity,
                bobTransitionSpeed * Time.deltaTime
            );

            // --- UPDATE MOVEMENT BOB TIMER ---
            if (_currentBobIntensity > 0.01f)
            {
                // Determine bob speed based on sprint state
                float bobSpeed = isSprinting ? sprintBobSpeed : walkBobSpeed;

                // Advance timer based on actual speed (scales with movement speed)
                float speedFactor = Mathf.InverseLerp(minSpeedThreshold, sprintSpeedThreshold, currentSpeed);
                _bobTimer += Time.deltaTime * bobSpeed * (0.5f + speedFactor * 0.5f);
            }
            else
            {
                // Reset timer when idle
                _bobTimer = 0f;
            }

            // --- CALCULATE TURN BOB INTENSITY ---
            // IMPORTANT: Turn bob should only apply when MOVING
            // Don't apply turn bob when standing still and just looking around
            float mouseDelta = Mathf.Abs(lookInput.x);

            // Calculate turn intensity based on mouse speed thresholds
            float targetTurnIntensity = 0f;

            // ONLY apply turn bob if player is actually moving (not standing still)
            if (_currentBobIntensity > 0.01f && mouseDelta > minTurnThreshold)
            {
                // Map mouse delta from minTurnThreshold to maxTurnThreshold → 0 to 1
                targetTurnIntensity = Mathf.InverseLerp(minTurnThreshold, maxTurnThreshold, mouseDelta);
                targetTurnIntensity = Mathf.Clamp01(targetTurnIntensity);

                // Scale turn intensity by movement intensity
                // This makes turn bob stronger when moving faster
                targetTurnIntensity *= _currentBobIntensity;
            }

            // Smoothly transition turn intensity
            _currentTurnIntensity = Mathf.Lerp(
                _currentTurnIntensity,
                targetTurnIntensity,
                turnResponseSpeed * Time.deltaTime
            );

            // --- UPDATE TURN BOB TIMER ---
            if (_currentTurnIntensity > 0.01f)
            {
                // Advance turn bob timer
                _turnBobTimer += Time.deltaTime * turnBobSpeed;
            }
            else
            {
                // Reset timer when not turning
                _turnBobTimer = 0f;
            }

            // --- CALCULATE TURN SWAY OFFSET ---
            // This is the directional sway (left/right) based on turn direction
            float turnDirection = Mathf.Sign(lookInput.x);
            float targetTurnSway = turnDirection * turnSwayAmount * _currentTurnIntensity;

            _turnSwayOffset = Mathf.Lerp(
                _turnSwayOffset,
                targetTurnSway,
                turnResponseSpeed * Time.deltaTime
            );

            // --- SMOOTH LANDING RECOVERY ---
            // Gradually return to zero after landing impact
            _landingOffset = Mathf.Lerp(
                _landingOffset,
                0f,
                landingRecoverySpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Applies the calculated viewbob to the camera position.
        /// </summary>
        private void ApplyViewBob()
        {
            // Determine movement bob amounts based on sprint state
            bool isSprinting = _sprintAction.IsPressed();
            float verticalBobAmount = isSprinting ? sprintBobAmount : walkBobAmount;
            float horizontalBobAmount = isSprinting ? sprintBobSwayAmount : walkBobSwayAmount;

            // --- CALCULATE MOVEMENT BOB ---
            // Calculate vertical bob using sine wave
            float verticalMovementBob = Mathf.Sin(_bobTimer) * verticalBobAmount * _currentBobIntensity;

            // Calculate horizontal sway using cosine wave (phase-shifted from vertical)
            // Cosine creates a figure-8 motion when combined with sine
            float horizontalMovementBob = Mathf.Cos(_bobTimer * 0.5f) * horizontalBobAmount * _currentBobIntensity;

            // --- CALCULATE TURN BOB ---
            // Vertical component when turning (subtle up/down motion)
            float verticalTurnBob = Mathf.Sin(_turnBobTimer) * turnBobAmount * _currentTurnIntensity;

            // --- COMBINE ALL EFFECTS ---
            // Combine vertical bobs (movement + turning)
            float totalVerticalOffset = verticalMovementBob + verticalTurnBob + _landingOffset;

            // Combine horizontal sways (movement + turning directional sway)
            float totalHorizontalOffset = horizontalMovementBob + _turnSwayOffset;

            // Apply all offsets to camera position
            Vector3 targetPosition = _defaultCameraPosition;
            targetPosition.y += totalVerticalOffset;
            targetPosition.x += totalHorizontalOffset;

            // Set camera position
            transform.localPosition = targetPosition;

            // Debug output
            if (showDebugInfo && (_currentBobIntensity > 0.01f || _currentTurnIntensity > 0.01f))
            {
                Debug.Log($"ViewBob - Movement[V:{verticalMovementBob:F3} H:{horizontalMovementBob:F3}] Turn[V:{verticalTurnBob:F3} Sway:{_turnSwayOffset:F3}] Intensity[Move:{_currentBobIntensity:F2} Turn:{_currentTurnIntensity:F2}]");
            }
        }

        #region Editor Helpers
        /// <summary>
        /// Reset camera position when script is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (transform != null)
            {
                transform.localPosition = _defaultCameraPosition;
            }
        }

        /// <summary>
        /// Visualize ground check in scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;

            // Draw ground check sphere
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        #endregion
    }
}