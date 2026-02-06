using UnityEngine;
using Unity.Netcode;

namespace ManhuntGame.Networking.Core
{
    /// <summary>
    /// Controls NetworkManager lifecycle and provides UI hooks for starting/stopping network sessions.
    /// This is the entry point for all multiplayer functionality.
    /// </summary>
    public class NetworkManagerController : MonoBehaviour
    {
        [Header("Network Configuration")]
        [Tooltip("Maximum number of players allowed in a session (1 Runner + 3-4 Hunters)")]
        [SerializeField] private int maxPlayers = 5;

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        private NetworkManager m_NetworkManager;

        #region Unity Lifecycle

        private void Awake()
        {
            // Cache NetworkManager reference for performance
            m_NetworkManager = GetComponent<NetworkManager>();

            if (m_NetworkManager == null)
            {
                Debug.LogError("[NetworkManagerController] NetworkManager component not found! " +
                             "This script must be on the same GameObject as NetworkManager.");
                enabled = false;
                return;
            }

            // Subscribe to connection events
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            // Always unsubscribe from events to prevent memory leaks
            if (m_NetworkManager != null)
            {
                m_NetworkManager.OnServerStarted -= OnServerStarted;
                m_NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                m_NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        #endregion

        #region Public Methods (Call from UI)

        /// <summary>
        /// Starts a game as Host (Server + Client combined).
        /// Use this for local testing or when one player hosts.
        /// </summary>
        public void StartHost()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Host.");
                return;
            }

            LogDebug("Starting Host...");
            m_NetworkManager.StartHost();
        }

        /// <summary>
        /// Starts a dedicated server (no local player).
        /// Use this for dedicated server builds.
        /// </summary>
        public void StartServer()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Server.");
                return;
            }

            LogDebug("Starting Server...");
            m_NetworkManager.StartServer();
        }

        /// <summary>
        /// Joins an existing game as a client.
        /// Make sure to set the server address in Unity Transport before calling this.
        /// </summary>
        public void StartClient()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Client.");
                return;
            }

            LogDebug("Starting Client...");
            m_NetworkManager.StartClient();
        }

        /// <summary>
        /// Disconnects from the current session and shuts down networking.
        /// </summary>
        public void Shutdown()
        {
            LogDebug("Shutting down network session...");
            m_NetworkManager.Shutdown();
        }

        #endregion

        #region Network Event Callbacks

        private void OnServerStarted()
        {
            LogDebug($"Server started successfully. Max players: {maxPlayers}");
        }

        private void OnClientConnected(ulong clientId)
        {
            // Check if we're the server (only server should handle connection logic)
            if (!m_NetworkManager.IsServer) return;

            int connectedPlayers = m_NetworkManager.ConnectedClientsList.Count;
            LogDebug($"Client {clientId} connected. Total players: {connectedPlayers}/{maxPlayers}");

            // Server-side: Check if lobby is full
            if (connectedPlayers > maxPlayers)
            {
                LogDebug($"Server full! Disconnecting client {clientId}");
                m_NetworkManager.DisconnectClient(clientId);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!m_NetworkManager.IsServer) return;

            int connectedPlayers = m_NetworkManager.ConnectedClientsList.Count;
            LogDebug($"Client {clientId} disconnected. Remaining players: {connectedPlayers}");
        }

        #endregion

        #region Helper Methods

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkManagerController] {message}");
            }
        }

        #endregion
    }
}