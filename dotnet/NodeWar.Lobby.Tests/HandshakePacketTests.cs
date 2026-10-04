using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class HandshakePacketTests
    {
        private static readonly BuildIdentity Local = new BuildIdentity(1, 7, -123456789);

        [Test]
        public void Handshake_RoundTrips()
        {
            byte[] data = InputSerializer.SerializeHandshake(Local);

            Assert.That(data.Length, Is.EqualTo(9));
            Assert.That(InputSerializer.ReadPacketType(data), Is.EqualTo(PacketType.Handshake));
            Assert.That(InputSerializer.TryDeserializeHandshake(data, out BuildIdentity back), Is.True);
            Assert.That(back, Is.EqualTo(Local));
        }

        [Test]
        public void AckAndReject_RoundTrip_AndAreNotEachOther()
        {
            byte[] ack = InputSerializer.SerializeHandshakeAck(Local);
            byte[] reject = InputSerializer.SerializeHandshakeReject(Local);

            Assert.That(InputSerializer.TryDeserializeHandshakeAck(ack, out BuildIdentity a), Is.True);
            Assert.That(InputSerializer.TryDeserializeHandshakeReject(reject, out BuildIdentity r), Is.True);
            Assert.That(a, Is.EqualTo(Local));
            Assert.That(r, Is.EqualTo(Local));

            Assert.That(InputSerializer.TryDeserializeHandshakeAck(reject, out _), Is.False);
            Assert.That(InputSerializer.TryDeserializeHandshakeReject(ack, out _), Is.False);
            Assert.That(InputSerializer.TryDeserializeHandshake(ack, out _), Is.False);
        }

        // Protocol 0 builds send a bare type byte. They must read as unreadable,
        // which the lobby treats as an older peer, never as a match.
        [Test]
        public void LegacyOneByteHandshake_IsUnreadable()
        {
            Assert.That(InputSerializer.TryDeserializeHandshake(new byte[] { (byte)PacketType.Handshake }, out _), Is.False);
            Assert.That(InputSerializer.TryDeserializeHandshakeAck(new byte[] { (byte)PacketType.HandshakeAck }, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(10)]
        public void WrongLength_IsRefused(int length)
        {
            byte[] data = new byte[length];
            if (length > 0) data[0] = (byte)PacketType.Handshake;

            Assert.That(InputSerializer.TryDeserializeHandshake(data, out _), Is.False);
        }

        [Test]
        public void NullIsRefused()
        {
            Assert.That(InputSerializer.TryDeserializeHandshake(null, out _), Is.False);
        }

        // The first three bytes are the part every future build must be able to read.
        [Test]
        public void TypeAndProtocol_AreTheFirstThreeBytes_LittleEndian()
        {
            byte[] data = InputSerializer.SerializeHandshake(new BuildIdentity(0x0102, 0, 0));

            Assert.That(data[0], Is.EqualTo((byte)PacketType.Handshake));
            Assert.That(data[1], Is.EqualTo(0x02));
            Assert.That(data[2], Is.EqualTo(0x01));
        }

        [TestCase(1, 7, -123456789, HandshakeVerdict.Compatible)]
        [TestCase(0, 7, -123456789, HandshakeVerdict.PeerOlder)]
        [TestCase(2, 7, -123456789, HandshakeVerdict.PeerNewer)]
        [TestCase(1, 6, -123456789, HandshakeVerdict.PeerOlder)]
        [TestCase(1, 8, -123456789, HandshakeVerdict.PeerNewer)]
        [TestCase(1, 7, 42, HandshakeVerdict.ContentMismatch)]
        public void Compare_ProtocolThenSimThenContent(int protocol, int sim, int content, HandshakeVerdict expected)
        {
            var peer = new BuildIdentity((ushort)protocol, (ushort)sim, content);

            Assert.That(InputSerializer.Compare(Local, peer), Is.EqualTo(expected));
        }

        // Protocol outranks sim: a newer wire with an older sim is still "newer".
        [Test]
        public void Compare_ProtocolDecidesBeforeSim()
        {
            var peer = new BuildIdentity(2, 1, 0);

            Assert.That(InputSerializer.Compare(Local, peer), Is.EqualTo(HandshakeVerdict.PeerNewer));
        }
    }
}
