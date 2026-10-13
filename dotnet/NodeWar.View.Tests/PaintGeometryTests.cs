using System;
using System.Collections.Generic;
using NodeWar.Input;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class PaintGeometryTests
    {
        private static GesturePoint P(float x, float y) => new GesturePoint(x, y);

        [Test]
        public void SegmentDistanceClampsToEndsAndHandlesRepeatedPoints()
        {
            Assert.AreEqual(9f, PaintGeometry.DistanceSquaredToSegment(P(5, 3), P(0, 0), P(10, 0)));
            Assert.AreEqual(25f, PaintGeometry.DistanceSquaredToSegment(P(-3, 4), P(0, 0), P(10, 0)));
            Assert.AreEqual(25f, PaintGeometry.DistanceSquaredToSegment(P(13, 4), P(0, 0), P(10, 0)));
            Assert.AreEqual(25f, PaintGeometry.DistanceSquaredToSegment(P(3, 4), P(0, 0), P(0, 0)));
        }

        [Test]
        public void SinglePointIsCircleWithInclusiveBoundary()
        {
            var stroke = new[] { P(0, 0) };
            Assert.IsTrue(PaintGeometry.Contains(stroke, P(0, 0), 5));
            Assert.IsTrue(PaintGeometry.Contains(stroke, P(3, 4), 5));
            Assert.IsFalse(PaintGeometry.Contains(stroke, P(5.001f, 0), 5));
            Assert.IsFalse(PaintGeometry.Contains(Array.Empty<GesturePoint>(), P(0, 0), 5));
            Assert.IsFalse(PaintGeometry.Bounds(Array.Empty<GesturePoint>(), 5).Contains(P(0, 0)));
            Assert.IsFalse(default(PaintBounds).Contains(P(0, 0)));
        }

        [Test]
        public void SweptCapsulesCoverBetweenSparseSamples()
        {
            var stroke = new[] { P(0, 0), P(1000, 0) };
            foreach (float x in new[] { 0f, 250f, 500f, 750f, 1000f })
                Assert.IsTrue(PaintGeometry.Contains(stroke, P(x, 5), 5));
            Assert.IsTrue(PaintGeometry.Contains(stroke, P(-5, 0), 5));
            Assert.IsTrue(PaintGeometry.Contains(stroke, P(1005, 0), 5));
            Assert.IsFalse(PaintGeometry.Contains(stroke, P(500, 5.001f), 5));
            var bounds = PaintGeometry.Bounds(stroke, 5);
            Assert.IsTrue(bounds.Contains(P(-5, -5)));
            Assert.IsTrue(bounds.Contains(P(1005, 5)));
            Assert.IsFalse(bounds.Contains(P(1005.001f, 0)));
        }

        [Test]
        public void OpenStrokeDoesNotCloseOrSelectLoopInterior()
        {
            var u = new[] { P(0, 10), P(0, 0), P(10, 0), P(10, 10) };
            Assert.IsFalse(PaintGeometry.Contains(u, P(5, 10), 1));
            Assert.IsFalse(PaintGeometry.Contains(u, P(5, 5), 1));
            var retrace = new[] { P(0, 0), P(10, 0), P(0, 0), P(10, 0) };
            Assert.IsTrue(PaintGeometry.Contains(retrace, P(5, 1), 1));
            var eight = new[] { P(-10, -10), P(10, 10), P(-10, 10), P(10, -10), P(-10, -10) };
            Assert.IsTrue(PaintGeometry.Contains(eight, P(0, 0), 1));
            Assert.IsTrue(PaintGeometry.Contains(eight, P(5, 5), 1));
            Assert.IsFalse(PaintGeometry.Contains(eight, P(0, 5), 1));
        }

        [Test]
        public void InvalidNumbersAndRadiusDoNotSelect()
        {
            var stroke = new[] { P(0, 0), P(10, 0) };
            foreach (float radius in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(PaintGeometry.Contains(stroke, P(0, 0), radius));
                Assert.IsFalse(PaintGeometry.Bounds(stroke, radius).Contains(P(0, 0)));
            }
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                foreach (var p in new[] { P(invalid, 0), P(0, invalid) })
                {
                    // A later invalid sample must invalidate even an earlier hit.
                    var broken = new[] { P(0, 0), P(10, 0), p };
                    Assert.IsFalse(PaintGeometry.Contains(broken, P(0, 0), 5));
                    Assert.IsFalse(PaintGeometry.Bounds(broken, 5).Contains(P(0, 0)));
                    Assert.IsFalse(PaintGeometry.Contains(stroke, p, 5));
                    Assert.IsFalse(PaintGeometry.Bounds(stroke, 5).Contains(p));
                    Assert.IsTrue(float.IsPositiveInfinity(PaintGeometry.DistanceSquaredToSegment(p, P(0, 0), P(10, 0))));
                    Assert.IsTrue(float.IsPositiveInfinity(PaintGeometry.DistanceSquaredToSegment(P(0, 0), p, P(10, 0))));
                    Assert.IsTrue(float.IsPositiveInfinity(PaintGeometry.DistanceSquaredToSegment(P(0, 0), P(10, 0), p)));
                }
            }
            Assert.IsFalse(PaintGeometry.Contains(null, P(0, 0), 5));
            Assert.IsFalse(PaintGeometry.Bounds(null, 5).Contains(P(0, 0)));
            var huge = new[] { P(-float.MaxValue, 0), P(float.MaxValue, 0) };
            Assert.IsTrue(PaintGeometry.Contains(huge, P(0, 5), 5));
            Assert.IsFalse(PaintGeometry.Contains(new[] { P(-float.MaxValue, 0) }, P(float.MaxValue, 0), float.MaxValue));
        }

        [TestCase(160f)]
        [TestCase(440f)]
        public void UnitScalingPreservesMembership(float dpi)
        {
            const float radiusMm = 5f;
            float scale = dpi / 25.4f;
            var stroke = new[] { P(0, 0), P(20, 0), P(20, 20) };
            var pixels = Array.ConvertAll(stroke, p => P(p.X * scale, p.Y * scale));
            foreach (var p in new[] { P(0, 0), P(10, 4), P(10, 6), P(16, 10), P(14, 10), P(10, 10) })
                Assert.AreEqual(PaintGeometry.Contains(stroke, p, radiusMm),
                    PaintGeometry.Contains(pixels, P(p.X * scale, p.Y * scale), radiusMm * scale));
            var capped = new List<GesturePoint>();
            for (int i = 0; i < 256; i++) capped.Add(P(0, 0));
            Assert.IsTrue(PaintGeometry.Contains(capped, P(3, 4), radiusMm));
            Assert.IsFalse(PaintGeometry.Contains(capped, P(10, 0), radiusMm));
            Assert.IsFalse(PaintGeometry.Bounds(capped, radiusMm).Contains(P(10, 0)));
        }
    }
}
