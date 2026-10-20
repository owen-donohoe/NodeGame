using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StageClusterFramingTests
    {
        [TestCase(390, 844, false)]
        [TestCase(390, 844, true)]
        [TestCase(540, 1200, false)]
        [TestCase(540, 1200, true)]
        [TestCase(320, 568, false)]
        [TestCase(320, 568, true)]
        public void AllSeparatedSlotsFitBelowStripAndAboveCards(int width, int height, bool suits)
        {
            float top = 172f / 844, bottom = 0.55f;
            double aspect = (double)width / height;
            var frame = StageClusterFraming.Fit((float)aspect, top, bottom, suits, 4.9f, 0.9f, 3.5f);
            double sin = Math.Sin(frame.Pitch * Math.PI / 180), cos = Math.Cos(frame.Pitch * Math.PI / 180);
            double tan = Math.Tan(StageClusterFraming.FieldOfView * Math.PI / 360);
            double minY = 1, maxY = 0;
            for (int i = 0; i < 8; i++)
            {
                double x = (i & 1) == 0 ? -4.9 : 4.9;
                double y = (i & 2) == 0 ? -0.9 : 0.9;
                double z = (i & 4) == 0 ? -3.5 : 3.5;
                double depth = frame.Distance - y * sin + z * cos;
                double screenX = 0.5 + x / (2 * depth * tan * aspect);
                double screenY = 0.5 - (y * cos + z * sin + frame.UpOffset) / (2 * depth * tan);
                Assert.That(depth, Is.GreaterThan(0.1));
                Assert.That(screenX, Is.InRange(0.0399, 0.9601));
                Assert.That(screenY, Is.InRange(top - 0.0001, bottom + 0.0001));
                minY = Math.Min(minY, screenY); maxY = Math.Max(maxY, screenY);
            }
            TestContext.WriteLine("{0}x{1} {2}: cluster y={3:F1}..{4:F1}; panel top={5:F1} (12 reference px clearance)",
                width, height, suits ? "Suits" : "Districts", minY * height, maxY * height, maxY * height + 12 * width / 390.0);
        }
    }
}
