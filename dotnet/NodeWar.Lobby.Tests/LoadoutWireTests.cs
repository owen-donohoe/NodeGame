using System.Linq;
using NodeWar.Lobby;
using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The loadout wire format: LoadoutData through DraftSerializer and back.
    ///
    /// Every expectation derives from LoadoutData.SuitSlots and NodeSlots rather
    /// than hardcoding 3 and 2. That is the whole point of the array-backed
    /// shape - how many suits and districts a player brings is an open balance
    /// question, and moving it must not require editing a serializer, a save
    /// migration, or this file. If a test here needs changing because a slot
    /// count changed, the design has regressed.
    /// </summary>
    [TestFixture]
    public class LoadoutWireTests
    {
        private static int S => LoadoutData.SuitSlots;
        private static int N => LoadoutData.NodeSlots;

        /// <summary>An array of `length` slots: `head` entries first, then empty strings.</summary>
        private static string[] Slots(int length, params string[] head)
        {
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
                result[i] = (head != null && i < head.Length) ? head[i] : "";
            return result;
        }

        private static LoadoutData RoundTrip(LoadoutData source, int playerID)
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(playerID, source);
            DraftSerializer.DeserializeDraftLoadout(packet, out int _, out LoadoutData decoded);
            return decoded;
        }

        // ===== Normalized =====

        [Test]
        public void DefaultStruct_NormalizesToEmptySlots()
        {
            LoadoutData result = LoadoutData.Normalized(new LoadoutData());

            Assert.AreEqual(Slots(S), result.suitIDs);
            Assert.AreEqual(Slots(N), result.nodeIDs);
        }

        [Test]
        public void CreateEmpty_AllocatesBothArraysAtSlotCount()
        {
            LoadoutData result = LoadoutData.CreateEmpty();

            Assert.AreEqual(S, result.suitIDs.Length);
            Assert.AreEqual(N, result.nodeIDs.Length);
            Assert.IsTrue(result.suitIDs.All(s => s == ""));
            Assert.IsTrue(result.nodeIDs.All(s => s == ""));
        }

        [Test]
        public void Normalized_PadsShortArrays()
        {
            LoadoutData result = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = new[] { "suit_scout" },
                nodeIDs = new string[0]
            });

            Assert.AreEqual(Slots(S, "suit_scout"), result.suitIDs);
            Assert.AreEqual(Slots(N), result.nodeIDs);
        }

        [Test]
        public void Normalized_DropsSurplusEntries()
        {
            string[] tooManySuits = Enumerable.Range(0, S + 3).Select(i => "s" + i).ToArray();
            string[] tooManyNodes = Enumerable.Range(0, N + 3).Select(i => "n" + i).ToArray();

            LoadoutData result = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = tooManySuits,
                nodeIDs = tooManyNodes
            });

            Assert.AreEqual(tooManySuits.Take(S).ToArray(), result.suitIDs);
            Assert.AreEqual(tooManyNodes.Take(N).ToArray(), result.nodeIDs);
        }

        [Test]
        public void Normalized_ReplacesNullEntriesWithEmptyStrings()
        {
            LoadoutData result = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = new string[S],
                nodeIDs = new string[N]
            });

            Assert.IsTrue(result.suitIDs.All(s => s == ""));
            Assert.IsTrue(result.nodeIDs.All(s => s == ""));
        }

        [Test]
        public void Normalized_ReturnsACopyNotAnAlias()
        {
            LoadoutData source = new LoadoutData
            {
                suitIDs = Slots(S, "a"),
                nodeIDs = Slots(N, "x")
            };

            LoadoutData copy = LoadoutData.Normalized(source);
            copy.suitIDs[0] = "MUTATED";

            Assert.AreEqual("a", source.suitIDs[0],
                "Normalized must not hand back the caller's own array.");
        }

        // ===== Round trip =====

        [Test]
        public void FullLoadout_SurvivesRoundTrip()
        {
            string[] suitPool = { "suit_warrior", "suit_guardian", "suit_berserker", "suit_scout", "suit_medic" };
            string[] nodePool = { "node_watchtower", "node_market", "node_shrine", "node_camp" };

            LoadoutData original = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Enumerable.Range(0, S).Select(i => suitPool[i % suitPool.Length]).ToArray(),
                nodeIDs = Enumerable.Range(0, N).Select(i => nodePool[i % nodePool.Length]).ToArray()
            });

            LoadoutData decoded = RoundTrip(original, 1);

            Assert.AreEqual(original.suitIDs, decoded.suitIDs);
            Assert.AreEqual(original.nodeIDs, decoded.nodeIDs);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(255)]
        [TestCase(65536)]
        [TestCase(int.MaxValue)]
        public void PlayerID_SurvivesRoundTrip(int playerID)
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(playerID, LoadoutData.CreateEmpty());
            DraftSerializer.DeserializeDraftLoadout(packet, out int decoded, out LoadoutData _);

            Assert.AreEqual(playerID, decoded);
        }

        [Test]
        public void EmptyLoadout_SurvivesRoundTrip()
        {
            LoadoutData decoded = RoundTrip(LoadoutData.CreateEmpty(), 0);

            Assert.AreEqual(Slots(S), decoded.suitIDs);
            Assert.AreEqual(Slots(N), decoded.nodeIDs);
        }

        /// <summary>
        /// A `default` LoadoutData has null arrays. Serializing one must not
        /// throw - MatchConnection carries a plain struct across the scene load,
        /// and a bot match never fills it in.
        /// </summary>
        [Test]
        public void DefaultStruct_SerializesWithoutThrowing()
        {
            LoadoutData decoded = RoundTrip(new LoadoutData(), 0);

            Assert.AreEqual(Slots(S), decoded.suitIDs);
            Assert.AreEqual(Slots(N), decoded.nodeIDs);
        }

        [Test]
        public void PartiallyFilledLoadout_SurvivesRoundTrip()
        {
            LoadoutData partial = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Slots(S, "suit_medic"),
                nodeIDs = Slots(N)
            });
            partial.nodeIDs[N - 1] = "node_shrine";

            LoadoutData decoded = RoundTrip(partial, 1);

            Assert.AreEqual(partial.suitIDs, decoded.suitIDs);
            Assert.AreEqual(partial.nodeIDs, decoded.nodeIDs);
        }

        // ===== Byte layout =====

        /// <summary>
        /// The packet is exactly as long as its contents require, with no slack.
        /// A serializer that over-allocates still round-trips, so only an exact
        /// size assertion catches it.
        /// </summary>
        [Test]
        public void PacketLength_IsExact()
        {
            LoadoutData loadout = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Slots(S, "ab"),
                nodeIDs = Slots(N, "de")
            });

            byte[] packet = DraftSerializer.SerializeDraftLoadout(7, loadout);

            // type(1) + playerID(4)
            // + suitCount(1) + one length byte per suit slot + 2 payload bytes
            // + nodeCount(1) + one length byte per node slot + 2 payload bytes
            int expected = 1 + 4 + (1 + S + 2) + (1 + N + 2);

            Assert.AreEqual(expected, packet.Length);
        }

        [Test]
        public void PacketHeader_IsTypeThenLittleEndianPlayerID()
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(7, LoadoutData.CreateEmpty());

            Assert.AreEqual((byte)PacketType.DraftLoadout, packet[0]);

            int playerID = packet[1] | (packet[2] << 8) | (packet[3] << 16) | (packet[4] << 24);
            Assert.AreEqual(7, playerID);
        }

        /// <summary>
        /// Each array is preceded by its own count byte. This is what makes the
        /// format independent of the slot counts.
        /// </summary>
        [Test]
        public void EachArray_IsCountPrefixed()
        {
            LoadoutData loadout = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Slots(S, "ab"),
                nodeIDs = Slots(N, "de")
            });

            byte[] packet = DraftSerializer.SerializeDraftLoadout(0, loadout);

            Assert.AreEqual(S, packet[5], "suit count byte");
            Assert.AreEqual(2, packet[6], "first suit length byte");

            int nodeCountOffset = 5 + 1 + S + 2; // suit count, its length bytes, its payload
            Assert.AreEqual(N, packet[nodeCountOffset], "node count byte");
        }

        // ===== Edge cases =====

        /// <summary>
        /// One byte per length caps an ID at 255 bytes. An over-long ID must be
        /// clamped rather than allowed to write a wrong length byte, which would
        /// misalign every field after it and desync the draft.
        /// </summary>
        [Test]
        public void OverLongID_IsClampedWithoutMisaligningLaterFields()
        {
            LoadoutData big = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Slots(S, new string('x', 400), "suit_scout"),
                nodeIDs = Slots(N, "node_camp")
            });

            LoadoutData decoded = RoundTrip(big, 0);

            Assert.AreEqual(255, decoded.suitIDs[0].Length);
            if (S > 1) Assert.AreEqual("suit_scout", decoded.suitIDs[1]);
            Assert.AreEqual("node_camp", decoded.nodeIDs[0]);
        }

        [Test]
        public void MultiByteUtf8_SurvivesRoundTrip()
        {
            LoadoutData utf8 = LoadoutData.Normalized(new LoadoutData
            {
                suitIDs = Slots(S, "suit_éè", "日本語"),
                nodeIDs = Slots(N, "node_ü")
            });

            LoadoutData decoded = RoundTrip(utf8, 0);

            Assert.AreEqual(utf8.suitIDs, decoded.suitIDs);
            Assert.AreEqual(utf8.nodeIDs, decoded.nodeIDs);
        }
    }
}
