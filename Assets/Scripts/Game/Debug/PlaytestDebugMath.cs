using NodeWar.Simulation;

namespace NodeWar.Debugging
{
    /// <summary>Testable policy and balance-copy preparation. Never writes simulation state.</summary>
    public static class PlaytestDebugMath
    {
        public static bool Allowed(bool development, bool networked, bool localRunner, bool playing, bool gameOver)
        {
            return development && !networked && localRunner && playing && !gameOver;
        }

        public static bool TrySuddenDeathInFiveSeconds(GameBalanceData source, int tick, out GameBalanceData changed)
        {
            changed = source;
            if (tick < 0 || !source.BreachBarEnabled() || !source.SuddenDeathValid() ||
                source.suddenDeathTicks == null || source.suddenDeathTicks.Length == 0) return false;
            long start = (long)tick + 50;
            var schedule = new int[source.suddenDeathTicks.Length];
            for (int i = 0; i < schedule.Length; i++)
            {
                long shifted = start + source.suddenDeathTicks[i] - source.suddenDeathTicks[0];
                if (shifted > int.MaxValue) return false;
                schedule[i] = (int)shifted;
            }
            changed.suddenDeathTicks = schedule;
            changed.suddenDeathThresholds = (int[])source.suddenDeathThresholds.Clone();
            return true;
        }
    }
}
