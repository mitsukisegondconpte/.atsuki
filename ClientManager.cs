using UnityEngine;
using Mirror;
using UnityEngine.UI;

namespace TsukiBR.Net
{
    public class ClientManager : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject connectionPanel;
        public InputField ipAddressInput;
        public Button hostButton;
        public Button joinButton;
        public Button disconnectButton;
        public Text statusText;

        [Header("Game UI")]
        public GameObject gameUI;
        public GameObject lobbyUI;

        private ServerManager serverManager;

        private void Start()
        {
            serverManager = FindObjectOfType<ServerManager>();
            
            if (serverManager == null)
            {
                Debug.LogError("ServerManager not found!");
                return;
            }

            SetupUI();
            UpdateConnectionStatus();
        }

        private void SetupUI()
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(StartHost);
            
            if (joinButton != null)
                joinButton.onClick.AddListener(JoinServer);
            
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(Disconnect);

            // Set default IP to local
            if (ipAddressInput != null)
                ipAddressInput.text = "localhost";
        }

        private void Update()
        {
            UpdateConnectionStatus();
        }

        private void UpdateConnectionStatus()
        {
            if (statusText == null) return;

            if (NetworkServer.active && NetworkClient.active)
            {
                statusText.text = $"Host - Players: {serverManager.GetConnectedPlayersCount()}, Bots: {serverManager.GetActiveBots()}";
                statusText.color = Color.green;
            }
            else if (NetworkClient.active)
            {
                statusText.text = NetworkClient.isConnected ? "Connected" : "Connecting...";
                statusText.color = NetworkClient.isConnected ? Color.green : Color.yellow;
            }
            else if (NetworkServer.active)
            {
                statusText.text = $"Server - Players: {serverManager.GetConnectedPlayersCount()}";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "Disconnected";
                statusText.color = Color.red;
            }

            // Show/hide UI panels
            bool isConnected = NetworkClient.active || NetworkServer.active;
            
            if (connectionPanel != null)
                connectionPanel.SetActive(!isConnected);
            
            if (gameUI != null)
                gameUI.SetActive(isConnected);
        }

        public void StartHost()
        {
            Debug.Log("Starting host...");
            serverManager.StartServer();
        }

        public void JoinServer()
        {
            string ipAddress = ipAddressInput != null ? ipAddressInput.text : "localhost";
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = "localhost";
            }

            Debug.Log($"Connecting to server: {ipAddress}");
            serverManager.StartClient(ipAddress);
        }

        public void Disconnect()
        {
            Debug.Log("Disconnecting...");
            serverManager.StopConnection();
        }

        // Called by NetworkManager events
        public void OnClientConnect()
        {
            Debug.Log("Connected to server");
        }

        public void OnClientDisconnect()
        {
            Debug.Log("Disconnected from server");
        }

        public void OnServerStart()
        {
            Debug.Log("Server started");
        }

        public void OnServerStop()
        {
            Debug.Log("Server stopped");
        }

        // Mobile-specific UI methods
        public void ShowMobileControls(bool show)
        {
            // Enable/disable touch controls for mobile
            var mobileControls = FindObjectOfType<MobileInputController>();
            if (mobileControls != null)
            {
                mobileControls.gameObject.SetActive(show);
            }
        }

        private void OnGUI()
        {
            // Emergency disconnect button
            if (NetworkClient.active || NetworkServer.active)
            {
                if (GUI.Button(new Rect(Screen.width - 120, 10, 110, 30), "Disconnect"))
                {
                    Disconnect();
                }
            }
        }

        // Split screen methods
        public void EnableSplitScreen()
        {
            // Setup split screen for local multiplayer
            var cameras = FindObjectsOfType<Camera>();
            
            if (cameras.Length >= 2)
            {
                // Set up two viewports
                cameras[0].rect = new Rect(0, 0.5f, 1, 0.5f); // Top half
                cameras[1].rect = new Rect(0, 0, 1, 0.5f);    // Bottom half
            }
        }

        public void DisableSplitScreen()
        {
            var cameras = FindObjectsOfType<Camera>();
            
            foreach (var camera in cameras)
            {
                camera.rect = new Rect(0, 0, 1, 1); // Full screen
            }
        }
    }

    // Mobile input controller for touch controls
    public class MobileInputController : MonoBehaviour
    {
        [Header("Touch Controls")]
        public RectTransform joystickArea;
        public RectTransform joystick;
        public Button jumpButton;
        public Button shootButton;
        public Button runButton;

        private Vector2 joystickInput;
        private bool isTouching;
        private int touchId = -1;

        private void Start()
        {
            // Only enable on mobile platforms
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            HandleTouch();
            
            // Apply input to player controller
            var playerController = FindObjectOfType<Player.PlayerController>();
            if (playerController != null && playerController.isLocalPlayer)
            {
                // Send touch input to player controller
                // This would need to be integrated with the PlayerController input system
            }
        }

        private void HandleTouch()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 touchPosition = touch.position;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickArea, touchPosition, null, out Vector2 localPoint))
                        {
                            if (joystickArea.rect.Contains(localPoint))
                            {
                                isTouching = true;
                                touchId = touch.fingerId;
                                UpdateJoystick(localPoint);
                            }
                        }
                        break;

                    case TouchPhase.Moved:
                        if (touch.fingerId == touchId)
                        {
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickArea, touchPosition, null, out localPoint))
                            {
                                UpdateJoystick(localPoint);
                            }
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touch.fingerId == touchId)
                        {
                            isTouching = false;
                            touchId = -1;
                            joystickInput = Vector2.zero;
                            joystick.localPosition = Vector2.zero;
                        }
                        break;
                }
            }
        }

        private void UpdateJoystick(Vector2 localPoint)
        {
            Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, joystickArea.rect.width * 0.5f);
            joystick.localPosition = clampedPoint;
            joystickInput = clampedPoint / (joystickArea.rect.width * 0.5f);
        }

        public Vector2 GetJoystickInput()
        {
            return joystickInput;
        }
    }
}