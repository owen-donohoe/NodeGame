using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace NodeWar.BalanceRig
{
    public class RigSmokeTests
    {
        // Short cap: these prove the plumbing, not the balance.
        private const int Cap = 1500;

        private static RigSetup Setup() => RigSetupLoader.Load(null, null, "Barracks");

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
            RigSetup setup = Setup();
            Assert.AreEqual(4, setup.board.gridCols);
            Assert.AreEqual(7, setup.board.gridRows);
            Assert.AreEqual(2, setup.board.initialPlacements.Length);
            Assert.AreEqual(3, setup.baseDraft[0].Length);
            Assert.AreEqual(463780689, setup.balanceHash);
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
