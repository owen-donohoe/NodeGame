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

        [TestCase(1, 2)] [TestCase(2, 4)] [TestCase(3, 4)]
        public void DefaultsFitWaterlineAndResolveShortestWave(int tier, int count)
        {
            double sum = 0, shortest = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var values = Regex.Match(Shader, "_Wave" + (char)('A' + i) + @"\([^\r\n]*= \(([^)]+)\)").Groups[1].Value.Split(',');
                double steepness = double.Parse(values[2], CultureInfo.InvariantCulture);
                double wavelength = double.Parse(values[3], CultureInfo.InvariantCulture);
                sum += steepness * wavelength / (2 * Math.PI);
                shortest = Math.Min(shortest, wavelength);
            }
            double amplitude = sum * Scalar("_WaveSteepnessScale");
            Assert.That(amplitude, Is.LessThanOrEqualTo(0.45 - 0.08));
            Assert.That(Scalar("_MaxWaveAmplitude"), Is.LessThanOrEqualTo(0.45 - 0.08));
            if (count == 4) Assert.That(2 * amplitude, Is.InRange(0.3, 0.4));
            Assert.That(shortest / (6.0 / WaterMeshBuilder.SubdivisionsForTier(tier)), Is.GreaterThanOrEqualTo(4));
        }
    }
}
