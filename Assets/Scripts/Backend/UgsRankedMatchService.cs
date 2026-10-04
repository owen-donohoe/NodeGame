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
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<RendezvousResult>(
                BackendServices.CloudModule, "Rendezvous",
                new Dictionary<string, object> { { "matchId", matchId }, { "joinCode", joinCode } });
        }

        public async Task ConfirmConnectedAsync(string matchId)
        {
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
                BackendServices.CloudModule, "ConfirmConnected",
                new Dictionary<string, object> { { "matchId", matchId } });
        }

        public async Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            LeaveMatchResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<LeaveMatchResult>(
                BackendServices.CloudModule, "LeaveMatch",
                new Dictionary<string, object> { { "matchId", matchId }, { "forfeit", forfeit } });
            if (result?.playerState != null) BackendServices.Remember(result.playerState, requestedFor);
            return result;
        }

        public async Task<MatchResultView> GetResultAsync(string matchId)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            MatchResultView result = await CloudCodeService.Instance.CallModuleEndpointAsync<MatchResultView>(
                BackendServices.CloudModule, "GetMatchResult",
                new Dictionary<string, object> { { "matchId", matchId } });
            RememberSettled(result, requestedFor);
            return result;
        }

        public async Task<PresenceResult> PresenceAsync(string matchId, bool holding)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            PresenceResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<PresenceResult>(
                BackendServices.CloudModule, "Presence",
                new Dictionary<string, object> { { "matchId", matchId }, { "holding", holding } });
            RememberSettled(result?.result, requestedFor);
            return result;
        }

        public async Task<ResolveHoldResult> ResolveHoldAsync(string matchId)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            ThrowIfSimulatedOffline();
            await GameServices.EnsureReadyAsync();
            ResolveHoldResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<ResolveHoldResult>(
                BackendServices.CloudModule, "ResolveHold",
                new Dictionary<string, object> { { "matchId", matchId } });
            RememberSettled(result?.result, requestedFor);
            return result;
        }

        private static void ThrowIfSimulatedOffline()
        {
            if (BackendServices.SimulatedOffline)
                throw new System.InvalidOperationException("Simulated offline (debug drop key).");
        }

        // A settled result carries the caller's new rank, so the lobby strip is
        // current on return without another GetPlayerState.
        private static void RememberSettled(MatchResultView result, string requestedFor)
        {
            if (result?.playerState != null) BackendServices.Remember(result.playerState, requestedFor);
        }
    }
}
