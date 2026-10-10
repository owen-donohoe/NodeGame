using System;

namespace NodeWar.Lobby
{
    /// <summary>One reusable tile's placement, in item units rather than pixels.</summary>
    public readonly struct SkinBandSlot
    {
        public readonly long LogicalIndex;
        public readonly int ChoiceIndex;
        public readonly double Offset;
        public readonly double Scale;
        public readonly double Alpha;
        // The nearest tile (ties to the right), even while between snap points.
        // Repeated decorative tiles never create additional centered choices.
        public readonly bool Centered;

        public SkinBandSlot(long logicalIndex, int choiceIndex, double offset,
            double scale, double alpha, bool centered)
        {
            LogicalIndex = logicalIndex;
            ChoiceIndex = choiceIndex;
            Offset = offset;
            Scale = scale;
            Alpha = alpha;
            Centered = centered;
        }
    }

    /// <summary>Constant-cost layout for a looping skin band; no Unity or inventory state.</summary>
    public static class SkinBandMath
    {
        public const int VisibleCount = 13;
        private const int HalfVisible = VisibleCount / 2;
        private const double RebaseThreshold = 1048576;

        /// <summary>Returns -1 for an empty list, otherwise a nonnegative choice index.</summary>
        public static int Wrap(int index, int count)
        {
            ValidateCount(count);
            if (count == 0) return -1;
            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        public static int CenterIndex(double position, int count)
        {
            ValidatePosition(position);
            ValidateCount(count);
            if (count == 0) return -1;
            return Wrap((int)(Snap(Rebase(position, count)) % count), count);
        }

        /// <summary>Nearest integer item position, with midpoint ties toward increasing index.</summary>
        public static double Snap(double position)
        {
            ValidatePosition(position);
            double floor = Math.Floor(position);
            return position - floor >= 0.5 ? floor + 1 : floor;
        }

        /// <summary>
        /// Subtracts whole list cycles while retaining fractional offset and wrapped identity.
        /// Gesture adapters should periodically rebase their stored position (and drag/snap
        /// anchors together) before precision is lost. Fill also rebases large inputs locally;
        /// returned logical indices then refer to that local cycle. Empty lists rebase to zero.
        /// </summary>
        public static double Rebase(double position, int count)
        {
            ValidatePosition(position);
            ValidateCount(count);
            return count == 0 ? 0 : position % count;
        }

        /// <summary>
        /// Overwrites exactly 13 caller-owned slots without allocating. Empty slots have
        /// choice -1 and zero scale/alpha. A single choice may repeat decoratively, with
        /// only the nearest tile centered. Scale and alpha depend solely on distance.
        /// </summary>
        public static void Fill(double position, int count, SkinBandSlot[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length != VisibleCount)
                throw new ArgumentException("The band requires exactly 13 slots.", nameof(destination));
            ValidatePosition(position);
            ValidateCount(count);

            if (count == 0)
            {
                for (int i = 0; i < VisibleCount; i++)
                    destination[i] = new SkinBandSlot(0, -1, 0, 0, 0, false);
                return;
            }

            if (Math.Abs(position) >= RebaseThreshold) position = Rebase(position, count);
            long center = (long)Snap(position);
            for (int i = 0; i < VisibleCount; i++)
            {
                long logical = center + i - HalfVisible;
                int choice = Wrap((int)(logical % count), count);
                double offset = logical - position;
                double distance = Math.Abs(offset);
                double scale = 1 / (1 + 0.12 * distance);
                double alpha = Math.Max(0, 1 - distance / (HalfVisible + 1));
                destination[i] = new SkinBandSlot(logical, choice, offset, scale, alpha, i == HalfVisible);
            }
        }

        private static void ValidatePosition(double position)
        {
            if (double.IsNaN(position) || double.IsInfinity(position))
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be finite.");
        }

        private static void ValidateCount(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
