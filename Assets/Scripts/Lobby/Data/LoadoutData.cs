namespace NodeWar.Lobby
{
    /// <summary>
    /// A player's pre-match selection: which suits and which districts they
    /// bring into the draft.
    ///
    /// Array lengths encode low (2/2) or high (3/5) chosen capacity, including
    /// blank slots. SuitSlots and DistrictSlots are maxima, not effective counts.
    ///
    /// This struct crosses the wire (DraftSerializer) and persists to disk
    /// (PlayerProfile, via JsonUtility). Both encode the arrays with an
    /// explicit count. Remote data preserves its own canonical mode; only local
    /// arena-aware operations resize it.
    ///
    /// Because it is a struct, `new LoadoutData()` leaves both arrays null.
    /// Anything that reads the arrays should go through
    /// <see cref="Normalized"/> first.
    /// </summary>
    [System.Serializable]
    public struct LoadoutData
    {
        /// <summary>Maximum chosen suit capacity; low arenas use two slots plus free Warrior.</summary>
        public const int SuitSlots = 3;

        /// <summary>Maximum chosen district capacity; low arenas use two slots plus free base pieces.</summary>
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
        /// A low-arena loadout with both arrays allocated at two slots and
        /// every entry an empty string. This is what an unset loadout looks
        /// like; it is never null-armed.
        /// </summary>
        public static LoadoutData CreateEmpty()
        {
            return CreateEmpty(1);
        }

        public static LoadoutData CreateEmpty(int arena)
        {
            return Resize(default, NodeWar.Backend.LoadoutCapacityPolicy.SuitCapacity(arena),
                NodeWar.Backend.LoadoutCapacityPolicy.DistrictCapacity(arena));
        }

        /// <summary>
        /// A detached copy preserving canonical 2/2 or 3/5 shape. Null/default
        /// and noncanonical saved shapes become low mode, retaining the first
        /// two choices; null entries become empty strings. Wire readers validate
        /// counts before calling this, so legacy save migration cannot mask a bad packet.
        /// </summary>
        public static LoadoutData Normalized(LoadoutData source)
        {
            bool high = NodeWar.Backend.LoadoutCapacityPolicy.IsHighShape(
                source.suitIDs == null ? 0 : source.suitIDs.Length,
                source.districtIDs == null ? 0 : source.districtIDs.Length);
            return Resize(source, high ? SuitSlots : 2, high ? DistrictSlots : 2);
        }

        /// <summary>Local resizing only. First migrate legacy shapes so their third suit stays dropped.</summary>
        public static LoadoutData Normalized(LoadoutData source, int arena)
        {
            return Resize(Normalized(source), NodeWar.Backend.LoadoutCapacityPolicy.SuitCapacity(arena),
                NodeWar.Backend.LoadoutCapacityPolicy.DistrictCapacity(arena));
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
