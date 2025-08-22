using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

namespace TsukiBR.Game
{
    public enum LootType
    {
        Weapon,
        Ammo,
        Health,
        Armor,
        Grenade
    }

    [System.Serializable]
    public class LootItem : NetworkBehaviour
    {
        public LootType lootType;
        public string itemName;
        public int quantity;
        public Sprite icon;
        public AudioClip pickupSound;

        [SyncVar]
        public bool isCollected = false;

        private void Start()
        {
            // Rotate loot item for visual effect
            if (!isCollected)
            {
                StartCoroutine(RotateLoot());
            }
        }

        private System.Collections.IEnumerator RotateLoot()
        {
            while (!isCollected)
            {
                transform.Rotate(0, 50 * Time.deltaTime, 0);
                yield return null;
            }
        }

        [Server]
        public void Collect(GameObject collector)
        {
            if (isCollected) return;

            var playerController = collector.GetComponent<Player.PlayerController>();
            var botController = collector.GetComponent<AI.BotController>();
            var weaponSystem = collector.GetComponent<Combat.WeaponSystem>();

            if (playerController != null || botController != null)
            {
                bool canCollect = false;

                switch (lootType)
                {
                    case LootType.Weapon:
                        if (weaponSystem != null)
                        {
                            Combat.WeaponType weaponType = (Combat.WeaponType)System.Enum.Parse(typeof(Combat.WeaponType), itemName);
                            weaponSystem.AddWeapon(weaponType, quantity);
                            canCollect = true;
                        }
                        break;

                    case LootType.Ammo:
                        if (weaponSystem != null)
                        {
                            Combat.WeaponType weaponType = (Combat.WeaponType)System.Enum.Parse(typeof(Combat.WeaponType), itemName);
                            if (weaponSystem.CanPickupAmmo(weaponType))
                            {
                                weaponSystem.AddWeapon(weaponType, quantity);
                                canCollect = true;
                            }
                        }
                        break;

                    case LootType.Health:
                        if (playerController != null && playerController.health < 100f)
                        {
                            playerController.Heal(quantity);
                            canCollect = true;
                        }
                        break;

                    case LootType.Grenade:
                        // Add grenade to inventory
                        canCollect = true;
                        break;
                }

                if (canCollect)
                {
                    isCollected = true;
                    RpcPlayPickupEffect(collector);
                    
                    // Destroy after short delay
                    Invoke(nameof(DestroySelf), 0.5f);
                }
            }
        }

        [ClientRpc]
        private void RpcPlayPickupEffect(GameObject collector)
        {
            // Play pickup sound
            if (pickupSound != null)
            {
                var audioSource = collector.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(pickupSound);
                }
            }

            // Hide visual
            GetComponent<Renderer>().enabled = false;
            GetComponent<Collider>().enabled = false;

            // Show pickup text if local player
            var playerController = collector.GetComponent<Player.PlayerController>();
            if (playerController != null && playerController.isLocalPlayer)
            {
                var gameManager = FindObjectOfType<GameManager>();
                if (gameManager != null && gameManager.uiManager != null)
                {
                    gameManager.uiManager.ShowPickupText($"Picked up {itemName} x{quantity}");
                }
            }
        }

        [Server]
        private void DestroySelf()
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    public class LootSystem : NetworkBehaviour
    {
        [Header("Loot Settings")]
        public GameObject[] lootPrefabs;
        public Transform[] lootSpawnPoints;
        public float lootSpawnRadius = 200f;
        public int maxLootItems = 100;

        [Header("Loot Tables")]
        public LootTable[] lootTables;

        private List<GameObject> activeLoot = new List<GameObject>();
        private GameConfig gameConfig;

        [System.Serializable]
        public class LootTable
        {
            public LootType lootType;
            public LootEntry[] entries;
        }

        [System.Serializable]
        public class LootEntry
        {
            public string itemName;
            public float spawnChance;
            public int minQuantity;
            public int maxQuantity;
            public GameObject prefab;
        }

        private void Start()
        {
            if (!isServer) return;

            gameConfig = GameManager.Instance.gameConfig;
            InvokeRepeating(nameof(SpawnRandomLoot), 10f, 30f); // Spawn loot every 30 seconds
        }

        [Server]
        private void SpawnRandomLoot()
        {
            if (activeLoot.Count >= maxLootItems) return;

            // Clean up null references
            activeLoot = activeLoot.Where(item => item != null).ToList();

            int spawnCount = Random.Range(1, 4); // Spawn 1-3 items
            
            for (int i = 0; i < spawnCount && activeLoot.Count < maxLootItems; i++)
            {
                SpawnLootAtRandomPosition();
            }
        }

        [Server]
        private void SpawnLootAtRandomPosition()
        {
            Vector3 spawnPosition;
            
            if (lootSpawnPoints.Length > 0)
            {
                // Use predefined spawn points
                Transform spawnPoint = lootSpawnPoints[Random.Range(0, lootSpawnPoints.Length)];
                spawnPosition = spawnPoint.position + Random.insideUnitSphere * 5f;
            }
            else
            {
                // Random position within spawn radius
                Vector2 randomCircle = Random.insideUnitCircle * lootSpawnRadius;
                spawnPosition = new Vector3(randomCircle.x, 0f, randomCircle.y);
            }

            // Ensure spawn position is on ground
            RaycastHit hit;
            if (Physics.Raycast(spawnPosition + Vector3.up * 100f, Vector3.down, out hit, 200f, LayerMask.GetMask("Ground")))
            {
                spawnPosition = hit.point + Vector3.up * 0.5f;
            }

            SpawnLootAtPosition(spawnPosition, GetRandomLootType(), GetRandomLootItem());
        }

        [Server]
        public void SpawnLootAtPosition(Vector3 position, LootType lootType, string itemName = "")
        {
            LootTable table = lootTables.FirstOrDefault(t => t.lootType == lootType);
            if (table == null) return;

            LootEntry entry;
            if (string.IsNullOrEmpty(itemName))
            {
                // Random item from table
                entry = GetRandomLootEntry(table);
            }
            else
            {
                // Specific item
                entry = table.entries.FirstOrDefault(e => e.itemName == itemName);
            }

            if (entry == null || entry.prefab == null) return;

            GameObject lootObject = Instantiate(entry.prefab, position, Quaternion.identity);
            NetworkServer.Spawn(lootObject);

            var lootItem = lootObject.GetComponent<LootItem>();
            if (lootItem != null)
            {
                lootItem.lootType = lootType;
                lootItem.itemName = entry.itemName;
                lootItem.quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
            }

            activeLoot.Add(lootObject);
        }

        private LootType GetRandomLootType()
        {
            float rand = Random.Range(0f, 1f);
            
            if (rand < 0.4f) return LootType.Ammo;
            if (rand < 0.6f) return LootType.Weapon;
            if (rand < 0.8f) return LootType.Health;
            if (rand < 0.95f) return LootType.Armor;
            return LootType.Grenade;
        }

        private string GetRandomLootItem()
        {
            // Return empty string to let SpawnLootAtPosition choose randomly
            return "";
        }

        private LootEntry GetRandomLootEntry(LootTable table)
        {
            float totalChance = table.entries.Sum(e => e.spawnChance);
            float rand = Random.Range(0f, totalChance);
            float currentChance = 0f;

            foreach (var entry in table.entries)
            {
                currentChance += entry.spawnChance;
                if (rand <= currentChance)
                {
                    return entry;
                }
            }

            return table.entries[0]; // Fallback
        }

        public void CollectLoot(GameObject collector, LootItem lootItem)
        {
            if (!isServer) return;

            lootItem.Collect(collector);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer) return;

            var lootItem = GetComponent<LootItem>();
            if (lootItem != null && !lootItem.isCollected)
            {
                var playerController = other.GetComponent<Player.PlayerController>();
                var botController = other.GetComponent<AI.BotController>();

                if (playerController != null || botController != null)
                {
                    lootItem.Collect(other.gameObject);
                }
            }
        }

        [Server]
        public void SpawnPlayerDeathLoot(Vector3 position, Combat.WeaponSystem weaponSystem)
        {
            // Spawn weapons from dead player
            foreach (var weapon in weaponSystem.inventory)
            {
                SpawnLootAtPosition(position + Random.insideUnitSphere * 2f, LootType.Weapon, weapon.ToString());
            }

            // Spawn some ammo
            SpawnLootAtPosition(position + Random.insideUnitSphere * 2f, LootType.Ammo, weaponSystem.currentWeaponType.ToString());
        }

        private void OnDrawGizmos()
        {
            // Draw loot spawn radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, lootSpawnRadius);

            // Draw spawn points
            if (lootSpawnPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var spawnPoint in lootSpawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawWireSphere(spawnPoint.position, 2f);
                    }
                }
            }
        }
    }
}