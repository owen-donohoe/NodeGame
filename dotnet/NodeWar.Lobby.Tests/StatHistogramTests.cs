using System.Linq;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StatHistogramTests
    {
        private static GameBalanceData Balance() => new GameBalanceData
        {
            baseHP = 13, baseAttackDamage = 3, baseMoveSpeedTicks = 9, baseAttackCooldownMax = 20,
            suitStats = new[] {
                new SuitStats { suitType = SuitType.None, attackDamage = 3, moveSpeedTicks = 9, attackCooldownMax = 20, foodCost = 8, materialCost = 6 },
                new SuitStats { suitType = SuitType.Warrior, era = 0, bonusHP = 2, attackDamage = 5, moveSpeedTicks = 7, attackCooldownMax = 15, foodCost = 4, materialCost = 2 },
                new SuitStats { suitType = SuitType.Warrior, era = 1, bonusHP = 6, attackDamage = 11, moveSpeedTicks = 5, attackCooldownMax = 10, foodCost = 3, materialCost = 1 }
            }
        };

        [Test] public void Higher_IsGain_Lower_IsLoss()
        {
            var up = StatHistogram.Row("Damage", 3, 5, true, 1);
            Assert.That((up.Filled, up.Gain, up.Loss), Is.EqualTo((3, 2, 0)));
            Assert.That(up.Delta, Is.EqualTo(2));
            var down = StatHistogram.Row("Damage", 5, 3, true, 1);
            Assert.That((down.Filled, down.Gain, down.Loss), Is.EqualTo((3, 0, 2)));
        }
        [Test] public void LowerIsBetter_DropIsGain_RiseIsLoss()
        {
            Assert.That(StatHistogram.Row("Move time", 9, 7, false, 1).Gain, Is.EqualTo(2));
            Assert.That(StatHistogram.Row("Move time", 7, 9, false, 1).Loss, Is.EqualTo(2));
        }
        [Test] public void SmallChange_ShowsAtLeastOneCell_NoChangeShowsNone()
        {
            Assert.That(StatHistogram.Row("x", 20, 21, true, 5).Gain, Is.EqualTo(1));
            var same = StatHistogram.Row("x", 20, 20, true, 5);
            Assert.That((same.Gain, same.Loss, same.Filled), Is.EqualTo((0, 0, 4)));
        }
        [Test] public void Unit_KeepsCellsWithinMax_AndIsSharedAcrossValues()
        {
            Assert.That(StatHistogram.Unit(3, 8), Is.EqualTo(1));
            Assert.That(StatHistogram.Unit(20, 4), Is.EqualTo(2));
            Assert.That(StatHistogram.Unit(-35), Is.EqualTo(5));
            Assert.That(StatHistogram.Cells(1000, StatHistogram.Unit(1000)), Is.LessThanOrEqualTo(StatHistogram.MaxCells));
        }
        [Test] public void SuitRows_PairOfRowsPerStat_BaseSuitThenVillager()
        {
            var b = Balance();
            var rows = StatHistogram.SuitRows(LoadoutInfoModel.SuitStats(b, "suit.warrior", 1), LoadoutInfoModel.SuitStats(b, "suit.warrior", 0));
            Assert.That(rows.Length, Is.EqualTo(14));
            var dmgSuit = rows.Single(r => r.Label == "Damage vs base suit");
            var dmgVillager = rows.Single(r => r.Label == "Damage vs villager");
            Assert.That(dmgSuit.Delta, Is.EqualTo(6), "11 against the base suit's 5");
            Assert.That(dmgVillager.Delta, Is.EqualTo(8), "11 against the villager's 3");
            Assert.That(rows.Single(r => r.Label == "Move time vs villager").Gain, Is.GreaterThan(0), "faster is better");
        }
        [Test] public void BaseSuit_AgainstItself_HasNoDelta()
        {
            var b = Balance();
            var own = LoadoutInfoModel.SuitStats(b, "suit.warrior", 0);
            Assert.That(StatHistogram.SuitRows(own, own).Where(r => r.Label.EndsWith("base suit")).All(r => r.Gain == 0 && r.Loss == 0), Is.True);
        }
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(int.MaxValue, int.MinValue)]
        [TestCase(99, 100)]
        [TestCase(50000, 50001)]
        [TestCase(0, -100000)]
        public void ExtremeValues_StayBoundedWithoutOverflow(int reference, int value)
        {
            var row = StatHistogram.Row("value", reference, value, true, 0);
            Assert.That(row.Delta, Is.EqualTo((long)value - reference));
            Assert.That(row.Filled + row.Gain + row.Loss, Is.LessThanOrEqualTo(StatHistogram.MaxCells));
            Assert.That(row.Gain + row.Loss, Is.GreaterThan(0));
            Assert.That(StatHistogram.Cells(value, StatHistogram.Unit(reference, value)), Is.LessThanOrEqualTo(StatHistogram.MaxCells));
        }
        [Test] public void SuitComparisons_ShareUnitsAndCellWidths()
        {
            var own = new[] { new LoadoutStatRow("Damage", 100, 99, 99) };
            var parent = new[] { new LoadoutStatRow("Damage", 99, 98, 98) };
            var rows = StatHistogram.SuitRows(own, parent);
            Assert.That(rows[0].Unit, Is.EqualTo(rows[1].Unit));
            Assert.That(rows.All(r => r.Filled + r.Gain + r.Loss <= StatHistogram.MaxCells), Is.True);
            Assert.That(rows[0].Gain, Is.GreaterThan(0));
        }
        [Test] public void Districts_CompareNumericFactsToBaseDistrict()
        {
            var b = new GameBalanceData { districtStats = new[] {
                new DistrictStats { districtType = DistrictType.Farm, era = 0, productionTicks = 40 },
                new DistrictStats { districtType = DistrictType.Farm, era = 1, productionTicks = 30 }
            } };
            var rows = StatHistogram.DistrictRows(b, DistrictType.Farm, 1);
            var cycle = rows.Single(r => r.Label == "Production cycle");
            Assert.That(cycle.Value, Is.EqualTo(30));
            Assert.That(cycle.Delta, Is.EqualTo(-10));
            Assert.That(cycle.Gain, Is.GreaterThan(0));
            Assert.That(rows.Single(r => r.Label == "Yield per cycle").Delta, Is.Zero);
        }    }
}
