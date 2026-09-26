using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NodeWar.Progression
{
    public enum CatalogItemKind { Variant, Skin }

    public sealed class CatalogItem
    {
        public string Id { get; set; } = "";
        public CatalogItemKind Kind { get; set; }
        public string BaseId { get; set; } = "";
        public int Era { get; set; } = -1;
        public bool Retired { get; set; }
    }

    public static class CatalogValidation
    {
        private static readonly Regex IdPattern = new Regex(@"\A[a-z0-9_]+(\.[a-z0-9_]+)*\z");

        public static List<string> Validate(IReadOnlyList<CatalogItem> catalog, int arenaCount)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (arenaCount <= 0) throw new ArgumentOutOfRangeException(nameof(arenaCount));
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var variants = new HashSet<(string BaseId, int Era)>();
            foreach (var item in catalog)
            {
                if (item == null)
                {
                    errors.Add("Catalog items must not be null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Id) || !IdPattern.IsMatch(item.Id))
                    errors.Add($"Invalid item ID '{item.Id}'.");
                if (!ids.Add(item.Id)) errors.Add($"Duplicate item ID '{item.Id}'.");
                if (string.IsNullOrWhiteSpace(item.BaseId))
                    errors.Add($"Item '{item.Id}' must have a non-empty BaseId.");
                if (item.Kind == CatalogItemKind.Variant)
                {
                    if (item.Era < 0 || item.Era >= arenaCount)
                        errors.Add($"Variant '{item.Id}' has an out-of-range era.");
                    if (!item.Retired && !variants.Add((item.BaseId, item.Era)))
                        errors.Add($"Duplicate active variant for BaseId '{item.BaseId}', era {item.Era}.");
                }
                else if (item.Kind == CatalogItemKind.Skin)
                {
                    if (item.Era != -1) errors.Add($"Skin '{item.Id}' must have era -1.");
                }
                else
                    errors.Add($"Item '{item.Id}' has an unknown kind.");
            }
            return errors;
        }

        /// <summary>
        /// Checks identity stability separately from Validate's catalog shape checks.
        /// Retirement can change in either direction; identity fields cannot change.
        /// </summary>
        public static List<string> ValidateAgainstPrevious(
            IReadOnlyList<CatalogItem> previous, IReadOnlyList<CatalogItem> next)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (next == null) throw new ArgumentNullException(nameof(next));
            var errors = new List<string>();
            var previousById = Index(previous, "Previous", errors);
            var nextById = Index(next, "Next", errors);
            foreach (var item in previousById.Values)
            {
                if (!nextById.TryGetValue(item.Id, out var replacement))
                    errors.Add($"Item '{item.Id}' must not be removed or renamed.");
                else if (item.Kind != replacement.Kind || item.Era != replacement.Era ||
                    !string.Equals(item.BaseId, replacement.BaseId, StringComparison.Ordinal))
                    errors.Add($"Item ID '{item.Id}' must not be reused with a different Kind, BaseId or Era.");
            }
            return errors;
        }

        private static Dictionary<string, CatalogItem> Index(IReadOnlyList<CatalogItem> catalog,
            string name, List<string> errors)
        {
            var index = new Dictionary<string, CatalogItem>(StringComparer.Ordinal);
            foreach (var item in catalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                    errors.Add($"{name} catalog contains an item without an ID.");
                else if (!index.TryAdd(item.Id, item))
                    errors.Add($"{name} catalog contains duplicate item ID '{item.Id}'.");
            }
            return index;
        }
    }
}
