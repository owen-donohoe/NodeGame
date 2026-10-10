using NodeWar.Simulation;

namespace NodeWar.Input
{
    /// <summary>Chooses an affordable drafted combat suit using the actual match balance.</summary>
    public static class BotSuitChoice
    {
        public static SuitType Choose(SimulationState state, GameBalanceData balance, int playerId, int nodeId)
        {
            if (state == null || state.players == null || state.nodes == null ||
                playerId < 0 || playerId >= state.players.Length ||
                nodeId < 0 || nodeId >= state.nodes.Length || state.nodes[nodeId].ownerID != playerId)
                return SuitType.None;
            var player = state.players[playerId];
            if (player.draftedSuits == null) return SuitType.None;
            SuitType choice = SuitType.None;
            for (int i = 0; i < player.draftedSuits.Length; i++)
            {
                SuitType suit = (SuitType)player.draftedSuits[i];
                if (!GameBalanceData.IsCombatSuit(suit) ||
                    !balance.CanEquipSuitAtNode(suit, state.nodes[nodeId].districtType) ||
                    !balance.TryGetSuitStats(suit, player.SuitEra(suit), out SuitStats stats) ||
                    player.food < stats.foodCost || player.materials < stats.materialCost)
                    continue;
                if (suit == SuitType.Warrior) return suit;
                if (choice == SuitType.None || (int)suit < (int)choice) choice = suit;
            }
            return choice;
        }
    }
}
