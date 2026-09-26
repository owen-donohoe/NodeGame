using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Core;
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
    public class LockstepRunner : MonoBehaviour, NodeWar.Core.ITickProvider, IEmoteChannel
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

        // One log for the life of the runner, cleared before every tick.
        private readonly TickEventLog tickEvents = new TickEventLog();

        public event System.Action<TickEventLog> TickSimulated;

        /// <summary>
        /// Every tick's commands in the order they were applied (all of P0's,
        /// then all of P1's), with the tick count before them. For recording
        /// only. Not raised for a tick with no commands.
        /// </summary>
        public event System.Action<int, GameCommand[]> CommandsApplied;

        /// <summary>
        /// A desync-check hash, with the tick count it was taken after. That
        /// count is one more than the tick index OnDesync reports.
        /// </summary>
        public event System.Action<int, int> HashComputed;

        public event System.Action<int, EmoteType> EmoteReceived;
        private ushort emoteSequence;
        private readonly ushort[] lastEmoteSequence = new ushort[2];
        private readonly bool[] hasEmoteSequence = new bool[2];

        public void Send(EmoteType emote)
        {
            if (networkManager == null) return;
            byte[] packet = InputSerializer.SerializeEmote(localPlayerID, emote, emoteSequence);
            emoteSequence = unchecked((ushort)(emoteSequence + 1));
            networkManager.Send(packet);
            networkManager.Send(packet);
        }

        public void Unpause()
        {
            // Re-stamp the timing baselines. Initialize() runs before the
            // post-draft transition, which can take longer than
            // DISCONNECT_TIMEOUT; without this the first unpaused frame would
            // see a stale lastReceiveTime and immediately report a disconnect.
            float now = Time.time;
            lastSendTime = now;
            lastReceiveTime = now;
            lastHeartbeatTime = now;

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
            emoteSequence = 0;
            System.Array.Clear(hasEmoteSequence, 0, hasEmoteSequence.Length);

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
            if (simState.gameOver)
            {
                // The end card still carries emotes. Keep the link alive without
                // turning a peer leaving that card into a match disconnect.
                ProcessIncomingPackets();
                SendHeartbeatIfNeeded();
                return;
            }

            // Pump the transport even while paused. Keeps lastReceiveTime fresh
            // and preserves any TickInput a peer sends if its transition
            // finishes before ours -- inputs are keyed by forTick, so receiving
            // them early loses nothing.
            ProcessIncomingPackets();

            if (paused)
            {
                // Keep the link alive so the peer does not time out waiting on
                // our transition. Deliberately no CheckDisconnect() while
                // paused: a long transition is not a disconnect, and Unpause()
                // re-baselines the timers anyway.
                SendHeartbeatIfNeeded();
                return;
            }

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

            tickEvents.Clear();

            if (CommandsApplied != null && p0Commands.Length + p1Commands.Length > 0)
            {
                GameCommand[] applied = new GameCommand[p0Commands.Length + p1Commands.Length];
                System.Array.Copy(p0Commands, applied, p0Commands.Length);
                System.Array.Copy(p1Commands, 0, applied, p0Commands.Length, p1Commands.Length);
                CommandsApplied(simState.tickCount, applied);
            }

            for (int i = 0; i < p0Commands.Length; i++)
                CommandProcessor.ProcessCommand(simState, p0Commands[i], tickEvents);

            for (int i = 0; i < p1Commands.Length; i++)
                CommandProcessor.ProcessCommand(simState, p1Commands[i], tickEvents);

            // Advance simulation
            GameSimulation.SimulateTick(simState, tickEvents);

            // Desync hash: compute after tick completes, store for next outgoing packet
            if (tick > 0 && tick % DESYNC_CHECK_INTERVAL == 0)
            {
                int computedHash = SimulationStateHasher.ComputeHash(simState);
                localHashes[tick] = computedHash;
                pendingHash = computedHash;
                pendingHashTick = tick;
                Debug.Log("[LOCKSTEP] Tick " + tick + " Hash: " + computedHash);
                HashComputed?.Invoke(simState.tickCount, computedHash);
            }

            // Compare remote's hash if they sent one
            if (remote.stateHash != 0)
            {
                CompareHash(remote.stateHash);
            }

            // Memory cleanup
            CleanupOldInputs(tick);

            TickSimulated?.Invoke(tickEvents);
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
                        if (simState.gameOver) break;
                        // A malformed packet is treated as a lost one, which the
                        // transport already has to survive. Nothing half-read
                        // reaches CommandProcessor.
                        if (!InputSerializer.TryDeserialize(packets[i], out TickInput remote))
                        {
                            Debug.LogWarning("[LOCKSTEP] Dropped malformed TickInput packet (" +
                                packets[i].Length + " bytes).");
                            break;
                        }
                        // Store if not already received (ignore duplicate resends)
                        if (!remoteInputs.ContainsKey(remote.forTick))
                        {
                            remoteInputs[remote.forTick] = remote;
                        }
                        break;

                    case PacketType.Emote:
                        if (!InputSerializer.TryDeserializeEmote(packets[i],
                            out int player, out EmoteType emote, out ushort sequence) ||
                            player == localPlayerID) break;

                        // Serial arithmetic survives ushort wrap. Late copies
                        // cannot replace a newer bubble with an older emote.
                        int advance = (sequence - lastEmoteSequence[player]) & 0xffff;
                        if (hasEmoteSequence[player] && (advance == 0 || advance >= 0x8000)) break;
                        hasEmoteSequence[player] = true;
                        lastEmoteSequence[player] = sequence;
                        EmoteReceived?.Invoke(player, emote);
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
