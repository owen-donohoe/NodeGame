using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Core;

namespace NodeWar.Network
{
    /// <summary>
    /// Pre-game lobby screen. Host/Join/Local buttons, IP text field, handshake flow.
    /// Lives as a scene object in the Lobby scene.
    /// On successful connection: creates MatchConnection (DontDestroyOnLoad),
    /// then loads the Gameplay scene.
    ///
    /// Flow:
    ///   Idle -> (Host clicked) -> Hosting (waiting for client handshake)
    ///   Idle -> (Join clicked) -> Joining (sending handshake, waiting for ack)
    ///   Either -> Connected -> creates MatchConnection, loads Gameplay scene
    ///   Idle -> (Local clicked) -> creates MatchConnection (no network), loads Gameplay scene
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        private enum LobbyState
        {
            Idle,
            Hosting,
            Joining,
            Connected
        }

        private LobbyState state = LobbyState.Idle;
        private NetworkManager networkManager;
        private GameObject networkManagerGO;
        private string joinIP = "192.168.1.";
        private string statusMessage = "";
        private float handshakeRetryTimer;
        private const float HANDSHAKE_RETRY_INTERVAL = 0.3f;
        private const float CONNECTION_TIMEOUT = 10f;
        private float stateEnterTime;

        private int localPlayerID;

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private void Start()
        {
            // If a MatchConnection already exists from a previous session
            // (e.g., returning to lobby after a match), clean it up.
            if (MatchConnection.Instance != null)
            {
                MatchConnection.Instance.Shutdown();
            }
        }

        private void Update()
        {
            switch (state)
            {
                case LobbyState.Hosting:
                    UpdateHosting();
                    break;
                case LobbyState.Joining:
                    UpdateJoining();
                    break;
            }
        }

        // ===== LOCAL PLAY =====

        private void StartLocalPlay()
        {
            localPlayerID = 0;

            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = false;
            mc.localPlayerID = 0;
            mc.networkManager = null;

            SceneManager.LoadScene("Gameplay");
        }

        // ======= BOT MATCH ======
        private void StartBotMatch()
        {
            localPlayerID = 0;

            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = false;
            mc.isBotMatch = true;
            mc.localPlayerID = 0;
            mc.networkManager = null;

            SceneManager.LoadScene("Gameplay");
        }

        // ===== HOSTING =====

        private void StartHosting()
        {
            CleanupNetworkManager();

            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();
            networkManager.StartAsHost();

            state = LobbyState.Hosting;
            stateEnterTime = Time.time;
            localPlayerID = 0;
            statusMessage = "Hosting on " + NetworkManager.GetLocalIPAddress() +
                            ":" + NetworkManager.DEFAULT_PORT + "\nWaiting for opponent...";
        }

        private void UpdateHosting()
        {
            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                PacketType type = InputSerializer.ReadPacketType(packets[i]);
                if (type == PacketType.Handshake)
                {
                    // Client connected - send ack
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());
                    networkManager.Send(InputSerializer.SerializeHandshakeAck());

                    OnConnected();
                    return;
                }
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusMessage = "No connection received. Try again.";
                CleanupNetworkManager();
                state = LobbyState.Idle;
            }
        }

        // ===== JOINING =====

        private void StartJoining()
        {
            CleanupNetworkManager();

            string ip = joinIP.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                statusMessage = "Enter a valid IP address.";
                return;
            }

            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();

            try
            {
                networkManager.StartAsClient(ip);
            }
            catch (System.Exception e)
            {
                statusMessage = "Invalid IP: " + e.Message;
                CleanupNetworkManager();
                return;
            }

            state = LobbyState.Joining;
            stateEnterTime = Time.time;
            handshakeRetryTimer = 0f;
            localPlayerID = 1;
            statusMessage = "Connecting to " + ip + "...";

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

            // Resend handshake periodically until ack received
            handshakeRetryTimer += Time.deltaTime;
            if (handshakeRetryTimer >= HANDSHAKE_RETRY_INTERVAL)
            {
                networkManager.Send(InputSerializer.SerializeHandshake());
                handshakeRetryTimer = 0f;
            }

            if (Time.time - stateEnterTime > CONNECTION_TIMEOUT)
            {
                statusMessage = "Connection timed out. Check IP and try again.";
                CleanupNetworkManager();
                state = LobbyState.Idle;
            }
        }

        // ===== CONNECTED =====

        private void OnConnected()
        {
            state = LobbyState.Connected;
            statusMessage = "Connected! Starting match...";
            Debug.Log("[Lobby] Connected. Local player: " + localPlayerID);

            // Create MatchConnection and parent NetworkManager under it
            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = true;
            mc.localPlayerID = localPlayerID;
            mc.networkManager = networkManager;

            // Parent NetworkManager GO under MatchConnection so DontDestroyOnLoad covers both
            networkManagerGO.transform.SetParent(mcGO.transform);

            // Clear local references (MatchConnection owns it now)
            networkManager = null;
            networkManagerGO = null;

            // Load gameplay scene
            SceneManager.LoadScene("Gameplay");
        }

        // ===== CLEANUP =====

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

        // ===== GUI =====

        private void OnGUI()
        {
            if (state == LobbyState.Connected) return;

            // Darken background
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float boxW = 340f;
            float boxH = 360f;
            float left = cx - boxW * 0.5f;
            float top = cy - boxH * 0.5f;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 28;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(left, top, boxW, 40f), "NODE WAR", titleStyle);

            top += 50f;

            if (state == LobbyState.Idle)
            {
                // Local Play button
                if (GUI.Button(new Rect(left + 20f, top, boxW - 40f, 40f), "LOCAL PLAY"))
                {
                    StartLocalPlay();
                }

                top += 50f;

                // Bot Match button
                if (GUI.Button(new Rect(left + 20f, top, boxW - 40f, 40f), "PLAY VS BOT"))
                {
                    StartBotMatch();
                }

                top += 60f;

                // Host button
                if (GUI.Button(new Rect(left + 20f, top, boxW - 40f, 40f), "HOST GAME"))
                {
                    StartHosting();
                }

                top += 60f;

                // IP field
                GUI.Label(new Rect(left + 20f, top, boxW - 40f, 20f), "Host IP:");
                top += 22f;
                joinIP = GUI.TextField(new Rect(left + 20f, top, boxW - 40f, 28f), joinIP);
                top += 38f;

                // Join button
                if (GUI.Button(new Rect(left + 20f, top, boxW - 40f, 40f), "JOIN GAME"))
                {
                    StartJoining();
                }

                top += 60f;
            }

            // Status message
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 14;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            statusStyle.normal.textColor = new Color(0.8f, 0.8f, 0.5f);
            statusStyle.wordWrap = true;
            GUI.Label(new Rect(left, top, boxW, 60f), statusMessage, statusStyle);

            // Cancel button when hosting/joining
            if (state == LobbyState.Hosting || state == LobbyState.Joining)
            {
                top += 70f;
                if (GUI.Button(new Rect(left + 60f, top, boxW - 120f, 30f), "CANCEL"))
                {
                    CleanupNetworkManager();
                    state = LobbyState.Idle;
                    statusMessage = "";
                }
            }
        }
    }
}