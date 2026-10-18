using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    /// <summary>Who may call admin functions.</summary>
    public interface IAdminDirectory
    {
        Task<bool> IsAdminAsync(string playerId);
    }

    /// <summary>
    /// Admins are player IDs listed in the Private custom item "admin", key
    /// "players" (a JSON array of strings), edited by hand in the UGS dashboard
    /// (Cloud Save, Custom Items). A missing item means nobody is an admin.
    /// </summary>
    public sealed class CloudSaveAdminDirectory : IAdminDirectory
    {
        public const string ItemId = "admin";
        public const string PlayersKey = "players";

        private readonly IGameApiClient api;
        private readonly IExecutionContext context;

        public CloudSaveAdminDirectory(IGameApiClient api, IExecutionContext context)
        { this.api = api; this.context = context; }

        public async Task<bool> IsAdminAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            var response = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, ItemId, new List<string> { PlayersKey });
            var item = response.Data.Results.SingleOrDefault(i => i.Key == PlayersKey);
            if (item == null) return false;
            var players = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(item.Value));
            return players != null && players.Contains(playerId, StringComparer.Ordinal);
        }
    }

    /// <summary>Admin rules with no Cloud Save I/O, so they are testable.</summary>
    public sealed class AdminService
    {
        private readonly IAdminDirectory admins;
        private readonly Func<string, IWalletStore> walletStoreFor;
        private readonly Func<long> nowUnixSeconds;

        public AdminService(IAdminDirectory admins, Func<string, IWalletStore> walletStoreFor, Func<long> nowUnixSeconds)
        {
            this.admins = admins;
            this.walletStoreFor = walletStoreFor;
            this.nowUnixSeconds = nowUnixSeconds;
        }

        /// <summary>Grants (or, with negative amounts, removes) currency. The target defaults to the caller.</summary>
        public async Task<WalletRecord> GrantCurrencyAsync(string callerId, string targetPlayerId, long coins, long premium,
            string reason, string requestId)
        {
            if (!await admins.IsAdminAsync(callerId))
                throw new UnauthorizedAccessException("Admin only.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Give a reason.", nameof(reason));
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Give a request ID.", nameof(requestId));
            string target = string.IsNullOrEmpty(targetPlayerId) ? callerId : targetPlayerId;
            // The caller is recorded so a grant can always be traced to who made it.
            return await new WalletService(walletStoreFor(target), nowUnixSeconds)
                .ApplyAsync(requestId, "admin:" + callerId + ":" + reason, coins, premium);
        }
    }

    /// <summary>
    /// Functions for the operator, refused for everyone not in the admin
    /// directory. The dashboard's Player Management stays the raw editor; these
    /// exist so changes go through the same rules and leave a transaction.
    /// </summary>
    public class AdminModule
    {
        private readonly IGameApiClient api;

        public AdminModule(IGameApiClient api) { this.api = api; }

        [CloudCodeFunction("AdminGrantCurrency")]
        public Task<WalletRecord> AdminGrantCurrency(IExecutionContext context, string targetPlayerId,
            long coins, long premium, string reason, string requestId)
        {
            var service = new AdminService(new CloudSaveAdminDirectory(api, context),
                target => new CloudSavePlayerRecordStore(api, context, target),
                () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return service.GrantCurrencyAsync(context.PlayerId, targetPlayerId, coins, premium, reason, requestId);
        }
    }
}
