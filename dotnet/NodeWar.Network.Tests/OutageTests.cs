using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// A link that stays dark for seconds, as the F8/F9/F10 drop keys simulate:
    /// speculation past a silent peer, the rollback and replay when it returns,
    /// and the hold when it does not return within the window.
    ///
    /// A 20-tick speculation window is two seconds, so 3 s outages exercise the
    /// full speculate-then-hold path and 25 s the long hold. Every run is held
    /// to the reference, which is what makes a bad rollback visible: a replay
    /// that applied a command on the wrong tick changes the state hash.
    /// </summary>
    public class OutageTests
    {
        private const double CutAt = 20.0;

        private static LockstepScenario Run(double length, bool oneWay, bool fromZero, double seconds = 60, int seed = 1)
        {
            var cut = new LinkProfile().Cut(CutAt, CutAt + length);
            return new LockstepScenario
            {
                Seed = seed,
                Seconds = seconds,
                ZeroToOne = oneWay && !fromZero ? new LinkProfile() : cut,
                OneToZero = oneWay && fromZero ? new LinkProfile() : (oneWay ? cut : new LinkProfile().Cut(CutAt, CutAt + length))
            }.Run();
        }

        /// <summary>
        /// The match resumes and keeps playing after the link returns: the
        /// slower peer reaches at least the ticks the run had time for, less
        /// the outage and a few seconds to recover.
        /// </summary>
        private static void AssertRecovered(LockstepScenario s, double outage, double recoverySeconds)
        {
            int expected = (int)((s.Seconds - outage - recoverySeconds) * 10);
            foreach (HarnessPeer p in s.Peers)
                Assert.GreaterOrEqual(p.State.tickCount, expected,
                    s.Describe("P" + p.Player + " did not recover: expected at least tick " + expected));
        }

        [TestCase(1.0)]
        [TestCase(3.0)]
        [TestCase(10.0)]
        [Category("Characterization")]
        public void OneWayDrop_ZeroToOne_RecoversAndMatchesTheReference(double length)
        {
            // Peer 1 stops hearing peer 0 (the F8 case), peer 0 still hears peer 1.
            LockstepScenario s = Run(length, oneWay: true, fromZero: true);
            s.Report("one-way drop 0->1 " + length + " s");

            s.AssertMatchesReference(minimum: 5);
            AssertRecovered(s, length, recoverySeconds: 6);
        }

        [TestCase(3.0)]
        [TestCase(10.0)]
        [Category("Characterization")]
        public void OneWayDrop_OneToZero_RecoversAndMatchesTheReference(double length)
        {
            LockstepScenario s = Run(length, oneWay: true, fromZero: false);
            s.Report("one-way drop 1->0 " + length + " s");

            s.AssertMatchesReference(minimum: 5);
            AssertRecovered(s, length, recoverySeconds: 6);
        }

        [TestCase(1.0)]
        [TestCase(3.0)]
        [TestCase(10.0)]
        [TestCase(25.0)]
        [Category("Characterization")]
        public void TwoWayDrop_RecoversAndMatchesTheReference(double length)
        {
            LockstepScenario s = Run(length, oneWay: false, fromZero: false, seconds: 70);
            s.Report("two-way drop " + length + " s");

            s.AssertMatchesReference(minimum: 5);
            AssertRecovered(s, length, recoverySeconds: 6);
        }

        [Test]
        public void ShortDrop_NeverHolds()
        {
            // Inside the speculation window: the match plays on, rolls back,
            // replays, and the player never sees a hold.
            LockstepScenario s = Run(1.0, oneWay: false, fromZero: false);

            foreach (HarnessPeer p in s.Peers)
                Assert.AreEqual(0, p.Holds, s.Describe("P" + p.Player + " held for a 1 s drop"));
        }

        [Test]
        public void LongDrop_HoldsOnBothSidesAndResumes()
        {
            LockstepScenario s = Run(25.0, oneWay: false, fromZero: false, seconds: 70);

            foreach (HarnessPeer p in s.Peers)
            {
                Assert.GreaterOrEqual(p.Holds, 1, s.Describe("P" + p.Player + " never held across a 25 s drop"));
                Assert.AreEqual(p.Holds, p.HoldEnds, s.Describe("P" + p.Player + " ended a hold it did not start, or never left one"));
            }
        }

        [Test]
        public void Speculation_ReplaysWithTheRealInputs()
        {
            // A 1.5 s drop is long enough to speculate and short enough to be
            // replayed rather than abandoned. The reference match proves the
            // replay applied the opponent's real commands, not the idle guess.
            LockstepScenario s = Run(1.5, oneWay: false, fromZero: false);

            Assert.Greater(s.Peers[0].SpeculationStarts + s.Peers[1].SpeculationStarts, 0,
                s.Describe("the drop did not cause a speculation, so this test covers nothing"));
            s.AssertMatchesReference(minimum: 5);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Drop_MatchesTheReferenceForAnySeed(int seed)
        {
            // Seeds move where the loss falls relative to the tick boundaries.
            LockstepScenario s = Run(3.0, oneWay: true, fromZero: true, seconds: 40, seed: seed);
            s.AssertMatchesReference(minimum: 3);
        }

        [Test]
        [Category("Characterization")]
        public void FrameSpikes_OnOneSide_StayInSync()
        {
            // The editor's frames run to 100-280 ms. A side that freezes for
            // 280 ms is, to its peer, a peer that has gone quiet.
            var s = new LockstepScenario { Seconds = 40 };
            for (int i = 0; i < 8; i++)
                s.Spikes.Add(new FrameSpike { peer = i % 2, at = 5 + 4 * i, duration = 0.28 });
            s.Run();
            s.Report("frame spikes 280 ms");

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
                Assert.AreEqual(0, p.Holds, s.Describe("a frame spike caused a hold"));
        }

        [Test]
        [Category("Characterization")]
        public void LongFrameFreeze_StaysInSync()
        {
            // A 3 s freeze on one side (a debugger break, a driver hang).
            var s = new LockstepScenario { Seconds = 40 };
            s.Spikes.Add(new FrameSpike { peer = 0, at = 15, duration = 3.0 });
            s.Run();
            s.Report("3 s freeze on P0");

            s.AssertMatchesReference(minimum: 5);
        }
    }
}
