using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Handles vertical camera rotation (X-axis pitch).
    /// Optimized with input caching and debug visualization.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// 
    /// ATTACH TO: Camera GameObject (child of CameraHolder)
    /// </summary>
    public class CameraVerticalLook : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Vertical mouse sensitivity.")]
        [Range(1f, 100f)]
        public float sensitivityY = 25f;

        [Tooltip("Invert vertical look (flight sim style).")]
        public bool invertY = false;

        [Tooltip("Smooth camera rotation (reduces jitter on low framerates).")]
        public bool smoothRotation = false;

        [Tooltip("Camera rotation smoothing speed.")]
        [Range(1f, 30f)]
        public float rotationSmoothSpeed = 20f;

        [Header("Constraints")]
        [Tooltip("Maximum downward look angle.")]
        [Range(-90f, 0f)]
        public float minXRotation = -90f;

        [Tooltip("Maximum upward look angle.")]
        [Range(0f, 90f)]
        public float maxXRotation = 90f;

        [Header("Debug")]
        [Tooltip("Show current pitch angle in console.")]
        public bool showDebugInfo = false;

        [Header("Runtime Info (Read-Only)")]
        [Tooltip("Current X rotation (pitch) of the camera.")]
        [SerializeField] private float _currentXRotation;

        // Cached references
        private PlayerInput _playerInput;
        private InputAction _lookAction;

        // Rotation state
        private float _targetXRotation;

        private void Awake()
        {
            // Find PlayerInput on parent (the Player GameObject)
            _playerInput = GetComponentInParent<PlayerInput>();

            if (_playerInput == null)
            {
                Debug.LogError("CameraVerticalLook: No PlayerInput found in parent! This script must be on Camera (child of CameraHolder → child of Player).");
                enabled = false;
                return;
            }

            // Cache Look action (avoid repeated string lookups)
            _lookAction = _playerInput.actions["Look"];

            if (_lookAction == null)
            {
                Debug.LogError("CameraVerticalLook: 'Look' action not found in Input Actions!");
                enabled = false;
                return;
            }

            // Initialize rotation
            _currentXRotation = transform.localEulerAngles.x;

            // Normalize to -180 to 180 range
            if (_currentXRotation > 180f)
                _currentXRotation -= 360f;

            _targetXRotation = _currentXRotation;
        }

        private void LateUpdate()
        {
            HandleVerticalRotation();
            UpdateDebugInfo();
        }

        /// <summary>
        /// Handles vertical (X-axis) rotation of the camera.
        /// </summary>
        private void HandleVerticalRotation()
        {
            // Read input (cached action reference)
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();
            float mouseY = lookInput.y * sensitivityY * Time.deltaTime;

            // Handle Y-axis inversion
            if (invertY)
                mouseY = -mouseY;

            if (smoothRotation)
            {
                // Smooth rotation
                _targetXRotation -= mouseY;
                _targetXRotation = Mathf.Clamp(_targetXRotation, minXRotation, maxXRotation);
                _currentXRotation = Mathf.LerpAngle(_currentXRotation, _targetXRotation, rotationSmoothSpeed * Time.deltaTime);
            }
            else
            {
                // Instant rotation
                _currentXRotation -= mouseY;
                _currentXRotation = Mathf.Clamp(_currentXRotation, minXRotation, maxXRotation);
            }

            // Apply rotation (local space, relative to CameraHolder)
            transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }

        /// <summary>
        /// Updates debug information for inspector display.
        /// </summary>
        private void UpdateDebugInfo()
        {
            if (showDebugInfo && Mathf.Abs(_lookAction.ReadValue<Vector2>().y) > 0.01f)
            {
                Debug.Log($"Camera X Rotation (Pitch): {_currentXRotation:F1}°");
            }
        }

        #region Public API
        /// <summary>
        /// Gets the current X rotation (pitch) of the camera.
        /// </summary>
        public float GetCurrentPitch() => _currentXRotation;

        /// <summary>
        /// Sets the camera's pitch directly (useful for cutscenes/animations).
        /// </summary>
        public void SetPitch(float angle)
        {
            _currentXRotation = Mathf.Clamp(angle, minXRotation, maxXRotation);
            _targetXRotation = _currentXRotation;
            transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }

        /// <summary>
        /// Check if camera is looking at extreme angles (useful for other systems).
        /// </summary>
        public bool IsLookingUp() => _currentXRotation > 45f;
        public bool IsLookingDown() => _currentXRotation < -45f;
        public bool IsLookingStraight() => Mathf.Abs(_currentXRotation) < 15f;
        #endregion

        #region Editor Helpers
        private void OnValidate()
        {
            // Ensure valid constraint values
            if (minXRotation > maxXRotation)
            {
                Debug.LogWarning("CameraVerticalLook: minXRotation should be less than maxXRotation!");
            }
        }
        #endregion
    }
}