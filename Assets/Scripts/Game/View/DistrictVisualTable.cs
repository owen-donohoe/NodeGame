using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// The set of <see cref="DistrictVisual"/> assets, and the one place a
    /// caller turns a <see cref="DistrictType"/> into art.
    ///
    /// A table asset rather than a Resources folder scan or an addressable
    /// lookup by name: nothing in this project loads a sprite by name, and a
    /// serialised array is both the cheapest way to keep that true and the
    /// thing an artist can look at to see what is still empty.
    ///
    /// Lookup is a linear scan, matching GetStickerSprite in
    /// DraftScreenController. Fourteen entries, called on a district changing
    /// rather than per frame.
    /// </summary>
    [CreateAssetMenu(fileName = "DistrictVisualTable", menuName = "NodeWar/District Visual Table")]
    public sealed class DistrictVisualTable : ScriptableObject
    {
        [Tooltip("One entry per DistrictType. Tools > Node War > Create District " +
                 "Visuals fills this; a duplicate or a null is reported by OnValidate.")]
        [SerializeField] private DistrictVisual[] entries;

        /// <summary>
        /// The visual for a district, or null when the table has no entry. Null
        /// is a legitimate answer -- three districts have no art -- so callers
        /// check rather than assume.
        /// </summary>
        public DistrictVisual For(DistrictType district)
        {
            if (entries == null) return null;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].district == district) return entries[i];
            }

            return null;
        }

        /// <summary>How many districts have nothing drawn for them. The art checklist's number.</summary>
        public int CountUnillustrated()
        {
            if (entries == null) return 0;

            int n = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].IsUnillustrated) n++;
            }

            return n;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Two entries claiming the same district makes For() depend on array
        /// order, which is invisible in the Inspector and silent at runtime.
        /// Catching it here costs nothing and turns a mystery into a warning.
        /// </summary>
        private void OnValidate()
        {
            if (entries == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;

                for (int j = i + 1; j < entries.Length; j++)
                {
                    if (entries[j] != null && entries[j].district == entries[i].district)
                    {
                        Debug.LogWarning("[DistrictVisualTable] " + name + " has two entries for " +
                                         entries[i].district + " (" + entries[i].name + ", " +
                                         entries[j].name + "). Lookup will take the first.", this);
                    }
                }
            }
        }
#endif
    }
}
