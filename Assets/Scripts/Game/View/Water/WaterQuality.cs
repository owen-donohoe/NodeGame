using UnityEngine;
using UnityEngine.Rendering;
using NodeWar.Lobby;

namespace NodeWar.View
{
    public static class WaterQuality
    {
        /// <summary>Low=0, Medium=1, High=2, Ultra=3, Custom=4; -1 follows the profile.</summary>
        public static int Override = -1;
        public static int CustomFeatures = -1, CustomWaves = -1;
        private static readonly GraphicsFeature[] flags = { GraphicsFeature.Transparency, GraphicsFeature.SurfaceFoamDistortion, GraphicsFeature.HorizonFresnel, GraphicsFeature.Specular, GraphicsFeature.Shadows, GraphicsFeature.DetailNormals, GraphicsFeature.MacroNormals, GraphicsFeature.Reflections, GraphicsFeature.CrestColour, GraphicsFeature.CrestFoam, GraphicsFeature.ShoreNoise, GraphicsFeature.LappingLines, GraphicsFeature.SecondFoamLayer, GraphicsFeature.Dither };
        private static readonly string[] keywords = { "_WATER_TRANSPARENCY", "_WATER_SURFACE_FOAM_DISTORTION", "_WATER_HORIZON_FRESNEL", "_WATER_SPECULAR", "_WATER_SHADOWS", "_WATER_DETAIL_NORMALS", "_WATER_MACRO_NORMALS", "_WATER_REFLECTIONS", "_WATER_CREST_COLOUR", "_WATER_CREST_FOAM", "_WATER_SHORE_NOISE", "_WATER_LAPPING_LINES", "_WATER_SECOND_FOAM_LAYER", "_WATER_DITHER" };
        private static GameSettingsData Settings => PlayerProfile.Instance != null ? PlayerProfile.Instance.Settings : GameSettingsData.CreateDefault();
        public static int Current => Override >= 0 ? Mathf.Clamp(Override, 0, 4) : Settings.graphicsQuality;
        public static int Features => Current == 4 ? (Override == 4 && CustomFeatures >= 0 ? CustomFeatures : Settings.graphicsFeatures) & WaterFeatures.All : WaterFeatures.FeaturesFor(Current);
        public static int Waves => Current == 4 ? WaterFeatures.NormalizeWaves(Override == 4 && CustomWaves >= 0 ? CustomWaves : Settings.waterWaves) : WaterFeatures.WavesFor(Current);
        public static bool Has(GraphicsFeature feature) => (Features & (int)feature) != 0;

        public static void SelectOverride(int tier)
        {
            if (tier == 4 && Current != 4) { CustomFeatures = Features; CustomWaves = Waves; }
            Override = tier;
            Apply();
        }

        public static void Apply()
        {
            int features = Features;
            for (int i = 0; i < flags.Length; i++) SetKeyword(keywords[i], (features & (int)flags[i]) != 0);
            SetKeyword("_WATER_WAVES_2", Waves == 2);
            SetKeyword("_WATER_WAVES_4", Waves == 4);
            Material material = WaterMaterialFactory.ForQuality(features, Waves, Current);
            bool transparent = Has(GraphicsFeature.Transparency);
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.SetFloat("_DetailNormalSamples", Current == 3 ? 2 : 1);
            material.renderQueue = (int)(transparent ? RenderQueue.Transparent : RenderQueue.Geometry);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            PlinthFactory.ApplyTransparency(transparent);
            WaterSurfaceView.RefreshActive();
        }

        private static void SetKeyword(string keyword, bool enabled)
        {
            if (enabled) Shader.EnableKeyword(keyword);
            else Shader.DisableKeyword(keyword);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverride() { Override = -1; CustomFeatures = CustomWaves = -1; }
    }
}
