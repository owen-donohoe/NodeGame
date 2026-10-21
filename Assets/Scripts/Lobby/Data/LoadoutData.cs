namespace NodeWar.Lobby
{
    /// <summary>
    /// A player's pre-match selection: which suits and which districts they
    /// bring into the draft.
    ///
    /// Arrays always contain three suits and five districts, including fixed
    /// items and blank choices. Legacy 2/2 saves gain the starter items once.
    ///
    /// This struct crosses the wire (DraftSerializer) and persists to disk
    /// (PlayerProfile, via JsonUtility). Both encode the arrays with an
    /// explicit count. Remote data preserves its choices; local arena-aware
    /// operations restore any missing locks.
    ///
    /// Because it is a struct, `new LoadoutData()` leaves both arrays null.
    /// Anything that reads the arrays should go through
    /// <see cref="Normalized"/> first.
    /// </summary>
    [System.Serializable]
    public struct LoadoutData
    {
        /// <summary>Total suit capacity, including locked items.</summary>
        public const int SuitSlots = 3;

        /// <summary>Total district capacity, including locked items.</summary>
        public const int DistrictSlots = 5;

        public string[] suitIDs;
        public string[] districtIDs;

        /// <summary>Length of <see cref="suitEras"/>: one entry per SuitType value.</summary>
        public const int SuitEraSlots = (int)NodeWar.Simulation.SuitType.Minion + 1;

        /// <summary>Length of <see cref="districtEras"/>: one entry per DistrictType value.</summary>
        public const int DistrictEraSlots = (int)NodeWar.Simulation.DistrictType.Workshop + 1;

        /// <summary>
        /// The era of every suit and district this player fields, indexed by
        /// (int)SuitType and (int)DistrictType. The server owns what is
        /// equipped; these are filled from it when a match launches
        /// (LoadoutEquipment) and then travel to the opponent with the loadout.
        /// </summary>
        public int[] suitEras;
        public int[] districtEras;

        /// <summary>
        /// Catalog skin IDs the player has equipped. Cosmetic only: shown to
        /// the opponent and recorded for replays, never read by the simulation.
        /// </summary>
        public string[] skinIDs;

        /// <summary>
        /// A fresh loadout containing Warrior and Farm/Mine/Town, with blank choices.
        /// </summary>
        public static LoadoutData CreateEmpty()
        {
            return CreateEmpty(1);
        }

        public static LoadoutData CreateEmpty(int arena)
        {
            return Normalized(default, arena);
        }

        /// <summary>
        /// A detached 3/5 copy. Legacy short saves gain defaults and retain up
        /// to two choices per side. Canonical blanks stay blank. Wire readers
        /// validate counts first so migration cannot mask malformed packets.
        /// </summary>
        public static LoadoutData Normalized(LoadoutData source)
        {
            bool high = source.suitIDs != null && source.suitIDs.Length >= SuitSlots &&
                source.districtIDs != null && source.districtIDs.Length >= DistrictSlots;
            if (high) return Resize(source, SuitSlots, DistrictSlots);
            // Low-shape choices follow the fixed defaults. Preserve up to two
            // distinct choices per side; repeated normalization is idempotent.
            var result = Resize(source, SuitSlots, DistrictSlots);
            result.suitIDs = WithDefaults(source.suitIDs, SuitSlots, NodeWar.Backend.LoadoutCapacityPolicy.DefaultSuits());
            result.districtIDs = WithDefaults(source.districtIDs, DistrictSlots,
                NodeWar.Backend.LoadoutCapacityPolicy.DefaultDistricts());
            return result;
        }

        /// <summary>Local editing: migrate legacy choices, then restore the current era's locks.</summary>
        public static LoadoutData Normalized(LoadoutData source, int arena)
        {
            var result = Normalized(source);
            result.suitIDs = WithLocks(result.suitIDs, NodeWar.Backend.LoadoutCapacityPolicy.LockedSuits(arena));
            result.districtIDs = WithLocks(result.districtIDs, NodeWar.Backend.LoadoutCapacityPolicy.LockedDistricts(arena));
            return result;
        }

        private static string[] WithDefaults(string[] source, int count, string[] defaults)
        {
            var result = NormalizeSlots(null, count);
            System.Array.Copy(defaults, result, defaults.Length);
            int next = defaults.Length;
            if (source != null)
                for (int i = 0; i < System.Math.Min(2, source.Length) && next < count; i++)
                    if (!string.IsNullOrEmpty(source[i]) && System.Array.IndexOf(result, source[i]) < 0)
                        result[next++] = source[i];
            return result;
        }

        private static string[] WithLocks(string[] source, string[] locks)
        {
            // Existing choices keep their slots where possible. Missing fixed
            // defaults use blanks first, then displace the last editable item.
            foreach (string id in locks)
            {
                if (System.Array.IndexOf(source, id) >= 0) continue;
                int slot = System.Array.FindIndex(source, string.IsNullOrEmpty);
                if (slot < 0) slot = System.Array.FindLastIndex(source, value => System.Array.IndexOf(locks, value) < 0);
                if (slot >= 0) source[slot] = id;
            }
            return source;
        }

        private static LoadoutData Resize(LoadoutData source, int suits, int districts)
        {
            return new LoadoutData
            {
                suitIDs = NormalizeSlots(source.suitIDs, suits),
                districtIDs = NormalizeSlots(source.districtIDs, districts),
                suitEras = NormalizeEras(source.suitEras, SuitEraSlots),
                districtEras = NormalizeEras(source.districtEras, DistrictEraSlots),
                skinIDs = NormalizeSkins(source.skinIDs)
            };
        }

        /// <summary>
        /// Exactly <paramref name="count"/> long; missing entries and eras
        /// outside 0..EraCount-1 are era 0, so a peer can never field an era
        /// this build has no numbers for.
        /// </summary>
        private static int[] NormalizeEras(int[] source, int count)
        {
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int era = source != null && i < source.Length ? source[i] : 0;
                result[i] = era >= 0 && era < NodeWar.Simulation.GameBalanceData.EraCount ? era : 0;
            }
            return result;
        }

        private static string[] NormalizeSkins(string[] source)
        {
            if (source == null) return new string[0];
            var kept = new System.Collections.Generic.List<string>(source.Length);
            for (int i = 0; i < source.Length; i++)
                if (!string.IsNullOrEmpty(source[i])) kept.Add(source[i]);
            return kept.ToArray();
        }

        private static string[] NormalizeSlots(string[] source, int count)
        {
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
            {
                bool present = source != null && i < source.Length && source[i] != null;
                result[i] = present ? source[i] : "";
            }
            return result;
        }
    }

    public enum DistrictCategory
    {
        Generic,    // Available in base draft pool for all players
        Selectable  // Only available when selected in player loadout
    }

    public enum GameMode
    {
        OneVsOne,
        Bot,
        Testing,
        Locked
    }
}
