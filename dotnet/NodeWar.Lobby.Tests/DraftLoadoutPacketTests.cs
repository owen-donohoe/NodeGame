using System;
using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The receive-side check on a draft loadout packet. It once validated only
    /// the two ID arrays, so after eras and skins joined the packet every real
    /// loadout failed it and networked drafts never started. These pin the
    /// check to the writer's full layout.
    /// </summary>
    [TestFixture]
    public class DraftLoadoutPacketTests
    {
        private static LoadoutData Full()
        {
            var loadout = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = new[] { "warrior", "scout" },
                districtIDs = new[] { "farm" },
                skinIDs = new[] { "skin.suit.warrior.default" }
            });
            loadout.suitEras[3] = 1;
            return loadout;
        }

        [Test]
        public void WhatTheWriterSends_IsAccepted()
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(1, Full());

            Assert.IsTrue(DraftSerializer.TryDeserializeDraftLoadout(packet, out int player, out LoadoutData loadout));
            Assert.AreEqual(1, player);
            Assert.AreEqual(1, loadout.suitEras[3]);
            CollectionAssert.Contains(loadout.skinIDs, "skin.suit.warrior.default");
        }

        [Test]
        public void AnOlderPacketEndingAfterTheIdArrays_IsAccepted()
        {
            byte[] full = DraftSerializer.SerializeDraftLoadout(0, Full());
            // Type, player, then the two ID arrays: walk them to find where they end.
            int offset = 5;
            for (int array = 0; array < 2; array++)
            {
                int count = full[offset++];
                for (int i = 0; i < count; i++) offset += 1 + full[offset];
            }
            byte[] legacy = new byte[offset];
            Array.Copy(full, legacy, offset);

            Assert.IsTrue(DraftSerializer.TryDeserializeDraftLoadout(legacy, out int player, out _));
            Assert.AreEqual(0, player);
        }

        [Test]
        public void EveryTruncationInsideASection_IsRefusedWithoutThrowing()
        {
            byte[] full = DraftSerializer.SerializeDraftLoadout(1, Full());
            int accepted = 0;
            for (int length = 0; length < full.Length; length++)
            {
                byte[] cut = new byte[length];
                Array.Copy(full, cut, length);
                if (DraftSerializer.TryDeserializeDraftLoadout(cut, out _, out _)) accepted++;
            }
            // Only cuts that land exactly on a section boundary after the ID
            // arrays read as an older, shorter packet: after the districts, and
            // after each era table.
            Assert.LessOrEqual(accepted, 3);
        }

        [Test]
        public void TrailingBytes_AreRefused()
        {
            byte[] full = DraftSerializer.SerializeDraftLoadout(1, Full());
            byte[] longer = new byte[full.Length + 1];
            Array.Copy(full, longer, full.Length);

            Assert.IsFalse(DraftSerializer.TryDeserializeDraftLoadout(longer, out _, out _));
        }

        [Test]
        public void AnotherPacketType_IsRefused()
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(1, Full());
            packet[0] = (byte)PacketType.Heartbeat;

            Assert.IsFalse(DraftSerializer.TryDeserializeDraftLoadout(packet, out _, out _));
        }
    }
}
