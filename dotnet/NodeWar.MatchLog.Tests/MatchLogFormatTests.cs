using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    public class MatchLogFormatTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void RoundTrip_EveryField(bool finished)
        {
            MatchLog expected = TestLogs.Full();
            if (!finished) expected.result = null;
            TestLogs.Equal(expected, TestLogs.Read(MatchLogFormat.Write(expected)));
        }

        [Test]
        public void UnknownChunks_BetweenLoadoutsAndTicksAndAtEnd_AreSkipped()
        {
            MatchLog expected = TestLogs.Full();
            byte[] bytes = MatchLogFormat.Write(expected);
            byte[] unknown = { 0x00, 0x7F, 5, 0, 0, 0, 19, 23, 42, 127, 255 };
            int loadouts = TestLogs.Find(bytes, 3);
            bytes = TestLogs.Insert(bytes, loadouts + 6 + TestLogs.IntAt(bytes, loadouts + 2), unknown);
            bytes = TestLogs.Insert(bytes, bytes.Length, unknown);
            TestLogs.Equal(expected, TestLogs.Read(bytes));
        }

        [Test]
        public void EveryStrictPrefix_OfMinimalValidFile_IsRefused()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            bytes = TestLogs.Remove(TestLogs.Remove(TestLogs.Remove(bytes, 7), 6), 4);
            TestLogs.Read(bytes);
            for (int length = 0; length < bytes.Length; length++)
                TestLogs.Refused(TestLogs.Segment(bytes, 0, length), "Prefix length " + length);
        }

        [Test]
        public void FullFilePrefixes_OnlyCompleteOptionalChunkBoundariesAreValid()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int hashes = TestLogs.Find(bytes, 6);
            int result = TestLogs.Find(bytes, 7);
            for (int length = 0; length < bytes.Length; length++)
            {
                byte[] prefix = TestLogs.Segment(bytes, 0, length);
                if (length == hashes || length == result)
                    Assert.IsNull(TestLogs.Read(prefix).result);
                else TestLogs.Refused(prefix, "Prefix length " + length);
            }
        }

        [Test]
        public void Null_IsRefused() { TestLogs.Refused(null); }

        [Test]
        public void BadMagic_IsRefused()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            bytes[0] = (byte)'X';
            TestLogs.Refused(bytes);
        }

        [Test]
        public void NewFramingVersion_IsRefused()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            bytes[4] = 2;
            TestLogs.Refused(bytes);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void DuplicateKnownChunk_IsRefused(int tag)
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int start = TestLogs.Find(bytes, tag);
            TestLogs.Refused(TestLogs.Insert(bytes, bytes.Length,
                TestLogs.Segment(bytes, start, 6 + TestLogs.IntAt(bytes, start + 2))));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(5)]
        public void MissingRequiredChunk_IsRefused(int tag)
        {
            TestLogs.Refused(TestLogs.Remove(MatchLogFormat.Write(TestLogs.Full()), tag));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void KnownChunkTrailingByte_IsRefused(int tag)
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int start = TestLogs.Find(bytes, tag);
            int length = TestLogs.IntAt(bytes, start + 2);
            bytes = TestLogs.Insert(bytes, start + 6 + length, new byte[] { 0x99 });
            TestLogs.PutInt(bytes, start + 2, length + 1);
            TestLogs.Refused(bytes);
        }

        [TestCase(2, 48)] [TestCase(3, 0)] [TestCase(3, 16)]
        [TestCase(3, 28)] [TestCase(3, 40)] [TestCase(4, 0)]
        [TestCase(5, 0)] [TestCase(6, 0)]
        public void InvalidCounts_AreRefusedBeforeAllocation(int tag, int countOffset)
        {
            foreach (int count in new[] { -1, int.MaxValue })
            {
                byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
                TestLogs.PutInt(bytes, TestLogs.Find(bytes, tag) + 6 + countOffset, count);
                TestLogs.Refused(bytes, "Count " + count);
            }
        }

        [Test]
        public void NestedCommandCount_IsBoundedByPayload()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int offset = TestLogs.Find(bytes, 5) + 6 + 4 + 4;
            bytes[offset] = 255; bytes[offset + 1] = 255;
            TestLogs.Refused(bytes);
        }

        [Test]
        public void StringLength_IsBoundedByPayload()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int offset = TestLogs.Find(bytes, 1) + 6 + 8;
            bytes[offset] = 255; bytes[offset + 1] = 255;
            TestLogs.Refused(bytes);
        }

        [Test]
        public void UnsignedChunkLength_CannotOverflowPastBounds()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            TestLogs.PutInt(bytes, 8, -1);
            TestLogs.Refused(bytes);
            bytes[6] = 0; bytes[7] = 127; // Unknown chunks have the same bounds check.
            TestLogs.Refused(bytes);
        }

        [TestCase(1)] [TestCase(7)]
        public void InvalidMatchEnums_AreRefused(int tag)
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int start = TestLogs.Find(bytes, tag);
            int offset = tag == 1 ? start + 6 + TestLogs.IntAt(bytes, start + 2) - 1 : start + 6;
            bytes[offset] = 255;
            TestLogs.Refused(bytes);
        }

        [Test]
        public void DefinedMatchEnums_AndUnknownSimulationEnums_RoundTrip()
        {
            MatchLog log = TestLogs.Full();
            log.board.initialPlacements[0].districtType = (DistrictType)12345;
            log.draft[0].districtType = (DistrictType)(-12345);
            log.ticks[0].commands[0].type = (CommandType)int.MaxValue;
            foreach (MatchKind kind in Enum.GetValues(typeof(MatchKind)))
                foreach (MatchEndReason reason in Enum.GetValues(typeof(MatchEndReason)))
                {
                    log.header.kind = kind; log.result.reason = reason;
                    TestLogs.Equal(log, TestLogs.Read(MatchLogFormat.Write(log)));
                }
        }

        [Test]
        public void NullStringsAndCollections_WriteAsEmpty_AndDraftIsAlwaysPresent()
        {
            MatchLog log = TestLogs.Full();
            log.header.matchId = null; log.header.playerIds = new string[2];
            log.board.initialPlacements = null; log.draft = null; log.ticks = null; log.hashes = null;
            log.loadouts = new[] { new PlayerLoadout(), new PlayerLoadout() };
            byte[] bytes = MatchLogFormat.Write(log);
            Assert.AreEqual(4, TestLogs.IntAt(bytes, TestLogs.Find(bytes, 4) + 2));
            MatchLog read = TestLogs.Read(bytes);
            Assert.AreEqual("", read.header.matchId);
            CollectionAssert.AreEqual(new[] { "", "" }, read.header.playerIds);
            Assert.IsEmpty(read.board.initialPlacements); Assert.IsEmpty(read.draft);
            Assert.IsEmpty(read.ticks); Assert.IsEmpty(read.hashes);
            foreach (PlayerLoadout loadout in read.loadouts)
            { Assert.IsEmpty(loadout.suits); Assert.IsEmpty(loadout.nodes); }
        }

        [Test]
        public void Writer_RejectsLengthsThatWouldTruncateOnWire()
        {
            MatchLog log = TestLogs.Full();
            log.header.matchId = new string('é', 32768); // 65536 UTF-8 bytes, not characters.
            Assert.Throws<ArgumentException>(() => MatchLogFormat.Write(log));
            log = TestLogs.Full();
            log.ticks[0] = new LoggedTick { commands = new GameCommand[65536] };
            Assert.Throws<ArgumentException>(() => MatchLogFormat.Write(log));
        }

        [Test]
        public void WireLayout_UsesLittleEndianAndSignedWinner()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            CollectionAssert.AreEqual(new byte[] { 78, 87, 77, 76, 1, 0 }, TestLogs.Segment(bytes, 0, 6));
            int header = TestLogs.Find(bytes, 1) + 6;
            CollectionAssert.AreEqual(new byte[] { 0x34, 0x12, 0x78, 0x56, 0xEB, 0x32, 0xA4, 0xF8 },
                TestLogs.Segment(bytes, header, 8));
            int headerEnd = TestLogs.Find(bytes, 2);
            CollectionAssert.AreEqual(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1, 2 },
                TestLogs.Segment(bytes, headerEnd - 9, 9));
            int command = TestLogs.Find(bytes, 5) + 6 + 4 + 6;
            CollectionAssert.AreEqual(new byte[]
            {
                1, 0, 0, 0, 1, 0, 0, 0, 11, 0, 0, 0,
                4, 0, 0, 0, 3, 0, 0, 0, 254, 255, 255, 255
            }, TestLogs.Segment(bytes, command, 24));
            int result = TestLogs.Find(bytes, 7);
            Assert.AreEqual(14, TestLogs.IntAt(bytes, result + 2));
            Assert.AreEqual(255, bytes[result + 7]);
            Assert.AreEqual(-1, TestLogs.Read(bytes).result.winner);
        }

        [Test]
        public void GameCommandLayout_RequiresCoordinatedSerializerChanges()
        {
            const string message = "Changing GameCommand requires updating InputSerializer and MatchLogFormat (new TICKS tag) together.";
            FieldInfo[] fields = typeof(GameCommand).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "type", "playerID", "villagerID", "targetNodeID", "issuedOnTick", "value" },
                fields.Select(f => f.Name).ToArray(), message);
            Assert.AreEqual(typeof(CommandType), fields[0].FieldType, message);
            foreach (FieldInfo field in fields)
                Assert.AreEqual(typeof(int), field.FieldType.IsEnum ? Enum.GetUnderlyingType(field.FieldType) : field.FieldType, message);
        }

        [Test]
        public void ArbitraryBytes_NeverThrow()
        {
            Random random = new Random(90210);
            for (int i = 0; i < 500; i++)
            {
                byte[] bytes = new byte[random.Next(1024)];
                random.NextBytes(bytes);
                if (bytes.Length >= 6 && i % 2 == 0)
                    Array.Copy(new byte[] { 78, 87, 77, 76, 1, 0 }, bytes, 6);
                Assert.DoesNotThrow(() => MatchLogFormat.TryRead(bytes, out _, out _));
            }
        }
    }
}
