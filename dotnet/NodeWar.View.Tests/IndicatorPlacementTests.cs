using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class IndicatorPlacementTests
    {
        // A 400 x 600 board rect starting at (0, 100), so the centre is (200, 400).
        private static readonly PlacementRect Board = new PlacementRect(0f, 100f, 400f, 700f);

        private const float Enter = 0.6f;
        private const float Exit = 0.66f;

        // ===== zones =====

        [Test]
        public void The_centre_is_inner()
        {
            Assert.AreEqual(IndicatorZone.Inner,
                IndicatorPlacement.Classify(200f, 400f, false, Board, Enter, Exit, IndicatorZone.Edge));
        }

        [Test]
        public void Past_sixty_percent_is_edge_even_though_it_is_on_screen()
        {
            // 60% of 400 wide is 240, so the inner box runs x 80..320.
            Assert.AreEqual(IndicatorZone.Edge,
                IndicatorPlacement.Classify(340f, 400f, false, Board, Enter, Exit, IndicatorZone.Edge));
        }

        [Test]
        public void Between_the_two_boxes_an_indicator_keeps_the_form_it_had()
        {
            // x 325: outside the 60% box (80..320), inside the 66% box (68..332).
            Assert.AreEqual(IndicatorZone.Inner,
                IndicatorPlacement.Classify(325f, 400f, false, Board, Enter, Exit, IndicatorZone.Inner));
            Assert.AreEqual(IndicatorZone.Edge,
                IndicatorPlacement.Classify(325f, 400f, false, Board, Enter, Exit, IndicatorZone.Edge));
        }

        [Test]
        public void Leaving_the_larger_box_ends_inner()
        {
            Assert.AreEqual(IndicatorZone.Edge,
                IndicatorPlacement.Classify(335f, 400f, false, Board, Enter, Exit, IndicatorZone.Inner));
        }

        [Test]
        public void Vertical_uses_the_rect_height_not_its_width()
        {
            // 60% of 600 tall is 360, so the inner box runs y 220..580.
            Assert.AreEqual(IndicatorZone.Inner,
                IndicatorPlacement.Classify(200f, 570f, false, Board, Enter, Exit, IndicatorZone.Edge));
            Assert.AreEqual(IndicatorZone.Edge,
                IndicatorPlacement.Classify(200f, 590f, false, Board, Enter, Exit, IndicatorZone.Edge));
        }

        [Test]
        public void Behind_the_camera_is_always_edge()
        {
            Assert.AreEqual(IndicatorZone.Edge,
                IndicatorPlacement.Classify(200f, 400f, true, Board, Enter, Exit, IndicatorZone.Inner));
        }

        // ===== edge position =====

        [Test]
        public void A_subject_inside_the_rect_is_not_moved_and_needs_no_arrow()
        {
            IndicatorPlacement.EdgePosition(350f, 150f, false, Board, 20f,
                out float px, out float py, out bool clamped, out _);

            Assert.AreEqual(350f, px);
            Assert.AreEqual(150f, py);
            Assert.IsFalse(clamped);
        }

        [Test]
        public void Off_the_right_edge_clamps_in_and_points_right()
        {
            IndicatorPlacement.EdgePosition(900f, 400f, false, Board, 20f,
                out float px, out float py, out bool clamped, out float angle);

            Assert.AreEqual(380f, px);
            Assert.AreEqual(400f, py);
            Assert.IsTrue(clamped);
            Assert.AreEqual(0f, angle, 0.001f);
        }

        [Test]
        public void Off_a_corner_clamps_into_the_corner_and_points_at_the_subject()
        {
            // Down and to the left, equally far out: the arrow points down-left.
            IndicatorPlacement.EdgePosition(-100f, 900f, false, Board, 20f,
                out float px, out float py, out bool clamped, out float angle);

            Assert.AreEqual(20f, px);
            Assert.AreEqual(680f, py);
            Assert.IsTrue(clamped);
            // Down is +90 in a y-down panel, so down-left is between 90 and 180.
            Assert.Greater(angle, 90f);
            Assert.Less(angle, 180f);
        }

        [Test]
        public void Behind_the_camera_is_flipped_through_the_centre()
        {
            // Projected up and to the left of centre while behind the camera,
            // so it is really down and to the right.
            IndicatorPlacement.EdgePosition(100f, 300f, true, Board, 20f,
                out float px, out float py, out bool clamped, out _);

            Assert.IsTrue(clamped);
            Assert.Greater(px, Board.CentreX);
            Assert.Greater(py, Board.CentreY);
        }

        [Test]
        public void Behind_the_camera_at_the_centre_goes_to_the_bottom()
        {
            IndicatorPlacement.EdgePosition(200f, 400f, true, Board, 20f,
                out float px, out float py, out _, out _);

            Assert.AreEqual(200f, px);
            Assert.AreEqual(680f, py);
        }

        [Test]
        public void A_rect_smaller_than_the_margin_collapses_to_its_centre()
        {
            PlacementRect tiny = new PlacementRect(0f, 0f, 30f, 30f);

            IndicatorPlacement.EdgePosition(500f, 500f, false, tiny, 20f,
                out float px, out float py, out _, out _);

            Assert.AreEqual(15f, px);
            Assert.AreEqual(15f, py);
        }

        // ===== clustering =====

        private static int[] Run(float[] xs, float[] ys, int[] priority, float radius, int maxShown, out int leaders)
        {
            int[] order = new int[xs.Length];
            int[] leaderOf = new int[xs.Length];
            leaders = IndicatorPlacement.Cluster(xs, ys, priority, xs.Length, radius, maxShown, order, leaderOf);
            return leaderOf;
        }

        [Test]
        public void Two_close_indicators_merge_into_the_more_important_one()
        {
            // Index 0 is low priority, index 1 high; they sit 10 apart.
            int[] leaderOf = Run(new[] { 100f, 110f }, new[] { 100f, 100f }, new[] { 0, 2 }, 48f, 6, out int leaders);

            Assert.AreEqual(1, leaders);
            Assert.AreEqual(1, leaderOf[1], "the high-priority one leads");
            Assert.AreEqual(1, leaderOf[0], "the low-priority one merges into it");
        }

        [Test]
        public void Indicators_far_apart_both_lead()
        {
            int[] leaderOf = Run(new[] { 0f, 300f }, new[] { 0f, 0f }, new[] { 1, 1 }, 48f, 6, out int leaders);

            Assert.AreEqual(2, leaders);
            Assert.AreEqual(0, leaderOf[0]);
            Assert.AreEqual(1, leaderOf[1]);
        }

        [Test]
        public void Past_the_cap_the_least_important_is_dropped()
        {
            int[] leaderOf = Run(new[] { 0f, 100f, 200f }, new[] { 0f, 0f, 0f }, new[] { 0, 2, 1 }, 48f, 2, out int leaders);

            Assert.AreEqual(2, leaders);
            Assert.AreEqual(-1, leaderOf[0], "lowest priority dropped");
            Assert.AreEqual(1, leaderOf[1]);
            Assert.AreEqual(2, leaderOf[2]);
        }

        [Test]
        public void Equal_priority_ties_go_to_the_lower_index()
        {
            int[] leaderOf = Run(new[] { 100f, 105f }, new[] { 100f, 100f }, new[] { 1, 1 }, 48f, 6, out _);

            Assert.AreEqual(0, leaderOf[0]);
            Assert.AreEqual(0, leaderOf[1]);
        }
    }
}
