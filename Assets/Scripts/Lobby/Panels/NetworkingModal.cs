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

        [Header("Idle State (Host/Join selection)")]
        [SerializeField] private GameObject idlePanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField ipInputField;

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

        private void OnHostClicked()
        {
            CleanupNetworkManager();

            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();
            networkManager.StartAsHost();

            state = NetState.Hosting;
            stateEnterTime = Time.time;
            localPlayerID = 0;

            string localIP = NetworkManager.GetLocalIPAddress();
            ShowActiveState("Hosting on " + localIP + ":" + NetworkManager.DEFAULT_PORT +
                "\nWaiting for opponent...");
        }

        private void UpdateHosting()
        {
            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                PacketType type = InputSerializer.ReadPacketType(packets[i]);
                if (type == PacketType.Handshake)
                {
                    // Client connected — send ack x3
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());

                    OnConnected();
                    return;
                }
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusText.text = "No connection received.\nTry again.";
                CleanupNetworkManager();
                ShowIdleState();
            }
        }

        // ===== JOIN =====

        private void OnJoinClicked()
        {
            string ip = ipInputField.text.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                statusText.text = "Enter a valid IP address.";
                return;
            }

            CleanupNetworkManager();

            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();

            try
            {
                networkManager.StartAsClient(ip);
            }
            catch (System.Exception e)
            {
                ShowActiveState("Invalid IP: " + e.Message);
                CleanupNetworkManager();
                ShowIdleState();
                return;
            }

            state = NetState.Joining;
            stateEnterTime = Time.time;
            handshakeRetryTimer = 0f;
            localPlayerID = 1;

            ShowActiveState("Connecting to " + ip + "...");

            // Send first handshake immediately
            networkManager.Send(InputSerializer.SerializeHandshake());
        }

        private void UpdateJoining()
        {
            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                PacketType type = InputSerializer.ReadPacketType(packets[i]);
                if (type == PacketType.HandshakeAck)
                {
                    OnConnected();
                    return;
                }
            }

            // Resend handshake periodically
            handshakeRetryTimer += Time.deltaTime;
            if (handshakeRetryTimer >= HANDSHAKE_RETRY_INTERVAL)
            {
                networkManager.Send(InputSerializer.SerializeHandshake());
                handshakeRetryTimer = 0f;
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusText.text = "Connection timed out.\nCheck IP and try again.";
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
        }
    }
}