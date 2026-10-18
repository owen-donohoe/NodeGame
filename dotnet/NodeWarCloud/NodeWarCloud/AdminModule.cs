using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NodeWar.Progression;
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
    /// Admins are the player IDs in Admin/admins.json, embedded in the module. Adding
    /// one is a commit plus a deploy, so the list is reviewed and versioned. Player
    /// IDs are not secrets; the server still checks the authenticated caller.
    /// </summary>
    public sealed class EmbeddedAdminDirectory : IAdminDirectory
    {
        private static readonly Lazy<HashSet<string>> players = new Lazy<HashSet<string>>(Load);

        public static HashSet<string> Parse(string json)
        {
            var ids = JsonConvert.DeserializeObject<List<string>>(json)
                ?? throw new InvalidDataException("The admin list must be a JSON array of player IDs.");
            return new HashSet<string>(ids.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        public Task<bool> IsAdminAsync(string playerId) =>
            Task.FromResult(!string.IsNullOrEmpty(playerId) && players.Value.Contains(playerId));

        private static HashSet<string> Load()
        {
            using var stream = typeof(EmbeddedAdminDirectory).Assembly.GetManifestResourceStream("NodeWarCloud.Admin.admins.json")
                ?? throw new InvalidDataException("Embedded admin list is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
    }

    /// <summary>Admin rules with no Cloud Save I/O, so they are testable.</summary>
    public sealed class AdminService
    {
        private readonly IAdminDirectory admins;
        private readonly Func<string, IWalletStore> walletStoreFor;
        private readonly Func<long> nowUnixSeconds;
        private readonly Func<string, ILockedPlayerRecordStore> inventoryStoreFor;
        private readonly IReadOnlyList<CatalogItem> catalog;

        public AdminService(IAdminDirectory admins, Func<string, IWalletStore> walletStoreFor, Func<long> nowUnixSeconds,
            Func<string, ILockedPlayerRecordStore> inventoryStoreFor, IReadOnlyList<CatalogItem> catalog)
        {
            this.admins = admins;
            this.walletStoreFor = walletStoreFor;
            this.nowUnixSeconds = nowUnixSeconds;
            this.inventoryStoreFor = inventoryStoreFor;
            this.catalog = catalog;
        }

        public async Task<InventoryRecord> GrantItemsAsync(string callerId, string targetPlayerId,
            string[] itemIds, bool allActive)
        {
            if (!await admins.IsAdminAsync(callerId))
                throw new UnauthorizedAccessException("Admin only.");
            var grants = new List<CatalogItem>();
            if (allActive) grants.AddRange(catalog.Where(item => !item.Retired));
            else
            {
                if (itemIds == null) throw new ArgumentNullException(nameof(itemIds));
                foreach (string id in itemIds)
                {
                    var item = catalog.FirstOrDefault(candidate => candidate.Id == id && !candidate.Retired);
                    if (item == null) throw new ArgumentException($"Unknown or retired item '{id ?? "<null>"}'.", nameof(itemIds));
                    grants.Add(item);
                }
            }
            string target = string.IsNullOrEmpty(targetPlayerId) ? callerId : targetPlayerId;
            var store = inventoryStoreFor(target);
            for (int attempt = 0; ; attempt++)
            {
                var (state, inventoryLock) = await store.ReadInventoryLockedAsync();
                var inventory = state?.Inventory
                    ?? throw new InvalidOperationException("Player must sign in once first.");
                PlayerStateLogic.NormalizeInventory(inventory);
                bool changed = false;
                foreach (var item in grants)
                {
                    var owned = item.Kind == CatalogItemKind.Variant ? inventory.OwnedVariants : inventory.OwnedSkins;
                    if (owned.Contains(item.Id)) continue;
                    owned.Add(item.Id);
                    changed = true;
                }
                if (!changed) return inventory;
                try
                {
                    await store.WriteInventoryLockedAsync(inventory, inventoryLock);
                    return inventory;
                }
                catch (RecordConflictException) when (attempt + 1 < 3) { }
            }
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
            var service = Service(context);
            return service.GrantCurrencyAsync(context.PlayerId, targetPlayerId, coins, premium, reason, requestId);
        }

        [CloudCodeFunction("AdminGrantItems")]
        public Task<InventoryRecord> AdminGrantItems(IExecutionContext context, string targetPlayerId,
            string[] itemIds, bool allActive) =>
            Service(context).GrantItemsAsync(context.PlayerId, targetPlayerId, itemIds, allActive);

        private AdminService Service(IExecutionContext context) => new AdminService(new EmbeddedAdminDirectory(),
            target => new CloudSavePlayerRecordStore(api, context, target),
            () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            target => new CloudSavePlayerRecordStore(api, context, target), ServerCatalog.Items);
    }
}
