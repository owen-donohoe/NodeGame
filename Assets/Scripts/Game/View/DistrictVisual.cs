using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Everything that says what one district looks like, in one asset.
    ///
    /// It exists because a district's art currently has no single owner. The
    /// board wants a prefab, the draft wants a sticker, the lobby's Workshop
    /// wants an icon, and each of the three is wired somewhere different --
    /// DraftScreenController carries its own DistrictType-to-Sprite table,
    /// NodeDefinition carries an icon field, and the board looks a prefab up by
    /// district. Three answers to one question is three things to keep in step.
    ///
    /// This is the fourth answer that is meant to replace them, and until it
    /// does its more immediate job is to be a checklist: fourteen assets with
    /// named empty slots say what has to be drawn far better than a folder of
    /// sprites says what is missing. Nothing is wired to read this yet, on
    /// purpose -- the migration of each caller is its own change, and an empty
    /// table that silently wins over a populated one is a worse state than two
    /// honest tables.
    ///
    /// Sound and hit effects belong here too, and are deliberately absent:
    /// there is no audio in the project and no FeelDirector to play one, so a
    /// field for either would be a slot nothing can fill. Add them when their
    /// consumer exists.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictVisual", menuName = "NodeWar/District Visual")]
    public sealed class DistrictVisual : ScriptableObject
    {
        [Tooltip("Which district this describes. One asset per DistrictType; " +
                 "DistrictVisualTable rejects duplicates.")]
        public DistrictType district;

        [Header("Board")]
        [Tooltip("The node prefab variant placed on the board, under " +
                 "Assets/Prefabs/Game/RevisedNodes/. Null means this district " +
                 "has no board art at all -- true today for Forge, Village and " +
                 "Watchtower.")]
        public GameObject boardPrefab;

        [Header("Flat art")]
        [Tooltip("The small UI icon: Workshop grid, node sheet, anywhere the " +
                 "district is named rather than stood on.")]
        public Sprite icon;

        [Tooltip("The draft board piece. Larger and squarer than the icon, and " +
                 "read at arm's length rather than in a row of text. Falls back " +
                 "to the icon when unset, so one drawing can serve both until " +
                 "they are worth separating.")]
        public Sprite sticker;

        [Header("Identity")]
        [Tooltip("The district's own colour, for chips, borders and effect " +
                 "tints. Not a player colour -- ownership is PlayerColors, and " +
                 "these two must never be confused on screen.")]
        public Color accent = Color.white;

        /// <summary>
        /// The draft piece, or the icon when none is set. Callers should use
        /// this rather than reading <see cref="sticker"/> directly, so the
        /// fallback lives in one place.
        /// </summary>
        public Sprite StickerOrIcon => sticker != null ? sticker : icon;

        /// <summary>
        /// True when this district has nothing drawn for it yet. What the art
        /// checklist counts; see docs/art-manifest.md.
        /// </summary>
        public bool IsUnillustrated => boardPrefab == null && icon == null && sticker == null;
    }
}
