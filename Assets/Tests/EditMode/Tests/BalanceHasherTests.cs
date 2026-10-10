using System;
using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class BalanceHasherTests
    {
        [TestCase("minionHP", 8)] [TestCase("minionMetalCost", 3)] [TestCase("minionMoveSpeedTicks", 2)]
        public void MinionGlobals_DefaultHashAndValidation(string name, int expected)
        {
            var b = GameBalanceData.Default(); var f = typeof(GameBalanceData).GetField(name); Assert.IsNotNull(f);
            Assert.AreEqual(expected, f.GetValue(b));
            Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(SetCoreScalar(b, name, expected + 1)));
            foreach (int invalid in new[] { 0, -1 }) Assert.IsFalse(SetCoreScalar(b, name, invalid).CoreRulesValid(out _));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void WorkshopCooldown_PerEraHashAndValidation(int era)
        {
            var b = GameBalanceData.Default(); var f = typeof(DistrictStats).GetField("forgeCooldownTicks"); Assert.IsNotNull(f);
            int index = Array.FindIndex(b.districtStats, d => (int)d.districtType == 19 && d.era == era); Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual(30, f.GetValue(b.districtStats[index]));
            var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone(); object entry = copy.districtStats[index];
            f.SetValue(entry, 31); copy.districtStats[index] = (DistrictStats)entry;
            Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy));
            foreach (int invalid in new[] { 0, -1 }) { f.SetValue(entry, invalid); copy.districtStats[index] = (DistrictStats)entry; Assert.IsFalse(copy.CoreRulesValid(out _)); }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void DistrictHealth_DefaultsEveryEraHashAndValidate(int era)
        {
            var b = GameBalanceData.Default();
            foreach (var type in new[] { DistrictType.Fortress, DistrictType.Storehouse, DistrictType.Infirmary })
            foreach (string name in new[] { "healthMax", "healthRegenPerTick" }) {
                var f = typeof(DistrictStats).GetField(name); Assert.IsNotNull(f);
                int index = Array.FindIndex(b.districtStats, d => d.districtType == type && d.era == era);
                Assert.AreEqual(name == "healthMax" ? 3000 : 17, f.GetValue(b.districtStats[index]));
                var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone(); object entry = copy.districtStats[index];
                f.SetValue(entry, (int)f.GetValue(entry) + 1); copy.districtStats[index] = (DistrictStats)entry;
                Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy));
                f.SetValue(entry, -1); copy.districtStats[index] = (DistrictStats)entry; Assert.IsFalse(copy.CoreRulesValid(out _));
            }
            foreach (var d in b.districtStats)
                if (d.districtType != DistrictType.Fortress && d.districtType != DistrictType.Storehouse && d.districtType != DistrictType.Infirmary) {
                    Assert.AreEqual(0, typeof(DistrictStats).GetField("healthMax").GetValue(d));
                    Assert.AreEqual(0, typeof(DistrictStats).GetField("healthRegenPerTick").GetValue(d));
                }
        }
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
        [TestCase("bankCapacity", 5)]
        [TestCase("collectProgressPerTick", 5)] [TestCase("collectProgressPerUnit", 16)]
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
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TownBonus_DefaultAndMutationPerEra(int era)
        {
            var b = GameBalanceData.Default();
            var f = typeof(DistrictStats).GetField("townBonusVillagers"); Assert.IsNotNull(f);
            int index = Array.FindIndex(b.districtStats, d => (int)d.districtType == 15 && d.era == era);
            Assert.GreaterOrEqual(index, 0); Assert.AreEqual(2, f.GetValue(b.districtStats[index]));
            int baseline = BalanceHasher.Hash(b);
            object entry = b.districtStats[index]; f.SetValue(entry, 3); b.districtStats[index] = (DistrictStats)entry;
            Assert.AreNotEqual(baseline, BalanceHasher.Hash(b));
            f.SetValue(entry, -1); b.districtStats[index] = (DistrictStats)entry;
            Assert.IsFalse(b.CoreRulesValid(out string reason)); Assert.IsNotEmpty(reason);
            foreach (var d in GameBalanceData.Default().districtStats)
                if ((int)d.districtType != 15) Assert.AreEqual(0, f.GetValue(d));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void Infirmary_DefaultAndHashMutationPerEra(int era)
        {
            var b = GameBalanceData.Default();
            int index = Array.FindIndex(b.districtStats, d => d.districtType == DistrictType.Infirmary && d.era == era);
            Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual(10, b.districtStats[index].healIntervalTicks);
            Assert.AreEqual(1, b.districtStats[index].respawnBoostPerWorker);
            Assert.AreEqual(20, b.districtStats[index].respawnCostReductionPercent);
            foreach (string field in new[] { "healIntervalTicks", "respawnBoostPerWorker", "respawnCostReductionPercent" })
            {
                var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone();
                var f = typeof(DistrictStats).GetField(field); object entry = copy.districtStats[index];
                f.SetValue(entry, (int)f.GetValue(entry) + 1); copy.districtStats[index] = (DistrictStats)entry;
                Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy), field);
                f.SetValue(entry, field == "healIntervalTicks" ? 0 : -1); copy.districtStats[index] = (DistrictStats)entry;
                Assert.IsFalse(copy.CoreRulesValid(out _), field);
                if (field == "respawnCostReductionPercent") { f.SetValue(entry, 101); copy.districtStats[index] = (DistrictStats)entry; Assert.IsFalse(copy.CoreRulesValid(out _)); }
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void FortressArrays_EveryElementEraHashedAndValidated(int era)
        {
            var b = GameBalanceData.Default(); int index = Array.FindIndex(b.districtStats, d => d.districtType == DistrictType.Fortress && d.era == era);
            foreach (string name in new[] { "fortressMaterialsCosts", "fortressMetalCosts", "fortressResistancePercent" })
            {
                var field = typeof(DistrictStats).GetField(name); Assert.IsNotNull(field, name);
                var values = (int[])field.GetValue(b.districtStats[index]);
                CollectionAssert.AreEqual(name == "fortressMaterialsCosts" ? new[] { 0, 4, 8, 12 } : name == "fortressMetalCosts" ? new[] { 0, 1, 2, 3 } : new[] { 0, 25, 40, 50 }, values);
                int other = Array.FindIndex(b.districtStats, d => d.districtType == DistrictType.Fortress && d.era == (era + 1) % 6);
                Assert.AreNotSame(values, field.GetValue(b.districtStats[other]));
                for (int j = 0; j < 4; j++)
                {
                    var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone(); object entry = copy.districtStats[index];
                    var changed = (int[])values.Clone(); changed[j]++; field.SetValue(entry, changed); copy.districtStats[index] = (DistrictStats)entry;
                    Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy), name + j);
                    changed[j] = j == 0 ? 1 : -1; Assert.IsFalse(copy.CoreRulesValid(out _));
                }
                foreach (var invalid in new[] { new int[3], new int[5] })
                {
                    var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone(); object entry = copy.districtStats[index]; field.SetValue(entry, invalid); copy.districtStats[index] = (DistrictStats)entry;
                    Assert.IsFalse(copy.CoreRulesValid(out _)); Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy));
                }
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void StorehouseDuration_PerEraAndHash(int era)
        {
            var b = GameBalanceData.Default();
            Assert.IsNull(typeof(GameBalanceData).GetField("fortificationHP"));
            foreach (var type in new[] { DistrictType.Storehouse }) {
                int index = Array.FindIndex(b.districtStats, d => d.districtType == type && d.era == era);
                Assert.GreaterOrEqual(index, 0);
                var field = typeof(DistrictStats).GetField("productionTicks");
                Assert.IsNotNull(field); int expected = 80;
                Assert.AreEqual(expected, field.GetValue(b.districtStats[index]));
                var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone(); object entry = copy.districtStats[index];
                field.SetValue(entry, expected + 1); copy.districtStats[index] = (DistrictStats)entry;
                Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy));
                field.SetValue(entry, -1); copy.districtStats[index] = (DistrictStats)entry; Assert.IsFalse(copy.CoreRulesValid(out _));
            }
        }

        [Test]
        public void BankTuning_RejectsInvalidActiveScalarsAndAllowsHistoricalZeros()
        {
            var b = GameBalanceData.Default();
            foreach (string name in new[] { "bankCapacity", "collectProgressPerTick", "collectProgressPerUnit" })
                foreach (int value in new[] { -1, 0 })
                    Assert.IsFalse(SetCoreScalar(b, name, value).CoreRulesValid(out _));
            var historical = b;
            foreach (string name in new[] { "bankCapacity", "collectProgressPerTick", "collectProgressPerUnit" })
                historical = SetCoreScalar(historical, name, 0);
            Assert.IsTrue(historical.CoreRulesValid(out _)); Assert.IsFalse(historical.BankTuningValid());
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void PierTuning_EveryEraHashesAndValidates(int era)
        {
            var b = GameBalanceData.Default();
            int index = Array.FindIndex(b.districtStats, d => d.districtType == DistrictType.Pier && d.era == era);
            Assert.GreaterOrEqual(index, 0);
            foreach (string name in new[] { "pierTravelDivisor" })
            {
                var f = typeof(DistrictStats).GetField(name); Assert.IsNotNull(f);
                Assert.AreEqual(2, f.GetValue(b.districtStats[index]));
                var copy = b; copy.districtStats = (DistrictStats[])b.districtStats.Clone();
                object entry = copy.districtStats[index]; f.SetValue(entry, (int)f.GetValue(entry) + 1);
                copy.districtStats[index] = (DistrictStats)entry;
                Assert.AreNotEqual(BalanceHasher.Hash(b), BalanceHasher.Hash(copy));
                foreach (int invalid in new[] { -1, int.MaxValue })
                {
                    f.SetValue(entry, invalid); copy.districtStats[index] = (DistrictStats)entry;
                    Assert.IsFalse(copy.CoreRulesValid(out _));
                }
            }
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
                if (field.FieldType == typeof(int[]))
                    field.SetValue(boxed, new[] { 0, 1, 2, 3 });
                else if (field.FieldType == typeof(int))
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
