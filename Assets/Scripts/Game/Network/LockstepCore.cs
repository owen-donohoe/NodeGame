using NodeWar.Simulation;
using NodeWar.Core;
using System.Collections.Generic;

namespace NodeWar.Network
{
    /// <summary>
    /// Where the lockstep core sends and receives. NetworkManager is the real
    /// one; the test harness supplies an in-memory lossy link.
    /// </summary>
    public interface ILockstepTransport
    {
        void Send(byte[] data);
        byte[][] ReceiveAll();
        void Flush();
    }

    public enum LockstepLogLevel { Info, Warning, Error }

    /// <summary>
    /// Replaces TickRunner for networked play.
    /// Same accumulator loop, but stalls until both local and remote inputs
    /// are available for the current tick before calling SimulateTick.
    /// Enforces command processing order: P0 first, P1 second, then simulate.
    /// Stamps local inputs for tick N+delay to hide network latency; the delay
    /// starts at INITIAL_DELAY and follows what the peer asks for (see
    /// <see cref="InputDelayController"/>).
    ///
    /// No UnityEngine in it: time is handed in by the caller on every entry
    /// (<see cref="Update"/>, <see cref="Unpause"/>, <see cref="Initialize"/>)
    /// and logging goes to a sink, so the whole loop runs under dotnet test
    /// against a simulated link. GameManager owns it and drives Update and
    /// Flush every frame.
    /// </summary>
    public sealed class LockstepCore : NodeWar.Core.ITickProvider, NodeWar.Core.IEmoteChannel
    {
        // The delay a match starts at. Ticks below it are pre-seeded empty on both
        // sides and never sent, so it must be the same on both: it is not adaptive.
        private const int INITIAL_DELAY = InputDelayController.BaseDelay;
        private const int MAX_DELAY = InputDelayController.MaxDelay;

        // Nothing changes the delay more often than this, whatever is asked.
        private const float DELAY_CHANGE_MIN_INTERVAL = 1.0f;
        private const int DESYNC_CHECK_INTERVAL = 50;
        // No tick for this long while unpaused starts a hold (8.2c, D15). Measured
        // on ticks, not packets: with one-way loss a side keeps receiving
        // heartbeats while it waits forever on an input that never comes.
        private const float HOLD_AFTER = 2.0f;
        private const float HEARTBEAT_INTERVAL = 0.5f;
        private const int MAX_ACCUMULATOR_TICKS = 3;
        private const float RESEND_INTERVAL = 0.1f;
        private const float HOLD_RESEND_INTERVAL = 0.25f;

        // How far past a missing opponent input the match plays on, predicting
        // the opponent idle, before rolling back and holding (8.2e, D16). 20
        // ticks is 2 s: a dropped text message or a tunnel, not a crash.
        private const int SPECULATION_WINDOW = 20;

        // An input a little late is ordinary jitter: Relay latency against the
        // input delay (200 ms at the start) leaves one arriving tens of milliseconds after its
        // tick is due several times a second. That waits a frame, as plain
        // lockstep always did. Only an input this late starts a speculation, or
        // every jitter becomes a rollback, a snap and a flash of the banner.
        private const float SPECULATE_AFTER = 0.3f;

        // The banner waits until a speculation has run this many ticks, so a
        // blip that settles quickly is never shown at all.
        private const int BANNER_AFTER_TICKS = 5;

        // How far behind our confirmed tick the peer can be: its speculation
        // window plus the input delay it generates ahead with.
        private const int PEER_LAG_TICKS = SPECULATION_WINDOW + MAX_DELAY + 2;

        // How far ahead of our tick a peer's input can honestly be: it may be
        // PEER_LAG_TICKS ahead and have generated a speculation window beyond.
        private const int MAX_INPUT_AHEAD = PEER_LAG_TICKS + SPECULATION_WINDOW + MAX_DELAY + 16;

        // Commands one player can issue in one tick. A full-army lasso move is
        // one command per villager, and a player has at most 25.
        private const int MAX_COMMANDS_PER_TICK = 64;

        // Every input goes out again with the next REDUNDANT_INPUTS, so a lost
        // packet costs nothing: its tick arrives 100 ms later inside the next
        // one's send, and a burst up to that many ticks long is bridged. Without
        // this a lost input stalls us until the peer itself runs dry and starts
        // resending, several hundred ms later. 6 from the lossy-link sweep
        // (SweepTests, 6 seeds, per 60 s, adaptive delay on): 2 -> 4 -> 6 -> 8
        // took random 30% loss from 9.2 s frozen to 2.1, 1.2, 1.2 and bursty
        // 5%/0.1 loss from 12.1 s to 6.9, 3.4, 2.0, at 0.6, 1.0, 1.4 and 1.7 KB/s
        // on a clean link. Redundancy and delay work together: the extra delay
        // gives a late copy time to land before its tick is needed. Each copy is
        // its own datagram today; bundling them into one per tick would keep the
        // same time diversity at a seventh of the packets. Internal so the tests
        // can state their copy budget.
        internal const int REDUNDANT_INPUTS = 6;

        // Peer inputs this many ticks past our own mean we are behind its
        // clock (after a hold, or a speculation that ran on the other side).
        // Ordinary latency and the peer's input delay keep it within that delay
        // plus a few ticks of jitter, so the threshold is the peer's delay (it
        // tells us, in every TickInput) plus this margin.
        private const int CATCHUP_MARGIN_TICKS = 3;
        private const float CATCHUP_RATE = 1.5f;

        // A stalled side resends only once the peer has gone this quiet. A
        // peer still sending new inputs every tick is not waiting on ours, and
        // an ordinary stall is latency, which a resend cannot fix: at 50 ms it
        // was over a thousand duplicates every five seconds.
        private const float PEER_QUIET = 0.15f;

        private int ticksPerSecond = 10;

        // The caller's clocks for the frame being run. `clock` is the game
        // clock (Time.time), `realClock` the wall clock the stats use
        // (Time.realtimeSinceStartup); they differ only under a time scale.
        private float clock;
        private float realClock;
        private float frameDt;
        private float frameUnscaledDt;

        private readonly System.Action<LockstepLogLevel, string> logSink;

        public LockstepCore(System.Action<LockstepLogLevel, string> logSink = null)
        {
            this.logSink = logSink;
        }

        private void Log(string message) { logSink?.Invoke(LockstepLogLevel.Info, message); }
        private void LogWarning(string message) { logSink?.Invoke(LockstepLogLevel.Warning, message); }
        private void LogError(string message) { logSink?.Invoke(LockstepLogLevel.Error, message); }

        private float tickInterval;
        private float accumulator;

        // Dependencies
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private ILockstepTransport transport;
        private int localPlayerID; // 0 for host, 1 for joiner

        // Tick tracking
        private int simulationTick;  // next tick to simulate
        private int nextInputTick;   // next forTick value for local input generation

        // Input storage (keyed by forTick)
        private Dictionary<int, TickInput> localInputs = new Dictionary<int, TickInput>();
        private Dictionary<int, TickInput> remoteInputs = new Dictionary<int, TickInput>();

        // Adaptive input delay. inputDelay is what we stamp our own inputs with; it
        // starts at INITIAL_DELAY and moves only when the peer asks. peerDelay is
        // what the peer says it stamps with, from its TickInput headers. The
        // controller watches the peer's inputs arrive and decides what to ask it for.
        private int inputDelay = INITIAL_DELAY;
        private int peerDelay = INITIAL_DELAY;
        private int delayInfoTick = -1;
        private float lastDelayChangeTime = float.MinValue;
        private readonly InputDelayController delayController = new InputDelayController();
        // A live tick had to wait for the peer, and the next one to run is the late one.
        private bool stallPending;
        private bool catchingUpThisFrame;
        private float lastLiveSlack;
        private bool lastLiveSlackValid;

        /// <summary>Ticks ahead our own inputs are stamped. For tests and the stats line.</summary>
        internal int InputDelay => inputDelay;

        /// <summary>The delay the peer says it stamps with.</summary>
        internal int PeerDelay => peerDelay;

        /// <summary>The delay we are currently asking the peer for, or 0.</summary>
        internal int RequestedPeerDelay => delayController.Request;

        /// <summary>Whether the peer's input for a tick has been accepted. For tests.</summary>
        internal bool HasRemoteInput(int tick) { return remoteInputs.ContainsKey(tick); }

        /// <summary>The farthest tick past our own the core will accept a peer input for.</summary>
        internal const int InputAheadLimit = MAX_INPUT_AHEAD;

        // Timing
        // When the resend loop last ran, and nothing else. Generating a new
        // input must not reset it: a speculating side generates one every tick,
        // so a clock shared with generation never reaches the resend interval
        // and the side resends nothing for the whole window.
        private float lastResendTime;
        private float lastAdvanceTime;
        // When a peer input we did not already have last arrived, and the
        // furthest tick any has been for. Speculation is for a silent peer;
        // a peer whose inputs are arriving, only late, is waited for.
        private float lastRemoteInputTime;
        private int highestRemoteTick;
        // The peer has just come back from silence. Whatever we generated
        // meanwhile went into the gap, and nothing else would resend it while
        // both sides play on: send the whole reach on the next frame.
        private bool resendNow;
        private bool holding;

        // Speculation (8.2e). The state as it stood before the first
        // unconfirmed tick, copied once per blip, and the tick it stood at.
        private readonly SimulationState confirmed = new SimulationState();
        private bool speculating;
        private int speculateFrom;
        // A speculation that had to be abandoned (it reached game over) is not
        // restarted until a confirmed tick runs, or it would loop.
        private bool speculationBlocked;
        private bool bannerShown;

        private enum TickMode { Live, Speculative, Replay }
        private float lastHeartbeatTime;

        // Resend
        private byte[] lastSentPacket;

        // Desync tracking
        private int pendingHash;
        private int pendingHashTick;
        private Dictionary<int, int> localHashes = new Dictionary<int, int>();

        // Public events for LobbyUI / GameManager to hook
        public event System.Action<int> OnDesync; // tick number where desync detected

        /// <summary>
        /// No tick has advanced for HOLD_AFTER seconds. The core keeps the link
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
        private bool ended;

        /// <summary>EndMatch was called: the core no longer ticks, resends or pumps packets.</summary>
        public bool IsEnded => ended;

        // One log for the life of the core, cleared before every tick.
        private readonly TickEventLog tickEvents = new TickEventLog();

        public event System.Action<TickEventLog> TickSimulated;

        /// <summary>After every confirmed tick, including replay. Observers read the current state.</summary>
        public event System.Action<int> ConfirmedTickSimulated;

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

        /// <summary>
        /// Raised with the forTick just before local commands are drained into
        /// it. Nothing subscribes in the game; the test harness injects scripted
        /// commands here so they are keyed to the tick they apply on, whatever
        /// the timing of the run.
        /// </summary>
        public event System.Action<int> LocalInputDue;
        private ushort emoteSequence;
        private readonly ushort[] lastEmoteSequence = new ushort[2];
        private readonly bool[] hasEmoteSequence = new bool[2];

        public void Send(EmoteType emote)
        {
            if (transport == null) return;
            byte[] packet = InputSerializer.SerializeEmote(localPlayerID, emote, emoteSequence);
            emoteSequence = unchecked((ushort)(emoteSequence + 1));
            transport.Send(packet);
            transport.Send(packet);
        }

        public void Unpause(float time, float realTime)
        {
            // Re-stamp the timing baselines. Initialize() runs before the
            // post-draft transition, which can take longer than HOLD_AFTER;
            // without this the first unpaused frame would see a stale
            // lastAdvanceTime and immediately start a hold.
            clock = time;
            realClock = realTime;
            float now = clock;
            lastResendTime = now;
            lastAdvanceTime = now;
            lastHeartbeatTime = now;
            lastRemoteInputTime = now;
            ResetStats();

            paused = false;
        }


        /// <summary>
        /// Normalized progress (0-1) between last tick and next tick.
        /// Used by View layer for interpolation. Same contract as TickRunner.TickAlpha.
        /// </summary>
        public float TickAlpha
        {
            get { return System.Math.Min(1f, System.Math.Max(0f, accumulator / tickInterval)); }
        }

        public void Initialize(SimulationState state, InputBuffer buffer,
            ILockstepTransport netTransport, int playerID, int ticksPerSecond, float time, float realTime)
        {
            simState = state;
            inputBuffer = buffer;
            transport = netTransport;
            this.ticksPerSecond = ticksPerSecond;
            clock = time;
            realClock = realTime;
            localPlayerID = playerID;
            emoteSequence = 0;
            System.Array.Clear(hasEmoteSequence, 0, hasEmoteSequence.Length);

            tickInterval = 1f / ticksPerSecond;
            accumulator = 0f;
            simulationTick = 0;
            nextInputTick = INITIAL_DELAY;
            inputDelay = INITIAL_DELAY;
            peerDelay = INITIAL_DELAY;
            delayInfoTick = -1;
            lastDelayChangeTime = float.MinValue;
            delayController.Reset();
            stallPending = false;
            catchingUpThisFrame = false;
            lastLiveSlackValid = false;

            float now = clock;
            lastResendTime = now;
            lastAdvanceTime = now;
            lastHeartbeatTime = now;
            lastRemoteInputTime = now;
            highestRemoteTick = INITIAL_DELAY - 1;
            holding = false;
            for (int i = 0; i < STAMP_RING; i++) { genTick[i] = -1; arriveTick[i] = -1; }

            pendingHash = 0;
            pendingHashTick = 0;

            // Pre-seed empty inputs for ticks 0 through INITIAL_DELAY-1.
            // Both machines do this identically, so ticks 0 and 1 are
            // immediately simulatable without waiting for network.
            for (int t = 0; t < INITIAL_DELAY; t++)
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

        public void Update(float time, float realTime, float deltaTime, float unscaledDeltaTime)
        {
            clock = time;
            realClock = realTime;
            frameDt = deltaTime;
            frameUnscaledDt = unscaledDeltaTime;

            if (ended) return;
            if (simState == null || transport == null) return;
            if (simState.gameOver)
            {
                // The end card still carries emotes. Keep the link alive without
                // turning a peer leaving that card into a match disconnect.
                ProcessIncomingPackets();
                SendHeartbeatIfNeeded();
                // Keep offering our last inputs: if the decisive one was lost on
                // the way, the peer is stuck short of the game over without it.
                ResendIfNeeded();
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

            SampleFrame();

            if (resendNow)
            {
                resendNow = false;
                ResendIfNeeded(force: true);
            }

            if (holding)
            {
                if (!HasBothInputs(simulationTick))
                {
                    // Keep offering every input the peer may lack, so the
                    // moment the link heals nothing is missing on either side.
                    ResendIfNeeded();
                    SendHeartbeatIfNeeded();
                    LogStatsIfDue();
                    return;
                }

                // Back. Start the clock fresh rather than replaying the held
                // time as a burst; if the peer got ahead meanwhile, catch-up
                // closes the gap at CATCHUP_RATE.
                holding = false;
                accumulator = 0f;
                Log("[LOCKSTEP] Resumed at tick " + simulationTick + " after " +
                          (clock - lastAdvanceTime).ToString("F1") + " s; peer inputs reach tick " +
                          highestRemoteTick + ".");
                lastAdvanceTime = clock;
                HoldEnded?.Invoke();
            }

            // A blip whose missing inputs have all arrived is settled first:
            // roll back and replay it with the real ones, before any new tick.
            if (speculating)
            {
                if (SpanConfirmed()) ReplaySpan();
                else ResendIfNeeded();
            }

            // Behind the peer's clock: run faster until its inputs are no
            // longer piling up ahead of us. Only ever runs ticks whose inputs
            // both sides already have, so it cannot outrun anything. Only while
            // the peer is still sending new ones: after a drop both sides hold
            // a speculation's worth of each other's inputs, arriving in one
            // burst, and that is no sign of either clock being ahead.
            bool catchingUp = !speculating && clock - lastRemoteInputTime < SPECULATE_AFTER &&
                              highestRemoteTick - simulationTick > peerDelay + CATCHUP_MARGIN_TICKS;
            catchingUpThisFrame = catchingUp;
            if (catchingUp) stats.catchupFrames++;
            accumulator += frameDt * (catchingUp ? CATCHUP_RATE : 1f);

            // Advance simulation as many ticks as possible
            while (accumulator >= tickInterval)
            {
                if (!speculating && HasBothInputs(simulationTick))
                {
                    GenerateLocalInputIfDue();
                    ExecuteTick(simulationTick, TickMode.Live);
                    FeedDelayController();
                    simulationTick++;
                    accumulator -= tickInterval;
                    lastAdvanceTime = clock;
                    speculationBlocked = false;
                }
                else if (CanSpeculate())
                {
                    if (!speculating) BeginSpeculation();
                    GenerateLocalInputIfDue();
                    ExecuteTick(simulationTick, TickMode.Speculative);
                    simulationTick++;
                    accumulator -= tickInterval;
                    ShowBannerIfLong();

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
                    // Stall: remote input not yet received for this tick, or a
                    // speculation waiting for a peer that is talking again.
                    stats.stallSeconds += frameUnscaledDt;
                    if (!speculating) stallPending = true;
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
                stats.holds++;
                LogWarning("[LOCKSTEP] Played " + SPECULATION_WINDOW + " ticks without the opponent; " +
                                 "rolled back to tick " + simulationTick + " and holding. Peer inputs reach tick " +
                                 highestRemoteTick + ", last new one " +
                                 (clock - lastRemoteInputTime).ToString("F2") + " s ago.");
                HoldStarted?.Invoke();
                return;
            }

            // A game-over tick ends the match normally; only a stall starts a hold.
            if (!speculating && !simState.gameOver && clock - lastAdvanceTime > HOLD_AFTER)
            {
                holding = true;
                stats.holds++;
                LogWarning("[LOCKSTEP] No tick for " + HOLD_AFTER + "s at tick " + simulationTick +
                                 "; holding. Peer inputs reach tick " + highestRemoteTick + ".");
                HoldStarted?.Invoke();
            }

            LogStatsIfDue();
        }

        /// <summary>
        /// After every Update this frame (GameManager calls it from LateUpdate), so the inputs, resends and heartbeats
        /// above and any emote the HUD sent all leave now, not next frame.
        /// </summary>
        public void Flush()
        {
            if (ended) return;
            if (transport != null) transport.Flush();
        }

        /// <summary>
        /// Stops the core for good: a hold resolved, or the player surrendered.
        /// The match is over on this side; the peer learns it from the server.
        /// </summary>
        public void EndMatch()
        {
            ended = true;
            holding = false;
            if (speculating) AbandonSpeculation();
        }

        // ===== SPECULATION (8.2e) =====

        /// <summary>
        /// A peer's input as this side will apply it. Every command is stamped
        /// with the peer's own slot, whatever the wire said: the slot is decided
        /// by who sent the packet, not by its contents. An honest peer's commands
        /// already carry it, so both sides apply the same thing. A modified
        /// client issuing commands for the other player's villagers now has them
        /// applied on its side only, which desyncs, is caught by the hash check,
        /// and leaves two disagreeing logs (Disputed) instead of an accepted
        /// result. Also capped at MAX_COMMANDS_PER_TICK, as local input is.
        /// </summary>
        private TickInput FromPeer(TickInput input)
        {
            GameCommand[] commands = input.commands ?? new GameCommand[0];
            int count = commands.Length < MAX_COMMANDS_PER_TICK ? commands.Length : MAX_COMMANDS_PER_TICK;
            var stamped = new GameCommand[count];
            int peerSlot = 1 - localPlayerID;
            for (int i = 0; i < count; i++)
            {
                stamped[i] = commands[i];
                stamped[i].playerID = peerSlot;
            }
            input.commands = stamped;
            return input;
        }

        private bool CanSpeculate()
        {
            if (speculationBlocked || holding) return false;
            // Only a silent peer is played past. A peer whose new inputs are
            // arriving is behind our clock, not gone: playing on would run
            // further ahead of inputs that can never catch up, and the span
            // would only end at the window, in a rollback and a hold. Instead
            // the frontier waits there until the peer's inputs reach it.
            if (clock - lastRemoteInputTime < SPECULATE_AFTER) return false;
            if (!speculating && clock - lastAdvanceTime < SPECULATE_AFTER) return false;
            if (!localInputs.ContainsKey(simulationTick)) return false;
            return !speculating || simulationTick - speculateFrom < SPECULATION_WINDOW;
        }

        private void BeginSpeculation()
        {
            confirmed.CopyFrom(simState);
            speculateFrom = simulationTick;
            speculating = true;
            stats.specBegun++;
            Log("[LOCKSTEP] Opponent input missing at tick " + simulationTick + "; playing on. Stalled " +
                      (clock - lastAdvanceTime).ToString("F2") + " s, last new peer input " +
                      (clock - lastRemoteInputTime).ToString("F2") + " s ago.");
        }

        private void ShowBannerIfLong()
        {
            if (bannerShown || simulationTick - speculateFrom < BANNER_AFTER_TICKS) return;
            bannerShown = true;
            SpeculationChanged?.Invoke(true);
        }

        private void HideBanner()
        {
            if (!bannerShown) return;
            bannerShown = false;
            SpeculationChanged?.Invoke(false);
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

            // Stop at a confirmed game over, exactly where a peer that played
            // these ticks live stopped. Ticks past it would change the state
            // and the log on this side only.
            while (simulationTick < until && !simState.gameOver)
            {
                ExecuteTick(simulationTick, TickMode.Replay);
                simulationTick++;
            }
            lastAdvanceTime = clock;
            stats.specReplayed++;
            Log("[LOCKSTEP] Opponent back; replayed ticks " + from + " to " + (until - 1) + ".");
            HideBanner();
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
            stats.specAbandoned++;
            RolledBack?.Invoke(simState.villagers != null ? simState.villagers.Length : 0);
            HideBanner();
        }

        // ===== ADAPTIVE INPUT DELAY =====

        /// <summary>
        /// Serialises an input with the delay fields as they stand now, not as
        /// they stood when the input was made: the redundant copies and the
        /// resends go out long after, and the peer should act on our current
        /// delay and our current request, not a stale one.
        /// </summary>
        private byte[] Pack(TickInput input)
        {
            input.senderDelay = (byte)inputDelay;
            input.requestedDelay = (byte)delayController.Request;
            return InputSerializer.Serialize(input);
        }

        /// <summary>
        /// A live tick has run. Tell the controller whether it had to wait for
        /// the peer's input, and how early that input had been, unless the tick
        /// says nothing about the delay: during a speculation, a hold, a catch-up
        /// or a silent peer the lateness is an outage, not a slow link.
        /// </summary>
        private void FeedDelayController()
        {
            bool late = stallPending;
            stallPending = false;

            bool disturbed = holding || speculating || catchingUpThisFrame ||
                             clock - lastRemoteInputTime >= SPECULATE_AFTER;
            // A tick with no arrival stamp (an early one) is neutral: plenty of slack.
            float slack = lastLiveSlackValid ? lastLiveSlack : 1f;
            delayController.OnLiveTick(clock, late, slack, disturbed, peerDelay);
        }

        /// <summary>
        /// Reads the delay fields of a peer input. Only from an input at least as
        /// new as any already read: an old copy delayed on the wire carries old
        /// values and must not undo a newer request. A request outside the
        /// allowed range is ignored, not clamped, so a hostile or broken peer
        /// cannot push our delay anywhere it could not have honestly asked for.
        /// Changes are rate-limited; raising takes effect on the next generated
        /// input, lowering as the inputs already sent run out.
        /// </summary>
        private void ReadPeerDelays(TickInput remote)
        {
            if (remote.forTick < delayInfoTick) return;
            delayInfoTick = remote.forTick;

            if (remote.senderDelay >= INITIAL_DELAY && remote.senderDelay <= MAX_DELAY)
                peerDelay = remote.senderDelay;

            int asked = remote.requestedDelay;
            if (asked < INITIAL_DELAY || asked > MAX_DELAY || asked == inputDelay) return;
            if (clock - lastDelayChangeTime < DELAY_CHANGE_MIN_INTERVAL) return;

            Log("[LOCKSTEP] Input delay " + inputDelay + " -> " + asked + " ticks at tick " +
                simulationTick + " (the peer asked).");
            inputDelay = asked;
            lastDelayChangeTime = clock;
        }

        // ===== INPUT GENERATION =====

        /// <summary>
        /// Generates the input for tick simulationTick + inputDelay unless it
        /// already exists. After an abandoned speculation, inputs for up to
        /// SPECULATION_WINDOW ticks ahead were already generated and sent;
        /// generating one per tick anyway would push every later command about
        /// 2 s back for the rest of the match.
        /// </summary>
        private void GenerateLocalInputIfDue()
        {
            // A loop, not an if: raising the delay by k leaves k more ticks due at
            // once, and each must get an input (empty after the first) or the peer
            // waits on a tick that never comes. Lowering it makes nothing due
            // until the surplus runs out, which needs no code.
            while (nextInputTick <= simulationTick + inputDelay) GenerateAndSendLocalInput();
        }

        private void GenerateAndSendLocalInput()
        {
            // Flush all commands accumulated since last tick. Capped as the peer
            // caps ours (FromPeer), so the two sides always apply the same list.
            // Test seam: a harness enqueues its scripted commands for this tick here.
            LocalInputDue?.Invoke(nextInputTick);
            GameCommand[] commands = inputBuffer.DrainCommands();
            if (commands.Length > MAX_COMMANDS_PER_TICK)
            {
                LogWarning("[LOCKSTEP] " + commands.Length + " commands in one tick; keeping " +
                                 MAX_COMMANDS_PER_TICK + ".");
                System.Array.Resize(ref commands, MAX_COMMANDS_PER_TICK);
            }
            // Stamped with our slot as the peer stamps them, so even a local bug
            // that put the wrong player on a command cannot make the sides differ.
            for (int i = 0; i < commands.Length; i++) commands[i].playerID = localPlayerID;

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
            byte[] packet = Pack(input);
            transport.Send(packet);
            lastSentPacket = packet;
            StampGenerated(nextInputTick);

            // The previous inputs ride along (REDUNDANT_INPUTS). Ticks below
            // INITIAL_DELAY are pre-seeded on both sides and never sent.
            for (int back = 1; back <= REDUNDANT_INPUTS; back++)
            {
                int tick = nextInputTick - back;
                if (tick >= INITIAL_DELAY && localInputs.TryGetValue(tick, out TickInput previous))
                    transport.Send(Pack(previous));
            }

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
            CountTick(tick, mode);

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
                Log("[LOCKSTEP] Tick " + tick + " Hash: " + computedHash);
                HashComputed?.Invoke(simState.tickCount, computedHash);
            }

            // Compare remote's hash if they sent one
            if (confirmedTick && remote.stateHash != 0)
            {
                CompareHash(remote.stateHash);
            }

            // Memory cleanup, keyed off confirmed ticks only.
            if (confirmedTick)
            {
                CleanupOldInputs(tick);
                ConfirmedTickSimulated?.Invoke(simState.tickCount);
            }

            // A replayed tick already played its cues when it was speculated; a
            // speculative game over is about to be rolled back. Neither is shown.
            if (mode == TickMode.Replay || (mode == TickMode.Speculative && simState.gameOver)) return;
            TickSimulated?.Invoke(tickEvents);
        }

        // ===== NETWORK RECEIVE =====

        private void ProcessIncomingPackets()
        {
            byte[][] packets = transport.ReceiveAll();

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
                            LogWarning("[LOCKSTEP] Dropped malformed TickInput packet (" +
                                packets[i].Length + " bytes).");
                            break;
                        }
                        // Only ticks an honest peer could be sending: a flood of
                        // distinct forTick values must not grow the dictionary.
                        if (remote.forTick < simulationTick - PEER_LAG_TICKS - 10 ||
                            remote.forTick > simulationTick + MAX_INPUT_AHEAD)
                            break;
                        ReadPeerDelays(remote);
                        // Store if not already received (ignore duplicate resends)
                        if (!remoteInputs.ContainsKey(remote.forTick))
                        {
                            remoteInputs[remote.forTick] = FromPeer(remote);
                            StampArrived(remote.forTick);
                            // Progress is a tick the peer never sent before. A
                            // held peer still resends old inputs; that is not
                            // talking, or our speculation would wait forever.
                            if (remote.forTick > highestRemoteTick)
                            {
                                if (clock - lastRemoteInputTime >= SPECULATE_AFTER)
                                {
                                    resendNow = true;
                                    stats.reconnects++;
                                }
                                highestRemoteTick = remote.forTick;
                                lastRemoteInputTime = clock;
                            }
                        }
                        else stats.dupInputs++;
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
                    LogError("[DESYNC] Tick " + pendingHashTick +
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
        /// clocks stop, heartbeats keep the link "alive". With speculation
        /// (8.2e) the peer can be up to PEER_LAG_TICKS behind our confirmed
        /// tick, so the reach runs from there up to the newest input we
        /// generated. Called only while stalled, speculating, holding or on the
        /// end card, never per live tick. Duplicates are ignored on receipt. No
        /// layout change.
        /// </summary>
        private void ResendIfNeeded(bool force = false)
        {
            if (lastSentPacket == null) return;
            if (!force)
            {
                // A hold can last a minute; the whole reach every 100 ms for
                // all of it is traffic for a link that is not answering.
                // Speculation has up to SPECULATION_WINDOW inputs outstanding,
                // so it takes the slower cadence too.
                bool slow = holding || speculating || simState.gameOver;
                float interval = slow ? HOLD_RESEND_INTERVAL : RESEND_INTERVAL;
                if (clock - lastResendTime < interval) return;
                // A plain stall: the peer may be waiting on us only once it has
                // stopped sending (PEER_QUIET).
                if (!slow && clock - lastRemoteInputTime < PEER_QUIET) return;
            }
            stats.resends++;

            // The peer can be up to PEER_LAG_TICKS behind our confirmed tick: it
            // may be speculating past an input of ours it never received while
            // we confirmed ticks on the inputs it kept sending. There is no ack
            // on the wire, so cover the whole reach.
            int confirmedTick = speculating ? speculateFrom : simulationTick;
            int first = System.Math.Max(0, confirmedTick - PEER_LAG_TICKS);
            for (int tick = first; tick < nextInputTick; tick++)
            {
                // Ticks below INITIAL_DELAY are pre-seeded on both sides, never sent.
                if (tick < INITIAL_DELAY) continue;
                if (localInputs.TryGetValue(tick, out TickInput input))
                    transport.Send(Pack(input));
            }
            lastResendTime = clock;
        }

        private void SendHeartbeatIfNeeded()
        {
            if (clock - lastHeartbeatTime < HEARTBEAT_INTERVAL) return;

            transport.Send(InputSerializer.SerializeHeartbeat());
            lastHeartbeatTime = clock;
        }

        // ===== NET STATS (diagnostics, logged as [NETSTAT]) =====
        //
        // lead: when the peer's input for tick T arrived, minus when we
        // generated ours for T. Both sides generate T's input at their own tick
        // T - INITIAL_DELAY, so lead = clock offset + one-way latency, and the two
        // sides' leads sum to the round trip exactly: offsets cancel. Compare
        // both peers' logs: RTT = leadA + leadB, offset = (leadA - leadB) / 2.
        // slack: how long a peer input waited between arriving and its tick
        // running live. Near zero means it arrived just in time (we stall).
        // Wall clock, unscaled; nothing here reaches the simulation.

        private const float STATS_INTERVAL = 5f;
        private const int STAMP_RING = 256;
        private readonly int[] genTick = new int[STAMP_RING];
        private readonly float[] genAt = new float[STAMP_RING];
        private readonly int[] arriveTick = new int[STAMP_RING];
        private readonly float[] arriveAt = new float[STAMP_RING];

        private struct NetStats
        {
            public float since;
            public int leadCount; public float leadSum, leadMin, leadMax;
            public int slackCount; public float slackSum, slackMin;
            public float stallSeconds, worstFrame;
            public int liveTicks, specTicks, replayTicks, catchupFrames;
            public int specBegun, specReplayed, specAbandoned, holds;
            public int newInputs, dupInputs, resends, reconnects;
            public int peerAheadMin, peerAheadMax;
        }

        private NetStats stats;

        // Lead over the whole match, from steady windows only: no speculation,
        // hold, catch-up or reconnect in them. After a drop the pre-generated
        // inputs land in a burst and skew a window's lead by seconds, so the
        // session figure is the one to sum across the two logs for the RTT.
        private int steadyWindows;
        private int steadyLeadCount;
        private float steadyLeadSum;

        private void ResetStats()
        {
            stats = new NetStats
            {
                since = realClock,
                leadMin = float.MaxValue, leadMax = float.MinValue, slackMin = float.MaxValue,
                peerAheadMin = int.MaxValue, peerAheadMax = int.MinValue
            };
        }

        private void SampleFrame()
        {
            if (frameUnscaledDt > stats.worstFrame) stats.worstFrame = frameUnscaledDt;
            int ahead = highestRemoteTick - simulationTick;
            if (ahead < stats.peerAheadMin) stats.peerAheadMin = ahead;
            if (ahead > stats.peerAheadMax) stats.peerAheadMax = ahead;
        }

        private void StampGenerated(int tick)
        {
            int slot = tick & (STAMP_RING - 1);
            genTick[slot] = tick;
            genAt[slot] = realClock;
            if (arriveTick[slot] == tick) AddLead(arriveAt[slot] - genAt[slot]);
        }

        private void StampArrived(int tick)
        {
            stats.newInputs++;
            int slot = tick & (STAMP_RING - 1);
            arriveTick[slot] = tick;
            arriveAt[slot] = realClock;
            if (genTick[slot] == tick) AddLead(arriveAt[slot] - genAt[slot]);
        }

        private void AddLead(float seconds)
        {
            stats.leadCount++;
            stats.leadSum += seconds;
            if (seconds < stats.leadMin) stats.leadMin = seconds;
            if (seconds > stats.leadMax) stats.leadMax = seconds;
        }

        private void CountTick(int tick, TickMode mode)
        {
            if (mode == TickMode.Speculative) { stats.specTicks++; return; }
            if (mode == TickMode.Replay) { stats.replayTicks++; return; }
            stats.liveTicks++;
            lastLiveSlackValid = false;
            int slot = tick & (STAMP_RING - 1);
            if (tick < INITIAL_DELAY || arriveTick[slot] != tick) return;
            float slack = realClock - arriveAt[slot];
            lastLiveSlack = slack;
            lastLiveSlackValid = true;
            stats.slackCount++;
            stats.slackSum += slack;
            if (slack < stats.slackMin) stats.slackMin = slack;
        }

        private void LogStatsIfDue()
        {
            float now = realClock;
            float span = now - stats.since;
            if (span < STATS_INTERVAL) return;

            string lead = stats.leadCount == 0 ? "n/a"
                : "avg " + Ms(stats.leadSum / stats.leadCount) + " min " + Ms(stats.leadMin) +
                  " max " + Ms(stats.leadMax) + " ms (n=" + stats.leadCount + ")";
            string slack = stats.slackCount == 0 ? "n/a"
                : "avg " + Ms(stats.slackSum / stats.slackCount) + " min " + Ms(stats.slackMin) + " ms";

            bool steady = !holding && !speculating && stats.specBegun == 0 && stats.specReplayed == 0 &&
                          stats.specAbandoned == 0 && stats.holds == 0 && stats.catchupFrames == 0 &&
                          stats.reconnects == 0 && stats.leadCount > 0;
            if (steady)
            {
                steadyWindows++;
                steadyLeadCount += stats.leadCount;
                steadyLeadSum += stats.leadSum;
            }
            string session = steadyLeadCount == 0 ? "n/a"
                : Ms(steadyLeadSum / steadyLeadCount) + " ms over " + steadyWindows + " windows";

            Log("[NETSTAT] P" + localPlayerID + " " + span.ToString("F1") + "s to tick " + simulationTick +
                      (holding ? " HOLDING" : speculating ? " SPECULATING" : "") +
                      (steady ? " steady" : " disturbed") +
                      " | match steady lead " + session +
                      " | lead " + lead +
                      " | slack " + slack +
                      " | stalled " + Ms(stats.stallSeconds) + " ms, worst frame " + Ms(stats.worstFrame) + " ms" +
                      " | ticks live " + stats.liveTicks + " spec " + stats.specTicks + " replay " + stats.replayTicks +
                      ", catch-up frames " + stats.catchupFrames +
                      " | spec begun " + stats.specBegun + " confirmed " + stats.specReplayed +
                      " abandoned " + stats.specAbandoned + ", holds " + stats.holds +
                      " | peer ahead " + stats.peerAheadMin + ".." + stats.peerAheadMax + " ticks" +
                      " | delay own " + inputDelay + " peer " + peerDelay + " asked " + delayController.Request +
                      " | inputs new " + stats.newInputs + " dup " + stats.dupInputs +
                      " | resends " + stats.resends + ", reconnects " + stats.reconnects);
            ResetStats();
        }

        private static string Ms(float seconds) => ((int)System.Math.Round(seconds * 1000f)).ToString();

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
            // Keep what a lagging peer may still need resent, and checkpoint
            // hashes that may arrive late after a replay, with some margin.
            int cutoff = completedTick - PEER_LAG_TICKS - 10;
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
