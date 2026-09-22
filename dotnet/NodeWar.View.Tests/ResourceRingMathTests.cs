using NUnit.Framework;
using NodeWar.UI;

namespace NodeWar.View.Tests
{
    public class ResourceRingMathTests
    {
        // ===== colour stop boundaries =====
        // v < 3 critical, v < 5 low, v < 8 warn, v < 10 ok, v < 20 good
        // (blends internally), else rich. 30/31 is not a stop boundary -
        // both sit in the flat StopRich zone - covered here to confirm the
        // colour does not keep changing once lit segments saturate.

        [TestCase(2, ResourceRingMath.StopCritical)]
        [TestCase(3, ResourceRingMath.StopLow)]
        [TestCase(4, ResourceRingMath.StopLow)]
        [TestCase(5, ResourceRingMath.StopWarn)]
        [TestCase(7, ResourceRingMath.StopWarn)]
        [TestCase(8, ResourceRingMath.StopOk)]
        [TestCase(9, ResourceRingMath.StopOk)]
        [TestCase(10, ResourceRingMath.StopGood)]
        [TestCase(19, ResourceRingMath.StopGood)]
        [TestCase(20, ResourceRingMath.StopRich)]
        [TestCase(30, ResourceRingMath.StopRich)]
        [TestCase(31, ResourceRingMath.StopRich)]
        public void ColorStopIndex_lands_on_the_right_side_of_every_boundary(int value, int expectedStop)
        {
            Assert.AreEqual(expectedStop, ResourceRingMath.ColorStopIndex(value));
        }

        // ===== the good zone's internal blend, 10 -> 19 =====

        [Test]
        public void GoodBlendFraction_is_zero_at_ten()
        {
            Assert.AreEqual(0f, ResourceRingMath.GoodBlendFraction(10));
        }

        [Test]
        public void GoodBlendFraction_is_one_at_nineteen()
        {
            Assert.AreEqual(1f, ResourceRingMath.GoodBlendFraction(19));
        }

        [Test]
        public void GoodBlendFraction_is_linear_between_the_ends()
        {
            Assert.AreEqual(4f / 9f, ResourceRingMath.GoodBlendFraction(14), 1e-6f);
        }

        [TestCase(0)]
        [TestCase(9)]
        public void GoodBlendFraction_clamps_to_zero_below_ten(int value)
        {
            Assert.AreEqual(0f, ResourceRingMath.GoodBlendFraction(value));
        }

        [TestCase(20)]
        [TestCase(45)]
        public void GoodBlendFraction_clamps_to_one_at_and_above_twenty(int value)
        {
            Assert.AreEqual(1f, ResourceRingMath.GoodBlendFraction(value));
        }

        // ===== lit segments per ring =====
        // Ring 0 = outer (units 1-10), ring 1 = middle (11-20), ring 2 =
        // inner (21-30). Above 30 every ring is full and the value keeps
        // counting past it without changing the lit count further.

        [TestCase(0, 0, 0)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 2, 0)]

        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 0)]
        [TestCase(1, 2, 0)]

        [TestCase(10, 0, 10)]
        [TestCase(10, 1, 0)]
        [TestCase(10, 2, 0)]

        [TestCase(11, 0, 10)]
        [TestCase(11, 1, 1)]
        [TestCase(11, 2, 0)]

        [TestCase(25, 0, 10)]
        [TestCase(25, 1, 10)]
        [TestCase(25, 2, 5)]

        [TestCase(30, 0, 10)]
        [TestCase(30, 1, 10)]
        [TestCase(30, 2, 10)]

        [TestCase(45, 0, 10)]
        [TestCase(45, 1, 10)]
        [TestCase(45, 2, 10)]
        public void LitSegments_matches_the_expected_count_per_ring(int value, int ring, int expectedLit)
        {
            Assert.AreEqual(expectedLit, ResourceRingMath.LitSegments(value, ring));
        }

        [Test]
        public void LitSegments_never_exceeds_ten_or_drops_below_zero()
        {
            for (int value = -5; value <= 60; value++)
            {
                for (int ring = 0; ring < ResourceRingMath.RingCount; ring++)
                {
                    int lit = ResourceRingMath.LitSegments(value, ring);
                    Assert.GreaterOrEqual(lit, 0);
                    Assert.LessOrEqual(lit, ResourceRingMath.SegmentsPerRing);
                }
            }
        }
    }
}
