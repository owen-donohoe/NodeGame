namespace NodeWar.UI
{
    /// <summary>
    /// The card-to-board handover: how solid the proxy under the finger and
    /// the ghost on the board each are, given how far above the card bar the
    /// finger has got.
    ///
    /// The two used to swap at the bar's edge - proxy or ghost, never both -
    /// which made the piece appear to teleport between two representations of
    /// itself at a line the player cannot see. They cross-fade now, over a
    /// band, so for a stretch of the drag both are on screen at once and the
    /// piece reads as moving from the card onto the board.
    ///
    /// THE PROXY FADES SLOWER THAN THE GHOST, both ways. The ghost is solid by
    /// GhostFadeDistance while the proxy still has most of its weight, so the
    /// board always has the piece before the card lets go of it, and coming
    /// back into the bar the card has it again before the board gives it up.
    /// There is never a moment where neither is really there, which is the
    /// failure the overlap exists to avoid.
    ///
    /// Distance is measured in panel units above the bar's top edge: zero at
    /// the edge, positive over the board. Linear, because the band is short
    /// enough that a curve would only be felt as imprecision.
    ///
    /// No UnityEngine types, so dotnet/NodeWar.View.Tests runs it directly.
    /// </summary>
    public static class DraftHandover
    {
        /// <summary>The proxy is gone by this far above the bar.</summary>
        public const float ProxyFadeDistance = 110f;

        /// <summary>The ghost is fully solid by this far above the bar.</summary>
        public const float GhostFadeDistance = 45f;

        /// <summary>Opacity of the finger's copy of the card, 1 over the bar.</summary>
        public static float ProxyOpacity(float aboveBar)
        {
            if (aboveBar <= 0f) return 1f;
            if (aboveBar >= ProxyFadeDistance) return 0f;
            return 1f - aboveBar / ProxyFadeDistance;
        }

        /// <summary>Opacity of the board ghost, 0 over the bar.</summary>
        public static float GhostOpacity(float aboveBar)
        {
            if (aboveBar <= 0f) return 0f;
            if (aboveBar >= GhostFadeDistance) return 1f;
            return aboveBar / GhostFadeDistance;
        }
    }
}
