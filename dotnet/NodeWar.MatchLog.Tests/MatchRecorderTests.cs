using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    public class MatchRecorderTests
    {
        private static MatchRecorder Create()
        {
            MatchLog log = TestLogs.Full();
            return new MatchRecorder(log.header, log.board, log.loadouts, log.draft);
        }

        [Test]
        public void EmptyAndNullTicks_AreOmitted()
        {
            MatchRecorder recorder = Create();
            recorder.RecordTick(0, null);
            recorder.RecordTick(1, Array.Empty<GameCommand>());
            Assert.IsEmpty(recorder.Log.ticks);
            Assert.IsFalse(recorder.IsFinished);
        }

        [Test]
        public void TickArrays_AreCopied_AndInterleavedOrderIsPreserved()
        {
            MatchRecorder recorder = Create();
            GameCommand[] commands = TestLogs.Full().ticks[0].commands;
            GameCommand[] expected = (GameCommand[])commands.Clone();
            recorder.RecordTick(17, commands);
            commands[0] = default;
            recorder.RecordTick(29, new[] { expected[0] });
            Assert.AreEqual(17, recorder.Log.ticks[0].tick);
            Assert.AreEqual(29, recorder.Log.ticks[1].tick);
            Assert.AreNotSame(commands, recorder.Log.ticks[0].commands);
            TestLogs.Equal(expected, recorder.Log.ticks[0].commands);
            CollectionAssert.AreEqual(new[] { 1, 0, 1 },
                Array.ConvertAll(recorder.Log.ticks[0].commands, c => c.playerID));
        }

        [Test]
        public void FirstFinishWins_AndFurtherRecordsAreIgnored()
        {
            MatchRecorder recorder = Create();
            recorder.RecordTick(1, TestLogs.Full().ticks[0].commands);
            recorder.RecordHash(50, 1234);
            MatchResult first = new MatchResult { reason = MatchEndReason.Win, winner = 0, endTick = 73, finalHash = 4567 };
            recorder.Finish(first);
            byte[] bytes = recorder.ToBytes();
            recorder.Finish(new MatchResult { reason = MatchEndReason.Disconnect, winner = 1 });
            recorder.Finish(null); // Even an invalid later result is ignored.
            recorder.RecordTick(74, TestLogs.Full().ticks[1].commands);
            recorder.RecordHash(100, 999);
            recorder.RecordDesync(74);
            Assert.IsTrue(recorder.IsFinished);
            Assert.AreSame(first, recorder.Log.result);
            Assert.AreEqual(-1, recorder.Log.result.firstDesyncTick);
            CollectionAssert.AreEqual(bytes, recorder.ToBytes());
        }

        [Test]
        public void RecordDesync_KeepsFirstReport_NotSmallestTick()
        {
            MatchRecorder recorder = Create();
            recorder.RecordDesync(100);
            recorder.RecordDesync(50);
            recorder.Finish(new MatchResult { reason = MatchEndReason.Abandoned, firstDesyncTick = -1 });
            Assert.AreEqual(100, recorder.Log.result.firstDesyncTick);
            Assert.AreEqual(100, TestLogs.Read(recorder.ToBytes()).result.firstDesyncTick);
        }

        [Test]
        public void Finish_PreservesExplicitResultDesync()
        {
            MatchRecorder recorder = Create();
            recorder.RecordDesync(100);
            recorder.Finish(new MatchResult { reason = MatchEndReason.Win, firstDesyncTick = 75 });
            Assert.AreEqual(75, recorder.Log.result.firstDesyncTick);
        }

        [Test]
        public void ToBytes_IsValidBeforeAndAfterFinish()
        {
            MatchRecorder recorder = Create();
            MatchLog before = TestLogs.Read(recorder.ToBytes());
            TestLogs.Equal(recorder.Log.header, before.header);
            TestLogs.Equal(recorder.Log.board, before.board);
            TestLogs.Equal(recorder.Log.loadouts, before.loadouts);
            TestLogs.Equal(recorder.Log.draft, before.draft);
            Assert.IsNull(before.result);
            recorder.RecordHash(50, -123);
            recorder.RecordHash(100, 456);
            recorder.Finish(new MatchResult { reason = MatchEndReason.Win, winner = 1, endTick = 120, finalHash = 789 });
            TestLogs.Equal(recorder.Log, TestLogs.Read(recorder.ToBytes()));
        }
    }
}
