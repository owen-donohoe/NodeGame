using System;
using System.Collections.Generic;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// What one direction of the link does to a packet. All times are seconds.
    /// The defaults are a clean link: 65 ms each way, which is the 130 ms RTT
    /// the last good two-player run measured.
    /// </summary>
    internal sealed class LinkProfile
    {
        public double Delay = 0.065;
        /// <summary>When set, the one-way delay as a function of send time, in place of <see cref="Delay"/>: a link that gets worse or better mid-match.</summary>
        public System.Func<double, double> DelayAt;
        /// <summary>Extra delay, uniform in [0, Jitter], added per packet. Packets overtake each other when it is large.</summary>
        public double Jitter;
        public double Loss;

        /// <summary>
        /// Gilbert-Elliott burst loss, stepped once per packet: the chance a
        /// good link turns bad, and a bad one recovers. A bad link loses
        /// <see cref="BurstLoss"/> of what it carries.
        /// </summary>
        public double BurstEnter;
        public double BurstExit = 0.3;
        public double BurstLoss = 1.0;

        public double Duplicate;
        /// <summary>Chance a packet is held back an extra <see cref="ReorderExtra"/>, so later ones pass it.</summary>
        public double Reorder;
        public double ReorderExtra = 0.15;

        /// <summary>Windows in which every packet sent is lost, by send time.</summary>
        public readonly List<(double from, double to)> Outages = new List<(double, double)>();

        /// <summary>
        /// Drops a TickInput packet when this returns true for (send time, the
        /// tick it carries). Lets a test lose exactly the copies of one tick
        /// while everything around them arrives, which random loss only does
        /// by luck.
        /// </summary>
        public System.Func<double, int, bool> DropInput;

        public LinkProfile Cut(double from, double to)
        {
            Outages.Add((from, to));
            return this;
        }
    }

    /// <summary>One direction of the link: packets in, packets out after their delay.</summary>
    internal sealed class LinkDirection
    {
        private struct InFlight
        {
            public double at;
            public long seq;
            public byte[] data;
        }

        private readonly LinkProfile profile;
        private readonly Random rng;
        private readonly List<InFlight> flight = new List<InFlight>();
        private long seq;
        private bool bad;

        public long Sent;
        public long Lost;
        public long BytesSent;
        /// <summary>By what the packet was: tick inputs and heartbeats, counted at send.</summary>
        public long InputsSent;
        public long HeartbeatsSent;

        public LinkDirection(LinkProfile profile, int seed)
        {
            this.profile = profile;
            rng = new Random(seed);
        }

        public void Send(double now, byte[] data)
        {
            Sent++;
            BytesSent += data.Length;
            if (data.Length > 0)
            {
                PacketType type = InputSerializer.ReadPacketType(data);
                if (type == PacketType.TickInput) InputsSent++;
                else if (type == PacketType.Heartbeat) HeartbeatsSent++;
            }

            if (profile.DropInput != null && data.Length > 0 && InputSerializer.ReadPacketType(data) == PacketType.TickInput &&
                InputSerializer.TryDeserialize(data, out TickInput carried) && profile.DropInput(now, carried.forTick))
            {
                Lost++;
                return;
            }

            for (int i = 0; i < profile.Outages.Count; i++)
            {
                if (now >= profile.Outages[i].from && now < profile.Outages[i].to) { Lost++; return; }
            }

            if (bad) { if (rng.NextDouble() < profile.BurstExit) bad = false; }
            else if (profile.BurstEnter > 0 && rng.NextDouble() < profile.BurstEnter) bad = true;

            if (bad && rng.NextDouble() < profile.BurstLoss) { Lost++; return; }
            if (profile.Loss > 0 && rng.NextDouble() < profile.Loss) { Lost++; return; }

            Enqueue(now, data);
            if (profile.Duplicate > 0 && rng.NextDouble() < profile.Duplicate) Enqueue(now, data);
        }

        private void Enqueue(double now, byte[] data)
        {
            double delay = profile.DelayAt != null ? profile.DelayAt(now) : profile.Delay;
            if (profile.Jitter > 0) delay += rng.NextDouble() * profile.Jitter;
            if (profile.Reorder > 0 && rng.NextDouble() < profile.Reorder) delay += profile.ReorderExtra;
            flight.Add(new InFlight { at = now + delay, seq = seq++, data = (byte[])data.Clone() });
        }

        /// <summary>Everything that has arrived by <paramref name="now"/>, in arrival order.</summary>
        public byte[][] Deliver(double now)
        {
            List<InFlight> due = null;
            for (int i = flight.Count - 1; i >= 0; i--)
            {
                if (flight[i].at > now) continue;
                if (due == null) due = new List<InFlight>();
                due.Add(flight[i]);
                flight.RemoveAt(i);
            }
            if (due == null) return Array.Empty<byte[]>();

            due.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : a.seq.CompareTo(b.seq));
            byte[][] result = new byte[due.Count][];
            for (int i = 0; i < due.Count; i++) result[i] = due[i].data;
            return result;
        }
    }
}
