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
        private static StatSegment[] Bar(params (StatSegment kind, int count)[] runs)
        {
            var cells = new StatSegment[StatHistogram.MaxCells];
            int at = 0;
            foreach (var (kind, count) in runs) for (int i = 0; i < count; i++) cells[at++] = kind;
            return cells;
        }
        [Test] public void SuitRows_OneRowPerStat_ComparedWithBaseSuit_BarCarriesVillager()
        {
            var b = Balance();
            var rows = StatHistogram.SuitRows(LoadoutInfoModel.SuitStats(b, "suit.warrior", 1), LoadoutInfoModel.SuitStats(b, "suit.warrior", 0));
            Assert.That(rows.Length, Is.EqualTo(6));
            var damage = rows.Single(r => r.Label == "Damage");
            Assert.That(damage.Delta, Is.EqualTo(6), "11 against the base suit's 5");
            Assert.That(damage.Better, Is.True);
            Assert.That(damage.Unit, Is.EqualTo(2));
            Assert.That(damage.Segments, Is.EqualTo(Bar((StatSegment.Villager, 2), (StatSegment.BaseSuit, 1), (StatSegment.Gain, 3))));
            var move = rows.Single(r => r.Label == "Move time");
            Assert.That(move.Delta, Is.EqualTo(-2));
            Assert.That(move.Better, Is.True, "faster is better");
            Assert.That(move.Segments, Is.EqualTo(Bar((StatSegment.Villager, 5), (StatSegment.Gain, 2), (StatSegment.BaseSuit, 2))));
        }
        [Test] public void BaseSuit_AgainstItself_HasNoChangeCells()
        {
            var b = Balance();
            var own = LoadoutInfoModel.SuitStats(b, "suit.warrior", 0);
            var rows = StatHistogram.SuitRows(own, own);
            Assert.That(rows.All(r => r.Delta == 0 && !r.Segments.Contains(StatSegment.Gain)), Is.True);
        }
        [Test] public void StatBar_UpUp_StacksVillagerBaseThenGain()
            => Assert.That(StatBar.Cells(2, 4, 7, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 2), (StatSegment.BaseSuit, 2), (StatSegment.Gain, 3))));
        [Test] public void StatBar_UpDown_BaseToneThenLostCells()
            => Assert.That(StatBar.Cells(2, 6, 4, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 2), (StatSegment.BaseSuit, 2), (StatSegment.Loss, 2))));
        [Test] public void StatBar_DownUp_BaseLossDashedUnderGain()
            => Assert.That(StatBar.Cells(6, 3, 5, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 3), (StatSegment.Gain, 2), (StatSegment.Loss, 1))));
        [Test] public void StatBar_DownDown_BothLossesDashed()
            => Assert.That(StatBar.Cells(6, 4, 2, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 2), (StatSegment.Loss, 4))));
        [Test] public void StatBar_ClampsAtTenCells()
        {
            Assert.That(StatBar.Cells(3, 8, 30, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 3), (StatSegment.BaseSuit, 5), (StatSegment.Gain, 2))));
            Assert.That(StatBar.Cells(50, 50, 50, true, 1), Is.EqualTo(Bar((StatSegment.Villager, 10))));
        }
        [Test] public void StatBar_SmallChangeInOneCell_StillShowsOneCell()
        {
            var bar = StatBar.Cells(20, 20, 21, true, 5);
            Assert.That(bar, Is.EqualTo(Bar((StatSegment.Villager, 4), (StatSegment.Gain, 1))));
        }
        [Test] public void StatBar_LowerIsBetter_RiseIsLossFallIsGain()
        {
            Assert.That(StatBar.Cells(9, 9, 11, false, 1)[9], Is.EqualTo(StatSegment.Loss));
            Assert.That(StatBar.Cells(9, 9, 7, false, 1), Is.EqualTo(Bar((StatSegment.Villager, 7), (StatSegment.Gain, 2))));
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
        [Test] public void SuitRows_HideAllZeroAndInternalStats_KeepNonzeroComparisons()
        {
            var own = new[] {
                new LoadoutStatRow("Damage", 0, 0, 0),
                new LoadoutStatRow("Food cost", 0, -4, 4),
                new LoadoutStatRow("Fight priority", 12, 12, 12)
            };
            var rows = StatHistogram.SuitRows(own, own);
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "Food cost" }));
            Assert.That(rows[0].Delta, Is.EqualTo(0), "base suit against itself");
        }
        [Test] public void DistrictRows_KeepOwnFactsAndHideSharedRulesAndZeroPairs()
        {
            var b = new GameBalanceData {
                bankCapacity = 20, collectProgressPerUnit = 5, collectProgressPerTick = 2,
                maxWorkersPerNode = 0,
                districtStats = new[] { new DistrictStats { districtType = DistrictType.Farm, productionTicks = 40 } }
            };
            var rows = StatHistogram.DistrictRows(b, DistrictType.Farm, 0);
            Assert.That(rows.Select(r => r.Label), Is.EquivalentTo(new[] { "Production cycle", "Yield per cycle", "Bank capacity" }));
            Assert.That(rows.All(r => r.Value != 0 || r.Delta != 0), Is.True);
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
