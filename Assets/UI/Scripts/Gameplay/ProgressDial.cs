using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>
    /// A ring that fills clockwise from the top - the Forge's cycle gauge.
    ///
    /// USS has no conic gradient, so the prototype's dial is drawn with
    /// Painter2D arcs. The colours still come from the stylesheet: the fill is
    /// the element's resolved color and the track its background-image tint,
    /// so the dial restyles with the theme like everything else and no colour
    /// is written here.
    /// </summary>
    public class ProgressDial : VisualElement
    {
        private const float Thickness = 7f;

        private float progress;

        /// <summary>How full the ring is, 0 to 1.</summary>
        public float Progress
        {
            get { return progress; }
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, progress)) return;
                progress = clamped;
                MarkDirtyRepaint();
            }
        }

        public ProgressDial()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            if (size <= Thickness * 2f) return;

            Vector2 centre = rect.center;
            float radius = size * 0.5f - Thickness * 0.5f;

            Painter2D painter = context.painter2D;
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = resolvedStyle.unityBackgroundImageTintColor;
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();

            if (progress <= 0f) return;

            painter.strokeColor = resolvedStyle.color;
            painter.BeginPath();
            painter.Arc(centre, radius, -90f, -90f + 360f * progress);
            painter.Stroke();
        }
    }
}
