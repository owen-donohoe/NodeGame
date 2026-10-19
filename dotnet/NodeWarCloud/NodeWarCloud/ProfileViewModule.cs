using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public interface IProfileViewStore
    {
        Task<PlayerState> ReadProfileViewAsync();
    }

    public sealed class ProfileViewModule
    {
        private readonly IGameApiClient api;
        public ProfileViewModule(IGameApiClient api) { this.api = api; }

        [CloudCodeFunction("GetProfileView")]
        public Task<PublicProfileView> GetProfileView(IExecutionContext context, string playerId) =>
            ProfileViews.GetAsync(context.PlayerId, playerId,
                target => new CloudSavePlayerRecordStore(api, context, target));
    }

    public static class ProfileViews
    {
        public static async Task<PublicProfileView> GetAsync(string callerId, string playerId,
            Func<string, IProfileViewStore> stores)
        {
            if (string.IsNullOrWhiteSpace(callerId)) throw new UnauthorizedAccessException("An authenticated player is required.");
            string target = string.IsNullOrEmpty(playerId) ? callerId : playerId;
            var state = await stores(target).ReadProfileViewAsync();
            var equipped = state?.Inventory?.Equipped?.Variants;
            var profile = new PublicProfileView
            {
                Card = new PlayerCard { PlayerId = target, Name = state?.Profile?.Name ?? "",
                    Tag = state?.Profile?.Tag ?? "", RR = state?.Rank?.RR ?? 0,
                    Arena = state?.Rank?.Arena ?? 0, Presence = PlayerPresence.Offline },
                Stats = state?.History?.Stats ?? new PlayerStats(),
                SuitIds = LobbyIds(equipped, true), DistrictIds = LobbyIds(equipped, false)
            };
            var history = state?.History;
            if (history?.MatchIds != null)
                foreach (string id in history.MatchIds.Take(HistorySummaryRules.Limit))
                {
                    var summary = history.Summaries?.FirstOrDefault(s => s.MatchId == id);
                    if (summary == null) continue;
                    profile.Recent.Add(new MatchSummary { MatchId = id,
                        Opponent = new PlayerCard { PlayerId = summary.OpponentId, Name = summary.OpponentName,
                            Tag = summary.OpponentTag, Presence = PlayerPresence.Offline },
                        Won = summary.Won, RRDelta = summary.RRDelta, Arena = summary.Arena, CreatedUnix = summary.CreatedUnix });
                }
            return profile;
        }

        private static string[] LobbyIds(Dictionary<string, string> equipped, bool suits)
        {
            if (equipped == null) return Array.Empty<string>();
            var ids = new List<string>();
            int length = suits ? CatalogKeys.SuitTableLength : CatalogKeys.DistrictTableLength;
            for (int type = 1; type < length; type++)
            {
                string baseId = suits ? CatalogKeys.SuitBase(type) : CatalogKeys.DistrictBase(type);
                if (baseId != null && equipped.ContainsKey(baseId))
                    ids.Add(suits ? CatalogKeys.SuitLobbyId(type) : CatalogKeys.DistrictLobbyId(type));
            }
            return ids.ToArray();
        }
    }
}
