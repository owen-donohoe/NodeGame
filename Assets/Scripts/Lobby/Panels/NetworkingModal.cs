using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using NodeWar.Core;
using NodeWar.Network;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Modal overlay for 1v1 online match connection.
    /// Reproduces the exact handshake flow from the old LobbyUI.cs:
    ///   Host: opens port 7777, waits for Handshake packet, sends HandshakeAck x3
    ///   Join: sends Handshake every 0.3s until HandshakeAck received
    ///   Both: 10s timeout, on success creates MatchConnection and loads Gameplay
    /// </summary>
    public class NetworkingModal : MonoBehaviour
    {
        private enum NetState
        {
            Idle,
            Hosting,
            Joining,
            Connected
        }

        [Header("Root")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Color active = new Color(0.25f, 0.35f, 0.55f, 1f);
        [SerializeField] private Color inactive = new Color(0.16f, 0.16f, 0.23f, 1f);

        [Header("Idle State (Host/Join selection)")]
        [SerializeField] private GameObject idlePanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField ipInputField;

        private bool useLAN = false;
        [SerializeField] private Button onlineModeButton;
        [SerializeField] private Button lanModeButton;
        [SerializeField] private Image onlineModeImage;
        [SerializeField] private Image lanModeImage;

        [Header("Active State (waiting/connecting)")]
        [SerializeField] private GameObject activePanel;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button cancelButton;



        private NetState state = NetState.Idle;
        private NetworkManager networkManager;
        private GameObject networkManagerGO;
        private int localPlayerID;
        private float stateEnterTime;
        private float handshakeRetryTimer;

        private const float HANDSHAKE_RETRY_INTERVAL = 0.3f;
        private const float CONNECTION_TIMEOUT = 30f;

        private void Awake()
        {
            if (modalRoot != null)
                modalRoot.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
            if (onlineModeButton != null) 
                onlineModeButton.onClick.AddListener(() => SetMode(false));
            if (lanModeButton != null) 
                lanModeButton.onClick.AddListener(() => SetMode(true));

            SetMode(false);
        }

        public void Open()
        {
            modalRoot.SetActive(true);
            ShowIdleState();
        }

        public void Close()
        {
            CleanupNetworkManager();
            state = NetState.Idle;
            modalRoot.SetActive(false);
        }

        private void Update()
        {
            if (modalRoot == null || !modalRoot.activeSelf) return;

            switch (state)
            {
                case NetState.Hosting:
                    UpdateHosting();
                    break;
                case NetState.Joining:
                    UpdateJoining();
                    break;
            }
        }

        // ===== STATE DISPLAY =====

        private void ShowIdleState()
        {
            state = NetState.Idle;
            idlePanel.SetActive(true);
            activePanel.SetActive(false);
        }

        private void ShowActiveState(string status)
        {
            idlePanel.SetActive(false);
            activePanel.SetActive(true);
            statusText.text = status;
        }

        // ===== HOST =====

        private void SetMode(bool lan)
        {
            useLAN = lan;
            UpdateModeButtons();
        }

        private void UpdateModeButtons() {
            
            if (onlineModeImage != null) onlineModeImage.color = useLAN ? inactive : active;
            if (lanModeImage != null) lanModeImage.color = useLAN ? active : inactive;
            if (ipInputField != null)
                ipInputField.placeholder.GetComponent<TMPro.TextMeshProUGUI>().text = useLAN ? "Enter IP" : "Enter Join Code";
        }

        private void OnHostClicked()
        {
            CleanupNetworkManager();
            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();

            if (useLAN)
            {
                networkManager.StartAsHost();
                state = NetState.Hosting;
                stateEnterTime = Time.time;
                localPlayerID = 0;
                ShowActiveState("Hosting on " + NetworkManager.GetLocalIPAddress() + "\nWaiting for opponent...");
            }
            else
            {
                networkManager.StartAsRelayHost();
                state = NetState.Hosting;
                stateEnterTime = Time.time;
                localPlayerID = 0;
                ShowActiveState("Creating room...");
            }
        }

        private void UpdateHosting()
        {
            if (!useLAN && !networkManager.RelayReady)
            {
                statusText.text = "Creating room...";
                return;
            }

            if (!useLAN && !networkManager.IsConnected)
                statusText.text = "Join Code: " + networkManager.JoinCode + "\nWaiting for opponent...";

            byte[][] packets = networkManager.ReceiveAll();
            for (int i = 0; i < packets.Length; i++)
            {
                if (InputSerializer.ReadPacketType(packets[i]) == PacketType.Handshake)
                {
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    OnConnected();
                    return;
                }
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusText.text = "Timed out. Try again.";
                CleanupNetworkManager();
                ShowIdleState();
            }
        }

        // ===== JOIN =====

        private void OnJoinClicked()
        {
            string input = ipInputField.text.Trim();
            if (string.IsNullOrEmpty(input)) { statusText.text = useLAN ? "Enter an IP address." : "Enter a join code."; return; }

            CleanupNetworkManager();
            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();

            if (useLAN)
            {
                try { networkManager.StartAsClient(input); }
                catch (System.Exception e) { ShowActiveState("Invalid IP: " + e.Message); CleanupNetworkManager(); ShowIdleState(); return; }

                state = NetState.Joining;
                stateEnterTime = Time.time;
                handshakeRetryTimer = 0f;
                localPlayerID = 1;
                ShowActiveState("Connecting to " + input + "...");
                networkManager.Send(InputSerializer.SerializeHandshake());
            }
            else
            {
                networkManager.StartAsRelayClient(input.ToUpper());
                state = NetState.Joining;
                stateEnterTime = Time.time;
                localPlayerID = 1;
                ShowActiveState("Connecting...");
            }
        }

        private void UpdateJoining()
        {
            if (!useLAN && !networkManager.RelayReady) return;

            // LAN: retry handshake on interval (original behaviour)
            // Relay: wait for transport connection then send handshake
            bool shouldSendHandshake = useLAN || networkManager.IsConnected;

            if (shouldSendHandshake)
            {
                handshakeRetryTimer += Time.deltaTime;
                if (handshakeRetryTimer >= HANDSHAKE_RETRY_INTERVAL)
                {
                    networkManager.Send(InputSerializer.SerializeHandshake());
                    handshakeRetryTimer = 0f;
                }
            }

            byte[][] packets = networkManager.ReceiveAll();
            for (int i = 0; i < packets.Length; i++)
            {
                if (InputSerializer.ReadPacketType(packets[i]) == PacketType.HandshakeAck)
                {
                    OnConnected();
                    return;
                }
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusText.text = "Timed out.";
                CleanupNetworkManager();
                ShowIdleState();
            }
        }

        // ===== CONNECTED =====

        private void OnConnected()
        {
            state = NetState.Connected;
            statusText.text = "Connected! Starting match...";
            Debug.Log("[NetworkingModal] Connected. Local player: " + localPlayerID);

            // Create MatchConnection
            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = true;
            mc.localPlayerID = localPlayerID;
            mc.networkManager = networkManager;

            // Pass loadout
            if (PlayerProfile.Instance != null)
                mc.loadout = PlayerProfile.Instance.Loadout;

            // Parent NetworkManager under MatchConnection for DontDestroyOnLoad coverage
            networkManagerGO.transform.SetParent(mcGO.transform);

            // Clear local references (MatchConnection owns it now)
            networkManager = null;
            networkManagerGO = null;

            SceneManager.LoadScene("Gameplay");
        }

        // ===== CANCEL / CLEANUP =====

        private void OnCancelClicked()
        {
            CleanupNetworkManager();
            ShowIdleState();
        }

        private void CleanupNetworkManager()
        {
            if (networkManager != null)
            {
                networkManager.Shutdown();
                networkManager = null;
            }

            if (networkManagerGO != null)
            {
                Destroy(networkManagerGO);
                networkManagerGO = null;
            }
        }

        private void OnDestroy()
        {
            CleanupNetworkManager();
            if (closeButton != null) closeButton.onClick.RemoveAllListeners();
            if (hostButton != null) hostButton.onClick.RemoveAllListeners();
            if (joinButton != null) joinButton.onClick.RemoveAllListeners();
            if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
            if (onlineModeButton != null) onlineModeButton.onClick.RemoveAllListeners();
            if (lanModeButton != null) lanModeButton.onClick.RemoveAllListeners();
        }
    }
}