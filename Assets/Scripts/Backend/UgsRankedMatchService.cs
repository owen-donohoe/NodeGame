using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    /// <summary>
    /// The server calls a found ranked match into play, from the NodeWarCloud
    /// Cloud Code module (dotnet/NodeWarCloud). MatchRendezvous is the module's
    /// referee for the join-code exchange and the active-match record; this
    /// client can only ask it.
    /// </summary>
    public sealed class UgsRankedMatchService : IRankedMatchService
    {
        public async Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode)
        {
            await GameServices.EnsureReadyAsync();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<RendezvousResult>(
                BackendServices.CloudModule, "Rendezvous",
                new Dictionary<string, object> { { "matchId", matchId }, { "joinCode", joinCode } });
        }

        public async Task ConfirmConnectedAsync(string matchId)
        {
            await GameServices.EnsureReadyAsync();
            await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
                BackendServices.CloudModule, "ConfirmConnected",
                new Dictionary<string, object> { { "matchId", matchId } });
        }

        public async Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            await GameServices.EnsureReadyAsync();
            LeaveMatchResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<LeaveMatchResult>(
                BackendServices.CloudModule, "LeaveMatch",
                new Dictionary<string, object> { { "matchId", matchId }, { "forfeit", forfeit } });
            if (result?.playerState != null) BackendServices.Remember(result.playerState, requestedFor);
            return result;
        }
    }
}
