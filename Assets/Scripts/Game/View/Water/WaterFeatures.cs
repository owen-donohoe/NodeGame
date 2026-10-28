using System;

namespace NodeWar.View
{
    [Flags]
    public enum GraphicsFeature
    {
        Transparency = 1,
        SurfaceFoamDistortion = 2,
        HorizonFresnel = 4,
        Specular = 8,
        Shadows = 16,
        DetailNormals = 32,
        MacroNormals = 64,
        Reflections = 128,
        CrestColour = 256,
        LappingLines = 2048,
        SecondFoamLayer = 8192,
        Dither = 16384,
    }

    /// <summary>Single source of truth for view-only water presets; usable without Unity.</summary>
    public static class WaterFeatures
    {
        public const int All = 32767 & ~(4096 | 512 | 1024); // Retired caustics bit stays reserved.
        private const GraphicsFeature Medium = GraphicsFeature.Transparency | GraphicsFeature.SurfaceFoamDistortion
            | GraphicsFeature.HorizonFresnel | GraphicsFeature.Specular | GraphicsFeature.CrestColour
            | GraphicsFeature.Dither | GraphicsFeature.MacroNormals | GraphicsFeature.LappingLines;
        private const GraphicsFeature High = Medium | GraphicsFeature.Shadows | GraphicsFeature.DetailNormals
            | GraphicsFeature.MacroNormals | GraphicsFeature.Reflections | GraphicsFeature.LappingLines;
        public static int FeaturesFor(int preset) => preset == 0 ? (int)(GraphicsFeature.HorizonFresnel | GraphicsFeature.LappingLines) : preset == 1 ? (int)Medium : preset == 3 ? All : (int)High;
        public static int WavesFor(int preset) => preset == 0 ? 0 : preset == 1 ? 1 : 4;
        public static int PaddingFor(int tier) => tier == 0 ? 4 : tier == 1 ? 6 : 10;
        // Preset tessellation; unusually small Gerstner scales increase density to avoid aliasing.
        public static int SubdivisionsFor(int tier, int waves, float waveScale = 1f, float cellSize = 6f)
            => waves == 0 ? 1 : Math.Max(tier == 1 ? 3 : tier == 3 ? 8 : 6, Subdivisions(waves, waveScale, cellSize));
        public static int Subdivisions(int waves, float waveScale = 1f, float cellSize = 6f)
            => waves == 0 ? 1 : waves == 1 ? 3 : (int)Math.Ceiling(4 * cellSize / ((waves == 2 ? 11.124f : 4.248f) * Math.Max(0.25f, waveScale)));
        public static string WaveName(int waves) => waves == 0 ? "Off" : waves == 1 ? "Noise" : waves == 2 ? "Gerstner 2" : "Gerstner 4";
        public static int NormalizeWaves(int waves) => waves <= 0 ? 0 : waves == 1 ? 1 : waves < 4 ? 2 : 4;
        public static int NextWaves(int waves) => waves == 0 ? 1 : waves == 1 ? 2 : waves == 2 ? 4 : 0;
    }
}
