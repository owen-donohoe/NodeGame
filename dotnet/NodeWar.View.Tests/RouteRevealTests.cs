using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class RouteRevealTests
    {
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void Contains_only_the_next_revealed_nodes(int node, bool expected)
        {
            Assert.AreEqual(expected, RouteReveal.Contains(new[] { 0, 1, 2, 3 }, 0, 2, node));
        }

        [Test]
        public void Window_moves_with_the_path_index_and_clips_at_the_end()
        {
            int[] path = { 0, 1, 2, 3 };
            Assert.IsFalse(RouteReveal.Contains(path, 2, 5, 2));
            Assert.IsTrue(RouteReveal.Contains(path, 2, 5, 3));
            Assert.AreEqual(1, RouteReveal.LegCount(path, 2, int.MaxValue));
        }

        [Test]
        public void Missing_exhausted_or_invalid_paths_reveal_nothing()
        {
            Assert.IsFalse(RouteReveal.Contains(null, 0, 2, 0));
            Assert.IsFalse(RouteReveal.Contains(new int[0], 0, 2, 0));
            Assert.IsFalse(RouteReveal.Contains(new[] { 0 }, 0, 2, 0));
            Assert.IsFalse(RouteReveal.Contains(new[] { 0, 1 }, -1, 2, 0));
            Assert.IsFalse(RouteReveal.Contains(new[] { 0, 1 }, 2, 2, 0));
            Assert.IsFalse(RouteReveal.Contains(new[] { 0, 1 }, 0, 0, 1));
        }

        [Test]
        public void Hidden_tails_do_not_split_a_squad()
        {
            Assert.IsTrue(RouteReveal.SameWindow(new[] { 0, 1, 2, 3 }, 0,
                                                new[] { 0, 1, 2, 4, 5 }, 0, 2));
        }

        [Test]
        public void Different_path_indices_can_reveal_the_same_window()
        {
            Assert.IsTrue(RouteReveal.SameWindow(new[] { 0, 1, 2 }, 0,
                                                new[] { 9, 8, 1, 2 }, 1, 2));
        }

        [Test]
        public void Window_order_and_clipped_length_both_matter()
        {
            Assert.IsFalse(RouteReveal.SameWindow(new[] { 0, 1, 2 }, 0, new[] { 0, 2, 1 }, 0, 2));
            Assert.IsFalse(RouteReveal.SameWindow(new[] { 0, 1, 2 }, 0, new[] { 0, 1 }, 0, 2));
        }

        [Test]
        public void Lines_still_compare_the_starting_node()
        {
            int[] a = { 0, 1, 2 };
            int[] b = { 9, 1, 2 };
            Assert.IsTrue(RouteReveal.SameWindow(a, 0, b, 0, 2));
            Assert.IsFalse(RouteReveal.SameRoute(a, 0, b, 0, 3));
        }

        [Test]
        public void Line_comparison_clips_both_paths_and_ignores_their_prefixes()
        {
            Assert.IsTrue(RouteReveal.SameRoute(new[] { 9, 0, 1, 2, 3 }, 1,
                                               new[] { 0, 1, 2, 4 }, 0, 3));
            Assert.IsFalse(RouteReveal.SameRoute(new[] { 0, 1 }, 0, new[] { 0, 1, 2 }, 0, 3));
        }

        [Test]
        public void Own_lines_compare_the_entire_remaining_route()
        {
            Assert.IsTrue(RouteReveal.SameRoute(new[] { 9, 0, 1, 2 }, 1,
                                               new[] { 0, 1, 2 }, 0, int.MaxValue));
            Assert.IsFalse(RouteReveal.SameRoute(new[] { 0, 1, 2 }, 0,
                                                new[] { 0, 1, 3 }, 0, int.MaxValue));
        }
    }
}
