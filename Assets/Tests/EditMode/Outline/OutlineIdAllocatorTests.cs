using NUnit.Framework;
using NodeWar.View.Outline;

namespace NodeWar.Tests
{
    /// <summary>
    /// The allocator is the part of the outline system with real edge cases in
    /// it, and the part whose failures are silent: a wrong ID does not throw, it
    /// draws a confidently incorrect picture. Two groups sharing an ID lose the
    /// line between them and read as one object, which is exactly the bug the
    /// group-ID design exists to prevent.
    /// </summary>
    public class OutlineIdAllocatorTests
    {
        /// <summary>
        /// Fresh allocator per test rather than a [SetUp] field, matching how
        /// the simulation tests use TestBoardFactory -- no shared state between
        /// cases, and each test reads as a complete story on its own.
        /// </summary>
        private static OutlineIdAllocator NewAllocator() => new OutlineIdAllocator();

        [Test]
        public void Rent_FromAFreshAllocator_HandsOutTheLowestIdFirst()
        {
            OutlineIdAllocator allocator = NewAllocator();

            // Lowest-free rather than most-recently-freed, so the same sequence
            // of selections produces the same IDs every run. That is what makes
            // a mask debug view legible.
            Assert.AreEqual(1, allocator.Rent());
            Assert.AreEqual(2, allocator.Rent());
            Assert.AreEqual(3, allocator.Rent());
        }

        [Test]
        public void Rent_NeverReturnsZero_BecauseZeroMeansNothingIsHere()
        {
            OutlineIdAllocator allocator = NewAllocator();

            for (int i = 0; i < OutlineIdAllocator.Capacity; i++)
            {
                Assert.AreNotEqual(OutlineIdAllocator.None, allocator.Rent());
            }
        }

        [Test]
        public void Rent_AfterAReturn_RecyclesTheFreedIdAheadOfHigherOnes()
        {
            OutlineIdAllocator allocator = NewAllocator();

            allocator.Rent();               // 1
            int second = allocator.Rent();  // 2
            allocator.Rent();               // 3

            allocator.Return(second);

            Assert.AreEqual(2, allocator.Rent());
        }

        [Test]
        public void Rent_WhenEveryIdIsOut_RefusesRatherThanSharingOne()
        {
            OutlineIdAllocator allocator = NewAllocator();

            for (int i = 0; i < OutlineIdAllocator.Capacity; i++) allocator.Rent();

            Assert.AreEqual(OutlineIdAllocator.Capacity, allocator.ActiveCount);
            Assert.AreEqual(OutlineIdAllocator.None, allocator.Rent());
            Assert.AreEqual(1, allocator.DroppedCount);

            // The refusal must not have consumed anything.
            Assert.AreEqual(OutlineIdAllocator.Capacity, allocator.ActiveCount);
        }

        [Test]
        public void Rent_AfterExhaustionThenARelease_SucceedsAgain()
        {
            OutlineIdAllocator allocator = NewAllocator();

            for (int i = 0; i < OutlineIdAllocator.Capacity; i++) allocator.Rent();
            Assert.AreEqual(OutlineIdAllocator.None, allocator.Rent());

            allocator.Return(7);

            Assert.AreEqual(7, allocator.Rent());
        }

        [Test]
        public void Return_TheSameIdTwice_IsANoOpAndLeavesTheCountIntact()
        {
            OutlineIdAllocator allocator = NewAllocator();

            int id = allocator.Rent();

            Assert.IsTrue(allocator.Return(id));
            Assert.IsFalse(allocator.Return(id));
            Assert.AreEqual(0, allocator.ActiveCount);

            // A double release driving ActiveCount negative would make the
            // "nothing is outlined, skip both passes" check wrong in the one
            // direction that costs a render target every frame.
            Assert.AreEqual(1, allocator.Rent());
        }

        [Test]
        public void Return_ZeroOrOutOfRange_IsANoOp()
        {
            OutlineIdAllocator allocator = NewAllocator();

            allocator.Rent();

            Assert.IsFalse(allocator.Return(OutlineIdAllocator.None));
            Assert.IsFalse(allocator.Return(-4));
            Assert.IsFalse(allocator.Return(OutlineIdAllocator.MaxId + 1));
            Assert.AreEqual(1, allocator.ActiveCount);
        }

        [Test]
        public void Return_AnIdThatWasNeverRented_IsANoOp()
        {
            OutlineIdAllocator allocator = NewAllocator();

            Assert.IsFalse(allocator.Return(42));
            Assert.AreEqual(0, allocator.ActiveCount);
        }

        [Test]
        public void IsRented_TracksRentAndReturn()
        {
            OutlineIdAllocator allocator = NewAllocator();

            int id = allocator.Rent();

            Assert.IsTrue(allocator.IsRented(id));
            Assert.IsFalse(allocator.IsRented(OutlineIdAllocator.None));
            Assert.IsFalse(allocator.IsRented(OutlineIdAllocator.MaxId + 1));

            allocator.Return(id);
            Assert.IsFalse(allocator.IsRented(id));
        }

        [Test]
        public void Clear_ReleasesEverythingAndForgetsTheDropCount()
        {
            OutlineIdAllocator allocator = NewAllocator();

            for (int i = 0; i < OutlineIdAllocator.Capacity; i++) allocator.Rent();
            allocator.Rent();

            allocator.Clear();

            Assert.AreEqual(0, allocator.ActiveCount);
            Assert.AreEqual(0, allocator.DroppedCount);
            Assert.AreEqual(1, allocator.Rent());
        }

        [Test]
        public void Capacity_FitsTheSingleEightBitMaskChannelItIsStoredIn()
        {
            // The mask keeps the group ID in one 8-bit UNorm channel and
            // reserves zero, so 255 is not an arbitrary cap -- widening it means
            // changing the mask format, not this constant.
            Assert.AreEqual(255, OutlineIdAllocator.Capacity);
            Assert.AreEqual(255, OutlineIdAllocator.MaxId);
            Assert.AreEqual(0, OutlineIdAllocator.None);
        }
    }
}
