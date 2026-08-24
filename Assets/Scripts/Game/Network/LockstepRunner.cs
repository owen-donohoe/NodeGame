using UnityEngine;
using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.Network
{
    /// <summary>
    /// Replaces TickRunner for networked play.
    /// Same accumulator loop, but stalls until both local and remote inputs
    /// are available for the current tick before calling SimulateTick.
    /// Enforces command processing order: P0 first, P1 second, then simulate.
    /// Stamps local inputs for tick N+INPUT_DELAY to hide network latency.
    /// </summary>
    public class LockstepRunner : MonoBehaviour, NodeWar.Core.ITickProvider
    {
        private const int INPUT_DELAY = 2;
        private const int DESYNC_CHECK_INTERVAL = 50;
        private const float DISCONNECT_TIMEOUT = 2.0f;
        private const float HEARTBEAT_INTERVAL = 0.5f;
        private const int MAX_ACCUMULATOR_TICKS = 3;
        private const float RESEND_INTERVAL = 0.05f;

        [Header("Tick Settings")]
        public int ticksPerSecond = 10;

        private float tickInterval;
        private float accumulator;

        // Dependencies
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private NetworkManager networkManager;
        private int localPlayerID; // 0 for host, 1 for joiner

        // Tick tracking
        private int simulationTick;  // next tick to simulate
        private int nextInputTick;   // next forTick value for local input generation

        // Input storage (keyed by forTick)
        private Dictionary<int, TickInput> localInputs = new Dictionary<int, TickInput>();
        private Dictionary<int, TickInput> remoteInputs = new Dictionary<int, TickInput>();

        // Timing
        private float lastSendTime;
        private float lastReceiveTime;
        private float lastHeartbeatTime;

        // Resend
        private byte[] lastSentPacket;

        // Desync tracking
        private int pendingHash;
        private int pendingHashTick;
        private Dictionary<int, int> localHashes = new Dictionary<int, int>();

        // Public events for LobbyUI / GameManager to hook
        public System.Action OnDisconnect;
        public System.Action<int> OnDesync; // tick number where desync detected

        private bool paused = true;

        public void Unpause()
        {
            paused = false;
        }


        /// <summary>
        /// Normalized progress (0-1) between last tick and next tick.
        /// Used by View layer for interpolation. Same contract as TickRunner.TickAlpha.
        /// </summary>
        public float TickAlpha
        {
            get { return Mathf.Clamp01(accumulator / tickInterval); }
        }

        public void Initialize(SimulationState state, InputBuffer buffer,
            NetworkManager netManager, int playerID)
        {
            simState = state;
            inputBuffer = buffer;
            networkManager = netManager;
            localPlayerID = playerID;

            tickInterval = 1f / ticksPerSecond;
            accumulator = 0f;
            simulationTick = 0;
            nextInputTick = INPUT_DELAY;

            float now = Time.time;
            lastSendTime = now;
            lastReceiveTime = now;
            lastHeartbeatTime = now;

            pendingHash = 0;
            pendingHashTick = 0;

            // Pre-seed empty inputs for ticks 0 through INPUT_DELAY-1.
            // Both machines do this identically, so ticks 0 and 1 are
            // immediately simulatable without waiting for network.
            for (int t = 0; t < INPUT_DELAY; t++)
            {
                TickInput empty = new TickInput
                {
                    forTick = t,
                    stateHash = 0,
                    commands = new GameCommand[0]
                };
                localInputs[t] = empty;
                remoteInputs[t] = empty;
            }
        }

        private void Update()
        {
            if (simState == null || networkManager == null) return;
            if (paused) return;
            if (simState.gameOver) return;

            ProcessIncomingPackets();

            if (CheckDisconnect()) return;

            accumulator += Time.deltaTime;

            // Advance simulation as many ticks as possible
            while (accumulator >= tickInterval)
            {
                if (HasBothInputs(simulationTick))
                {
                    GenerateAndSendLocalInput();
                    ExecuteTick(simulationTick);
                    simulationTick++;
                    accumulator -= tickInterval;
                }
                else
                {
                    // Stall: remote input not yet received for this tick
                    ResendIfNeeded();
                    SendHeartbeatIfNeeded();

                    // Cap accumulator to prevent death spiral on resume
                    if (accumulator > tickInterval * MAX_ACCUMULATOR_TICKS)
                        accumulator = tickInterval * MAX_ACCUMULATOR_TICKS;
                    break;
                }
            }

            SendHeartbeatIfNeeded();
        }

        // ===== INPUT GENERATION =====

        private void GenerateAndSendLocalInput()
        {
            // Flush all commands accumulated since last tick
            GameCommand[] commands = inputBuffer.DrainCommands();

            // Attach pending hash if one was computed after last tick
            int hash = pendingHash;
            pendingHash = 0;

            TickInput input = new TickInput
            {
                forTick = nextInputTick,
                stateHash = hash,
                commands = commands
            };

            // Store locally (we'll need it when simulationTick reaches nextInputTick)
            localInputs[nextInputTick] = input;

            // Serialize and send
            byte[] packet = InputSerializer.Serialize(input);
            networkManager.Send(packet);
            lastSentPacket = packet;
            lastSendTime = Time.time;

            nextInputTick++;
        }

        // ===== TICK EXECUTION =====

        private void ExecuteTick(int tick)
        {
            TickInput local = localInputs[tick];
            TickInput remote = remoteInputs[tick];

            // Enforce command processing order contract:
            // ALL P0 commands (in issue order), then ALL P1 commands (in issue order)
            GameCommand[] p0Commands;
            GameCommand[] p1Commands;

            if (localPlayerID == 0)
            {
                p0Commands = local.commands;
                p1Commands = remote.commands;
            }
            else
            {
                p0Commands = remote.commands;
                p1Commands = local.commands;
            }

            for (int i = 0; i < p0Commands.Length; i++)
                CommandProcessor.ProcessCommand(simState, p0Commands[i]);

            for (int i = 0; i < p1Commands.Length; i++)
                CommandProcessor.ProcessCommand(simState, p1Commands[i]);

            // Advance simulation
            GameSimulation.SimulateTick(simState);

            // Desync hash: compute after tick completes, store for next outgoing packet
            if (tick > 0 && tick % DESYNC_CHECK_INTERVAL == 0)
            {
                int computedHash = SimulationStateHasher.ComputeHash(simState);
                localHashes[tick] = computedHash;
                pendingHash = computedHash;
                pendingHashTick = tick;
                Debug.Log("[LOCKSTEP] Tick " + tick + " Hash: " + computedHash);
            }

            // Compare remote's hash if they sent one
            if (remote.stateHash != 0)
            {
                CompareHash(remote.stateHash);
            }

            // Memory cleanup
            CleanupOldInputs(tick);
        }

        // ===== NETWORK RECEIVE =====

        private void ProcessIncomingPackets()
        {
            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                if (packets[i] == null || packets[i].Length == 0) continue;

                lastReceiveTime = Time.time;
                PacketType type = InputSerializer.ReadPacketType(packets[i]);

                switch (type)
                {
                    case PacketType.TickInput:
                        TickInput remote = InputSerializer.Deserialize(packets[i]);
                        // Store if not already received (ignore duplicate resends)
                        if (!remoteInputs.ContainsKey(remote.forTick))
                        {
                            remoteInputs[remote.forTick] = remote;
                        }
                        break;

                    case PacketType.Heartbeat:
                        // lastReceiveTime already updated above
                        break;
                }
            }
        }

        // ===== DESYNC =====

        /// <summary>
        /// Compare a received hash against our most recent local hash.
        /// Both machines compute hashes at the same simulation tick (lockstep guarantees this).
        /// The remote's hash was computed after the same tick our most recent hash was.
        /// </summary>
        private void CompareHash(int remoteHash)
        {
            // Find the most recent local hash to compare against.
            // In lockstep, both sides hash after the same tick, so pendingHashTick
            // (or the last stored hash tick) should match.
            if (pendingHashTick > 0 && localHashes.ContainsKey(pendingHashTick))
            {
                int localHash = localHashes[pendingHashTick];
                if (localHash != remoteHash)
                {
                    Debug.LogError("[DESYNC] Tick " + pendingHashTick +
                        " Local: " + localHash + " Remote: " + remoteHash);
                    OnDesync?.Invoke(pendingHashTick);
                }
            }
        }

        // ===== RESEND / HEARTBEAT / DISCONNECT =====

        private void ResendIfNeeded()
        {
            if (lastSentPacket == null) return;
            if (Time.time - lastSendTime < RESEND_INTERVAL) return;

            networkManager.Send(lastSentPacket);
            lastSendTime = Time.time;
        }

        private void SendHeartbeatIfNeeded()
        {
            if (Time.time - lastHeartbeatTime < HEARTBEAT_INTERVAL) return;

            networkManager.Send(InputSerializer.SerializeHeartbeat());
            lastHeartbeatTime = Time.time;
        }

        /// <summary>
        /// Returns true if disconnected (caller should abort frame).
        /// </summary>
        private bool CheckDisconnect()
        {
            if (Time.time - lastReceiveTime > DISCONNECT_TIMEOUT)
            {
                Debug.LogError("[LOCKSTEP] Opponent disconnected (no data for " +
                    DISCONNECT_TIMEOUT + "s).");
                OnDisconnect?.Invoke();
                enabled = false; // stop processing
                return true;
            }
            return false;
        }

        // ===== HELPERS =====

        private bool HasBothInputs(int tick)
        {
            return localInputs.ContainsKey(tick) && remoteInputs.ContainsKey(tick);
        }

        /// <summary>
        /// Remove stored inputs older than 10 ticks to prevent unbounded memory growth.
        /// </summary>
        private void CleanupOldInputs(int completedTick)
        {
            int cutoff = completedTick - 10;
            if (cutoff < 0) return;

            // Collect keys to remove (cannot modify during enumeration)
            List<int> keysToRemove = new List<int>();

            foreach (int key in localInputs.Keys)
                if (key <= cutoff) keysToRemove.Add(key);
            for (int i = 0; i < keysToRemove.Count; i++)
                localInputs.Remove(keysToRemove[i]);

            keysToRemove.Clear();
            foreach (int key in remoteInputs.Keys)
                if (key <= cutoff) keysToRemove.Add(key);
            for (int i = 0; i < keysToRemove.Count; i++)
                remoteInputs.Remove(keysToRemove[i]);

            keysToRemove.Clear();
            foreach (int key in localHashes.Keys)
                if (key <= cutoff) keysToRemove.Add(key);
            for (int i = 0; i < keysToRemove.Count; i++)
                localHashes.Remove(keysToRemove[i]);
        }
    }
}