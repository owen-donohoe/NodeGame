using System;
using System.Collections.Generic;

namespace NodeWar.Progression
{
    public static class EraUnlocks
    {
        /// <summary>Returns grants without mutating ownership. Catalogs must pass validation first.</summary>
        public static List<string> GrantsFor(IReadOnlyList<CatalogItem> catalog,
            int highestArena, ICollection<string> owned)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            var seen = new HashSet<string>(owned, StringComparer.Ordinal);
            var grants = new List<string>();
            foreach (var item in catalog)
                if (item.Kind == CatalogItemKind.Variant && !item.Retired &&
                    item.Era <= highestArena && seen.Add(item.Id))
                    grants.Add(item.Id);
            grants.Sort(StringComparer.Ordinal);
            return grants;
        }

        /// <summary>Checks arena eligibility, independently of ownership or retirement.</summary>
        public static bool IsUsable(CatalogItem variant, int currentArena)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));
            if (variant.Kind != CatalogItemKind.Variant)
                throw new ArgumentException("Expected a variant.", nameof(variant));
            return variant.Era <= currentArena;
        }
    }
}
