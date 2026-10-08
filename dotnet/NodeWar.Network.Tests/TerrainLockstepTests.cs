using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// The lossy-link lockstep harness on the shipped hourglass, with and without a
    /// drafted Pier: a sparse board, links that skip water, a Lake node. Held to the
    /// same standard as every other scenario: the reference is the same match on a
    /// perfect link.
    /// </summary>
    public class TerrainLockstepTests
    {
        private static DraftPlacement[] Draft(bool withPier)
        {
            var draft = new System.Collections.Generic.List<DraftPlacement>
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 4, gridZ = 4 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 4, gridZ = 2 }
            };
            if (withPier)
                draft.Add(new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 3 });
            return draft.ToArray();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hourglass_LossRollbackMatchesReference(bool withPier)
        {
            // 1500 ticks. A 400 ms one-way delay spike exceeds 300 ms but recovers
            // inside the 20-tick speculation window. Loss also forces input recovery.
            var scenario = new LockstepScenario
            {
                Seconds = 150.1, // allow the initial input delay, then simulate exactly 1500 ticks
                Board = PremadeMaps.Hourglass01(),
                Draft = Draft(withPier),
                ZeroToOne = new LinkProfile { DelayAt = t => t >= 60.0 && t < 60.4 ? 0.4 : 0.065 }.Cut(90.0, 90.4),
                OneToZero = new LinkProfile { Loss = 0.02 }
            }.Run();
            scenario.Report("hourglass " + (withPier ? "with" : "without") + " Pier");

            Assert.AreEqual(withPier ? 19 : 18, scenario.Peers[0].State.nodes.Length);
            scenario.AssertMatchesReference(minimum: 29);
            int rollbacks = 0;
            foreach (HarnessPeer p in scenario.Peers)
            {
                Assert.AreEqual(1500, p.State.tickCount, scenario.Describe("P" + p.Player + " must simulate 1500 ticks"));
                rollbacks += p.Rollbacks;
            }
            Assert.Greater(rollbacks, 0, scenario.Describe("the blackout must actually cost a rollback"));
            Assert.AreEqual(1451, scenario.ConfirmedTicks(), "The final checkpoint before tick 1500 was confirmed on both peers.");
        }

        [Test]
        public void Hourglass_LossRollbackMatchesReference_OracleIsNotVacuous()
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 30, Board = PremadeMaps.Hourglass01(), Draft = Draft(true), BlankReference = true
            }.Run();
            int compared = 0, different = 0;
            foreach (var pair in s.Peers[0].Hashes)
            {
                compared++;
                if (s.Reference[pair.Key] != pair.Value) different++;
            }
            Assert.Greater(compared, 0);
            Assert.AreEqual(compared, different, "the scripted commands must move the hourglass state at every checkpoint");
        }
    }
}
