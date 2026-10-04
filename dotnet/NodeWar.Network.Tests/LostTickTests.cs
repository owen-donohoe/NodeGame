using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// One tick's input lost in every copy while the peer carries on talking:
    /// the case a burst of consecutive packets produces, and the one the
    /// resend rules have to cover, because the redundant copies of that tick
    /// are all gone and every later packet arrives.
    /// </summary>
    public class LostTickTests
    {
        private const int LostTick = 152;

        /// <summary>
        /// Peer 0's input for LostTick never arrives in its first three sends
        /// (its own flush and the two redundant ones), nor in anything sent for
        /// <paramref name="blackoutSeconds"/> after it was first generated.
        /// Anything later gets through, so only a resend can recover it.
        /// </summary>
        private static LockstepScenario Run(double blackoutSeconds, double seconds = 40)
        {
            // Tick 150 generates the input for tick 152 (INPUT_DELAY 2), at about 15.0 s.
            double firstSent = 15.0;
            var lossy = new LinkProfile
            {
                DropInput = (sendTime, forTick) => forTick == LostTick && sendTime < firstSent + blackoutSeconds
            };
            return new LockstepScenario { Seconds = seconds, ZeroToOne = lossy }.Run();
        }

        [Test]
        public void AllThreeCopiesLost_RecoversWithoutAHold()
        {
            LockstepScenario s = Run(blackoutSeconds: 0.35);
            s.Report("lost tick, 0.35 s of copies gone");

            s.AssertMatchesReference(minimum: 3);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds, s.Describe("P" + p.Player + " held because one input was lost"));
                Assert.Less(p.WorstTickGap, 1.0, s.Describe("P" + p.Player + " froze for over a second on one lost input"));
            }
        }

        /// <summary>
        /// The same loss with enough of a blackout that the receiver starts a
        /// speculation. A speculating sender keeps generating inputs, so its
        /// resend clock must not be reset by them: the lost tick has to be
        /// offered again within a resend interval or two, not after the
        /// window runs out.
        /// </summary>
        [TestCase(0.6)]
        [TestCase(1.0)]
        [TestCase(1.5)]
        public void LostTick_StillRecoversWhenTheSenderSpeculates(double blackoutSeconds)
        {
            LockstepScenario s = Run(blackoutSeconds);
            s.Report("lost tick, " + blackoutSeconds + " s of copies gone");

            s.AssertMatchesReference(minimum: 3);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.AreEqual(0, p.Holds,
                    s.Describe("P" + p.Player + " held, with every packet but one tick's arriving"));
                Assert.Less(p.WorstTickGap, blackoutSeconds + 1.0,
                    s.Describe("P" + p.Player + " stayed frozen long after the loss ended"));
            }
        }
    }
}
