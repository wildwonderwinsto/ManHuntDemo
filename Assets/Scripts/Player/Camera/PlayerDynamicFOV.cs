using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Dynamically adjusts the camera's field of view based on player movement speed.
    /// Provides a sense of speed and immersion during gameplay.
    /// Only considers horizontal movement - jumping does NOT affect FOV.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerDynamicFOV : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the player's Rigidbody. Drag your Player GameObject here.")]
        public Rigidbody playerRigidbody;

        [Header("FOV Settings")]
        [Tooltip("Base field of view when standing still or moving slowly.")]
        public float baseFOV = 60f;

        [Tooltip("Maximum FOV when sprinting at full speed.")]
        public float sprintFOV = 75f;

        [Tooltip("How quickly the FOV transitions (higher = faster). Recommended: 5-10.")]
        [Range(1f, 20f)]
        public float fovTransitionSpeed = 8f;

        [Header("Speed Thresholds")]
        [Tooltip("Speed at which FOV begins to increase (typically walking speed).")]
        public float minSpeedThreshold = 3f;

        [Tooltip("Speed at which FOV reaches maximum (typically sprint speed).")]
        public float maxSpeedThreshold = 12f;

        [Header("Optional Sprint Detection")]
        [Tooltip("Optionally reference PlayerMovement to detect sprint state for enhanced FOV effect.")]
        public PlayerMovement playerMovement;

        [Tooltip("Additional FOV boost when sprint is active (adds to speed-based FOV).")]
        public float sprintBoost = 5f;

        // Private variables
        private Camera _camera;
        private float _currentFOV;
        private float _targetFOV;

        private void Awake()
        {
            // Cache camera component
            _camera = GetComponent<Camera>();

            // Validation
            if (_camera == null)
            {
                Debug.LogError("PlayerDynamicFOV: No Camera component found! This script must be attached to a camera.");
                enabled = false;
                return;
            }

            if (playerRigidbody == null)
            {
                Debug.LogError("PlayerDynamicFOV: No Rigidbody assigned! Please assign the player's Rigidbody in the inspector.");
                enabled = false;
                return;
            }

            // Initialize FOV to base value
            _currentFOV = baseFOV;
            _camera.fieldOfView = _currentFOV;
        }

        private void LateUpdate()
        {
            CalculateTargetFOV();
            SmoothFOVTransition();
        }

        /// <summary>
        /// Calculates the target FOV based on current player speed.
        /// Only uses horizontal velocity to prevent FOV changes during jumping.
        /// </summary>
        private void CalculateTargetFOV()
        {
            // Get HORIZONTAL speed only (excludes vertical jump velocity)
            // This prevents FOV from changing when jumping or falling
            Vector3 horizontalVelocity = new Vector3(
                playerRigidbody.linearVelocity.x,
                0f,
                playerRigidbody.linearVelocity.z
            );
            float currentSpeed = horizontalVelocity.magnitude;

            // Calculate FOV based on speed
            // When speed is below minSpeedThreshold, use baseFOV
            // When speed is above maxSpeedThreshold, use sprintFOV
            // Linear interpolation between these thresholds
            float speedFactor = Mathf.InverseLerp(minSpeedThreshold, maxSpeedThreshold, currentSpeed);
            _targetFOV = Mathf.Lerp(baseFOV, sprintFOV, speedFactor);

            // Optional: Add extra boost if sprint is detected via PlayerMovement
            if (playerMovement != null && IsPlayerSprinting())
            {
                _targetFOV += sprintBoost;
            }

            // Clamp to reasonable values (prevent extreme FOV)
            _targetFOV = Mathf.Clamp(_targetFOV, baseFOV, sprintFOV + sprintBoost);
        }

        /// <summary>
        /// Smoothly transitions the camera FOV to the target value.
        /// Uses frame-rate independent interpolation.
        /// </summary>
        private void SmoothFOVTransition()
        {
            // Smooth damping for natural feel
            // Using Mathf.Lerp with Time.deltaTime creates an exponential ease-out
            _currentFOV = Mathf.Lerp(_currentFOV, _targetFOV, fovTransitionSpeed * Time.deltaTime);

            // Apply to camera
            _camera.fieldOfView = _currentFOV;
        }

        /// <summary>
        /// Checks if the player is currently sprinting.
        /// Requires PlayerMovement reference and checks the Sprint input action.
        /// </summary>
        /// <returns>True if sprinting, false otherwise.</returns>
        private bool IsPlayerSprinting()
        {
            // Access the sprint action from PlayerMovement
            // This requires the _sprintAction to be accessible or a public property/method
            // Since _sprintAction is private, we'll use a workaround:

            // Check if current velocity is close to sprint speed
            Vector3 horizontalVelocity = new Vector3(
                playerRigidbody.linearVelocity.x,
                0f,
                playerRigidbody.linearVelocity.z
            );
            float currentSpeed = horizontalVelocity.magnitude;

            // If we have access to PlayerMovement, check against sprint speed
            if (playerMovement != null)
            {
                // Sprint is likely active if speed is close to sprintSpeed
                // Add small tolerance for floating point comparison
                return currentSpeed >= (playerMovement.sprintSpeed - 1f);
            }

            return false;
        }

        #region Editor Helpers
        /// <summary>
        /// Validates references and settings in the editor.
        /// </summary>
        private void OnValidate()
        {
            // Ensure logical FOV values
            if (sprintFOV < baseFOV)
            {
                Debug.LogWarning("PlayerDynamicFOV: sprintFOV should be greater than baseFOV for the speed effect to work correctly.");
            }

            // Ensure logical speed thresholds
            if (maxSpeedThreshold < minSpeedThreshold)
            {
                Debug.LogWarning("PlayerDynamicFOV: maxSpeedThreshold should be greater than minSpeedThreshold.");
            }
        }

        #endregion
    }
}