using System;

namespace NodeWar.View
{
    /// <summary>Presentation-only size and bounded, index-stable crowd slots.</summary>
    public static class VillagerLayoutMath
    {
        public readonly struct Position
        {
            public readonly float X, Z;
            // Once capacity is exhausted, repeat existing slots instead of spilling.
            public readonly bool Overflow;
            public Position(float x, float z, bool overflow)
            { X = x; Z = z; Overflow = overflow; }
        }

        public static float ScaledSize(float authoredSize, float multiplier)
        {
            if (!PositiveFinite(multiplier)) multiplier = 1f;
            return authoredSize * multiplier;
        }

        public static float WorkSpacing(float authoredSpacing, float spriteWidth, float spacingMultiplier)
        {
            return Math.Max(authoredSpacing, ScaledSize(spriteWidth, spacingMultiplier));
        }

        public static Position Slot(int index, int count, float desiredSpacing, float maxRadius)
        {
            if (count <= 1) return new Position(0f, 0f, false);
            if (!PositiveFinite(maxRadius) || !PositiveFinite(desiredSpacing))
                return new Position(0f, 0f, index > 0);

            // Limit pathological Inspector spacing to 256 rings. Increasing spacing
            // retains the requested minimum and keeps layout work bounded.
            double spacing = Math.Max(desiredSpacing, (double)maxRadius / 256);
            int rings = (int)Math.Floor(maxRadius / spacing);
            bool center = rings > 0;
            if (rings == 0) rings = 1;
            int capacity = center ? 1 : 0;
            for (int ring = 1; ring <= rings; ring++)
                capacity += RingCapacity(center ? ring * spacing : maxRadius, spacing);

            index = Math.Max(0, index);
            bool overflow = index >= capacity;
            int slot = index % capacity;
            if (center)
            {
                if (slot == 0) return new Position(0f, 0f, overflow);
                slot--;
            }
            for (int ring = 1; ring <= rings; ring++)
            {
                double radius = center ? ring * spacing : maxRadius;
                int ringCapacity = RingCapacity(radius, spacing);
                if (slot < ringCapacity)
                {
                    double angle = slot * (2 * Math.PI / ringCapacity);
                    return new Position((float)(Math.Cos(angle) * radius),
                        (float)(Math.Sin(angle) * radius), overflow);
                }
                slot -= ringCapacity;
            }
            return new Position(0f, 0f, true);
        }

        private static int RingCapacity(double radius, double spacing)
        {
            if (spacing > 2 * radius) return 1;
            // Circumference alone overestimates capacity: adjacent chords must
            // also be at least the requested spacing, including the wrap seam.
            return Math.Max(1, (int)Math.Floor(Math.PI / Math.Asin(spacing / (2 * radius)) + 1e-9));
        }

        private static bool PositiveFinite(float value)
        { return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
