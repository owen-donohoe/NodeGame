using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Player-facing ranked calls: rendezvous, leaving, presence, hold
    /// resolution and results. Unlike MatchmakerAllocatorModule, which
    /// refuses a call WITH a player identity, these refuse a call WITHOUT
    /// one: a service call has no caller to look up in the roster.
    /// </summary>
    public sealed class RankedMatchModule
    {
        private readonly IGameApiClient api;
        private readonly ILogger<MatchDiscipline> logger;
        private static readonly InventoryRules Inventory = new InventoryRules(ServerCatalog.Items);

        public RankedMatchModule(IGameApiClient api, ILogger<MatchDiscipline> logger = null)
        { this.api = api; this.logger = logger; }

        public const string NoIdentityRefused = "A player identity is required.";

        public static bool HasNoIdentity(IExecutionContext context) => string.IsNullOrWhiteSpace(context?.PlayerId);

        [CloudCodeFunction("GetMatchResult")]
        public Task<MatchResultView> GetMatchResult(IExecutionContext context, string matchId)
        {
            if (HasNoIdentity(context))
                return Task.FromResult(new MatchResultView { message = NoIdentityRefused });
            return BuildHold(context).GetResult(matchId, context.PlayerId);
        }

        [CloudCodeFunction("Presence")]
        public Task<PresenceResult> Presence(IExecutionContext context, string matchId, bool holding)
        {
            if (HasNoIdentity(context))
                return Task.FromResult(new PresenceResult { message = NoIdentityRefused });
            return BuildHold(context).Presence(matchId, context.PlayerId, holding, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [CloudCodeFunction("ResolveHold")]
        public Task<ResolveHoldResult> ResolveHold(IExecutionContext context, string matchId)
        {
            if (HasNoIdentity(context))
                return Task.FromResult(new ResolveHoldResult { message = NoIdentityRefused });
            return BuildHold(context).ResolveHold(matchId, context.PlayerId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [CloudCodeFunction("Rendezvous")]
        public Task<RendezvousResult> Rendezvous(IExecutionContext context, string matchId, string joinCode)
        {
            if (HasNoIdentity(context))
                return Task.FromResult(new RendezvousResult { message = NoIdentityRefused });
            return Build(context).Rendezvous(matchId, context.PlayerId, joinCode);
        }

        [CloudCodeFunction("ConfirmConnected")]
        public Task ConfirmConnected(IExecutionContext context, string matchId)
        {
            if (HasNoIdentity(context)) return Task.CompletedTask;
            return Build(context).ConfirmConnected(matchId, context.PlayerId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [CloudCodeFunction("LeaveMatch")]
        public Task<LeaveMatchResult> LeaveMatch(IExecutionContext context, string matchId, bool forfeit)
        {
            if (HasNoIdentity(context))
                return Task.FromResult(new LeaveMatchResult { message = NoIdentityRefused });
            return Build(context).Leave(matchId, context.PlayerId, forfeit, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        private MatchHold BuildHold(IExecutionContext context)
        {
            var settler = new MatchSettler(id => new CloudSavePlayerRecordStore(api, context, id), Inventory);
            return new MatchHold(new CloudSaveMatchRecordStore(api, context),
                id => new CloudSavePlayerRecordStore(api, context, id), settler,
                new MatchDiscipline(id => new CloudSavePlayerRecordStore(api, context, id), logger));
        }

        private MatchRendezvous Build(IExecutionContext context)
        {
            var settler = new MatchSettler(id => new CloudSavePlayerRecordStore(api, context, id), Inventory);
            return new MatchRendezvous(new CloudSaveMatchRecordStore(api, context),
                id => new CloudSavePlayerRecordStore(api, context, id), settler,
                new MatchDiscipline(id => new CloudSavePlayerRecordStore(api, context, id), logger));
        }
    }
}
