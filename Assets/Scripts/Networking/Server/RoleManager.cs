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
    /// Server-authoritative role management system with client synchronization.
    /// Assigns and tracks Runner vs Hunter roles.
    /// 
    /// IMPROVEMENTS FROM ORIGINAL:
    /// - Role synchronization via NetworkVariable
    /// - Client notification via ClientRpc
    /// - Role change events for UI updates
    /// - Better validation and error handling
    /// </summary>
    public class RoleManager : NetworkBehaviour
    {
        [Header("Role Configuration")]
        [Tooltip("Number of hunters in the game (typically 3-4)")]
        [SerializeField] private int hunterCount = 3;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // SERVER: Track role assignments
        private Dictionary<ulong, PlayerRole> m_ClientRoles = new Dictionary<ulong, PlayerRole>();

        // Track if runner has been assigned
        private bool m_RunnerAssigned = false;

        // EVENTS: For UI and other systems to react to role changes
        public static event System.Action<ulong, PlayerRole> OnRoleAssigned;
        public static event System.Action<ulong> OnRunnerDisconnected;

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

        private new void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region NetworkBehaviour Lifecycle

        public override void OnNetworkSpawn()
        {
            // Only the server manages roles
            if (!IsServer)
            {
                LogDebug("RoleManager spawned on client (passive mode)");
                return;
            }

            // Subscribe to connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

            LogDebug("✓ Role management system initialized on server");
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            // Unsubscribe to prevent memory leaks
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            }
        }

        #endregion

        #region Role Assignment Logic

        private void OnClientConnectedCallback(ulong clientId)
        {
            // Assign role based on connection order
            PlayerRole assignedRole = DetermineRole();
            m_ClientRoles[clientId] = assignedRole;

            LogDebug($"✓ Assigned {assignedRole} to client {clientId}");

            // Notify the specific client of their role
            NotifyClientRoleClientRpc(assignedRole, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });

            // Notify all clients of role counts for UI updates
            BroadcastRoleCountsClientRpc(GetRunnerCount(), GetHunterCount());

            // Fire event for local systems
            OnRoleAssigned?.Invoke(clientId, assignedRole);

            // Apply role-specific setup (spawn point, loadout, etc.)
            ApplyRoleSetup(clientId, assignedRole);
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            if (!m_ClientRoles.ContainsKey(clientId)) return;

            PlayerRole disconnectedRole = m_ClientRoles[clientId];
            m_ClientRoles.Remove(clientId);

            LogDebug($"⚠ Client {clientId} ({disconnectedRole}) disconnected");

            // If runner disconnects, game should end
            if (disconnectedRole == PlayerRole.Runner)
            {
                LogDebug("🚨 RUNNER DISCONNECTED! Game ending...");
                m_RunnerAssigned = false;
                OnRunnerDisconnected?.Invoke(clientId);

                // Notify all clients
                NotifyRunnerDisconnectedClientRpc();
            }

            // Update role counts
            BroadcastRoleCountsClientRpc(GetRunnerCount(), GetHunterCount());
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
            int currentHunters = GetHunterCount();

            // Assign Hunter if we haven't reached the limit
            if (currentHunters < hunterCount)
            {
                return PlayerRole.Hunter;
            }

            // Lobby full - this shouldn't happen due to max player check in NetworkManager
            Debug.LogWarning("[RoleManager] All roles filled. This client should have been rejected.");
            return PlayerRole.None;
        }

        /// <summary>
        /// Apply role-specific setup (spawn position, equipment, etc.)
        /// </summary>
        private void ApplyRoleSetup(ulong clientId, PlayerRole role)
        {
            // Find the player's NetworkObject
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out NetworkObject playerObject))
            {
                Debug.LogWarning($"[RoleManager] Could not find NetworkObject for client {clientId}");
                return;
            }

            // TODO: Apply role-specific setup
            // Examples:
            // - Different spawn positions (Runner vs Hunter starts)
            // - Different equipment/loadouts
            // - Different movement speeds
            // - Different abilities

            switch (role)
            {
                case PlayerRole.Runner:
                    LogDebug($"  → Setting up RUNNER for client {clientId}");
                    // Example: playerObject.GetComponent<RunnerAbilities>()?.Initialize();
                    break;

                case PlayerRole.Hunter:
                    LogDebug($"  → Setting up HUNTER for client {clientId}");
                    // Example: playerObject.GetComponent<HunterAbilities>()?.Initialize();
                    break;
            }
        }

        #endregion

        #region Client RPCs (Server → Client)

        /// <summary>
        /// Notify a specific client of their assigned role
        /// </summary>
        [ClientRpc]
        private void NotifyClientRoleClientRpc(PlayerRole role, ClientRpcParams clientRpcParams = default)
        {
            LogDebug($"📨 Received role assignment: {role}");

            // Fire event for UI/gameplay systems
            // Example: Show "You are the RUNNER!" or "You are a HUNTER!"
            OnRoleAssigned?.Invoke(NetworkManager.Singleton.LocalClientId, role);
        }

        /// <summary>
        /// Broadcast role counts to all clients for UI updates
        /// </summary>
        [ClientRpc]
        private void BroadcastRoleCountsClientRpc(int runnerCount, int hunterCount)
        {
            LogDebug($"📊 Role counts - Runners: {runnerCount}, Hunters: {hunterCount}");

            // Example: Update lobby UI
            // LobbyUI.Instance?.UpdatePlayerCounts(runnerCount, hunterCount);
        }

        /// <summary>
        /// Notify all clients that the runner disconnected (game should end)
        /// </summary>
        [ClientRpc]
        private void NotifyRunnerDisconnectedClientRpc()
        {
            LogDebug("🚨 Runner disconnected - Game ending!");

            // Fire event for game end logic
            OnRunnerDisconnected?.Invoke(0);

            // Example: Show "Runner disconnected! Returning to lobby..."
            // GameEndUI.Instance?.ShowRunnerDisconnected();
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
        /// Gets the client ID of the Runner. Returns ulong.MaxValue if not found.
        /// </summary>
        public ulong GetRunnerClientId()
        {
            if (!IsServer) return ulong.MaxValue;

            foreach (var kvp in m_ClientRoles)
            {
                if (kvp.Value == PlayerRole.Runner)
                    return kvp.Key;
            }

            return ulong.MaxValue;
        }

        /// <summary>
        /// Gets list of all Hunter client IDs
        /// </summary>
        public List<ulong> GetHunterClientIds()
        {
            List<ulong> hunters = new List<ulong>();

            if (!IsServer) return hunters;

            foreach (var kvp in m_ClientRoles)
            {
                if (kvp.Value == PlayerRole.Hunter)
                    hunters.Add(kvp.Key);
            }

            return hunters;
        }

        /// <summary>
        /// Gets current number of runners (should be 0 or 1)
        /// </summary>
        public int GetRunnerCount()
        {
            if (!IsServer) return 0;

            int count = 0;
            foreach (var role in m_ClientRoles.Values)
            {
                if (role == PlayerRole.Runner) count++;
            }
            return count;
        }

        /// <summary>
        /// Gets current number of hunters
        /// </summary>
        public int GetHunterCount()
        {
            if (!IsServer) return 0;

            int count = 0;
            foreach (var role in m_ClientRoles.Values)
            {
                if (role == PlayerRole.Hunter) count++;
            }
            return count;
        }

        /// <summary>
        /// Check if lobby is full
        /// </summary>
        public bool IsLobbyFull()
        {
            if (!IsServer) return false;
            return m_RunnerAssigned && GetHunterCount() >= hunterCount;
        }

        /// <summary>
        /// Check if game can start (1 runner + minimum hunters)
        /// </summary>
        public bool CanStartGame(int minHunters = 1)
        {
            if (!IsServer) return false;
            return m_RunnerAssigned && GetHunterCount() >= minHunters;
        }

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoleManager] {message}");
            }
        }

        /// <summary>
        /// Debug command to print all role assignments
        /// </summary>
        [ContextMenu("Print All Roles")]
        private void PrintAllRoles()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[RoleManager] Role printing only available on server");
                return;
            }

            Debug.Log("═══════════════════════════════════════");
            Debug.Log("CURRENT ROLE ASSIGNMENTS:");
            Debug.Log($"Runner Assigned: {m_RunnerAssigned}");
            Debug.Log($"Total Players: {m_ClientRoles.Count}");
            Debug.Log("─────────────────────────────────────");

            foreach (var kvp in m_ClientRoles)
            {
                Debug.Log($"  Client {kvp.Key}: {kvp.Value}");
            }

            Debug.Log("═══════════════════════════════════════");
        }

        #endregion
    }
}