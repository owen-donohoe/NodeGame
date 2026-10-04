using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// The match start is not synchronised: each side unpauses when its own
    /// transition finishes, so one can begin 0.5-1 s before the other. The side
    /// that starts first has nobody to play against until its peer arrives.
    /// These record what that costs and hold the result to the reference.
    /// </summary>
    public class StartSkewTests
    {
        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(2.5)]
        [TestCase(6.0)]
        [Category("Characterization")]
        public void LateStart_StaysCorrectAndSettles(double skew)
        {
            LockstepScenario s = new LockstepScenario { Seconds = 40, StartAt = new[] { 0.0, skew } }.Run();
            s.Report("start skew " + skew + " s");

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.Greater(p.State.tickCount, (40 - skew - 4) * 10, s.Describe("P" + p.Player + " did not settle after the late start"));
        }
    }
}
