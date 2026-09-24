using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>
    /// Three concentric segmented semicircles for one resource - outer 1-10,
    /// middle 11-20, inner 21-30 - drawn with Painter2D the way ProgressDial
    /// draws its single arc. Each ring is the upper half of a circle, flat
    /// side down: all three share one centre sitting on the host's bottom
    /// edge, so every ring's flat base lines up along it. Segment counts, the
    /// colour stop and both animation curves come from ResourceRingMath; only
    /// the HSV shade per ring (middle darker and more saturated, inner darker
    /// and more saturated again) lives here, since it is a rendering detail
    /// rather than presentation maths worth testing in isolation.
    ///
    /// NOTHING IS DRAWN BEHIND THE VALUE. There is no unlit track: a segment
    /// the resource has not reached is not drawn at all, so the ring is the
    /// amount and the sheet behind it is the background.
    ///
    /// THREE THINGS IN ONE ARC. Lit, production, spend ghost - all of them
    /// ResourceRingMath.SegmentFraction asked per segment, which is what keeps
    /// the gaps between them intact - a gain of two sweeps in as two separate
    /// sections rather than one merged arc. See DrawRing for the stacking
    /// order and why the ghost goes on last.
    ///
    ///   A GAIN sweeps. The arc eases from where it stood up to the new value
    ///   over ResourceRingMath.FillSeconds.
    ///
    ///   PRODUCTION shows what is on its way, in a dimmer white, one job per
    ///   segment above the current value - four food with two farms half done
    ///   puts both segments five and six at half. It is not animated here at
    ///   all: ResourceProduction reads the simulation's own timers, so the
    ///   fill moves at the rate the thing it reports moves at, and a job that
    ///   stops before landing simply stops being reported.
    ///
    ///   A SPEND lands at once and leaves white behind it. The lit arc snaps
    ///   to the new value - the pop belongs to the host, see
    ///   GameplayHUDController's ResourceReadout - and a white band is left
    ///   standing where the spent amount was, holding nearly still for six
    ///   tenths of a second and then closing fast-to-slow. That is the breach
    ///   wall's ghost, in a curve rather than a USS transition because
    ///   Painter2D cannot be transitioned.
    ///
    /// The two animated ones run off one scheduled tick that pauses the moment
    /// nothing is moving, so a ring at rest costs a repaint only when its value
    /// changes - production, which changes every frame it exists, repaints
    /// through SetProduction instead.
    ///
    /// Colours are read from the stylesheet via CustomStyleResolvedEvent, the
    /// same as ProgressDial, so the ring restyles with the theme rather than
    /// hard-coding a palette here. The six-stop read and blend are
    /// ResourceRingColors, shared with NodeSheet's ResourceChip; only the
    /// two whites, which nothing else draws, are read directly here.
    /// </summary>
    public class ResourceRing : VisualElement
    {
        private const float Thickness = 8f;
        private const float RingGap = 3f;
        private const float SegmentGapDegrees = 5f;

        /// <summary>Tick period while something is moving. Paused otherwise.</summary>
        private const long TickMilliseconds = 16;

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
        private Color colorGhost = new Color(1f, 1f, 1f, 0.6f);
        private Color colorPending = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>The real value. Colour and both animations aim at this.</summary>
        private int currentValue;
        private bool hasValue;

        // The lit arc's leading edge, and the sweep carrying it to currentValue.
        private float shownValue;
        private float fillFrom;
        private float fillElapsed;
        private float fillSeconds;

        // The white band's outer edge, and where the spend that left it stood.
        // ghostFrom no higher than currentValue means nothing is outstanding.
        private float ghostValue;
        private float ghostFrom;
        private float ghostElapsed;

        // Production in flight, nearest to landing first: job i fills the
        // segment at unit currentValue + i. Written whole by SetProduction,
        // never animated here - the progress is the simulation's, not a curve.
        private readonly float[] production = new float[ResourceProduction.MaxInFlight];
        private int productionCount;

        private IVisualElementScheduledItem tick;
        private double lastTickTime;

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

            ResourceRingColors.Read(style, ref colorCritical, ref colorLow, ref colorWarn,
                ref colorOk, ref colorGood, ref colorRich);
            ResourceRingColors.TryRead(style, "--ring-ghost", ref colorGhost);
            ResourceRingColors.TryRead(style, "--ring-pending", ref colorPending);

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Sets the resource amount the rings show. A rise sweeps in, a fall
        /// lands at once and leaves a ghost. <paramref name="snap"/> skips
        /// both and draws the new value flat - for the first render and for a
        /// viewer switch, where the number changed but nothing happened in the
        /// match, the same cases GameplayHUDController holds its pop for.
        /// </summary>
        public void SetValue(int value, bool snap = false)
        {
            if (hasValue && value == currentValue) return;

            int previous = currentValue;
            bool wasFirst = !hasValue;

            currentValue = value;
            hasValue = true;

            if (snap || wasFirst)
            {
                StopTicking();
                shownValue = value;
                ghostValue = value;
                ghostFrom = value;
                fillSeconds = 0f;
                MarkDirtyRepaint();
                return;
            }

            if (value > previous)
            {
                // A gain sweeps from wherever the arc currently stands, which
                // may be mid-sweep already. An outstanding ghost is left alone:
                // the lit arc rises underneath it, and the white still showing
                // is the part of the spend not yet paid back.
                fillFrom = shownValue;
                fillElapsed = 0f;
                fillSeconds = ResourceRingMath.FillSeconds(value - fillFrom);
            }
            else
            {
                // A spend. The arc lands now; white is left where it was.
                // Whichever of the two stood higher is where the band starts,
                // so spending twice in a row cannot shorten the white.
                float from = shownValue > ghostValue ? shownValue : ghostValue;

                shownValue = value;
                fillSeconds = 0f;

                ghostFrom = from;
                ghostValue = from;
                ghostElapsed = 0f;
            }

            StartTicking();
        }

        /// <summary>
        /// The player's in-flight production for this resource, nearest to
        /// landing first - see ResourceProduction. Job 0 fills the segment
        /// that will light next, job 1 the one after it, and so on.
        ///
        /// Nothing is eased here. The fraction is the simulation's own timer
        /// read through the tick alpha, so the fill moves at exactly the rate
        /// the thing it reports moves at; inventing a curve on top would make
        /// it lie about when the resource lands. A job that stops - the
        /// villager moved, died, or a forge ran out of materials - simply
        /// stops being reported and its white goes.
        /// </summary>
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

        private void StartTicking()
        {
            lastTickTime = Time.unscaledTimeAsDouble;

            if (tick == null) tick = schedule.Execute(Tick).Every(TickMilliseconds);
            else tick.Resume();

            MarkDirtyRepaint();
        }

        private void StopTicking()
        {
            if (tick != null) tick.Pause();
        }

        private void Tick()
        {
            double now = Time.unscaledTimeAsDouble;
            float delta = (float)(now - lastTickTime);
            lastTickTime = now;
            if (delta <= 0f) return;

            bool moving = false;

            if (fillSeconds > 0f)
            {
                fillElapsed += delta;
                float t = fillElapsed / fillSeconds;

                if (t >= 1f)
                {
                    shownValue = currentValue;
                    fillSeconds = 0f;
                }
                else
                {
                    shownValue = fillFrom + (currentValue - fillFrom) * ResourceRingMath.FillEase(t);
                    moving = true;
                }
            }
            else if (shownValue != currentValue)
            {
                shownValue = currentValue;
            }

            if (ghostFrom > currentValue)
            {
                ghostElapsed += delta;
                float closed = ResourceRingMath.GhostFraction(ghostElapsed);

                if (closed >= 1f)
                {
                    ghostValue = currentValue;
                    ghostFrom = currentValue;
                }
                else
                {
                    ghostValue = ghostFrom + (currentValue - ghostFrom) * closed;
                    moving = true;
                }
            }
            else
            {
                ghostValue = currentValue;
            }

            MarkDirtyRepaint();

            if (!moving) StopTicking();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;

            float step = Thickness + RingGap;
            float minWidth = 2f * (ResourceRingMath.RingCount * Thickness + (ResourceRingMath.RingCount - 1) * RingGap);
            if (rect.width <= minWidth) return;

            // Flat side down: the diameter runs along the host's bottom edge,
            // so the centre sits there too and every ring bulges upward from
            // it rather than surrounding a mid-box centre.
            Vector2 centre = new Vector2(rect.center.x, rect.yMax - Thickness * 0.5f);
            float outerRadius = rect.width * 0.5f - Thickness * 0.5f;

            Painter2D painter = context.painter2D;
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Butt;

            // The colour follows the real value, not the sweeping one, so a
            // gain across a stop boundary changes colour once on landing
            // rather than part-way through its own animation.
            Color baseColor = BaseColorFor(currentValue);

            for (int ring = 0; ring < ResourceRingMath.RingCount; ring++)
            {
                float radius = outerRadius - ring * step;
                DrawRing(painter, centre, radius, ring, ShadeForRing(baseColor, ring));
            }
        }

        /// <summary>
        /// One ring, segment by segment. Each segment draws up to three things
        /// stacked in the same arc, and asking per segment rather than per ring
        /// is what keeps the gaps between them unpainted by any of the three.
        ///
        ///   THE LIT HEAD, in the resource's colour: what the player has.
        ///   PRODUCTION, dim white: what is on its way, one job per segment,
        ///     filling at the rate its timer is actually running at.
        ///   THE SPEND GHOST, solid white: what just left.
        ///
        /// The ghost goes on last, so where a spend covers a segment that is
        /// also producing, the heavier white wins - and as the ghost closes it
        /// uncovers the dimmer one underneath rather than hiding it for the
        /// whole beat. Leaving is louder than arriving, which is the right way
        /// round.
        /// </summary>
        private void DrawRing(Painter2D painter, Vector2 centre, float radius, int ring, Color litColor)
        {
            float sweepPerSegment = ResourceRingMath.SweepDegrees / ResourceRingMath.SegmentsPerRing;
            float drawSweep = sweepPerSegment - SegmentGapDegrees;

            for (int i = 0; i < ResourceRingMath.SegmentsPerRing; i++)
            {
                float lit = ResourceRingMath.SegmentFraction(shownValue, ring, i);
                float ghost = ResourceRingMath.SegmentFraction(ghostValue, ring, i);
                float pending = PendingFor(ring, i);

                if (lit <= 0f && ghost <= 0f && pending <= 0f) continue;

                float start = ResourceRingMath.StartDegrees + i * sweepPerSegment + SegmentGapDegrees * 0.5f;

                if (lit > 0f) StrokeArc(painter, centre, radius, start, drawSweep, 0f, lit, litColor);
                if (pending > lit) StrokeArc(painter, centre, radius, start, drawSweep, lit, pending, colorPending);
                if (ghost > lit) StrokeArc(painter, centre, radius, start, drawSweep, lit, ghost, colorGhost);
            }
        }

        /// <summary>
        /// The in-flight job sitting on this segment, if there is one. Segments
        /// are counted in whole units from zero, so the segment at unit
        /// currentValue holds job 0 - the next one to land - and the queue runs
        /// upward from there.
        /// </summary>
        private float PendingFor(int ring, int segment)
        {
            int job = ring * ResourceRingMath.SegmentsPerRing + segment - currentValue;
            if (job < 0 || job >= productionCount) return 0f;
            return production[job];
        }

        /// <summary>Strokes the from..to fraction of one segment's arc.</summary>
        private static void StrokeArc(Painter2D painter, Vector2 centre, float radius,
            float segmentStart, float segmentSweep, float from, float to, Color color)
        {
            painter.strokeColor = color;
            painter.BeginPath();
            painter.Arc(centre, radius,
                segmentStart + segmentSweep * from,
                segmentStart + segmentSweep * to);
            painter.Stroke();
        }

        private Color BaseColorFor(int value)
        {
            return ResourceRingColors.BaseColorFor(value, colorCritical, colorLow, colorWarn,
                colorOk, colorGood, colorRich);
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
