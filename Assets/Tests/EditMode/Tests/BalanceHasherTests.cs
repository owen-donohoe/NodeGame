using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class BalanceHasherTests
    {
        private static GameBalanceData SetCoreScalar(GameBalanceData b, string name, int value)
        {
            var field = typeof(GameBalanceData).GetField(name);
            Assert.IsNotNull(field, name + " must be registered in the DTO");
            object boxed = b; field.SetValue(boxed, value); return (GameBalanceData)boxed;
        }
        [TestCase("captureBonusPercentPerStep", 25)]
        [TestCase("captureBonusMaxSteps", 2)]
        [TestCase("recruitBaseCost", 6)]
        [TestCase("recruitCostPerRecruit", 3)]
        public void CoreScalar_RegisteredDefaultAndIndependentMutation(string name, int expected)
        {
            var b = GameBalanceData.Default(); var field = typeof(GameBalanceData).GetField(name);
            Assert.IsNotNull(field); Assert.AreEqual(expected, field.GetValue(b));
            Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(SetCoreScalar(b, name, expected + 1)));
        }
        [Test]
        public void CoreRulesValidation_RejectsNegativeAndOverflowingPercentageProducts()
        {
            var method = typeof(GameBalanceData).GetMethod("CoreRulesValid"); Assert.IsNotNull(method);
            var b = GameBalanceData.Default(); object[] args = { null };
            Assert.IsTrue((bool)method.Invoke(b, args));
            foreach (string name in new[] { "captureBonusPercentPerStep", "captureBonusMaxSteps" })
            {
                Assert.IsFalse((bool)method.Invoke(SetCoreScalar(b, name, -1), args)); Assert.IsNotEmpty((string)args[0]);
            }
            b = SetCoreScalar(SetCoreScalar(b, "captureBonusPercentPerStep", int.MaxValue), "captureBonusMaxSteps", int.MaxValue);
            Assert.IsFalse((bool)method.Invoke(b, args)); Assert.IsNotEmpty((string)args[0]);
        }
        [Test]
        public void CoreRulesValidation_ChecksNonOpposingRateWhenDecrementIsZero()
        {
            var b = GameBalanceData.Default(); b.baseClaimPerTick = int.MaxValue;
            b.decrementMultiplier = 0; b.breachSwarmRate = null;
            b.captureBonusPercentPerStep = b.captureBonusMaxSteps = 100000;
            Assert.IsFalse(b.CoreRulesValid(out string reason)); Assert.IsNotEmpty(reason);
        }
        private static GameBalanceData WithOneSuit()
        {
            GameBalanceData b = GameBalanceData.Default();
            b.suitStats = new[]
            {
                new SuitStats
                {
                    suitType = SuitType.Warrior, bonusHP = 1, attackDamage = 2, moveSpeedTicks = 3,
                    attackCooldownMax = 4, foodCost = 5, materialCost = 6, fightPriority = 7
                }
            };
            return b;
        }

        [Test]
        public void EqualBalance_HashesEqual()
        {
            Assert.AreEqual(BalanceHasher.Hash(WithOneSuit()), BalanceHasher.Hash(WithOneSuit()));
        }

        // Walks the struct rather than a hand-kept list, so a field added to
        // GameBalanceData and forgotten in BalanceHasher fails here.
        [Test]
        public void EveryBalanceField_MovesTheHash()
        {
            int baseline = BalanceHasher.Hash(WithOneSuit());

            foreach (FieldInfo field in typeof(GameBalanceData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(SuitStats[]) || field.FieldType == typeof(DistrictStats[])) continue;
                if (field.FieldType == typeof(int[]))
                {
                    GameBalanceData b = WithOneSuit();
                    object arrayBox = b;
                    int[] original = (int[])field.GetValue(arrayBox);
                    int[] changed = original == null || original.Length == 0 ? new[] { 1 } : (int[])original.Clone();
                    if (original != null && original.Length > 0) changed[0]++;
                    field.SetValue(arrayBox, changed);
                    Assert.AreNotEqual(baseline, BalanceHasher.Hash((GameBalanceData)arrayBox), field.Name);
                    continue;
                }
                Assert.AreEqual(typeof(int), field.FieldType,
                    field.Name + " is not an int: BalanceHasher and this test need to learn its type.");

                object boxed = WithOneSuit();
                field.SetValue(boxed, (int)field.GetValue(boxed) + 1);
                Assert.AreNotEqual(baseline, BalanceHasher.Hash((GameBalanceData)boxed),
                    field.Name + " does not reach BalanceHasher.");
            }
        }

        [Test]
        public void EverySuitStatsField_MovesTheHash()
        {
            int baseline = BalanceHasher.Hash(WithOneSuit());

            foreach (FieldInfo field in typeof(SuitStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                GameBalanceData b = WithOneSuit();
                object boxed = b.suitStats[0];
                if (field.FieldType == typeof(int))
                    field.SetValue(boxed, (int)field.GetValue(boxed) + 1);
                else if (field.FieldType == typeof(SuitType))
                    field.SetValue(boxed, SuitType.Guardian);
                else
                    Assert.Fail(field.Name + " has a type BalanceHasher and this test do not handle.");
                b.suitStats[0] = (SuitStats)boxed;

                Assert.AreNotEqual(baseline, BalanceHasher.Hash(b), "SuitStats." + field.Name + " does not reach BalanceHasher.");
            }
        }

        [Test]
        public void EveryDistrictStatsField_MovesTheHash()
        {
            int baseline = BalanceHasher.Hash(WithOneSuit());

            foreach (FieldInfo field in typeof(DistrictStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                GameBalanceData b = WithOneSuit();
                object boxed = b.districtStats[0];
                if (field.FieldType == typeof(int))
                    field.SetValue(boxed, (int)field.GetValue(boxed) + 1);
                else if (field.FieldType == typeof(DistrictType))
                    field.SetValue(boxed, DistrictType.Camp);
                else
                    Assert.Fail(field.Name + " has a type BalanceHasher and this test do not handle.");
                b.districtStats[0] = (DistrictStats)boxed;

                Assert.AreNotEqual(baseline, BalanceHasher.Hash(b), "DistrictStats." + field.Name + " does not reach BalanceHasher.");
            }
        }

        [Test]
        public void NullAndEmptySuitStats_HashDifferently()
        {
            GameBalanceData none = GameBalanceData.Default();
            GameBalanceData empty = GameBalanceData.Default();
            empty.suitStats = new SuitStats[0];

            Assert.AreNotEqual(BalanceHasher.Hash(none), BalanceHasher.Hash(empty));
        }

        [Test]
        public void SuitStatsOrder_MovesTheHash()
        {
            GameBalanceData a = WithOneSuit();
            GameBalanceData b = WithOneSuit();
            SuitStats second = a.suitStats[0];
            second.suitType = SuitType.Guardian;
            a.suitStats = new[] { a.suitStats[0], second };
            b.suitStats = new[] { second, b.suitStats[0] };

            Assert.AreNotEqual(BalanceHasher.Hash(a), BalanceHasher.Hash(b));
        }
    }
}
