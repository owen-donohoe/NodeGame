using System;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.Tests;

namespace NodeWar.MatchLog
{
    /// <summary>
    /// BOARD_V2 (tag 10) carries a terrain board through a match log; the old
    /// BOARD chunk (tag 2) stays readable as history only.
    /// </summary>
    public class TerrainLogTests
    {
        private static readonly GameBalanceData Balance = GameBalanceData.Default();

        private static PlayerSetup[] Setups(PlayerLoadout[] loadouts)
        {
            return new[]
            {
                new PlayerSetup { suits = loadouts[0].suits, districts = loadouts[0].districts },
                new PlayerSetup { suits = loadouts[1].suits, districts = loadouts[1].districts }
            };
        }

        [Test]
        public void TerrainBoard_RoundTripsBeforeReplay()
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 3 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 4, gridZ = 2 }
            };
            PlayerLoadout[] loadouts =
            {
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] }
            };
            var header = new MatchLogHeader
            {
                sim = (ushort)SimulationVersion.Current, matchId = "terrain", playerIds = new[] { "", "" }, kind = MatchKind.Bot
            };

            var recorder = new MatchRecorder(header, MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, header.content), board, loadouts, draft);
            MatchFactory.Configure(Balance, board);
            SimulationState original = MatchFactory.Build(Balance, board, draft, Setups(loadouts));
            int startHash = SimulationStateHasher.ComputeHash(original);
            SimulationState state = MatchFactory.Build(Balance, board, draft, Setups(loadouts));
            while (state.tickCount < 100)
            {
                GameSimulation.SimulateTick(state);
                if (state.tickCount % 50 == 0)
                    recorder.RecordHash(state.tickCount, SimulationStateHasher.ComputeHash(state));
            }
            recorder.Finish(new MatchResult
            {
                reason = MatchEndReason.Abandoned, winner = -1, endTick = state.tickCount,
                finalHash = SimulationStateHasher.ComputeHash(state), firstDesyncTick = -1
            });

            byte[] bytes = recorder.ToBytes();
            Assert.GreaterOrEqual(TestLogs.Find(bytes, 10), 6, "BOARD_V2 is written for a version 3 log.");
            Assert.Throws<InvalidOperationException>(() => TestLogs.Find(bytes, 2), "The old BOARD chunk is not.");
            Assert.GreaterOrEqual(TestLogs.Find(bytes, 11), 6, "SETUP rides with it.");

            MatchLog read = TestLogs.Read(bytes);
            TestLogs.Equal(board, read.board, "board");
            Assert.AreEqual(49, read.board.terrain.Length);
            Assert.AreEqual(49, read.board.districtSlots.Length);
            CollectionAssert.AreEqual(board.baseDraftDistrictsP0, read.board.baseDraftDistrictsP0);
            CollectionAssert.AreEqual(board.baseDraftDistrictsP1, read.board.baseDraftDistrictsP1);
            Assert.AreEqual(BoardHasher.Hash(board), BoardHasher.Hash(read.board));

            MatchFactory.Configure(Balance, read.board);
            SimulationState rebuilt = MatchFactory.Build(Balance, read.board, read.draft, Setups(read.loadouts));
            Assert.AreEqual(19, rebuilt.nodes.Length);
            Assert.AreEqual(startHash, SimulationStateHasher.ComputeHash(rebuilt));

            ReplayOutcome replay = MatchReplay.Run(read, Balance);
            Assert.IsTrue(replay.ok, replay.error);
        }

        [Test]
        public void BoardV2AndSetup_RoundTrip()
        {
            MatchLog expected = TestLogs.Full();
            byte[] bytes = MatchLogFormat.Write(expected);

            Assert.GreaterOrEqual(TestLogs.Find(bytes, 10), 6);
            Assert.GreaterOrEqual(TestLogs.Find(bytes, 11), 6);
            Assert.Throws<InvalidOperationException>(() => TestLogs.Find(bytes, 2), "A current log has no tag 2.");

            MatchLog read = TestLogs.Read(bytes);
            TestLogs.Equal(expected, read);
            Assert.IsTrue(expected.setup.Equals(read.setup));
            CollectionAssert.AreEqual(expected.board.terrain, read.board.terrain);
            CollectionAssert.AreEqual(expected.board.districtSlots, read.board.districtSlots);

            // A log from before terrain still parses, with no setup and no terrain.
            byte[] oldBytes = MatchLogFormat.Write(TestLogs.FullV2History());
            MatchLog history = TestLogs.Read(oldBytes);
            Assert.IsNull(history.setup);
            Assert.IsNull(history.board.terrain);
            Assert.GreaterOrEqual(TestLogs.Find(oldBytes, 2), 6);

            // Missing, duplicate and conflicting chunks are refused.
            TestLogs.Refused(TestLogs.Remove(bytes, 11), "BOARD_V2 without SETUP");
            int setupAt = TestLogs.Find(bytes, 11);
            byte[] setupChunk = TestLogs.Segment(bytes, setupAt, 6 + TestLogs.IntAt(bytes, setupAt + 2));
            TestLogs.Refused(TestLogs.Insert(bytes, bytes.Length, setupChunk), "duplicate SETUP");
            TestLogs.Refused(TestLogs.Insert(oldBytes, oldBytes.Length, setupChunk), "SETUP beside an old BOARD");
        }

        [Test]
        public void OldSimulationLog_IsReadableButReplayRefused()
        {
            byte[] bytes = new byte[MatchLogFormatTests.MinimalV2.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(MatchLogFormatTests.MinimalV2.Substring(i * 2, 2), 16);

            MatchLog log = TestLogs.Read(bytes);
            Assert.AreEqual(2, log.header.sim, "The header of an old log is still readable.");
            Assert.AreEqual("match-42", log.header.matchId);
            Assert.IsNull(log.setup);

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);
            Assert.IsFalse(outcome.ok);
            StringAssert.Contains("simulation version", outcome.error);
            Assert.AreEqual(0, outcome.endTick, "Refused before the factory built or ran anything.");
        }

        [Test]
        public void ASetupThatDoesNotDescribeItsBoardOrRules_IsNotReplayed()
        {
            BoardConfigData board = BoardFixtures.LandGrid3x3();
            MatchLog Build()
            {
                MatchLog log = TestLogs.Full();
                log.header.sim = (ushort)SimulationVersion.Current;
                log.header.content = 0;
                log.board = board;
                log.draft = Array.Empty<DraftPlacement>();
                log.setup = BoardFixtures.SetupFor(board, 0);
                log.ticks.Clear();
                log.hashes.Clear();
                log.result = new MatchResult { reason = MatchEndReason.Abandoned, endTick = 0, finalHash = 0 };
                return log;
            }

            MatchLog missing = Build();
            missing.setup = null;
            StringAssert.Contains("setup", MatchReplay.Run(missing, Balance).error);

            MatchLog wrongBoard = Build();
            wrongBoard.setup = new MatchSetup(wrongBoard.setup.MapId, wrongBoard.setup.BoardHash + 1,
                wrongBoard.setup.SimulationVersion, wrongBoard.setup.BalanceHash);
            StringAssert.Contains("setup", MatchReplay.Run(wrongBoard, Balance).error);

            MatchLog wrongRules = Build();
            wrongRules.setup = new MatchSetup(wrongRules.setup.MapId, wrongRules.setup.BoardHash,
                wrongRules.setup.SimulationVersion, 99);
            StringAssert.Contains("setup", MatchReplay.Run(wrongRules, Balance).error);
        }

        [Test]
        public void V2Golden_KeepsTag2AndItsBytes_ButIsNotReplayable()
        {
            byte[] bytes = new byte[MatchLogFormatTests.MinimalV2.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(MatchLogFormatTests.MinimalV2.Substring(i * 2, 2), 16);

            Assert.GreaterOrEqual(TestLogs.Find(bytes, 2), 6);
            MatchLog log = TestLogs.Read(bytes);
            Assert.AreEqual(2, log.header.sim);
            Assert.IsNull(log.board.terrain, "A tag 2 board is history, not a map.");
            CollectionAssert.AreEqual(bytes, MatchLogFormat.Write(log));

            ReplayOutcome replay = MatchReplay.Run(log, Balance);
            Assert.IsFalse(replay.ok);
            StringAssert.Contains("simulation version", replay.error);
        }

        [Test]
        public void VersionTwoHistoryFixture_WritesTag2_AndCurrentWritesTag10()
        {
            byte[] old = MatchLogFormat.Write(TestLogs.FullV2History());
            Assert.GreaterOrEqual(TestLogs.Find(old, 2), 6);
            Assert.Throws<InvalidOperationException>(() => TestLogs.Find(old, 10));
            TestLogs.Equal(TestLogs.FullV2History(), TestLogs.Read(old));

            TestLogs.Equal(TestLogs.Full(), TestLogs.Read(MatchLogFormat.Write(TestLogs.Full())));
        }

        [Test]
        public void CurrentLogWithoutTerrain_IsNotWritten_AndATag2BoardIsNotReplayed()
        {
            MatchLog log = TestLogs.Full();
            log.board.terrain = null;
            Assert.Throws<ArgumentException>(() => MatchLogFormat.Write(log));

            MatchLog tag2 = TestLogs.Read(MatchLogFormat.Write(TestLogs.FullV2History()));
            tag2.header.sim = (ushort)SimulationVersion.Current;
            ReplayOutcome replay = MatchReplay.Run(tag2, Balance);
            Assert.IsFalse(replay.ok);
            StringAssert.Contains("board", replay.error);
        }

        [Test]
        public void MalformedBoardV2_IsRefused()
        {
            byte[] good = MatchLogFormat.Write(TestLogs.Full());
            int terrainCount = TestLogs.Find(good, 10) + 6 + 48 + 4 + 2 * 20;
            Assert.AreEqual(45, TestLogs.IntAt(good, terrainCount));

            byte[] badTerrain = (byte[])good.Clone();
            badTerrain[terrainCount + 4] = 3;
            TestLogs.Refused(badTerrain, "unknown terrain");

            byte[] badSlot = (byte[])good.Clone();
            badSlot[terrainCount + 4 + 45 + 4] = 2;
            TestLogs.Refused(badSlot, "slot flag");

            byte[] shortTerrain = (byte[])good.Clone();
            TestLogs.PutInt(shortTerrain, terrainCount, 44);
            TestLogs.Refused(shortTerrain, "terrain does not cover the grid");

            foreach (int count in new[] { -1, int.MaxValue })
            {
                byte[] huge = (byte[])good.Clone();
                TestLogs.PutInt(huge, terrainCount, count);
                TestLogs.Refused(huge, "count " + count);
            }

            byte[] both = TestLogs.Insert(good, good.Length, TestLogs.Segment(
                MatchLogFormat.Write(TestLogs.FullV2History()),
                TestLogs.Find(MatchLogFormat.Write(TestLogs.FullV2History()), 2),
                6 + TestLogs.IntAt(MatchLogFormat.Write(TestLogs.FullV2History()),
                    TestLogs.Find(MatchLogFormat.Write(TestLogs.FullV2History()), 2) + 2)));
            TestLogs.Refused(both, "tags 2 and 10 together");

            TestLogs.Refused(TestLogs.Remove(good, 10), "no board at all");
        }

        [Test]
        public void EveryStrictPrefixOfABoardV2Chunk_IsRefused()
        {
            byte[] bytes = MatchLogFormat.Write(TestLogs.Full());
            int start = TestLogs.Find(bytes, 10);
            int end = start + 6 + TestLogs.IntAt(bytes, start + 2);
            for (int length = start; length < end; length++)
                TestLogs.Refused(TestLogs.Segment(bytes, 0, length), "prefix " + length);
        }
    }
}
