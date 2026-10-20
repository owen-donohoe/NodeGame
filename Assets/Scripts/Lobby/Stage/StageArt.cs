using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Generated art is intentional until authored stage art is supplied.</summary>
    public static class StageArt
    {
        public static PaperKind District(int type) => (PaperKind)((type % 3 + 3) % 3);
        public static PaperKind Suit(int type) => PaperKind.Villager;
        public static string Identity(int type, bool suit) =>
            (suit ? CatalogKeys.SuitLobbyId(type) : CatalogKeys.DistrictLobbyId(type)) ?? (suit ? "suit_unknown" : "node_unknown");
        // Binary badge on the body/facade distinguishes items even when palette colours collide.
        public static bool Mark(int type, float x, float y)
        {
            for (int bit = 0; bit < 5; bit++)
                if ((type & (1 << bit)) != 0 && x >= 37 + bit * 5 && x <= 40 + bit * 5 && y >= 39 && y <= 44) return true;
            return false;
        }
    }
}
