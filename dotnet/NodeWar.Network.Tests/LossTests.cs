using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// Packet loss: isolated, random, and in bursts measured in milliseconds,
    /// because how long a link stays bad is what redundant inputs have to outlast.
    ///
    /// Every test holds the run to the reference. The [Category("Characterization")]
    /// ones also print what the loss cost (frozen time, speculation, holds) and
    /// assert only that the match survived and recovered; the numbers are what
    /// the netcode changes are measured against, and the asserts tighten when a
    /// change makes them true.
    /// </summary>
    public class LossTests
    {
        [Test]
        public void OracleIsNotVacuous_AMatchWithoutTheCommandsHashesDifferently()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 20, BlankReference = true }.Run();

            int compared = 0, different = 0;
            foreach (var pair in s.Peers[0].Hashes)
            {
                compared++;
                if (s.Reference[pair.Key] != pair.Value) different++;
            }
            Assert.Greater(compared, 0);
            Assert.AreEqual(compared, different, "the scripted commands must change the state at every checkpoint");
        }

        [TestCase(0.02)]
        [TestCase(0.05)]
        [TestCase(0.15)]
        [TestCase(0.30)]
        [Category("Characterization")]
        public void RandomLoss_BothWays_StaysInSyncAndKeepsPlaying(double loss)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 60,
                ZeroToOne = new LinkProfile { Loss = loss },
                OneToZero = new LinkProfile { Loss = loss }
            }.Run();
            s.Report("random loss " + loss);

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.Greater(p.State.tickCount, 300, s.Describe("P" + p.Player + " barely played"));
        }

        /// <summary>
        /// A link that goes completely dark for a stretch, in one direction,
        /// in the middle of a match. 300 ms is three ticks: longer than the
        /// two redundant copies of an input can bridge.
        /// </summary>
        [TestCase(100)]
        [TestCase(200)]
        [TestCase(300)]
        [TestCase(500)]
        [TestCase(800)]
        [Category("Characterization")]
        public void OneWayBurst_ThenRecovers(int milliseconds)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 40,
                ZeroToOne = new LinkProfile().Cut(20.0, 20.0 + milliseconds / 1000.0)
            }.Run();
            s.Report("one-way burst " + milliseconds + " ms");

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds, s.Describe("P" + p.Player + " held for a " + milliseconds + " ms burst"));
                Assert.Greater(p.State.tickCount, 380, s.Describe("P" + p.Player + " did not recover"));
            }
        }

        [TestCase(100)]
        [TestCase(300)]
        [TestCase(500)]
        [TestCase(800)]
        [Category("Characterization")]
        public void TwoWayBurst_ThenRecovers(int milliseconds)
        {
            double end = 20.0 + milliseconds / 1000.0;
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 40,
                ZeroToOne = new LinkProfile().Cut(20.0, end),
                OneToZero = new LinkProfile().Cut(20.0, end)
            }.Run();
            s.Report("two-way burst " + milliseconds + " ms");

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.Greater(p.State.tickCount, 380, s.Describe("P" + p.Player + " did not recover"));
        }

        /// <summary>
        /// Regression: a lost tick used to end in a hold. A speculating side's
        /// own input generation reset the resend clock every tick, so it
        /// resent nothing while it speculated, and the peer sat frozen until
        /// the window ran out and a hold started resends. Bursty loss found
        /// it, because a burst can take all three copies of one tick.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void BurstyLoss_NeverHolds_ForAnySeed(int seed)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seed = seed,
                Seconds = 60,
                ZeroToOne = new LinkProfile { BurstEnter = 0.02, BurstExit = 0.1 },
                OneToZero = new LinkProfile { BurstEnter = 0.02, BurstExit = 0.1 }
            }.Run();

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds, s.Describe("P" + p.Player + " held under bursty loss"));
                Assert.Less(p.WorstTickGap, 1.5, s.Describe("P" + p.Player + " froze for over 1.5 s under bursty loss"));
            }
        }

        /// <summary>
        /// Bursty loss as a wireless link does it: mostly fine, then a run of
        /// consecutive packets gone. Gilbert-Elliott, stepped per packet.
        /// </summary>
        [TestCase(0.02, 0.3)]
        [TestCase(0.02, 0.1)]
        [TestCase(0.05, 0.1)]
        [Category("Characterization")]
        public void BurstyLoss_StaysInSyncAndKeepsPlaying(double enter, double exit)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 60,
                ZeroToOne = new LinkProfile { BurstEnter = enter, BurstExit = exit },
                OneToZero = new LinkProfile { BurstEnter = enter, BurstExit = exit }
            }.Run();
            s.Report("bursty loss enter " + enter + " exit " + exit);

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.Greater(p.State.tickCount, 300, s.Describe("P" + p.Player + " barely played"));
        }

        [TestCase(0.1)]
        [TestCase(0.5)]
        public void Duplication_IsHarmless(double duplicate)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 30,
                ZeroToOne = new LinkProfile { Duplicate = duplicate },
                OneToZero = new LinkProfile { Duplicate = duplicate }
            }.Run();

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds);
                Assert.AreEqual(0, p.SpeculationStarts, s.Describe("duplicates started a speculation"));
            }
        }

        [TestCase(0.2)]
        [TestCase(0.6)]
        public void Reordering_IsHarmless(double reorder)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 30,
                ZeroToOne = new LinkProfile { Reorder = reorder, ReorderExtra = 0.15 },
                OneToZero = new LinkProfile { Reorder = reorder, ReorderExtra = 0.15 }
            }.Run();
            s.Report("reorder " + reorder);

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.AreEqual(0, p.Holds, s.Describe("reordering caused a hold"));
        }
    }
}
