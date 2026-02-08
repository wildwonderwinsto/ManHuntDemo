using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Handles horizontal body rotation (Y-axis) based on Mouse X input.
    /// Works in conjunction with CameraVerticalLook (X-axis) for full FPS look controls.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerBodyRotation : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Horizontal mouse sensitivity.")]
        [Range(1f, 100f)]
        public float sensitivityX = 25f;

        [Tooltip("Smooth rotation to reduce jitter.")]
        public bool smoothRotation = false;

        [Tooltip("Smoothing speed.")]
        [Range(1f, 30f)]
        public float smoothSpeed = 20f;

        // Internal state
        private float _currentYRotation;
        private float _targetYRotation;

        // Components
        private PlayerInput _playerInput;
        private InputAction _lookAction;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();

            if (_playerInput == null)
            {
                Debug.LogError("PlayerBodyRotation: No PlayerInput found!");
                enabled = false;
                return;
            }

            // Cache the Look action
            _lookAction = _playerInput.actions["Look"];

            // Initialize rotation to current transform
            _currentYRotation = transform.localEulerAngles.y;
            _targetYRotation = _currentYRotation;
        }

        private void Update()
        {
            HandleRotation();
        }

        private void HandleRotation()
        {
            if (_lookAction == null) return;

            // Read Mouse X input
            float mouseX = _lookAction.ReadValue<Vector2>().x;

            // Calculate rotation amount
            float rotationAmount = mouseX * sensitivityX * Time.deltaTime;

            if (smoothRotation)
            {
                _targetYRotation += rotationAmount;
                _currentYRotation = Mathf.Lerp(_currentYRotation, _targetYRotation, smoothSpeed * Time.deltaTime);
            }
            else
            {
                _currentYRotation += rotationAmount;
                _targetYRotation = _currentYRotation; // Keep synced
            }

            // Apply rotation to the body (Y-axis only)
            transform.localRotation = Quaternion.Euler(0f, _currentYRotation, 0f);
        }

        /// <summary>
        /// Public API for diagnostics and other scripts
        /// </summary>
        public float GetCurrentYRotation()
        {
            return _currentYRotation;
        }
    }
}