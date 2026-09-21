using UnityEngine;

namespace NodeWar.View
{
    /// <summary>What an in-match indicator is about.</summary>
    public enum IndicatorKind
    {
        /// <summary>A fight on a node.</summary>
        Battle,

        /// <summary>A node you own, with enemy claimers pushing its bar toward neutral.</summary>
        NodeUnderAttack,

        /// <summary>An unowned node leaning your way, with enemy claimers pushing it back.</summary>
        NodeContested,

        /// <summary>An enemy whose revealed route reaches your Core.</summary>
        ThreatToCore,

        /// <summary>An enemy whose revealed route reaches another node you own.</summary>
        ThreatToTerritory,

        /// <summary>One of your unsuited villagers standing about off your Core.</summary>
        Idle,

        /// <summary>One of your villagers just came back at your Core.</summary>
        Respawn,

        /// <summary>Reserved for node abilities. Nothing produces it yet.</summary>
        Effect
    }

    /// <summary>How loudly an indicator reads: size, colour, and which wins a merge.</summary>
    public enum IndicatorTier { Low, Medium, High }

    /// <summary>What an indicator looks like while its subject is in the middle of the view.</summary>
    public enum InViewForm { None, Small, Full }

    /// <summary>The rules for one IndicatorKind.</summary>
    [System.Serializable]
    public class IndicatorKindSettings
    {
        public bool enabled = true;

        public IndicatorTier tier = IndicatorTier.Low;

        [Tooltip("The form while the subject is inside the central zone, where " +
                 "the player is already looking. None hides it; Small is the " +
                 "same icon at a fraction of the size.")]
        public InViewForm inView = InViewForm.Small;

        [Tooltip("Show the edge form when the subject is outside the central zone " +
                 "or off screen.")]
        public bool showAtEdge = true;

        [Tooltip("How long the condition must hold before anything shows, in " +
                 "seconds, so a one-tick flicker never pops.")]
        public float minSeconds = 0.5f;

        [Tooltip("How long it stays after the condition stops, in seconds. If the " +
                 "condition comes back inside this window the same indicator " +
                 "carries on rather than popping again.")]
        public float graceSeconds = 1f;

        [Tooltip("For a moment rather than a condition (a respawn): how long it " +
                 "stays up, in seconds.")]
        public float holdSeconds = 2f;

        public IndicatorKindSettings() { }

        public IndicatorKindSettings(IndicatorTier tier, InViewForm inView,
                                     float minSeconds, float graceSeconds, float holdSeconds)
        {
            this.tier = tier;
            this.inView = inView;
            this.minSeconds = minSeconds;
            this.graceSeconds = graceSeconds;
            this.holdSeconds = holdSeconds;
        }
    }

    /// <summary>
    /// Every tunable number behind the in-match indicators, in one place on
    /// GameManager, like OpponentRouteSettings. The defaults are the ones agreed
    /// when the feature was designed; the Inspector is where they get tuned.
    /// </summary>
    [System.Serializable]
    public class IndicatorSettings
    {
        [Header("Zones")]
        [Tooltip("The central box, as a fraction of the visible board, inside " +
                 "which a subject takes its in-view form.")]
        [Range(0.2f, 1f)] public float innerEnter = 0.6f;

        [Tooltip("The box it has to leave to stop being in view. Larger than the " +
                 "one above, so a subject on the line does not flicker.")]
        [Range(0.2f, 1f)] public float innerExit = 0.66f;

        [Header("Edge")]
        [Tooltip("Edge indicators closer than this, in panel units, merge into " +
                 "the most important one, which shows a count.")]
        public float mergeRadius = 48f;

        [Tooltip("At most this many edge indicators at once. The least important go.")]
        [Range(1, 12)] public int maxAtEdge = 6;

        [Header("Anchors")]
        [Tooltip("How far above a node its indicator floats, in world units.")]
        public float nodeHeight = 2.5f;

        [Tooltip("How far above a villager its indicator floats, in world units.")]
        public float villagerHeight = 1.6f;

        [Header("Kinds")]
        public IndicatorKindSettings battle =
            new IndicatorKindSettings(IndicatorTier.High, InViewForm.None, 0.5f, 1f, 0f);

        public IndicatorKindSettings nodeUnderAttack =
            new IndicatorKindSettings(IndicatorTier.High, InViewForm.Small, 0.5f, 1f, 1.5f);

        public IndicatorKindSettings nodeContested =
            new IndicatorKindSettings(IndicatorTier.Low, InViewForm.Small, 0.5f, 1f, 0f);

        public IndicatorKindSettings threatToCore =
            new IndicatorKindSettings(IndicatorTier.High, InViewForm.Full, 0f, 0.5f, 0f);

        public IndicatorKindSettings threatToTerritory =
            new IndicatorKindSettings(IndicatorTier.Low, InViewForm.Small, 0f, 0.5f, 0f);

        public IndicatorKindSettings idle =
            new IndicatorKindSettings(IndicatorTier.Low, InViewForm.Small, 3f, 0f, 0f);

        public IndicatorKindSettings respawn =
            new IndicatorKindSettings(IndicatorTier.Medium, InViewForm.Small, 0f, 0f, 2f);

        public IndicatorKindSettings effect =
            new IndicatorKindSettings(IndicatorTier.Low, InViewForm.Small, 0f, 0f, 2f);

        public IndicatorKindSettings For(IndicatorKind kind)
        {
            switch (kind)
            {
                case IndicatorKind.Battle: return battle;
                case IndicatorKind.NodeUnderAttack: return nodeUnderAttack;
                case IndicatorKind.NodeContested: return nodeContested;
                case IndicatorKind.ThreatToCore: return threatToCore;
                case IndicatorKind.ThreatToTerritory: return threatToTerritory;
                case IndicatorKind.Idle: return idle;
                case IndicatorKind.Respawn: return respawn;
                default: return effect;
            }
        }
    }
}
