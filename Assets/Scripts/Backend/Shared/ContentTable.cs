using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>
    /// When each base enters the game and which bases every player starts with.
    /// Reaching a base's debut era adds it to the crate pools; it grants nothing
    /// by itself. Starters are owned from the first sign-in.
    ///
    /// Placeholder data: the Roadmap item "Era content table" owns the real
    /// values, and eras, districts and suits will all grow. Shared by the client
    /// and the Cloud Code module so both read one table.
    /// </summary>
    public static class ContentTable
    {
        public const int UnknownEra = -1;

        private static readonly Dictionary<string, int> debutEra = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // Era 0, gather and fight: the whole core loop.
            { "district.farm", 0 }, { "district.mine", 0 }, { "district.town", 0 }, { "district.barracks", 0 },
            { "suit.warrior", 0 }, { "suit.guardian", 0 },
            // Era 1, grow and specialise.
            { "district.village", 1 }, { "suit.scout", 1 }, { "suit.berserker", 1 },
            // Era 2, metal and logistics.
            { "district.forge", 2 }, { "district.fortress", 2 }, { "district.storehouse", 2 },
            { "district.workshop", 2 }, { "district.pier", 2 },
            // Era 3, sustain; later the power-up era that introduces side-variants.
            { "district.infirmary", 3 }, { "suit.medic", 3 },
            // Production and unused suits are never a player's choice, so everyone has them.
            { "suit.farmer", 0 }, { "suit.miner", 0 }, { "suit.smelter", 0 },
            { "suit.acolyte", 0 }, { "suit.merchant", 0 }, { "suit.watcher", 0 },
        };

        private static readonly string[] starters =
        {
            "district.barracks", "district.farm", "district.mine", "district.town",
            "suit.acolyte", "suit.farmer", "suit.guardian", "suit.merchant",
            "suit.miner", "suit.smelter", "suit.warrior", "suit.watcher",
        };

        /// <summary>Every base a new player owns, sorted by ordinal.</summary>
        public static IReadOnlyList<string> Starters => starters;

        /// <summary>The era a base enters the crate pools, or UnknownEra for a base not in the table.</summary>
        public static int DebutEra(string baseId) =>
            baseId != null && debutEra.TryGetValue(baseId, out int era) ? era : UnknownEra;

        public static bool IsStarter(string baseId) => Array.IndexOf(starters, baseId) >= 0;

        /// <summary>The bases whose debut is exactly this era, sorted by ordinal.</summary>
        public static List<string> Debuting(int era)
        {
            var bases = new List<string>();
            foreach (var entry in debutEra)
                if (entry.Value == era) bases.Add(entry.Key);
            bases.Sort(StringComparer.Ordinal);
            return bases;
        }
    }
}
