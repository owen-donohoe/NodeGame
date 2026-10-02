using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// What a core does outside the middle of a match: before it is unpaused
    /// and after EndMatch. GameManager drives it every frame in both states, so
    /// both have to be quiet in the right ways.
    /// </summary>
    public class LifecycleTests
    {
        private const double Frame = 1.0 / 60.0;

        /// <summary>A scenario whose peers are started and unpaused, with no frames run yet.</summary>
        private static LockstepScenario Started(LinkProfile zeroToOne = null, LinkProfile oneToZero = null)
        {
            return new LockstepScenario
            {
                Seconds = 0,
                ZeroToOne = zeroToOne ?? new LinkProfile(),
                OneToZero = oneToZero ?? new LinkProfile()
            }.Run();
        }

        /// <summary>Frames every peer given for <paramref name="seconds"/>, advancing the scenario clock.</summary>
        private static void Step(LockstepScenario s, double seconds, params HarnessPeer[] peers)
        {
            double until = s.Now + seconds;
            while (s.Now < until)
            {
                foreach (HarnessPeer peer in peers) peer.Frame(s.Now, Frame);
                s.Now += Frame;
            }
        }

        [Test]
        public void AfterEndMatch_NothingTicksSendsOrFlushes()
        {
            LockstepScenario s = Started();
            Step(s, 5, s.Peers);
            HarnessPeer ended = s.Peers[0];
            Assert.Greater(ended.State.tickCount, 0, "the match never got going");

            ended.Core.EndMatch();
            int tick = ended.State.tickCount;
            long sent = s.Links[0].Sent;
            int flushes = ended.FlushCalls;

            // Long enough for a heartbeat (0.5 s) and several resends (0.1 s).
            Step(s, 5, s.Peers);

            Assert.AreEqual(tick, ended.State.tickCount, "an ended core ticked");
            Assert.AreEqual(sent, s.Links[0].Sent, "an ended core sent a packet");
            Assert.AreEqual(flushes, ended.FlushCalls, "an ended core reached the transport on Flush");
        }

        [Test]
        public void NotUnpaused_ReceivesAndHeartbeatsButNeverTicksOrHolds()
        {
            LockstepScenario s = Started();
            HarnessPeer active = s.Peers[1];
            var paused = new HarnessPeer(0, s.Peers[0].State, s.Links[0], s.Links[1], s.Script, () => s.Now);
            paused.Core.Initialize(paused.State, paused.Buffer, paused, 0, 10, 0f, 0f);

            Step(s, 10, active, paused);

            Assert.AreEqual(0, paused.State.tickCount, "a paused core ticked");
            Assert.AreEqual(0, paused.Holds, "a paused core started a hold");
            Assert.IsFalse(paused.Core.IsHolding);
            Assert.Greater(paused.InputDeliveries, 0, "a paused core did not read its link");
            Assert.Greater(s.Links[0].Sent, 0, "a paused core sent no heartbeat");
        }

        [Test]
        public void EndMatchDuringAHold_ClearsIt()
        {
            var cut = new LinkProfile().Cut(0, 1000);
            var alsoCut = new LinkProfile().Cut(0, 1000);
            LockstepScenario s = Started(cut, alsoCut);
            HarnessPeer peer = s.Peers[0];

            // Speculation window plus the wait before it, with margin.
            for (int i = 0; i < 20 && !peer.Core.IsHolding; i++) Step(s, 1, s.Peers);
            Assert.IsTrue(peer.Core.IsHolding, "the hold never started");

            peer.Core.EndMatch();

            Assert.IsFalse(peer.Core.IsHolding);
        }

        [Test]
        public void IsEnded_ReflectsEndMatch()
        {
            LockstepScenario s = Started();
            Assert.IsFalse(s.Peers[0].Core.IsEnded);

            s.Peers[0].Core.EndMatch();

            Assert.IsTrue(s.Peers[0].Core.IsEnded);
        }
    }
}
