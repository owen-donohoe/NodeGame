using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// The delay-request rules on synthetic tick sequences. Time here is just a
    /// counter: one tick is 0.1 s, as in the game.
    /// </summary>
    public class InputDelayControllerTests
    {
        private const float Tick = 0.1f;
        private const float Roomy = 0.2f;

        private InputDelayController controller;
        private float now;

        [SetUp]
        public void SetUp()
        {
            controller = new InputDelayController();
            now = 0f;
        }

        private void Tick_(bool late, float slack = Roomy, bool disturbed = false, int peerDelay = 2)
        {
            controller.OnLiveTick(now, late, slack, disturbed, peerDelay);
            now += Tick;
        }

        private void Run(int ticks, bool late = false, float slack = Roomy, int peerDelay = 2)
        {
            for (int i = 0; i < ticks; i++) Tick_(late, slack, false, peerDelay);
        }

        [Test]
        public void StartsWithNoOpinion()
        {
            Assert.AreEqual(0, controller.Request);
        }

        [Test]
        public void CalmAtTheBaseDelay_NeverAsksForAnything()
        {
            Run(2000, late: false, slack: 0.5f);

            Assert.AreEqual(0, controller.Request, "there is nothing below the base delay to lower to");
        }

        [Test]
        public void ThreeLateTicksInTheWindow_AskForOneMore()
        {
            Tick_(true);
            Run(5);
            Tick_(true);
            Run(5);
            Assert.AreEqual(0, controller.Request, "two late ticks are not yet a pattern");

            Tick_(true);

            Assert.AreEqual(3, controller.Request);
        }

        [Test]
        public void LateTicksSpreadOutOfTheWindow_AreNotAPattern()
        {
            for (int i = 0; i < 6; i++)
            {
                Tick_(true);
                Run(40); // the window is 30 ticks, so each late tick has left it
            }

            Assert.AreEqual(0, controller.Request);
        }

        [Test]
        public void TheRequestIsRelativeToWhatThePeerSaysItUses()
        {
            for (int i = 0; i < 3; i++) Tick_(true, peerDelay: 4);

            Assert.AreEqual(5, controller.Request);
        }

        [Test]
        public void NeverAsksForMoreThanTheMaximum()
        {
            for (int i = 0; i < 50; i++) Tick_(true, peerDelay: InputDelayController.MaxDelay);

            Assert.AreEqual(0, controller.Request);
        }

        [Test]
        public void AFurtherRaiseWaitsOutTheCooldown()
        {
            for (int i = 0; i < 3; i++) Tick_(true);
            Assert.AreEqual(3, controller.Request);

            // The peer has acted (it reports 3) and its ticks are still late in
            // flight: inside the cooldown that must not read as a reason for 4.
            for (int i = 0; i < 10; i++) Tick_(true, peerDelay: 3);
            Assert.AreEqual(3, controller.Request, "asked again inside the cooldown");

            // Past it, and the peer is now using 3 and is still late.
            for (int i = 0; i < 10; i++) Tick_(true, peerDelay: 3);
            Assert.AreEqual(4, controller.Request);
        }

        [Test]
        public void DisturbedTicks_SayNothingAndClearWhatCameBefore()
        {
            Tick_(true);
            Tick_(true);
            Tick_(false, disturbed: true);
            Tick_(true);

            Assert.AreEqual(0, controller.Request, "late ticks either side of a disturbance were counted together");

            for (int i = 0; i < 100; i++) Tick_(true, disturbed: true);
            Assert.AreEqual(0, controller.Request, "an outage was read as a slow link");
        }

        [Test]
        public void LongCalmWithSlackToSpare_AsksForOneLess()
        {
            Run(200, slack: 0.2f, peerDelay: 4);

            Assert.AreEqual(3, controller.Request);
        }

        [Test]
        public void CalmWithoutEnoughSlack_KeepsTheDelay()
        {
            // 0.1 s of slack at 4 ticks would leave nothing at 3.
            Run(400, slack: 0.1f, peerDelay: 4);

            Assert.AreEqual(0, controller.Request);
        }

        [Test]
        public void OneThinSlackTick_SpoilsTheWholeCalmStretch()
        {
            Run(150, slack: 0.3f, peerDelay: 4);
            Tick_(false, slack: 0.05f, peerDelay: 4);
            Run(100, slack: 0.3f, peerDelay: 4);

            Assert.AreEqual(0, controller.Request, "the minimum slack over the stretch is what counts");
        }


        [Test]
        public void AThinSlackTickIsForgottenOnceItLeavesTheWindow()
        {
            // A bad stretch long ago must not hold the delay up forever after the
            // link recovers: only the last 200 calm ticks count.
            Run(10, slack: 0.3f, peerDelay: 4);
            Tick_(false, slack: 0.05f, peerDelay: 4);
            Run(150, slack: 0.3f, peerDelay: 4);
            Assert.AreEqual(0, controller.Request, "lowered with the thin tick still in the window");

            Run(120, slack: 0.3f, peerDelay: 4);

            Assert.AreEqual(3, controller.Request);
        }
        [Test]
        public void ALateTickRestartsTheCalm()
        {
            Run(150, slack: 0.3f, peerDelay: 4);
            Tick_(true, peerDelay: 4);
            Run(100, slack: 0.3f, peerDelay: 4);

            Assert.AreEqual(0, controller.Request);
        }

        [Test]
        public void LoweringWaitsOutItsLongerCooldown()
        {
            // Raise first, so a change has just happened.
            for (int i = 0; i < 3; i++) Tick_(true, peerDelay: 3);
            Assert.AreEqual(4, controller.Request);

            // 200 calm ticks is 20 s: well past the cooldown, so it lowers.
            Run(200, slack: 0.3f, peerDelay: 4);

            Assert.AreEqual(3, controller.Request);
        }

        [Test]
        public void NeverLowersBelowTheBase()
        {
            Run(600, slack: 0.5f, peerDelay: InputDelayController.BaseDelay);

            Assert.AreEqual(0, controller.Request);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(99)]
        public void ANonsensePeerDelayIsClamped(int peerDelay)
        {
            for (int i = 0; i < 3; i++) Tick_(true, peerDelay: peerDelay);

            if (peerDelay > InputDelayController.MaxDelay) Assert.AreEqual(0, controller.Request);
            else Assert.AreEqual(InputDelayController.BaseDelay + 1, controller.Request);
        }

        [Test]
        public void Reset_ForgetsTheRequestAndTheHistory()
        {
            for (int i = 0; i < 3; i++) Tick_(true);
            Assert.AreEqual(3, controller.Request);

            controller.Reset();

            Assert.AreEqual(0, controller.Request);
            Tick_(true);
            Tick_(true);
            Assert.AreEqual(0, controller.Request, "late ticks from before the reset still counted");
        }
    }
}
