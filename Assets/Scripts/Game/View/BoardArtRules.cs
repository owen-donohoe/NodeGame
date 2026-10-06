using System;

namespace NodeWar.View
{
    /// <summary>
    /// The two small rules behind a district's board art, kept free of UnityEngine so they are
    /// tested: which prefab a node gets, and when the per-district placement tuning is a no-op.
    /// GameManager and BoardArtPlacer do the Unity half.
    /// </summary>
    public static class BoardArtRules
    {
        /// <summary>
        /// Which prefab a node uses: the district's DistrictVisual board prefab if it has one, else the
        /// per-district slot on GameManager, else the default. Nothing changes until an entry is filled.
        /// <paramref name="present"/> is Unity's own null test, passed in because a reference to a
        /// deleted asset is non-null to C# and null to Unity.
        /// </summary>
        public static T ChoosePrefab<T>(T fromTable, T fromSlot, T fallback, Func<T, bool> present) where T : class
        {
            if (present(fromTable)) return fromTable;
            if (present(fromSlot)) return fromSlot;
            return fallback;
        }

        private const float Epsilon = 1e-5f;

        /// <summary>True when the tuning changes nothing, so the instance is left exactly as the prefab made it.</summary>
        public static bool IsIdentity(float offsetX, float offsetY, float offsetZ,
            float eulerX, float eulerY, float eulerZ, float scale)
        {
            return Math.Abs(offsetX) < Epsilon && Math.Abs(offsetY) < Epsilon && Math.Abs(offsetZ) < Epsilon
                && Math.Abs(eulerX) < Epsilon && Math.Abs(eulerY) < Epsilon && Math.Abs(eulerZ) < Epsilon
                && Math.Abs(scale - 1f) < Epsilon;
        }

        /// <summary>A zero, negative or non-finite scale from the Inspector means "leave it": 1.</summary>
        public static float SafeScale(float scale)
        {
            return scale > 0f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
        }
    }
}
