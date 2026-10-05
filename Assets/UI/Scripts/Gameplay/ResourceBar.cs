using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>
    /// A plain horizontal segmented bar for metal: one segment per unit up to the cap, filled
    /// in the colour the value falls in (the ring's colour stops), with a faint track so the
    /// capacity reads. Where the rings draw only what is held, a bar is read as "x of y", so
    /// it keeps its empty segments.
    ///
    /// Full (at the cap) it behaves like a full ring: a pulse toward white and a white wave
    /// that sweeps along the bar, right end to left, fading as it travels, repeating every
    /// ~1.8 s; steady brighter tint and no motion under reduced motion. The maths is
    /// ResourceHudMath, shared with the ring so the two cannot disagree about what full is.
    ///
    /// Drawn with Painter2D like ResourceRing, and the colours come from the same custom
    /// properties (the host carries the hud__res-ring class), read the same way.
    /// </summary>
    public class ResourceBar : VisualElement
    {
        private const float SegmentGap = 3f;
        private const long TickMilliseconds = 16;
        private const int WaveSlices = 3;

        private Color colorCritical = Color.gray;
        private Color colorLow = Color.gray;
        private Color colorWarn = Color.gray;
        private Color colorOk = Color.gray;
        private Color colorGood = Color.gray;
        private Color colorRich = Color.gray;
        private Color colorTrack = new Color(1f, 1f, 1f, 0.14f);
        private Color colorPending = new Color(1f, 1f, 1f, 0.3f);

        private int value;
        private int cap = ResourceHudMath.DefaultMetalCap;
        private bool reducedMotion;
        private bool wasFull;
        private float fullElapsed;

        private readonly float[] production = new float[ResourceProduction.MaxInFlight];
        private int productionCount;

        private IVisualElementScheduledItem tick;
        private double lastTickTime;

        public ResourceBar()
        {
            pickingMode = PickingMode.Ignore;
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
            ResourceRingColors.Read(style, ref colorCritical, ref colorLow, ref colorWarn,
                ref colorOk, ref colorGood, ref colorRich);
            ResourceRingColors.TryRead(style, "--ring-track", ref colorTrack);
            ResourceRingColors.TryRead(style, "--ring-pending", ref colorPending);
            MarkDirtyRepaint();
        }

        public void SetValue(int amount)
        {
            if (amount == value) return;
            value = amount;
            UpdateFullState();
        }

        public void SetCap(int amount)
        {
            if (amount == cap) return;
            cap = amount;
            UpdateFullState();
        }

        public void SetReducedMotion(bool reduced)
        {
            if (reduced == reducedMotion) return;
            reducedMotion = reduced;
            UpdateFullState();
        }

        /// <summary>The player's in-flight metal, nearest to landing first. Same contract as ResourceRing.</summary>
        public void SetProduction(float[] fractions, int count)
        {
            if (fractions == null) count = 0;
            if (count > production.Length) count = production.Length;
            if (count < 0) count = 0;

            bool changed = count != productionCount;
            for (int i = 0; i < count && !changed; i++) changed = production[i] != fractions[i];
            if (!changed) return;

            productionCount = count;
            for (int i = 0; i < count; i++) production[i] = fractions[i];
            MarkDirtyRepaint();
        }

        private void UpdateFullState()
        {
            bool full = ResourceHudMath.IsFull(value, cap);
            if (!full || !wasFull) fullElapsed = 0f;
            wasFull = full;

            if (full && !reducedMotion)
            {
                lastTickTime = Time.unscaledTimeAsDouble;
                if (tick == null) tick = schedule.Execute(Tick).Every(TickMilliseconds);
                else tick.Resume();
            }
            else if (tick != null) tick.Pause();

            MarkDirtyRepaint();
        }

        private void Tick()
        {
            double now = Time.unscaledTimeAsDouble;
            float delta = (float)(now - lastTickTime);
            lastTickTime = now;
            if (delta <= 0f) return;

            fullElapsed += delta;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            int segments = cap;
            if (segments <= 0 || rect.width <= SegmentGap * segments || rect.height <= 0f) return;

            float segmentWidth = (rect.width - SegmentGap * (segments - 1)) / segments;
            Painter2D painter = context.painter2D;

            bool full = ResourceHudMath.IsFull(value, cap);
            Color lit = BaseColor();
            if (full) lit = Color.Lerp(lit, Color.white, ResourceHudMath.FullWhiteMix(fullElapsed, reducedMotion));
            float waveProgress = full ? ResourceHudMath.EffectiveWaveProgress(fullElapsed, reducedMotion) : -1f;

            for (int i = 0; i < segments; i++)
            {
                float x = rect.xMin + i * (segmentWidth + SegmentGap);
                Fill(painter, x, rect, segmentWidth, 0f, 1f, colorTrack);

                float fraction = ResourceHudMath.BarSegmentFraction(value, i);
                if (fraction > 0f) Fill(painter, x, rect, segmentWidth, 0f, fraction, lit);

                // In-flight production, one job per segment above the current value.
                int job = i - value;
                if (job >= 0 && job < productionCount && production[job] > fraction)
                    Fill(painter, x, rect, segmentWidth, fraction, production[job], colorPending);

                if (waveProgress >= 0f && fraction >= 1f)
                {
                    for (int slice = 0; slice < WaveSlices; slice++)
                    {
                        float alpha = ResourceHudMath.SliceWaveAlpha(i, segments, slice, WaveSlices, waveProgress);
                        if (alpha <= 0f) continue;
                        Fill(painter, x, rect, segmentWidth, (float)slice / WaveSlices, (slice + 1f) / WaveSlices,
                            new Color(1f, 1f, 1f, alpha));
                    }
                }
            }
        }

        private static void Fill(Painter2D painter, float x, Rect rect, float segmentWidth, float from, float to,
            Color color)
        {
            float x0 = x + segmentWidth * from;
            float x1 = x + segmentWidth * to;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x0, rect.yMin));
            painter.LineTo(new Vector2(x1, rect.yMin));
            painter.LineTo(new Vector2(x1, rect.yMax));
            painter.LineTo(new Vector2(x0, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private Color BaseColor()
        {
            return ResourceRingColors.BaseColorFor(value, colorCritical, colorLow, colorWarn,
                colorOk, colorGood, colorRich);
        }
    }
}
