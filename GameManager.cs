using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace TsukiBR.Game
{
    [System.Serializable]
    public class GameConfig
    {
        public GameSettings gameSettings;
        public WeaponConfig[] weapons;
        public LootTableConfig lootTables;
        public MapSettings mapSettings;
        public PlayerSettings playerSettings;
        public BotSettings botSettings;

        [System.Serializable]
        public class GameSettings
        {
            public int maxPlayers;
            public int maxBots;
            public int gameTime;
            public SafeZoneSettings safeZoneSettings;
        }

        [System.Serializable]
        public class SafeZoneSettings
        {
            public float initialRadius;
            public float finalRadius;
            public float shrinkDuration;
            public float damagePerSecond;
        }

        [System.Serializable]
        public class WeaponConfig
        {
            public string type;
            public string name;
            public float damage;
            public float fireRate;
            public int maxAmmo;
            public float reloadTime;
            public float range;
            public bool isAutomatic;
            public string rarity;
        }

        [System.Serializable]
        public class LootTableConfig
        {
            public LootEntry[] common;
            public LootEntry[] rare;
            public LootEntry[] epic;
        }

        [System.Serializable]
        public class LootEntry
        {
            public string itemType;
            public string itemName;
            public int spawnChance;
            public int minQuantity;
            public int maxQuantity;
        }

        [System.Serializable]
        public class MapSettings
        {
            public MapSize mapSize;
            public MapZone[] zones;
        }

        [System.Serializable]
        public class MapSize
        {
            public int width;
            public int height;
        }

        [System.Serializable]
        public class MapZone
        {
            public string name;
            public int spawnWeight;
            public string lootDensity;
        }

        [System.Serializable]
        public class PlayerSettings
        {
            public int maxHealth;
            public float walkSpeed;
            public float runSpeed;
            public float jumpHeight;
            public int maxInventorySlots;
        }

        [System.Serializable]
        public class BotSettings
        {
            public BotDifficultyLevels difficultyLevels;
        }

        [System.Serializable]
        public class BotDifficultyLevels
        {
            public BotDifficulty Easy;
            public BotDifficulty Medium;
            public BotDifficulty Hard;
        }

        [System.Serializable]
        public class BotDifficulty
        {
            public float accuracy;
            public float reactionTime;
            public float detectionRange;
        }
    }

    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Configuration")]
        public GameConfig gameConfig;
        
        [Header("Managers")]
        public UIManager uiManager;
        public Net.ServerManager serverManager;
        public Net.ClientManager clientManager;

        [Header("Game State")]
        [SyncVar] public bool gameStarted = false;
        [SyncVar] public float gameTime = 0f;
        [SyncVar] public int playersAlive = 0;
        [SyncVar] public int botsAlive = 0;

        private SafeZoneController safeZone;
        private LootSystem lootSystem;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadGameConfig();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            safeZone = FindObjectOfType<SafeZoneController>();
            lootSystem = FindObjectOfType<LootSystem>();
            
            if (isServer)
            {
                InvokeRepeating(nameof(UpdateGameState), 1f, 1f);
            }
        }

        private void LoadGameConfig()
        {
            TextAsset configFile = Resources.Load<TextAsset>("Configs/GameConfig");
            if (configFile != null)
            {
                try
                {
                    gameConfig = JsonUtility.FromJson<GameConfig>(configFile.text);
                    Debug.Log("Game configuration loaded successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load game config: {e.Message}");
                    CreateDefaultConfig();
                }
            }
            else
            {
                Debug.LogWarning("Game config file not found, creating default");
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            gameConfig = new GameConfig
            {
                gameSettings = new GameConfig.GameSettings
                {
                    maxPlayers = 50,
                    maxBots = 30,
                    gameTime = 1200,
                    safeZoneSettings = new GameConfig.SafeZoneSettings
                    {
                        initialRadius = 500f,
                        finalRadius = 50f,
                        shrinkDuration = 300f,
                        damagePerSecond = 5f
                    }
                }
            };
        }

        [Server]
        private void UpdateGameState()
        {
            if (!gameStarted) return;

            // Update game timer
            gameTime += 1f;

            // Count alive players and bots
            var players = FindObjectsOfType<Player.PlayerController>();
            var bots = FindObjectsOfType<AI.BotController>();

            playersAlive = 0;
            botsAlive = 0;

            foreach (var player in players)
            {
                if (player.isAlive) playersAlive++;
            }

            foreach (var bot in bots)
            {
                if (bot.isAlive) botsAlive++;
            }

            // Check win condition
            int totalAlive = playersAlive + botsAlive;
            if (totalAlive <= 1)
            {
                EndGame();
            }
        }

        [Server]
        public void StartGame()
        {
            gameStarted = true;
            gameTime = 0f;
            
            RpcGameStarted();
        }

        [ClientRpc]
        private void RpcGameStarted()
        {
            if (uiManager != null)
            {
                uiManager.ShowGameStarted();
            }
            
            Debug.Log("Battle Royale started!");
        }

        [Server]
        private void EndGame()
        {
            gameStarted = false;
            
            // Find winner
            string winner = "No one";
            
            var players = FindObjectsOfType<Player.PlayerController>();
            foreach (var player in players)
            {
                if (player.isAlive)
                {
                    winner = $"Player {player.playerUID}";
                    break;
                }
            }

            if (winner == "No one")
            {
                var bots = FindObjectsOfType<AI.BotController>();
                foreach (var bot in bots)
                {
                    if (bot.isAlive)
                    {
                        winner = "Bot";
                        break;
                    }
                }
            }

            RpcGameEnded(winner);
        }

        [ClientRpc]
        private void RpcGameEnded(string winner)
        {
            if (uiManager != null)
            {
                uiManager.ShowGameOver(winner);
            }
            
            Debug.Log($"Game ended! Winner: {winner}");
        }

        public void RestartGame()
        {
            if (isServer)
            {
                // Reset all game state
                gameStarted = false;
                gameTime = 0f;
                
                // This would trigger a full game restart
                if (serverManager != null)
                {
                    // Restart logic handled by ServerManager
                }
            }
        }

        public GameConfig GetGameConfig()
        {
            return gameConfig;
        }

        // Utility methods
        public bool IsGameActive()
        {
            return gameStarted;
        }

        public int GetTotalPlayersAlive()
        {
            return playersAlive + botsAlive;
        }

        public float GetRemainingTime()
        {
            float maxGameTime = gameConfig?.gameSettings?.gameTime ?? 1200f;
            return Mathf.Max(0f, maxGameTime - gameTime);
        }

        private void OnGUI()
        {
            if (!isServer) return;

            // Debug info
            GUILayout.BeginArea(new Rect(10, 200, 300, 150));
            GUILayout.Label($"Game Started: {gameStarted}");
            GUILayout.Label($"Game Time: {gameTime:F0}s");
            GUILayout.Label($"Players Alive: {playersAlive}");
            GUILayout.Label($"Bots Alive: {botsAlive}");
            
            if (!gameStarted && GUILayout.Button("Start Game"))
            {
                StartGame();
            }
            
            GUILayout.EndArea();
        }
    }

    // UI Manager class
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        public GameObject hudPanel;
        public GameObject gameOverPanel;
        public GameObject safeZoneWarningPanel;
        public GameObject pickupTextPanel;

        [Header("HUD Elements")]
        public UnityEngine.UI.Text healthText;
        public UnityEngine.UI.Text ammoText;
        public UnityEngine.UI.Text killsText;
        public UnityEngine.UI.Text playersAliveText;
        public UnityEngine.UI.Text gameTimeText;
        public UnityEngine.UI.Image crosshair;

        [Header("Effects")]
        public UnityEngine.UI.Image damageOverlay;
        public UnityEngine.UI.Text pickupText;
        public UnityEngine.UI.Text safeZoneText;

        private Player.PlayerController localPlayer;

        private void Start()
        {
            // Find local player
            StartCoroutine(FindLocalPlayer());
        }

        private System.Collections.IEnumerator FindLocalPlayer()
        {
            while (localPlayer == null)
            {
                var players = FindObjectsOfType<Player.PlayerController>();
                foreach (var player in players)
                {
                    if (player.isLocalPlayer)
                    {
                        localPlayer = player;
                        break;
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void Update()
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (localPlayer == null) return;

            // Update health
            if (healthText != null)
                healthText.text = $"HP: {localPlayer.health:F0}";

            // Update ammo
            var weaponSystem = localPlayer.GetComponent<Combat.WeaponSystem>();
            if (weaponSystem != null && ammoText != null)
            {
                ammoText.text = $"{weaponSystem.currentAmmo} / {weaponSystem.totalAmmo}";
            }

            // Update kills
            if (killsText != null)
                killsText.text = $"Kills: {localPlayer.kills}";

            // Update players alive
            if (playersAliveText != null && GameManager.Instance != null)
            {
                playersAliveText.text = $"Alive: {GameManager.Instance.GetTotalPlayersAlive()}";
            }

            // Update game time
            if (gameTimeText != null && GameManager.Instance != null)
            {
                float remainingTime = GameManager.Instance.GetRemainingTime();
                int minutes = (int)(remainingTime / 60f);
                int seconds = (int)(remainingTime % 60f);
                gameTimeText.text = $"{minutes:D2}:{seconds:D2}";
            }
        }

        public void ShowGameOver(string winner)
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                var winnerText = gameOverPanel.GetComponentInChildren<UnityEngine.UI.Text>();
                if (winnerText != null)
                    winnerText.text = $"Winner: {winner}";
            }
        }

        public void HideGameOver()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        public void ShowGameStarted()
        {
            // Show game started notification
            if (safeZoneWarningPanel != null)
            {
                safeZoneWarningPanel.SetActive(true);
                if (safeZoneText != null)
                    safeZoneText.text = "Battle Royale Started!";
                
                Invoke(nameof(HideSafeZoneWarning), 3f);
            }
        }

        public void ShowSafeZoneWarning(string message)
        {
            if (safeZoneWarningPanel != null && safeZoneText != null)
            {
                safeZoneWarningPanel.SetActive(true);
                safeZoneText.text = message;
                Invoke(nameof(HideSafeZoneWarning), 5f);
            }
        }

        public void HideSafeZoneWarning()
        {
            if (safeZoneWarningPanel != null)
                safeZoneWarningPanel.SetActive(false);
        }

        public void ShowPickupText(string message)
        {
            if (pickupTextPanel != null && pickupText != null)
            {
                pickupTextPanel.SetActive(true);
                pickupText.text = message;
                Invoke(nameof(HidePickupText), 2f);
            }
        }

        public void HidePickupText()
        {
            if (pickupTextPanel != null)
                pickupTextPanel.SetActive(false);
        }

        public void ShowDamageEffect()
        {
            if (damageOverlay != null)
            {
                StartCoroutine(DamageFlash());
            }
        }

        private System.Collections.IEnumerator DamageFlash()
        {
            damageOverlay.color = new Color(1, 0, 0, 0.3f);
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float alpha = Mathf.Lerp(0.3f, 0f, elapsed / duration);
                damageOverlay.color = new Color(1, 0, 0, alpha);
                elapsed += Time.deltaTime;
                yield return null;
            }

            damageOverlay.color = new Color(1, 0, 0, 0f);
        }
    }
}