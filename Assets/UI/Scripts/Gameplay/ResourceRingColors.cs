using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>
    /// The six --ring-critical.. --ring-rich custom properties HUD.uss
    /// declares on "hud__res-ring", and the blend ResourceRingMath's colour
    /// stop says a value falls in. Shared by ResourceRing and NodeSheet's
    /// ResourceChip, so the HUD ring and the sheet's chip can never read
    /// HUD.uss two different ways for the same value.
    ///
    /// --ring-track is not here: only ResourceRing draws an unlit track, so
    /// that read stays local to it - through <see cref="TryRead"/>, the same
    /// helper this uses for the six it does own.
    /// </summary>
    public static class ResourceRingColors
    {
        public static void ApplyResourceClass(VisualElement element, ResourceKind kind)
        {
            element.EnableInClassList("hud__resource--metal", kind == ResourceKind.Metal);
            element.EnableInClassList("hud__resource--magic", kind == ResourceKind.Magic);
        }

        public static Color BaseColorFor(ResourceKind kind, int value, int cap, Color critical, Color low,
            Color warn, Color ok, Color good, Color rich)
        {
            if (kind == ResourceKind.Metal || kind == ResourceKind.Magic)
                return Color.Lerp(critical, rich, ResourceHudMath.BarFill(value, cap));
            return BaseColorFor(value, critical, low, warn, ok, good, rich);
        }
        /// <summary>Reads all six colour stops from a CustomStyleResolvedEvent's style, in place.</summary>
        public static void Read(ICustomStyle style, ref Color critical, ref Color low, ref Color warn,
            ref Color ok, ref Color good, ref Color rich)
        {
            critical = low = warn = ok = good = rich = Color.gray;
            TryRead(style, "--ring-critical", ref critical);
            TryRead(style, "--ring-low", ref low);
            TryRead(style, "--ring-warn", ref warn);
            TryRead(style, "--ring-ok", ref ok);
            TryRead(style, "--ring-good", ref good);
            TryRead(style, "--ring-rich", ref rich);
            // New skin properties win; the old names remain compatibility defaults.
            TryRead(style, "--res-stop-0", ref critical);
            TryRead(style, "--res-stop-1", ref low);
            TryRead(style, "--res-stop-2", ref warn);
            TryRead(style, "--res-stop-3", ref ok);
            TryRead(style, "--res-stop-4", ref good);
            TryRead(style, "--res-stop-5", ref rich);
        }

        public static void TryRead(ICustomStyle style, string name, ref Color target)
        {
            CustomStyleProperty<Color> property = new CustomStyleProperty<Color>(name);
            if (style.TryGetValue(property, out Color value)) target = value;
        }

        /// <summary>
        /// The colour a value should show: one of the five flat stops, or -
        /// only across the Good zone, 10-19 - a blend from Ok toward Good.
        /// See ResourceRingMath.ColorStopIndex and GoodBlendFraction.
        /// </summary>
        public static Color BaseColorFor(int value, Color critical, Color low, Color warn,
            Color ok, Color good, Color rich)
        {
            switch (ResourceRingMath.ColorStopIndex(value))
            {
                case ResourceRingMath.StopCritical: return critical;
                case ResourceRingMath.StopLow: return low;
                case ResourceRingMath.StopWarn: return warn;
                case ResourceRingMath.StopOk: return ok;
                case ResourceRingMath.StopGood:
                    return Color.Lerp(ok, good, ResourceRingMath.GoodBlendFraction(value));
                default: return rich;
            }
        }
    }
}
