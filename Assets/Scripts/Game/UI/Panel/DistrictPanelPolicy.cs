using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Decides which districts get a panel.
    ///
    /// The rule: a district is *functional* if there is something to press.
    /// That makes an open panel a reliable signal rather than a thing that
    /// sometimes appears with nothing in it, and it is why farms and mines
    /// stop opening one -- their state belongs on the node itself.
    ///
    /// Five of the six fall straight out of the simulation:
    ///
    ///   Forge  -- ProcessSetAllocation rejects any district but Forge
    ///             (CommandProcessor.cs:54).
    ///   Camp, Barracks, Arsenal, Sanctuary
    ///          -- the only districts CanEquipSuitAtNode returns true for
    ///             (GameBalanceData.cs:109-130).
    ///
    /// Core is the exception, and deliberately not dressed up as derived:
    /// ProcessRespawnCommand has no district check at all. It respawns the
    /// villager at the player's coreNodeID wherever the command came from. The
    /// Core panel is functional because respawn is surfaced there by
    /// convention, not because the simulation constrains it. If respawn ever
    /// moves, this entry moves with it and nothing in the simulation will
    /// complain.
    ///
    /// Move is excluded throughout: it is a world gesture, not a panel action.
    /// </summary>
    public static class DistrictPanelPolicy
    {
        /// <summary>
        /// True if this district has an action a player can take from a panel.
        /// </summary>
        public static bool IsFunctional(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Core:        // respawn (by convention, see above)
                case DistrictType.Forge:       // SetAllocation
                case DistrictType.Camp:        // Equip
                case DistrictType.Barracks:    // Equip
                case DistrictType.Arsenal:     // Equip
                case DistrictType.Sanctuary:   // Equip
                    return true;

                // Farm, Mine      -- production only, shown on the node
                // Village         -- passive spawn bonus
                // Shrine          -- passive heal, no equip
                // Watchtower      -- passive vision
                // Rampart         -- passive max-HP bonus
                // Market          -- produces food, but no command targets it
                // None            -- crossroads, no state and no action
                default:
                    return false;
            }
        }

        /// <summary>
        /// Districts that carry per-node state a player will want to read even
        /// though they have no action: worker presence and task progress. These
        /// are the ones the on-node display exists for.
        ///
        /// Kept separate from IsFunctional because "has nothing to press" and
        /// "has nothing to show" are different questions, and a crossroads is
        /// the only district where both answers are no.
        /// </summary>
        public static bool HasOnNodeState(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm:
                case DistrictType.Mine:
                case DistrictType.Market:
                    return true;

                default:
                    return false;
            }
        }
    }
}
