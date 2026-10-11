using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class NodeFloorFootprintTests
    {
        [TestCase(0.25f)]
        [TestCase(1f)]
        [TestCase(6f)]
        [TestCase(100f)]
        public void FootprintScalesWithBoardCells(float scale)
        {
            var unit = NodeFloorFootprint.ForScale(1f);
            var scaled = NodeFloorFootprint.ForScale(scale);
            Assert.IsFalse(scaled.IsEmpty);
            Assert.AreEqual(unit.QuadHalfExtent * scale, scaled.QuadHalfExtent, 1e-5f);
            Assert.AreEqual(unit.TerritoryHalfExtent * scale, scaled.TerritoryHalfExtent, 1e-5f);
            Assert.AreEqual(unit.TerritoryWidth * scale, scaled.TerritoryWidth, 1e-5f);
            Assert.AreEqual(unit.ClaimHalfExtent * scale, scaled.ClaimHalfExtent, 1e-5f);
            Assert.AreEqual(unit.ClaimWidth * scale, scaled.ClaimWidth, 1e-5f);
            Assert.AreEqual(unit.HealthHalfExtent * scale, scaled.HealthHalfExtent, 1e-5f);
            Assert.AreEqual(unit.HealthWidth * scale, scaled.HealthWidth, 1e-5f);
            Assert.AreEqual(unit.CornerRadius * scale, scaled.CornerRadius, 1e-5f);
            Assert.AreEqual(unit.DitherHalfExtent * scale, scaled.DitherHalfExtent, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidScaleYieldsEmptyFootprint(float scale)
        {
            var footprint = NodeFloorFootprint.ForScale(scale);
            Assert.IsTrue(footprint.IsEmpty);
            Assert.AreEqual(0f, footprint.QuadHalfExtent);
            Assert.AreEqual(0f, footprint.TerritoryHalfExtent);
            Assert.AreEqual(0f, footprint.TerritoryWidth);
            Assert.AreEqual(0f, footprint.ClaimHalfExtent);
            Assert.AreEqual(0f, footprint.ClaimWidth);
            Assert.AreEqual(0f, footprint.HealthHalfExtent);
            Assert.AreEqual(0f, footprint.HealthWidth);
            Assert.AreEqual(0f, footprint.CornerRadius);
            Assert.AreEqual(0f, footprint.DitherHalfExtent);
        }

        [TestCase(1f)]
        [TestCase(6f)]
        public void TerritoryClaimAndHealthTracksHavePositiveEnvelopeGaps(float scale)
        {
            var f = NodeFloorFootprint.ForScale(scale);
            Assert.Greater(f.TerritoryHalfExtent, f.ClaimHalfExtent);
            Assert.Greater(f.ClaimHalfExtent, f.HealthHalfExtent);
            Assert.Greater(f.HealthHalfExtent, 0f);
            Assert.Greater(f.TerritoryHalfExtent - f.TerritoryWidth * 0.5f,
                f.ClaimHalfExtent + f.ClaimWidth * 0.5f);
            Assert.Greater(f.ClaimHalfExtent - f.ClaimWidth * 0.5f,
                f.HealthHalfExtent + f.HealthWidth * 0.5f);
        }

        [Test]
        public void RoundedCornersAndStrokeEnvelopesFitInsideGroundQuad()
        {
            var f = NodeFloorFootprint.ForScale(1f);
            Assert.Greater(f.CornerRadius, 0f);
            Assert.Greater(f.TerritoryWidth, 0f);
            Assert.Greater(f.ClaimWidth, 0f);
            Assert.Greater(f.HealthWidth, 0f);
            Assert.Less(f.TerritoryHalfExtent + f.TerritoryWidth * 0.5f, f.QuadHalfExtent);
            Assert.Less(f.CornerRadius, f.HealthHalfExtent);
            Assert.Greater(f.CornerRadius, f.TerritoryWidth * 0.5f);
            Assert.Greater(f.CornerRadius, f.ClaimWidth * 0.5f);
            Assert.Greater(f.CornerRadius, f.HealthWidth * 0.5f);
        }

        [Test]
        public void DitherReservationStaysInsideTerritoryInnerEdge()
        {
            var f = NodeFloorFootprint.ForScale(1f);
            Assert.Greater(f.DitherHalfExtent, 0f);
            Assert.LessOrEqual(f.DitherHalfExtent, f.TerritoryHalfExtent - f.TerritoryWidth * 0.5f);
            Assert.Less(f.DitherHalfExtent, f.QuadHalfExtent);
        }
    }
}
