using UnityEngine;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// Projects world-space renderer bounds into the screen rectangle the
    /// outline passes actually have to touch.
    ///
    /// The composite is a full-screen triangle running a sixteen-tap loop, but
    /// the groups it lights up occupy a few percent of a portrait phone screen.
    /// Everything outside their bounds reaches <c>if (nearest &gt; 1.0) discard</c>
    /// and is thrown away. Scissoring to this rect throws it away before the
    /// fragment shader runs instead, which is the same picture for a fraction of
    /// the fill.
    ///
    /// Deliberately free of MonoBehaviour and the render graph so the maths can
    /// be unit tested with a hand-built matrix, no scene and no pipeline --
    /// the same reason <see cref="OutlineIdAllocator"/> avoids UnityEngine.
    /// </summary>
    public static class OutlineScreenBounds
    {
        /// <summary>
        /// Smallest clip-space w treated as being in front of the camera.
        ///
        /// Not zero: a corner exactly on the near plane divides by something
        /// close enough to zero to produce an infinity, and an infinity folded
        /// into a min/max poisons the whole union rather than the one corner.
        /// </summary>
        private const float NearPlaneEpsilon = 1e-5f;

        /// <summary>An empty rect. Contributes nothing to a union.</summary>
        public static Rect Empty => default;

        public static bool IsEmpty(Rect rect) => rect.width <= 0f || rect.height <= 0f;

        /// <summary>The whole render target, for the cases that give up on bounds.</summary>
        public static Rect FullTarget(int targetWidth, int targetHeight) =>
            new Rect(0f, 0f, Mathf.Max(0, targetWidth), Mathf.Max(0, targetHeight));

        /// <summary>
        /// Grows <paramref name="union"/> to cover <paramref name="worldBounds"/>
        /// projected into target pixels, and reports whether the projection can
        /// be trusted.
        /// </summary>
        /// <returns>
        /// False when any corner of the box is at or behind the camera's near
        /// plane. A box straddling the near plane projects to a rectangle that
        /// is not merely wrong but inside out, and clamping it would quietly
        /// produce a small rect where the correct answer is a large one -- an
        /// outline that vanishes on one side of the screen. The caller is meant
        /// to fall back to <see cref="FullTarget"/> rather than to salvage this.
        ///
        /// This project's camera looks down at the board from above and never
        /// reaches geometry, so it is insurance rather than a live case.
        /// </returns>
        public static bool TryProject(Matrix4x4 viewProjection, Bounds worldBounds,
                                      int targetWidth, int targetHeight, ref Rect union)
        {
            Vector3 centre = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            // The eight corners, enumerated by sign bits rather than built into
            // an array. This runs per renderer per outlined group per frame and
            // the budget allows no per-frame allocation -- the same constraint
            // that makes OutlineMaskPass hold its draw list as structs.
            for (int corner = 0; corner < 8; corner++)
            {
                float x = centre.x + ((corner & 1) == 0 ? -extents.x : extents.x);
                float y = centre.y + ((corner & 2) == 0 ? -extents.y : extents.y);
                float z = centre.z + ((corner & 4) == 0 ? -extents.z : extents.z);

                Vector4 clip = viewProjection * new Vector4(x, y, z, 1f);

                if (clip.w <= NearPlaneEpsilon) return false;

                float invW = 1f / clip.w;

                // NDC to pixels, y up from the bottom-left. That is the
                // convention EnableScissorRect wants, and it is also what
                // GetProjectionMatrix gives -- as opposed to the flipped,
                // platform-specific matrix GetGPUProjectionMatrix returns,
                // which must not be used here.
                float px = (clip.x * invW * 0.5f + 0.5f) * targetWidth;
                float py = (clip.y * invW * 0.5f + 0.5f) * targetHeight;

                if (px < minX) minX = px;
                if (px > maxX) maxX = px;
                if (py < minY) minY = py;
                if (py > maxY) maxY = py;
            }

            minX = Mathf.Max(minX, 0f);
            minY = Mathf.Max(minY, 0f);
            maxX = Mathf.Min(maxX, targetWidth);
            maxY = Mathf.Min(maxY, targetHeight);

            // Entirely off-target. A real answer, not a failure: the group is
            // off screen, so it contributes nothing to the union and the caller
            // should keep trusting the other groups.
            if (maxX <= minX || maxY <= minY) return true;

            union = Combine(union, Rect.MinMaxRect(minX, minY, maxX, maxY));
            return true;
        }

        /// <summary>
        /// The smallest rect covering both. An empty operand is absorbed rather
        /// than treated as a rect at the origin, so a union can start from
        /// <see cref="Empty"/> without seeding a corner the groups never reach.
        /// </summary>
        public static Rect Combine(Rect a, Rect b)
        {
            if (IsEmpty(b)) return a;
            if (IsEmpty(a)) return b;

            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        /// <summary>
        /// Grows a rect by <paramref name="margin"/> on every side, clamped to
        /// the target and snapped outwards to whole pixels.
        ///
        /// Both halves matter. The margin is the outline's own width: the mask
        /// stops at the silhouette, and the composite paints up to a tap radius
        /// *outside* it, so a scissor at the bare bounds would shave the line
        /// off. The outward snap is because the GPU scissor is integral and
        /// rounding to nearest would lose a fractional pixel at the edge --
        /// which reads as an outline that is thinner on one side, the exact
        /// symptom that is hardest to attribute back to here.
        /// </summary>
        public static Rect Expand(Rect rect, float margin, int targetWidth, int targetHeight)
        {
            if (IsEmpty(rect)) return Empty;

            float minX = Mathf.Floor(Mathf.Max(0f, rect.xMin - margin));
            float minY = Mathf.Floor(Mathf.Max(0f, rect.yMin - margin));
            float maxX = Mathf.Ceil(Mathf.Min(targetWidth, rect.xMax + margin));
            float maxY = Mathf.Ceil(Mathf.Min(targetHeight, rect.yMax + margin));

            if (maxX <= minX || maxY <= minY) return Empty;

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }
}
