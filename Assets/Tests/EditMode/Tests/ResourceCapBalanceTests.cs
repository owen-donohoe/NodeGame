using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class ResourceCapBalanceTests
    {
        [Test]
        public void DefaultsCapFoodMaterialsAndMetal()
        {
            var b = GameBalanceData.Default();
            Assert.AreEqual(30, b.foodCap);
            Assert.AreEqual(30, b.materialsCap);
            Assert.AreEqual(10, b.metalCap);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void NonpositiveCapsKeepLegacyResources(int cap)
        {
            Assert.AreEqual(100, GameBalanceData.ClampResource(100, cap));
            Assert.AreEqual(101, GameBalanceData.AddResource(100, cap));
            Assert.IsTrue(GameBalanceData.HasResourceRoom(100, cap));
        }

        [Test]
        public void PositiveCapsClampAndPreventOverflowAtFullStock()
        {
            Assert.AreEqual(30, GameBalanceData.ClampResource(100, 30));
            Assert.AreEqual(30, GameBalanceData.AddResource(29, 30));
            Assert.AreEqual(30, GameBalanceData.AddResource(30, 30));
            Assert.AreEqual(30, GameBalanceData.AddResource(int.MaxValue, 30));
            Assert.IsFalse(GameBalanceData.HasResourceRoom(30, 30));
            Assert.IsTrue(GameBalanceData.HasResourceRoom(29, 30));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void EachCapChangesFingerprintEvenWhenOthersAreAbsent(int cap)
        {
            var b = GameBalanceData.Default();
            b.foodCap = b.materialsCap = b.metalCap = 0;
            int uncapped = BalanceHasher.Hash(b);
            b.foodCap = cap;
            int food = BalanceHasher.Hash(b);
            b.foodCap = 0;
            b.materialsCap = cap;
            int materials = BalanceHasher.Hash(b);
            b.materialsCap = 0;
            b.metalCap = cap;
            int metal = BalanceHasher.Hash(b);
            CollectionAssert.AllItemsAreUnique(new[] { uncapped, food, materials, metal });
        }
    }
}
