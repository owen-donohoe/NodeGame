using System;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class PacketRobustnessTests
    {
        private static TickInput Representative(int tick)
        {
            Array types = Enum.GetValues(typeof(CommandType));
            var commands = new GameCommand[types.Length];
            for (int i = 0; i < commands.Length; i++)
                commands[i] = new GameCommand
                {
                    type = (CommandType)types.GetValue(i), playerID = i % 2,
                    villagerID = 17 + i, targetNodeID = 4 + i,
                    issuedOnTick = tick, value = i % 2 == 0 ? int.MinValue : int.MaxValue
                };
            return new TickInput
            {
                forTick = tick, stateHash = int.MinValue, commands = commands,
                senderDelay = 5, requestedDelay = 255
            };
        }

        [TestCase(0)]
        [TestCase(50)]
        [TestCase(int.MaxValue)]
        public void TickInput_RoundTripsEveryCommandField(int tick)
        {
            TickInput expected = Representative(tick);
            byte[] packet = InputSerializer.Serialize(expected);
            Assert.AreEqual(15 + 24 * expected.commands.Length, packet.Length);
            Assert.IsTrue(InputSerializer.TryDeserialize(packet, out TickInput actual));
            Assert.AreEqual(expected.forTick, actual.forTick);
            Assert.AreEqual(expected.stateHash, actual.stateHash);
            Assert.AreEqual(expected.senderDelay, actual.senderDelay);
            Assert.AreEqual(expected.requestedDelay, actual.requestedDelay);
            CollectionAssert.AreEqual(expected.commands, actual.commands);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TickInput_NullOrEmptyCommandsReadAsEmpty(bool useNull)
        {
            var input = new TickInput { commands = useNull ? null : Array.Empty<GameCommand>() };
            byte[] packet = InputSerializer.Serialize(input);
            Assert.AreEqual(15, packet.Length);
            Assert.IsTrue(InputSerializer.TryDeserialize(packet, out TickInput actual));
            Assert.IsEmpty(actual.commands);
        }

        [Test]
        public void TickInput_EveryStrictPrefixIsRefusedWithoutThrowing()
        {
            byte[] packet = InputSerializer.Serialize(Representative(50));
            for (int length = 0; length < packet.Length; length++)
            {
                var prefix = new byte[length];
                Array.Copy(packet, prefix, length);
                bool accepted = true;
                Assert.DoesNotThrow(() => accepted = InputSerializer.TryDeserialize(prefix, out _),
                    "prefix length " + length);
                Assert.IsFalse(accepted, "prefix length " + length);
            }
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void TickInput_OutOfRangeCommandCountsAreRefused(int count)
        {
            byte[] packet = InputSerializer.Serialize(Representative(50));
            WriteInt(packet, 9, count);
            Assert.IsFalse(InputSerializer.TryDeserialize(packet, out _));
        }

        [Test]
        public void TickInput_TrailingBytesNullAndLengthMismatchesAreRefused()
        {
            Assert.IsFalse(InputSerializer.TryDeserialize(null, out _));
            byte[] packet = InputSerializer.Serialize(Representative(50));
            var extra = new byte[packet.Length + 1];
            Array.Copy(packet, extra, packet.Length);
            Assert.IsFalse(InputSerializer.TryDeserialize(extra, out _));
            WriteInt(packet, 9, 0);
            Assert.IsFalse(InputSerializer.TryDeserialize(packet, out _));
        }

        [Test]
        public void TickInput_SeededGarbageNeverThrows()
        {
            var random = new Random(27013);
            for (int sample = 0; sample < 2000; sample++)
            {
                var bytes = new byte[random.Next(0, 2049)];
                random.NextBytes(bytes);
                // Half the samples have a plausible tag: don't only fuzz routing.
                if (bytes.Length > 0 && sample % 2 == 0) bytes[0] = (byte)PacketType.TickInput;
                Assert.DoesNotThrow(() => InputSerializer.TryDeserialize(bytes, out _), "sample " + sample);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DraftAck_RoundTripsFlagsAndPlacementBoundary(bool ready, bool loadout)
        {
            byte[] bytes = InputSerializer.SerializeDraftAck(ready, loadout, int.MaxValue);
            Assert.AreEqual(6, bytes.Length);
            Assert.IsTrue(InputSerializer.TryDeserializeDraftAck(bytes, out bool r, out bool l, out int count));
            Assert.AreEqual(ready, r);
            Assert.AreEqual(loadout, l);
            Assert.AreEqual(int.MaxValue, count);
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void DraftAck_NegativePlacementCountIsRefused(int count)
        {
            byte[] bytes = InputSerializer.SerializeDraftAck(true, true, count);
            Assert.IsFalse(InputSerializer.TryDeserializeDraftAck(bytes, out _, out _, out _));
        }

        [TestCase(4)]
        [TestCase(255)]
        public void DraftAck_UnknownFlagBitsAreRefused(int flags)
        {
            byte[] bytes = InputSerializer.SerializeDraftAck(false, false, 0);
            bytes[1] = (byte)flags;
            Assert.IsFalse(InputSerializer.TryDeserializeDraftAck(bytes, out _, out _, out _));
        }

        [Test]
        public void DraftAck_EveryStrictPrefixIsRefusedWithoutThrowing()
        {
            byte[] packet = InputSerializer.SerializeDraftAck(true, true, 12);
            for (int length = 0; length < packet.Length; length++)
            {
                var prefix = new byte[length];
                Array.Copy(packet, prefix, length);
                Assert.IsFalse(InputSerializer.TryDeserializeDraftAck(prefix, out _, out _, out _));
            }
        }

        [Test]
        public void DraftLoadout_SeededGarbageNeverThrows()
        {
            var random = new Random(27014);
            for (int sample = 0; sample < 1000; sample++)
            {
                var bytes = new byte[random.Next(0, 1025)];
                random.NextBytes(bytes);
                if (bytes.Length > 0) bytes[0] = (byte)PacketType.DraftLoadout;
                Assert.DoesNotThrow(() => DraftSerializer.TryDeserializeDraftLoadout(bytes, out _, out _),
                    "sample " + sample);
            }
        }

        // Tick range, enum validity and a fixed command cap are receive-policy gaps,
        // not rejection guarantees of TryDeserialize today. Do not fake their coverage.
        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
        }
    }
}
