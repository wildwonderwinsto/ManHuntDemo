using Unity.Netcode;
using UnityEngine;
using System;

namespace ManhuntGame.Networking.Player
{
    /// <summary>
    /// Server-authoritative health system with client synchronization.
    /// Handles damage, healing, death, and respawn for multiplayer.
    /// 
    /// FEATURES:
    /// - Server validates all damage/healing
    /// - NetworkVariable for health synchronization
    /// - Events for UI updates and gameplay reactions
    /// - Anti-cheat: Server controls all health changes
    /// </summary>
    public class NetworkHealth : NetworkBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health points")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Starting health (usually same as max)")]
        [SerializeField] private float startingHealth = 100f;

        [Tooltip("Respawn delay after death (seconds)")]
        [SerializeField] private float respawnDelay = 5f;

        [Header("Damage Settings")]
        [Tooltip("Invulnerability time after taking damage (prevents spam)")]
        [SerializeField] private float damageInvulnerabilityTime = 0.5f;

        [Tooltip("Enable friendly fire (hunters can damage hunters)")]
        [SerializeField] private bool friendlyFireEnabled = false;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // NETWORK VARIABLE: Health synced to all clients
        private NetworkVariable<float> m_NetworkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // NETWORK VARIABLE: Is player alive?
        private NetworkVariable<bool> m_IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Server-side state
        private float m_LastDamageTime;
        private bool m_IsInvulnerable;

        // EVENTS: For UI and gameplay systems
        public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
        public event Action<float, ulong> OnDamageTaken;   // (damage, attackerId)
        public event Action OnDeath;
        public event Action OnRespawn;

        #region NetworkBehaviour Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to network variable changes
            m_NetworkHealth.OnValueChanged += OnHealthValueChanged;
            m_IsAlive.OnValueChanged += OnAliveValueChanged;

            // Initialize health
            if (IsServer)
            {
                m_NetworkHealth.Value = startingHealth;
                m_IsAlive.Value = true;
                LogDebug($"Initialized health for client {OwnerClientId}: {startingHealth}/{maxHealth}");
            }

            // Trigger initial event for UI
            OnHealthChanged?.Invoke(m_NetworkHealth.Value, maxHealth);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Unsubscribe to prevent memory leaks
            m_NetworkHealth.OnValueChanged -= OnHealthValueChanged;
            m_IsAlive.OnValueChanged -= OnAliveValueChanged;
        }

        #endregion

        #region Server-Side Health Management

        /// <summary>
        /// Apply damage to this player (SERVER ONLY)
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <param name="attackerId">Client ID of the attacker</param>
        /// <param name="damageType">Type of damage (for future expansion)</param>
        public void TakeDamage(float damage, ulong attackerId, string damageType = "")
        {
            if (!IsServer)
            {
                Debug.LogError("[NetworkHealth] TakeDamage can only be called on server!");
                return;
            }

            // Validate damage
            if (damage < 0)
            {
                Debug.LogWarning($"[NetworkHealth] Negative damage attempted: {damage}");
                return;
            }

            // Check if alive
            if (!m_IsAlive.Value)
            {
                LogDebug("Damage ignored - player is dead");
                return;
            }

            // Check invulnerability
            if (m_IsInvulnerable)
            {
                LogDebug("Damage ignored - player is invulnerable");
                return;
            }

            // Validate friendly fire
            if (!ValidateFriendlyFire(attackerId))
            {
                LogDebug("Damage ignored - friendly fire disabled");
                return;
            }

            // Apply damage
            float newHealth = Mathf.Max(0, m_NetworkHealth.Value - damage);
            m_NetworkHealth.Value = newHealth;

            LogDebug($"Client {OwnerClientId} took {damage} damage from {attackerId}. Health: {newHealth}/{maxHealth}");

            // Notify client of damage taken
            NotifyDamageClientRpc(damage, attackerId);

            // Apply invulnerability
            m_IsInvulnerable = true;
            m_LastDamageTime = Time.time;
            Invoke(nameof(ResetInvulnerability), damageInvulnerabilityTime);

            // Check for death
            if (newHealth <= 0)
            {
                Die(attackerId);
            }
        }

        /// <summary>
        /// Heal this player (SERVER ONLY)
        /// </summary>
        public void Heal(float amount)
        {
            if (!IsServer) return;

            if (!m_IsAlive.Value)
            {
                LogDebug("Heal ignored - player is dead");
                return;
            }

            float newHealth = Mathf.Min(maxHealth, m_NetworkHealth.Value + amount);
            m_NetworkHealth.Value = newHealth;

            LogDebug($"Client {OwnerClientId} healed {amount}. Health: {newHealth}/{maxHealth}");

            // Notify client
            NotifyHealClientRpc(amount);
        }

        /// <summary>
        /// Kill this player (SERVER ONLY)
        /// </summary>
        private void Die(ulong killerId)
        {
            if (!IsServer) return;

            m_IsAlive.Value = false;
            LogDebug($"Client {OwnerClientId} died (killed by {killerId})");

            // Notify all clients of death
            NotifyDeathClientRpc(killerId);

            // Schedule respawn
            Invoke(nameof(Respawn), respawnDelay);
        }

        /// <summary>
        /// Respawn this player (SERVER ONLY)
        /// </summary>
        private void Respawn()
        {
            if (!IsServer) return;

            m_NetworkHealth.Value = maxHealth;
            m_IsAlive.Value = true;
            m_IsInvulnerable = false;

            LogDebug($"Client {OwnerClientId} respawned");

            // Notify client
            NotifyRespawnClientRpc();
        }

        /// <summary>
        /// Reset invulnerability after damage cooldown
        /// </summary>
        private void ResetInvulnerability()
        {
            m_IsInvulnerable = false;
        }

        /// <summary>
        /// Validate friendly fire rules
        /// </summary>
        private bool ValidateFriendlyFire(ulong attackerId)
        {
            if (friendlyFireEnabled) return true;

            // Get roles from RoleManager
            if (ManhuntGame.Networking.Server.RoleManager.Instance == null)
                return true; // Allow if RoleManager not available

            var myRole = ManhuntGame.Networking.Server.RoleManager.Instance.GetClientRole(OwnerClientId);
            var attackerRole = ManhuntGame.Networking.Server.RoleManager.Instance.GetClientRole(attackerId);

            // Prevent hunter-on-hunter damage if friendly fire disabled
            if (myRole == ManhuntGame.Networking.Server.PlayerRole.Hunter &&
                attackerRole == ManhuntGame.Networking.Server.PlayerRole.Hunter)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Client RPCs (Server → Client)

        /// <summary>
        /// Notify client they took damage
        /// </summary>
        [ClientRpc]
        private void NotifyDamageClientRpc(float damage, ulong attackerId)
        {
            // Only execute on the owning client
            if (!IsOwner) return;

            LogDebug($"📉 Took {damage} damage! Health: {m_NetworkHealth.Value}/{maxHealth}");
            OnDamageTaken?.Invoke(damage, attackerId);

            // Example: Play damage sound, show damage indicator, screen shake
            // DamageIndicatorUI.Instance?.ShowDamageDirection(attackerId);
            // AudioManager.Instance?.PlaySound("PlayerHurt");
        }

        /// <summary>
        /// Notify client they healed
        /// </summary>
        [ClientRpc]
        private void NotifyHealClientRpc(float amount)
        {
            if (!IsOwner) return;

            LogDebug($"💚 Healed {amount}! Health: {m_NetworkHealth.Value}/{maxHealth}");

            // Example: Play heal sound, show heal effect
            // AudioManager.Instance?.PlaySound("Heal");
        }

        /// <summary>
        /// Notify all clients of death
        /// </summary>
        [ClientRpc]
        private void NotifyDeathClientRpc(ulong killerId)
        {
            LogDebug($"💀 Player {OwnerClientId} died (killed by {killerId})");
            OnDeath?.Invoke();

            // Different logic for owner vs others
            if (IsOwner)
            {
                // Show death screen
                // DeathScreenUI.Instance?.Show(killerId);
            }
            else
            {
                // Play death animation for other players
                // GetComponent<Animator>()?.SetTrigger("Death");
            }
        }

        /// <summary>
        /// Notify all clients of respawn
        /// </summary>
        [ClientRpc]
        private void NotifyRespawnClientRpc()
        {
            LogDebug($"♻ Player {OwnerClientId} respawned");
            OnRespawn?.Invoke();

            if (IsOwner)
            {
                // Hide death screen, reset camera
                // DeathScreenUI.Instance?.Hide();
            }
        }

        #endregion

        #region Network Variable Callbacks

        private void OnHealthValueChanged(float previousValue, float newValue)
        {
            // Trigger event for UI updates
            OnHealthChanged?.Invoke(newValue, maxHealth);

            // Update health bar
            // HealthBarUI.Instance?.UpdateHealth(newValue, maxHealth);
        }

        private void OnAliveValueChanged(bool previousValue, bool newValue)
        {
            // Enable/disable player controls based on alive state
            if (IsOwner)
            {
                // Example: Disable movement when dead
                // GetComponent<PlayerMovement>()?.enabled = newValue;
            }
        }

        #endregion

        #region Server RPCs (Client → Server)

        /// <summary>
        /// Client requests to take damage (for self-damage scenarios)
        /// </summary>
        [ServerRpc]
        public void RequestTakeDamageServerRpc(float damage)
        {
            // Validate request
            TakeDamage(damage, OwnerClientId, "self");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get current health (works on all clients via NetworkVariable)
        /// </summary>
        public float GetCurrentHealth() => m_NetworkHealth.Value;

        /// <summary>
        /// Get max health
        /// </summary>
        public float GetMaxHealth() => maxHealth;

        /// <summary>
        /// Get health percentage (0-1)
        /// </summary>
        public float GetHealthPercentage() => m_NetworkHealth.Value / maxHealth;

        /// <summary>
        /// Check if player is alive
        /// </summary>
        public bool IsPlayerAlive() => m_IsAlive.Value;

        /// <summary>
        /// Check if player is at full health
        /// </summary>
        public bool IsFullHealth() => m_NetworkHealth.Value >= maxHealth;

        /// <summary>
        /// Set max health (SERVER ONLY, use carefully)
        /// </summary>
        public void SetMaxHealth(float newMax)
        {
            if (!IsServer) return;
            maxHealth = newMax;
            m_NetworkHealth.Value = Mathf.Min(m_NetworkHealth.Value, maxHealth);
        }

        #endregion

        #region Debug

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkHealth] {message}");
            }
        }

        /// <summary>
        /// Debug command to damage self
        /// </summary>
        [ContextMenu("Debug: Take 25 Damage")]
        private void DebugTakeDamage()
        {
            if (IsServer)
            {
                TakeDamage(25f, OwnerClientId, "debug");
            }
        }

        /// <summary>
        /// Debug command to heal self
        /// </summary>
        [ContextMenu("Debug: Heal 50")]
        private void DebugHeal()
        {
            if (IsServer)
            {
                Heal(50f);
            }
        }

        #endregion
    }
}