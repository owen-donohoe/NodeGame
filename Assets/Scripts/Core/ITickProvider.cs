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
    }
}