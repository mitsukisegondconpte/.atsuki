using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace TsukiBR.Net
{
    public class ServerManager : NetworkManager
    {
        [Header("Game Settings")]
        public int maxPlayers = 50;
        public int maxBots = 30;
        public GameObject botPrefab;
        
        [Header("Spawn Points")]
        public Transform[] playerSpawnPoints;
        public Transform[] botSpawnPoints;

        private List<GameObject> activeBots = new List<GameObject>();
        private Game.GameManager gameManager;

        public override void Start()
        {
            base.Start();
            gameManager = FindObjectOfType<Game.GameManager>();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Find available spawn point
            Transform spawnPoint = GetAvailableSpawnPoint();
            
            // Instantiate player
            GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkServer.AddPlayerForConnection(conn, player);

            Debug.Log($"Player connected. Total players: {NetworkServer.connections.Count}");

            // Spawn bots if needed
            CheckAndSpawnBots();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"Player disconnected. Remaining players: {NetworkServer.connections.Count - 1}");
            base.OnServerDisconnect(conn);

            // Check if game should end
            CheckGameEndConditions();
        }

        private Transform GetAvailableSpawnPoint()
        {
            if (playerSpawnPoints.Length == 0)
            {
                Debug.LogWarning("No player spawn points defined! Using default position.");
                return transform;
            }

            // Find spawn point furthest from other players
            Transform bestSpawn = playerSpawnPoints[0];
            float maxDistance = 0f;

            foreach (var spawnPoint in playerSpawnPoints)
            {
                float minDistanceToPlayer = float.MaxValue;
                
                foreach (var player in FindObjectsOfType<Player.PlayerController>())
                {
                    float distance = Vector3.Distance(spawnPoint.position, player.transform.position);
                    if (distance < minDistanceToPlayer)
                    {
                        minDistanceToPlayer = distance;
                    }
                }

                if (minDistanceToPlayer > maxDistance)
                {
                    maxDistance = minDistanceToPlayer;
                    bestSpawn = spawnPoint;
                }
            }

            return bestSpawn;
        }

        [Server]
        private void CheckAndSpawnBots()
        {
            int totalPlayers = NetworkServer.connections.Count;
            int totalBots = activeBots.Count;
            int totalEntities = totalPlayers + totalBots;

            // Clean up destroyed bots
            activeBots.RemoveAll(bot => bot == null);
            totalBots = activeBots.Count;
            totalEntities = totalPlayers + totalBots;

            // Spawn bots to reach target player count
            int targetTotal = Mathf.Min(maxPlayers, totalPlayers + maxBots);
            int botsToSpawn = targetTotal - totalEntities;

            for (int i = 0; i < botsToSpawn; i++)
            {
                SpawnBot();
            }
        }

        [Server]
        private void SpawnBot()
        {
            if (botPrefab == null)
            {
                Debug.LogError("Bot prefab not assigned!");
                return;
            }

            Transform spawnPoint = GetBotSpawnPoint();
            GameObject bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkServer.Spawn(bot);

            activeBots.Add(bot);
            Debug.Log($"Bot spawned. Total bots: {activeBots.Count}");
        }

        private Transform GetBotSpawnPoint()
        {
            if (botSpawnPoints.Length == 0)
            {
                // Use player spawn points if no bot spawn points defined
                if (playerSpawnPoints.Length > 0)
                {
                    return playerSpawnPoints[Random.Range(0, playerSpawnPoints.Length)];
                }
                else
                {
                    // Random position around map center
                    Vector3 randomPos = Random.insideUnitSphere * 100f;
                    randomPos.y = 0f;
                    
                    // Ensure spawn on ground
                    RaycastHit hit;
                    if (Physics.Raycast(randomPos + Vector3.up * 100f, Vector3.down, out hit, 200f, LayerMask.GetMask("Ground")))
                    {
                        randomPos = hit.point;
                    }
                    
                    GameObject tempSpawn = new GameObject("TempSpawn");
                    tempSpawn.transform.position = randomPos;
                    return tempSpawn.transform;
                }
            }

            return botSpawnPoints[Random.Range(0, botSpawnPoints.Length)];
        }

        [Server]
        private void CheckGameEndConditions()
        {
            var alivePlayers = FindObjectsOfType<Player.PlayerController>().Length;
            var aliveBots = FindObjectsOfType<AI.BotController>().Length;
            
            int totalAlive = alivePlayers + aliveBots;

            if (totalAlive <= 1)
            {
                // Game over - last player/bot wins
                EndGame();
            }
        }

        [Server]
        private void EndGame()
        {
            Debug.Log("Game Over!");
            
            // Find winner
            var players = FindObjectsOfType<Player.PlayerController>();
            var bots = FindObjectsOfType<AI.BotController>();

            GameObject winner = null;
            
            foreach (var player in players)
            {
                if (player.isAlive)
                {
                    winner = player.gameObject;
                    break;
                }
            }

            if (winner == null)
            {
                foreach (var bot in bots)
                {
                    if (bot.isAlive)
                    {
                        winner = bot.gameObject;
                        break;
                    }
                }
            }

            RpcGameOver(winner?.name ?? "Unknown");

            // Restart game after delay
            Invoke(nameof(RestartGame), 10f);
        }

        [ClientRpc]
        private void RpcGameOver(string winnerName)
        {
            if (gameManager != null && gameManager.uiManager != null)
            {
                gameManager.uiManager.ShowGameOver(winnerName);
            }
            
            Debug.Log($"Game Over! Winner: {winnerName}");
        }

        [Server]
        private void RestartGame()
        {
            // Reset all players
            foreach (var player in FindObjectsOfType<Player.PlayerController>())
            {
                player.health = 100f;
                player.isAlive = true;
                player.kills = 0;
                
                // Respawn at new location
                Transform spawnPoint = GetAvailableSpawnPoint();
                player.transform.position = spawnPoint.position;
                player.transform.rotation = spawnPoint.rotation;
            }

            // Destroy all bots and respawn them
            foreach (var bot in activeBots)
            {
                if (bot != null)
                {
                    NetworkServer.Destroy(bot);
                }
            }
            activeBots.Clear();

            CheckAndSpawnBots();

            // Reset safe zone
            var safeZone = FindObjectOfType<Game.SafeZoneController>();
            if (safeZone != null)
            {
                safeZone.currentRadius = safeZone.initialRadius;
                safeZone.isActive = false;
            }

            RpcGameRestarted();
        }

        [ClientRpc]
        private void RpcGameRestarted()
        {
            if (gameManager != null && gameManager.uiManager != null)
            {
                gameManager.uiManager.HideGameOver();
            }
            
            Debug.Log("Game Restarted!");
        }

        public void StartServer()
        {
            StartHost(); // Start as host (server + client)
        }

        public void StartClient(string ipAddress)
        {
            networkAddress = ipAddress;
            StartClient();
        }

        public void StopConnection()
        {
            if (NetworkServer.active && NetworkClient.active)
            {
                StopHost();
            }
            else if (NetworkClient.active)
            {
                StopClient();
            }
            else if (NetworkServer.active)
            {
                StopServer();
            }
        }

        // Server status methods
        public bool IsServerRunning()
        {
            return NetworkServer.active;
        }

        public int GetConnectedPlayersCount()
        {
            return NetworkServer.connections.Count;
        }

        public int GetActiveBots()
        {
            return activeBots.Count;
        }

        private void OnGUI()
        {
            if (!showLogs) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Players: {GetConnectedPlayersCount()}");
            GUILayout.Label($"Bots: {GetActiveBots()}");
            GUILayout.Label($"Server: {(IsServerRunning() ? "Running" : "Stopped")}");
            GUILayout.EndArea();
        }

        [Header("Debug")]
        public bool showLogs = true;
    }
}