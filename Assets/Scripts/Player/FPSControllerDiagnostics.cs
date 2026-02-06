using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Performance monitor and diagnostics for the FPS controller system.
    /// Shows frame timing, input lag, and component status.
    /// ATTACH TO: Player GameObject
    /// </summary>
    public class FPSControllerDiagnostics : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Show on-screen diagnostics overlay.")]
        public bool showOnScreenStats = true;

        [Tooltip("Show detailed console logging.")]
        public bool showConsoleStats = false;

        [Tooltip("Update frequency in seconds (lower = more frequent updates).")]
        [Range(0.1f, 2f)]
        public float updateInterval = 0.5f;

        [Header("Component References (Auto-Found)")]
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerBodyRotation _bodyRotation;
        [SerializeField] private CameraVerticalLook _cameraLook;
        [SerializeField] private PlayerCameraTilt _cameraTilt;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerViewBob _viewBob;
        [SerializeField] private PlayerDynamicFOV _dynamicFOV;

        // Performance tracking
        private float _deltaTime;
        private float _updateTimer;
        private int _frameCount;
        private float _fps;

        // Input tracking
        private Vector2 _lastMoveInput;
        private Vector2 _lastLookInput;

        private GUIStyle _guiStyle;

        private void Awake()
        {
            // Auto-find all components
            _playerInput = GetComponent<PlayerInput>();
            _bodyRotation = GetComponent<PlayerBodyRotation>();
            _movement = GetComponent<PlayerMovement>();

            Transform cameraHolder = transform.Find("CameraHolder");
            if (cameraHolder != null)
            {
                _cameraTilt = cameraHolder.GetComponent<PlayerCameraTilt>();
                Camera cam = cameraHolder.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    _cameraLook = cam.GetComponent<CameraVerticalLook>();
                    _viewBob = cam.GetComponent<PlayerViewBob>();
                    _dynamicFOV = cam.GetComponent<PlayerDynamicFOV>();
                }
            }
        }

        private void Update()
        {
            // Track frame timing
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _frameCount++;
            _updateTimer += Time.unscaledDeltaTime;

            if (_updateTimer >= updateInterval)
            {
                _fps = _frameCount / _updateTimer;
                _frameCount = 0;
                _updateTimer = 0f;

                if (showConsoleStats)
                {
                    LogDiagnostics();
                }
            }

            // Track input
            if (_playerInput != null)
            {
                _lastMoveInput = _playerInput.actions["Move"].ReadValue<Vector2>();
                _lastLookInput = _playerInput.actions["Look"].ReadValue<Vector2>();
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenStats) return;

            // Initialize GUI style
            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle(GUI.skin.label);
                _guiStyle.fontSize = 14;
                _guiStyle.normal.textColor = Color.white;
                _guiStyle.alignment = TextAnchor.UpperLeft;
            }

            // Create background
            int lineHeight = 18;
            int lines = 12;
            GUI.Box(new Rect(10, 10, 350, lineHeight * lines + 10), "");

            // Display stats
            int y = 15;
            GUI.Label(new Rect(15, y, 340, lineHeight), $"<b>FPS Controller Diagnostics</b>", _guiStyle);
            y += lineHeight;

            // Performance
            float msec = _deltaTime * 1000.0f;
            GUI.Label(new Rect(15, y, 340, lineHeight), $"FPS: {_fps:F0} ({msec:F1} ms)", _guiStyle);
            y += lineHeight;

            // Input
            GUI.Label(new Rect(15, y, 340, lineHeight), $"Move Input: ({_lastMoveInput.x:F2}, {_lastMoveInput.y:F2})", _guiStyle);
            y += lineHeight;
            GUI.Label(new Rect(15, y, 340, lineHeight), $"Look Input: ({_lastLookInput.x:F2}, {_lastLookInput.y:F2})", _guiStyle);
            y += lineHeight;

            // Rotation info
            if (_bodyRotation != null)
            {
                GUI.Label(new Rect(15, y, 340, lineHeight), $"Body Y Rotation: {_bodyRotation.GetCurrentYRotation():F1}°", _guiStyle);
                y += lineHeight;
            }

            if (_cameraLook != null)
            {
                GUI.Label(new Rect(15, y, 340, lineHeight), $"Camera Pitch: {_cameraLook.GetCurrentPitch():F1}°", _guiStyle);
                y += lineHeight;
            }

            if (_cameraTilt != null)
            {
                GUI.Label(new Rect(15, y, 340, lineHeight), $"Camera Tilt: {_cameraTilt.GetCurrentTilt():F2}° (Active: {_cameraTilt.IsTilting()})", _guiStyle);
                y += lineHeight;
            }

            // Component status
            y += 5;
            GUI.Label(new Rect(15, y, 340, lineHeight), "<b>Component Status:</b>", _guiStyle);
            y += lineHeight;

            string status = GetComponentStatus();
            GUI.Label(new Rect(15, y, 340, lineHeight * 3), status, _guiStyle);
        }

        private void LogDiagnostics()
        {
            float msec = _deltaTime * 1000.0f;
            Debug.Log($"[FPS Controller] FPS: {_fps:F0} | Frame Time: {msec:F1}ms | Input: Move({_lastMoveInput.x:F2},{_lastMoveInput.y:F2}) Look({_lastLookInput.x:F2},{_lastLookInput.y:F2})");
        }

        private string GetComponentStatus()
        {
            return $"PlayerInput: {(_playerInput != null ? "✓" : "✗")} | " +
                   $"BodyRot: {(_bodyRotation != null ? "✓" : "✗")} | " +
                   $"CamLook: {(_cameraLook != null ? "✓" : "✗")}\n" +
                   $"CamTilt: {(_cameraTilt != null ? "✓" : "✗")} | " +
                   $"Movement: {(_movement != null ? "✓" : "✗")} | " +
                   $"ViewBob: {(_viewBob != null ? "✓" : "✗")}\n" +
                   $"DynamicFOV: {(_dynamicFOV != null ? "✓" : "✗")}";
        }
    }
}