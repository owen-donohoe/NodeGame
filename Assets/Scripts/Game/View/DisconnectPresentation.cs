using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>The draft owns an empty state; disconnects can precede match construction.</summary>
    public static class DisconnectPresentation
    {
        public static bool HasMatchTally(SimulationState state)
        {
            return state != null && state.players != null && state.players.Length == 2;
        }
    }
}
