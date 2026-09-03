using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Core;
using NodeWar.Network;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The networked match connection flow, with no UI attached.
    ///
    /// This is the same handshake NetworkingModal performs, deliberately
    /// reimplemented rather than shared: refactoring NetworkingModal to extract
    /// it would mean editing the shipped lobby during a migration whose whole
    /// safety story is that the old path is untouched until S5. The duplication
    /// is temporary and dies with NetworkingModal.
    ///
    /// Flow, unchanged from the shipped one because both peers must agree:
    ///   Host: create room, wait for a Handshake, reply HandshakeAck x3
    ///   Join: wait for transport, send Handshake every 0.3s until HandshakeAck
    ///   Both: create MatchConnection, parent the NetworkManager under it, load Gameplay
    ///
    /// What is NOT the same is failure handling. NetworkManager.StartAsRelayHost
    /// and StartAsRelayClient are `async void` with no try/catch, so a failed
    /// UGS sign-in or a wrong join code throws into nothing: relayReady simply
    /// stays false forever. The shipped modal cannot tell that apart from "the
    /// opponent is slow", so it shows one 30-second "Timed out." for both.
    ///
    /// Here the two are separate phases with separate deadlines, so a wrong code
    /// fails in a few seconds saying it might be wrong, and waiting for a friend
    /// does not time out at all. That is the difference between a message and a
    /// recovery action.
    /// </summary>
    public class MatchLauncher
    {
        public enum Phase
        {
            Idle,
            CreatingRoom,       // relay allocation / auth in flight
            WaitingForOpponent, // host is up, code is known, nobody has joined
            Connecting,         // client is handshaking
            Connected,          // about to load Gameplay
            Failed
        }

        /// <summary>
        /// How long relay setup may take before we call it broken. UGS sign-in
        /// plus an allocation is normally two or three seconds; twelve is
        /// generous without leaving someone staring at a wrong join code for
        /// half a minute.
        /// </summary>
        private const float RelaySetupTimeout = 12f;

        /// <summary>
        /// How long a client waits for the host to answer its handshake once the
        /// transport is up. Matches the shipped modal.
        /// </summary>
        private const float HandshakeTimeout = 30f;

        private const float HandshakeRetryInterval = 0.3f;

        public Phase CurrentPhase { get; private set; }
        public string JoinCode { get; private set; }

        /// <summary>What went wrong. Empty unless CurrentPhase is Failed.</summary>
        public string FailureMessage { get; private set; }

        /// <summary>What the player can do about it. Never empty when FailureMessage is not.</summary>
        public string FailureRecovery { get; private set; }

        /// <summary>Raised whenever anything above changes, so the UI can redraw.</summary>
        public event Action Changed;

        private NetworkManager networkManager;
        private GameObject networkManagerGO;
        private int localPlayerID;
        private bool useLan;

        private float phaseEnterTime;
        private float handshakeRetryTimer;

        public bool IsBusy
        {
            get { return CurrentPhase != Phase.Idle && CurrentPhase != Phase.Failed; }
        }

        // ===== ENTRY POINTS =====

        public void HostRelay()
        {
            BeginHost(false, null);
        }

        public void HostLan()
        {
            BeginHost(true, null);
        }

        /// <summary>
        /// Join by relay code. Codes are case-insensitive to type and
        /// upper-cased before use, matching the shipped modal.
        /// </summary>
        public void JoinRelay(string joinCode)
        {
            string code = (joinCode ?? "").Trim();

            if (code.Length == 0)
            {
                Fail("No join code entered.", "Ask the host for their code and type it above.");
                return;
            }

            BeginJoin(false, code.ToUpperInvariant());
        }

        public void JoinLan(string address)
        {
            string ip = (address ?? "").Trim();

            if (ip.Length == 0)
            {
                Fail("No address entered.", "Type the host's local IP address above.");
                return;
            }

            BeginJoin(true, ip);
        }

        public void Cancel()
        {
            Cleanup();
            CurrentPhase = Phase.Idle;
            JoinCode = null;
            ClearFailure();
            Notify();
        }

        // ===== HOST =====

        private void BeginHost(bool lan, string _)
        {
            Cleanup();
            ClearFailure();

            useLan = lan;
            localPlayerID = 0;
            CreateNetworkManager();

            if (lan)
            {
                networkManager.StartAsHost();
                JoinCode = NetworkManager.GetLocalIPAddress();
                EnterPhase(Phase.WaitingForOpponent);
            }
            else
            {
                networkManager.StartAsRelayHost();
                JoinCode = null;
                EnterPhase(Phase.CreatingRoom);
            }
        }

        // ===== JOIN =====

        private void BeginJoin(bool lan, string address)
        {
            Cleanup();
            ClearFailure();

            useLan = lan;
            localPlayerID = 1;
            CreateNetworkManager();

            if (lan)
            {
                // StartAsClient parses the IP and will throw on a malformed one.
                // This is the one connection failure the shipped code already
                // surfaces, and it is worth keeping fast.
                try
                {
                    networkManager.StartAsClient(address);
                }
                catch (Exception e)
                {
                    Cleanup();
                    Fail("That is not a valid IP address.",
                         "Check the host's address and try again. (" + e.GetType().Name + ")");
                    return;
                }

                JoinCode = address;
                handshakeRetryTimer = HandshakeRetryInterval; // send one immediately
                EnterPhase(Phase.Connecting);
            }
            else
            {
                networkManager.StartAsRelayClient(address);
                JoinCode = address;
                EnterPhase(Phase.CreatingRoom);
            }
        }

        // ===== PUMP =====

        public void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.CreatingRoom:
                    UpdateCreatingRoom();
                    break;
                case Phase.WaitingForOpponent:
                    UpdateWaitingForOpponent();
                    break;
                case Phase.Connecting:
                    UpdateConnecting();
                    break;
            }
        }

        /// <summary>
        /// Relay allocation and UGS sign-in. Because those run in an async void
        /// that swallows its exceptions, the only signal available here is
        /// "relayReady still false", so a deadline is the only way to notice.
        /// </summary>
        private void UpdateCreatingRoom()
        {
            if (networkManager == null) return;

            if (networkManager.RelayReady)
            {
                if (networkManager.IsHost)
                {
                    JoinCode = networkManager.JoinCode;
                    EnterPhase(Phase.WaitingForOpponent);
                }
                else
                {
                    handshakeRetryTimer = HandshakeRetryInterval;
                    EnterPhase(Phase.Connecting);
                }
                return;
            }

            if (Time.time - phaseEnterTime <= RelaySetupTimeout) return;

            Cleanup();

            if (localPlayerID == 0)
            {
                Fail("Couldn't create a room.",
                     "Check your internet connection, then try hosting again. " +
                     "You can also use a local network match if you are on the same Wi-Fi.");
            }
            else
            {
                Fail("Couldn't join that room.",
                     "The code may be wrong or expired. Check it with the host and try again.");
            }
        }

        /// <summary>
        /// The host has a code and is waiting. Deliberately no deadline: someone
        /// reading a code out to a friend should not lose the room because they
        /// took thirty seconds. Cancel is the way out.
        /// </summary>
        private void UpdateWaitingForOpponent()
        {
            if (networkManager == null) return;

            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                if (InputSerializer.ReadPacketType(packets[i]) != PacketType.Handshake) continue;

                // Three acks, as the shipped flow does: the reply is unreliable
                // and a dropped ack would strand the client.
                networkManager.Send(InputSerializer.SerializeHandshakeAck());
                networkManager.Send(InputSerializer.SerializeHandshakeAck());
                networkManager.Send(InputSerializer.SerializeHandshakeAck());

                Succeed();
                return;
            }
        }

        private void UpdateConnecting()
        {
            if (networkManager == null) return;

            // On LAN the socket is usable immediately. On relay the transport
            // has to come up first, and sending before then would be dropped.
            bool transportReady = useLan || networkManager.IsConnected;

            if (transportReady)
            {
                handshakeRetryTimer += Time.deltaTime;

                if (handshakeRetryTimer >= HandshakeRetryInterval)
                {
                    networkManager.Send(InputSerializer.SerializeHandshake());
                    handshakeRetryTimer = 0f;
                }
            }

            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                if (InputSerializer.ReadPacketType(packets[i]) != PacketType.HandshakeAck) continue;

                Succeed();
                return;
            }

            if (Time.time - phaseEnterTime <= HandshakeTimeout) return;

            Cleanup();
            Fail("The host didn't answer.",
                 "Make sure they still have the room open, then try the code again.");
        }

        // ===== SUCCESS =====

        private void Succeed()
        {
            CurrentPhase = Phase.Connected;
            Notify();

            GameObject matchGO = new GameObject("MatchConnection");
            MatchConnection match = matchGO.AddComponent<MatchConnection>();
            match.isNetworked = true;
            match.isBotMatch = false;
            match.localPlayerID = localPlayerID;
            match.networkManager = networkManager;

            if (PlayerProfile.Instance != null)
                match.loadout = PlayerProfile.Instance.Loadout;

            // MatchConnection owns the NetworkManager from here, and is
            // DontDestroyOnLoad, so parenting is what carries the socket across
            // the scene load.
            networkManagerGO.transform.SetParent(matchGO.transform);

            networkManager = null;
            networkManagerGO = null;

            SceneManager.LoadScene("Gameplay");
        }

        // ===== PLUMBING =====

        private void CreateNetworkManager()
        {
            networkManagerGO = new GameObject("NetworkManager");
            networkManager = networkManagerGO.AddComponent<NetworkManager>();
        }

        private void EnterPhase(Phase phase)
        {
            CurrentPhase = phase;
            phaseEnterTime = Time.time;
            Notify();
        }

        private void Fail(string message, string recovery)
        {
            CurrentPhase = Phase.Failed;
            FailureMessage = message;
            FailureRecovery = recovery;
            Notify();
        }

        private void ClearFailure()
        {
            FailureMessage = null;
            FailureRecovery = null;
        }

        private void Cleanup()
        {
            if (networkManager != null)
            {
                networkManager.Shutdown();
                networkManager = null;
            }

            if (networkManagerGO != null)
            {
                UnityEngine.Object.Destroy(networkManagerGO);
                networkManagerGO = null;
            }
        }

        /// <summary>Called when the owning UI goes away, so a socket is never orphaned.</summary>
        public void Dispose()
        {
            Cleanup();
            CurrentPhase = Phase.Idle;
        }

        private void Notify()
        {
            if (Changed != null) Changed();
        }
    }
}
