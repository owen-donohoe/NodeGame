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
        Caustics = 4096,
        SecondFoamLayer = 8192,
        Dither = 16384,
    }

    /// <summary>Single source of truth for view-only water presets; usable without Unity.</summary>
    public static class WaterFeatures
    {
        public const int All = 32767;
        private const GraphicsFeature Medium = GraphicsFeature.Transparency | GraphicsFeature.SurfaceFoamDistortion
            | GraphicsFeature.HorizonFresnel | GraphicsFeature.Specular | GraphicsFeature.CrestColour
            | GraphicsFeature.ShoreNoise | GraphicsFeature.Dither;
        private const GraphicsFeature High = Medium | GraphicsFeature.Shadows | GraphicsFeature.DetailNormals
            | GraphicsFeature.MacroNormals | GraphicsFeature.Reflections | GraphicsFeature.LappingLines;
        public static int FeaturesFor(int preset) => preset == 0 ? 0 : preset == 1 ? (int)Medium : preset == 3 ? All : (int)High;
        public static int WavesFor(int preset) => preset == 0 ? 0 : preset == 1 ? 2 : 4;
        public static int Subdivisions(int waves) => waves == 0 ? 1 : waves == 2 ? 4 : 8;
        public static int NormalizeWaves(int waves) => waves <= 0 ? 0 : waves < 4 ? 2 : 4;
        public static int NextWaves(int waves) => waves == 0 ? 2 : waves == 2 ? 4 : 0;
    }
}
