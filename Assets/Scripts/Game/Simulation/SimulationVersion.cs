namespace NodeWar.Simulation
{
    /// <summary>
    /// Which game this simulation plays. Two builds with the same inputs and a
    /// different result must not share a number: peers compare it in the
    /// handshake and refuse each other, and a match log records it so a replay
    /// is only ever run on the simulation that produced it.
    ///
    /// Bump it in the same commit as any change that alters what the same
    /// inputs produce: a tick-rule change, a new state field that feeds a
    /// result, a re-pinned determinism baseline. Balance values are covered
    /// separately by <see cref="BalanceHasher"/>, so a pure balance edit does
    /// not need a bump. DeterminismBaselineTests pins this number beside the
    /// baselines, so re-pinning them without bumping it fails a test.
    /// </summary>
    public static class SimulationVersion
    {
        public const int Current = 1;
    }
}
