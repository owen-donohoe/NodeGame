using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Per-villager cache of the curve VillagerView.BuildRouteCurve produces,
    /// so MovementPathRenderer can draw the exact same geometry instead of
    /// rebuilding it a second time every frame.
    ///
    /// Keyed by villagerID with the villager's movePath reference as the
    /// identity check: CommandProcessor.ApplyMove always assigns a freshly
    /// allocated array on a new Move, and GameSimulation.CollapseReversalLeg
    /// does the same when a reversal is cut short -- movePath is never
    /// mutated in place while a villager is simply walking it. A reference
    /// match therefore means "this is still the same route", cheaply.
    ///
    /// VillagerView is the only writer. PathCurve's own points/legStarts are
    /// scratch buffers shared by every caller and overwritten by the next
    /// Build -- for a different villager, most frames -- so a curve meant to
    /// survive past the call that built it has to be copied out into buffers
    /// this cache owns per villager.
    ///
    /// Array-backed and grown lazily, the same shape MovementPathRenderer
    /// already uses for its own per-villager tracking (routeOrderedAt,
    /// lastTargetNode). Reset on play mode start for the same reason
    /// OutlineRegistry resets its static instance: domain reload already
    /// covers this today, but the guard is cheap and the failure mode of a
    /// stale entry surviving into a new session is not.
    /// </summary>
    public static class RouteCurveCache
    {
        private struct Entry
        {
            public int[] builtFrom;
            public List<Vector3> points;
            public List<int> legStarts;
        }

        private static Entry[] entries = new Entry[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            entries = new Entry[0];
        }

        private static void EnsureCapacity(int villagerID)
        {
            if (villagerID < entries.Length) return;

            int newLength = Mathf.Max(villagerID + 1, entries.Length * 2);
            Entry[] grown = new Entry[newLength];
            for (int i = 0; i < entries.Length; i++) grown[i] = entries[i];
            entries = grown;
        }

        /// <summary>
        /// True and populated with the cached curve if villagerID already
        /// holds one built from this exact movePath reference. False means
        /// there is nothing to reuse -- either nothing has been built yet, or
        /// the route has changed since -- and the caller must build.
        /// </summary>
        public static bool TryGetCurrent(int villagerID, int[] currentMovePath,
                                         out List<Vector3> points, out List<int> legStarts)
        {
            if (villagerID >= 0 && villagerID < entries.Length &&
                entries[villagerID].points != null &&
                ReferenceEquals(entries[villagerID].builtFrom, currentMovePath))
            {
                points = entries[villagerID].points;
                legStarts = entries[villagerID].legStarts;
                return true;
            }

            points = null;
            legStarts = null;
            return false;
        }

        /// <summary>
        /// Stores the curve PathCurve just built for villagerID, copied out of
        /// its shared scratch buffers before anything else can overwrite them.
        /// Call immediately after PathCurve.Build.
        /// </summary>
        public static void Store(int villagerID, int[] builtFromMovePath)
        {
            EnsureCapacity(villagerID);

            if (entries[villagerID].points == null)
            {
                entries[villagerID].points = new List<Vector3>();
                entries[villagerID].legStarts = new List<int>();
            }

            PathCurve.CopyTo(entries[villagerID].points, entries[villagerID].legStarts);
            entries[villagerID].builtFrom = builtFromMovePath;
        }
    }
}
