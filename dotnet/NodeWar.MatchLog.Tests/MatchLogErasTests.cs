using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    /// <summary>
    /// The ERAS (8) and SKINS (9) chunks: optional, order-independent, and
    /// what a replay needs to play a non-zero era the way the match did.
    /// </summary>
    public class MatchLogErasTests
    {
        private static MatchLog WithEras()
        {
            MatchLog log = TestLogs.Full();
            log.loadouts[0].suitEras = new[] { 0, 0, 0, 2 };
            log.loadouts[0].districtEras = new[] { 0, 1 };
            log.loadouts[1].suitEras = new int[0];
            log.loadouts[1].districtEras = new[] { 0, 0, 5 };
            log.loadouts[0].skins = new[] { "skin.suit.warrior.default", "skin.district.farm.gilded" };
            log.loadouts[1].skins = new string[0];
            return log;
        }

        [Test]
        public void ErasAndSkins_RoundTrip()
        {
            MatchLog log = WithEras();
            MatchLog read = TestLogs.Read(MatchLogFormat.Write(log));

            TestLogs.Equal(log, read);
        }

        [Test]
        public void ALogWithoutThem_ReadsAsNull()
        {
            MatchLog read = TestLogs.Read(MatchLogFormat.Write(TestLogs.Full()));

            Assert.IsNull(read.loadouts[0].suitEras);
            Assert.IsNull(read.loadouts[1].districtEras);
            Assert.IsNull(read.loadouts[0].skins);
            Assert.Throws<InvalidOperationException>(() => TestLogs.Find(MatchLogFormat.Write(TestLogs.Full()), 8),
                "no ERAS chunk without eras");
        }

        [Test]
        public void ErasBeforeLoadouts_StillFillTheSameLoadouts()
        {
            MatchLog log = WithEras();
            byte[] data = MatchLogFormat.Write(log);
            int eras = TestLogs.Find(data, 8);
            int length = 6 + TestLogs.IntAt(data, eras + 2);
            byte[] chunk = TestLogs.Segment(data, eras, length);
            byte[] moved = TestLogs.Insert(TestLogs.Remove(data, 8), 6, chunk); // right after the file header

            MatchLog read = TestLogs.Read(moved);

            Assert.AreEqual(log.loadouts[0].suitEras, read.loadouts[0].suitEras);
            Assert.AreEqual(log.loadouts[0].suits, read.loadouts[0].suits);
        }

        [Test]
        public void TruncatedSkins_AreRefused()
        {
            byte[] data = MatchLogFormat.Write(WithEras());
            int skins = TestLogs.Find(data, 9);
            TestLogs.Refused(TestLogs.Segment(data, 0, data.Length - 3), "cut inside SKINS");
            Assert.Greater(skins, 0);
        }

        [Test]
        public void Replay_PlaysTheLoggedEras()
        {
            // Farm era 1 produces every 10 ticks instead of 30. P0 drafts a farm
            // at era 1 and works it; only a replay that reads ERAS reproduces
            // the food, and so the hashes.
            GameBalanceData balance = GameBalanceData.Default();
            for (int i = 0; i < balance.districtStats.Length; i++)
                if (balance.districtStats[i].districtType == DistrictType.Farm && balance.districtStats[i].era == 1)
                    balance.districtStats[i].productionTicks = 10;

            var eras = new int[(int)DistrictType.Market + 1];
            eras[(int)DistrictType.Farm] = 1;
            BoardConfigData board = BoardConfigData.Default();
            DraftPlacement[] draft = { new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 5 } };
            PlayerLoadout[] loadouts =
            {
                new PlayerLoadout { suits = new int[0], nodes = new int[0], suitEras = new int[12], districtEras = eras },
                new PlayerLoadout { suits = new int[0], nodes = new int[0] }
            };
            var header = new MatchLogHeader
            {
                sim = (ushort)SimulationVersion.Current, matchId = "eras", playerIds = new[] { "", "" }, kind = MatchKind.Bot
            };

            var recorder = new MatchRecorder(header, board, loadouts, draft);
            MatchFactory.Configure(balance, board);
            SimulationState state = MatchFactory.Build(balance, board, draft, new[]
            {
                new PlayerSetup { suits = loadouts[0].suits, nodes = loadouts[0].nodes, suitEras = loadouts[0].suitEras, districtEras = eras },
                new PlayerSetup { suits = loadouts[1].suits, nodes = loadouts[1].nodes }
            });
            Assert.AreEqual(1, state.nodes[21].districtEra);

            while (state.tickCount < 1500) // one claimer takes ~590 ticks to claim the farm
            {
                GameCommand[] commands = state.tickCount == 0
                    ? new[] { new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 21 } }
                    : null;
                recorder.RecordTick(state.tickCount, commands);
                if (commands != null) CommandProcessor.ProcessCommand(state, commands[0]);
                GameSimulation.SimulateTick(state);
                if (state.tickCount % 50 == 0)
                    recorder.RecordHash(state.tickCount, SimulationStateHasher.ComputeHash(state));
            }
            Assert.Greater(state.players[0].food, 10, "the era-1 farm must actually have been worked");
            recorder.Finish(new MatchResult
            {
                reason = MatchEndReason.Abandoned, winner = -1, endTick = state.tickCount,
                finalHash = SimulationStateHasher.ComputeHash(state), firstDesyncTick = -1
            });

            MatchLog read = TestLogs.Read(recorder.ToBytes());
            Assert.IsTrue(MatchReplay.Run(read, balance).ok, "the replay must reproduce the era-1 match");

            read.loadouts[0].districtEras = null; // what a reader that ignored ERAS would build
            Assert.IsFalse(MatchReplay.Run(read, balance).ok, "without the eras the replay must diverge");
        }
    }
}
