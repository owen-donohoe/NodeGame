using UnityEngine;

namespace NodeWar.View
{
    public static class WaterQuality
    {
        /// <summary>Low=0, Medium=1, High=2, Ultra=3.</summary>
        public static int Current => Application.isMobilePlatform ? 1 : 2;
    }
}
