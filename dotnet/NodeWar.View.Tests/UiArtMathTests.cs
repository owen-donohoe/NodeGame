using NodeWar.UI;
using NUnit.Framework;

public class UiArtMathTests
{
    [TestCase(-10f, 1f)]
    [TestCase(0f, 1f)]
    [TestCase(4f, 4f)]
    [TestCase(100f, 16f)]
    [TestCase(float.NaN, 4f)]
    [TestCase(float.PositiveInfinity, 4f)]
    [TestCase(float.NegativeInfinity, 4f)]
    public void SkinGeometryIsFiniteAndBounded(float input, float expected)
    {
        Assert.AreEqual(expected, UiArtMath.Geometry(input, 4f, 1f, 16f));
    }
}
