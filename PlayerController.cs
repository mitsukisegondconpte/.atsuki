using UnityEngine;
using Mirror;
using System.Collections;

namespace TsukiBR.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        public float walkSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 2f;
        public float gravity = -20f;
        public float slideSpeed = 10f;
        public float slideDuration = 1f;

        [Header("Camera Settings")]
        public Transform cameraFollowTarget;
        public float mouseSensitivity = 2f;
        public float verticalLookLimit = 80f;

        [Header("Player Stats")]
        [SyncVar] public float health = 100f;
        [SyncVar] public int kills = 0;
        [SyncVar] public bool isAlive = true;

        private CharacterController characterController;
        private Animator animator;
        private Vector3 velocity;
        private bool isGrounded;
        private bool isRunning;
        private bool isSliding;
        private float slideTimer;
        private float verticalRotation;

        // Input variables
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpInput;
        private bool runInput;
        private bool slideInput;
        private bool shootInput;

        [Header("Player ID")]
        [SyncVar] public string playerUID;

        private Combat.WeaponSystem weaponSystem;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            weaponSystem = GetComponent<Combat.WeaponSystem>();

            if (isLocalPlayer)
            {
                // Generate or load unique player ID
                playerUID = GetPlayerUID();
                CmdSetPlayerUID(playerUID);

                // Set up camera for local player
                SetupCamera();
            }
            else
            {
                // Disable camera for remote players
                if (cameraFollowTarget != null)
                    cameraFollowTarget.GetComponent<Camera>()?.gameObject.SetActive(false);
            }
        }

        private string GetPlayerUID()
        {
            string uid = PlayerPrefs.GetString("PlayerUID", "");
            if (string.IsNullOrEmpty(uid))
            {
                uid = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("PlayerUID", uid);
                PlayerPrefs.Save();
            }
            return uid;
        }

        [Command]
        private void CmdSetPlayerUID(string uid)
        {
            playerUID = uid;
        }

        private void SetupCamera()
        {
            if (cameraFollowTarget != null)
            {
                var camera = cameraFollowTarget.GetComponent<Camera>();
                if (camera == null)
                {
                    camera = cameraFollowTarget.gameObject.AddComponent<Camera>();
                }
                camera.enabled = true;
            }
        }

        private void Update()
        {
            if (!isLocalPlayer || !isAlive) return;

            HandleInput();
            HandleMovement();
            HandleLook();
            HandleShooting();
            UpdateAnimator();
        }

        private void HandleInput()
        {
            // Movement input
            moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            
            // Look input
            lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            
            // Action inputs
            jumpInput = Input.GetKeyDown(KeyCode.Space);
            runInput = Input.GetKey(KeyCode.LeftShift);
            slideInput = Input.GetKeyDown(KeyCode.LeftControl);
            shootInput = Input.GetMouseButton(0);

            // Mobile touch input handling could be added here
        }

        private void HandleMovement()
        {
            isGrounded = characterController.isGrounded;

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            
            if (isSliding)
            {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0)
                {
                    isSliding = false;
                }
                move = transform.forward * slideSpeed;
            }
            else
            {
                float currentSpeed = (runInput && move.magnitude > 0) ? runSpeed : walkSpeed;
                isRunning = runInput && move.magnitude > 0;
                move *= currentSpeed;

                if (slideInput && isGrounded && move.magnitude > 0)
                {
                    isSliding = true;
                    slideTimer = slideDuration;
                }
            }

            characterController.Move(move * Time.deltaTime);

            // Jump
            if (jumpInput && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }

        private void HandleLook()
        {
            // Horizontal rotation
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);

            // Vertical rotation (camera)
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);
            
            if (cameraFollowTarget != null)
            {
                cameraFollowTarget.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
        }

        private void HandleShooting()
        {
            if (shootInput && weaponSystem != null)
            {
                weaponSystem.TryShoot();
            }
        }

        private void UpdateAnimator()
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", moveInput.magnitude);
                animator.SetBool("IsRunning", isRunning);
                animator.SetBool("IsGrounded", isGrounded);
                animator.SetBool("IsSliding", isSliding);
            }
        }

        [Command]
        public void CmdTakeDamage(float damage)
        {
            if (!isAlive) return;

            health -= damage;
            if (health <= 0)
            {
                health = 0;
                isAlive = false;
                RpcPlayerDied();
            }
        }

        [ClientRpc]
        private void RpcPlayerDied()
        {
            if (isLocalPlayer)
            {
                // Show death screen, spectate mode, etc.
                Debug.Log("Player died!");
            }
            
            // Disable player controls and show death animation
            gameObject.SetActive(false);
        }

        public void Heal(float amount)
        {
            if (!isAlive) return;
            
            health = Mathf.Min(health + amount, 100f);
        }

        [Command]
        public void CmdAddKill()
        {
            kills++;
        }

        private void OnDrawGizmos()
        {
            // Draw player bounds for debugging
            Gizmos.color = isAlive ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }
    }
}