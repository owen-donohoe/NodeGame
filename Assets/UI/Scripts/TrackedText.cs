using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Sizes a label to its own text when letter-spacing is set. UI Toolkit's
    /// text measurement leaves letter-spacing out, so a label sized to its
    /// content is a few pixels too narrow and elides its last glyphs ("Bu…"
    /// for "Build"). Labels that grow into spare room do not need this.
    /// </summary>
    public static class TrackedText
    {
        /// <summary>Refit whenever the label's font or layout settles, not only when its text is set.</summary>
        public static void Track(Label label)
        {
            label.RegisterCallback<CustomStyleResolvedEvent>(_ => Fit(label));
            label.RegisterCallback<GeometryChangedEvent>(_ => Fit(label));
        }

        public static void SetText(Label label, string text)
        {
            label.text = text;
            Fit(label);
        }

        private static void Fit(Label label)
        {
            if (label.panel == null) return;
            string text = label.text ?? "";
            IResolvedStyle s = label.resolvedStyle;
            float content = label.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined).x;
            // The width style includes padding and border; flex-shrink still ellipsizes long text.
            float width = content + s.letterSpacing * text.Length
                + s.paddingLeft + s.paddingRight + s.borderLeftWidth + s.borderRightWidth;
            StyleLength current = label.style.width;
            if (current.keyword == StyleKeyword.Undefined && System.Math.Abs(current.value.value - width) < 0.25f) return;
            label.style.width = width;
        }
    }
}
