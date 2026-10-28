using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterWaveDefaultsTests
    {
        private static string Shader => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Water/NodeWarWater.shader"));
        private static double Scalar(string name) => double.Parse(Regex.Match(Shader, name + @"\([^\r\n]*= ([0-9.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        private static double[] Wave(int index) => Array.ConvertAll(Regex.Match(Shader, "_Wave" + (char)('A' + index) + @"\([^\r\n]*= \(([^)]+)\)").Groups[1].Value.Split(','), x => double.Parse(x, CultureInfo.InvariantCulture));

        [TestCase(2, 4)] [TestCase(3, 4)]
        public void ScaledWavesResolveAtDefaultAndMinimumScale(int tier, int count)
        {
            double amplitude = 0, shortest = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                double[] wave = Wave(i);
                amplitude += wave[2] * wave[3] / (2 * Math.PI);
                shortest = Math.Min(shortest, wave[3]);
            }
            Assert.That(amplitude * Scalar("_WaveScale") * Scalar("_WaveSteepnessScale"), Is.GreaterThan(0.4), "Open water must exceed the old global cap.");
            Assert.That(Scalar("_ShoreMaxAmplitude"), Is.LessThanOrEqualTo(0.45 - 0.05));
            foreach (float scale in new[] { 0.25f, 1f, 4f })
                Assert.That(shortest * scale / (6.0 / WaterMeshBuilder.SubdivisionsForTier(tier, scale)), Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void DefaultDirectionsAreSeparated()
        {
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                {
                    double[] a = Wave(i), b = Wave(j);
                    double cosine = (a[0] * b[0] + a[1] * b[1]) / Math.Sqrt((a[0] * a[0] + a[1] * a[1]) * (b[0] * b[0] + b[1] * b[1]));
                    Assert.That(Math.Acos(cosine) * 180 / Math.PI, Is.GreaterThanOrEqualTo(25));
                }
        }
    }
}
