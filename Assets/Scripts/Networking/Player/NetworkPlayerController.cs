using Unity.Netcode;
using UnityEngine;
using Game.Player;
using UnityEngine.InputSystem;

namespace ManhuntGame.Networking.Player
{
    /// <summary>
    /// Network-aware player controller - CRITICAL FIX V2
    /// 
    /// FIXED ISSUES:
    /// 1. Movement now works on both host AND client
    /// 2. Camera rotation works on client (left/right AND up/down)
    /// 3. Removed manual Rigidbody.isKinematic setting (NetworkRigidbody handles this)
    /// 
    /// KEY PRINCIPLE:
    /// - Input/Camera scripts: Run on OWNER only (client-side, instant)
    /// - Physics: Managed by SERVER (via PlayerMovement)
    /// - Rigidbody state: Managed by NetworkRigidbody component
    /// </summary>
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] private MonoBehaviour playerMovementScript;
        [SerializeField] private MonoBehaviour playerInputScript;
        [SerializeField] private MonoBehaviour playerBodyRotationScript;

        [Header("Camera Components")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerListener;
        [SerializeField] private MonoBehaviour cameraLookScript; // CameraVerticalLook
        [SerializeField] private MonoBehaviour cameraTiltScript; // PlayerCameraTilt
        [SerializeField] private MonoBehaviour viewBobScript;    // PlayerViewBob
        [SerializeField] private MonoBehaviour dynamicFOVScript; // PlayerDynamicFOV

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            // Auto-find components if not assigned
            if (playerMovementScript == null)
                playerMovementScript = GetComponent<PlayerMovement>();

            if (playerInputScript == null)
                playerInputScript = GetComponent<PlayerInput>();

            if (playerBodyRotationScript == null)
                playerBodyRotationScript = GetComponent<PlayerBodyRotation>();

            // Find Camera and all camera scripts
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();

            if (playerListener == null)
                playerListener = GetComponentInChildren<AudioListener>();

            // Auto-find camera scripts
            if (playerCamera != null)
            {
                if (cameraLookScript == null)
                    cameraLookScript = playerCamera.GetComponent<CameraVerticalLook>();

                if (viewBobScript == null)
                    viewBobScript = playerCamera.GetComponent<PlayerViewBob>();

                if (dynamicFOVScript == null)
                    dynamicFOVScript = playerCamera.GetComponent<PlayerDynamicFOV>();
            }

            // Find camera tilt on CameraHolder
            if (cameraTiltScript == null && playerCamera != null)
            {
                Transform cameraHolder = playerCamera.transform.parent;
                if (cameraHolder != null)
                    cameraTiltScript = cameraHolder.GetComponent<PlayerCameraTilt>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            bool isMine = IsOwner;
            bool isServer = IsServer;

            LogDebug($"════════════════════════════════════════════════");
            LogDebug($"OnNetworkSpawn - IsOwner: {isMine}, IsServer: {isServer}, ClientId: {OwnerClientId}");
            LogDebug($"════════════════════════════════════════════════");

            // ═══════════════════════════════════════════════════════════════
            // CRITICAL FIX V2: Proper component enable logic
            // ═══════════════════════════════════════════════════════════════

            // 1. CAMERA & AUDIO - OWNER ONLY (for local player view)
            if (playerCamera)
            {
                playerCamera.enabled = isMine;
                LogDebug($"  📷 Camera: {isMine}");
            }

            if (playerListener)
            {
                playerListener.enabled = isMine;
                LogDebug($"  🔊 AudioListener: {isMine}");
            }

            // 2. CAMERA LOOK CONTROLS - OWNER ONLY (instant response, no lag)
            //    These handle mouse input for camera rotation
            if (cameraLookScript)
            {
                cameraLookScript.enabled = isMine;
                LogDebug($"  👀 CameraVerticalLook (up/down): {isMine}");
            }

            if (playerBodyRotationScript)
            {
                playerBodyRotationScript.enabled = isMine;
                LogDebug($"  🔄 PlayerBodyRotation (left/right): {isMine}");
            }

            if (cameraTiltScript)
            {
                cameraTiltScript.enabled = isMine;
                LogDebug($"  ↔️ PlayerCameraTilt: {isMine}");
            }

            if (viewBobScript)
            {
                viewBobScript.enabled = isMine;
                LogDebug($"  📳 ViewBob: {isMine}");
            }

            if (dynamicFOVScript)
            {
                dynamicFOVScript.enabled = isMine;
                LogDebug($"  🎥 DynamicFOV: {isMine}");
            }

            // 3. INPUT - OWNER ONLY (for reading WASD, mouse, etc.)
            if (playerInputScript)
            {
                playerInputScript.enabled = isMine;
                LogDebug($"  🎮 PlayerInput: {isMine}");
            }

            // 4. MOVEMENT - OWNER ONLY (for reading input and sending to server)
            //    CRITICAL FIX: Must run on owner to read input!
            //    Physics only applies on server, but input reading needs owner
            if (playerMovementScript)
            {
                playerMovementScript.enabled = isMine;
                LogDebug($"  🏃 PlayerMovement: {isMine}");
            }

            // 5. CURSOR - OWNER ONLY
            if (isMine)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                LogDebug("  🖱️ Cursor locked");
            }

            // 6. SETUP NAME TAG (optional)
            SetupNameTag(isMine);

            LogDebug($"════════════════════════════════════════════════");
        }

        /// <summary>
        /// Optional: Set up name tag or player identifier
        /// </summary>
        private void SetupNameTag(bool isMine)
        {
            // Example implementation for player name display:
            /*
            TextMeshPro nameTag = GetComponentInChildren<TextMeshPro>();
            if (nameTag != null)
            {
                nameTag.gameObject.SetActive(!isMine);
                
                // Get role from RoleManager if available
                if (ManhuntGame.Networking.Server.RoleManager.Instance != null)
                {
                    var role = ManhuntGame.Networking.Server.RoleManager.Instance.GetClientRole(OwnerClientId);
                    nameTag.text = isMine ? "You" : $"Player {OwnerClientId} ({role})";
                }
                else
                {
                    nameTag.text = isMine ? "You" : $"Player {OwnerClientId}";
                }
            }
            */
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkPlayerController] {message}");
            }
        }

        #region Public API
        /// <summary>
        /// Get the player's client ID
        /// </summary>
        public ulong GetClientId() => OwnerClientId;

        /// <summary>
        /// Check if this player is the local client
        /// Note: Use IsOwner from NetworkBehaviour instead of this
        /// </summary>
        // Removed redundant IsLocalPlayer - use base.IsOwner instead
        #endregion
    }
}