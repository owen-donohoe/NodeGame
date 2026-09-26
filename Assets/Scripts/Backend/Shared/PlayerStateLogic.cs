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
            var created = new PlayerState();

            if (stored.Rating == null) stored.Rating = created.Rating = PlayerStateDefaults.Rating();
            if (stored.Rank == null) stored.Rank = created.Rank = PlayerStateDefaults.Rank();
            if (stored.Inventory == null) stored.Inventory = created.Inventory = PlayerStateDefaults.Inventory();
            if (stored.History == null) stored.History = created.History = PlayerStateDefaults.History();

            if (NormalizeInventory(stored.Inventory)) created.Inventory = stored.Inventory;
            if (updateInventory != null && updateInventory(stored)) created.Inventory = stored.Inventory;

            if (created.Rating != null || created.Rank != null
                || created.Inventory != null || created.History != null)
                await store.WriteAsync(created);

            return stored;
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
