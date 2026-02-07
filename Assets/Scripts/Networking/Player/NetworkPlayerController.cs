using Unity.Netcode;
using UnityEngine;

namespace ManhuntGame.Networking.Player
{
    /// <summary>
    /// Handles network ownership and controls which player can control which character.
    /// This wraps your existing movement scripts to only allow the owner to control their player.
    /// </summary>
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] private MonoBehaviour playerMovementScript;  // Your existing PlayerMovement script
        [SerializeField] private MonoBehaviour playerInputScript;     // Your existing PlayerInput script
        [SerializeField] private MonoBehaviour playerBodyRotationScript; // Your existing rotation script
        [SerializeField] private Camera playerCamera;                 // Reference to player's camera

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            // Auto-find components if not assigned
            if (playerMovementScript == null)
                playerMovementScript = GetComponent<MonoBehaviour>(); // You'll need to assign this in Inspector

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only enable controls for the owner of this player
            if (IsOwner)
            {
                EnablePlayerControls();
                LogDebug($"Player {OwnerClientId} spawned - Controls ENABLED (You are the owner)");
            }
            else
            {
                DisablePlayerControls();
                LogDebug($"Player {OwnerClientId} spawned - Controls DISABLED (Not your player)");
            }
        }

        /// <summary>
        /// Enable movement controls for the local player
        /// </summary>
        private void EnablePlayerControls()
        {
            // Enable your existing movement scripts
            if (playerMovementScript != null)
                playerMovementScript.enabled = true;

            if (playerInputScript != null)
                playerInputScript.enabled = true;

            if (playerBodyRotationScript != null)
                playerBodyRotationScript.enabled = true;

            // Enable the camera for this player
            if (playerCamera != null)
                playerCamera.enabled = true;

            // Lock cursor for gameplay (you can toggle this with ESC)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Disable controls for remote players (players you don't own)
        /// </summary>
        private void DisablePlayerControls()
        {
            // Disable movement scripts for players you don't control
            if (playerMovementScript != null)
                playerMovementScript.enabled = false;

            if (playerInputScript != null)
                playerInputScript.enabled = false;

            if (playerBodyRotationScript != null)
                playerBodyRotationScript.enabled = false;

            // Disable camera for remote players
            if (playerCamera != null)
                playerCamera.enabled = false;
        }

        private void Update()
        {
            // Allow ESC to unlock cursor (useful for testing)
            // NOTE: You can add ESC key handling using the new Input System in your PlayerInput script
            // For now, we'll remove this to avoid Input System conflicts

            // To add ESC functionality, add this action to your Input Actions asset:
            // 1. Open your Input Actions asset
            // 2. Add new action called "ToggleCursor" 
            // 3. Bind it to ESC key
            // 4. Handle it in your PlayerInput script
        }

        private void LogDebug(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[NetworkPlayerController] {message}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            LogDebug($"Player {OwnerClientId} despawned");
        }
    }
}