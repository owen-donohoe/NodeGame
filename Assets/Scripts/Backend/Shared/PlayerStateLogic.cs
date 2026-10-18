using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// Where a player's stored records are read from and written to. Cloud Code
    /// implements it over Cloud Save's protected access class; the local fake
    /// over memory. A record that has never been written reads as null.
    /// </summary>
    public interface IPlayerRecordStore
    {
        Task<PlayerState> ReadAsync();

        /// <summary>Writes every non-null record in <paramref name="records"/> and leaves the rest alone.</summary>
        Task WriteAsync(PlayerState records);
    }

    /// <summary>
    /// Server rules for player state that do not depend on where it is stored.
    /// Cloud Code runs these against Cloud Save, and the local fake runs the
    /// same code against memory, so the two cannot drift.
    /// </summary>
    public static class PlayerStateLogic
    {
        /// <summary>
        /// The whole state, creating first-time defaults for any record the
        /// player does not have yet. Only the missing records are written, so
        /// calling this never resets anything.
        /// </summary>
        public static async Task<PlayerState> GetOrCreateAsync(IPlayerRecordStore store,
            Func<PlayerState, bool> updateInventory = null)
        {
            PlayerState stored = await store.ReadAsync() ?? new PlayerState();
            var created = ApplyDefaults(stored, updateInventory);
            if (created.Rating != null || created.Rank != null || created.Inventory != null
                || created.History != null || created.Discipline != null || created.Wallet != null)
                await store.WriteAsync(created);
            return stored;
        }

        /// <summary>Apply defaults to a detached read and return only records requiring a write.</summary>
        public static PlayerState ApplyDefaults(PlayerState stored, Func<PlayerState, bool> updateInventory = null)
        {
            var created = new PlayerState();

            if (stored.Rating == null) stored.Rating = created.Rating = PlayerStateDefaults.Rating();
            if (stored.Rank == null) stored.Rank = created.Rank = PlayerStateDefaults.Rank();
            if (stored.Inventory == null) stored.Inventory = created.Inventory = PlayerStateDefaults.Inventory();
            if (stored.History == null) stored.History = created.History = PlayerStateDefaults.History();
            if (stored.Discipline == null) stored.Discipline = created.Discipline = new DisciplineRecord();
            if (stored.Wallet == null) stored.Wallet = created.Wallet = WalletLedger.Starting();

            if (NormalizeInventory(stored.Inventory) | DistrictMigration.Apply(stored)) created.Inventory = stored.Inventory;
            if (updateInventory != null && updateInventory(stored)) created.Inventory = stored.Inventory;

            return created;
        }

        /// <summary>Old records may predate Equipped or have null collections.</summary>
        public static bool NormalizeInventory(InventoryRecord inventory)
        {
            bool changed = false;
            if (inventory.OwnedVariants == null) { inventory.OwnedVariants = new List<string>(); changed = true; }
            if (inventory.OwnedSkins == null) { inventory.OwnedSkins = new List<string>(); changed = true; }
            if (inventory.Equipped == null) { inventory.Equipped = new EquippedRecord(); changed = true; }
            if (inventory.Equipped.Variants == null)
            { inventory.Equipped.Variants = new Dictionary<string, string>(StringComparer.Ordinal); changed = true; }
            if (inventory.Equipped.Skins == null)
            { inventory.Equipped.Skins = new Dictionary<string, string>(StringComparer.Ordinal); changed = true; }
            return changed;
        }
    }

    public enum WalletApplyResult { Applied, Duplicate, Insufficient }

    /// <summary>
    /// The only way a wallet changes. Pure: the server reads the wallet with its
    /// write lock, applies here and writes back with that lock, so a concurrent
    /// change conflicts instead of being lost.
    /// </summary>
    public static class WalletLedger
    {
        public const int CurrentVersion = 1;

        // Placeholders until the economy is tuned: a high coin start so early
        // progress feels fast, a low premium start so premium stays a saving goal.
        public const long StartingCoins = 5000;
        public const long StartingPremium = 25;

        /// <summary>
        /// How many transactions are kept. A request ID repeated after it has been
        /// evicted would apply again, so callers retry within minutes, not weeks.
        /// </summary>
        public const int RecentLimit = 50;

        public static WalletRecord Starting()
        {
            return new WalletRecord { Coins = StartingCoins, Premium = StartingPremium };
        }

        /// <summary>
        /// Applies the transaction unless its ID was already applied or it would take
        /// either balance below zero. Refusals leave the wallet unchanged.
        /// </summary>
        public static WalletApplyResult Apply(WalletRecord wallet, WalletTransaction transaction)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (string.IsNullOrEmpty(transaction.Id)) throw new ArgumentException("A transaction needs a request ID.", nameof(transaction));
            if (string.IsNullOrEmpty(transaction.Reason)) throw new ArgumentException("A transaction needs a reason.", nameof(transaction));
            if (wallet.Recent == null) wallet.Recent = new List<WalletTransaction>();

            foreach (WalletTransaction applied in wallet.Recent)
                if (applied.Id == transaction.Id) return WalletApplyResult.Duplicate;

            long coins, premium;
            try
            {
                coins = checked(wallet.Coins + transaction.Coins);
                premium = checked(wallet.Premium + transaction.Premium);
            }
            catch (OverflowException) { return WalletApplyResult.Insufficient; }
            if (coins < 0 || premium < 0) return WalletApplyResult.Insufficient;

            wallet.Coins = coins;
            wallet.Premium = premium;
            wallet.Recent.Add(transaction);
            if (wallet.Recent.Count > RecentLimit) wallet.Recent.RemoveRange(0, wallet.Recent.Count - RecentLimit);
            return WalletApplyResult.Applied;
        }
    }

    /// <summary>What a brand-new player starts with.</summary>
    public static class PlayerStateDefaults
    {
        // Glicko-2's standard starting point. NodeWar.Progression.Rating uses the
        // same numbers, and a test holds the two together.
        public const double InitialRating = 1500;
        public const double InitialRd = 350;
        public const double InitialSigma = 0.06;

        public static RatingRecord Rating()
        {
            return new RatingRecord { R = InitialRating, Rd = InitialRd, Sigma = InitialSigma };
        }

        public static RankRecord Rank()
        {
            return new RankRecord();
        }

        public static InventoryRecord Inventory()
        {
            return new InventoryRecord
            {
                OwnedVariants = new List<string>(),
                OwnedSkins = new List<string>(),
                EquippedSuitIDs = new string[0],
                EquippedNodeIDs = new string[0],
                Equipped = new EquippedRecord
                {
                    Variants = new Dictionary<string, string>(StringComparer.Ordinal),
                    Skins = new Dictionary<string, string>(StringComparer.Ordinal)
                }
            };
        }

        public static HistoryRecord History()
        {
            return new HistoryRecord { MatchIds = new List<string>() };
        }
    }
}
