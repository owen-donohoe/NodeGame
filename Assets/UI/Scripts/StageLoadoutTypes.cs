using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public static class StageLoadoutTypes
    {
        public static int[] Districts(string[] ids) => Resolve(ids, false);
        public static int[] Suits(string[] ids) => Resolve(ids, true);

        private static int[] Resolve(string[] ids, bool suits)
        {
            var types = new List<int>();
            if (ids == null) return types.ToArray();
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                int length = suits ? CatalogKeys.SuitTableLength : CatalogKeys.DistrictTableLength;
                for (int type = 1; type < length; type++)
                {
                    string candidate = suits ? CatalogKeys.SuitLobbyId(type) : CatalogKeys.DistrictLobbyId(type);
                    string baseId = suits ? CatalogKeys.SuitBase(type) : CatalogKeys.DistrictBase(type);
                    bool variant = CatalogIds.TryParseVariant(id, out string parsedBase, out _) &&
                        string.Equals(parsedBase, baseId, StringComparison.OrdinalIgnoreCase);
                    if (!string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(id, baseId, StringComparison.OrdinalIgnoreCase) && !variant) continue;
                    types.Add(type);
                    break;
                }
            }
            return types.ToArray();
        }

        /// <summary>Chosen types first, then each extra group, skipping any type already present.</summary>
        public static int[] WithGranted(int[] chosen, params int[][] extras)
        {
            var result = new List<int>(chosen ?? new int[0]);
            foreach (var group in extras)
                foreach (int type in group)
                    if (type < 0 || !result.Contains(type)) result.Add(type);
            return result.ToArray();
        }

        public static bool Same<T>(T[] left, T[] right)
        {
            if (left == null || right == null) return left == right;
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
            return true;
        }
    }
}
