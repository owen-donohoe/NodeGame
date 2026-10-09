using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.Tests;

namespace NodeWar.BalanceRig
{
    public class TimelineMetricsTests
    {
        // RigSmokeTests.Setup(): 3x3, P0's core is node 7 (high Z) and P1's node 1.
        private const int CoreP0 = 1, CoreP1 = 7;

        private static SimulationState State(RigSetup setup = null)
        {
            var s = setup ?? RigSmokeTests.Setup();
            MatchFactory.Configure(s.balance, s.board);
            return MatchFactory.Build(s.balance, s.board, new DraftPlacement[0],
                new[] { new PlayerSetup(), new PlayerSetup() });
        }

        private static GameCommand Cmd(int player, int issuedOnTick = 0) =>
            new GameCommand { type = CommandType.Move, playerID = player, issuedOnTick = issuedOnTick };

        private static void Tick(TimelineMetrics m, SimulationState s, int postTick, TickEventLog log = null)
        {
            s.tickCount = postTick;
            m.ObserveTick(s, log);
        }

        [TestCase(10, 100)]
        [TestCase(20, 200)]
        public void Windows_UseAppliedTickAndIncludePartialTail(int ticksPerSecond, int split)
        {
            var s = State();
            var m = new TimelineMetrics(s, ticksPerSecond);
            Assert.AreEqual(split, m.WindowTicks);

            for (int t = 0; t < split + 1; t++)
            {
                // The command applied on the last tick of window 0 and the one
                // applied on the first tick of window 1. The second was issued
                // on tick 0 and held back by an input delay.
                if (t == split - 1) m.ObserveCommands(t, new[] { Cmd(0, t) });
                if (t == split) m.ObserveCommands(t, new[] { Cmd(0, 0) });
                Tick(m, s, t + 1);
            }

            Assert.AreEqual(2, m.Windows.Count);
            Assert.AreEqual((0, split, 1), (m.Windows[0].startTick, m.WindowEnd(0), m.Windows[0].commands[0]));
            Assert.AreEqual((split, split + 1, 1), (m.Windows[1].startTick, m.WindowEnd(1), m.Windows[1].commands[0]));
        }

        [Test]
        public void IdleVillagerTicks_ExcludeDeadConsumedAndOtherStates()
        {
            var s = State();
            s.villagers = new VillagerData[6];
            for (int i = 0; i < 6; i++)
                s.villagers[i] = new VillagerData { villagerID = i, ownerID = 0, currentNodeID = CoreP0, state = VillagerState.Idle };
            s.villagers[4].state = VillagerState.Dead;
            s.villagers[5].isConsumed = true; // Idle-marked, but spent
            var m = new TimelineMetrics(s, 10);

            s.villagers[2].state = VillagerState.Moving;
            s.villagers[3].state = VillagerState.Working;
            Tick(m, s, 1); // villagers 0 and 1 idle: 2
            s.villagers[1].state = VillagerState.Claiming;
            Tick(m, s, 2); // 1
            s.villagers[0].state = VillagerState.Fighting;
            Tick(m, s, 3); // 0

            Assert.AreEqual(1, m.Windows.Count);
            Assert.AreEqual(3, m.Windows[0].idleSamples);
            Assert.AreEqual(3, m.Windows[0].idleVillagerTicks[0]);
            Assert.AreEqual(2, m.Windows[0].idlePeak[0]);
            Assert.AreEqual(0, m.Windows[0].idleVillagerTicks[1]);
        }

        [Test]
        public void OwnershipTimeline_DistinguishesClaimNeutralisationAndRecapture()
        {
            var s = State();
            var m = new TimelineMetrics(s, 10);
            int[] owners = { 0, -1, 1, -1, 1 };
            for (int i = 0; i < owners.Length; i++)
            {
                s.nodes[3].ownerID = owners[i];
                Tick(m, s, i + 1);
            }

            Assert.AreEqual(5, m.Transitions.Count);
            Assert.AreEqual(3, m.ClaimCount);
            Assert.AreEqual(2, m.NeutralisationCount);
            Assert.AreEqual(1, m.RecaptureCount);
            CollectionAssert.AreEqual(new[] { false, false, true, false, false }, m.Transitions.Select(t => t.recapture));
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3, 3 }, m.Transitions.Select(t => t.nodeID));
            Assert.AreEqual((-1, 0), (m.Transitions[0].fromOwner, m.Transitions[0].toOwner));
        }

        [Test]
        public void BanksDoNotChangeMetricMeaning()
        {
            var s = State();
            s.nodes[3].districtType = DistrictType.Pier; s.nodes[3].ownerID = 1;
            s.villagers = new VillagerData[3];
            for (int i = 0; i < 3; i++)
                s.villagers[i] = new VillagerData { villagerID = i, ownerID = 0, currentNodeID = 3, hp = 1, state = VillagerState.Idle };
            var m = new TimelineMetrics(s, 10);

            // An ordinary claim and a gated claim on an enemy Pier are busy, not idle.
            s.villagers[0].state = VillagerState.Claiming;
            s.villagers[1].state = VillagerState.Claiming; s.villagers[1].targetNodeID = 4;
            Tick(m, s, 1);
            Assert.AreEqual(1, m.Windows[0].idleVillagerTicks[0], "only the third villager is idle");
            Assert.AreEqual(1, m.Windows[0].idlePeak[0]);
            int before = m.Windows[0].idleVillagerTicks[0];

            // A population append (a recruit lands at the end of the array) never drops what was counted.
            var grown = new VillagerData[4];
            System.Array.Copy(s.villagers, grown, 3);
            grown[3] = new VillagerData { villagerID = 3, ownerID = 0, currentNodeID = CoreP0, hp = 1, state = VillagerState.Idle };
            s.villagers = grown;
            Tick(m, s, 2);
            Assert.GreaterOrEqual(m.Windows[0].idleVillagerTicks[0], before);
            Assert.AreEqual(1 + 2, m.Windows[0].idleVillagerTicks[0]);
            Assert.AreEqual(2, m.Windows[0].idlePeak[0]);

            // The Pier is neutralised by the gated claimers: one Neutralisation, no Claim, not a Core.
            s.nodes[3].ownerID = -1;
            Tick(m, s, 3);
            Assert.AreEqual(1, m.NeutralisationCount);
            Assert.AreEqual(0, m.ClaimCount);
            Assert.AreEqual((OwnerTransitionKind.Neutralisation, 1, -1, false),
                (m.Transitions[0].kind, m.Transitions[0].fromOwner, m.Transitions[0].toOwner, m.Transitions[0].isCore));
            Assert.AreEqual(1, m.NeutralisedFrom[1]);
        }

        [Test]
        public void OpeningContest_OnlyNonCoreThroughTick600()
        {
            // Core combat at 10 and non-core combat at 601 are not an opening contest.
            var s = State();
            var m = new TimelineMetrics(s, 10);
            Tick(m, s, 10, Combat(CoreP0));
            Tick(m, s, 601, Combat(3));
            Assert.IsFalse(m.OpeningContested);

            // Non-core combat on tick 600 itself is.
            s = State();
            m = new TimelineMetrics(s, 10);
            Tick(m, s, 600, Combat(3));
            Assert.IsTrue(m.OpeningContested);
            Assert.AreEqual((600, 3), (m.FirstContestTick, m.FirstContestNode));

            // Later events do not replace the first.
            s = State();
            m = new TimelineMetrics(s, 10);
            Tick(m, s, 400, Combat(4));
            Tick(m, s, 500, Combat(3));
            Assert.AreEqual((400, 4), (m.FirstContestTick, m.FirstContestNode));
        }

        private static TickEventLog Combat(int nodeID)
        {
            var log = new TickEventLog();
            log.Add(TickEventType.CombatStarted, nodeID, -1, -1, 0);
            return log;
        }

        [Test]
        public void TwoMinuteLead_ExcludesCoresAndUsesPostTick1200()
        {
            var s = State();
            s.nodes[CoreP0].ownerID = 0; s.nodes[CoreP1].ownerID = 1;
            var m = new TimelineMetrics(s, 10);

            foreach (int n in new[] { 0, 2 }) s.nodes[n].ownerID = 0;
            foreach (int n in new[] { 3, 5 }) s.nodes[n].ownerID = 1;
            Tick(m, s, 1199);
            Assert.IsFalse(m.HasLead, "2:2 at 1199 is before the snapshot tick.");

            Assert.IsFalse(m.HasLead, "1199 is not the snapshot tick.");

            s.nodes[4].ownerID = 0;
            s.nodes[5].ownerID = -1; // P0 owns 0,2,4; P1 owns 3
            Tick(m, s, 1200);
            Assert.IsTrue(m.HasLead);
            Assert.AreEqual((3, 1, 2), (m.NonCoreOwned[0], m.NonCoreOwned[1], m.LeadNonCore));

            s.nodes[0].ownerID = 1; s.nodes[2].ownerID = 1;
            Tick(m, s, 1201);
            Assert.AreEqual(2, m.LeadNonCore, "Later flips do not change the snapshot.");
        }

        [Test]
        public void Snowball_DenominatorIsExplicit()
        {
            MatchResult R(int winner, int ticks, bool capped, bool hasLead, int lead) =>
                new MatchResult { winner = winner, ticks = ticks, capped = capped, hasLead = hasLead, twoMinuteLead = lead };

            var results = new List<MatchResult>
            {
                R(0, 1500, false, true, 2),  // leader won
                R(0, 1500, false, true, 0),  // tie at 1200
                R(0, 1199, false, false, 0), // ended before 1200
                R(-1, 6000, true, true, 2)   // capped
            };
            SnowballStats stats = Snowball.Evaluate(results);
            Assert.AreEqual((1, 1, 1, 1, 1), (stats.eligible, stats.correct, stats.ties, stats.endedBefore, stats.capped));

            results.Add(R(1, 1600, false, true, 2)); // leader lost
            stats = Snowball.Evaluate(results);
            Assert.AreEqual((2, 1), (stats.eligible, stats.correct));

            SnowballStats none = Snowball.Evaluate(new List<MatchResult> { R(0, 1500, false, true, 0) });
            Assert.AreEqual(0, none.eligible);
            string text = Snowball.Format(none);
            StringAssert.Contains("n/a", text);
            StringAssert.DoesNotContain("100", text);
            StringAssert.DoesNotContain("0.0%", text);
        }

        [Test]
        public void Observer_DoesNotChangeSimulation()
        {
            RigSetup setup = RigSmokeTests.Setup();
            var withHashes = new List<int>(); var withCommands = new List<string>();
            var withoutHashes = new List<int>(); var withoutCommands = new List<string>();

            RunHooks Hooks(bool timeline, List<int> hashes, List<string> commands) => new RunHooks
            {
                timeline = timeline,
                afterTick = st => hashes.Add(SimulationStateHasher.ComputeHash(st)),
                onCommands = c => commands.AddRange(c.Select(x => x.playerID + ":" + x.type + ":" + x.villagerID + ":" + x.targetNodeID + ":" + x.value))
            };

            MatchResult on = MatchRunner.Run(MatchRunner.Prepare(setup, 77), 600, 2, null, Hooks(true, withHashes, withCommands));
            MatchResult off = MatchRunner.Run(MatchRunner.Prepare(setup, 77), 600, 2, null, Hooks(false, withoutHashes, withoutCommands));

            Assert.IsNotNull(on.timeline);
            Assert.IsNull(off.timeline);
            Assert.That(withHashes, Is.Not.Empty);
            CollectionAssert.AreEqual(withoutHashes, withHashes);
            CollectionAssert.AreEqual(withoutCommands, withCommands);

            MatchResult again = MatchRunner.Run(MatchRunner.Prepare(setup, 77), 600, 2, null, Hooks(true, new List<int>(), new List<string>()));
            Assert.AreEqual(TimelineReport.FlipsCsv(new[] { on }), TimelineReport.FlipsCsv(new[] { again }));
            Assert.AreEqual(TimelineReport.WindowsCsv(new[] { on }), TimelineReport.WindowsCsv(new[] { again }));
            Assert.AreEqual(Report.Row(on), Report.Row(again));
        }

        private static RigSetup Line3()
        {
            RigSetup s = RigSmokeTests.Setup();
            // Three nodes in a column: P1's core at z=0, one connector, P0's core at z=2.
            s.board = BoardFixtures.LandGrid(1, 3);
            s.board.startingVillagersPerPlayer = 1;
            s.mirror = (x, z) => (x, 2 - z);
            return s;
        }

        [TestCase(3, "1x3")]
        [TestCase(9, "3x3")]
        public void Reports_HaveStableSchemaAndNoDefaultNodeAssumption(int nodeCount, string grid)
        {
            RigSetup setup = nodeCount == 3 ? Line3() : RigSmokeTests.Setup();
            var results = new List<MatchResult>();
            for (int seed = 1; seed <= 3; seed++)
                results.Add(MatchRunner.Run(MatchRunner.Prepare(setup, seed), 800, 0, null, new RunHooks { timeline = true }));

            // The bots may not flip anything on a map this small in so few ticks,
            // so one scripted match flips the highest-numbered non-core node (so IDs up to the map's size are exercised).
            SimulationState scripted = State(setup);
            var scriptedTimeline = new TimelineMetrics(scripted, 10);
            int last = nodeCount == 3 ? 1 : nodeCount - 1; // the connector between the two cores
            Assert.AreNotEqual(DistrictType.Core, scripted.nodes[last].districtType);
            foreach (int owner in new[] { 0, -1, 1 })
            {
                scripted.nodes[last].ownerID = owner;
                scripted.tickCount++;
                scriptedTimeline.ObserveTick(scripted, null);
            }
            var scriptedResult = new MatchResult { seed = 99, pairID = 99 };
            scriptedTimeline.Complete(scriptedResult);
            results.Add(scriptedResult);
            Assert.AreEqual(last, scriptedResult.timeline.Transitions[0].nodeID);

            foreach (MatchResult r in results)
                foreach (OwnerTransition t in r.timeline.Transitions)
                    Assert.That(t.nodeID, Is.InRange(0, nodeCount - 1));

            string windows = TimelineReport.WindowsCsv(results);
            string flips = TimelineReport.FlipsCsv(results);
            string summary = Report.Header + "\n" + string.Join("\n", results.Select(Report.Row));
            foreach (string csv in new[] { windows, flips, summary })
            {
                string[] lines = csv.Split('\n').Where(l => l.Length > 0).Select(l => l.TrimEnd('\r')).ToArray();
                int columns = lines[0].Split(',').Length;
                Assert.That(lines.Length, Is.GreaterThan(1));
                foreach (string line in lines) Assert.AreEqual(columns, line.Split(',').Length, line);
                StringAssert.Contains("schema", lines[0]);
                StringAssert.Contains("map", lines[0]);
            }

            string[] header = windows.Split('\n')[0].TrimEnd('\r').Split(',');
            string[] firstRow = windows.Split('\n')[1].TrimEnd('\r').Split(',');
            Assert.AreEqual(TimelineMetrics.SchemaVersion.ToString(), firstRow[System.Array.IndexOf(header, "schema")]);
            StringAssert.StartsWith(grid + "-n" + nodeCount + "-", firstRow[System.Array.IndexOf(header, "map")]);
        }
    }
}
