using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>
    /// Everything the server keeps about one player, as the client receives it.
    ///
    /// Each record is its own Cloud Save key in the protected access class: the
    /// player can read it, and only Cloud Code can write it. The client never
    /// edits these and sends them back; it asks the server to act, and shows
    /// whatever state the server returns.
    ///
    /// Public fields rather than properties because Unity's convention is
    /// fields, and Newtonsoft (used by Cloud Code on both ends) serializes
    /// them by name either way. Renaming a field renames stored data: add a new
    /// field instead.
    ///
    /// This folder compiles into Unity (NodeWar.Backend.Shared.asmdef, no engine
    /// references) and into the Cloud Code module (dotnet/NodeWarCloud). Keep it
    /// free of UnityEngine and of anything newer than C# 9.
    /// </summary>
    public sealed class PlayerState
    {
        public RatingRecord Rating;
        public RankRecord Rank;
        public InventoryRecord Inventory;
        public HistoryRecord History;
        public ActiveMatchRecord ActiveMatch;
        public DisciplineRecord Discipline;
        public WalletRecord Wallet;
        public ProfileRecord Profile;
    }

    /// <summary>
    /// The player's public identity, shown as Name#TAG. Uniqueness is on
    /// (NameNorm, Tag) and is reserved in the server's handle registry; this
    /// record is the source of truth for who holds a handle.
    /// </summary>
    public sealed class ProfileRecord
    {
        public int Version = 1;
        /// <summary>As the player typed it: 3-16 of A-Z a-z 0-9 - _.</summary>
        public string Name;
        /// <summary>1-4 of A-Z 0-9, upper case.</summary>
        public string Tag;
        /// <summary>Name lowercased; the uniqueness key with Tag.</summary>
        public string NameNorm;
        /// <summary>Server time of the last free rename, Unix seconds. 0 means never.</summary>
        public long LastFreeRenameUnixSeconds;
    }

    /// <summary>Hidden Glicko-2 rating. Never shown to the player.</summary>
    public sealed class RatingRecord
    {
        public double R;
        public double Rd;
        public double Sigma;

        /// <summary>Server time of the last rated match, Unix seconds. 0 means never.</summary>
        public long LastMatchUnixSeconds;

        /// <summary>
        /// Idempotency guard for settlement, newest first, capped at 200. Separate
        /// from HistoryRecord.MatchIds (capped at 20, display-oriented): a player
        /// who plays enough matches to evict one from history must still not be
        /// settled twice for it. Written in the same batch as the settlement.
        /// </summary>
        public List<string> SettledMatchIds;
    }

    /// <summary>Visible rank: RR and the arena it places the player in.</summary>
    public sealed class RankRecord
    {
        public int RR;
        public int Arena;
        public int HighestArena;
    }

    /// <summary>
    /// What the player owns and has equipped. Item IDs are stable catalog
    /// strings. Equipped entries are keyed by base ID, independently of draft slots.
    /// </summary>
    public sealed class InventoryRecord
    {
        public List<string> OwnedVariants;
        public List<string> OwnedSkins;
        // Obsolete stored fields: retained for compatibility, never read by equip logic.
        public string[] EquippedSuitIDs;
        public string[] EquippedNodeIDs;
        public EquippedRecord Equipped;
    }

    public sealed class EquippedRecord
    {
        public Dictionary<string, string> Variants; // baseId -> variantId
        public Dictionary<string, string> Skins; // baseId -> skinId
    }

    /// <summary>Most recent match IDs, newest first.</summary>
    public sealed class HistoryRecord
    {
        public List<string> MatchIds;
    }

    public sealed class ActiveMatchRecord
    {
        public string matchId;
        public long expiresUnixSeconds;
    }

    /// <summary>Disconnect strikes and recent non-reports, written separately from settlement.</summary>
    public sealed class DisciplineRecord
    {
        public int Level;
        public long LastStrikeUnixSeconds;
        public long LastDecayUnixSeconds;
        public long BlockedUntilUnixSeconds;
        public List<long> NonReports = new List<long>();
        public List<string> StruckMatchIds = new List<string>();
    }

    /// <summary>
    /// Both currencies and the transactions that produced them. Coins pay for
    /// normal progression; Premium is the bought currency, stored under a
    /// neutral name so its display name can change without touching stored data.
    /// The balances are the result of Recent; written only by the server through
    /// WalletLedger, never by Settlement's four-record batch.
    /// </summary>
    public sealed class WalletRecord
    {
        public int Version = WalletLedger.CurrentVersion;
        public long Coins;
        public long Premium;

        /// <summary>Newest last, capped at WalletLedger.RecentLimit. Also the idempotency guard.</summary>
        public List<WalletTransaction> Recent = new List<WalletTransaction>();
    }

    /// <summary>One grant or spend. Amounts are signed: negative spends.</summary>
    public sealed class WalletTransaction
    {
        /// <summary>The caller's request ID; a repeat is applied once.</summary>
        public string Id;
        /// <summary>Why, for support and audit: "admin:...", "rename", "crate", ...</summary>
        public string Reason;
        public long Coins;
        public long Premium;
        public long AtUnixSeconds;
    }

    /// <summary>The Cloud Save key each record is stored under. Never rename one.</summary>
    public static class PlayerStateKeys
    {
        public const string Rating = "rating";
        public const string Rank = "rank";
        public const string Inventory = "inventory";
        public const string History = "history";
        public const string ActiveMatch = "activeMatch";
        public const string Wallet = "wallet";
        public const string Profile = "profile";
        public const string Discipline = "discipline";

        // The four state records; claims are independent of settlement writes.
        public static readonly string[] All = { Rating, Rank, Inventory, History };
    }
}
