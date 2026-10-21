using NodeWar.Backend;
using NodeWar.Lobby;
using NodeWar.Simulation;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud
{
    public static class MatchEligibility
    {
        public static string Check(MatchRecord record, Log log)
        {
            if (record.protocol == 6 || record.protocol > ProtocolVersion.Current)
                return "Incompatible protocol version; use protocol 7.";
            var header = log?.header;
            if (header == null || header.matchId != record.matchId || header.playerIds == null ||
                header.playerIds.Length != 2 || header.playerIds[0] != record.playerIds[0] ||
                header.playerIds[1] != record.playerIds[1] || header.protocol != record.protocol ||
                header.sim != record.sim || header.content != record.content)
                return "Log header does not match the server match record.";
            string mapError = CheckMap(record, log);
            if (mapError != null) return mapError;
            if (log.loadouts == null || log.loadouts.Length != 2) return "Log needs two loadouts.";
            var board = log.board;
            if (record.protocol == ProtocolVersion.Current)
            {
                string budgetError = CheckBudgets(record, log, out board);
                if (budgetError != null) return budgetError;
            }
            for (int p = 0; p < 2; p++)
            {
                var loadout = log.loadouts[p];
                if (loadout?.suits == null || loadout.districts == null) return "Log needs complete loadouts.";
                foreach (int type in loadout.suits)
                {
                    string error = CheckVariant(record.players[p], LoadoutTypes.CatalogBaseForSuit(type), type, loadout.suitEras);
                    if (error != null) return error;
                }
                foreach (int type in loadout.districts)
                {
                    if (!NodeWar.Simulation.DistrictRoster.IsActive((NodeWar.Simulation.DistrictType)type)) return "Inactive district.";
                    string error = CheckVariant(record.players[p], LoadoutTypes.CatalogBaseForDistrict(type), type, loadout.districtEras);
                    if (error != null) return error;
                }
            }
            // Draft placements can include the default pool, absent from nodes.
            if (log.draft != null)
                foreach (var placement in log.draft)
                {
                    int p = placement.playerID;
                    if (p != 0 && p != 1) return "Invalid draft player.";
                    if (!NodeWar.Simulation.PlacementLegality.IsDraftable(placement.districtType)) return "Inactive draft district.";
                    int type = (int)placement.districtType;
                    string error = CheckVariant(record.players[p], LoadoutTypes.CatalogBaseForDistrict(type),
                        type, log.loadouts[p].districtEras);
                    if (error != null) return error;
                }
            return null;
        }

        /// <summary>
        /// The log's setup must be the record's map and rules. A record stored before maps
        /// has none and is only checked as far as it ever was, but a current-version record
        /// without a map is never admitted: the server always writes one.
        /// </summary>
        private static string CheckMap(MatchRecord record, Log log)
        {
            if (record.mapId == null)
                return record.sim >= 3 ? "Server match record has no map." : null;

            var setup = log.setup;
            if (setup == null || setup.MapId != record.mapId || setup.BoardHash != record.boardHash ||
                setup.SimulationVersion != record.sim || setup.BalanceHash != record.content ||
                log.board.terrain == null ||
                NodeWar.Simulation.BoardHasher.Hash(log.board) != record.boardHash)
                return "Log map does not match the server match record.";
            return null;
        }

        private static string CheckBudgets(MatchRecord record, Log log, out BoardConfigData board)
        {
            if (!PremadeMaps.Catalog.TryGet(record.mapId, out board) ||
                BoardHasher.Hash(board) != record.boardHash)
                return "Unknown server map or board hash.";
            var budgets = new int[2][];
            for (int p = 0; p < 2; p++)
            {
                var loadout = log.loadouts[p];
                if (loadout?.suits == null || loadout.districts == null)
                    return "Log needs complete loadouts.";
                int arena = record.players[p].Rank.Arena;
                int suitBudget = LoadoutCapacityPolicy.SuitCapacity(arena);
                if (loadout.suits.Length > suitBudget ||
                    loadout.districts.Length > LoadoutCapacityPolicy.DistrictCapacity(arena))
                    return "Loadout exceeds snapshot arena budget.";
                for (int i = 0; i < loadout.suits.Length; i++)
                {
                    int type = loadout.suits[i];
                    if (!GameBalanceData.IsCombatSuit((SuitType)type) ||
                        LoadoutTypes.CatalogBaseForSuit(type) == null)
                        return "Invalid chosen suit.";
                    for (int j = 0; j < i; j++)
                        if (loadout.suits[j] == type) return "Duplicate chosen suit.";
                    if (!LoadoutCapacityPolicy.IsBaseUsable(LoadoutTypes.CatalogBaseForSuit(type), arena))
                        return "Drafted base is unavailable at the snapshot arena.";
                }
                foreach (string id in LoadoutCapacityPolicy.LockedSuits(arena))
                    if (System.Array.IndexOf(loadout.suits, (int)LoadoutTypes.SuitForLobbyId(id)) < 0)
                        return "Loadout is missing a locked suit.";

                budgets[p] = new int[LoadoutTypes.DistrictTypeCount];
                for (int i = 0; i < loadout.districts.Length; i++)
                {
                    int type = loadout.districts[i];
                    if (type < 0 || type >= budgets[p].Length ||
                        !PlacementLegality.IsDraftable((DistrictType)type) ||
                        LoadoutTypes.CatalogBaseForDistrict(type) == null)
                        return "Invalid chosen district.";
                    if (budgets[p][type] != 0) return "Duplicate chosen district.";
                    if (!LoadoutCapacityPolicy.IsBaseUsable(LoadoutTypes.CatalogBaseForDistrict(type), arena))
                        return "Drafted base is unavailable at the snapshot arena.";
                    budgets[p][type]++;
                }
                foreach (string id in LoadoutCapacityPolicy.LockedDistricts(arena))
                    if (budgets[p][(int)LoadoutTypes.DistrictForLobbyId(id)] == 0)
                        return "Loadout is missing a locked district.";
            }
            if (log.draft != null)
                foreach (var placement in log.draft)
                {
                    int p = placement.playerID;
                    int type = (int)placement.districtType;
                    if (p < 0 || p > 1) return "Invalid draft player.";
                    if (type < 0 || type >= budgets[p].Length || budgets[p][type] == 0)
                        return "Draft placement exceeds snapshot arena budget.";
                    budgets[p][type]--;
                }
            return null;
        }
        private static string CheckVariant(MatchPlayerSnapshot player, string baseId, int type, int[] eras,
            bool requireOwnership = true)
        {
            if (baseId == null) return "Unknown drafted type.";
            // ERAS encodes a null table as an empty array when another table is present.
            if (eras != null && eras.Length > 0 && type >= eras.Length) return "Incomplete era table.";
            int era = eras == null || eras.Length == 0 ? 0 : eras[type];
            if (era < 0 || era >= CatalogIds.EraCount ||
                (SuitTree.IsTreeSuit(baseId)
                    ? !SuitTree.IsAvailable(CatalogIds.Variant(baseId, era), player.Rank.Arena)
                    : era > player.Rank.Arena))
                return "Drafted era exceeds the snapshot arena.";
            if (requireOwnership && !player.OwnedVariants.Contains(CatalogIds.Variant(baseId, era)))
                return "Drafted variant was not owned in the snapshot.";
            return null;
        }
    }
}
