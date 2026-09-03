namespace NodeWar.Lobby
{
    /// <summary>
    /// A player's pre-match selection: which suits and which districts they
    /// bring into the draft.
    ///
    /// Array-backed rather than five flat fields, so the slot counts are data.
    /// How many of each a player gets is an open balance question, and moving
    /// it should be an edit to <see cref="SuitSlots"/> / <see cref="NodeSlots"/>
    /// rather than an edit to six call sites and a wire format.
    ///
    /// This struct crosses the wire (DraftSerializer) and persists to disk
    /// (PlayerProfile, via JsonUtility). Both encode the arrays with an
    /// explicit count, so neither needs touching when the counts change.
    ///
    /// Because it is a struct, `new LoadoutData()` leaves both arrays null.
    /// Anything that reads the arrays should go through
    /// <see cref="Normalized"/> first.
    /// </summary>
    [System.Serializable]
    public struct LoadoutData
    {
        /// <summary>Combat suits a player brings. Warrior is granted on top of these.</summary>
        public const int SuitSlots = 3;

        /// <summary>Districts a player brings into the draft pool.</summary>
        public const int NodeSlots = 2;

        public string[] suitIDs;
        public string[] nodeIDs;

        /// <summary>
        /// A loadout with both arrays allocated at the current slot counts and
        /// every entry an empty string. This is what an unset loadout looks
        /// like; it is never null-armed.
        /// </summary>
        public static LoadoutData CreateEmpty()
        {
            return Normalized(new LoadoutData());
        }

        /// <summary>
        /// A copy with both arrays non-null, exactly the declared slot count
        /// long, and free of null entries. Missing entries become empty
        /// strings; surplus entries are dropped.
        ///
        /// This is the one place that reconciles a loadout of some other shape
        /// with the current slot counts — an older save, a peer on a build with
        /// different counts, or a `default` struct. Call it at every boundary
        /// rather than trusting the arrays.
        /// </summary>
        public static LoadoutData Normalized(LoadoutData source)
        {
            return new LoadoutData
            {
                suitIDs = NormalizeSlots(source.suitIDs, SuitSlots),
                nodeIDs = NormalizeSlots(source.nodeIDs, NodeSlots)
            };
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

    public enum NodeCategory
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
