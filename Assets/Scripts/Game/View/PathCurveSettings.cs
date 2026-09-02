using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Shape and timing of a drawn movement route.
    ///
    /// One instance is owned by MovementPathRenderer and handed to every
    /// VillagerView, so the curve the sprite walks and the curve the line draws
    /// come from the same numbers. Two copies of these values that happened to
    /// disagree would put every villager visibly beside its own path, which is
    /// the whole failure this shared object exists to rule out.
    ///
    /// Serialized rather than const because the corner radius in particular is a
    /// feel decision that wants tuning against the real board, not a rebuild per
    /// adjustment.
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
        [Tooltip("Width of the drawn route in world units.")]
        [Range(0.01f, 0.5f)]
        public float lineWidth = 0.07f;

        [Tooltip("Height above the ground plane. Above the board art, below " +
                 "the villagers, so a route never draws over a unit.")]
        [Range(0f, 1f)]
        public float lineHeight = 0.05f;

        [Tooltip("Dashes per world unit along the route. Higher is a finer " +
                 "dotted line.")]
        [Range(0.5f, 20f)]
        public float dashesPerUnit = 3f;

        [Header("Fade")]
        [Tooltip("Colour and opacity of a route the moment it is ordered.")]
        public Color freshColor = new Color(0.55f, 0.8f, 1f, 0.9f);

        [Tooltip("What a route settles to once it has been running a while. " +
                 "Dimmed rather than hidden: a standing order stays checkable.")]
        public Color settledColor = new Color(0.55f, 0.8f, 1f, 0.3f);

        [Tooltip("Seconds from ordering a route to it reaching its settled " +
                 "opacity, so the order just given reads loudest.")]
        [Range(0.5f, 15f)]
        public float settleSeconds = 4f;
    }
}
