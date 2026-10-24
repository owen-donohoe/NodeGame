using UnityEngine;
using NodeWar.Lobby;

namespace NodeWar.View
{
    public static class WaterQuality
    {
        /// <summary>Low=0, Medium=1, High=2, Ultra=3.</summary>
        public static int Override = -1;

        public static int Current => Override >= 0 ? Mathf.Clamp(Override, 0, 3) : PlayerProfile.Instance != null
            ? PlayerProfile.Instance.Settings.graphicsQuality : Application.isMobilePlatform ? 1 : 2;
        public static void Apply()
        {
            int tier = Current;
            SetKeyword("_WATER_MEDIUM", tier == 1);
            SetKeyword("_WATER_HIGH", tier == 2);
            SetKeyword("_WATER_ULTRA", tier == 3);
        }

        private static void SetKeyword(string keyword, bool enabled)
        {
            if (enabled) Shader.EnableKeyword(keyword);
            else Shader.DisableKeyword(keyword);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverride() => Override = -1;
    }
}
