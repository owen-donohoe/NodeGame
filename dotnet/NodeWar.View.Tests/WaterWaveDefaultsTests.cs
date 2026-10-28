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

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)]
        public void FloorBoundMatchesShaderPerWaveFormula(int mode)
        {
            double sum = 0;
            for (int i = 0; i < (mode == 4 ? 4 : mode == 2 ? 2 : 0); i++)
            {
                double[] wave = Wave(i);
                double term = wave[2] / (6.2831853 / Math.Max(wave[3] * 4, 0.01));
                Assert.That(WaterWaveBounds.Amplitude(wave[2], wave[3], 4), Is.EqualTo(term).Within(1e-9));
                sum += term;
            }
            double bound = WaterWaveBounds.MaxTrough(mode, 0.7, sum, 1.5, 1);
            Assert.That(bound, Is.EqualTo(mode == 0 ? 0 : mode == 1 ? 0.7 : sum * 3).Within(1e-9));
            string core = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Water/WaterCore.hlsl"));
            StringAssert.Contains("max(wave.w * _WaveScale, 0.01)", core);
            StringAssert.Contains("float amplitude = steepness / k", core);
            StringAssert.Contains("swellA.r + swellB.r - 1", core);
        }

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
        public void DefaultWavesStayWithinWindSpreadWithoutIntegerWavelengthRatios()
        {
            string core = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Water/WaterCore.hlsl"));
            double spread = Scalar("_WindSpread"), wind = Scalar("_WindDirection") * 360;
            Assert.That(spread, Is.EqualTo(35));
            Assert.That(Scalar("_WindDirection"), Is.EqualTo(0.1));
            var directions = new double[4];
            double[] phaseOffsets = { 0, 1.7, 3.1, 4.6 };
            double[] wavelengthFactors = { 1, 0.382, 0.618, 0.236 };
            for (int i = 0; i < 4; i++)
            {
                var call = Regex.Match(core, "Gerstner\\(_Wave" + (char)('A' + i) + @", ([-0-9.]+), ([-0-9.]+), xz");
                Assert.That(call.Success, Is.True);
                double offset = double.Parse(call.Groups[1].Value, CultureInfo.InvariantCulture);
                Assert.That(Math.Abs(offset), Is.LessThanOrEqualTo(1));
                directions[i] = wind + offset * spread;
                Assert.That(double.Parse(call.Groups[2].Value, CultureInfo.InvariantCulture), Is.EqualTo(phaseOffsets[i]));
                Assert.That(Wave(i)[3] / Wave(0)[3], Is.EqualTo(wavelengthFactors[i]).Within(0.0001));
            }
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                {
                    Assert.That(Math.Abs(directions[i] - directions[j]), Is.LessThanOrEqualTo(2 * spread));
                    double ratio = Math.Max(Wave(i)[3], Wave(j)[3]) / Math.Min(Wave(i)[3], Wave(j)[3]);
                    double nearestInteger = Math.Round(ratio);
                    Assert.That(Math.Abs(ratio - nearestInteger) / nearestInteger, Is.GreaterThan(0.05),
                        "Near-integer wavelength ratios create repeated interference.");
                }
        }
    }
}
