using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NodeWar.Backend
{
    /// <summary>Stable catalog IDs. Names, not simulation enums, cross this boundary.</summary>
    public static class CatalogIds
    {
        public const int EraCount = 6;
        private static readonly Regex BasePattern = new Regex(@"\A(suit|district)\.[a-z0-9_]+\z");

        public static string SuitBase(string suitTypeName) => "suit." + suitTypeName.ToLowerInvariant();
        public static string DistrictBase(string districtTypeName) => "district." + districtTypeName.ToLowerInvariant();
        public static string Variant(string baseId, int era) => baseId + ".e" + era.ToString(CultureInfo.InvariantCulture);
        public static string DefaultSkin(string baseId) => "skin." + baseId + ".default";

        public static bool TryParseVariant(string id, out string baseId, out int era)
        {
            baseId = null;
            era = -1;
            if (id == null) return false;
            int separator = id.LastIndexOf(".e", StringComparison.Ordinal);
            if (separator < 0) return false;
            string candidate = id.Substring(0, separator);
            if (!BasePattern.IsMatch(candidate) ||
                !int.TryParse(id.Substring(separator + 2), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ||
                parsed < 0 || parsed >= EraCount || id != Variant(candidate, parsed)) return false;
            baseId = candidate;
            era = parsed;
            return true;
        }

        public static bool TryParseSkin(string id, out string baseId)
        {
            baseId = null;
            if (id == null || !id.StartsWith("skin.", StringComparison.Ordinal)) return false;
            int separator = id.LastIndexOf('.');
            if (separator <= 5) return false;
            string candidate = id.Substring(5, separator - 5);
            string skin = id.Substring(separator + 1);
            if (!BasePattern.IsMatch(candidate) || !Regex.IsMatch(skin, @"\A[a-z0-9_]+\z")) return false;
            baseId = candidate;
            return true;
        }
    }
}
