using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NodeWar.Backend;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud
{
    /// <summary>
    /// A single-record locked read/write for Inventory. Equip only ever
    /// touches Inventory, never all four records, so it does not need (and must
    /// not take) ISettlementPlayerStore's four-record lock set. Reading the
    /// Inventory write lock with the read, and writing with it, means a
    /// concurrent settlement's clamp (which writes Inventory in its own batch)
    /// conflicts with a stale Equip instead of being silently overwritten by it.
    /// </summary>
    public interface ILockedPlayerRecordStore : IPlayerRecordStore
    {
        // Includes all state records so Get can initialize only missing records.
        Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync();
        Task WriteInventoryLockedAsync(InventoryRecord inventory, string expectedWriteLock);
        Task WriteDefaultsLockedAsync(PlayerState records, string inventoryWriteLock);
    }

    /// <summary>
    /// The wallet alone, with its write lock. Grants and spends read and write
    /// only this key, so they never contend with settlement or equip.
    /// </summary>
    public interface IWalletStore
    {
        /// <summary>Null wallet when the player's records were never initialized.</summary>
        Task<(WalletRecord Wallet, string WriteLock)> ReadWalletLockedAsync();
        Task WriteWalletLockedAsync(WalletRecord wallet, string expectedWriteLock);
    }

    /// <summary>
    /// Player records in Cloud Save's protected access class: the player can
    /// read them, and only a service token (this module) can write them.
    /// </summary>
    public sealed class CloudSavePlayerRecordStore : IPlayerRecordStore, ISettlementPlayerStore, ILockedPlayerRecordStore,
        IDisciplinePlayerStore, IWalletStore, IProfileStore, IProfileViewStore
    {
        private readonly IGameApiClient api;
        private readonly IExecutionContext context;
        private readonly string playerId;

        public CloudSavePlayerRecordStore(IGameApiClient api, IExecutionContext context, string playerId = null)
        {
            this.api = api;
            this.context = context;
            this.playerId = playerId ?? context.PlayerId;
        }

        public async Task<PlayerState> ReadAsync()
        {
            return (await ReadWholeStateAsync()).State;
        }

        public Task<LockedPlayerState> ReadForSettlementAsync() => ReadLockedAsync(
            PlayerStateKeys.All.Concat(new[] { PlayerStateKeys.ActiveMatch, PlayerStateKeys.Discipline, PlayerStateKeys.Profile }).ToList());

        public async Task<PlayerState> ReadProfileViewAsync() => (await ReadLockedAsync(new List<string>
            { PlayerStateKeys.Profile, PlayerStateKeys.Rank, PlayerStateKeys.History, PlayerStateKeys.Inventory })).State;


        // Everything the client is shown: settlement records, wallet and profile.
        private Task<LockedPlayerState> ReadWholeStateAsync() => ReadLockedAsync(
            PlayerStateKeys.All.Concat(new[] { PlayerStateKeys.ActiveMatch, PlayerStateKeys.Discipline, PlayerStateKeys.Wallet, PlayerStateKeys.Profile }).ToList());

        public Task<LockedPlayerState> ReadDisciplineAsync() => ReadLockedAsync(
            new List<string> { PlayerStateKeys.Discipline, PlayerStateKeys.Rating });

        private async Task<LockedPlayerState> ReadLockedAsync(List<string> keys)
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId,
                keys);

            var state = new PlayerState();
            var locks = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Item item in response.Data.Results)
            {
                locks[item.Key] = item.WriteLock;
                switch (item.Key)
                {
                    case PlayerStateKeys.Rating: state.Rating = Convert<RatingRecord>(item.Value); break;
                    case PlayerStateKeys.Rank: state.Rank = Convert<RankRecord>(item.Value); break;
                    case PlayerStateKeys.Inventory: state.Inventory = Convert<InventoryRecord>(item.Value); break;
                    case PlayerStateKeys.History: state.History = Convert<HistoryRecord>(item.Value); break;
                    case PlayerStateKeys.ActiveMatch: state.ActiveMatch = Convert<ActiveMatchRecord>(item.Value); break;
                    case PlayerStateKeys.Discipline: state.Discipline = Convert<DisciplineRecord>(item.Value); break;
                    case PlayerStateKeys.Wallet: state.Wallet = Convert<WalletRecord>(item.Value); break;
                    case PlayerStateKeys.Profile: state.Profile = Convert<ProfileRecord>(item.Value); break;
                }
            }
            return new LockedPlayerState(state, locks);
        }

        public async Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read)
        {
            if (read.State.Rating == null || !read.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) ||
                string.IsNullOrEmpty(ratingLock))
                throw new InvalidOperationException("Initialize player records before claiming a match.");
            read.WriteLocks.TryGetValue(PlayerStateKeys.ActiveMatch, out string claimLock);
            try
            {
                // Null locks bypass CAS in Cloud Save. Including the existing rating
                // key makes even the first claim a conditional, atomic batch.
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
                    {
                        new SetItemBody(PlayerStateKeys.Rating, read.State.Rating, ratingLock),
                        new SetItemBody(PlayerStateKeys.ActiveMatch, claim, claimLock)
                    }));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Active match changed.", ex); }
        }

        public async Task WriteAsync(PlayerState records)
        {
            await WriteAsync(records, null);
            await WriteDisciplineDefault(records);
            await WriteWalletDefault(records);
        }

        public async Task WriteDisciplineAsync(DisciplineRecord discipline, LockedPlayerState read)
        {
            read.WriteLocks.TryGetValue(PlayerStateKeys.Discipline, out string token);
            var items = new List<SetItemBody> { new SetItemBody(PlayerStateKeys.Discipline, discipline, token) };
            if (string.IsNullOrEmpty(token))
            {
                // Cloud Save has no create-if-absent. Fence first creation with
                // rating, as claims do; later discipline writes use only its key.
                if (read.State.Discipline != null || read.State.Rating == null ||
                    !read.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) || string.IsNullOrEmpty(ratingLock))
                    throw new InvalidOperationException("Initialize player records before writing discipline.");
                items.Add(new SetItemBody(PlayerStateKeys.Rating, read.State.Rating, ratingLock));
            }
            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(items));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Discipline changed.", ex); }
        }

        private async Task WriteDisciplineDefault(PlayerState records)
        {
            if (records.Discipline == null) return;
            for (int attempt = 0; ; attempt++)
            {
                var read = await ReadDisciplineAsync();
                if (read.State.Discipline != null)
                {
                    records.Discipline = read.State.Discipline;
                    return;
                }
                try { await WriteDisciplineAsync(records.Discipline, read); return; }
                catch (RecordConflictException) when (attempt + 1 < 3) { }
            }
        }

        // Created like discipline: fenced by the rating lock, so two first-time
        // calls cannot both create it and the later one cannot overwrite a grant.
        private async Task WriteWalletDefault(PlayerState records)
        {
            if (records.Wallet == null) return;
            for (int attempt = 0; ; attempt++)
            {
                var read = await ReadLockedAsync(new List<string> { PlayerStateKeys.Wallet, PlayerStateKeys.Rating });
                if (read.State.Wallet != null)
                {
                    records.Wallet = read.State.Wallet;
                    return;
                }
                try { await CreateWalletAsync(records.Wallet, read); return; }
                catch (RecordConflictException) when (attempt + 1 < 3) { }
            }
        }

        private async Task CreateWalletAsync(WalletRecord wallet, LockedPlayerState read)
        {
            if (read.State.Rating == null || !read.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) ||
                string.IsNullOrEmpty(ratingLock))
                throw new InvalidOperationException("Initialize player records before creating a wallet.");
            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
                    {
                        new SetItemBody(PlayerStateKeys.Rating, read.State.Rating, ratingLock),
                        new SetItemBody(PlayerStateKeys.Wallet, wallet, null)
                    }));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Wallet created concurrently.", ex); }
        }

        // Retain the rating lock from the SAME read as the absent profile. A fresh
        // rating read at write time would not fence concurrent first-handle writes.
        private LockedPlayerState profileRead;

        public async Task<(ProfileRecord Profile, string ProfileLock, WalletRecord Wallet, string WalletLock)> ReadProfileWalletLockedAsync()
        {
            profileRead = await ReadLockedAsync(new List<string>
                { PlayerStateKeys.Profile, PlayerStateKeys.Wallet, PlayerStateKeys.Rating });
            profileRead.WriteLocks.TryGetValue(PlayerStateKeys.Profile, out string profileLock);
            profileRead.WriteLocks.TryGetValue(PlayerStateKeys.Wallet, out string walletLock);
            return (profileRead.State.Profile, profileLock, profileRead.State.Wallet, walletLock);
        }

        public async Task WriteProfileWalletLockedAsync(ProfileRecord profile, string profileLock, WalletRecord wallet, string walletLock)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var items = new List<SetItemBody>();
            if (profileLock == null)
            {
                if (profileRead == null || profileRead.State.Profile != null || profileRead.State.Rating == null ||
                    !profileRead.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) || string.IsNullOrEmpty(ratingLock))
                    throw new InvalidOperationException("Read an absent profile on an initialized player before creating a handle.");
                items.Add(new SetItemBody(PlayerStateKeys.Rating, profileRead.State.Rating, ratingLock));
            }
            else if (profileLock.Length == 0)
                throw new InvalidOperationException("A profile write needs the lock from its read.");
            items.Add(new SetItemBody(PlayerStateKeys.Profile, profile, profileLock));
            if (wallet != null)
            {
                if (string.IsNullOrEmpty(walletLock))
                    throw new InvalidOperationException("A wallet write needs the lock from its read.");
                items.Add(new SetItemBody(PlayerStateKeys.Wallet, wallet, walletLock));
            }
            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(items));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Profile or wallet changed.", ex); }
        }

        public async Task<(WalletRecord Wallet, string WriteLock)> ReadWalletLockedAsync()
        {
            var read = await ReadLockedAsync(new List<string> { PlayerStateKeys.Wallet });
            read.WriteLocks.TryGetValue(PlayerStateKeys.Wallet, out string walletLock);
            return (read.State.Wallet, walletLock);
        }

        public async Task WriteWalletLockedAsync(WalletRecord wallet, string expectedWriteLock)
        {
            // A null lock would bypass the check and could overwrite a concurrent change.
            if (string.IsNullOrEmpty(expectedWriteLock))
                throw new InvalidOperationException("A wallet write needs the lock from its read.");
            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
                    {
                        new SetItemBody(PlayerStateKeys.Wallet, wallet, expectedWriteLock)
                    }));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Wallet changed.", ex); }
        }

        public async Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync()
        {
            var read = await ReadWholeStateAsync();
            read.WriteLocks.TryGetValue(PlayerStateKeys.Inventory, out string inventoryLock);
            return (read.State, inventoryLock);
        }

        // Other records here are missing defaults only. Existing inventory must
        // use the lock from the same read as the normalization/grant decision.
        public async Task WriteDefaultsLockedAsync(PlayerState records, string inventoryWriteLock)
        {
            await WriteAsync(records, PlayerStateKeys.All.ToDictionary(key => key,
                key => key == PlayerStateKeys.Inventory ? inventoryWriteLock : null));
            await WriteDisciplineDefault(records);
            await WriteWalletDefault(records);
        }

        // Writes Inventory alone, using only the Inventory write lock: a stale
        // lock (a settlement clamped the same key meanwhile) conflicts here.
        public Task WriteInventoryLockedAsync(InventoryRecord inventory, string expectedWriteLock) =>
            WriteAsync(new PlayerState { Inventory = inventory },
                new Dictionary<string, string> { [PlayerStateKeys.Inventory] = expectedWriteLock });

        public Task WriteForSettlementAsync(PlayerState state, IReadOnlyDictionary<string, string> expectedWriteLocks)
        {
            if (state?.Rating == null || state.Rank == null || state.Inventory == null || state.History == null)
                throw new ArgumentException("Settlement requires all four player records.", nameof(state));
            foreach (string key in PlayerStateKeys.All)
                if (expectedWriteLocks == null || !expectedWriteLocks.TryGetValue(key, out string token) || string.IsNullOrEmpty(token))
                    throw new ArgumentException("Settlement requires every player record's write lock.", nameof(expectedWriteLocks));
            return WriteAsync(state, expectedWriteLocks);
        }

        private async Task WriteAsync(PlayerState records, IReadOnlyDictionary<string, string> locks)
        {
            var items = new List<SetItemBody>();
            if (records.Rating != null) items.Add(new SetItemBody(PlayerStateKeys.Rating, records.Rating, locks?[PlayerStateKeys.Rating]));
            if (records.Rank != null) items.Add(new SetItemBody(PlayerStateKeys.Rank, records.Rank, locks?[PlayerStateKeys.Rank]));
            if (records.Inventory != null) items.Add(new SetItemBody(PlayerStateKeys.Inventory, records.Inventory, locks?[PlayerStateKeys.Inventory]));
            if (records.History != null) items.Add(new SetItemBody(PlayerStateKeys.History, records.History, locks?[PlayerStateKeys.History]));
            if (items.Count == 0) return;

            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(
                    context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(items));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Player records changed.", ex); }
        }

        // Item.Value arrives as whatever the API client deserialized (a JObject
        // for JSON objects); a round trip through Newtonsoft gives the record type.
        private static T Convert<T>(object value)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        }
    }
}
