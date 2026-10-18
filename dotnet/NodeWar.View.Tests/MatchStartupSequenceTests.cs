using NUnit.Framework;
using NodeWar.Core;
using NodeWar.Simulation;
using NodeWar.Tests;
using MatchPhase = NodeWar.Core.MatchStartupSequence.MatchPhase;

namespace NodeWar.View.Tests
{
    [TestFixture]
    [NonParallelizable] // MatchFactory.Configure sets simulation statics.
    public class MatchStartupSequenceTests
    {
        private PausedSimulationDriver driver;
        private MatchStartupSequence startup;

        [SetUp]
        public void SetUp()
        {
            var balance = GameBalanceData.Default();
            var board = BoardFixtures.LandGrid3x3();
            MatchFactory.Configure(balance, board);
            var state = MatchFactory.Build(balance, board, null, new PlayerSetup[2]);
            driver = new PausedSimulationDriver(state);
            startup = new MatchStartupSequence(() =>
            {
                Assert.AreEqual(MatchPhase.Playing, startup.Phase);
                driver.Unpause();
            });
        }

        [Test]
        public void Draft_with_transition_waits_for_completion_then_ticks()
        {
            startup.StartDraft();
            startup.CompleteDraft();
            bool presentationStarted = false;
            startup.BeginPostDraftTransition(true, () => presentationStarted = true);

            Assert.IsTrue(presentationStarted);
            Assert.AreEqual(MatchPhase.Countdown, startup.Phase);
            AssertPaused();

            startup.CompleteTransition();

            AssertPlayingAndTicking();
        }

        [Test]
        public void Draft_without_controller_falls_back_after_presentation_setup_then_ticks()
        {
            startup.StartDraft();
            startup.CompleteDraft();
            bool sideSelected = false;
            startup.BeginPostDraftTransition(false, () =>
            {
                Assert.AreEqual(MatchPhase.Countdown, startup.Phase);
                AssertPaused();
                sideSelected = true;
            });

            Assert.IsTrue(sideSelected);
            AssertPlayingAndTicking();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Skip_draft_ticks_without_a_completion_event(bool hasStartupWave)
        {
            bool initialized = false;
            bool waveStarted = false;
            startup.SkipDraftAndInitialize(() =>
            {
                // Preserve testing mode's existing phase during board/UI setup.
                Assert.AreEqual(MatchPhase.Playing, startup.Phase);
                AssertPaused();
                initialized = true;
                if (hasStartupWave) waveStarted = true; // Animation raises no event.
            });

            Assert.IsTrue(initialized);
            Assert.AreEqual(hasStartupWave, waveStarted);
            AssertPlayingAndTicking();
        }

        [Test]
        public void PreDraft_Drafting_PostDraft_and_Countdown_keep_driver_paused()
        {
            Assert.AreEqual(MatchPhase.PreDraft, startup.Phase);
            AssertPaused();
            startup.StartDraft();
            Assert.AreEqual(MatchPhase.Drafting, startup.Phase);
            AssertPaused();
            startup.CompleteDraft();
            Assert.AreEqual(MatchPhase.PostDraft, startup.Phase);
            AssertPaused();
            startup.BeginPostDraftTransition(true, () => AssertPaused());
            Assert.AreEqual(MatchPhase.Countdown, startup.Phase);
            AssertPaused();
        }

        [TestCase("draft-transition")]
        [TestCase("draft-fallback")]
        [TestCase("skip-draft")]
        public void Repeated_completion_unpauses_only_once(string path)
        {
            if (path == "skip-draft")
                startup.SkipDraftAndInitialize(() => { });
            else
            {
                startup.StartDraft();
                startup.CompleteDraft();
                startup.BeginPostDraftTransition(path == "draft-transition", () => { });
            }

            startup.CompleteTransition();
            startup.CompleteTransition();

            AssertPlayingAndTicking();
        }

        private void AssertPaused()
        {
            Assert.AreEqual(0, driver.UnpauseCount);
            driver.AdvanceTicks(3);
            Assert.AreEqual(0, driver.State.tickCount);
        }

        private void AssertPlayingAndTicking()
        {
            Assert.AreEqual(MatchPhase.Playing, startup.Phase);
            Assert.AreEqual(1, driver.UnpauseCount);
            int before = driver.State.tickCount;
            driver.AdvanceTicks(3);
            Assert.AreEqual(before + 3, driver.State.tickCount);
        }

        // Only the pause gate is faked. Ticks run the real simulation over a
        // factory-built board, without a MonoBehaviour, wall clock or lockstep.
        private sealed class PausedSimulationDriver
        {
            private bool paused = true;
            public SimulationState State { get; }
            public int UnpauseCount { get; private set; }

            public PausedSimulationDriver(SimulationState state) => State = state;

            public void Unpause()
            {
                UnpauseCount++;
                paused = false;
            }

            public void AdvanceTicks(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (paused || State.gameOver) return;
                    GameSimulation.SimulateTick(State);
                }
            }
        }
    }
}
