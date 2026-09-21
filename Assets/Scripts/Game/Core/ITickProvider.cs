namespace NodeWar.Core
{
    /// <summary>
    /// Shared interface for TickRunner (local) and LockstepRunner (networked).
    /// View layer code references this to get interpolation alpha without
    /// knowing which runner is active.
    /// </summary>
    public interface ITickProvider
    {
        float TickAlpha { get; }

        /// <summary>
        /// Raised once per simulated tick, straight after it, with what that
        /// tick did. Raised inside the runner's catch-up loop, so a frame that
        /// runs three ticks raises it three times and nothing is lost.
        ///
        /// The log is the runner's own and is cleared before the next tick, so
        /// a subscriber copies what it needs rather than holding the log.
        /// </summary>
        event System.Action<NodeWar.Simulation.TickEventLog> TickSimulated;
    }
}
