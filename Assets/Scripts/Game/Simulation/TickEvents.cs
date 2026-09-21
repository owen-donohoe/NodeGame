using System.Collections.Generic;

namespace NodeWar.Simulation
{
    /// <summary>
    /// A moment the simulation passed through. Only moments: a fight starting,
    /// a death, a node changing hands. Anything that stays true for a while --
    /// a fight still going, a node still being pushed -- is read off
    /// SimulationState, the way the claim bar already is, because a log that
    /// had to say "still happening" every tick would be the state again.
    /// </summary>
    public enum TickEventType
    {
        /// <summary>A node's defenders were first drawn into a fight. nodeID; playerID is -1.</summary>
        CombatStarted,

        /// <summary>A villager fell in combat. villagerID, nodeID where it fell, playerID its owner.</summary>
        VillagerDied,

        /// <summary>A villager came back at its owner's Core. value is 1 if paid for, 0 if its timer ran out.</summary>
        VillagerRespawned,

        /// <summary>An owned node was pushed back to neutral. playerID is who lost it, value who pushed.</summary>
        NodeNeutralised,

        /// <summary>A claim completed. playerID is the new owner, value the owner before (-1 if none).</summary>
        NodeClaimed,

        /// <summary>A villager reached an undefended enemy Core and was spent. playerID is the defender.</summary>
        Breach
    }

    /// <summary>
    /// One entry in a TickEventLog. Flat and integer-only, in the style of
    /// GameCommand. Fields a given type does not use are -1.
    /// </summary>
    public struct TickEvent
    {
        public TickEventType type;
        public int nodeID;
        public int villagerID;
        public int playerID;
        public int value;
    }

    /// <summary>
    /// What one tick did, alongside the state it produced.
    ///
    /// Output only. The simulation appends to it and never reads it back, so
    /// even a wrong entry is a cosmetic bug and cannot move the match. That is
    /// also why it is not a SimulationState field: it is not state, it is not
    /// replicated, and it is not in SimulationStateHasher. Both peers produce
    /// the same entries anyway, because they are written from integer state in
    /// tick order.
    ///
    /// Passed in rather than owned by the simulation, and null means nothing
    /// is recorded -- which is what every test and any headless run gets
    /// without asking. The tick driver owns one, clears it before each tick,
    /// and hands it on once the tick is done.
    /// </summary>
    public sealed class TickEventLog
    {
        private readonly List<TickEvent> events = new List<TickEvent>();

        public int Count => events.Count;

        public TickEvent this[int index] => events[index];

        public void Clear()
        {
            events.Clear();
        }

        public void Add(TickEventType type, int nodeID, int villagerID, int playerID, int value)
        {
            events.Add(new TickEvent
            {
                type = type,
                nodeID = nodeID,
                villagerID = villagerID,
                playerID = playerID,
                value = value
            });
        }
    }
}
