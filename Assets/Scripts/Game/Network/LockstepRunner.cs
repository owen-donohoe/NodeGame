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
        // No tick for this long while unpaused starts a hold (8.2c, D15). Measured
        // on ticks, not packets: with one-way loss a side keeps receiving
        // heartbeats while it waits forever on an input that never comes.
        private const float HOLD_AFTER = 2.0f;
        private const float HEARTBEAT_INTERVAL = 0.5f;
        private const int MAX_ACCUMULATOR_TICKS = 3;
        private const float RESEND_INTERVAL = 0.05f;
        private const float HOLD_RESEND_INTERVAL = 0.25f;

        // How far past a missing opponent input the match plays on, predicting
        // the opponent idle, before rolling back and holding (8.2e, D16). 20
        // ticks is 2 s: a dropped text message or a tunnel, not a crash.
        private const int SPECULATION_WINDOW = 20;

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
        private float lastAdvanceTime;
        private bool holding;

        // Speculation (8.2e). The state as it stood before the first
        // unconfirmed tick, copied once per blip, and the tick it stood at.
        private readonly SimulationState confirmed = new SimulationState();
        private bool speculating;
        private int speculateFrom;
        // A speculation that had to be abandoned (it reached game over) is not
        // restarted until a confirmed tick runs, or it would loop.
        private bool speculationBlocked;

        private enum TickMode { Live, Speculative, Replay }
        private float lastHeartbeatTime;

        // Resend
        private byte[] lastSentPacket;

        // Desync tracking
        private int pendingHash;
        private int pendingHashTick;
        private Dictionary<int, int> localHashes = new Dictionary<int, int>();

        // Public events for LobbyUI / GameManager to hook
        public System.Action<int> OnDesync; // tick number where desync detected

        /// <summary>
        /// No tick has advanced for HOLD_AFTER seconds. The runner keeps the link
        /// alive (packets, resends, heartbeats) and never ends the match itself:
        /// GameManager's hold decides that, and calls EndMatch.
        /// </summary>
        public event System.Action HoldStarted;

        /// <summary>The missing input arrived and the match is advancing again.</summary>
        public event System.Action HoldEnded;

        public bool IsHolding => holding;

        /// <summary>
        /// The match is playing on past a missing opponent input (8.2e). True
        /// while the ticks shown may still be rolled back.
        /// </summary>
        public bool IsSpeculating => speculating;

        /// <summary>Raised when speculation starts or ends, for the HUD's banner.</summary>
        public event System.Action<bool> SpeculationChanged;

        /// <summary>
        /// The state was put back to the last confirmed tick, with the villager
        /// count it had then. Views past that count may point at villagers that
        /// no longer exist.
        /// </summary>
        public event System.Action<int> RolledBack;

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
            // post-draft transition, which can take longer than HOLD_AFTER;
            // without this the first unpaused frame would see a stale
            // lastAdvanceTime and immediately start a hold.
            float now = Time.time;
            lastSendTime = now;
            lastAdvanceTime = now;
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
            lastAdvanceTime = now;
            lastHeartbeatTime = now;
            holding = false;

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

            // Pump the transport even while paused. Preserves any TickInput a
            // peer sends if its transition finishes before ours -- inputs are
            // keyed by forTick, so receiving them early loses nothing.
            ProcessIncomingPackets();

            if (paused)
            {
                // Keep the link alive so the peer does not start a hold while
                // waiting on our transition. Deliberately no hold check while
                // paused: a long transition is not a disconnect, and Unpause()
                // re-baselines the timers anyway.
                SendHeartbeatIfNeeded();
                return;
            }

            if (holding)
            {
                if (!HasBothInputs(simulationTick))
                {
                    // Keep offering every input the peer may lack, so the
                    // moment the link heals nothing is missing on either side.
                    ResendIfNeeded();
                    SendHeartbeatIfNeeded();
                    return;
                }

                // Back. Start the clock fresh rather than replaying the held
                // time as a burst of catch-up ticks.
                holding = false;
                accumulator = 0f;
                lastAdvanceTime = Time.time;
                Debug.Log("[LOCKSTEP] Resumed at tick " + simulationTick + ".");
                HoldEnded?.Invoke();
            }

            // A blip whose missing inputs have all arrived is settled first:
            // roll back and replay it with the real ones, before any new tick.
            if (speculating)
            {
                if (SpanConfirmed()) ReplaySpan();
                else ResendIfNeeded();
            }

            accumulator += Time.deltaTime;

            // Advance simulation as many ticks as possible
            while (accumulator >= tickInterval)
            {
                if (!speculating && HasBothInputs(simulationTick))
                {
                    GenerateLocalInputIfDue();
                    ExecuteTick(simulationTick, TickMode.Live);
                    simulationTick++;
                    accumulator -= tickInterval;
                    lastAdvanceTime = Time.time;
                    speculationBlocked = false;
                }
                else if (CanSpeculate())
                {
                    if (!speculating) BeginSpeculation();
                    GenerateLocalInputIfDue();
                    ExecuteTick(simulationTick, TickMode.Speculative);
                    simulationTick++;
                    accumulator -= tickInterval;

                    // A speculative game over is not a result: nobody has
                    // confirmed the inputs that produced it. Put the board back
                    // before anything outside the runner sees it.
                    if (simState.gameOver)
                    {
                        AbandonSpeculation();
                        speculationBlocked = true;
                        break;
                    }
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

            // The window ran out: the opponent is really gone. Rewind to what
            // both sides agreed on and hold (D16).
            if (speculating && simulationTick - speculateFrom >= SPECULATION_WINDOW)
            {
                AbandonSpeculation();
                holding = true;
                Debug.LogWarning("[LOCKSTEP] Played " + SPECULATION_WINDOW + " ticks without the opponent; " +
                                 "rolled back to tick " + simulationTick + " and holding.");
                HoldStarted?.Invoke();
                return;
            }

            // A game-over tick ends the match normally; only a stall starts a hold.
            if (!speculating && !simState.gameOver && Time.time - lastAdvanceTime > HOLD_AFTER)
            {
                holding = true;
                Debug.LogWarning("[LOCKSTEP] No tick for " + HOLD_AFTER + "s at tick " + simulationTick + "; holding.");
                HoldStarted?.Invoke();
            }
        }

        /// <summary>
        /// Stops the runner for good: a hold resolved, or the player surrendered.
        /// The match is over on this side; the peer learns it from the server.
        /// </summary>
        public void EndMatch()
        {
            holding = false;
            if (speculating) AbandonSpeculation();
            enabled = false;
        }

        // ===== SPECULATION (8.2e) =====

        private bool CanSpeculate()
        {
            if (speculationBlocked || holding) return false;
            if (!localInputs.ContainsKey(simulationTick)) return false;
            return !speculating || simulationTick - speculateFrom < SPECULATION_WINDOW;
        }

        private void BeginSpeculation()
        {
            confirmed.CopyFrom(simState);
            speculateFrom = simulationTick;
            speculating = true;
            Debug.Log("[LOCKSTEP] Opponent input missing at tick " + simulationTick + "; playing on.");
            SpeculationChanged?.Invoke(true);
        }

        private bool SpanConfirmed()
        {
            for (int t = speculateFrom; t < simulationTick; t++)
                if (!remoteInputs.ContainsKey(t)) return false;
            return true;
        }

        /// <summary>
        /// The opponent's inputs for the whole blip arrived. Always roll back
        /// and replay, even when they turn out empty, so this path runs on every
        /// blip instead of hiding until a rare one. The replay is the only pass
        /// that records and hashes; it raises no TickSimulated, so no cue plays
        /// twice.
        /// </summary>
        private void ReplaySpan()
        {
            int from = speculateFrom;
            int until = simulationTick;
            simState.CopyFrom(confirmed);
            simulationTick = from;
            speculating = false;
            RolledBack?.Invoke(simState.villagers != null ? simState.villagers.Length : 0);

            while (simulationTick < until)
            {
                ExecuteTick(simulationTick, TickMode.Replay);
                simulationTick++;
            }
            lastAdvanceTime = Time.time;
            Debug.Log("[LOCKSTEP] Opponent back; replayed ticks " + from + " to " + (until - 1) + ".");
            SpeculationChanged?.Invoke(false);
        }

        /// <summary>
        /// Puts the board back to the last confirmed tick. The local inputs
        /// generated meanwhile stay: they were sent, and the peer will apply
        /// them on those ticks too.
        /// </summary>
        private void AbandonSpeculation()
        {
            simState.CopyFrom(confirmed);
            simulationTick = speculateFrom;
            speculating = false;
            RolledBack?.Invoke(simState.villagers != null ? simState.villagers.Length : 0);
            SpeculationChanged?.Invoke(false);
        }

        // ===== INPUT GENERATION =====

        /// <summary>
        /// Generates the input for tick simulationTick + INPUT_DELAY unless it
        /// already exists. After an abandoned speculation, inputs for up to
        /// SPECULATION_WINDOW ticks ahead were already generated and sent;
        /// generating one per tick anyway would push every later command about
        /// 2 s back for the rest of the match.
        /// </summary>
        private void GenerateLocalInputIfDue()
        {
            if (nextInputTick <= simulationTick + INPUT_DELAY) GenerateAndSendLocalInput();
        }

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

        private void ExecuteTick(int tick, TickMode mode)
        {
            TickInput local = localInputs[tick];
            // Speculating: the opponent is predicted idle where their input is missing.
            if (!remoteInputs.TryGetValue(tick, out TickInput remote))
                remote = new TickInput { forTick = tick, stateHash = 0, commands = new GameCommand[0] };
            bool confirmedTick = mode != TickMode.Speculative;

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

            if (confirmedTick && CommandsApplied != null && p0Commands.Length + p1Commands.Length > 0)
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

            // Desync hash: compute after tick completes, store for next outgoing
            // packet. Only on confirmed ticks: a speculative state is not one
            // either peer has agreed to.
            if (confirmedTick && tick > 0 && tick % DESYNC_CHECK_INTERVAL == 0)
            {
                int computedHash = SimulationStateHasher.ComputeHash(simState);
                localHashes[tick] = computedHash;
                pendingHash = computedHash;
                pendingHashTick = tick;
                Debug.Log("[LOCKSTEP] Tick " + tick + " Hash: " + computedHash);
                HashComputed?.Invoke(simState.tickCount, computedHash);
            }

            // Compare remote's hash if they sent one
            if (confirmedTick && remote.stateHash != 0)
            {
                CompareHash(remote.stateHash);
            }

            // Memory cleanup, keyed off confirmed ticks only.
            if (confirmedTick) CleanupOldInputs(tick);

            // A replayed tick already played its cues when it was speculated; a
            // speculative game over is about to be rolled back. Neither is shown.
            if (mode == TickMode.Replay || (mode == TickMode.Speculative && simState.gameOver)) return;
            TickSimulated?.Invoke(tickEvents);
        }

        // ===== NETWORK RECEIVE =====

        private void ProcessIncomingPackets()
        {
            byte[][] packets = networkManager.ReceiveAll();

            for (int i = 0; i < packets.Length; i++)
            {
                if (packets[i] == null || packets[i].Length == 0) continue;

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
                        // Keeps the Relay allocation alive. A hold is decided by
                        // ticks, so a heartbeat alone never ends one.
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

        // ===== RESEND / HEARTBEAT =====

        /// <summary>
        /// Re-sends every local input the peer may still be missing, not just
        /// the newest. Resending only the last packet deadlocks on one loss:
        /// if our input for tick N is dropped after N+1 has gone out, the peer
        /// stalls on N while we stall on its N and keep resending N+1 - both
        /// clocks stop, heartbeats keep the link "alive". The peer can need any
        /// input from INPUT_DELAY ticks behind our simulation tick up to the
        /// newest we generated (at most 2 * INPUT_DELAY packets). Duplicates are
        /// ignored on receipt. No layout change.
        /// </summary>
        private void ResendIfNeeded()
        {
            if (lastSentPacket == null) return;
            // A hold can last a minute; four packets every 50 ms for all of it
            // is traffic for a link that is not answering.
            // Speculation has up to SPECULATION_WINDOW inputs outstanding, so it
            // takes the slower cadence too.
            float interval = holding || speculating ? HOLD_RESEND_INTERVAL : RESEND_INTERVAL;
            if (Time.time - lastSendTime < interval) return;

            // From the last confirmed tick: the peer may lack anything since.
            int confirmedTick = speculating ? speculateFrom : simulationTick;
            int first = Mathf.Max(0, confirmedTick - INPUT_DELAY);
            for (int tick = first; tick < nextInputTick; tick++)
            {
                // Ticks below INPUT_DELAY are pre-seeded on both sides, never sent.
                if (tick < INPUT_DELAY) continue;
                if (localInputs.TryGetValue(tick, out TickInput input))
                    networkManager.Send(InputSerializer.Serialize(input));
            }
            lastSendTime = Time.time;
        }

        private void SendHeartbeatIfNeeded()
        {
            if (Time.time - lastHeartbeatTime < HEARTBEAT_INTERVAL) return;

            networkManager.Send(InputSerializer.SerializeHeartbeat());
            lastHeartbeatTime = Time.time;
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
