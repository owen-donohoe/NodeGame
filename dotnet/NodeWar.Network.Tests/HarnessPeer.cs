using System;
using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// One player: a LockstepCore over a real match state, wired to the link in
    /// both directions. It is the core's transport, which is where the harness
    /// sees every packet and can tell a duplicate from a first arrival.
    /// </summary>
    internal sealed class HarnessPeer : ILockstepTransport
    {
        public readonly int Player;
        public readonly LockstepCore Core;
        public readonly SimulationState State;
        public readonly InputBuffer Buffer = new InputBuffer();

        private readonly LinkDirection outgoing;
        private readonly LinkDirection incoming;
        private readonly Func<int, int, GameCommand[]> script;
        private readonly Func<double> clock;

        /// <summary>Hash after the tick count in the key, confirmed ticks only (live or replayed).</summary>
        public readonly Dictionary<int, int> Hashes = new Dictionary<int, int>();
        public readonly List<string> Log = new List<string>();
        public readonly List<int> DesyncTicks = new List<int>();

        // The first delivery of each forTick, and every one after it.
        private readonly HashSet<int> seenInputs = new HashSet<int>();
        public int InputDeliveries;
        public int DuplicateDeliveries;

        public int Holds;
        public int HoldEnds;
        public int Rollbacks;
        public int SpeculationStarts;
        public int SpeculatingFrames;
        public int Frames;
        public int FlushCalls;
        /// <summary>The largest input delay the core used at any frame.</summary>
        public int MaxInputDelay;

        /// <summary>Ticks shown to the player (live and speculative; a replay raises none).</summary>
        public int TicksShown;
        private double lastTickAt = -1;
        public double WorstTickGap;
        /// <summary>Seconds beyond one tick interval, summed over every gap: time the player watched a frozen board.</summary>
        public double FrozenSeconds;

        public double LastFrameAt;
        public double NextFrameAt;
        private bool wasSpeculating;

        public HarnessPeer(int player, SimulationState state, LinkDirection outgoing, LinkDirection incoming,
            Func<int, int, GameCommand[]> script, Func<double> clock)
        {
            Player = player;
            State = state;
            this.outgoing = outgoing;
            this.incoming = incoming;
            this.script = script;
            this.clock = clock;

            Core = new LockstepCore((level, message) =>
            {
                Log.Add("[" + clock().ToString("F2") + "] " + message);
                if (level == LockstepLogLevel.Error) DesyncTicks.Add(-1);
            });
            Core.OnDesync += tick => DesyncTicks.Add(tick);
            Core.HoldStarted += () => Holds++;
            Core.HoldEnded += () => HoldEnds++;
            Core.RolledBack += _ => Rollbacks++;
            Core.HashComputed += (tickCount, hash) => Hashes[tickCount] = hash;
            Core.TickSimulated += _ => OnTick();
            Core.LocalInputDue += forTick =>
            {
                GameCommand[] commands = script(Player, forTick);
                if (commands == null) return;
                for (int i = 0; i < commands.Length; i++) Buffer.EnqueueCommand(commands[i]);
            };
        }

        private void OnTick()
        {
            double now = clock();
            TicksShown++;
            if (lastTickAt >= 0)
            {
                double gap = now - lastTickAt;
                if (gap > WorstTickGap) WorstTickGap = gap;
                if (gap > 0.1) FrozenSeconds += gap - 0.1;
            }
            lastTickAt = now;
        }

        public void Start(double now)
        {
            Core.Initialize(State, Buffer, this, Player, 10, (float)now, (float)now);
            Core.Unpause((float)now, (float)now);
            LastFrameAt = now;
            NextFrameAt = now;
        }

        public void Frame(double now, double frameInterval)
        {
            double dt = now - LastFrameAt;
            Core.Update((float)now, (float)now, (float)dt, (float)dt);
            Core.Flush();
            LastFrameAt = now;
            NextFrameAt = now + frameInterval;
            Frames++;
            if (Core.InputDelay > MaxInputDelay) MaxInputDelay = Core.InputDelay;

            bool speculating = Core.IsSpeculating;
            if (speculating) SpeculatingFrames++;
            if (speculating && !wasSpeculating) SpeculationStarts++;
            wasSpeculating = speculating;
        }

        // ILockstepTransport

        public void Send(byte[] data) { outgoing.Send(clock(), data); }

        public void Flush() { FlushCalls++; }

        public byte[][] ReceiveAll()
        {
            byte[][] packets = incoming.Deliver(clock());
            for (int i = 0; i < packets.Length; i++)
            {
                if (packets[i].Length == 0 || InputSerializer.ReadPacketType(packets[i]) != PacketType.TickInput) continue;
                if (!InputSerializer.TryDeserialize(packets[i], out TickInput input)) continue;
                InputDeliveries++;
                if (!seenInputs.Add(input.forTick)) DuplicateDeliveries++;
            }
            return packets;
        }
    }
}
