using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// Latency and jitter: the input delay is a fixed 200 ms, so a link slower
    /// than that is waited on every tick. These record how much the board
    /// freezes at each speed, which is the number adaptive delay has to improve;
    /// they assert only that the match stays correct and keeps playing.
    /// </summary>
    public class LatencyTests
    {
        [TestCase(0.025, 0.0)]
        [TestCase(0.065, 0.0)]
        [TestCase(0.100, 0.02)]
        [TestCase(0.200, 0.05)]
        [TestCase(0.250, 0.10)]
        [TestCase(0.300, 0.15)]
        [Category("Characterization")]
        public void OneWayLatency_StaysCorrectAndKeepsPlaying(double oneWay, double jitter)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 60,
                ZeroToOne = new LinkProfile { Delay = oneWay, Jitter = jitter },
                OneToZero = new LinkProfile { Delay = oneWay, Jitter = jitter }
            }.Run();
            s.Report("one-way " + (int)(oneWay * 1000) + " ms, jitter " + (int)(jitter * 1000) + " ms");

            s.AssertMatchesReference(minimum: 3);
            foreach (HarnessPeer p in s.Peers)
                Assert.Greater(p.State.tickCount, 200, s.Describe("P" + p.Player + " barely played"));
        }

        [TestCase(0.065, 0.2)]
        [Category("Characterization")]
        public void AsymmetricLink_StaysCorrect(double fast, double slow)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 40,
                ZeroToOne = new LinkProfile { Delay = fast },
                OneToZero = new LinkProfile { Delay = slow, Jitter = 0.05 }
            }.Run();
            s.Report("asymmetric " + (int)(fast * 1000) + "/" + (int)(slow * 1000) + " ms");

            s.AssertMatchesReference(minimum: 3);
        }
    }
}
