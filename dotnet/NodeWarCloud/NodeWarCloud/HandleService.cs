using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    public interface IProfileStore : IPlayerRecordStore
    {
        Task<(ProfileRecord Profile, string ProfileLock, WalletRecord Wallet, string WalletLock)> ReadProfileWalletLockedAsync();
        Task WriteProfileWalletLockedAsync(ProfileRecord profile, string profileLock, WalletRecord wallet, string walletLock);
    }

    public sealed class HandleRefusedException : Exception
    {
        public HandleRefusedException(string message) : base(message) { }
    }

    public sealed class HandleService
    {
        private readonly IHandleRegistryStore registry;
        private readonly Func<string, IProfileStore> storeFor;
        private readonly IEnumerable<string> blockedNames;
        private readonly Func<string> randomTag;

        public HandleService(IHandleRegistryStore registry, Func<string, IProfileStore> storeFor,
            IEnumerable<string> blockedNames, Func<string> randomTag = null)
        {
            this.registry = registry;
            this.storeFor = storeFor;
            this.blockedNames = blockedNames;
            this.randomTag = randomTag ?? RandomTag;
        }

        private static string RandomTag()
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var chars = new char[4];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
            return new string(chars);
        }

        public Task<PlayerState> SetHandleAsync(string callerId, string name, string tag, string requestId, long now) =>
            SetAsync(callerId, name, tag, requestId, now, false);

        // Called only after AdminHandleService authenticates the operator.
        internal Task<PlayerState> AdminSetHandleAsync(string targetPlayerId, string name, string tag, long now) =>
            SetAsync(targetPlayerId, name, tag, null, now, true);

        public async Task<ProfileRecord> GetPublicProfileAsync(string playerId)
        {
            var profile = (await storeFor(playerId).ReadProfileWalletLockedAsync()).Profile;
            return profile == null ? null : new ProfileRecord { Name = profile.Name, Tag = profile.Tag };
        }

        private async Task<PlayerState> SetAsync(string playerId, string name, string tag, string requestId, long now, bool admin)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new HandleRefusedException("Sign in before choosing a handle.");
            if (!HandleRules.IsValidName(name))
                throw new HandleRefusedException("Name must be 3-16 letters, numbers, hyphens or underscores.");
            string norm = HandleRules.NormalizeName(name);
            if (!admin && HandleRules.IsBlocked(norm, blockedNames))
                throw new HandleRefusedException("Please choose another name.");
            if (!admin && string.IsNullOrWhiteSpace(requestId))
                throw new HandleRefusedException("A request ID is required.");
            bool automatic = string.IsNullOrEmpty(tag);
            tag = HandleRules.NormalizeTag(tag);
            if (!automatic && !HandleRules.IsValidTag(tag))
                throw new HandleRefusedException("Tag must be 1-4 letters or numbers.");
            var store = storeFor(playerId);
            for (int candidate = 0; candidate < (automatic ? 10 : 1); candidate++)
            {
                string chosenTag = automatic ? randomTag() : tag;
                if (!HandleRules.IsValidTag(chosenTag)) throw new InvalidOperationException("Invalid generated tag.");
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var read = await store.ReadProfileWalletLockedAsync();
                    var old = read.Profile;
                    string key = HandleRegistry.Key(norm, chosenTag);
                    string oldKey = old == null ? null : HandleRegistry.Key(old.NameNorm, old.Tag);
                    bool casingOnly = key == oldKey;
                    var profile = new ProfileRecord
                    {
                        Version = old?.Version ?? 1, Name = name, NameNorm = norm, Tag = chosenTag,
                        LastFreeRenameUnixSeconds = old?.LastFreeRenameUnixSeconds ?? 0
                    };
                    WalletRecord paidWallet = null;
                    if (!admin && old != null && !casingOnly)
                    {
                        if (old.LastFreeRenameUnixSeconds == 0 ||
                            (now >= old.LastFreeRenameUnixSeconds && now - old.LastFreeRenameUnixSeconds >= HandleRules.FreeRenameIntervalSeconds))
                            profile.LastFreeRenameUnixSeconds = now;
                        else
                        {
                            paidWallet = read.Wallet ?? throw new HandleRefusedException("Sign in before renaming your handle.");
                            var result = WalletLedger.Apply(paidWallet, new WalletTransaction
                            {
                                Id = requestId, Reason = "rename", Premium = -HandleRules.PaidRenamePremium, AtUnixSeconds = now
                            });
                            if (result == WalletApplyResult.Insufficient) throw new HandleRefusedException("You need 50 premium to rename now.");
                            if (result == WalletApplyResult.Duplicate) throw new HandleRefusedException("That rename request was already used.");
                        }
                    }
                    bool reserved = false;
                    try
                    {
                        if (!casingOnly)
                        {
                            if (!await ReserveAsync(playerId, norm, key)) break;
                            reserved = true;
                        }
                        await store.WriteProfileWalletLockedAsync(profile, read.ProfileLock, paidWallet, read.WalletLock);
                    }
                    catch (RecordConflictException)
                    {
                        if (reserved) await ReleaseBestEffortAsync(playerId, norm, key);
                        if (attempt + 1 == 3) throw new HandleRefusedException("Your handle changed. Please try again.");
                        continue;
                    }
                    catch
                    {
                        if (reserved) await ReleaseBestEffortAsync(playerId, norm, key);
                        throw;
                    }
                    if (!casingOnly && old != null) await ReleaseBestEffortAsync(playerId, old.NameNorm, oldKey);
                    return await store.ReadAsync();
                }
                if (!automatic) throw new HandleRefusedException("That handle is taken.");
            }
            throw new HandleRefusedException("No free tag found. Please try again.");
        }

        private async Task<bool> ReserveAsync(string playerId, string norm, string key)
        {
            string shard = HandleRegistry.Shard(norm);
            var read = await registry.ReadShardAsync(shard);
            if (read.Entries == null) throw new HandleRefusedException("Handle registry not initialized.");
            if (read.Entries.TryGetValue(key, out string owner) && owner != playerId)
            {
                var held = (await storeFor(owner).ReadProfileWalletLockedAsync()).Profile;
                // The profile is authoritative. This reclaims completed stale reservations;
                // the cross-item pending-reservation race requires a stronger storage protocol.
                if (held != null && HandleRegistry.Key(held.NameNorm, held.Tag) == key) return false;
            }
            read.Entries[key] = playerId;
            await registry.WriteShardAsync(shard, read.Entries, read.WriteLock);
            return true;
        }

        private async Task ReleaseBestEffortAsync(string playerId, string norm, string key)
        {
            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    // A concurrent rename for this player may have committed the key.
                    var held = (await storeFor(playerId).ReadProfileWalletLockedAsync()).Profile;
                    if (held != null && HandleRegistry.Key(held.NameNorm, held.Tag) == key) return;
                    string shard = HandleRegistry.Shard(norm);
                    var read = await registry.ReadShardAsync(shard);
                    if (read.Entries == null || !read.Entries.TryGetValue(key, out string owner) || owner != playerId) return;
                    read.Entries.Remove(key);
                    try { await registry.WriteShardAsync(shard, read.Entries, read.WriteLock); return; }
                    catch (RecordConflictException) when (attempt + 1 < 3) { }
                }
            }
            catch (Exception) { /* Stale reservations are reclaimed by ReserveAsync. */ }
        }
    }

    public sealed class AdminHandleService
    {
        private readonly IAdminDirectory admins;
        private readonly HandleService handles;
        private readonly IHandleRegistryStore registry;

        public AdminHandleService(IAdminDirectory admins, HandleService handles, IHandleRegistryStore registry)
        { this.admins = admins; this.handles = handles; this.registry = registry; }

        public async Task<PlayerState> SetHandleAsync(string callerId, string targetPlayerId, string name, string tag, long now)
        {
            if (!await admins.IsAdminAsync(callerId)) throw new UnauthorizedAccessException("Admin only.");
            if (string.IsNullOrWhiteSpace(targetPlayerId)) throw new HandleRefusedException("Choose a target player.");
            return await handles.AdminSetHandleAsync(targetPlayerId, name, tag, now);
        }

        public async Task<int> InitRegistryAsync(string callerId)
        {
            if (!await admins.IsAdminAsync(callerId)) throw new UnauthorizedAccessException("Admin only.");
            return await registry.CreateEmptyShardsAsync();
        }
    }
}
