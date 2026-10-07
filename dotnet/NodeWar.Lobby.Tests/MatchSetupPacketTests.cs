using System.Collections.Generic;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The pre-draft setup exchange: the host proposes the map and rules, the
    /// guest verifies them against the shipped catalog and acknowledges, and
    /// no draft packet is honoured by either side until that agreement holds.
    /// </summary>
    public class MatchSetupPacketTests
    {
        private const int Balance = 777;
        private static readonly ushort Sim = (ushort)SimulationVersion.Current;

        private static MatchSetup Hourglass() => MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, Balance);

        private static SetupAgreement Guest(string expectedMapId = null) =>
            SetupAgreement.Guest(PremadeMaps.Catalog, Sim, Balance, expectedMapId);

        [Test]
        public void Setup_RoundTripsAndRejectsTruncation()
        {
            MatchSetup setup = Hourglass();
            byte[] packet = DraftSerializer.SerializeMatchSetup(setup);

            Assert.AreEqual(PacketType.MatchSetup, InputSerializer.ReadPacketType(packet));
            Assert.IsTrue(DraftSerializer.TryDeserializeMatchSetup(packet, out MatchSetup read));
            Assert.AreEqual("hourglass-01", read.MapId);
            Assert.AreEqual(setup.BoardHash, read.BoardHash);
            Assert.AreEqual(setup.SimulationVersion, read.SimulationVersion);
            Assert.AreEqual(setup.BalanceHash, read.BalanceHash);
            Assert.IsTrue(setup.Equals(read));

            for (int length = 0; length < packet.Length; length++)
            {
                var prefix = new byte[length];
                System.Array.Copy(packet, prefix, length);
                Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetup(prefix, out _), "prefix " + length);
            }
            var longer = new byte[packet.Length + 1];
            System.Array.Copy(packet, longer, packet.Length);
            Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetup(longer, out _), "trailing byte");
            Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetup(null, out _));

            byte[] wrongType = (byte[])packet.Clone();
            wrongType[0] = (byte)PacketType.Heartbeat;
            Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetup(wrongType, out _));

            // An ID length that overruns the packet is refused, not read.
            byte[] overrun = (byte[])packet.Clone();
            overrun[1] = 250;
            Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetup(overrun, out _));
        }

        [Test]
        public void SetupAck_RoundTripsAndRejectsTruncation()
        {
            MatchSetup setup = Hourglass();
            foreach (bool accepted in new[] { true, false })
            {
                byte[] packet = DraftSerializer.SerializeMatchSetupAck(setup, accepted);
                Assert.AreEqual(PacketType.MatchSetupAck, InputSerializer.ReadPacketType(packet));
                Assert.IsTrue(DraftSerializer.TryDeserializeMatchSetupAck(packet, out MatchSetup read, out bool got));
                Assert.IsTrue(setup.Equals(read));
                Assert.AreEqual(accepted, got);

                for (int length = 0; length < packet.Length; length++)
                {
                    var prefix = new byte[length];
                    System.Array.Copy(packet, prefix, length);
                    Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetupAck(prefix, out _, out _), "prefix " + length);
                }
            }

            byte[] bad = DraftSerializer.SerializeMatchSetupAck(setup, true);
            bad[bad.Length - 1] = 2;
            Assert.IsFalse(DraftSerializer.TryDeserializeMatchSetupAck(bad, out _, out _), "accepted flag is 0 or 1");
        }

        [Test]
        public void Setup_UnsupportedMapIsRefusedByTheGuest()
        {
            var host = SetupAgreement.Host(new MatchSetup("not-shipped", 5, Sim, Balance));
            SetupAgreement guest = Guest();

            byte[] reply = guest.Receive(host.NextToSend());

            Assert.IsTrue(guest.Refused);
            Assert.IsFalse(guest.Agreed);
            StringAssert.Contains("map", guest.Error);
            Assert.IsNotNull(reply, "The guest tells the host it refused.");
            host.Receive(reply);
            Assert.IsTrue(host.Refused);
            Assert.IsFalse(host.Agreed);
            Assert.IsNull(host.NextToSend(), "A refused host stops proposing.");
        }

        [Test]
        public void Setup_ConflictingAckFails_AndRepeatedIdenticalSetupIsIdempotent()
        {
            SetupAgreement host = SetupAgreement.Host(Hourglass());
            byte[] proposal = host.NextToSend();

            // An ack for some other setup is a conflict, not agreement.
            var other = new MatchSetup("hourglass-01", Hourglass().BoardHash + 1, Sim, Balance);
            host.Receive(DraftSerializer.SerializeMatchSetupAck(other, true));
            Assert.IsTrue(host.Refused);
            Assert.IsFalse(host.Agreed);

            SetupAgreement guest = Guest();
            byte[] first = guest.Receive(proposal);
            byte[] second = guest.Receive(proposal);
            Assert.IsTrue(guest.Agreed);
            Assert.IsFalse(guest.Refused);
            CollectionAssert.AreEqual(first, second, "A repeated setup gets the same ack.");

            // A different setup after agreement is a conflict.
            guest.Receive(DraftSerializer.SerializeMatchSetup(other));
            Assert.IsTrue(guest.Refused);
        }

        [Test]
        public void Setup_GuestChecksTheAllocatedMapWhenItHasOne()
        {
            SetupAgreement guest = Guest(expectedMapId: "some-other-shipped-map");
            guest.Receive(DraftSerializer.SerializeMatchSetup(Hourglass()));
            Assert.IsTrue(guest.Refused, "A valid map that is not the allocated one is still refused.");

            SetupAgreement fine = Guest(expectedMapId: PremadeMaps.Hourglass01Id);
            fine.Receive(DraftSerializer.SerializeMatchSetup(Hourglass()));
            Assert.IsTrue(fine.Agreed);
        }

        [Test]
        public void Setup_OtherPacketsAndGarbageChangeNothing()
        {
            SetupAgreement host = SetupAgreement.Host(Hourglass());
            SetupAgreement guest = Guest();
            foreach (SetupAgreement side in new[] { host, guest })
            {
                Assert.IsNull(side.Receive(null));
                Assert.IsNull(side.Receive(new byte[0]));
                Assert.IsNull(side.Receive(InputSerializer.SerializeHeartbeat()));
                Assert.IsNull(side.Receive(new byte[] { (byte)PacketType.MatchSetup, 3 }));
                Assert.IsNull(side.Receive(new byte[] { (byte)PacketType.MatchSetupAck }));
                Assert.IsFalse(side.Agreed);
                Assert.IsFalse(side.Refused);
            }
            Assert.IsNull(guest.NextToSend(), "The guest only answers.");
        }

        // A link that drops whatever the test tells it to.
        private sealed class Wire
        {
            public readonly List<byte[]> Delivered = new List<byte[]>();
            public bool Drop;
            public byte[] Carry(byte[] packet)
            {
                if (packet == null || Drop) return null;
                Delivered.Add(packet);
                return packet;
            }
        }

        [Test]
        public void DraftCannotStartBeforeMatchingSetupAck()
        {
            SetupAgreement host = SetupAgreement.Host(Hourglass());
            SetupAgreement guest = Guest();
            var toGuest = new Wire();
            var toHost = new Wire();

            int appliedByHost = 0;
            void DraftPlacementArrivesAtHost()
            {
                // What DraftManager does with a placement: honour it only once agreed.
                if (host.AcceptsDraftPackets) appliedByHost++;
            }

            Assert.IsFalse(host.AcceptsDraftPackets);
            Assert.IsFalse(guest.AcceptsDraftPackets);

            // The proposal is lost.
            toGuest.Drop = true;
            Assert.IsNull(toGuest.Carry(host.NextToSend()));
            DraftPlacementArrivesAtHost();
            Assert.IsFalse(guest.AcceptsDraftPackets);

            // Retried and delivered, but the guest's ack is lost.
            toGuest.Drop = false;
            byte[] ack = guest.Receive(toGuest.Carry(host.NextToSend()));
            Assert.IsTrue(guest.Agreed);
            toHost.Drop = true;
            Assert.IsNull(toHost.Carry(ack));
            DraftPlacementArrivesAtHost();
            Assert.IsFalse(host.AcceptsDraftPackets, "The host has not seen the ack.");
            Assert.AreEqual(0, appliedByHost, "Zero placements before the ack.");
            Assert.IsNotNull(host.NextToSend(), "The host keeps proposing until acknowledged.");

            // The retry gets through, and so does the answer.
            toHost.Drop = false;
            byte[] again = guest.Receive(toGuest.Carry(host.NextToSend()));
            host.Receive(toHost.Carry(again));
            Assert.IsTrue(host.Agreed);
            Assert.IsTrue(host.AcceptsDraftPackets);
            Assert.IsNull(host.NextToSend(), "Agreed: nothing more to send.");
            DraftPlacementArrivesAtHost();
            Assert.AreEqual(1, appliedByHost);
        }

        [Test]
        public void DraftCannotStartBeforeMatchingSetupAck_ChangedTerrainHashRefusesInsteadOfStarting()
        {
            MatchSetup honest = Hourglass();
            var tampered = new MatchSetup(honest.MapId, honest.BoardHash ^ 0x5A5A, Sim, Balance);
            SetupAgreement host = SetupAgreement.Host(tampered);
            SetupAgreement guest = Guest();

            byte[] reply = guest.Receive(host.NextToSend());
            host.Receive(reply);

            Assert.IsTrue(guest.Refused);
            Assert.IsTrue(host.Refused);
            Assert.IsFalse(guest.AcceptsDraftPackets);
            Assert.IsFalse(host.AcceptsDraftPackets);
            StringAssert.Contains("board", guest.Error);
        }
    }
}
