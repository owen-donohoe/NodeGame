using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    /// <summary>
    /// The three player-facing calls of ranked step 8.2b: publishing a Relay
    /// join code and reading the roster, confirming the connection, and
    /// leaving (void or forfeit). Unlike MatchmakerAllocatorModule, which
    /// refuses a call WITH a player identity, these refuse a call WITHOUT
    /// one: a service call has no caller to look up in the roster.
    /// </summary>
    public sealed class RankedMatchModule
    {
        private readonly IGameApiClient api;
        private static readonly InventoryRules Inventory = new InventoryRules(ServerCatalog.Items);

        public RankedMatchModule(IGameApiClient api) { this.api = api; }

        public const string NoIdentityRefused = "A player identity is required.";

        public static bool HasNoIdentity(IExecutionContext context) => string.IsNullOrEmpty(context?.PlayerId);

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

        private MatchRendezvous Build(IExecutionContext context)
        {
            var settler = new MatchSettler(id => new CloudSavePlayerRecordStore(api, context, id), Inventory);
            return new MatchRendezvous(new CloudSaveMatchRecordStore(api, context),
                id => new CloudSavePlayerRecordStore(api, context, id), settler);
        }
    }
}
