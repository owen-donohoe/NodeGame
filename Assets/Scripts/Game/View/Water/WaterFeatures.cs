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
        CrestFoam = 512,
        ShoreNoise = 1024,
        LappingLines = 2048,
        SecondFoamLayer = 8192,
        Dither = 16384,
    }

    /// <summary>Single source of truth for view-only water presets; usable without Unity.</summary>
    public static class WaterFeatures
    {
        public const int All = 32767 & ~4096; // Retired caustics bit stays reserved.
        private const GraphicsFeature Medium = GraphicsFeature.Transparency | GraphicsFeature.SurfaceFoamDistortion
            | GraphicsFeature.HorizonFresnel | GraphicsFeature.Specular | GraphicsFeature.CrestColour
            | GraphicsFeature.ShoreNoise | GraphicsFeature.Dither;
        private const GraphicsFeature High = Medium | GraphicsFeature.Shadows | GraphicsFeature.DetailNormals
            | GraphicsFeature.MacroNormals | GraphicsFeature.Reflections | GraphicsFeature.LappingLines;
        public static int FeaturesFor(int preset) => preset == 0 ? 0 : preset == 1 ? (int)Medium : preset == 3 ? All : (int)High;
        public static int WavesFor(int preset) => preset == 0 ? 0 : preset == 1 ? 2 : 4;
        public static int PaddingFor(int tier) => tier == 0 ? 4 : tier == 1 ? 6 : 10;
        // Four intervals per shortest scaled wavelength, including the minimum bench scale.
        public static int Subdivisions(int waves, float waveScale = 1.5f, float cellSize = 6f)
            => waves == 0 ? 1 : Math.Max(waves == 2 ? 4 : 8,
                (int)Math.Ceiling(4 * cellSize / ((waves == 2 ? 11f : 3.5f) * Math.Max(0.25f, waveScale))));
        public static int NormalizeWaves(int waves) => waves <= 0 ? 0 : waves < 4 ? 2 : 4;
        public static int NextWaves(int waves) => waves == 0 ? 2 : waves == 2 ? 4 : 0;
    }
}
