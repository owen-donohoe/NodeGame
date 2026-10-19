using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StageSlotsTests
    {
        [TestCase(int.MinValue, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(int.MaxValue, 5)]
        public void Layout_AlwaysThreeOverTwo_InFillOrder(int equipped, int filled)
        {
            var slots = StageSlots.Layout(equipped);
            Assert.AreEqual(5, slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.AreEqual(i < 3 ? 0 : 1, slots[i].Row);
                Assert.AreEqual(i < 3 ? i : i - 3, slots[i].Column);
                Assert.AreEqual(i < filled, slots[i].Filled);
            }
        }
    }
}
