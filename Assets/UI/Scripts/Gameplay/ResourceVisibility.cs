using NodeWar.Backend;

namespace NodeWar.UI
{
    /// <summary>
    /// Whether metal is shown, for the HUD's bar and the node sheet's chip alike so the two
    /// cannot disagree. The rule is ResourceHudMath.MetalVisible; this supplies the one input
    /// it cannot know, the local player's arena.
    ///
    /// The arena is the last player state the backend returned this session
    /// (BackendServices.LastKnownState), the same value the lobby shows, for ranked and
    /// unranked matches alike: the match itself carries no arena. With no state yet (offline,
    /// a local test match) it is 0, so metal stays hidden until the player holds some.
    /// </summary>
    public static class ResourceVisibility
    {
        private static bool debugMetal;
        private static bool debugMagic;

        public static void SetDebugOverrides(bool localOrBot, bool metal, bool magic)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugMetal = localOrBot && metal;
            debugMagic = localOrBot && magic;
#else
            debugMetal = debugMagic = false;
#endif
        }
        public static int LocalArena()
        {
            PlayerState state = BackendServices.LastKnownState;
            return state != null && state.Rank != null ? state.Rank.Arena : 0;
        }

        public static bool MetalVisible(int metal)
        {
            return ResourceHudMath.MetalVisible(LocalArena(), metal, debugMetal);
        }

        public static bool MagicVisible()
        {
            return ResourceHudMath.MagicVisible(LocalArena(), debugMagic);
        }
    }
}
