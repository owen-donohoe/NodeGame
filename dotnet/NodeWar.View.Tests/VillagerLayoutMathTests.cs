using System;
using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class VillagerLayoutMathTests
    {
        [TestCase(1f, 0.5f, 0.8f)]
        [TestCase(1.35f, 0.675f, 1.08f)]
        public void DimensionsScaleFromAuthoredSize(float multiplier, float width, float height)
        {
            Assert.That(VillagerLayoutMath.ScaledSize(0.5f, multiplier), Is.EqualTo(width).Within(0.00001f));
            Assert.That(VillagerLayoutMath.ScaledSize(0.8f, multiplier), Is.EqualTo(height).Within(0.00001f));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void EmptyOrSingleGroupsUseCenter(int count)
        {
            var slot = VillagerLayoutMath.Slot(0, count, 0.675f, 1.5f);
            Assert.AreEqual(0f, slot.X);
            Assert.AreEqual(0f, slot.Z);
            Assert.IsFalse(slot.Overflow);
        }

        [TestCase(2, 1.5f)]
        [TestCase(5, 1.5f)]
        [TestCase(18, 1.5f)]
        [TestCase(2, 0.5f)]
        public void SlotsKeepDesiredPairwiseSpacingWhenCapacityAllows(int count, float radius)
        {
            const float spacing = 0.675f;
            for (int i = 0; i < count; i++)
            {
                var a = VillagerLayoutMath.Slot(i, count, spacing, radius);
                Assert.IsFalse(a.Overflow);
                for (int j = 0; j < i; j++)
                {
                    var b = VillagerLayoutMath.Slot(j, count, spacing, radius);
                    double dx = a.X - b.X, dz = a.Z - b.Z;
                    Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(spacing - 0.00001));
                }
            }
        }

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(1000)]
        public void CrowdsRemainBoundedAndFinite(int count)
        {
            bool overflow = false;
            for (int i = 0; i < count; i++)
            {
                var p = VillagerLayoutMath.Slot(i, count, 0.675f, 0.75f);
                Assert.IsFalse(float.IsNaN(p.X) || float.IsInfinity(p.X) || float.IsNaN(p.Z) || float.IsInfinity(p.Z));
                Assert.That(Math.Sqrt(p.X * p.X + p.Z * p.Z), Is.LessThanOrEqualTo(0.75001));
                overflow |= p.Overflow;
            }
            Assert.AreEqual(count > 7, overflow);
        }

        [Test]
        public void ExistingIndicesStayPutAcrossArrivalsAndOverflowRepeatsSlots()
        {
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(VillagerLayoutMath.Slot(i, 5, 0.675f, 1.5f), VillagerLayoutMath.Slot(i, 50, 0.675f, 1.5f));
            var first = VillagerLayoutMath.Slot(0, 100, 1f, 1f);
            var repeated = VillagerLayoutMath.Slot(7, 100, 1f, 1f);
            Assert.AreEqual(first.X, repeated.X);
            Assert.AreEqual(first.Z, repeated.Z);
            Assert.IsTrue(repeated.Overflow);
        }

        [TestCase(0f, 0f)]
        [TestCase(-1f, -1f)]
        [TestCase(float.NaN, float.PositiveInfinity)]
        public void InvalidLayoutInputsYieldFiniteCenter(float spacing, float radius)
        {
            var p = VillagerLayoutMath.Slot(10, 20, spacing, radius);
            Assert.AreEqual(0f, p.X);
            Assert.AreEqual(0f, p.Z);
            Assert.IsTrue(p.Overflow);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidMultiplierPreservesAuthoredSize(float multiplier)
        {
            Assert.AreEqual(0.5f, VillagerLayoutMath.ScaledSize(0.5f, multiplier));
        }

        [Test]
        public void FallbackWorkSpacingUsesFinalSizeWithAuthoredMinimum()
        {
            Assert.AreEqual(1f, VillagerLayoutMath.WorkSpacing(1f, 0.675f, 1f));
            Assert.AreEqual(1.35f, VillagerLayoutMath.WorkSpacing(1f, VillagerLayoutMath.ScaledSize(1f, 1.35f), 1f));
        }
    }
}
