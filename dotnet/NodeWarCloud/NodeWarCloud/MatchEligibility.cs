using NodeWar.Backend;
using NodeWar.Lobby;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud
{
    public static class MatchEligibility
    {
        public static string Check(MatchRecord record, Log log)
        {
            var header = log?.header;
            if (header == null || header.matchId != record.matchId || header.playerIds == null ||
                header.playerIds.Length != 2 || header.playerIds[0] != record.playerIds[0] ||
                header.playerIds[1] != record.playerIds[1] || header.protocol != record.protocol ||
                header.sim != record.sim || header.content != record.content)
                return "Log header does not match the server match record.";
            if (log.loadouts == null || log.loadouts.Length != 2) return "Log needs two loadouts.";
            for (int p = 0; p < 2; p++)
            {
                var loadout = log.loadouts[p];
                if (loadout?.suits == null || loadout.nodes == null) return "Log needs complete loadouts.";
                foreach (int type in loadout.suits)
                {
                    string error = CheckVariant(record.players[p], LoadoutTypes.CatalogBaseForSuit(type), type, loadout.suitEras);
                    if (error != null) return error;
                }
                foreach (int type in loadout.nodes)
                {
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
                    int type = (int)placement.districtType;
                    string error = CheckVariant(record.players[p], LoadoutTypes.CatalogBaseForDistrict(type),
                        type, log.loadouts[p].districtEras);
                    if (error != null) return error;
                }
            return null;
        }

        private static string CheckVariant(MatchPlayerSnapshot player, string baseId, int type, int[] eras)
        {
            if (baseId == null) return "Unknown drafted type.";
            if (eras != null && type >= eras.Length) return "Incomplete era table.";
            int era = eras == null ? 0 : eras[type];
            if (era < 0 || era >= CatalogIds.EraCount || era > player.Rank.Arena)
                return "Drafted era exceeds the snapshot arena.";
            if (!player.OwnedVariants.Contains(CatalogIds.Variant(baseId, era)))
                return "Drafted variant was not owned in the snapshot.";
            return null;
        }
    }
}
