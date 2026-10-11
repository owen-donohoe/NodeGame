using System;
using NUnit.Framework;
using NodeWar.View;

namespace NodeWar.View.Tests
{
    public class VillagerTiltMathTests
    {
        [TestCase(0f, 0f)]
        [TestCase(20f, 20f)]
        [TestCase(-10f, 0f)]
        [TestCase(90f, 89f)]
        [TestCase(float.NaN, 20f)]
        [TestCase(float.PositiveInfinity, 89f)]
        [TestCase(float.NegativeInfinity, 0f)]
        public void PitchIsBounded(float input, float expected)
            => Assert.AreEqual(expected, VillagerTiltMath.Pitch(input));

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void UprightFacingPreservesEveryPovYaw(float yaw)
        {
            var facing = VillagerTiltMath.Facing(50f, yaw, 7f, true, VillagerTiltMath.DefaultPitch);
            Assert.AreEqual(20f, facing.Pitch);
            Assert.AreEqual(yaw, facing.Yaw);
            Assert.AreEqual(0f, facing.Roll);
        }

        [Test]
        public void CameraFacingModePreservesOriginalRotation()
        {
            var facing = VillagerTiltMath.Facing(50f, 180f, 7f, false, 20f);
            Assert.AreEqual(50f, facing.Pitch);
            Assert.AreEqual(180f, facing.Yaw);
            Assert.AreEqual(7f, facing.Roll);
        }

        [TestCase(0f)]
        [TestCase(20f)]
        [TestCase(89f)]
        public void ExtentsMatchSpritePlaneGeometry(float pitch)
        {
            Assert.AreEqual(2 * Math.Cos(pitch * Math.PI / 180), VillagerTiltMath.VerticalExtent(2f, pitch), 0.00001);
            Assert.AreEqual(2 * Math.Abs(Math.Cos((50 - pitch) * Math.PI / 180)), VillagerTiltMath.ProjectedHeight(2f, pitch, 50f), 0.00001);
            Assert.AreEqual(2f, VillagerTiltMath.ProjectedHeight(2f, pitch, pitch), 0.00001);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDimensionsYieldFiniteZero(float height)
        {
            Assert.AreEqual(0f, VillagerTiltMath.VerticalExtent(height, 20f));
            Assert.AreEqual(0f, VillagerTiltMath.ProjectedHeight(height, 20f, 50f));
        }

        [Test]
        public void TopDownCameraUsesItsActualPitch()
            => Assert.AreEqual(2 * Math.Sin(20 * Math.PI / 180), VillagerTiltMath.ProjectedHeight(2f, 20f, 90f), 0.00001);

        [Test]
        public void InvalidCameraPitchStillYieldsFiniteExtent()
            => Assert.AreEqual(2f, VillagerTiltMath.ProjectedHeight(2f, 20f, float.NaN), 0.00001);
    }
}
