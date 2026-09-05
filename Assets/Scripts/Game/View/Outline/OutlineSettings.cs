using System;
using UnityEngine;

namespace NodeWar.View.Outline
{
    /// <summary>Mask render target size, relative to the camera's target.</summary>
    public enum OutlineMaskResolution
    {
        /// <summary>
        /// Matches the camera target. The default: Mobile_RPAsset already runs
        /// at m_RenderScale 0.8, and halving on top of that lands near 0.4 of
        /// native, where thin building details and the villager body start
        /// dropping out of the mask entirely.
        /// </summary>
        Full = 0,

        /// <summary>
        /// Half on each axis. Cheaper, and gives a chunky line that stays
        /// chunky, which suits the intended 2.5D style -- at the cost of thin
        /// features. A deliberate art choice, not a default.
        /// </summary>
        Half = 1,
    }

    /// <summary>Where the composite lands in the frame.</summary>
    public enum OutlineInjectionPoint
    {
        /// <summary>
        /// Outline is written before post, so it picks up bloom and tonemapping.
        /// Note what that means here specifically: Gameplay.unity's Global
        /// Volume runs Bloom at intensity 1.54 and Bokeh depth of field, so an
        /// outline placed here is both bloomed and depth-blurred. Choose it
        /// when you want that, not by accident.
        /// </summary>
        BeforePostProcessing = 0,

        /// <summary>Outline stays crisp, unaffected by bloom or depth of field. Default.</summary>
        AfterPostProcessing = 1,
    }

    /// <summary>
    /// Every tunable the outline system has, in one asset.
    ///
    /// This is the single place outline colours are defined. Nothing else --
    /// no caller, no material, no prefab -- picks an outline colour.
    /// </summary>
    [CreateAssetMenu(fileName = "OutlineSettings", menuName = "NodeWar/Outline Settings")]
    public sealed class OutlineSettings : ScriptableObject
    {
        /// <summary>One palette entry per <see cref="OutlineStyle"/> member.</summary>
        [Serializable]
        public struct StyleEntry
        {
            public Color color;

            [Tooltip("Draw this style's outline even where the group is hidden " +
                     "behind something. Off by default: an occluded part writes " +
                     "no ID and grows no line, so depth ordering reads correctly. " +
                     "Turn it on for a style that has to be findable behind a " +
                     "node -- a selected villager walking behind one.")]
            public bool drawThrough;
        }

        [Tooltip("Indexed by OutlineStyle. Entry 0 is None and is never drawn.")]
        [SerializeField]
        private StyleEntry[] palette = DefaultPalette();

        [Header("Thickness")]
        [Tooltip("Outline thickness, in pixels at the reference height below.")]
        [Range(1f, 8f)]
        [SerializeField] private float thicknessReferencePixels = 3f;

        [Tooltip("Screen height the thickness above is authored against. " +
                 "Thickness is converted to mask texels as thickness * " +
                 "(maskHeight / referenceHeight), which holds a constant " +
                 "fraction of screen height -- so the line neither thins when " +
                 "render scale drops to 0.8 on mobile, nor shrinks on a " +
                 "higher-density screen.")]
        [SerializeField] private float referenceHeight = 1080f;

        [Header("Mask")]
        [SerializeField] private OutlineMaskResolution maskResolution = OutlineMaskResolution.Full;

        [Tooltip("The mask pass is cutout, not blended. Sprite edges softer " +
                 "than this give a noisy mask boundary, so this doubles as an " +
                 "art constraint on how much feathering a sprite edge may have.")]
        [Range(0.01f, 0.99f)]
        [SerializeField] private float alphaClipThreshold = 0.5f;

        [Header("Composite")]
        [SerializeField]
        private OutlineInjectionPoint injectionPoint = OutlineInjectionPoint.AfterPostProcessing;

        public float ThicknessReferencePixels => thicknessReferencePixels;
        public float ReferenceHeight => Mathf.Max(1f, referenceHeight);
        public OutlineMaskResolution MaskResolution => maskResolution;
        public float AlphaClipThreshold => alphaClipThreshold;
        public OutlineInjectionPoint InjectionPoint => injectionPoint;

        public StyleEntry GetStyle(OutlineStyle style)
        {
            int index = (int)style;
            if (palette == null || index < 0 || index >= palette.Length) return default;

            return palette[index];
        }

        private static StyleEntry[] DefaultPalette()
        {
            StyleEntry[] entries = new StyleEntry[OutlineStyleMask.StyleCount];

            // None. Never drawn -- a group in this state holds no ID and never
            // reaches the mask. Present so the array indexes by enum value.
            entries[(int)OutlineStyle.None] = new StyleEntry
            {
                color = new Color(0f, 0f, 0f, 0f),
                drawThrough = false,
            };

            entries[(int)OutlineStyle.Hover] = new StyleEntry
            {
                color = new Color(1f, 1f, 1f, 0.55f),
                drawThrough = false,
            };

            entries[(int)OutlineStyle.Contested] = new StyleEntry
            {
                color = new Color(1f, 0.42f, 0.2f, 1f),
                drawThrough = false,
            };

            entries[(int)OutlineStyle.Selected] = new StyleEntry
            {
                color = new Color(1f, 1f, 1f, 1f),

                // The one style that defaults to drawing through. A selected
                // villager that walks behind a node has to stay findable, and
                // it is the case where losing the outline is most disorienting.
                drawThrough = true,
            };

            entries[(int)OutlineStyle.CommandAck] = new StyleEntry
            {
                color = new Color(0.55f, 1f, 0.65f, 1f),
                drawThrough = false,
            };

            return entries;
        }

        private void OnValidate()
        {
            // The composite indexes this array by style value with no bounds
            // check in the shader, so a short palette would sample garbage.
            if (palette == null || palette.Length != OutlineStyleMask.StyleCount)
            {
                StyleEntry[] resized = DefaultPalette();

                if (palette != null)
                {
                    int shared = Mathf.Min(palette.Length, resized.Length);
                    for (int i = 0; i < shared; i++) resized[i] = palette[i];
                }

                palette = resized;
            }
        }
    }
}
