namespace NodeWar.View
{
    /// <summary>
    /// The numbers and wording behind the breach bar, the breacher highlight
    /// and the tempo / sudden-death banners. UnityEngine-free so it can be
    /// tested without the Editor; the drawing that reads it stays in Unity.
    ///
    /// Presentation only. Everything here is a function of values already in
    /// SimulationState and GameBalanceData, and nothing here is read back by
    /// the simulation.
    /// </summary>
    public static class BreachTempoMath
    {
        /// <summary>Seconds of warning before a sudden-death tick.</summary>
        public const int CountdownSeconds = 5;

        /// <summary>The most pips a core bar draws; a bigger swarm still reads as full.</summary>
        public const int MaxPips = 8;

        /// <summary>
        /// Breaches until this defender loses. R1 leaves an active core at one
        /// even after a threshold drop or simultaneous loss cancellation.
        /// Only a defeated defender has an empty wall.
        /// </summary>
        public static int WallRemaining(int breaches, int threshold, bool defeated)
        {
            if (defeated) return 0;
            if (threshold < 1) threshold = 1;
            if (breaches < 0) breaches = 0;
            return breaches >= threshold ? 1 : threshold - breaches;
        }

        /// <summary>
        /// How much of the wall is left, measured against the ORIGINAL threshold the match
        /// opened with, never the current one. Sudden death lowers the threshold, and a wall
        /// drawn against the lowered value would read as full (1/1); against the original it
        /// reads as the one segment of three that is actually left.
        /// </summary>
        public static float WallFill(int breaches, int threshold, bool defeated, int originalMax)
        {
            if (threshold < 1) threshold = 1;
            int max = originalMax > threshold ? originalMax : threshold;
            return (float)WallRemaining(breaches, threshold, defeated) / max;
        }

        public static string WallLabel(int breaches, int threshold, bool defeated)
        {
            int remaining = WallRemaining(breaches, threshold, defeated);
            if (remaining == 0) return "Core breached";
            return remaining == 1 ? "Next breach loses" : remaining + " breaches left";
        }

        /// <summary>Progress against a core, 0..1. A bad max reads as empty, not as a divide by zero.</summary>
        public static float BarFill(int bar, int barMax)
        {
            if (barMax <= 0 || bar <= 0) return 0f;
            if (bar >= barMax) return 1f;
            return (float)bar / barMax;
        }

        /// <summary>Hidden at rest: nothing banked and nobody channelling.</summary>
        public static bool BarVisible(int bar, int breachers)
        {
            return bar > 0 || breachers > 0;
        }

        public static int PipsToShow(int breachers)
        {
            if (breachers < 0) return 0;
            return breachers > MaxPips ? MaxPips : breachers;
        }

        /// <summary>
        /// The next sudden-death tick still ahead of <paramref name="tick"/>, as an
        /// index into the schedule, or -1 when there is none.
        /// </summary>
        public static int NextSuddenDeathIndex(int[] suddenDeathTicks, int tick)
        {
            if (suddenDeathTicks == null) return -1;
            for (int i = 0; i < suddenDeathTicks.Length; i++)
                if (suddenDeathTicks[i] > tick) return i;
            return -1;
        }

        /// <summary>
        /// Whole seconds left before the next sudden-death tick while it is inside
        /// the warning window, rounded up so "5" shows first and "1" shows last.
        /// Returns 0 outside the window (no countdown), and index/threshold say
        /// which step is coming.
        /// </summary>
        public static int SuddenDeathCountdown(int[] suddenDeathTicks, int[] suddenDeathThresholds,
                                               int tick, int ticksPerSecond,
                                               out int nextThreshold)
        {
            nextThreshold = 0;
            if (ticksPerSecond <= 0) return 0;
            if (suddenDeathThresholds == null) return 0;

            int index = NextSuddenDeathIndex(suddenDeathTicks, tick);
            if (index < 0 || index >= suddenDeathThresholds.Length) return 0;

            int ticksLeft = suddenDeathTicks[index] - tick;
            int seconds = (ticksLeft + ticksPerSecond - 1) / ticksPerSecond;
            if (seconds < 1 || seconds > CountdownSeconds) return 0;

            nextThreshold = suddenDeathThresholds[index];
            return seconds;
        }

        public static string TempoStageTitle(int stageIndex)
        {
            return stageIndex <= 0 ? "Tempo rising" : "Tempo rising again";
        }

        public static string TempoStageSub(int stageIndex)
        {
            return stageIndex <= 0 ? "Claims speed up; respawns slow down" : "Claims speed up again; respawns slow further";
        }

        public static string CountdownTitle(int seconds)
        {
            return "Sudden death in " + seconds + "…";
        }

        public static string ThresholdLine(int threshold)
        {
            return threshold == 1 ? "One breach will win" : "Breaches to win drop to " + threshold;
        }

        public static string SuddenDeathTitle()
        {
            return "Sudden death";
        }
    }
}
