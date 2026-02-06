using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ManhuntGame.Networking.Server
{
    /// <summary>
    /// Defines player roles in the Manhunt game.
    /// </summary>
    public enum PlayerRole
    {
        None = 0,
        Runner = 1,
        Hunter = 2
    }

    /// <summary>
    /// Server-authoritative role management system.
    /// Assigns and tracks Runner vs Hunter roles.
    /// IMPORTANT: This runs ONLY on the server.
    /// </summary>
    public class RoleManager : NetworkBehaviour
    {
        [Header("Role Configuration")]
        [Tooltip("Number of hunters in the game (typically 3-4)")]
        [SerializeField] private int hunterCount = 3;

        // Track role assignments (Server only)
        private Dictionary<ulong, PlayerRole> m_ClientRoles = new Dictionary<ulong, PlayerRole>();

        // Track if runner has been assigned
        private bool m_RunnerAssigned = false;

        #region Singleton Pattern

        public static RoleManager Instance { get; private set; }

        private void Awake()
        {
            // Ensure only one RoleManager exists
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #endregion

        #region NetworkBehaviour Lifecycle

        public override void OnNetworkSpawn()
        {
            // Only the server manages roles
            if (!IsServer) return;

            // Subscribe to connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

            Debug.Log("[RoleManager] Role management system initialized on server.");
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            // Unsubscribe to prevent memory leaks
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        }

        #endregion

        #region Role Assignment Logic

        private void OnClientConnectedCallback(ulong clientId)
        {
            // Assign role based on connection order
            PlayerRole assignedRole = DetermineRole();
            m_ClientRoles[clientId] = assignedRole;

            Debug.Log($"[RoleManager] Assigned {assignedRole} to client {clientId}");

            // TODO: Send role to client via ClientRpc (we'll add this in Step 6)
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            if (!m_ClientRoles.ContainsKey(clientId)) return;

            PlayerRole disconnectedRole = m_ClientRoles[clientId];
            m_ClientRoles.Remove(clientId);

            Debug.Log($"[RoleManager] Client {clientId} ({disconnectedRole}) disconnected.");

            // If runner disconnects, game should end
            if (disconnectedRole == PlayerRole.Runner)
            {
                Debug.LogWarning("[RoleManager] Runner disconnected! Game should end.");
                m_RunnerAssigned = false;
                // TODO: Trigger game end logic
            }
        }

        /// <summary>
        /// Determines what role a newly connected player should receive.
        /// First player = Runner, subsequent players = Hunter (up to hunterCount)
        /// </summary>
        private PlayerRole DetermineRole()
        {
            // First player is always the Runner
            if (!m_RunnerAssigned)
            {
                m_RunnerAssigned = true;
                return PlayerRole.Runner;
            }

            // Count existing hunters
            int currentHunters = 0;
            foreach (var role in m_ClientRoles.Values)
            {
                if (role == PlayerRole.Hunter)
                    currentHunters++;
            }

            // Assign Hunter if we haven't reached the limit
            if (currentHunters < hunterCount)
            {
                return PlayerRole.Hunter;
            }

            // Lobby full - this shouldn't happen due to max player check
            Debug.LogWarning("[RoleManager] All roles filled. This client should have been rejected.");
            return PlayerRole.None;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the role of a specific client. Server-only.
        /// </summary>
        public PlayerRole GetClientRole(ulong clientId)
        {
            if (!IsServer)
            {
                Debug.LogError("[RoleManager] GetClientRole can only be called on server!");
                return PlayerRole.None;
            }

            return m_ClientRoles.TryGetValue(clientId, out PlayerRole role) ? role : PlayerRole.None;
        }

        /// <summary>
        /// Gets the client ID of the Runner. Returns NetworkManager.ServerClientId if not found.
        /// </summary>
        public ulong GetRunnerClientId()
        {
            if (!IsServer) return NetworkManager.ServerClientId;

            foreach (var kvp in m_ClientRoles)
            {
                if (kvp.Value == PlayerRole.Runner)
                    return kvp.Key;
            }

            return NetworkManager.ServerClientId; // Fallback
        }

        #endregion
    }
}