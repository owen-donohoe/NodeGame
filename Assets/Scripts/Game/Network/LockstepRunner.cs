using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Core;

namespace NodeWar.Network
{
    /// <summary>
    /// The MonoBehaviour around <see cref="LockstepCore"/>, which holds all of
    /// the lockstep logic (see its summary). This class only hands the core
    /// Unity's clocks and log, forwards its events, and ends it on EndMatch.
    /// Nothing else lives here: logic added to this file is logic the
    /// NodeWar.Network.Tests harness cannot reach.
    /// </summary>
    public class LockstepRunner : MonoBehaviour, NodeWar.Core.ITickProvider, IEmoteChannel
    {
        [Header("Tick Settings")]
        public int ticksPerSecond = 10;

        private readonly LockstepCore core = new LockstepCore(WriteLog);
        private NetworkManager networkManager;

        private static void WriteLog(LockstepLogLevel level, string message)
        {
            switch (level)
            {
                case LockstepLogLevel.Warning: Debug.LogWarning(message); break;
                case LockstepLogLevel.Error: Debug.LogError(message); break;
                default: Debug.Log(message); break;
            }
        }

        /// <summary>NetworkManager as the core's transport.</summary>
        private sealed class ManagerTransport : ILockstepTransport
        {
            private static readonly byte[][] None = new byte[0][];
            private readonly NetworkManager manager;

            public ManagerTransport(NetworkManager manager) { this.manager = manager; }

            public void Send(byte[] data) { manager.Send(data); }
            public byte[][] ReceiveAll() { return manager.ReceiveAll() ?? None; }
            public void Flush() { manager.Flush(); }
        }

        // ===== Events and state, forwarded from the core =====

        /// <summary>Tick number where a desync was detected.</summary>
        public event System.Action<int> OnDesync
        {
            add { core.OnDesync += value; }
            remove { core.OnDesync -= value; }
        }

        /// <summary>
        /// No tick has advanced for a while. The runner keeps the link alive
        /// and never ends the match itself: GameManager's hold decides that,
        /// and calls EndMatch.
        /// </summary>
        public event System.Action HoldStarted
        {
            add { core.HoldStarted += value; }
            remove { core.HoldStarted -= value; }
        }

        /// <summary>The missing input arrived and the match is advancing again.</summary>
        public event System.Action HoldEnded
        {
            add { core.HoldEnded += value; }
            remove { core.HoldEnded -= value; }
        }

        public bool IsHolding => core.IsHolding;

        /// <summary>
        /// The match is playing on past a missing opponent input (8.2e). True
        /// while the ticks shown may still be rolled back.
        /// </summary>
        public bool IsSpeculating => core.IsSpeculating;

        /// <summary>Raised when speculation starts or ends, for the HUD's banner.</summary>
        public event System.Action<bool> SpeculationChanged
        {
            add { core.SpeculationChanged += value; }
            remove { core.SpeculationChanged -= value; }
        }

        /// <summary>
        /// The state was put back to the last confirmed tick, with the villager
        /// count it had then. Views past that count may point at villagers that
        /// no longer exist.
        /// </summary>
        public event System.Action<int> RolledBack
        {
            add { core.RolledBack += value; }
            remove { core.RolledBack -= value; }
        }

        public event System.Action<TickEventLog> TickSimulated
        {
            add { core.TickSimulated += value; }
            remove { core.TickSimulated -= value; }
        }

        /// <summary>
        /// Every tick's commands in the order they were applied, with the tick
        /// count before them. For recording only.
        /// </summary>
        public event System.Action<int, GameCommand[]> CommandsApplied
        {
            add { core.CommandsApplied += value; }
            remove { core.CommandsApplied -= value; }
        }

        /// <summary>A desync-check hash, with the tick count it was taken after.</summary>
        public event System.Action<int, int> HashComputed
        {
            add { core.HashComputed += value; }
            remove { core.HashComputed -= value; }
        }

        public event System.Action<int, EmoteType> EmoteReceived
        {
            add { core.EmoteReceived += value; }
            remove { core.EmoteReceived -= value; }
        }

        public void Send(EmoteType emote) { core.Send(emote); }

        /// <summary>
        /// Normalized progress (0-1) between last tick and next tick.
        /// Used by View layer for interpolation. Same contract as TickRunner.TickAlpha.
        /// </summary>
        public float TickAlpha => core.TickAlpha;

        public void Initialize(SimulationState state, InputBuffer buffer,
            NetworkManager netManager, int playerID)
        {
            networkManager = netManager;
            core.Initialize(state, buffer, new ManagerTransport(netManager), playerID,
                ticksPerSecond, Time.time, Time.realtimeSinceStartup);
        }

        public void Unpause()
        {
            core.Unpause(Time.time, Time.realtimeSinceStartup);
        }

        private void Update()
        {
            if (networkManager == null) return;
            core.Update(Time.time, Time.realtimeSinceStartup, Time.deltaTime, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// After every Update this frame, so the inputs, resends and heartbeats
        /// and any emote the HUD sent all leave now, not next frame.
        /// </summary>
        private void LateUpdate()
        {
            if (networkManager != null) core.Flush();
        }

        /// <summary>
        /// Stops the runner for good: a hold resolved, or the player surrendered.
        /// The match is over on this side; the peer learns it from the server.
        /// </summary>
        public void EndMatch()
        {
            core.EndMatch();
            enabled = false;
        }
    }
}
