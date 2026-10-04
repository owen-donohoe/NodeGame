using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.View.Tests
{
    public class BreachTempoMathTests
    {
        private static readonly int[] SdTicks = { 2400, 3000 };
        private static readonly int[] SdThresholds = { 2, 1 };

        [TestCase(0, 4000, 0f)]
        [TestCase(1000, 4000, 0.25f)]
        [TestCase(4000, 4000, 1f)]
        [TestCase(9999, 4000, 1f)]
        [TestCase(-5, 4000, 0f)]
        [TestCase(100, 0, 0f)]
        [TestCase(100, -1, 0f)]
        public void BarFill_is_clamped_and_safe_on_a_bad_max(int bar, int max, float expected)
        {
            Assert.AreEqual(expected, BreachTempoMath.BarFill(bar, max), 1e-6f);
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 0, true)]
        [TestCase(0, 1, true)]
        [TestCase(500, 3, true)]
        public void BarVisible_only_when_banked_or_channelling(int bar, int breachers, bool expected)
        {
            Assert.AreEqual(expected, BreachTempoMath.BarVisible(bar, breachers));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(3, 3)]
        [TestCase(8, 8)]
        [TestCase(20, BreachTempoMath.MaxPips)]
        public void PipsToShow_caps_at_the_row_width(int breachers, int expected)
        {
            Assert.AreEqual(expected, BreachTempoMath.PipsToShow(breachers));
        }

        [Test]
        public void Countdown_is_off_before_the_window()
        {
            // 2400 - 51 ticks = 5.1 s away.
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, 2349, 10, out int t));
            Assert.AreEqual(0, t);
        }

        [TestCase(2350, 5)]
        [TestCase(2351, 5)]
        [TestCase(2359, 5)]
        [TestCase(2360, 4)]
        [TestCase(2390, 1)]
        [TestCase(2399, 1)]
        public void Countdown_rounds_up_so_five_shows_first_and_one_last(int tick, int seconds)
        {
            Assert.AreEqual(seconds, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, tick, 10, out int t));
            Assert.AreEqual(2, t);
        }

        [Test]
        public void Countdown_is_gone_on_the_tick_it_fires_and_resumes_for_the_next_step()
        {
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, 2400, 10, out _));
            Assert.AreEqual(5, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, 2950, 10, out int t));
            Assert.AreEqual(1, t);
        }

        [Test]
        public void Countdown_is_off_after_the_last_step_and_with_no_schedule()
        {
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, 3500, 10, out _));
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(null, null, 100, 10, out _));
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(new int[0], new int[0], 100, 10, out _));
            Assert.AreEqual(0, BreachTempoMath.SuddenDeathCountdown(SdTicks, SdThresholds, 2390, 0, out _));
        }

        [Test]
        public void Countdown_agrees_with_the_default_balance_schedule()
        {
            GameBalanceData bal = GameBalanceData.Default();
            int tps = bal.ticksPerSecond;
            int fireTick = bal.suddenDeathTicks[0];

            int seconds = BreachTempoMath.SuddenDeathCountdown(
                bal.suddenDeathTicks, bal.suddenDeathThresholds,
                fireTick - BreachTempoMath.CountdownSeconds * tps, tps, out int next);

            Assert.AreEqual(BreachTempoMath.CountdownSeconds, seconds);
            // The threshold the countdown announces is the one the simulation
            // applies once the tick arrives.
            Assert.AreEqual(next, bal.BreachThresholdAt(fireTick));
        }

        [Test]
        public void Wording_is_distinct_per_stage_and_names_the_threshold()
        {
            Assert.AreEqual("Tempo rising", BreachTempoMath.TempoStageTitle(0));
            Assert.AreNotEqual(BreachTempoMath.TempoStageTitle(0), BreachTempoMath.TempoStageTitle(1));
            Assert.AreEqual(BreachTempoMath.TempoStageTitle(1), BreachTempoMath.TempoStageTitle(5));
            Assert.AreEqual("Sudden death in 5…", BreachTempoMath.CountdownTitle(5));
            StringAssert.Contains("2", BreachTempoMath.ThresholdLine(2));
            StringAssert.Contains("One", BreachTempoMath.ThresholdLine(1));
        }
    }
}
