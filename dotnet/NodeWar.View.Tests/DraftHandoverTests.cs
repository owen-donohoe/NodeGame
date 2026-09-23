using NUnit.Framework;
using NodeWar.UI;

namespace NodeWar.View.Tests
{
    /// <summary>
    /// The card-to-board cross-fade. The property that matters is not either
    /// curve on its own but the overlap between them: the board must have the
    /// piece before the card lets go of it, in both directions.
    /// </summary>
    public class DraftHandoverTests
    {
        // ===== over the bar, the card still has it =====

        [TestCase(0f)]
        [TestCase(-20f)]
        [TestCase(-500f)]
        public void Over_the_bar_the_proxy_is_solid_and_the_ghost_is_absent(float aboveBar)
        {
            Assert.AreEqual(1f, DraftHandover.ProxyOpacity(aboveBar));
            Assert.AreEqual(0f, DraftHandover.GhostOpacity(aboveBar));
        }

        // ===== far over the board, the board has it =====

        [Test]
        public void Past_both_bands_the_ghost_is_solid_and_the_proxy_is_gone()
        {
            float far = DraftHandover.ProxyFadeDistance + 50f;

            Assert.AreEqual(0f, DraftHandover.ProxyOpacity(far));
            Assert.AreEqual(1f, DraftHandover.GhostOpacity(far));
        }

        [Test]
        public void Each_reaches_its_end_exactly_at_its_own_distance()
        {
            Assert.AreEqual(0f, DraftHandover.ProxyOpacity(DraftHandover.ProxyFadeDistance), 1e-5f);
            Assert.AreEqual(1f, DraftHandover.GhostOpacity(DraftHandover.GhostFadeDistance), 1e-5f);
        }

        // ===== the overlap, which is the whole point =====

        [Test]
        public void The_proxy_fades_slower_than_the_ghost_arrives()
        {
            Assert.Greater(DraftHandover.ProxyFadeDistance, DraftHandover.GhostFadeDistance);
        }

        [Test]
        public void The_ghost_is_solid_while_the_proxy_still_has_most_of_its_weight()
        {
            float atGhostFull = DraftHandover.ProxyOpacity(DraftHandover.GhostFadeDistance);

            Assert.AreEqual(1f, DraftHandover.GhostOpacity(DraftHandover.GhostFadeDistance), 1e-5f);
            Assert.Greater(atGhostFull, 0.5f);
        }

        // Neither can be absent at the same time as the other, at any distance.
        // That is the failure the overlap exists to prevent, and a band that
        // was retuned the wrong way would reintroduce it silently.
        [Test]
        public void The_piece_is_never_missing_from_both_at_once()
        {
            for (float d = -50f; d <= DraftHandover.ProxyFadeDistance + 50f; d += 0.5f)
            {
                float together = DraftHandover.ProxyOpacity(d) + DraftHandover.GhostOpacity(d);
                Assert.Greater(together, 0.5f, "at " + d);
            }
        }

        // ===== monotone, so a steady drag never sees anything jump back =====

        [Test]
        public void The_proxy_only_ever_thins_as_the_finger_rises()
        {
            float previous = 2f;
            for (float d = -20f; d <= DraftHandover.ProxyFadeDistance + 20f; d += 0.5f)
            {
                float now = DraftHandover.ProxyOpacity(d);
                Assert.LessOrEqual(now, previous, "at " + d);
                Assert.GreaterOrEqual(now, 0f);
                Assert.LessOrEqual(now, 1f);
                previous = now;
            }
        }

        [Test]
        public void The_ghost_only_ever_thickens_as_the_finger_rises()
        {
            float previous = -1f;
            for (float d = -20f; d <= DraftHandover.ProxyFadeDistance + 20f; d += 0.5f)
            {
                float now = DraftHandover.GhostOpacity(d);
                Assert.GreaterOrEqual(now, previous, "at " + d);
                Assert.GreaterOrEqual(now, 0f);
                Assert.LessOrEqual(now, 1f);
                previous = now;
            }
        }

        // ===== halfway points, so the shape is pinned and not just the ends =====

        [Test]
        public void Both_are_linear_across_their_bands()
        {
            Assert.AreEqual(0.5f, DraftHandover.ProxyOpacity(DraftHandover.ProxyFadeDistance * 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, DraftHandover.GhostOpacity(DraftHandover.GhostFadeDistance * 0.5f), 1e-5f);
        }
    }
}
