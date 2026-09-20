using System;

namespace NodeWar.View
{
    public enum ViewerMode { Player, Spectator }

    /// <summary>
    /// The four cardinal camera orientations, as quarter turns: yaw = 90 * side.
    /// A side is a viewing direction, not a player ID -- player 0 happens to
    /// look from side 2 on the default board, but nothing here knows that.
    ///
    /// Free of UnityEngine so dotnet/NodeWar.View.Tests can run it. Everything
    /// works in the ground plane as (x, z).
    /// </summary>
    public static class ViewSide
    {
        public const int Count = 4;

        /// <summary>Side-on for a board that runs along Z.</summary>
        public const int SpectatorDefault = 1;

        /// <summary>
        /// Room per group for the depth rank. A sprite's order is
        /// height * OrderStride + rank, so height always wins while the rank
        /// stays under the stride. Heights are clamped so the product stays
        /// inside Unity's 16-bit sortingOrder.
        /// </summary>
        public const int OrderStride = 256;
        public const int MaxHeight = 100;

        public static int Wrap(int side) => ((side % Count) + Count) % Count;

        public static float Yaw(int side) => 90f * Wrap(side);

        public static int Rotate(int side, int quarterTurns) => Wrap(side + quarterTurns);

        /// <summary>Nearest side to a yaw in degrees, so 44 -> 0 and 46 -> 1.</summary>
        public static int FromYaw(float yawDegrees)
        {
            return Wrap((int)Math.Floor(yawDegrees / 90f + 0.5f));
        }

        /// <summary>
        /// The horizontal direction the camera looks along. Exact table rather
        /// than sin/cos, so quarter turns never carry float noise.
        /// </summary>
        public static void Forward(int side, out float x, out float z)
        {
            switch (Wrap(side))
            {
                case 0: x = 0f; z = 1f; break;
                case 1: x = 1f; z = 0f; break;
                case 2: x = 0f; z = -1f; break;
                default: x = -1f; z = 0f; break;
            }
        }

        /// <summary>
        /// The transparency sort axis. Unity draws the larger projection onto
        /// it first, so the axis has to point away from the camera: it is the
        /// horizontal forward, and a nearer sprite draws over a further one.
        /// (Measured in a preview scene: camera looking +Z, CustomAxis +Z put
        /// the near sprite on top and -Z put the far one on top. The old
        /// per-player comment, P0 -> +Z and P1 -> -Z, had both inverted.)
        /// </summary>
        public static void SortAxis(int side, out float x, out float z)
        {
            Forward(side, out x, out z);
        }

        /// <summary>
        /// The side whose camera sits on the far side of a core from the board
        /// centre and looks back across it. The offset is snapped to the
        /// nearest quarter turn; an exact diagonal prefers the Z axis, and a
        /// core dead on the centre gives side 0.
        /// </summary>
        public static int FromCoreOffset(float dx, float dz)
        {
            float ax = Math.Abs(dx);
            float az = Math.Abs(dz);
            if (ax == 0f && az == 0f) return 0;

            if (az >= ax) return dz > 0f ? 2 : 0;
            return dx > 0f ? 3 : 1;
        }

        /// <summary>
        /// The one place that decides which side a viewer looks from. A player
        /// looks from behind their own core, so it works for any player count
        /// and board layout. coreX/coreZ are indexed by player ID, NaN when a
        /// core is not known yet, which falls back to side 0.
        /// </summary>
        public static int ResolveViewer(ViewerMode mode, int playerID,
            float[] coreX, float[] coreZ, float centreX, float centreZ)
        {
            if (mode == ViewerMode.Spectator) return SpectatorDefault;

            if (coreX == null || coreZ == null) return 0;
            if (playerID < 0 || playerID >= coreX.Length || playerID >= coreZ.Length) return 0;
            if (float.IsNaN(coreX[playerID]) || float.IsNaN(coreZ[playerID])) return 0;

            return FromCoreOffset(coreX[playerID] - centreX, coreZ[playerID] - centreZ);
        }

        /// <summary>How far back a ground point is along the sort axis: larger is further.</summary>
        public static float Depth(int side, float x, float z)
        {
            SortAxis(side, out float ax, out float az);
            return x * ax + z * az;
        }

        /// <summary>
        /// Fills orders[0..count) with height * OrderStride + depth rank, where
        /// rank 0 is the furthest sprite. Ties on depth fall back to index, so
        /// the order is total. O(n^2) and allocation-free: n is a node's
        /// sprites, and this runs on a POV change, not per frame.
        /// </summary>
        public static void ComputeOrders(float[] depth, int[] height, int count, int[] orders)
        {
            for (int i = 0; i < count; i++)
            {
                int rank = 0;
                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    if (depth[j] > depth[i] || (depth[j] == depth[i] && j < i)) rank++;
                }

                int h = height[i] < -MaxHeight ? -MaxHeight : (height[i] > MaxHeight ? MaxHeight : height[i]);
                orders[i] = h * OrderStride + rank;
            }
        }
    }
}
