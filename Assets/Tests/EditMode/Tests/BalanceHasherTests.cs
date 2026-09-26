using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class BalanceHasherTests
    {
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
                if (field.FieldType == typeof(SuitStats[])) continue;
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
