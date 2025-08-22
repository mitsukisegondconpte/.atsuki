using UnityEngine;
using Mirror;
using System.Collections;

namespace TsukiBR.Game
{
    public class SafeZoneController : NetworkBehaviour
    {
        [Header("Safe Zone Settings")]
        public float initialRadius = 500f;
        public float finalRadius = 50f;
        public float shrinkDuration = 300f; // 5 minutes
        public float damagePerSecond = 5f;
        public float damageInterval = 1f;

        [Header("Visual")]
        public Transform safeZoneVisual;
        public Material safeZoneMaterial;
        public Material dangerZoneMaterial;

        [SyncVar] public float currentRadius;
        [SyncVar] public Vector3 safeZoneCenter;
        [SyncVar] public bool isActive = false;

        private bool isShrinking = false;
        private Coroutine shrinkCoroutine;
        private Coroutine damageCoroutine;

        private void Start()
        {
            currentRadius = initialRadius;
            safeZoneCenter = Vector3.zero; // Center of map
            
            if (isServer)
            {
                // Start safe zone after initial grace period
                Invoke(nameof(StartSafeZone), 60f); // 1 minute grace period
            }

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (safeZoneVisual == null)
            {
                // Create safe zone visual
                GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                zoneObject.name = "SafeZone";
                zoneObject.transform.parent = transform;
                
                // Remove collider, we only want visuals
                DestroyImmediate(zoneObject.GetComponent<Collider>());
                
                safeZoneVisual = zoneObject.transform;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (safeZoneVisual == null) return;

            safeZoneVisual.position = safeZoneCenter;
            safeZoneVisual.localScale = new Vector3(currentRadius * 2, 0.1f, currentRadius * 2);
            
            var renderer = safeZoneVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = isActive ? safeZoneMaterial : dangerZoneMaterial;
            }
        }

        [Server]
        private void StartSafeZone()
        {
            isActive = true;
            RpcAnnounceStart();
            
            shrinkCoroutine = StartCoroutine(ShrinkSafeZone());
            damageCoroutine = StartCoroutine(ApplyDamageOutsideSafeZone());
        }

        [ClientRpc]
        private void RpcAnnounceStart()
        {
            // Show UI notification about safe zone starting
            var gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null && gameManager.uiManager != null)
            {
                gameManager.uiManager.ShowSafeZoneWarning("The safe zone is now active!");
            }
        }

        [Server]
        private IEnumerator ShrinkSafeZone()
        {
            float elapsedTime = 0f;
            float startRadius = currentRadius;
            
            while (elapsedTime < shrinkDuration && currentRadius > finalRadius)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / shrinkDuration;
                
                currentRadius = Mathf.Lerp(startRadius, finalRadius, progress);
                
                // Update visuals for all clients
                RpcUpdateVisuals();
                
                yield return null;
            }
            
            currentRadius = finalRadius;
            RpcUpdateVisuals();
            
            // Announce final zone
            RpcAnnounceFinalZone();
        }

        [ClientRpc]
        private void RpcUpdateVisuals()
        {
            UpdateVisuals();
        }

        [ClientRpc]
        private void RpcAnnounceFinalZone()
        {
            var gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null && gameManager.uiManager != null)
            {
                gameManager.uiManager.ShowSafeZoneWarning("Final safe zone reached!");
            }
        }

        [Server]
        private IEnumerator ApplyDamageOutsideSafeZone()
        {
            while (isActive)
            {
                // Find all players and bots outside safe zone
                var players = FindObjectsOfType<Player.PlayerController>();
                var bots = FindObjectsOfType<AI.BotController>();

                foreach (var player in players)
                {
                    if (player.isAlive && !IsInSafeZone(player.transform.position))
                    {
                        player.CmdTakeDamage(damagePerSecond);
                        
                        // Show damage effect to player
                        RpcShowZoneDamage(player.gameObject);
                    }
                }

                foreach (var bot in bots)
                {
                    if (bot.isAlive && !IsInSafeZone(bot.transform.position))
                    {
                        bot.TakeDamage(damagePerSecond);
                    }
                }

                yield return new WaitForSeconds(damageInterval);
            }
        }

        [ClientRpc]
        private void RpcShowZoneDamage(GameObject player)
        {
            // Show red screen effect or damage indicator
            if (player != null)
            {
                var playerController = player.GetComponent<Player.PlayerController>();
                if (playerController != null && playerController.isLocalPlayer)
                {
                    // Trigger damage UI effect
                    var gameManager = FindObjectOfType<GameManager>();
                    if (gameManager != null && gameManager.uiManager != null)
                    {
                        gameManager.uiManager.ShowDamageEffect();
                    }
                }
            }
        }

        public bool IsInSafeZone(Vector3 position)
        {
            if (!isActive) return true;
            
            float distance = Vector3.Distance(position, safeZoneCenter);
            return distance <= currentRadius;
        }

        public Vector3 GetSafeZoneCenter()
        {
            return safeZoneCenter;
        }

        public float GetSafeZoneRadius()
        {
            return currentRadius;
        }

        public float GetDistanceToSafeZone(Vector3 position)
        {
            float distanceToCenter = Vector3.Distance(position, safeZoneCenter);
            return Mathf.Max(0f, distanceToCenter - currentRadius);
        }

        private void OnDrawGizmos()
        {
            // Draw safe zone in editor
            Gizmos.color = isActive ? Color.green : Color.red;
            Gizmos.DrawWireSphere(safeZoneCenter, currentRadius);
            
            if (Application.isPlaying && isActive)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(safeZoneCenter, finalRadius);
            }
        }

        [Server]
        public void ForceShrink()
        {
            // For testing or admin commands
            if (shrinkCoroutine != null)
            {
                StopCoroutine(shrinkCoroutine);
            }
            
            shrinkCoroutine = StartCoroutine(ShrinkSafeZone());
        }

        private void OnDestroy()
        {
            if (shrinkCoroutine != null)
            {
                StopCoroutine(shrinkCoroutine);
            }
            
            if (damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
            }
        }
    }
}