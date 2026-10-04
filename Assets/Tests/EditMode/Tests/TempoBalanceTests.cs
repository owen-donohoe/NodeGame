using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class TempoBalanceTests
    {
        [Test]
        public void DefaultsAreValidAndProductionDurationsAtLeastTwo()
        {
            var b = GameBalanceData.Default();
            Assert.IsTrue(b.TempoAndBreachValid(out string reason), reason);
            foreach (var d in b.districtStats)
            {
                if (d.productionTicks != 0) Assert.GreaterOrEqual(d.productionTicks, 2);
                if (d.secondaryProductionTicks != 0) Assert.GreaterOrEqual(d.secondaryProductionTicks, 2);
            }
            Assert.IsTrue(default(GameBalanceData).TempoAndBreachValid(out reason), reason);
            Assert.AreEqual(100, default(GameBalanceData).TempoPercent(null, 5000));
            Assert.AreEqual(5000, default(GameBalanceData).ScaledTicks(null, 5000));
        }

        [Test]
        public void InclusiveIntegrationMatchesExplicitSumAtNonRoundBoundaries()
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = new[] { 3, 8 };
            int[] pct = { 125, 151 };
            long sum = 0;
            for (int t = 1; t <= 1000; t++)
            {
                long before = sum / 100;
                sum += t < 3 ? 100 : t < 8 ? 125 : 151;
                Assert.AreEqual(sum / 100, b.ScaledTicks(pct, t), "tick " + t);
                Assert.AreEqual(sum / 100 - before, b.TimerDecrement(pct, t), "tick " + t);
            }
            Assert.AreEqual(100, b.TempoPercent(pct, 2));
            Assert.AreEqual(125, b.TempoPercent(pct, 3));
            Assert.AreEqual(151, b.TempoPercent(pct, 8));
        }

        [TestCase(110)] [TestCase(125)] [TestCase(150)] [TestCase(200)]
        public void TimerAveragesExactlyOver600Ticks(int percent)
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = new[] { 1 };
            int total = 0;
            for (int t = 1; t <= 600; t++) total += b.TimerDecrement(new[] { percent }, t);
            Assert.AreEqual(6 * percent, total);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidTempoFallsBackTo100(int defect)
        {
            var b = GameBalanceData.Default();
            switch (defect)
            {
                case 0: b.tempoStageTicks = new[] { 5, 5 }; break;
                case 1: b.tempoStageTicks = new[] { 0, 5 }; break;
                case 2: b.tempoClaimPercent = new[] { 125 }; break;
                case 3: b.tempoClaimPercent[0] = 0; break;
                case 4: b.tempoClaimPercent = null; break;
            }
            Assert.IsFalse(b.TempoAndBreachValid(out _));
            Assert.AreEqual(100, b.TempoPercent(b.tempoClaimPercent, 10000));
            Assert.AreEqual(10000, b.ScaledTicks(b.tempoClaimPercent, 10000));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void InvalidSuddenDeathFallsBackToBaseThreshold(int defect)
        {
            var b = GameBalanceData.Default();
            switch (defect)
            {
                case 0: b.suddenDeathTicks = new[] { 5, 4 }; break;
                case 1: b.suddenDeathThresholds = new[] { 2 }; break;
                case 2: b.suddenDeathThresholds[1] = 0; break;
                case 3: b.suddenDeathThresholds[1] = 2; break;
                case 4: b.suddenDeathThresholds[0] = 3; break;
            }
            Assert.IsFalse(b.TempoAndBreachValid(out _));
            Assert.AreEqual(3, b.BreachThresholdAt(10000));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void InvalidBarDisablesFeature(int defect)
        {
            var b = GameBalanceData.Default();
            switch (defect)
            {
                case 0: b.breachSwarmRate = null; break;
                case 1: b.breachSwarmRate = new int[0]; break;
                case 2: b.breachSwarmRate[1] = 0; break;
                case 3: b.breachBarDecayPerTick = -1; break;
            }
            Assert.IsFalse(b.TempoAndBreachValid(out _));
            Assert.IsFalse(b.BreachBarEnabled());
            Assert.AreEqual(3, b.BreachThresholdAt(10000));
        }

        [Test]
        public void UnsafeProductionTempoIsRejected()
        {
            var b = GameBalanceData.Default();
            b.districtStats[0].productionTicks = 1;
            Assert.IsFalse(b.ProductionTempoValid());
            Assert.IsFalse(b.TempoAndBreachValid(out _));
            b = GameBalanceData.Default();
            b.tempoProductionPercent[0] = 4000;
            Assert.IsFalse(b.ProductionTempoValid());
        }

        [Test]
        public void ThresholdBoundariesAndLegacyMode()
        {
            var b = GameBalanceData.Default();
            Assert.AreEqual(3, b.BreachThresholdAt(2399));
            Assert.AreEqual(2, b.BreachThresholdAt(2400));
            Assert.AreEqual(1, b.BreachThresholdAt(3000));
            b.breachBarMax = 0;
            Assert.AreEqual(3, b.BreachThresholdAt(3000));
            b.breachBarMax = 4000;
            b.breachBarDecayPerTick = 0;
            Assert.IsTrue(b.TempoAndBreachValid(out _));
        }
    }
}
