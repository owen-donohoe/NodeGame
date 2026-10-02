using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// Adaptive input delay end to end: two cores, a simulated link, and the
    /// requests each sends the other. Every run is still held to the reference,
    /// which is what shows that raising the delay mid-match leaves no hole in
    /// the inputs and lowering it leaves no tick stamped twice.
    /// </summary>
    public class AdaptiveDelayTests
    {
        private static LinkProfile Link(double oneWay, double jitter = 0.0)
        {
            return new LinkProfile { Delay = oneWay, Jitter = jitter };
        }

        [Test]
        public void GoodLink_KeepsTheBaseDelay()
        {
            // The feel guarantee: a link that never needed more delay never gets it.
            LockstepScenario s = new LockstepScenario { Seconds = 90, ZeroToOne = Link(0.065), OneToZero = Link(0.065) }.Run();

            s.AssertMatchesReference(minimum: 10);
            foreach (HarnessPeer p in s.Peers)
                Assert.AreEqual(2, p.MaxInputDelay, s.Describe("P" + p.Player + " raised its delay on a link that did not need it"));
        }

        [Test]
        public void ModestLossAndJitter_DoNotRaiseTheDelay()
        {
            // Jitter and the odd lost packet are what the redundant inputs are for.
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 90,
                ZeroToOne = new LinkProfile { Delay = 0.065, Jitter = 0.03, Loss = 0.02 },
                OneToZero = new LinkProfile { Delay = 0.065, Jitter = 0.03, Loss = 0.02 }
            }.Run();
            s.Report("modest loss and jitter");

            s.AssertMatchesReference(minimum: 10);
            foreach (HarnessPeer p in s.Peers)
                Assert.LessOrEqual(p.MaxInputDelay, 3, s.Describe("P" + p.Player + " over-reacted to ordinary jitter"));
        }

        [Test]
        [Category("Characterization")]
        public void SlowLink_RaisesTheDelayAndStopsFreezing()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 60, ZeroToOne = Link(0.2, 0.05), OneToZero = Link(0.2, 0.05) }.Run();
            s.Report("200 ms one-way, 50 ms jitter");

            s.AssertMatchesReference(minimum: 5);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.GreaterOrEqual(p.MaxInputDelay, 3, s.Describe("P" + p.Player + " never raised its delay"));
                Assert.Less(p.FrozenSeconds, 3.0, s.Describe("P" + p.Player + " still froze on a slow link"));
                Assert.AreEqual(0, p.Holds);
            }
        }

        [Test]
        public void OneSlowDirection_RaisesOnlyTheSideWhoseInputsAreLate()
        {
            // Peer 1's inputs take 250 ms to reach peer 0; peer 0's take 65 ms to reach peer 1.
            // Peer 0 is the one stalling, so it asks peer 1 for more delay and peer 1 alone raises it.
            LockstepScenario s = new LockstepScenario { Seconds = 60, ZeroToOne = Link(0.065), OneToZero = Link(0.25, 0.05) }.Run();
            s.Report("asymmetric 65 / 250 ms");

            s.AssertMatchesReference(minimum: 5);
            Assert.AreEqual(2, s.Peers[0].MaxInputDelay, "the side with the fast outbound link raised its delay");
            Assert.GreaterOrEqual(s.Peers[1].MaxInputDelay, 4, "the side with the slow outbound link did not");
        }

        [Test]
        public void TheDelayIsCapped_WhateverTheLink()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 90, ZeroToOne = Link(0.7, 0.2), OneToZero = Link(0.7, 0.2) }.Run();
            s.Report("700 ms one-way");

            s.AssertMatchesReference(minimum: 3);
            foreach (HarnessPeer p in s.Peers)
                Assert.LessOrEqual(p.MaxInputDelay, InputDelayController.MaxDelay);
        }

        [Test]
        public void TheDelayComesBackDownWhenTheLinkRecovers()
        {
            // 60 s on a slow link, then good for the rest. Lowering is deliberately
            // slow (20 s calm per step), so give it room: 6 -> 2 is up to four steps.
            System.Func<double, double> delay = t => t < 60 ? 0.22 : 0.065;
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 240,
                ZeroToOne = new LinkProfile { DelayAt = delay, Jitter = 0.04 },
                OneToZero = new LinkProfile { DelayAt = delay, Jitter = 0.04 }
            }.Run();
            s.Report("slow for 60 s, then good");

            s.AssertMatchesReference(minimum: 20);
            foreach (HarnessPeer p in s.Peers)
            {
                Assert.GreaterOrEqual(p.MaxInputDelay, 3, s.Describe("never raised during the bad stretch"));
                Assert.AreEqual(2, p.Core.InputDelay, s.Describe("P" + p.Player + " is still on a raised delay long after the link recovered"));
            }
        }

        [Test]
        public void ALinkThatGetsWorseMidMatch_IsFollowedWithoutDesync()
        {
            System.Func<double, double> delay = t => t < 30 ? 0.065 : 0.24;
            LockstepScenario s = new LockstepScenario
            {
                Seconds = 90,
                ZeroToOne = new LinkProfile { DelayAt = delay, Jitter = 0.04, Loss = 0.03 },
                OneToZero = new LinkProfile { DelayAt = delay, Jitter = 0.04, Loss = 0.03 }
            }.Run();
            s.Report("good, then 240 ms one-way with 3% loss");

            s.AssertMatchesReference(minimum: 15);
            foreach (HarnessPeer p in s.Peers)
                Assert.GreaterOrEqual(p.MaxInputDelay, 3);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AdaptiveDelay_MatchesTheReferenceForAnySeed(int seed)
        {
            LockstepScenario s = new LockstepScenario
            {
                Seed = seed,
                Seconds = 80,
                ZeroToOne = new LinkProfile { Delay = 0.17, Jitter = 0.08, Loss = 0.05, BurstEnter = 0.01, BurstExit = 0.3 },
                OneToZero = new LinkProfile { Delay = 0.17, Jitter = 0.08, Loss = 0.05, BurstEnter = 0.01, BurstExit = 0.3 }
            }.Run();

            s.AssertMatchesReference(minimum: 10);
        }

        // ===== What a peer may ask =====

        private static byte[] AskPacket(int forTick, int senderDelay, int requested)
        {
            return InputSerializer.Serialize(new TickInput
            {
                forTick = forTick,
                commands = new NodeWar.Simulation.GameCommand[0],
                senderDelay = (byte)senderDelay,
                requestedDelay = (byte)requested
            });
        }

        private static void Deliver(LockstepScenario s, byte[] packet)
        {
            // Link 1 carries peer 1's packets to peer 0.
            s.Links[1].Send(s.Now, packet);
            for (int i = 0; i < 8; i++)
            {
                s.Now += 1.0 / 60.0;
                s.Peers[0].Frame(s.Now, 1.0 / 60.0);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(99)]
        [TestCase(255)]
        public void ARequestOutsideTheAllowedRange_IsIgnored(int asked)
        {
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();

            Deliver(s, AskPacket(5, 2, asked));

            Assert.AreEqual(2, s.Peers[0].Core.InputDelay);
        }

        [Test]
        public void AValidRequest_IsAppliedAndRaisesTheDelay()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();

            Deliver(s, AskPacket(5, 2, 5));

            Assert.AreEqual(5, s.Peers[0].Core.InputDelay);
            Assert.AreEqual(2, s.Peers[0].Core.PeerDelay, "the peer's own delay is what it reports, not what it asked of us");
        }

        [Test]
        public void ARequestToChangeAgainInsideTheMinimumInterval_IsIgnored()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();

            Deliver(s, AskPacket(5, 2, 4));
            Deliver(s, AskPacket(6, 2, 6));

            Assert.AreEqual(4, s.Peers[0].Core.InputDelay, "two requests in a row both took effect");
        }

        [Test]
        public void AStaleCopy_CannotUndoANewerRequest()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();
            Deliver(s, AskPacket(20, 2, 4));
            Assert.AreEqual(4, s.Peers[0].Core.InputDelay);

            // Past the interval, an older packet arrives late asking for 2.
            s.Now += 2.0;
            Deliver(s, AskPacket(10, 2, 2));

            Assert.AreEqual(4, s.Peers[0].Core.InputDelay, "a delayed older packet reversed a newer request");
        }


        [Test]
        public void AnInputFromAPeerOnTheMaximumDelay_IsAcceptedUpToTheLimit()
        {
            // A peer on the maximum delay, speculating, can honestly be this far
            // ahead. The limit is derived from MAX_DELAY, not from the starting delay.
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();
            // The core runs its two pre-seeded ticks while packets are delivered, so the
            // limit it applies has moved by up to 2 from the one at tick 0.
            int atLimit = LockstepCore.InputAheadLimit;
            int pastLimit = atLimit + 3;

            Deliver(s, AskPacket(atLimit, InputDelayController.MaxDelay, 0));
            Deliver(s, AskPacket(pastLimit, InputDelayController.MaxDelay, 0));

            Assert.IsTrue(s.Peers[0].Core.HasRemoteInput(atLimit), "a legitimate far-ahead input was refused");
            Assert.IsFalse(s.Peers[0].Core.HasRemoteInput(pastLimit), "an input past the limit was accepted");
        }

        [Test]
        public void TheInputAcceptanceLimit_LeavesRoomForTheMaximumDelayAndAFullSpeculation()
        {
            // 20 ticks of speculation and the largest delay, both ends, plus the lag allowance.
            Assert.GreaterOrEqual(LockstepCore.InputAheadLimit, 20 + InputDelayController.MaxDelay + 20 + InputDelayController.MaxDelay);
        }
        [Test]
        public void ThePeersReportedDelay_IsReadOnlyFromInRangeValues()
        {
            LockstepScenario s = new LockstepScenario { Seconds = 0 }.Run();

            Deliver(s, AskPacket(5, 4, 0));
            Assert.AreEqual(4, s.Peers[0].Core.PeerDelay);

            Deliver(s, AskPacket(6, 200, 0));
            Assert.AreEqual(4, s.Peers[0].Core.PeerDelay, "an out-of-range report replaced the peer's delay");
        }
    }
}
