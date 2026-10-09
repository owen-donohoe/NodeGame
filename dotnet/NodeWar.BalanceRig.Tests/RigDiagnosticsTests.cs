using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.BalanceRig
{
    public class RigDiagnosticsTests
    {
        private static SimulationState State()
        {
            var s = RigSmokeTests.Setup();
            MatchFactory.Configure(s.balance, s.board);
            return MatchFactory.Build(s.balance, s.board, new DraftPlacement[0],
                new[] { new PlayerSetup(), new PlayerSetup() });
        }

        [Test]
        public void Trace_BreachingDoesNotIndexPastBuckets()
        {
            var state = State();
            state.villagers[0].state = VillagerState.Breaching;
            int before = SimulationStateHasher.ComputeHash(state);
            var text = new StringWriter();
            Assert.DoesNotThrow(() => MatchRunner.Trace(text, state));
            StringAssert.Contains("breaching=1", text.ToString());
            Assert.AreEqual(before, SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void AttackingStructure_TraceAndFleetCountItAsBusy()
        {
            var state = State();
            state.villagers = new VillagerData[3];
            for (int i = 0; i < 3; i++) state.villagers[i] = new VillagerData {
                villagerID = i, ownerID = 0, currentNodeID = state.players[0].coreNodeID,
                state = VillagerState.AttackingStructure, suit = SuitType.Warrior, hp = 5 };
            int before = SimulationStateHasher.ComputeHash(state);
            var text = new StringWriter(); MatchRunner.Trace(text, state);
            StringAssert.Contains("attackingStructure=3", text.ToString());
            var result = new MatchResult(); MatchRunner.TrackFleet(state, result);
            Assert.AreEqual(3, result.soldiersEnd[0]); Assert.AreEqual(0, result.ticksThreeIdleSoldiers[0]);
            var timeline = new TimelineMetrics(state, 10); state.tickCount = 1;
            timeline.ObserveTick(state, null); Assert.AreEqual(0, timeline.Windows[0].idleVillagerTicks[0]);
            state.tickCount = 0; Assert.AreEqual(before, SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void SwapSeats_MirrorsDraftAndSwapsSetups()
        {
            var setup = RigSmokeTests.Setup();
            setup.mirror = (x, z) => (x, 2 - z);
            setup.playerSetups = new[]
            {
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior }, districts = new[] { (int)DistrictType.Farm } },
                new PlayerSetup { suits = new[] { (int)SuitType.Guardian }, districts = new[] { (int)DistrictType.Mine } }
            };
            var draft = new[] { new DraftPlacement { playerID = 0, gridX = 0, gridZ = 0, districtType = DistrictType.Farm } };
            var first = MatchRunner.Prepare(setup, 31, draft);
            var paired = MatchRunner.SwapSeats(first);
            Assert.AreEqual(first.seed, paired.seed);
            Assert.AreEqual(first.pairID, paired.pairID);
            Assert.AreEqual(0, first.seat); Assert.AreEqual(1, paired.seat);
            Assert.AreEqual((0, 2, 1, DistrictType.Farm), (paired.draft[0].gridX, paired.draft[0].gridZ, paired.draft[0].playerID, paired.draft[0].districtType));
            CollectionAssert.AreEqual(first.players[0].districts, paired.players[1].districts);
            CollectionAssert.AreEqual(first.players[1].suits, paired.players[0].suits);
            Assert.AreEqual((1, 0, 1, -10000), (paired.setup.board.initialPlacements[0].gridX, paired.setup.board.initialPlacements[0].gridZ, paired.setup.board.initialPlacements[0].ownerID, paired.setup.board.initialPlacements[0].claimBar));
            Assert.AreEqual(0, first.draft[0].gridZ, "Original draft remains intact");
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(0, 2)] [TestCase(1, 2)]
        public void Commands_AlwaysApplyP0ThenP1(int tick, int delay)
        {
            var state = State(); state.tickCount = tick;
            var input = new[] { new GameCommand { playerID = 1, value = 11 }, new GameCommand { playerID = 0, value = 1 }, new GameCommand { playerID = 1, value = 12 }, new GameCommand { playerID = 0, value = 2 } };
            var pending = new List<KeyValuePair<int, GameCommand>>();
            var applied = new List<int>();
            MatchRunner.ApplyCommands(state, input, delay, pending, commands => applied.AddRange(commands.Select(c => c.value)));
            if (delay > 0)
            {
                Assert.IsEmpty(applied);
                state.tickCount++;
                MatchRunner.ApplyCommands(state, new GameCommand[0], delay, pending, commands => applied.AddRange(commands.Select(c => c.value)));
                Assert.IsEmpty(applied);
                state.tickCount++;
                MatchRunner.ApplyCommands(state, new GameCommand[0], delay, pending, commands => applied.AddRange(commands.Select(c => c.value)));
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 11, 12 }, applied);
            Assert.IsEmpty(pending);
        }

        [Test]
        public void Overlay_PreservesSourceAndHashesEffectiveBalance()
        {
            string dir = Path.Combine(Path.GetTempPath(), "rig-overlay-" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            try
            {
                var json = JObject.FromObject(GameBalanceData.Default());
                foreach (string name in new[] { "tempoStageTicks", "tempoClaimPercent", "tempoRespawnPercent", "tempoProductionPercent", "suddenDeathTicks", "suddenDeathThresholds", "breachBarMax", "breachSwarmRate", "breachBarDecayPerTick" }) json.Remove(name);
                int source = BalanceHasher.Hash(json.ToObject<GameBalanceData>());
                string path = Path.Combine(dir, source + ".json");
                File.WriteAllText(path, json.ToString(Formatting.None));
                var setup = RigSetupLoader.Load(path, null, "none", true);
                Assert.AreEqual(source, setup.sourceBalanceHash);
                Assert.AreEqual(BalanceHasher.Hash(setup.balance), setup.balanceHash);
                Assert.AreNotEqual(source, setup.balanceHash);
                string bad = Path.Combine(dir, (source == 1 ? 2 : 1) + ".json"); File.Copy(path, bad);
                Assert.Throws<FormatException>(() => RigSetupLoader.Load(bad, null, "none", true));
            }
            finally { Directory.Delete(dir, true); }
        }

        [TestCase(false, 10, 0)] [TestCase(true, 20, 1)]
        public void FinalTickBreaches_DoesNotCountEarlierBreachOnCappedMatch(bool win, int breachTick, int expected)
        {
            var state = State(); state.tickCount = 20; state.gameOver = win; state.winnerID = win ? 0 : -1;
            var result = new MatchResult(); result.breachTicks[1].Add(breachTick);
            MatchRunner.Finish(state, result, GameBalanceData.Default());
            Assert.AreEqual(expected, result.finalTickBreaches);
        }

        [Test]
        public void StallCounters_CountTicksNotUnitMultiplicity()
        {
            var state = State(); state.nodes[0].districtType = DistrictType.Barracks; state.nodes[0].ownerID = 0;
            state.villagers = new VillagerData[6];
            for (int i = 0; i < 6; i++) state.villagers[i] = new VillagerData { villagerID = i, ownerID = 0, currentNodeID = 0, state = VillagerState.Idle, suit = i < 3 ? SuitType.Warrior : SuitType.None };
            var result = new MatchResult(); MatchRunner.TrackFleet(state, result);
            state.villagers[3].suit = SuitType.Warrior; MatchRunner.TrackFleet(state, result);
            Assert.AreEqual(2, result.ticksThreeIdleSoldiers[0]); Assert.AreEqual(2, result.ticksIdleOnBarracks[0]); Assert.AreEqual(4, result.peakSoldiers[0]);
        }

        [Test]
        public void SwapCli_NSeedsProducesTwoNRows()
        {
            var setup = RigSmokeTests.Setup(); setup.mirror = (x, z) => (x, 2 - z);
            var options = RigOptions.Parse(new[] { "--matches", "2", "--seed", "40", "--cap", "20", "--swap-seats", "on" });
            var results = Program.RunMatches(setup, options);
            CollectionAssert.AreEqual(new[] { 40, 40, 41, 41 }, results.Select(r => r.seed));
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, results.Select(r => r.seat));
            CollectionAssert.AreEqual(new[] { 40, 40, 41, 41 }, results.Select(r => r.pairID));
            Assert.AreEqual(5, (Report.Header + "\n" + string.Join("\n", results.Select(Report.Row))).Split('\n').Length);
            options.swapSeats = false;
            Assert.AreEqual(2, Program.RunMatches(setup, options).Count);
            Assert.Throws<ArgumentException>(() => RigOptions.Parse(new[] { "--swap-seats", "invalid" }));
        }
    }
}
