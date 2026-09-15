namespace NodeWar.Lobby
{
    /// <summary>
    /// The prototype's shape language for Workshop cards: every district is
    /// round (produces), square (holds) or triangle (strikes), and every suit is
    /// combat. The family picks the card's art colour and the note under its
    /// name.
    ///
    /// No definition asset carries a family yet, so this is a table keyed by
    /// item ID, following the prototype's assignments where the names match.
    /// TODO(data): move the family onto NodeDefinition when the district data is
    /// next revised; this class then reads the field and the table goes.
    ///
    /// No UnityEngine reference, like LoadoutEditor and ItemTint, so the test
    /// project can link it.
    /// </summary>
    public static class ItemFamily
    {
        public enum Family
        {
            Round,
            Square,
            Triangle,
            Combat
        }

        private static readonly string[] RoundNodes = { "node_market", "node_shrine", "node_sanctuary" };
        private static readonly string[] SquareNodes = { "node_rampart" };
        private static readonly string[] TriangleNodes = { "node_camp", "node_barracks", "node_arsenal", "node_watchtower" };

        /// <summary>
        /// A district's family. An ID the table does not know is Square, the
        /// neutral middle, rather than an exception on a new asset.
        /// </summary>
        public static Family ForNode(string nodeID)
        {
            if (Contains(RoundNodes, nodeID)) return Family.Round;
            if (Contains(TriangleNodes, nodeID)) return Family.Triangle;
            if (Contains(SquareNodes, nodeID)) return Family.Square;
            return Family.Square;
        }

        /// <summary>The note under a district card's name, from the prototype.</summary>
        public static string NoteFor(Family family)
        {
            switch (family)
            {
                case Family.Round: return "Produces";
                case Family.Triangle: return "Strikes";
                case Family.Combat: return "Combat";
                default: return "Holds";
            }
        }

        /// <summary>The USS class that colours a card's art for this family.</summary>
        public static string ClassFor(Family family)
        {
            switch (family)
            {
                case Family.Round: return "lb-art--round";
                case Family.Triangle: return "lb-art--triangle";
                case Family.Combat: return "lb-art--combat";
                default: return "lb-art--square";
            }
        }

        /// <summary>Card order within a grid: round, square, triangle, as the prototype lists them.</summary>
        public static int SortKey(Family family)
        {
            switch (family)
            {
                case Family.Round: return 0;
                case Family.Square: return 1;
                case Family.Triangle: return 2;
                default: return 3;
            }
        }

        private static bool Contains(string[] ids, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return true;
            return false;
        }
    }
}
