using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    public sealed class UgsProfileService : IProfileService
    {
        public async Task<PublicProfileView> GetAsync(string playerId)
        {
            await GameServices.EnsureReadyAsync();
            string account = BackendServices.Account.Current?.PlayerId;
            var result = await CloudCodeService.Instance.CallModuleEndpointAsync<PublicProfileView>(
                BackendServices.CloudModule, "GetProfileView", new Dictionary<string, object> { ["playerId"] = playerId ?? "" });
            if (!string.Equals(account, BackendServices.Account.Current?.PlayerId, StringComparison.Ordinal))
                throw new InvalidOperationException("The signed-in player changed.");
            return result;
        }
    }
}
