using Unity.Netcode;
using UnityEngine;
using Game.Player; // Needed to reference CameraVerticalLook
using UnityEngine.InputSystem;

namespace ManhuntGame.Networking.Player
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] private MonoBehaviour playerMovementScript;
        [SerializeField] private MonoBehaviour playerInputScript;
        [SerializeField] private MonoBehaviour playerBodyRotationScript;

        [Header("Camera Components")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerListener;
        [SerializeField] private MonoBehaviour cameraLookScript; // Reference to CameraVerticalLook

        private void Awake()
        {
            // Auto-find components if not assigned
            if (playerMovementScript == null) playerMovementScript = GetComponent<PlayerMovement>();
            if (playerInputScript == null) playerInputScript = GetComponent<PlayerInput>();
            if (playerBodyRotationScript == null) playerBodyRotationScript = GetComponent<PlayerBodyRotation>();

            // Find Camera stuff
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (playerListener == null) playerListener = GetComponentInChildren<AudioListener>();

            // Auto-find the look script on the camera
            if (cameraLookScript == null && playerCamera != null)
            {
                cameraLookScript = playerCamera.GetComponent<CameraVerticalLook>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            bool isMine = IsOwner;

            // 1. Setup Camera & Audio (Only for the local player)
            if (playerCamera) playerCamera.enabled = isMine;
            if (playerListener) playerListener.enabled = isMine;
            if (cameraLookScript) cameraLookScript.enabled = isMine;

            // 2. Setup Controls
            // Movement logic runs on Owner (to send input) AND Server (to process physics)
            bool enableControls = isMine || IsServer;

            if (playerMovementScript) playerMovementScript.enabled = enableControls;
            if (playerInputScript) playerInputScript.enabled = enableControls;
            if (playerBodyRotationScript) playerBodyRotationScript.enabled = enableControls;

            // 3. Cursor Management
            if (isMine)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}