using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Decides which districts get a panel.
    ///
    /// Two questions, not one. IsFunctional asks whether there is something to
    /// press -- true for anyone, friend or enemy, because an enemy Forge or
    /// Core is worth reading even with its controls withheld. HasOnNodeState
    /// marks the districts with per-node state worth a small sheet even
    /// without an action -- Farm, Mine and Market -- and those only earn one
    /// for their own owner: an opponent's farm has nothing you couldn't
    /// already see on the node itself. HasSheet is the single rule both feed,
    /// and the one OpenForNode calls.
    ///
    /// Five of the six functional districts fall straight out of the simulation:
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

                // Farm, Mine, Market -- no action; HasOnNodeState covers their
                //                       sheet, and only for their own owner
                // Village         -- passive spawn bonus
                // Shrine          -- passive heal, no equip
                // Watchtower      -- passive vision
                // Rampart         -- passive max-HP bonus
                // None            -- crossroads, no state and no action
                default:
                    return false;
            }
        }

        /// <summary>
        /// Districts that carry per-node state a player will want to read even
        /// though they have no action: worker presence and task progress. These
        /// are the ones the on-node display exists for, and the ones HasSheet
        /// opens a sheet for when the viewer owns them.
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

        /// <summary>
        /// True if a tap on this node should open a sheet for the given
        /// viewer.
        ///
        /// A functional district opens for anyone once it is owned -- there is
        /// something worth reading even for an enemy's Forge or Core. A
        /// Farm, Mine or Market opens only for its own owner: it has no
        /// action either way, so the only reason to cover the board with one
        /// is to read state that belongs to you.
        /// </summary>
        public static bool HasSheet(NodeData node, int viewerID)
        {
            if (IsFunctional(node.districtType)) return node.ownerID != -1;
            if (HasOnNodeState(node.districtType)) return node.ownerID == viewerID;
            return false;
        }
    }
}
