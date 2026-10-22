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
        public void Layout_AlwaysTwoOverThree_InFillOrder(int equipped, int filled)
        {
            var slots = StageSlots.Layout(equipped);
            Assert.AreEqual(5, slots.Length);
            int[] rows = { 1, 1, 1, 0, 0 };
            int[] columns = { 0, 1, 2, 0, 1 };
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.AreEqual(rows[i], slots[i].Row);
                Assert.AreEqual(columns[i], slots[i].Column);
                Assert.AreEqual(i < filled, slots[i].Filled);
            }
        }
    }
}
