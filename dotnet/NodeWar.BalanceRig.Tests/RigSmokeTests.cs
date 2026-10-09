using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.Tests;

namespace NodeWar.BalanceRig
{
    public class RigSmokeTests
    {
        // Short cap: these prove the plumbing, not the balance.
        private const int Cap = 1500;

        internal static RigSetup Setup()
        {
            var board = BoardFixtures.LandGrid3x3();
            board.startingVillagersPerPlayer = 1;
            return new RigSetup
            {
                balance = GameBalanceData.Default(), board = board,
                baseDraft = new[] { new[] { DistrictType.Farm }, new[] { DistrictType.Mine } },
                loadoutNodes = new[] { DistrictType.Barracks },
                mirror = (x, z) => (x, 2 - z)
            };
        }

        private static List<string> Rows(RigSetup setup, int baseSeed, int count, int delay = 0)
        {
            var rows = new List<string>();
            for (int i = 0; i < count; i++)
                rows.Add(Report.Row(MatchRunner.Run(setup, baseSeed + i, Cap, delay)));
            return rows;
        }

        [Test]
        public void ThreeMatchesComplete()
        {
            RigSetup setup = Setup();
            for (int i = 0; i < 3; i++)
            {
                MatchResult r = MatchRunner.Run(setup, 100 + i, Cap, 0);
                Assert.That(r.ticks, Is.GreaterThan(0).And.LessThanOrEqualTo(Cap));
                Assert.That(r.capped || r.winner >= 0, "A match ends in a win or hits the cap.");
                Assert.That(r.capped, Is.EqualTo(r.winner < 0));
            }
        }

        [Test]
        public void SameSeedGivesIdenticalRows()
        {
            RigSetup setup = Setup();
            CollectionAssert.AreEqual(Rows(setup, 7, 3), Rows(setup, 7, 3));
        }

        [Test]
        public void SetupLoadedFromOtherMatchesDoesNotLeakBetweenRuns()
        {
            // Interleave another seed in between: a static left dirty by one
            // match must not change the next.
            RigSetup setup = Setup();
            string first = Report.Row(MatchRunner.Run(setup, 11, Cap, 0));
            MatchRunner.Run(setup, 12, Cap, 0);
            Assert.AreEqual(first, Report.Row(MatchRunner.Run(setup, 11, Cap, 0)));
        }

        [Test]
        public void DifferentSeedsDraftDifferently()
        {
            RigSetup setup = Setup();
            var a = MatchRunner.RandomDraft(setup, new System.Random(1));
            var b = MatchRunner.RandomDraft(setup, new System.Random(2));
            bool differs = a.Length != b.Length;
            for (int i = 0; !differs && i < a.Length; i++)
                differs = a[i].gridX != b[i].gridX || a[i].gridZ != b[i].gridZ || a[i].districtType != b[i].districtType;
            Assert.IsTrue(differs);
        }

        [Test]
        public void DraftPlacesEveryDistrictOnDistinctFreeCells()
        {
            RigSetup setup = Setup();
            var draft = MatchRunner.RandomDraft(setup, new System.Random(3));
            Assert.AreEqual(2 * (setup.baseDraft[0].Length + setup.loadoutNodes.Length), draft.Length);

            var seen = new HashSet<int>();
            foreach (var ip in setup.board.initialPlacements) seen.Add(ip.gridZ * setup.board.gridCols + ip.gridX);
            foreach (var dp in draft) Assert.IsTrue(seen.Add(dp.gridZ * setup.board.gridCols + dp.gridX), "cell reused");
        }

        [Test]
        public void ShippedBoardAndBalanceLoad()
        {
            RigSetup setup = RigSetupLoader.Load(null, null, "Barracks");
            Assert.AreEqual(7, setup.board.gridCols);
            Assert.AreEqual(7, setup.board.gridRows);
            Assert.AreEqual(2, setup.board.initialPlacements.Length);
            Assert.AreEqual(3, setup.baseDraft[0].Length);
            Assert.AreEqual(PremadeMaps.Hourglass01Id, setup.mapId);
            Assert.AreEqual(-893741384, setup.balanceHash);
        }

        [Test]
        public void HourglassPairedRun_UsesLegalDraftAndTimeline()
        {
            // A plumbing smoke on the shipped map: no win-rate claim, a small current-bot sample.
            RigSetup setup = RigSetupLoader.Load(null, null, "Barracks,Forge");
            var rows = new List<string>();
            var results = new List<MatchResult>();
            for (int pass = 0; pass < 2; pass++)
            {
                results.Clear();
                for (int seed = 1; seed <= 3; seed++)
                {
                    PreparedMatch first = MatchRunner.Prepare(setup, seed);
                    foreach (PreparedMatch match in new[] { first, MatchRunner.SwapSeats(first) })
                    {
                        // Every pick is legal, in the order it was drafted, on the very map being played.
                        var occupied = new bool[49];
                        foreach (var ip in match.setup.board.initialPlacements) occupied[ip.gridZ * 7 + ip.gridX] = true;
                        foreach (DraftPlacement dp in match.draft)
                        {
                            Assert.IsTrue(PlacementLegality.CanPlace(match.setup.board, occupied, dp.districtType, dp.gridX, dp.gridZ),
                                "seed " + seed + " seat " + match.seat + ": " + dp.districtType + " at " + dp.gridX + "," + dp.gridZ);
                            occupied[dp.gridZ * 7 + dp.gridX] = true;
                        }

                        MatchFactory.Configure(match.setup.balance, match.setup.board);
                        SimulationState built = MatchFactory.Build(match.setup.balance, match.setup.board, match.draft, match.players);
                        Assert.That(built.nodes.Length, Is.InRange(18, 19), "the hourglass has 18 nodes, 19 with a Pier");

                        MatchResult r = MatchRunner.Run(match, 600, 0, null, new RunHooks { timeline = true });
                        Assert.IsNotNull(r.timeline);
                        foreach (OwnerTransition t in r.timeline.Transitions)
                            Assert.That(t.nodeID, Is.InRange(0, built.nodes.Length - 1), "a timeline ID is a node of the board that was built");
                        results.Add(r);
                    }
                }
                Assert.AreEqual(6, results.Count, "3 seeds x 2 seats");
                if (pass == 0) foreach (MatchResult r in results) rows.Add(Report.Row(r));
                else for (int i = 0; i < results.Count; i++)
                    Assert.AreEqual(rows[i], Report.Row(results[i]), "row " + i + " is a function of the seed and the setup");
            }
        }

        [Test]
        public void CsvRowsMatchTheHeader()
        {
            RigSetup setup = Setup();
            string path = Path.Combine(Path.GetTempPath(), "rig-smoke-" + System.Guid.NewGuid() + ".csv");
            try
            {
                var results = new List<MatchResult> { MatchRunner.Run(setup, 1, Cap, 0), MatchRunner.Run(setup, 2, Cap, 0) };
                Report.WriteCsv(path, results);
                string[] lines = File.ReadAllLines(path);
                Assert.AreEqual(3, lines.Length);
                int columns = lines[0].Split(',').Length;
                Assert.AreEqual(columns, lines[1].Split(',').Length);
                Assert.AreEqual(columns, lines[2].Split(',').Length);
            }
            finally { File.Delete(path); }
        }
    }
}
