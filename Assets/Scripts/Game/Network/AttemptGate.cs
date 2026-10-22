namespace NodeWar.Network
{
    /// <summary>
    /// Generation counter for async setup. An attempt captures Begin() before its first await and
    /// checks IsCurrent after every await; Invalidate() (cancel, shutdown, destroy) or a newer Begin()
    /// makes every older attempt stale. Pure C#, main-thread only.
    /// </summary>
    public sealed class AttemptGate
    {
        private int current;
        public int Begin() => ++current;
        public void Invalidate() => current++;
        public bool IsCurrent(int attempt) => attempt == current && attempt > 0;
    }
}
