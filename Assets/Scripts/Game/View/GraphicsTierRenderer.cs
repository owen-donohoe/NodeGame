using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NodeWar.View
{
    /// <summary>Owns one runtime URP copy; tier changes must never dirty a project asset.</summary>
    public static class GraphicsTierRenderer
    {
        private static UniversalRenderPipelineAsset runtime;
        private static RenderPipelineAsset originalQualityAsset;
        private static float authoredShadowDistance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => Apply(WaterQuality.Current);

        public static void Apply(int tier)
        {
            if (runtime == null)
            {
                var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (source == null) return;
                originalQualityAsset = QualitySettings.renderPipeline;
                authoredShadowDistance = source.shadowDistance;
                // Mutating the active asset changes the user's project in Editor play mode.
                // Clone once and keep renderer-feature wiring from the selected PC/Mobile asset.
                runtime = Object.Instantiate(source);
                runtime.name = source.name + " (runtime graphics tier)";
                runtime.hideFlags = HideFlags.HideAndDontSave;
                QualitySettings.renderPipeline = runtime;
                Application.quitting += Release;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
            }
            // Custom uses High's renderer budget; its controls customise water features only.
            int budget = tier == 4 ? 2 : tier;
            runtime.renderScale = budget == 0 ? 0.7f : budget == 1 ? 0.8f : 1f;
            runtime.msaaSampleCount = budget < 2 ? 1 : budget == 2 ? 2 : 4;
            runtime.shadowDistance = budget == 0 ? 0 : authoredShadowDistance;
            // Preserve authored HDR: SampleSceneProfile enables Bloom (0.25) and tonemapping.
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Release();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Release()
        {
            Application.quitting -= Release;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            if (runtime == null) return;
            if (QualitySettings.renderPipeline == runtime) QualitySettings.renderPipeline = originalQualityAsset;
            if (Application.isPlaying) Object.Destroy(runtime);
            else Object.DestroyImmediate(runtime);
            runtime = null;
            originalQualityAsset = null;
        }
    }
}
