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

        // ===== semicircle sweep: the upper half of a ring, flat side down =====

        [Test]
        public void SweepDegrees_is_half_a_circle()
        {
            Assert.AreEqual(180f, ResourceRingMath.SweepDegrees);
        }

        [Test]
        public void StartDegrees_is_the_flat_bases_left_end()
        {
            Assert.AreEqual(-180f, ResourceRingMath.StartDegrees);
        }

        [Test]
        public void The_sweep_ends_at_the_flat_bases_right_end()
        {
            Assert.AreEqual(0f, ResourceRingMath.StartDegrees + ResourceRingMath.SweepDegrees);
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

        // ===== per-segment progress =====
        // The one primitive both animations are drawn from. Ring r's segment
        // s spans values r*10+s .. r*10+s+1.

        [TestCase(0f, 0, 0, 0f)]
        [TestCase(0.3f, 0, 0, 0.3f)]
        [TestCase(1f, 0, 0, 1f)]
        [TestCase(4.3f, 0, 3, 1f)]
        [TestCase(4.3f, 0, 4, 0.3f)]
        [TestCase(4.3f, 0, 5, 0f)]
        [TestCase(4.3f, 1, 0, 0f)]
        [TestCase(14.5f, 0, 9, 1f)]
        [TestCase(14.5f, 1, 4, 0.5f)]
        [TestCase(14.5f, 1, 5, 0f)]
        [TestCase(25f, 2, 4, 1f)]
        [TestCase(25f, 2, 5, 0f)]
        public void SegmentFraction_fills_one_segment_at_a_time(
            float value, int ring, int segment, float expected)
        {
            Assert.AreEqual(expected, ResourceRingMath.SegmentFraction(value, ring, segment), 1e-5f);
        }

        [Test]
        public void SegmentFraction_stays_inside_zero_to_one_everywhere()
        {
            for (float value = -3f; value <= 40f; value += 0.25f)
            {
                for (int ring = 0; ring < ResourceRingMath.RingCount; ring++)
                {
                    for (int segment = 0; segment < ResourceRingMath.SegmentsPerRing; segment++)
                    {
                        float fraction = ResourceRingMath.SegmentFraction(value, ring, segment);
                        Assert.GreaterOrEqual(fraction, 0f);
                        Assert.LessOrEqual(fraction, 1f);
                    }
                }
            }
        }

        // A whole value has to agree with LitSegments, or the ghost's leading
        // edge and the lit arc would part company at rest.
        [TestCase(0)]
        [TestCase(7)]
        [TestCase(10)]
        [TestCase(16)]
        [TestCase(30)]
        [TestCase(45)]
        public void SegmentFraction_agrees_with_LitSegments_on_whole_values(int value)
        {
            for (int ring = 0; ring < ResourceRingMath.RingCount; ring++)
            {
                int lit = ResourceRingMath.LitSegments(value, ring);
                for (int segment = 0; segment < ResourceRingMath.SegmentsPerRing; segment++)
                {
                    float expected = segment < lit ? 1f : 0f;
                    Assert.AreEqual(expected, ResourceRingMath.SegmentFraction(value, ring, segment), 1e-5f,
                        "value " + value + " ring " + ring + " segment " + segment);
                }
            }
        }

        // ===== the spend ghost's catch-up curve =====

        [Test]
        public void GhostFraction_has_not_moved_at_the_spend()
        {
            Assert.AreEqual(0f, ResourceRingMath.GhostFraction(0f));
        }

        [TestCase(-1f)]
        [TestCase(-0.01f)]
        public void GhostFraction_clamps_to_zero_before_the_spend(float elapsed)
        {
            Assert.AreEqual(0f, ResourceRingMath.GhostFraction(elapsed));
        }

        [Test]
        public void GhostFraction_has_barely_crept_by_the_end_of_the_hold()
        {
            float atHold = ResourceRingMath.GhostFraction(ResourceRingMath.GhostHoldSeconds);
            Assert.AreEqual(ResourceRingMath.GhostHoldFraction, atHold, 1e-5f);
        }

        [Test]
        public void GhostFraction_is_caught_up_at_the_end_of_the_curve()
        {
            Assert.AreEqual(1f, ResourceRingMath.GhostFraction(ResourceRingMath.GhostTotalSeconds), 1e-5f);
            Assert.AreEqual(1f, ResourceRingMath.GhostFraction(5f));
        }

        // Fast to slow: the first half of the catch-up covers more ground
        // than the second. This is the shape, not an incidental value.
        [Test]
        public void GhostFraction_decelerates_across_the_catch_up()
        {
            float start = ResourceRingMath.GhostFraction(ResourceRingMath.GhostHoldSeconds);
            float middle = ResourceRingMath.GhostFraction(
                ResourceRingMath.GhostHoldSeconds + ResourceRingMath.GhostCatchSeconds * 0.5f);

            float firstHalf = middle - start;
            float secondHalf = 1f - middle;

            Assert.Greater(firstHalf, secondHalf);
        }

        [Test]
        public void GhostFraction_never_goes_backwards()
        {
            float previous = -1f;
            for (float elapsed = 0f; elapsed <= ResourceRingMath.GhostTotalSeconds + 0.2f; elapsed += 0.01f)
            {
                float fraction = ResourceRingMath.GhostFraction(elapsed);
                Assert.GreaterOrEqual(fraction, previous, "at " + elapsed);
                Assert.LessOrEqual(fraction, 1f);
                previous = fraction;
            }
        }

        // The hold is the point: a spend has to sit still long enough to be
        // read before the white starts moving.
        [Test]
        public void GhostFraction_leaves_most_of_the_gap_standing_through_the_hold()
        {
            Assert.Less(ResourceRingMath.GhostFraction(ResourceRingMath.GhostHoldSeconds * 0.99f), 0.1f);
        }

        // ===== the gain sweep =====

        [TestCase(1f, ResourceRingMath.FillSecondsMin)]
        [TestCase(0f, ResourceRingMath.FillSecondsMin)]
        [TestCase(3f, 0.33f)]
        [TestCase(20f, ResourceRingMath.FillSecondsMax)]
        public void FillSeconds_scales_with_the_gain_between_its_two_clamps(float units, float expected)
        {
            Assert.AreEqual(expected, ResourceRingMath.FillSeconds(units), 1e-5f);
        }

        [Test]
        public void FillEase_runs_zero_to_one_and_decelerates()
        {
            Assert.AreEqual(0f, ResourceRingMath.FillEase(0f));
            Assert.AreEqual(1f, ResourceRingMath.FillEase(1f));
            Assert.AreEqual(1f, ResourceRingMath.FillEase(1.5f));
            Assert.AreEqual(0f, ResourceRingMath.FillEase(-1f));
            Assert.Greater(ResourceRingMath.FillEase(0.5f), 0.5f);
        }
    }
}
