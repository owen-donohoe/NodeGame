using UnityEngine;

namespace NodeWar.Core
{
    /// <summary>
    /// Caps the frame rate so an uncapped build does not run the GPU flat out.
    /// The rate is a player setting (GameSettingsData.frameCap); both settings
    /// surfaces call <see cref="Apply"/> when it changes and when they load.
    /// Until a profile has been read the build runs at the default.
    ///
    /// Application.targetFrameRate only applies while QualitySettings.vSyncCount
    /// is 0, which it is on both quality levels; if vSync is ever switched on,
    /// the display rate takes over and the setting is ignored.
    /// </summary>
    public static class FrameRateCap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyDefault()
        {
            Apply(NodeWar.Lobby.GameSettingsData.DefaultFrameCap);
        }

        /// <summary>Sets Application.targetFrameRate from a stored frame-cap index.</summary>
        public static void Apply(int frameCap)
        {
            int rate = NodeWar.Lobby.GameSettingsData.TargetFrameRate(frameCap);
            if (Application.targetFrameRate != rate) Application.targetFrameRate = rate;
        }
    }
}
