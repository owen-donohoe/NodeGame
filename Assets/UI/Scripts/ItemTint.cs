namespace NodeWar.Lobby
{
    /// <summary>
    /// Picks a stable tint for an item that has no art.
    ///
    /// Every NodeDefinition.icon and SuitDefinition.icon in the project is null
    /// and Assets/Sprites/Icons is an empty directory
    /// (docs/ui-migration-inventory.md, finding 6). Rather than show fourteen
    /// identical grey squares, each item gets a lettered tile tinted from its
    /// own ID, so the fourteen are told apart at a glance and the grid reads as
    /// designed rather than broken. When real icons arrive the letter is
    /// replaced and none of this has to change shape.
    ///
    /// Deliberately NOT string.GetHashCode(). On .NET Core that is randomised
    /// per process, so the same district would tint differently between runs -
    /// which would make the one property this type exists to provide, stability,
    /// silently false. FNV-1a is written out here so the mapping is fixed
    /// forever, and the test suite pins it.
    ///
    /// Fourteen items into eight tints collide - suit_guardian and
    /// suit_berserker share one, and they sit next to each other in the list.
    /// That is accepted rather than worked around. Tinting by position in the
    /// list would separate every neighbour, but an item would then wear one
    /// colour in its slot and another in the list, which is the correspondence a
    /// player actually reads. Identity beats neighbour contrast, and the letter
    /// is what carries the identity.
    ///
    /// No UnityEngine reference, so dotnet/NodeWar.Lobby.Tests can link it.
    /// </summary>
    public static class ItemTint
    {
        /// <summary>
        /// How many tints exist. Matches the --tile-0 .. --tile-7 custom
        /// properties and the .tile-tint--0 .. --7 classes in Theme.uss; adding
        /// a tint means adding it in both places.
        /// </summary>
        public const int Count = 8;

        /// <summary>
        /// The tint index for an item ID, always in [0, Count). A null or empty
        /// ID gets 0 rather than throwing - a definition asset with an unset ID
        /// is a data problem for the Workshop to display, not a crash.
        /// </summary>
        public static int IndexFor(string itemID)
        {
            if (string.IsNullOrEmpty(itemID)) return 0;

            // FNV-1a, 32-bit. Unchecked because the whole point is that the
            // multiply wraps.
            unchecked
            {
                uint hash = 2166136261u;

                for (int i = 0; i < itemID.Length; i++)
                {
                    hash ^= itemID[i];
                    hash *= 16777619u;
                }

                return (int)(hash % Count);
            }
        }

        /// <summary>
        /// The USS class carrying that tint's background colour. The colour
        /// itself lives in Theme.uss, so the rule that no page invents a colour
        /// value holds here too.
        /// </summary>
        public static string ClassFor(string itemID)
        {
            return "tile-tint--" + IndexFor(itemID);
        }

        /// <summary>
        /// The single character shown on a tile: the first letter of the
        /// display name, upper-cased. Falls back to the ID, then to a dash, so
        /// a tile always has something on it.
        /// </summary>
        public static string MonogramFor(string displayName, string itemID)
        {
            string source = !string.IsNullOrEmpty(displayName) ? displayName : itemID;

            if (string.IsNullOrEmpty(source)) return "-";

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c))
                    return char.ToUpperInvariant(c).ToString();
            }

            return "-";
        }
    }
}
