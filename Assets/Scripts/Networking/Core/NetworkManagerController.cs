using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace ManhuntGame.Networking.Core
{
    /// <summary>
    /// Enhanced NetworkManager controller with automatic port conflict resolution.
    /// Now includes port synchronization for clients.
    /// </summary>
    public class NetworkManagerController : MonoBehaviour
    {
        [Header("Network Configuration")]
        [Tooltip("Maximum number of players allowed in a session (1 Runner + 3-4 Hunters)")]
        [SerializeField] private int maxPlayers = 5;

        [Header("Port Configuration")]
        [Tooltip("Starting port to try (default: 7777)")]
        [SerializeField] private ushort basePort = 7777;

        [Tooltip("How many ports to try if base port is busy")]
        [SerializeField] private int portRetryAttempts = 5;

        [Header("Client Configuration")]
        [Tooltip("Server address for clients to connect to (127.0.0.1 for localhost)")]
        [SerializeField] private string serverAddress = "127.0.0.1";

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        private NetworkManager m_NetworkManager;
        private UnityTransport m_UnityTransport;
        private ushort m_CurrentPort;

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

            // Cache UnityTransport reference
            m_UnityTransport = GetComponent<UnityTransport>();
            if (m_UnityTransport == null)
            {
                Debug.LogError("[NetworkManagerController] UnityTransport component not found! " +
                             "Please add UnityTransport to the NetworkManager GameObject.");
                enabled = false;
                return;
            }

            // Subscribe to connection events
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // Set initial port
            m_CurrentPort = basePort;
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
        /// Starts a game as Host (Server + Client combined) with automatic port retry.
        /// Use this for local testing or when one player hosts.
        /// </summary>
        public void StartHost()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Host.");
                return;
            }

            LogDebug("Starting Host with automatic port detection...");
            TryStartHostWithPortRetry();
        }

        /// <summary>
        /// Starts a dedicated server (no local player) with automatic port retry.
        /// Use this for dedicated server builds.
        /// </summary>
        public void StartServer()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Server.");
                return;
            }

            LogDebug("Starting Server with automatic port detection...");
            TryStartServerWithPortRetry();
        }

        /// <summary>
        /// Joins an existing game as a client.
        /// Automatically tries multiple ports to find the host.
        /// </summary>
        public void StartClient()
        {
            if (m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                LogDebug("Already running as Host, Server, or Client. Cannot start Client.");
                return;
            }

            LogDebug($"Starting Client... Will try ports {basePort} to {basePort + portRetryAttempts - 1}");
            TryStartClientWithPortRetry();
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

        #region Port Retry Logic

        /// <summary>
        /// Tries to start host, retrying with different ports if binding fails.
        /// </summary>
        private void TryStartHostWithPortRetry()
        {
            for (int attempt = 0; attempt < portRetryAttempts; attempt++)
            {
                m_CurrentPort = (ushort)(basePort + attempt);
                SetTransportPort(m_CurrentPort);

                LogDebug($"Attempt {attempt + 1}/{portRetryAttempts}: Trying to start Host on port {m_CurrentPort}...");

                bool success = m_NetworkManager.StartHost();

                if (success)
                {
                    LogDebug($"✓ Successfully started Host on port {m_CurrentPort}!");
                    LogDebug($"→ Clients should connect to: {serverAddress}:{m_CurrentPort}");
                    return;
                }
                else
                {
                    LogDebug($"✗ Failed to start Host on port {m_CurrentPort}. Trying next port...");

                    // Shutdown failed attempt
                    if (m_NetworkManager.IsListening)
                    {
                        m_NetworkManager.Shutdown();
                    }
                }
            }

            // All attempts failed
            Debug.LogError($"[NetworkManagerController] Failed to start Host after {portRetryAttempts} attempts. " +
                          $"Tried ports {basePort} to {basePort + portRetryAttempts - 1}. " +
                          $"Please close other Unity instances or change the base port.");
        }

        /// <summary>
        /// Tries to start server, retrying with different ports if binding fails.
        /// </summary>
        private void TryStartServerWithPortRetry()
        {
            for (int attempt = 0; attempt < portRetryAttempts; attempt++)
            {
                m_CurrentPort = (ushort)(basePort + attempt);
                SetTransportPort(m_CurrentPort);

                LogDebug($"Attempt {attempt + 1}/{portRetryAttempts}: Trying to start Server on port {m_CurrentPort}...");

                bool success = m_NetworkManager.StartServer();

                if (success)
                {
                    LogDebug($"✓ Successfully started Server on port {m_CurrentPort}!");
                    LogDebug($"→ Clients should connect to: {serverAddress}:{m_CurrentPort}");
                    return;
                }
                else
                {
                    LogDebug($"✗ Failed to start Server on port {m_CurrentPort}. Trying next port...");

                    // Shutdown failed attempt
                    if (m_NetworkManager.IsListening)
                    {
                        m_NetworkManager.Shutdown();
                    }
                }
            }

            // All attempts failed
            Debug.LogError($"[NetworkManagerController] Failed to start Server after {portRetryAttempts} attempts. " +
                          $"Tried ports {basePort} to {basePort + portRetryAttempts - 1}. " +
                          $"Please close other Unity instances or change the base port.");
        }

        /// <summary>
        /// Tries to connect as client, retrying with different ports to find the host.
        /// </summary>
        private void TryStartClientWithPortRetry()
        {
            for (int attempt = 0; attempt < portRetryAttempts; attempt++)
            {
                m_CurrentPort = (ushort)(basePort + attempt);
                SetTransportAddress(serverAddress, m_CurrentPort);

                LogDebug($"Attempt {attempt + 1}/{portRetryAttempts}: Trying to connect to {serverAddress}:{m_CurrentPort}...");

                bool success = m_NetworkManager.StartClient();

                if (success)
                {
                    LogDebug($"✓ Client started! Attempting connection to {serverAddress}:{m_CurrentPort}");

                    // Give it time to connect
                    StartCoroutine(CheckClientConnection(attempt));
                    return;
                }
                else
                {
                    LogDebug($"✗ Failed to start client. Trying next port...");
                }
            }

            // All attempts failed
            Debug.LogError($"[NetworkManagerController] Failed to start Client after {portRetryAttempts} attempts. " +
                          $"Tried ports {basePort} to {basePort + portRetryAttempts - 1}. " +
                          $"Make sure Host is running!");
        }

        /// <summary>
        /// Checks if client successfully connected, retries if not.
        /// </summary>
        private System.Collections.IEnumerator CheckClientConnection(int attemptNumber)
        {
            float timeout = 3f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (m_NetworkManager.IsConnectedClient)
                {
                    LogDebug($"✓ Successfully connected to server!");
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Connection timeout - try next port
            LogDebug($"✗ Connection timeout on port {m_CurrentPort}. Trying next port...");
            m_NetworkManager.Shutdown();

            // Try next port if we have attempts left
            if (attemptNumber + 1 < portRetryAttempts)
            {
                yield return new WaitForSeconds(0.5f);
                ushort nextPort = (ushort)(basePort + attemptNumber + 1);
                SetTransportAddress(serverAddress, nextPort);

                LogDebug($"Trying port {nextPort}...");
                m_NetworkManager.StartClient();
                StartCoroutine(CheckClientConnection(attemptNumber + 1));
            }
            else
            {
                Debug.LogError($"[NetworkManagerController] Could not connect to server after trying all ports. " +
                              $"Make sure the Host is running and using one of these ports: {basePort}-{basePort + portRetryAttempts - 1}");
            }
        }

        /// <summary>
        /// Sets the port on the Unity Transport component.
        /// </summary>
        private void SetTransportPort(ushort port)
        {
            if (m_UnityTransport == null) return;

            // Access the ConnectionData struct and modify the port
            var connectionData = m_UnityTransport.ConnectionData;
            connectionData.Port = port;
            m_UnityTransport.ConnectionData = connectionData;

            LogDebug($"Transport port set to: {port}");
        }

        /// <summary>
        /// Sets the address and port for client connections.
        /// </summary>
        private void SetTransportAddress(string address, ushort port)
        {
            if (m_UnityTransport == null) return;

            // Access the ConnectionData struct and modify address and port
            var connectionData = m_UnityTransport.ConnectionData;
            connectionData.Address = address;
            connectionData.Port = port;
            m_UnityTransport.ConnectionData = connectionData;

            LogDebug($"Transport set to connect to: {address}:{port}");
        }

        #endregion

        #region Network Event Callbacks

        private void OnServerStarted()
        {
            LogDebug($"Server started successfully on port {m_CurrentPort}. Max players: {maxPlayers}");
            LogDebug($"═══════════════════════════════════════");
            LogDebug($"   HOST IS READY ON PORT {m_CurrentPort}");
            LogDebug($"   Clients connect to: {serverAddress}:{m_CurrentPort}");
            LogDebug($"═══════════════════════════════════════");
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

        #region Public Getters

        /// <summary>
        /// Gets the current port the server is running on.
        /// </summary>
        public ushort GetCurrentPort() => m_CurrentPort;

        /// <summary>
        /// Gets the server address clients should connect to.
        /// </summary>
        public string GetServerAddress() => serverAddress;

        #endregion
    }
}