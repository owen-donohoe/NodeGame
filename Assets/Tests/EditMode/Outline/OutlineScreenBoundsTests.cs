using NodeWar.View.Outline;
using NUnit.Framework;
using UnityEngine;

namespace NodeWar.Tests
{
    /// <summary>
    /// The rectangle these cases pin is what the composite pass scissors to, so
    /// a rect that comes out too small does not look like a maths bug -- it
    /// looks like an outline with a straight bite taken out of one side, which
    /// is very easy to blame on the shader instead.
    ///
    /// No camera and no scene: the projection is a hand-built matrix, which is
    /// the whole reason OutlineScreenBounds is a static helper rather than
    /// living inside the render pass.
    /// </summary>
    public class OutlineScreenBoundsTests
    {
        private const int TargetWidth = 200;
        private const int TargetHeight = 200;

        // World x and y in -10..10 fill the target exactly, so a world unit is
        // ten pixels and every expectation below is checkable by hand. w stays
        // 1 under an orthographic projection, which keeps the near-plane path
        // out of the way of the cases that are not about it.
        private static Matrix4x4 Ortho => Matrix4x4.Ortho(-10f, 10f, -10f, 10f, 0.1f, 100f);

        private static Bounds Box(float x, float y, float size) =>
            new Bounds(new Vector3(x, y, 0f), new Vector3(size, size, 0f));

        private static void AssertRect(Rect actual, float x, float y, float width, float height)
        {
            Assert.AreEqual(x, actual.xMin, 0.001f, "xMin");
            Assert.AreEqual(y, actual.yMin, 0.001f, "yMin");
            Assert.AreEqual(width, actual.width, 0.001f, "width");
            Assert.AreEqual(height, actual.height, 0.001f, "height");
        }

        [Test]
        public void TryProject_BoxOnScreen_GivesThePixelRectItCovers()
        {
            Rect union = OutlineScreenBounds.Empty;

            bool reliable = OutlineScreenBounds.TryProject(
                Ortho, Box(0f, 0f, 4f), TargetWidth, TargetHeight, ref union);

            Assert.IsTrue(reliable);

            // World -2..2 on both axes, ten pixels to the unit, centred.
            AssertRect(union, 80f, 80f, 40f, 40f);
        }

        [Test]
        public void TryProject_BoxOverTheEdge_IsClampedNotWrapped()
        {
            Rect union = OutlineScreenBounds.Empty;

            // World x 7..11. The right half is past the edge of the target.
            OutlineScreenBounds.TryProject(
                Ortho, Box(9f, 0f, 4f), TargetWidth, TargetHeight, ref union);

            AssertRect(union, 170f, 80f, 30f, 40f);
        }

        [Test]
        public void TryProject_BoxEntirelyOffScreen_ContributesNothingAndStaysReliable()
        {
            Rect union = OutlineScreenBounds.Empty;

            bool reliable = OutlineScreenBounds.TryProject(
                Ortho, Box(100f, 0f, 2f), TargetWidth, TargetHeight, ref union);

            // Off screen is an answer, not a failure. Reporting it as unreliable
            // would make one off-screen group throw away the tight rect every
            // other group had already contributed.
            Assert.IsTrue(reliable);
            Assert.IsTrue(OutlineScreenBounds.IsEmpty(union));
        }

        [Test]
        public void TryProject_BoxBehindTheCamera_IsReported()
        {
            // Unity's perspective matrix assumes a view space looking down -Z,
            // so positive z is behind the camera and w comes out negative.
            Matrix4x4 perspective = Matrix4x4.Perspective(60f, 1f, 0.1f, 100f);

            Rect union = OutlineScreenBounds.Empty;

            bool reliable = OutlineScreenBounds.TryProject(
                perspective, new Bounds(new Vector3(0f, 0f, 5f), Vector3.one),
                TargetWidth, TargetHeight, ref union);

            Assert.IsFalse(reliable);
        }

        [Test]
        public void TryProject_BoxInFrontOfTheCamera_IsNotReportedAsBehind()
        {
            Matrix4x4 perspective = Matrix4x4.Perspective(60f, 1f, 0.1f, 100f);

            Rect union = OutlineScreenBounds.Empty;

            bool reliable = OutlineScreenBounds.TryProject(
                perspective, new Bounds(new Vector3(0f, 0f, -5f), Vector3.one),
                TargetWidth, TargetHeight, ref union);

            Assert.IsTrue(reliable);
            Assert.IsFalse(OutlineScreenBounds.IsEmpty(union));
        }

        [Test]
        public void TryProject_TwoDisjointBoxes_UnionCoversBoth()
        {
            Rect union = OutlineScreenBounds.Empty;

            OutlineScreenBounds.TryProject(
                Ortho, Box(-5f, -5f, 2f), TargetWidth, TargetHeight, ref union);
            OutlineScreenBounds.TryProject(
                Ortho, Box(5f, 5f, 2f), TargetWidth, TargetHeight, ref union);

            // 40..60 and 140..160 on each axis.
            AssertRect(union, 40f, 40f, 120f, 120f);
        }

        [Test]
        public void Combine_AbsorbsAnEmptyOperand_RatherThanTreatingItAsTheOrigin()
        {
            Rect real = new Rect(80f, 80f, 40f, 40f);

            AssertRect(OutlineScreenBounds.Combine(OutlineScreenBounds.Empty, real),
                       80f, 80f, 40f, 40f);
            AssertRect(OutlineScreenBounds.Combine(real, OutlineScreenBounds.Empty),
                       80f, 80f, 40f, 40f);
        }

        [Test]
        public void Expand_GrowsOnEverySide()
        {
            Rect expanded = OutlineScreenBounds.Expand(
                new Rect(80f, 80f, 40f, 40f), 5f, TargetWidth, TargetHeight);

            AssertRect(expanded, 75f, 75f, 50f, 50f);
        }

        [Test]
        public void Expand_ClampsToTheTargetOnAllFourEdges()
        {
            Rect expanded = OutlineScreenBounds.Expand(
                new Rect(0f, 0f, TargetWidth, TargetHeight), 5f, TargetWidth, TargetHeight);

            AssertRect(expanded, 0f, 0f, TargetWidth, TargetHeight);
        }

        [Test]
        public void Expand_SnapsOutwardsToWholePixels()
        {
            // Rounding to nearest here would lose a fraction of a pixel at the
            // edge, and the GPU scissor is integral either way -- so the loss
            // would show up as a line that is thinner on one side.
            Rect expanded = OutlineScreenBounds.Expand(
                Rect.MinMaxRect(80.4f, 80.6f, 120.2f, 120.9f), 0f, TargetWidth, TargetHeight);

            AssertRect(expanded, 80f, 80f, 41f, 41f);
        }

        [Test]
        public void Expand_OfAnEmptyRect_StaysEmpty()
        {
            Rect expanded = OutlineScreenBounds.Expand(
                OutlineScreenBounds.Empty, 8f, TargetWidth, TargetHeight);

            Assert.IsTrue(OutlineScreenBounds.IsEmpty(expanded));
        }

        [Test]
        public void FullTarget_CoversTheWholeTarget()
        {
            AssertRect(OutlineScreenBounds.FullTarget(TargetWidth, TargetHeight),
                       0f, 0f, TargetWidth, TargetHeight);
        }
    }
}
