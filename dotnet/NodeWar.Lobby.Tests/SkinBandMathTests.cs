using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class SkinBandMathTests
    {
        [TestCase(-11, 5, 4)]
        [TestCase(-5, 5, 0)]
        [TestCase(-1, 5, 4)]
        [TestCase(0, 5, 0)]
        [TestCase(11, 5, 1)]
        [TestCase(int.MinValue, 5, 2)]
        public void NegativeAndPositive_Wrap(int index, int count, int expected)
        {
            Assert.AreEqual(expected, SkinBandMath.Wrap(index, count));
        }

        [TestCase(-1.51, -2)]
        [TestCase(-1.5, -1)]
        [TestCase(-0.5, 0)]
        [TestCase(0.49, 0)]
        [TestCase(0.5, 1)]
        [TestCase(1.51, 2)]
        public void Snapping_MidpointTieAndNearest(double position, double expected)
        {
            Assert.AreEqual(expected, SkinBandMath.Snap(position));
            Assert.AreEqual(SkinBandMath.Wrap((int)expected, 5), SkinBandMath.CenterIndex(position, 5));
        }

        [Test]
        public void EmptyAndSingle_NoDivisionByZero()
        {
            var slots = new SkinBandSlot[SkinBandMath.VisibleCount];
            SkinBandMath.Fill(-12.5, 0, slots);
            Assert.AreEqual(-1, SkinBandMath.Wrap(3, 0));
            Assert.AreEqual(-1, SkinBandMath.CenterIndex(-12.5, 0));
            foreach (var slot in slots)
            {
                Assert.AreEqual(-1, slot.ChoiceIndex);
                Assert.AreEqual(0, slot.Alpha);
                Assert.AreEqual(0, slot.Scale);
                Assert.IsFalse(slot.Centered);
            }

            SkinBandMath.Fill(-12.5, 1, slots);
            int centered = 0;
            foreach (var slot in slots)
            {
                Assert.AreEqual(0, slot.ChoiceIndex);
                if (slot.Centered) centered++;
            }
            Assert.AreEqual(1, centered, "Decorative repeats must not create multiple centered Use choices.");
            Assert.AreEqual(0, SkinBandMath.CenterIndex(-12.5, 1));
        }

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(31)]
        public void ThirteenSlots_StableIdentityAcrossWrap(int count)
        {
            var before = new SkinBandSlot[SkinBandMath.VisibleCount];
            var after = new SkinBandSlot[SkinBandMath.VisibleCount];
            SkinBandMath.Fill(-0.25, count, before);
            SkinBandMath.Fill(count - 0.25, count, after);
            Assert.AreEqual(13, before.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(before[i].ChoiceIndex, Is.InRange(0, count - 1));
                Assert.AreEqual(before[i].ChoiceIndex, after[i].ChoiceIndex);
                Assert.AreEqual(before[i].LogicalIndex + count, after[i].LogicalIndex);
                Assert.AreEqual(before[i].LogicalIndex + 0.25, before[i].Offset);
                Assert.AreEqual(before[i].Offset, after[i].Offset);
                Assert.AreEqual(before[i].Scale, after[i].Scale);
                Assert.AreEqual(before[i].Alpha, after[i].Alpha);
                Assert.AreEqual(before[i].Centered, after[i].Centered);
            }
        }

        [Test]
        public void CenteredSlot_HasSymmetricScaleAndAlpha()
        {
            var slots = new SkinBandSlot[SkinBandMath.VisibleCount];
            SkinBandMath.Fill(7, 5, slots);
            Assert.AreEqual(7, slots[6].LogicalIndex);
            Assert.AreEqual(2, slots[6].ChoiceIndex);
            Assert.AreEqual(0, slots[6].Offset);
            Assert.AreEqual(1, slots[6].Scale);
            Assert.AreEqual(1, slots[6].Alpha);
            for (int distance = 1; distance <= 6; distance++)
            {
                Assert.IsFalse(slots[6 - distance].Centered);
                Assert.IsFalse(slots[6 + distance].Centered);
                Assert.AreEqual(slots[6 - distance].Scale, slots[6 + distance].Scale);
                Assert.AreEqual(slots[6 - distance].Alpha, slots[6 + distance].Alpha);
                Assert.Less(slots[6 + distance].Scale, slots[6 + distance - 1].Scale);
                Assert.Less(slots[6 + distance].Alpha, slots[6 + distance - 1].Alpha);
            }
        }

        [Test]
        public void Midpoint_CentersIncreasingIndexWithEqualNeighborAppearance()
        {
            var slots = new SkinBandSlot[SkinBandMath.VisibleCount];
            SkinBandMath.Fill(-0.5, 5, slots);
            Assert.AreEqual(-1, slots[5].LogicalIndex);
            Assert.AreEqual(0, slots[6].LogicalIndex);
            Assert.IsFalse(slots[5].Centered);
            Assert.IsTrue(slots[6].Centered);
            Assert.AreEqual(slots[5].Scale, slots[6].Scale);
            Assert.AreEqual(slots[5].Alpha, slots[6].Alpha);
        }

        [Test]
        public void LargeCount_LogicalNeighborsDoNotOverflow()
        {
            var slots = new SkinBandSlot[SkinBandMath.VisibleCount];
            double position = int.MaxValue - 0.5;
            SkinBandMath.Fill(position, int.MaxValue, slots);
            Assert.AreEqual(0, SkinBandMath.CenterIndex(position, int.MaxValue));
            Assert.AreEqual(0, slots[6].ChoiceIndex);
            Assert.AreEqual((long)int.MaxValue + 6, slots[12].LogicalIndex);
            foreach (var slot in slots) Assert.That(slot.ChoiceIndex, Is.InRange(0, int.MaxValue - 1));
        }

        [TestCase(1000000000000.25)]
        [TestCase(-1000000000000.5)]
        public void Rebase_PreservesCenteredSkin(double position)
        {
            var before = new SkinBandSlot[SkinBandMath.VisibleCount];
            var after = new SkinBandSlot[SkinBandMath.VisibleCount];
            double rebased = SkinBandMath.Rebase(position, 7);
            Assert.Less(Math.Abs(rebased), 7);
            Assert.AreEqual((position - Math.Floor(position)), rebased - Math.Floor(rebased));
            int expected = SkinBandMath.Wrap((int)(SkinBandMath.Snap(position) % 7), 7);
            Assert.AreEqual(expected, SkinBandMath.CenterIndex(position, 7));
            Assert.AreEqual(expected, SkinBandMath.CenterIndex(rebased, 7));
            SkinBandMath.Fill(position, 7, before);
            SkinBandMath.Fill(rebased, 7, after);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].ChoiceIndex, after[i].ChoiceIndex);
                Assert.AreEqual(before[i].Offset, after[i].Offset);
                Assert.AreEqual(before[i].Centered, after[i].Centered);
            }
        }

        [Test]
        public void Fill_ReusesBuffer()
        {
            var slots = new SkinBandSlot[SkinBandMath.VisibleCount];
            for (int i = 0; i < 1000; i++) SkinBandMath.Fill(i * 0.125, 7, slots);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) SkinBandMath.Fill(i * -0.125, 7, slots);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.AreEqual(0, allocated);
            SkinBandMath.Fill(2, 7, slots);
            Assert.AreEqual(2, slots[6].ChoiceIndex);
            SkinBandMath.Fill(0, 0, slots);
            foreach (var slot in slots) Assert.AreEqual(-1, slot.ChoiceIndex);
        }

        [Test]
        public void InvalidInputs_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => SkinBandMath.Fill(0, 2, null));
            Assert.Throws<ArgumentException>(() => SkinBandMath.Fill(0, 2, new SkinBandSlot[12]));
            Assert.Throws<ArgumentException>(() => SkinBandMath.Fill(0, 2, new SkinBandSlot[14]));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkinBandMath.Snap(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkinBandMath.Fill(double.PositiveInfinity, 2, new SkinBandSlot[13]));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkinBandMath.Wrap(0, -1));
        }
    }
}
