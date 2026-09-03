using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Shape and look of a drawn movement route.
    ///
    /// One instance is owned by GameManager and handed to both
    /// MovementPathRenderer and every VillagerView, so the curve the sprite walks
    /// and the curve the line draws come from the same numbers. Two copies of
    /// these values that happened to disagree would put every villager visibly
    /// beside its own path, which is the whole failure this sharing rules out.
    ///
    /// A route has no colour of its own: it takes the colour of whoever owns the
    /// villager, so a route always reads as that side regardless of which side
    /// the local player is on. Only the treatment differs -- your own routes are
    /// drawn whole, an opponent route is cut short and dissolves.
    /// </summary>
    [System.Serializable]
    public class PathCurveSettings
    {
        [Header("Curve")]
        [Tooltip("How far back from a node the turn begins, in world units. " +
                 "Clamped per corner to half the shorter adjacent leg, so a " +
                 "short leg rounds less rather than overshooting. Larger reads " +
                 "the direction earlier but drifts further from the node.")]
        [Range(0.05f, 3f)]
        public float cornerRadius = 1.35f;

        [Tooltip("Vertices per rounded corner. Rounded up to an even number so " +
                 "the corner has an exact midpoint, which is the boundary " +
                 "between two legs and the point the sprite must land on.")]
        [Range(2, 24)]
        public int cornerSegments = 8;

        [Header("Line")]
        [Tooltip("Thickness of the drawn route, in world units.")]
        [Range(0.01f, 0.5f)]
        public float lineWidth = 0.07f;

        [Tooltip("ELEVATION above the ground plane, in world units -- how high " +
                 "the route floats over the board, not how thick it is. Keep it " +
                 "above the board art and below the villagers so a route never " +
                 "draws over a unit.")]
        [Range(0f, 1f)]
        public float lineHeight = 0.05f;

        [Tooltip("Sorting layer the routes draw on. Lasso by default: it sits " +
                 "above Ground, where the node art renders, and below Villagers, " +
                 "so a route lies on the board without covering a unit. The " +
                 "lowest layer would put routes behind the nodes and hide them.")]
        public string sortingLayerName = "Lasso";

        [Tooltip("Distance from one dash to the next, in world units. This is " +
                 "the gap pattern along the line, unrelated to lineHeight. " +
                 "Smaller is a finer dotted line.")]
        [Range(0.05f, 3f)]
        public float dashSpacing = 0.33f;

        [Header("Player Colours")]
        [Tooltip("Route colour for player 0. Authored here rather than shared " +
                 "with VillagerView, which keeps its own per-state palette; if " +
                 "these drift apart visually, match them by hand.")]
        public Color player0Color = new Color(0.40f, 0.70f, 1f, 1f);

        [Tooltip("Route colour for player 1.")]
        public Color player1Color = new Color(1f, 0.45f, 0.50f, 1f);

        [Header("Your Routes")]
        [Tooltip("Opacity of one of your routes the moment it is ordered.")]
        [Range(0f, 1f)]
        public float freshAlpha = 0.9f;

        [Tooltip("What it settles to once it has been running a while. Dimmed " +
                 "rather than hidden: a standing order stays checkable.")]
        [Range(0f, 1f)]
        public float settledAlpha = 0.3f;

        [Tooltip("Seconds from ordering a route to it reaching its settled " +
                 "opacity, so the order just given reads loudest.")]
        [Range(0.5f, 15f)]
        public float settleSeconds = 4f;
    }
}
