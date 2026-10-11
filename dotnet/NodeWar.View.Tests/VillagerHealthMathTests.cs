using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class VillagerHealthMathTests
    {
        [TestCase(0, 100, 0f)]
        [TestCase(100, 100, 1f)]
        [TestCase(25, 100, 0.25f)]
        [TestCase(-1, 100, 0f)]
        [TestCase(150, 100, 1f)]
        [TestCase(10, 0, 0f)]
        [TestCase(10, -10, 0f)]
        [TestCase(int.MaxValue, int.MaxValue, 1f)]
        public void FractionClampsHealth(int hp, int maxHP, float expected)
            => Assert.AreEqual(expected, VillagerHealthMath.Fraction(hp, maxHP));

        [TestCase(50, 100, true, false, false, true)]
        [TestCase(100, 100, true, false, false, false)]
        [TestCase(150, 100, true, false, false, false)]
        [TestCase(100, 100, true, false, true, true)]
        [TestCase(50, 100, false, false, true, false)]
        [TestCase(50, 100, true, true, true, false)]
        [TestCase(50, 100, false, false, false, false)]
        [TestCase(50, 100, true, true, false, false)]
        [TestCase(0, 100, true, false, false, false)]
        [TestCase(-1, 100, true, false, true, false)]
        [TestCase(10, 0, true, false, true, false)]
        [TestCase(10, -10, true, false, false, false)]
        public void VisibilityRespectsPolicyAndLife(int hp, int maxHP, bool alive,
            bool consumed, bool alwaysVisible, bool expected)
            => Assert.AreEqual(expected, VillagerHealthMath.Visible(hp, maxHP, alive, consumed, alwaysVisible));

        [Test]
        public void HealingAndRespawnHaveNoVisibilityTimer()
        {
            Assert.IsTrue(VillagerHealthMath.Visible(50, 100, true, false, false));
            Assert.IsTrue(VillagerHealthMath.Visible(99, 100, true, false, false));
            Assert.IsFalse(VillagerHealthMath.Visible(100, 100, true, false, false));
            Assert.IsFalse(VillagerHealthMath.Visible(0, 100, false, false, false));
            Assert.IsFalse(VillagerHealthMath.Visible(100, 100, true, false, false));
            Assert.IsTrue(VillagerHealthMath.Visible(90, 100, true, false, false));
        }

        [TestCase(1f, 0f)]
        [TestCase(1.35f, 20f)]
        [TestCase(1.35f, 89f)]
        public void LayoutScalesWithFinalSpriteBounds(float multiplier, float pitch)
        {
            float width = VillagerLayoutMath.ScaledSize(2f, multiplier);
            float height = VillagerTiltMath.VerticalExtent(VillagerLayoutMath.ScaledSize(3f, multiplier), pitch);
            var layout = VillagerHealthMath.Layout(width, height, 4f);
            Assert.AreEqual(width * 0.65f, layout.Width, 0.00001f);
            Assert.AreEqual(height * 0.08f, layout.Height, 0.00001f);
            Assert.AreEqual(4f + height * 0.10f, layout.BottomY, 0.00001f);
        }
    }
}
