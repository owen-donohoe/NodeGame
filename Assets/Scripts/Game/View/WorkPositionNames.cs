using System;
using System.Collections.Generic;

namespace NodeWar.View
{
    /// <summary>
    /// Which child transforms of a district prefab are villager work
    /// positions. The prefabs name them with the older "WorkSlot" prefix and
    /// are not re-authored by a code rename, so that spelling stays
    /// recognised; the order they are found in the hierarchy is the order
    /// villagers are given them.
    ///
    /// UnityEngine-free so the lookup can be tested without a scene.
    /// </summary>
    public static class WorkPositionNames
    {
        public const string LegacyPrefix = "WorkSlot";
        public const string IdleCenter = "IdleCenter";
        public const string ClaimCenter = "ClaimCenter";

        public static bool IsWorkPosition(string name)
        {
            return name != null && name.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        }

        /// <summary>Indices into <paramref name="names"/> of the work positions, in the order given.</summary>
        public static int[] Order(IList<string> names)
        {
            var found = new List<int>();
            for (int i = 0; i < names.Count; i++)
                if (IsWorkPosition(names[i])) found.Add(i);
            return found.ToArray();
        }
    }
}
