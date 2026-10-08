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
            // 1500 ticks. A 400 ms one-way blackout is longer than the redundant copies of an
            // input can bridge but shorter than the speculation window, so the peer that
            // missed the inputs plays on, rolls back, and must land on the same hashes.
            var scenario = new LockstepScenario
            {
                Seconds = 150,
                Board = PremadeMaps.Hourglass01(),
                Draft = Draft(withPier),
                ZeroToOne = new LinkProfile().Cut(60.0, 60.4),
                OneToZero = new LinkProfile { Loss = 0.02 }
            }.Run();
            scenario.Report("hourglass " + (withPier ? "with" : "without") + " Pier");

            Assert.AreEqual(withPier ? 19 : 18, scenario.Peers[0].State.nodes.Length);
            scenario.AssertMatchesReference(minimum: 20);
            int rollbacks = 0;
            foreach (HarnessPeer p in scenario.Peers)
            {
                Assert.Greater(p.State.tickCount, 1400, scenario.Describe("P" + p.Player + " did not play on"));
                rollbacks += p.Rollbacks;
            }
            Assert.Greater(rollbacks, 0, scenario.Describe("the blackout must actually cost a rollback"));
            Assert.GreaterOrEqual(scenario.ConfirmedTicks(), 1400);
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
