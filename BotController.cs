using UnityEngine;
using UnityEngine.AI;
using Mirror;
using System.Collections.Generic;

namespace TsukiBR.AI
{
    public enum BotState
    {
        Patrolling,
        Chasing,
        Attacking,
        Looting,
        MovingToSafeZone
    }

    public class BotController : NetworkBehaviour
    {
        [Header("Bot Settings")]
        public float health = 100f;
        public float detectionRange = 15f;
        public float attackRange = 10f;
        public float lootRange = 5f;
        public float shootAccuracy = 0.7f;
        public float reactionTime = 0.5f;

        [Header("Movement")]
        public float walkSpeed = 3f;
        public float runSpeed = 6f;
        public float patrolRadius = 20f;

        [SyncVar] public bool isAlive = true;
        [SyncVar] public BotState currentState = BotState.Patrolling;

        private NavMeshAgent navAgent;
        private Animator animator;
        private Transform target;
        private Vector3 patrolCenter;
        private Vector3 patrolDestination;
        private float lastShotTime;
        private float nextDecisionTime;

        // Bot equipment
        private Combat.WeaponSystem weaponSystem;
        private List<Combat.WeaponType> botInventory;
        private Game.LootSystem lootSystem;

        private void Start()
        {
            // Only server controls bots
            if (!isServer) return;

            navAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            weaponSystem = GetComponent<Combat.WeaponSystem>();
            lootSystem = FindObjectOfType<Game.LootSystem>();

            patrolCenter = transform.position;
            botInventory = new List<Combat.WeaponType> { Combat.WeaponType.Pistol };
            
            // Give bot a random weapon
            Combat.WeaponType[] availableWeapons = { 
                Combat.WeaponType.Pistol, 
                Combat.WeaponType.SMG, 
                Combat.WeaponType.Rifle 
            };
            Combat.WeaponType startWeapon = availableWeapons[Random.Range(0, availableWeapons.Length)];
            
            if (weaponSystem != null)
            {
                weaponSystem.AddWeapon(startWeapon, 120);
                weaponSystem.EquipWeapon(startWeapon);
            }

            SetNewPatrolDestination();
            InvokeRepeating(nameof(UpdateAI), 1f, 0.5f); // Update AI every 0.5 seconds
        }

        private void UpdateAI()
        {
            if (!isServer || !isAlive) return;

            switch (currentState)
            {
                case BotState.Patrolling:
                    HandlePatrolling();
                    break;
                case BotState.Chasing:
                    HandleChasing();
                    break;
                case BotState.Attacking:
                    HandleAttacking();
                    break;
                case BotState.Looting:
                    HandleLooting();
                    break;
                case BotState.MovingToSafeZone:
                    HandleMovingToSafeZone();
                    break;
            }

            UpdateAnimator();
            CheckForThreats();
            CheckForLoot();
            CheckSafeZone();
        }

        private void HandlePatrolling()
        {
            navAgent.speed = walkSpeed;
            
            if (!navAgent.pathPending && navAgent.remainingDistance < 1f)
            {
                SetNewPatrolDestination();
            }
        }

        private void HandleChasing()
        {
            if (target == null || !IsTargetValid())
            {
                currentState = BotState.Patrolling;
                target = null;
                return;
            }

            navAgent.speed = runSpeed;
            navAgent.SetDestination(target.position);

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            
            if (distanceToTarget <= attackRange)
            {
                currentState = BotState.Attacking;
            }
        }

        private void HandleAttacking()
        {
            if (target == null || !IsTargetValid())
            {
                currentState = BotState.Patrolling;
                target = null;
                return;
            }

            navAgent.speed = 0f;
            
            // Face target
            Vector3 lookDirection = (target.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(lookDirection);

            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            
            if (distanceToTarget > attackRange)
            {
                currentState = BotState.Chasing;
                return;
            }

            // Try to shoot
            if (Time.time >= lastShotTime + reactionTime)
            {
                TryShoot();
                lastShotTime = Time.time;
            }
        }

        private void HandleLooting()
        {
            // Move to nearest loot and collect it
            navAgent.speed = walkSpeed;
            
            if (!navAgent.pathPending && navAgent.remainingDistance < 1f)
            {
                // Try to collect loot
                Collider[] lootInRange = Physics.OverlapSphere(transform.position, lootRange, LayerMask.GetMask("Loot"));
                
                if (lootInRange.Length > 0)
                {
                    foreach (var lootCollider in lootInRange)
                    {
                        var lootItem = lootCollider.GetComponent<Game.LootItem>();
                        if (lootItem != null && lootSystem != null)
                        {
                            // Collect the loot
                            lootSystem.CollectLoot(gameObject, lootItem);
                            break;
                        }
                    }
                }

                currentState = BotState.Patrolling;
            }
        }

        private void HandleMovingToSafeZone()
        {
            navAgent.speed = runSpeed;
            
            var safeZone = FindObjectOfType<Game.SafeZoneController>();
            if (safeZone != null)
            {
                Vector3 safeCenter = safeZone.GetSafeZoneCenter();
                navAgent.SetDestination(safeCenter);
                
                if (Vector3.Distance(transform.position, safeCenter) <= safeZone.GetSafeZoneRadius())
                {
                    currentState = BotState.Patrolling;
                    patrolCenter = transform.position; // Update patrol center to current safe area
                }
            }
            else
            {
                currentState = BotState.Patrolling;
            }
        }

        private void CheckForThreats()
        {
            if (currentState == BotState.Attacking || currentState == BotState.MovingToSafeZone) return;

            Collider[] potentialTargets = Physics.OverlapSphere(transform.position, detectionRange, LayerMask.GetMask("Player"));
            
            float closestDistance = float.MaxValue;
            Transform closestTarget = null;

            foreach (var targetCollider in potentialTargets)
            {
                var playerController = targetCollider.GetComponent<Player.PlayerController>();
                if (playerController != null && playerController.isAlive && targetCollider.gameObject != gameObject)
                {
                    float distance = Vector3.Distance(transform.position, targetCollider.transform.position);
                    
                    // Check line of sight
                    if (HasLineOfSight(targetCollider.transform) && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = targetCollider.transform;
                    }
                }
            }

            if (closestTarget != null)
            {
                target = closestTarget;
                currentState = closestDistance <= attackRange ? BotState.Attacking : BotState.Chasing;
            }
        }

        private void CheckForLoot()
        {
            if (currentState != BotState.Patrolling) return;
            if (Random.Range(0f, 1f) > 0.3f) return; // 30% chance to look for loot

            Collider[] lootInRange = Physics.OverlapSphere(transform.position, lootRange * 2, LayerMask.GetMask("Loot"));
            
            if (lootInRange.Length > 0)
            {
                Transform closestLoot = lootInRange[0].transform;
                float closestDistance = Vector3.Distance(transform.position, closestLoot.position);
                
                foreach (var loot in lootInRange)
                {
                    float distance = Vector3.Distance(transform.position, loot.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestLoot = loot.transform;
                    }
                }

                navAgent.SetDestination(closestLoot.position);
                currentState = BotState.Looting;
            }
        }

        private void CheckSafeZone()
        {
            var safeZone = FindObjectOfType<Game.SafeZoneController>();
            if (safeZone != null && !safeZone.IsInSafeZone(transform.position))
            {
                if (currentState != BotState.MovingToSafeZone)
                {
                    currentState = BotState.MovingToSafeZone;
                }
            }
        }

        private bool HasLineOfSight(Transform targetTransform)
        {
            Vector3 rayDirection = (targetTransform.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, targetTransform.position);
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, rayDirection, out hit, distance, ~LayerMask.GetMask("Player", "Bot")))
            {
                return hit.collider.transform == targetTransform;
            }
            
            return true;
        }

        private bool IsTargetValid()
        {
            if (target == null) return false;
            
            var playerController = target.GetComponent<Player.PlayerController>();
            if (playerController != null && !playerController.isAlive) return false;
            
            float distance = Vector3.Distance(transform.position, target.position);
            return distance <= detectionRange * 1.5f; // Allow some extra range before giving up
        }

        private void TryShoot()
        {
            if (weaponSystem == null || target == null) return;

            // Aim at target with some inaccuracy
            Vector3 aimDirection = (target.position - transform.position).normalized;
            
            if (Random.Range(0f, 1f) > shootAccuracy)
            {
                // Add inaccuracy
                aimDirection += Random.insideUnitSphere * 0.2f;
                aimDirection.Normalize();
            }

            // Use weapon system to shoot
            weaponSystem.TryShoot();
        }

        private void SetNewPatrolDestination()
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += patrolCenter;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
            {
                patrolDestination = hit.position;
                navAgent.SetDestination(patrolDestination);
            }
        }

        private void UpdateAnimator()
        {
            if (animator != null)
            {
                float speed = navAgent.velocity.magnitude / runSpeed;
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsAttacking", currentState == BotState.Attacking);
            }
        }

        public void TakeDamage(float damage)
        {
            if (!isServer || !isAlive) return;

            health -= damage;
            
            if (health <= 0)
            {
                health = 0;
                isAlive = false;
                RpcBotDied();
            }
        }

        [ClientRpc]
        private void RpcBotDied()
        {
            // Show death effects, drop loot
            if (lootSystem != null)
            {
                lootSystem.SpawnLootAtPosition(transform.position, Game.LootType.Weapon, botInventory[0].ToString());
            }
            
            gameObject.SetActive(false);
            
            // Remove from game after delay
            if (isServer)
            {
                Invoke(nameof(DestroyBot), 5f);
            }
        }

        private void DestroyBot()
        {
            if (isServer)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            // Draw attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Draw patrol radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(patrolCenter, patrolRadius);
        }
    }
}