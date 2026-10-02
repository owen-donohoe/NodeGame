using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    public class CleanLinkTests
    {
        [Test]
        public void CleanLink_PlaysSteadyAndMatchesTheReference()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 60 }.Run();
            s.Report("clean");

            s.AssertMatchesReference(minimum: 10);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds, s.Describe("P" + p.Player + " held on a clean link"));
                Assert.AreEqual(0, p.SpeculationStarts, s.Describe("P" + p.Player + " speculated on a clean link"));
                Assert.AreEqual(0, p.Rollbacks, s.Describe("P" + p.Player + " rolled back on a clean link"));
                Assert.Greater(p.State.tickCount, 560, s.Describe("P" + p.Player + " fell behind real time"));
            }
        }

        [Test]
        public void CleanLink_SendsOnlyTheRedundantCopiesItIntends()
        {
            // Every input goes out three times (REDUNDANT_INPUTS = 2) and the
            // link loses none, so each tick arrives once new and twice again.
            // Anything above that is resends on a link that needs none: the
            // "over a thousand duplicates every five seconds" regression.
            LockstepScenario s = new LockstepScenario { Seconds = 60 }.Run();

            foreach (HarnessPeer p in s.Peers)
            {
                int unique = p.InputDeliveries - p.DuplicateDeliveries;
                Assert.Greater(unique, 500, s.Describe("too few inputs arrived"));
                double perInput = (double)p.DuplicateDeliveries / unique;
                Assert.LessOrEqual(perInput, 2.1,
                    s.Describe("P" + p.Player + " received " + perInput.ToString("F2") + " duplicates per input"));
            }
        }

        [Test]
        public void CleanLink_HasNoFrozenTime()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 60 }.Run();
            foreach (HarnessPeer p in s.Peers)
                Assert.Less(p.FrozenSeconds, 1.0, s.Describe("P" + p.Player + " watched a frozen board"));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CleanLink_IsTheSameForAnySeed(int seed)
        {
            LockstepScenario s = new LockstepScenario { Seed = seed, Seconds = 30 }.Run();
            s.AssertMatchesReference(minimum: 5);
        }
    }
}
