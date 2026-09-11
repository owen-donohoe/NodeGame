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
    /// Diagnostic overrides for the composite pass. Off in shipped content.
    /// </summary>
    public enum OutlineDebugView
    {
        /// <summary>Normal outlines.</summary>
        Off = 0,

        /// <summary>
        /// Fills the group silhouettes solid instead of outlining them, so the
        /// mask can be seen on screen without the frame debugger. Answers "did
        /// the composite read the mask correctly?".
        /// </summary>
        Mask = 1,

        /// <summary>
        /// Ignores the mask and paints an opaque band down the left of the
        /// screen. Answers the more basic question underneath Mask: "did the
        /// composite pass reach the screen at all?" -- separating a plumbing
        /// problem from a dilation problem.
        ///
        /// A band rather than a full fill, and opaque rather than blended, on
        /// purpose. A full-screen fill is indistinguishable from a camera clear
        /// colour, and a blended one can be lost to the blend unit; a hard
        /// vertical edge with the scene still visible beside it can only be
        /// this pass.
        /// </summary>
        SolidFill = 2,
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

            [Tooltip("This style's line width, as a fraction of the thickness " +
                     "below. Selected is the style most worth turning down: it " +
                     "wins every contested pixel, so it reads as heavy at the " +
                     "same width another style reads as normal.")]
            [Range(MinThicknessScale, 1f)]
            public float thicknessScale;

            [Tooltip("Draw this style's outline even where the group is hidden " +
                     "behind something. Off by default: an occluded part writes " +
                     "no ID and grows no line, so depth ordering reads correctly. " +
                     "Turn it on for a style that has to be findable behind a " +
                     "node -- a selected villager walking behind one.")]
            public bool drawThrough;
        }

        /// <summary>
        /// Floor for a style's thickness fraction. Not zero: a style scaled to
        /// nothing is a style that silently stops drawing, which looks like the
        /// priority rule eating it rather than like a width of zero.
        /// </summary>
        public const float MinThicknessScale = 0.05f;

        /// <summary>
        /// Normalises a serialised thickness fraction into the usable range.
        ///
        /// Zero maps to full width rather than to nothing, because zero is what
        /// Unity hands back for a field that did not exist when the asset was
        /// written -- every palette entry saved before this field was added.
        /// Treating that as "no line" would make the outline vanish on upgrade
        /// and look like the feature had broken.
        /// </summary>
        public static float ResolveThicknessScale(float serialised)
        {
            if (serialised <= 0f) return 1f;

            return Mathf.Clamp(serialised, MinThicknessScale, 1f);
        }

        [Tooltip("Indexed by OutlineStyle. Entry 0 is None and is never drawn.")]
        [SerializeField]
        private StyleEntry[] palette = DefaultPalette();

        [Header("Thickness")]
        [Tooltip("Outline thickness, in pixels at the reference height below.")]
        [Range(1f, 8f)]
        [SerializeField] private float thicknessReferencePixels = 3f;

        [Tooltip("Fraction of the line's width spent fading out, measured from " +
                 "its outer edge. This is the antialiasing, and it is also " +
                 "what rounds corners -- coverage falls off with distance " +
                 "rather than stopping where the tap pattern reached. Zero is " +
                 "a hard, aliased edge; past about 0.5 the line reads as a " +
                 "glow rather than a line.")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeSoftness = 0.35f;

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

        [Tooltip("Restrict the composite to the screen rectangle the outlined " +
                 "groups actually cover, instead of rasterising the whole " +
                 "screen and discarding most of it. On by default and visually " +
                 "identical either way -- it is here so the two can be " +
                 "compared in play mode, because the failure it could cause is " +
                 "a line shaved off at one edge rather than anything obvious.")]
        [SerializeField] private bool scissorComposite = true;

        [Tooltip("Diagnostics. Leave Off unless something is wrong.")]
        [SerializeField] private OutlineDebugView debugView = OutlineDebugView.Off;

        public float ThicknessReferencePixels => thicknessReferencePixels;
        public float EdgeSoftness => edgeSoftness;
        public float ReferenceHeight => Mathf.Max(1f, referenceHeight);
        public OutlineMaskResolution MaskResolution => maskResolution;
        public float AlphaClipThreshold => alphaClipThreshold;
        public OutlineInjectionPoint InjectionPoint => injectionPoint;
        public bool ScissorComposite => scissorComposite;
        public OutlineDebugView DebugView => debugView;

        // Built once, and only if something actually needs it. Unity does not
        // run C# field initializers when deserializing an asset, so a settings
        // asset authored without an explicit palette -- or one saved before a
        // style was added -- arrives here with a null or short array. Falling
        // back to the defaults keeps that case working instead of drawing every
        // outline in transparent black, which would look exactly like the
        // feature being broken.
        private static StyleEntry[] fallbackPalette;

        public StyleEntry GetStyle(OutlineStyle style)
        {
            int index = (int)style;
            if (index < 0 || index >= OutlineStyleMask.StyleCount) return default;

            if (palette != null && index < palette.Length) return palette[index];

            if (fallbackPalette == null) fallbackPalette = DefaultPalette();
            return fallbackPalette[index];
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
                thicknessScale = 1f,
            };

            entries[(int)OutlineStyle.Hover] = new StyleEntry
            {
                color = new Color(1f, 1f, 1f, 0.55f),
                drawThrough = false,
                thicknessScale = 1f,
            };

            entries[(int)OutlineStyle.Contested] = new StyleEntry
            {
                color = new Color(1f, 0.42f, 0.2f, 1f),
                drawThrough = false,
                thicknessScale = 1f,
            };

            entries[(int)OutlineStyle.Selected] = new StyleEntry
            {
                color = new Color(1f, 1f, 1f, 1f),

                // The one style that defaults to drawing through. A selected
                // villager that walks behind a node has to stay findable, and
                // it is the case where losing the outline is most disorienting.
                drawThrough = true,
                thicknessScale = 1f,
            };

            entries[(int)OutlineStyle.CommandAck] = new StyleEntry
            {
                color = new Color(0.55f, 1f, 0.65f, 1f),
                drawThrough = false,
                thicknessScale = 1f,
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

            // Stamps the upgrade default onto the asset itself, so an entry
            // written before thicknessScale existed reads 1 in the Inspector
            // rather than a 0 that ResolveThicknessScale quietly reinterprets
            // every frame. A value the shader treats as 1 should not display
            // as 0.
            for (int i = 0; i < palette.Length; i++)
            {
                palette[i].thicknessScale = ResolveThicknessScale(palette[i].thicknessScale);
            }
        }
    }
}
