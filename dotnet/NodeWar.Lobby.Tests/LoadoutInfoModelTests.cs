using System.Linq;
using NodeWar.Lobby;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutInfoModelTests
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

        [Test] public void Deltas_AgainstBaseNotParent()
        {
            var rows = LoadoutInfoModel.SuitStats(Balance(), "suit.warrior", 1);
            Assert.That(rows.Single(r => r.Label == "Damage").Delta, Is.EqualTo(8));
            Assert.That(rows[0].Value, Is.EqualTo(6), "bonus HP, not total HP");
            Assert.That(rows[0].Delta, Is.EqualTo(6));
        }
        [Test] public void LowerCostAndCooldown_ArePositiveImprovement()
        {
            var rows = LoadoutInfoModel.SuitStats(Balance(), "suit.warrior", 1);
            foreach (string label in new[] { "Move time", "Attack cooldown", "Food cost", "Material cost" })
            {
                var row = rows.Single(r => r.Label == label);
                Assert.That(row.Delta, Is.LessThan(0));
                Assert.That(row.Improvement, Is.GreaterThan(0));
                Assert.That(row.Trend, Is.EqualTo(StatTrend.Better));
            }
        }
        [Test] public void EqualStats_AreZero()
        {
            Assert.That(LoadoutInfoModel.SuitStats(Balance(), "suit.none", 0).All(r => r.Delta == 0 && r.Improvement == 0), Is.True);
        }
        [Test] public void VariantIndex_SelectsExistingBalanceEntry()
        {
            Assert.That(LoadoutInfoModel.SuitStats(Balance(), "suit.warrior", 1)[1].Value, Is.EqualTo(11));
            Assert.That(LoadoutInfoModel.SuitStats(Balance(), "suit.warrior", 99)[1].Value, Is.EqualTo(5));
        }
        [Test] public void MissingNoneEntry_UsesBaseVillagerFields()
        {
            var b = Balance(); b.suitStats = b.suitStats.Skip(1).ToArray();
            Assert.That(LoadoutInfoModel.SuitStats(b, "suit.warrior", 0)[1].Delta, Is.EqualTo(2));
            Assert.That(LoadoutInfoModel.SuitDescription(null), Is.EqualTo(""));
            Assert.That(LoadoutInfoModel.SuitDescription("Frontline fighter"), Is.EqualTo("Frontline fighter"));
        }
        [Test] public void AbsoluteCosts_CompareWithFreeBaseAndNegativeHpStaysBonus()
        {
            var b = Balance(); b.suitStats = new[] { new SuitStats { suitType = SuitType.Scout, bonusHP = -2, foodCost = 7, materialCost = 4 } };
            var rows = LoadoutInfoModel.SuitStats(b, "suit.scout", 0);
            Assert.That(rows[0].Value, Is.EqualTo(-2));
            Assert.That(rows[0].Delta, Is.EqualTo(-2));
            Assert.That(rows.Single(r => r.Label == "Food cost").Value, Is.EqualTo(7));
            Assert.That(rows.Single(r => r.Label == "Food cost").Improvement, Is.EqualTo(-7));
            Assert.That(rows.Single(r => r.Label == "Material cost").Improvement, Is.EqualTo(-4));
        }
    }
}
