namespace NodeWar.Lobby
{
    /// <summary>
    /// Pure C# sliding-range calculator for the trophy bar.
    /// The bar represents a window [rangeMin, rangeMax].
    /// Current value stays within the first ~70% of the displayed range.
    /// When current exceeds 70%, range shifts upward.
    /// When current drops below rangeMin, range shifts downward.
    /// 
    /// Range size is always fixed (rangeSize). Only the window moves.
    /// </summary>
    public class TrophyBarLogic
    {
        private int rangeMin;
        private int rangeMax;
        private int rangeSize;

        public int RangeMin => rangeMin;
        public int RangeMax => rangeMax;

        /// <summary>
        /// Initialize with a range size and the current trophy value.
        /// Centers the range so current sits at ~50%.
        /// </summary>
        public TrophyBarLogic(int currentTrophies, int rangeSize = 100)
        {
            this.rangeSize = rangeSize;
            CenterOnValue(currentTrophies);
        }

        /// <summary>
        /// Call when trophies change. Adjusts range if needed.
        /// Returns normalized fill (0-1) representing current position within range.
        /// </summary>
        public float UpdateAndGetFill(int currentTrophies)
        {
            // Shift up if past 70% mark
            int seventyPercent = rangeMin + (int)(rangeSize * 0.7f);
            if (currentTrophies > seventyPercent)
            {
                // Shift range so current sits at ~40%
                rangeMin = currentTrophies - (int)(rangeSize * 0.4f);
                rangeMax = rangeMin + rangeSize;
            }

            // Shift down if below min
            if (currentTrophies < rangeMin)
            {
                // Shift range so current sits at ~30%
                rangeMin = currentTrophies - (int)(rangeSize * 0.3f);
                rangeMax = rangeMin + rangeSize;
            }

            // Clamp rangeMin to 0 minimum
            if (rangeMin < 0)
            {
                rangeMin = 0;
                rangeMax = rangeSize;
            }

            return GetFill(currentTrophies);
        }

        /// <summary>
        /// Returns normalized fill (0-1) without adjusting range.
        /// </summary>
        public float GetFill(int currentTrophies)
        {
            if (rangeSize <= 0) return 0f;
            float fill = (float)(currentTrophies - rangeMin) / rangeSize;
            if (fill < 0f) fill = 0f;
            if (fill > 1f) fill = 1f;
            return fill;
        }

        private void CenterOnValue(int value)
        {
            rangeMin = value - (int)(rangeSize * 0.4f);
            if (rangeMin < 0) rangeMin = 0;
            rangeMax = rangeMin + rangeSize;
        }
    }
}