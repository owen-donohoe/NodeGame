using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>
    /// Three concentric segmented rings for one resource - outer 1-10, middle
    /// 11-20, inner 21-30 - drawn with Painter2D the way ProgressDial draws
    /// its single arc. Segment counts and the colour stop come from
    /// ResourceRingMath; only the HSV shade per ring (middle darker and more
    /// saturated, inner darker and more saturated again) lives here, since it
    /// is a rendering detail rather than presentation maths worth testing in
    /// isolation.
    ///
    /// Colours are read from the stylesheet via CustomStyleResolvedEvent, the
    /// same as ProgressDial, so the ring restyles with the theme rather than
    /// hard-coding a palette here.
    /// </summary>
    public class ResourceRing : VisualElement
    {
        private const float Thickness = 8f;
        private const float RingGap = 3f;
        private const float SegmentGapDegrees = 5f;

        // Middle ring: value x0.85, saturation x1.15. Inner: x0.7 / x1.3.
        private const float MiddleValueScale = 0.85f;
        private const float MiddleSaturationScale = 1.15f;
        private const float InnerValueScale = 0.7f;
        private const float InnerSaturationScale = 1.3f;

        private Color colorCritical = Color.gray;
        private Color colorLow = Color.gray;
        private Color colorWarn = Color.gray;
        private Color colorOk = Color.gray;
        private Color colorGood = Color.gray;
        private Color colorRich = Color.gray;
        private Color colorTrack = new Color(1f, 1f, 1f, 0.15f);

        private int currentValue;

        public ResourceRing()
        {
            pickingMode = PickingMode.Ignore;

            // The class, not just Tokens.uss's :root, carries the
            // --resource-ring-* custom properties: they resolve reliably on
            // a selector matching this element, which is what
            // CustomStyleResolvedEvent actually reads.
            AddToClassList("hud__res-ring");

            style.position = Position.Absolute;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.left = 0;

            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ICustomStyle style = customStyle;

            TryRead(style, "--resource-ring-critical", ref colorCritical);
            TryRead(style, "--resource-ring-low", ref colorLow);
            TryRead(style, "--resource-ring-warn", ref colorWarn);
            TryRead(style, "--resource-ring-ok", ref colorOk);
            TryRead(style, "--resource-ring-good", ref colorGood);
            TryRead(style, "--resource-ring-rich", ref colorRich);
            TryRead(style, "--resource-ring-track", ref colorTrack);

            MarkDirtyRepaint();
        }

        private static void TryRead(ICustomStyle style, string name, ref Color target)
        {
            CustomStyleProperty<Color> property = new CustomStyleProperty<Color>(name);
            if (style.TryGetValue(property, out Color value)) target = value;
        }

        /// <summary>Sets the resource amount the rings show. Repaints only when it changes.</summary>
        public void SetValue(int value)
        {
            if (value == currentValue) return;
            currentValue = value;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);

            float step = Thickness + RingGap;
            float minSize = 2f * (ResourceRingMath.RingCount * Thickness + (ResourceRingMath.RingCount - 1) * RingGap);
            if (size <= minSize) return;

            Vector2 centre = rect.center;
            float outerRadius = size * 0.5f - Thickness * 0.5f;

            Painter2D painter = context.painter2D;
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Butt;

            Color baseColor = BaseColorFor(currentValue);

            for (int ring = 0; ring < ResourceRingMath.RingCount; ring++)
            {
                float radius = outerRadius - ring * step;
                int lit = ResourceRingMath.LitSegments(currentValue, ring);
                Color litColor = ShadeForRing(baseColor, ring);

                DrawRing(painter, centre, radius, lit, litColor);
            }
        }

        private void DrawRing(Painter2D painter, Vector2 centre, float radius, int litSegments, Color litColor)
        {
            float sweepPerSegment = 360f / ResourceRingMath.SegmentsPerRing;
            float drawSweep = sweepPerSegment - SegmentGapDegrees;

            for (int i = 0; i < ResourceRingMath.SegmentsPerRing; i++)
            {
                painter.strokeColor = i < litSegments ? litColor : colorTrack;

                float start = -90f + i * sweepPerSegment + SegmentGapDegrees * 0.5f;
                painter.BeginPath();
                painter.Arc(centre, radius, start, start + drawSweep);
                painter.Stroke();
            }
        }

        private Color BaseColorFor(int value)
        {
            switch (ResourceRingMath.ColorStopIndex(value))
            {
                case ResourceRingMath.StopCritical: return colorCritical;
                case ResourceRingMath.StopLow: return colorLow;
                case ResourceRingMath.StopWarn: return colorWarn;
                case ResourceRingMath.StopOk: return colorOk;
                case ResourceRingMath.StopGood:
                    return Color.Lerp(colorOk, colorGood, ResourceRingMath.GoodBlendFraction(value));
                default: return colorRich;
            }
        }

        private static Color ShadeForRing(Color baseColor, int ring)
        {
            if (ring == 0) return baseColor;

            Color.RGBToHSV(baseColor, out float h, out float s, out float v);

            if (ring == 1)
            {
                s *= MiddleSaturationScale;
                v *= MiddleValueScale;
            }
            else
            {
                s *= InnerSaturationScale;
                v *= InnerValueScale;
            }

            Color shaded = Color.HSVToRGB(Mathf.Clamp01(h), Mathf.Clamp01(s), Mathf.Clamp01(v));
            shaded.a = baseColor.a;
            return shaded;
        }
    }
}
