using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;

namespace TsukiBR.Combat
{
    public enum WeaponType
    {
        Pistol,
        SMG,
        Rifle,
        Sniper,
        Grenade,
        Molotov,
        Melee
    }

    [System.Serializable]
    public class WeaponData
    {
        public WeaponType type;
        public string name;
        public float damage;
        public float fireRate;
        public int maxAmmo;
        public int currentAmmo;
        public float reloadTime;
        public float range;
        public bool isAutomatic;
        public GameObject prefab;
        public AudioClip fireSound;
        public AudioClip reloadSound;
    }

    public class WeaponSystem : NetworkBehaviour
    {
        [Header("Weapon Settings")]
        public Transform weaponHolder;
        public Transform firePoint;
        public LayerMask enemyLayers;
        public GameObject muzzleFlash;
        public GameObject bulletTrail;

        [Header("Current Weapon")]
        [SyncVar] public WeaponType currentWeaponType;
        [SyncVar] public int currentAmmo;
        [SyncVar] public int totalAmmo;

        private WeaponData currentWeapon;
        private GameObject currentWeaponObject;
        private Dictionary<WeaponType, WeaponData> weapons;
        private bool isReloading;
        private float nextFireTime;
        private AudioSource audioSource;

        [Header("Inventory")]
        public List<WeaponType> inventory = new List<WeaponType>();

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            InitializeWeapons();
            
            if (isLocalPlayer)
            {
                // Start with a pistol
                EquipWeapon(WeaponType.Pistol);
            }
        }

        private void InitializeWeapons()
        {
            weapons = new Dictionary<WeaponType, WeaponData>();
            
            // Load weapon data from config
            var config = Game.GameManager.Instance.gameConfig;
            
            foreach (var weaponConfig in config.weapons)
            {
                WeaponData weapon = new WeaponData
                {
                    type = (WeaponType)System.Enum.Parse(typeof(WeaponType), weaponConfig.type),
                    name = weaponConfig.name,
                    damage = weaponConfig.damage,
                    fireRate = weaponConfig.fireRate,
                    maxAmmo = weaponConfig.maxAmmo,
                    currentAmmo = weaponConfig.maxAmmo,
                    reloadTime = weaponConfig.reloadTime,
                    range = weaponConfig.range,
                    isAutomatic = weaponConfig.isAutomatic
                };
                
                weapons[weapon.type] = weapon;
            }
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            HandleWeaponSwitching();
            HandleReload();
        }

        private void HandleWeaponSwitching()
        {
            for (int i = 1; i <= 7; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) && i <= inventory.Count)
                {
                    EquipWeapon(inventory[i - 1]);
                }
            }

            // Mouse wheel weapon switching
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f && inventory.Count > 0)
            {
                int currentIndex = inventory.IndexOf(currentWeaponType);
                int nextIndex = (currentIndex + 1) % inventory.Count;
                EquipWeapon(inventory[nextIndex]);
            }
            else if (scroll < 0f && inventory.Count > 0)
            {
                int currentIndex = inventory.IndexOf(currentWeaponType);
                int prevIndex = (currentIndex - 1 + inventory.Count) % inventory.Count;
                EquipWeapon(inventory[prevIndex]);
            }
        }

        private void HandleReload()
        {
            if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < currentWeapon.maxAmmo && totalAmmo > 0)
            {
                StartCoroutine(Reload());
            }
        }

        public void TryShoot()
        {
            if (!isLocalPlayer || isReloading || Time.time < nextFireTime) return;

            if (currentAmmo <= 0)
            {
                // Auto reload if no ammo
                if (totalAmmo > 0)
                    StartCoroutine(Reload());
                return;
            }

            Shoot();
            nextFireTime = Time.time + (1f / currentWeapon.fireRate);
        }

        [Command]
        private void CmdShoot(Vector3 origin, Vector3 direction, float damage, float range)
        {
            RpcShoot(origin, direction, damage, range);
        }

        [ClientRpc]
        private void RpcShoot(Vector3 origin, Vector3 direction, float damage, float range)
        {
            // Visual effects
            ShowMuzzleFlash();
            PlayFireSound();

            // Raycast for hit detection (authoritative on server)
            if (isServer)
            {
                RaycastHit hit;
                if (Physics.Raycast(origin, direction, out hit, range, enemyLayers))
                {
                    // Check if hit player or bot
                    var playerController = hit.collider.GetComponent<Player.PlayerController>();
                    var botController = hit.collider.GetComponent<AI.BotController>();

                    if (playerController != null && playerController != GetComponent<Player.PlayerController>())
                    {
                        playerController.CmdTakeDamage(damage);
                        if (!playerController.isAlive)
                        {
                            GetComponent<Player.PlayerController>().CmdAddKill();
                        }
                    }
                    else if (botController != null)
                    {
                        botController.TakeDamage(damage);
                        if (!botController.isAlive)
                        {
                            GetComponent<Player.PlayerController>().CmdAddKill();
                        }
                    }
                }
            }
        }

        private void Shoot()
        {
            if (currentWeapon == null) return;

            currentAmmo--;
            Vector3 shootDirection = firePoint.forward;
            
            // Add spread for non-sniper weapons
            if (currentWeapon.type != WeaponType.Sniper)
            {
                shootDirection += Random.insideUnitSphere * 0.1f;
                shootDirection.Normalize();
            }

            CmdShoot(firePoint.position, shootDirection, currentWeapon.damage, currentWeapon.range);
        }

        private IEnumerator Reload()
        {
            isReloading = true;
            PlayReloadSound();

            yield return new WaitForSeconds(currentWeapon.reloadTime);

            int ammoNeeded = currentWeapon.maxAmmo - currentAmmo;
            int ammoToReload = Mathf.Min(ammoNeeded, totalAmmo);
            
            currentAmmo += ammoToReload;
            totalAmmo -= ammoToReload;
            
            isReloading = false;
        }

        public void EquipWeapon(WeaponType weaponType)
        {
            if (!weapons.ContainsKey(weaponType)) return;

            currentWeaponType = weaponType;
            currentWeapon = weapons[weaponType];
            currentAmmo = currentWeapon.currentAmmo;

            // Destroy current weapon object
            if (currentWeaponObject != null)
            {
                Destroy(currentWeaponObject);
            }

            // Instantiate new weapon object
            if (currentWeapon.prefab != null)
            {
                currentWeaponObject = Instantiate(currentWeapon.prefab, weaponHolder);
            }

            CmdUpdateWeapon(weaponType, currentAmmo);
        }

        [Command]
        private void CmdUpdateWeapon(WeaponType weaponType, int ammo)
        {
            currentWeaponType = weaponType;
            currentAmmo = ammo;
        }

        public void AddWeapon(WeaponType weaponType, int ammo)
        {
            if (!inventory.Contains(weaponType))
            {
                inventory.Add(weaponType);
            }

            if (weaponType == currentWeaponType)
            {
                totalAmmo += ammo;
            }
            else if (weapons.ContainsKey(weaponType))
            {
                // Store ammo for this weapon type
                weapons[weaponType].currentAmmo = ammo;
            }
        }

        private void ShowMuzzleFlash()
        {
            if (muzzleFlash != null && firePoint != null)
            {
                GameObject flash = Instantiate(muzzleFlash, firePoint.position, firePoint.rotation);
                Destroy(flash, 0.1f);
            }
        }

        private void PlayFireSound()
        {
            if (currentWeapon.fireSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(currentWeapon.fireSound);
            }
        }

        private void PlayReloadSound()
        {
            if (currentWeapon.reloadSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(currentWeapon.reloadSound);
            }
        }

        public bool CanPickupAmmo(WeaponType weaponType)
        {
            return inventory.Contains(weaponType) && 
                   (weaponType == currentWeaponType ? totalAmmo < 999 : true);
        }

        public WeaponData GetCurrentWeapon()
        {
            return currentWeapon;
        }
    }
}