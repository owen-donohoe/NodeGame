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

        [TestCase(1, 2)] [TestCase(2, 4)] [TestCase(3, 4)]
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
            foreach (float scale in new[] { 0.25f, 1.5f, 4f })
                Assert.That(shortest * scale / (6.0 / WaterMeshBuilder.SubdivisionsForTier(tier, scale)), Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void DefaultFoldProducesWhitecapsInOpenWater()
        {
            double maximum = 0, sum = 0;
            double[][] waves = { Wave(0), Wave(1), Wave(2), Wave(3) };
            double scale = Scalar("_WaveScale"), speed = Scalar("_WaveSpeed"), steepness = Scalar("_WaveSteepnessScale"), threshold = Scalar("_CrestFoamThreshold" );
            for (int i = 0; i < 4; i++) sum += waves[i][2];
            int visible = 0;
            for (int t = 0; t < 100; t++)
                for (int x = 0; x < 100; x++)
                {
                    double fold = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        double[] w = waves[i];
                        double k = 2 * Math.PI / (w[3] * scale);
                        double phase = k * (w[0] * x - Math.Sqrt(9.8 / k) * speed * t * 0.2);
                        fold += w[2] * Math.Min(steepness, 0.95 / sum) * Math.Sin(phase);
                    }
                    double normalized = fold / sum;
                    maximum = Math.Max(maximum, normalized);
                    if (normalized > threshold + 0.15) visible++;
                }
            Assert.That(maximum, Is.GreaterThan(Scalar("_CrestFoamThreshold") + 0.15));
            Assert.That(visible, Is.GreaterThan(100), "Whitecaps must occupy patches, not just one coincident phase.");
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
