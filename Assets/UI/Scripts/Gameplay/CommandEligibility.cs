using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>Why ProcessEquipCommand would drop an Equip, in the order it checks.</summary>
    public enum EquipRefusal
    {
        None,
        NoSuchVillager,
        NotYours,
        Dead,
        Consumed,
        Busy,
        AlreadySuited,
        NotCombatSuit,
        NodeNotYours,
        DoesNotFit,
        NotDrafted,
        CannotAfford
    }

    /// <summary>Why ProcessRespawnCommand would drop a Respawn, in the order it checks.</summary>
    public enum RespawnRefusal
    {
        None,
        NoSuchVillager,
        NotYours,
        NotDead,
        Consumed,
        CannotAfford
    }

    /// <summary>Why ProcessSetAllocation would drop a SetAllocation, in the order it checks.</summary>
    public enum AllocationRefusal
    {
        None,
        NoSuchNode,
        NotYours,
        NotForge,
        Negative
    }

    /// <summary>
    /// Whether the simulation would accept a command, and if not, why - asked
    /// before the command is sent.
    ///
    /// WHY THIS EXISTS. CommandProcessor drops a command it will not run
    /// without a word, so a button that is enabled when the command would be
    /// refused looks exactly like a bug, and a price shown that differs from
    /// the price charged is one. Every check here restates a check in
    /// CommandProcessor, in the same order, so the first refusal named is the
    /// one the simulation acts on.
    ///
    /// Restating rules is how UIs drift from simulations, so this file is held
    /// to the real thing: NodeWar.Lobby.Tests runs every answer here against
    /// CommandProcessor itself over a grid of states, and fails the moment the
    /// two disagree. Change a check in CommandProcessor and that suite says
    /// which line here to change.
    ///
    /// Read-only and free of UnityEngine, so it compiles outside Unity. It
    /// never writes state; the UI still only changes the game by sending a
    /// GameCommand.
    /// </summary>
    public static class CommandEligibility
    {
        // ===== EQUIP =====

        /// <summary>The full ProcessEquipCommand check for one villager and suit.</summary>
        public static EquipRefusal Equip(SimulationState state, GameBalanceData balance,
                                         int playerID, int villagerID, SuitType suit)
        {
            EquipRefusal villager = EquipVillager(state, playerID, villagerID);
            if (villager != EquipRefusal.None) return villager;

            if (!GameBalanceData.IsCombatSuit(suit)) return EquipRefusal.NotCombatSuit;

            int nodeID = state.villagers[villagerID].currentNodeID;
            if (nodeID < 0 || nodeID >= state.nodes.Length) return EquipRefusal.NodeNotYours;
            if (state.nodes[nodeID].ownerID != playerID) return EquipRefusal.NodeNotYours;
            if (!balance.CanEquipSuitAtNode(suit, state.nodes[nodeID].districtType)) return EquipRefusal.DoesNotFit;

            return EquipSuit(state, balance, playerID, suit);
        }

        /// <summary>
        /// The checks that depend only on the villager: whether it could take a
        /// suit at all. Used for a roster row, before any suit is chosen.
        /// </summary>
        public static EquipRefusal EquipVillager(SimulationState state, int playerID, int villagerID)
        {
            if (villagerID < 0 || villagerID >= state.villagers.Length) return EquipRefusal.NoSuchVillager;

            VillagerData v = state.villagers[villagerID];
            if (v.ownerID != playerID) return EquipRefusal.NotYours;
            if (v.state == VillagerState.Dead) return EquipRefusal.Dead;
            if (v.isConsumed) return EquipRefusal.Consumed;
            if (v.state != VillagerState.Idle) return EquipRefusal.Busy;
            if (GameBalanceData.IsCombatSuit(v.suit)) return EquipRefusal.AlreadySuited;

            return EquipRefusal.None;
        }

        /// <summary>
        /// The checks that depend only on the player and the suit: drafted, and
        /// affordable. Used for a suit card, before any villager is chosen.
        /// </summary>
        public static EquipRefusal EquipSuit(SimulationState state, GameBalanceData balance,
                                             int playerID, SuitType suit)
        {
            if (!HasDrafted(state, playerID, suit)) return EquipRefusal.NotDrafted;

            SuitStats stats = balance.GetSuitStats(suit, state.players[playerID].SuitEra(suit));
            if (state.players[playerID].food < stats.foodCost) return EquipRefusal.CannotAfford;
            if (state.players[playerID].materials < stats.materialCost) return EquipRefusal.CannotAfford;

            return EquipRefusal.None;
        }

        public static bool HasDrafted(SimulationState state, int playerID, SuitType suit)
        {
            int[] drafted = state.players[playerID].draftedSuits;
            if (drafted == null) return false;

            for (int i = 0; i < drafted.Length; i++)
            {
                if (drafted[i] == (int)suit) return true;
            }

            return false;
        }

        // ===== RESPAWN =====

        /// <summary>
        /// What a respawn costs this player right now. The same integer
        /// arithmetic as ProcessRespawnCommand: each Sanctuary worker takes its
        /// Sanctuary era's percentage off, where the worker is the player's,
        /// working, not consumed, and standing on a Sanctuary the player owns -
        /// floor 1.
        /// </summary>
        public static int RespawnCost(SimulationState state, GameBalanceData balance, int playerID)
        {
            int baseCost = balance.respawnCostFood;
            int reductionPercent = SanctuaryReductionPercent(state, balance, playerID);
            int cost = baseCost - (baseCost * reductionPercent) / 100;

            return cost < 1 ? 1 : cost;
        }

        public static RespawnRefusal Respawn(SimulationState state, GameBalanceData balance,
                                             int playerID, int villagerID)
        {
            if (villagerID < 0 || villagerID >= state.villagers.Length) return RespawnRefusal.NoSuchVillager;

            VillagerData v = state.villagers[villagerID];
            if (v.ownerID != playerID) return RespawnRefusal.NotYours;
            if (v.state != VillagerState.Dead) return RespawnRefusal.NotDead;
            if (v.isConsumed) return RespawnRefusal.Consumed;
            if (state.players[playerID].food < RespawnCost(state, balance, playerID)) return RespawnRefusal.CannotAfford;

            return RespawnRefusal.None;
        }

        /// <summary>
        /// Which dead villager a single Respawn button should bring back: the one
        /// with the longest wait left, lowest ID on a tie, or -1 when none is
        /// waiting. A respawned villager returns with base stats and no suit,
        /// so dead villagers differ only in their wait - paying to skip the
        /// longest is always the best use of the food.
        /// </summary>
        public static int RespawnTarget(SimulationState state, int playerID)
        {
            int best = -1;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                if (best < 0 || v.respawnTicksRemaining > state.villagers[best].respawnTicksRemaining)
                    best = i;
            }

            return best;
        }

        private static int SanctuaryReductionPercent(SimulationState state, GameBalanceData balance, int playerID)
        {
            int percent = 0;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;

                int node = v.currentNodeID;
                if (node < 0 || node >= state.nodes.Length) continue;
                if (state.nodes[node].districtType != DistrictType.Sanctuary) continue;
                if (state.nodes[node].ownerID != playerID) continue;

                percent += balance.GetDistrictStats(DistrictType.Sanctuary, state.nodes[node].districtEra)
                    .respawnCostReductionPercent;
            }

            return percent;
        }

        // ===== ALLOCATION =====

        /// <summary>
        /// The ProcessSetAllocation check. Floor 0 and no ceiling: the simulation
        /// accepts any non-negative allocation, so the UI must not invent a
        /// maximum.
        /// </summary>
        public static AllocationRefusal Allocation(SimulationState state, int playerID, int nodeID, int value)
        {
            if (nodeID < 0 || nodeID >= state.nodes.Length) return AllocationRefusal.NoSuchNode;
            if (state.nodes[nodeID].ownerID != playerID) return AllocationRefusal.NotYours;
            if (state.nodes[nodeID].districtType != DistrictType.Forge) return AllocationRefusal.NotForge;
            if (value < 0) return AllocationRefusal.Negative;

            return AllocationRefusal.None;
        }
    }
}
