using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Dynamically tilts the camera holder based on player movement (WASD) and mouse look direction.
    /// OPTIMIZED: Cached references, reduced lookups, performance improvements.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// JITTER FIX: Uses LateUpdate() to sync with camera systems.
    /// 
    /// ATTACH TO: CameraHolder GameObject
    /// </summary>
    public class PlayerCameraTilt : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the PlayerInput component on the Player.")]
        public PlayerInput playerInput;

        [Header("Strafe Tilt Settings (Left/Right)")]
        [Tooltip("Maximum tilt angle when strafing left or right (in degrees).")]
        [Range(0f, 15f)]
        public float maxStrafeTilt = 8f;

        [Tooltip("How quickly the camera tilts when strafing.")]
        [Range(1f, 20f)]
        public float strafeTiltSpeed = 8f;

        [Header("Forward/Backward Tilt Settings")]
        [Tooltip("Maximum tilt angle when moving forward (camera tilts up slightly).")]
        [Range(0f, 10f)]
        public float maxForwardTilt = 4f;

        [Tooltip("Maximum tilt angle when moving backward (camera tilts down slightly).")]
        [Range(0f, 10f)]
        public float maxBackwardTilt = 3f;

        [Tooltip("How quickly the camera tilts when moving forward/backward.")]
        [Range(1f, 20f)]
        public float forwardTiltSpeed = 6f;

        [Header("Mouse Look Tilt Settings")]
        [Tooltip("Maximum tilt when looking left or right with mouse (enhances turning feel).")]
        [Range(0f, 10f)]
        public float maxMouseLookTilt = 5f;

        [Tooltip("How quickly the camera tilts based on mouse movement.")]
        [Range(1f, 20f)]
        public float mouseLookTiltSpeed = 12f;

        [Header("Return to Neutral Settings")]
        [Tooltip("How quickly the camera returns to neutral position when no input is detected.")]
        [Range(1f, 15f)]
        public float returnToNeutralSpeed = 10f;

        [Header("Gimbal Lock Prevention")]
        [Tooltip("At what pitch angle (up/down look) should tilt start reducing? (degrees from horizontal)")]
        [Range(60f, 85f)]
        public float tiltReductionStartAngle = 70f;

        [Tooltip("At what pitch angle should tilt be completely disabled?")]
        [Range(75f, 90f)]
        public float tiltDisableAngle = 85f;

        [Header("Debug")]
        [Tooltip("Show debug logs of current tilt values in the console.")]
        public bool showDebugInfo = false;

        [Header("Runtime Info (Read-Only)")]
        [SerializeField] private float _totalTilt;
        [SerializeField] private float _pitchDampening;

        // Cached references
        private InputAction _moveAction;
        private InputAction _lookAction;
        private Transform _cameraTransform;
        private CameraVerticalLook _cameraVerticalLook;

        // Current tilt values
        private float _currentStrafeTilt = 0f;
        private float _currentForwardTilt = 0f;
        private float _currentLookTilt = 0f;

        // Optimization: Cache small input threshold check
        private const float INPUT_THRESHOLD = 0.01f;
        private const float LOOK_THRESHOLD = 0.5f;

        private void Awake()
        {
            // Validation
            if (playerInput == null)
            {
                Debug.LogError("PlayerCameraTilt: No PlayerInput assigned!");
                enabled = false;
                return;
            }

            // Cache input actions (avoid repeated string lookups)
            _moveAction = playerInput.actions["Move"];
            _lookAction = playerInput.actions["Look"];

            if (_moveAction == null || _lookAction == null)
            {
                Debug.LogError("PlayerCameraTilt: Could not find 'Move' or 'Look' input actions!");
                enabled = false;
                return;
            }

            // Cache camera transform
            _cameraTransform = GetComponentInChildren<Camera>()?.transform;
            if (_cameraTransform == null)
            {
                Debug.LogWarning("PlayerCameraTilt: No Camera found in children. Pitch-based tilt dampening will be disabled.");
            }
            else
            {
                // Cache CameraVerticalLook for more accurate pitch reading
                _cameraVerticalLook = _cameraTransform.GetComponent<CameraVerticalLook>();
            }
        }

        // JITTER FIX: Changed from Update() to LateUpdate()
        // This ensures tilt is calculated AFTER body rotation but BEFORE camera look
        private void LateUpdate()
        {
            CalculateAndApplyTilt();
        }

        /// <summary>
        /// Calculates tilt based on input and applies it to the CameraHolder transform.
        /// OPTIMIZED: Reduced calculations, cached values.
        /// </summary>
        private void CalculateAndApplyTilt()
        {
            // Cache input values (read once per frame)
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();

            // --- CALCULATE PITCH DAMPENING FACTOR ---
            _pitchDampening = CalculatePitchDampening();

            // --- CALCULATE TARGET TILTS ---

            // STRAFE TILT (Left/Right = Z-axis Roll)
            float targetStrafeTilt = -moveInput.x * maxStrafeTilt * _pitchDampening;

            // FORWARD/BACKWARD TILT (W/S = X-axis Pitch offset)
            float targetForwardTilt = 0f;
            if (moveInput.y > 0f) // Moving forward
            {
                targetForwardTilt = moveInput.y * maxForwardTilt;
            }
            else if (moveInput.y < 0f) // Moving backward
            {
                targetForwardTilt = moveInput.y * maxBackwardTilt;
            }

            // MOUSE LOOK TILT (Turning = Z-axis Roll)
            float mouseDelta = Mathf.Clamp(lookInput.x, -10f, 10f);
            float targetLookTilt = -mouseDelta * maxMouseLookTilt * 0.1f * _pitchDampening;

            // --- SMOOTH TRANSITIONS ---
            float deltaTime = Time.deltaTime; // Cache deltaTime

            _currentStrafeTilt = Mathf.Lerp(_currentStrafeTilt, targetStrafeTilt, strafeTiltSpeed * deltaTime);
            _currentForwardTilt = Mathf.Lerp(_currentForwardTilt, targetForwardTilt, forwardTiltSpeed * deltaTime);
            _currentLookTilt = Mathf.Lerp(_currentLookTilt, targetLookTilt, mouseLookTiltSpeed * deltaTime);

            // Return to neutral when no input (optimized condition check)
            if (moveInput.sqrMagnitude < INPUT_THRESHOLD && Mathf.Abs(lookInput.x) < LOOK_THRESHOLD)
            {
                float neutralSpeed = returnToNeutralSpeed * deltaTime;
                _currentStrafeTilt = Mathf.Lerp(_currentStrafeTilt, 0f, neutralSpeed);
                _currentForwardTilt = Mathf.Lerp(_currentForwardTilt, 0f, neutralSpeed);
                _currentLookTilt = Mathf.Lerp(_currentLookTilt, 0f, neutralSpeed);
            }

            // --- APPLY TILT TO CAMERA HOLDER ---

            // Combine roll tilts
            float finalRoll = _currentStrafeTilt + _currentLookTilt;
            _totalTilt = finalRoll; // For debug display

            // Apply rotation
            transform.localRotation = Quaternion.Euler(_currentForwardTilt, 0f, finalRoll);

            // Debug output
            if (showDebugInfo && (Mathf.Abs(finalRoll) > 0.1f || Mathf.Abs(_currentForwardTilt) > 0.1f))
            {
                Debug.Log($"CameraHolder Tilt - Roll: {finalRoll:F2}° | Pitch: {_currentForwardTilt:F2}° | Dampening: {_pitchDampening:F2}");
            }
        }

        /// <summary>
        /// Calculates pitch dampening to prevent gimbal lock at extreme angles.
        /// OPTIMIZED: Uses cached CameraVerticalLook reference if available.
        /// </summary>
        private float CalculatePitchDampening()
        {
            if (_cameraTransform == null) return 1f;

            float cameraPitch;

            // OPTIMIZED: Use CameraVerticalLook's cached pitch if available
            if (_cameraVerticalLook != null)
            {
                cameraPitch = _cameraVerticalLook.GetCurrentPitch();
            }
            else
            {
                // Fallback: Read from transform
                cameraPitch = _cameraTransform.localEulerAngles.x;
                if (cameraPitch > 180f) cameraPitch -= 360f;
            }

            // Get absolute pitch
            float absPitch = Mathf.Abs(cameraPitch);

            // Calculate dampening
            if (absPitch > tiltReductionStartAngle)
            {
                return 1f - Mathf.InverseLerp(tiltReductionStartAngle, tiltDisableAngle, absPitch);
            }

            return 1f;
        }

        #region Public API
        /// <summary>
        /// Get current total tilt amount (for debugging or other systems).
        /// </summary>
        public float GetCurrentTilt() => _totalTilt;

        /// <summary>
        /// Check if tilt is active.
        /// </summary>
        public bool IsTilting() => Mathf.Abs(_totalTilt) > 0.1f;
        #endregion

        #region Editor Helpers
        private void OnValidate()
        {
            if (maxStrafeTilt > 15f)
            {
                Debug.LogWarning("PlayerCameraTilt: maxStrafeTilt is quite high. Excessive tilt can cause motion sickness.");
            }

            if (tiltDisableAngle <= tiltReductionStartAngle)
            {
                Debug.LogWarning("PlayerCameraTilt: tiltDisableAngle should be greater than tiltReductionStartAngle!");
            }
        }
        #endregion
    }
}