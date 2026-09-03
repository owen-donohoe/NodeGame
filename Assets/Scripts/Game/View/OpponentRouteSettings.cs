using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// How much of an opponent route the player is allowed to see.
    ///
    /// Separate from PathCurveSettings deliberately. That object exists to keep
    /// VillagerView and MovementPathRenderer drawing the same curve, and nothing
    /// here concerns the sprite -- these are the rules of an information gate,
    /// and mixing them into a shared drawing contract would blur what the
    /// sharing is for.
    ///
    /// No colours live here. A route is drawn in its owner player colour from
    /// PathCurveSettings, so the opponent route is whichever side the opponent
    /// actually is; only opacity and reach are decided here.
    ///
    /// Worth stating plainly: the game has no fog of war. Every node and every
    /// villager already renders for both players, so this hands the player
    /// information they did not previously have rather than taking any away.
    /// </summary>
    [System.Serializable]
    public class OpponentRouteSettings
    {
        [Header("Reach")]
        [Tooltip("Draw opponent routes at all.")]
        public bool show = true;

        [Tooltip("How many legs ahead of the villager are drawn. A real " +
                 "truncation: past it nothing is rendered, so the destination " +
                 "cannot be recovered by brightening the screen. Beware that a " +
                 "route only this many nodes long is revealed in full.")]
        [Range(1, 5)]
        public int revealLegs = 2;

        [Tooltip("Only draw a route for an opponent within this many graph hops " +
                 "of a node you own or stand on -- board presence is what earns " +
                 "the information. Zero means no range limit. This is what stops " +
                 "zooming out from revealing the whole board.")]
        [Range(0, 12)]
        public int withinHopsOfYou = 3;

        [Header("Distance Fade")]
        [Tooltip("Over how many NODES the route fades from nearAlpha to " +
                 "farAlpha. Measured in node spacings along the route, not in " +
                 "screen space, so the fade means the same thing at any zoom. " +
                 "Set it below revealLegs and the route is already invisible by " +
                 "the cut; set it above and the cut is still faintly visible.")]
        [Range(0.25f, 5f)]
        public float fadeNodes = 1.6f;

        [Tooltip("Opacity at the villager end, where the route is certain.")]
        [Range(0f, 1f)]
        public float nearAlpha = 0.85f;

        [Tooltip("Opacity it dissolves to. Leave at zero, or the cut reads as a " +
                 "hard stop rather than as knowledge running out.")]
        [Range(0f, 1f)]
        public float farAlpha = 0f;

        [Header("Off-screen Fade")]
        [Tooltip("How far outside the view an opponent may be before its route " +
                 "is fully invisible, in SCREEN WIDTHS (viewport units): 0.5 is " +
                 "half a screen beyond the edge, 1.0 a whole screen. Opacity " +
                 "falls off linearly with the distance out, so a route dims as " +
                 "it leaves rather than blinking off at the edge. Zero cuts hard " +
                 "at the edge instead.")]
        [Range(0f, 2f)]
        public float offScreenFade = 0.35f;

        [Header("Look")]
        [Tooltip("Fraction of the width of your own routes. Thinner reads as " +
                 "less certain without needing a second material.")]
        [Range(0.2f, 1.5f)]
        public float widthScale = 0.7f;
    }
}
