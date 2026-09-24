using NodeWar.Core;
using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class EmotePacketTests
    {
        [TestCase(0, EmoteType.Happy, 0)]
        [TestCase(1, EmoteType.Sad, 1)]
        [TestCase(0, EmoteType.Angry, 65535)]
        [TestCase(1, EmoteType.WhiteFlag, 4660)]
        public void RoundTrip(int player, EmoteType emote, int sequence)
        {
            byte[] packet = InputSerializer.SerializeEmote(player, emote, (ushort)sequence);
            Assert.AreEqual(5, packet.Length);
            Assert.AreEqual(8, packet[0]);
            Assert.AreEqual(sequence & 255, packet[3]);
            Assert.AreEqual(sequence >> 8, packet[4]);
            Assert.IsTrue(InputSerializer.TryDeserializeEmote(packet, out int actualPlayer,
                out EmoteType actualEmote, out ushort actualSequence));
            Assert.AreEqual(player, actualPlayer);
            Assert.AreEqual(emote, actualEmote);
            Assert.AreEqual(sequence, actualSequence);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(100)]
        public void RejectsWrongLength(int length)
        {
            var packet = new byte[length];
            if (length > 0) packet[0] = 8;
            Assert.IsFalse(InputSerializer.TryDeserializeEmote(packet, out _, out _, out _));
        }

        [Test]
        public void RejectsNull()
        {
            Assert.IsFalse(InputSerializer.TryDeserializeEmote(null, out _, out _, out _));
        }

        [TestCase(0, 7)]
        [TestCase(0, 255)]
        [TestCase(1, 2)]
        [TestCase(1, 255)]
        [TestCase(2, 4)]
        [TestCase(2, 255)]
        public void RejectsMalformedFields(int index, int value)
        {
            byte[] packet = InputSerializer.SerializeEmote(0, EmoteType.Happy, 0);
            packet[index] = (byte)value;
            Assert.IsFalse(InputSerializer.TryDeserializeEmote(packet, out _, out _, out _));
        }
    }
}
