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
                    if (!string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase)) continue;
                    types.Add(type);
                    break;
                }
            }
            return types.ToArray();
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
