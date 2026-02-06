using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Handles horizontal player body rotation (Y-axis).
    /// Optimized with input caching and debug visualization.
    /// Compatible with Unity 6 LTS (6000.3.6f1).
    /// JITTER FIX: Uses LateUpdate() to sync with camera systems.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerBodyRotation : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Horizontal mouse sensitivity.")]
        [Range(1f, 100f)]
        public float sensitivityX = 25f;

        [Tooltip("Smooth rotation instead of instant (good for controller input).")]
        public bool smoothRotation = false;

        [Tooltip("Rotation smoothing speed (only used if smoothRotation is enabled).")]
        [Range(1f, 30f)]
        public float rotationSmoothSpeed = 15f;

        [Header("Debug")]
        [Tooltip("Show current rotation angle in console.")]
        public bool showDebugInfo = false;

        [Header("Runtime Info (Read-Only)")]
        [Tooltip("Current Y rotation of the player.")]
        [SerializeField] private float _currentYRotation;

        // Cached references
        private PlayerInput _playerInput;
        private InputAction _lookAction;

        // Rotation state
        private float _targetYRotation;

        private void Awake()
        {
            // Cache PlayerInput component
            _playerInput = GetComponent<PlayerInput>();

            if (_playerInput == null)
            {
                Debug.LogError("PlayerBodyRotation: No PlayerInput component found!");
                enabled = false;
                return;
            }

            // Cache Look action
            _lookAction = _playerInput.actions["Look"];

            if (_lookAction == null)
            {
                Debug.LogError("PlayerBodyRotation: 'Look' action not found in Input Actions!");
                enabled = false;
                return;
            }

            // Initialize rotation values
            _currentYRotation = transform.eulerAngles.y;
            _targetYRotation = _currentYRotation;

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // JITTER FIX: Changed from Update() to LateUpdate()
        // This ensures body rotation happens in sync with camera rotation systems
        private void LateUpdate()
        {
            HandleHorizontalRotation();
            UpdateDebugInfo();
        }

        /// <summary>
        /// Handles horizontal (Y-axis) rotation of the player body.
        /// </summary>
        private void HandleHorizontalRotation()
        {
            // Read input (cached action reference - no repeated string lookups)
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();
            float mouseX = lookInput.x * sensitivityX * Time.deltaTime;

            if (smoothRotation)
            {
                // Smooth rotation (good for controllers or cinematic feel)
                _targetYRotation += mouseX;
                _currentYRotation = Mathf.LerpAngle(_currentYRotation, _targetYRotation, rotationSmoothSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, _currentYRotation, 0f);
            }
            else
            {
                // Instant rotation (standard FPS feel)
                transform.Rotate(Vector3.up * mouseX);
                _currentYRotation = transform.eulerAngles.y;
            }
        }

        /// <summary>
        /// Updates debug information for inspector display.
        /// </summary>
        private void UpdateDebugInfo()
        {
            if (showDebugInfo && Mathf.Abs(_lookAction.ReadValue<Vector2>().x) > 0.01f)
            {
                Debug.Log($"Player Y Rotation: {_currentYRotation:F1}°");
            }
        }

        #region Public API
        /// <summary>
        /// Gets the current Y rotation of the player.
        /// </summary>
        public float GetCurrentYRotation() => _currentYRotation;

        /// <summary>
        /// Sets the player's Y rotation directly (useful for teleports/cutscenes).
        /// </summary>
        public void SetYRotation(float angle)
        {
            _currentYRotation = angle;
            _targetYRotation = angle;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
        #endregion
    }
}