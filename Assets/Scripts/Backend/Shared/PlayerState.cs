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
    }

    /// <summary>Hidden Glicko-2 rating. Never shown to the player.</summary>
    public sealed class RatingRecord
    {
        public double R;
        public double Rd;
        public double Sigma;

        /// <summary>Server time of the last rated match, Unix seconds. 0 means never.</summary>
        public long LastMatchUnixSeconds;
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

    /// <summary>The Cloud Save key each record is stored under. Never rename one.</summary>
    public static class PlayerStateKeys
    {
        public const string Rating = "rating";
        public const string Rank = "rank";
        public const string Inventory = "inventory";
        public const string History = "history";

        public static readonly string[] All = { Rating, Rank, Inventory, History };
    }
}
