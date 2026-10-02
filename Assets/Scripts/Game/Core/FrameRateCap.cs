using UnityEngine;

namespace NodeWar.Core
{
    /// <summary>
    /// Caps the frame rate so an uncapped build does not run the GPU flat out.
    /// Application.targetFrameRate only applies while QualitySettings.vSyncCount
    /// is 0, which it is on both quality levels; if vSync is ever switched on,
    /// the display rate takes over and this value is ignored.
    /// </summary>
    internal static class FrameRateCap
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}