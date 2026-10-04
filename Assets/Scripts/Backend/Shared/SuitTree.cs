using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>How a tree node is acquired once its arena is reached.</summary>
    public enum SuitUnlockKind { Cost, DropOnly }

    /// <summary>
    /// One suit variant. Variant is the existing era slot of the catalog ID
    /// (suit.warrior.e1 is Variant 1); for suits it names a role, not a power level.
    /// </summary>
    public sealed class SuitTreeNode
    {
        public readonly string BaseId;
        public readonly int Variant;
        /// <summary>Variant index of the parent node, -1 for a root.</summary>
        public readonly int ParentVariant;
        /// <summary>The first arena in which the node can be owned and fielded.</summary>
        public readonly int Arena;
        public readonly SuitUnlockKind Unlock;
        /// <summary>
        /// Placeholder. The catalog has no currency yet, so this has no unit; 0 until
        /// the unlock stage defines one.
        /// </summary>
        public readonly int Cost;

        public SuitTreeNode(string baseId, int variant, int parentVariant, int arena, SuitUnlockKind unlock, int cost)
        {
            BaseId = baseId;
            Variant = variant;
            ParentVariant = parentVariant;
            Arena = arena;
            Unlock = unlock;
            Cost = cost;
        }

        public bool IsRoot => ParentVariant < 0;
        public string Id => CatalogIds.Variant(BaseId, Variant);
        public string ParentId => IsRoot ? null : CatalogIds.Variant(BaseId, ParentVariant);
    }

    /// <summary>
    /// The suit variant tree and its rules. Shared by the client and the Cloud Code
    /// module, so both read one table. Per tree suit: a root (variant 0) and two
    /// sibling sidegrades, available in the arena after the root debuts. Suits not in
    /// the table (the production suits) and all districts are outside the tree and
    /// keep the era rules.
    /// </summary>
    public static class SuitTree
    {
        public const int RootVariant = 0;

        private static readonly SuitTreeNode[] nodes = Build();

        public static IReadOnlyList<SuitTreeNode> Nodes => nodes;

        private static SuitTreeNode[] Build()
        {
            var list = new List<SuitTreeNode>();
            // Combat suits, by debut arena. Variants arrive one arena later.
            AddSuit(list, "Warrior", 0);
            AddSuit(list, "Guardian", 0);
            AddSuit(list, "Scout", 0);
            AddSuit(list, "Berserker", 1);
            AddSuit(list, "Medic", 1);
            return list.ToArray();
        }

        private static void AddSuit(List<SuitTreeNode> list, string suitName, int debutArena)
        {
            string baseId = CatalogIds.SuitBase(suitName);
            list.Add(new SuitTreeNode(baseId, 0, -1, debutArena, SuitUnlockKind.Cost, 0));
            list.Add(new SuitTreeNode(baseId, 1, 0, debutArena + 1, SuitUnlockKind.Cost, 0));
            list.Add(new SuitTreeNode(baseId, 2, 0, debutArena + 1, SuitUnlockKind.Cost, 0));
        }

        /// <summary>True when the base has a tree, so its variants follow the tree rules.</summary>
        public static bool IsTreeSuit(string baseId) => TryGet(baseId, RootVariant, out _);

        public static bool TryGet(string baseId, int variant, out SuitTreeNode node)
        {
            foreach (var candidate in nodes)
                if (candidate.Variant == variant && string.Equals(candidate.BaseId, baseId, StringComparison.Ordinal))
                {
                    node = candidate;
                    return true;
                }
            node = null;
            return false;
        }

        public static bool TryGet(string variantId, out SuitTreeNode node)
        {
            node = null;
            return CatalogIds.TryParseVariant(variantId, out string baseId, out int variant) &&
                TryGet(baseId, variant, out node);
        }

        public static SuitTreeNode Root(string baseId) => TryGet(baseId, RootVariant, out var node) ? node : null;

        public static bool IsAvailable(SuitTreeNode node, int arena)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            return arena >= node.Arena;
        }

        /// <summary>False for anything outside the table, including old era 3-5 suit variants.</summary>
        public static bool IsAvailable(string variantId, int arena) =>
            TryGet(variantId, out var node) && IsAvailable(node, arena);

        /// <summary>
        /// Whether a player who has reached highestArena and owns the given variants
        /// may own this node: its arena is reached and its parent is owned.
        /// </summary>
        public static bool CanOwn(SuitTreeNode node, int highestArena, ICollection<string> owned)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            if (highestArena < node.Arena) return false;
            return node.IsRoot || owned.Contains(node.ParentId);
        }

        /// <summary>
        /// Owned suit-variant IDs that the table cannot account for: tree-suit variants
        /// outside the table (eras 3-5 from before the tree), above the player's highest
        /// arena, or missing their parent. Ownership is never removed for these; they
        /// simply cannot be equipped. Districts and non-tree suits are not reported.
        /// </summary>
        public static List<string> OwnershipProblems(ICollection<string> owned, int highestArena)
        {
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            var problems = new List<string>();
            foreach (string id in owned)
            {
                if (!CatalogIds.TryParseVariant(id, out string baseId, out int variant) || !IsTreeSuit(baseId)) continue;
                if (!TryGet(baseId, variant, out var node) || !CanOwn(node, highestArena, owned)) problems.Add(id);
            }
            problems.Sort(StringComparer.Ordinal);
            return problems;
        }
    }
}
