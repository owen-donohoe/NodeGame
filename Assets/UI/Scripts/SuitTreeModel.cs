using System;
using System.Collections.Generic;
using NodeWar.Backend;
using NodeWar.Simulation;

namespace NodeWar.Lobby
{
    public enum SuitNodeState { Locked, Available, Owned, Equipped }

    public enum StatTrend { Same, Better, Worse }

    public enum SuitNodeAction { None, Equip, Unlock }

    /// <summary>One suit variant as the tree draws it: the table's facts plus this player's state.</summary>
    public sealed class SuitNodeView
    {
        public string Id;
        public string BaseId;
        public int Variant;
        public int ParentVariant;
        public int Arena;
        public SuitUnlockKind Unlock;
        public int Cost;
        public SuitNodeState State;
        public bool Owned;
        /// <summary>The node's arena has been reached at the player's current arena.</summary>
        public bool Usable;

        public bool IsRoot => ParentVariant < 0;
        public bool CanEquip => Owned && Usable && State != SuitNodeState.Equipped;
    }

    /// <summary>One line of the info panel's stats table. Delta is against the parent when there is one.</summary>
    public sealed class StatRow
    {
        public string Label;
        public int Value;
        public bool HasParent;
        public int Delta;
        public StatTrend Trend;
        public string DeltaText;
        /// <summary>Plain-words fragments for the role line.</summary>
        internal string BetterPhrase;
        internal string WorsePhrase;
    }

    public struct SuitNodeButton
    {
        public SuitNodeAction Action;
        public string Label;
        public bool Enabled;
    }

    /// <summary>Where a node sits: X is 0..1 across the tree, Band indexes the arena gates top to bottom.</summary>
    public sealed class SuitNodeLayout
    {
        public int Variant;
        public int ParentVariant;
        public int Band;
        public float X;
    }

    /// <summary>
    /// What the suit tree view shows, decided without UnityEngine: node states from
    /// SuitTree plus the player's inventory and arena, layout, and the info panel's
    /// stat deltas, equip points and wording. The rules themselves stay in SuitTree.
    /// </summary>
    public static class SuitTreeModel
    {
        public static List<string> TreeSuits()
        {
            var result = new List<string>();
            foreach (var node in SuitTree.Nodes)
                if (!result.Contains(node.BaseId)) result.Add(node.BaseId);
            return result;
        }

        /// <summary>The arenas in which a suit debuts, ascending. The view's arena tabs.</summary>
        public static List<int> DebutArenas()
        {
            var result = new List<int>();
            foreach (var node in SuitTree.Nodes)
                if (node.IsRoot && !result.Contains(node.Arena)) result.Add(node.Arena);
            result.Sort();
            return result;
        }

        public static List<string> SuitsDebutingAt(int arena)
        {
            var result = new List<string>();
            foreach (var node in SuitTree.Nodes)
                if (node.IsRoot && node.Arena == arena) result.Add(node.BaseId);
            return result;
        }

        /// <summary>The suit's nodes in variant order, with this player's state. A null state is a new player.</summary>
        public static SuitNodeView[] Nodes(PlayerState state, string baseId)
        {
            var nodes = new List<SuitTreeNode>();
            foreach (var node in SuitTree.Nodes)
                if (node.BaseId == baseId) nodes.Add(node);
            nodes.Sort((a, b) => a.Variant.CompareTo(b.Variant));

            InventoryRecord inventory = state?.Inventory;
            int arena = state?.Rank != null ? state.Rank.Arena : 0;
            var owned = inventory?.OwnedVariants ?? new List<string>();
            string equipped = null;
            inventory?.Equipped?.Variants?.TryGetValue(baseId, out equipped);

            var result = new SuitNodeView[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                SuitTreeNode node = nodes[i];
                bool isOwned = owned.Contains(node.Id);
                bool usable = SuitTree.IsAvailable(node, arena);
                SuitNodeState nodeState;
                if (equipped == node.Id && isOwned && usable) nodeState = SuitNodeState.Equipped;
                else if (isOwned && usable) nodeState = SuitNodeState.Owned;
                else if (!isOwned && SuitTree.CanOwn(node, arena, owned)) nodeState = SuitNodeState.Available;
                else nodeState = SuitNodeState.Locked;
                result[i] = new SuitNodeView
                {
                    Id = node.Id, BaseId = baseId, Variant = node.Variant, ParentVariant = node.ParentVariant,
                    Arena = node.Arena, Unlock = node.Unlock, Cost = node.Cost,
                    State = nodeState, Owned = isOwned, Usable = usable
                };
            }
            return result;
        }

        /// <summary>The root is at the top, siblings spread evenly beneath their parent, one band per arena.</summary>
        public static SuitNodeLayout[] Layout(string baseId)
        {
            var nodes = new List<SuitTreeNode>();
            foreach (var node in SuitTree.Nodes)
                if (node.BaseId == baseId) nodes.Add(node);
            nodes.Sort((a, b) => a.Variant.CompareTo(b.Variant));

            var arenas = new List<int>();
            foreach (var node in nodes) if (!arenas.Contains(node.Arena)) arenas.Add(node.Arena);
            arenas.Sort();

            var result = new SuitNodeLayout[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                SuitTreeNode node = nodes[i];
                int siblings = 0, index = 0;
                foreach (var other in nodes)
                    if (other.ParentVariant == node.ParentVariant)
                    {
                        if (other.Variant < node.Variant) index++;
                        siblings++;
                    }
                result[i] = new SuitNodeLayout
                {
                    Variant = node.Variant,
                    ParentVariant = node.ParentVariant,
                    Band = arenas.IndexOf(node.Arena),
                    X = node.IsRoot ? 0.5f : (index + 1f) / (siblings + 1f)
                };
            }
            return result;
        }

        /// <summary>The arena of each gate band, top to bottom, for the band labels.</summary>
        public static List<int> BandArenas(string baseId)
        {
            var arenas = new List<int>();
            foreach (var node in SuitTree.Nodes)
                if (node.BaseId == baseId && !arenas.Contains(node.Arena)) arenas.Add(node.Arena);
            arenas.Sort();
            return arenas;
        }

        // ===== NAMES AND WORDING =====

        /// <summary>"suit.warrior" is "Warrior". Names for variants do not exist yet.</summary>
        public static string SuitName(string baseId)
        {
            if (string.IsNullOrEmpty(baseId)) return "";
            string name = baseId.StartsWith("suit.", StringComparison.Ordinal) ? baseId.Substring(5) : baseId;
            return name.Length == 0 ? "" : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>The root keeps the suit's name; siblings are lettered until they have names of their own.</summary>
        public static string NodeName(string baseId, int variant)
        {
            return variant <= SuitTree.RootVariant
                ? SuitName(baseId)
                : SuitName(baseId) + " " + (char)('A' + variant - 1);
        }

        public static string HowToGet(SuitNodeView node, SuitNodeView[] siblings)
        {
            if (node.State == SuitNodeState.Equipped) return "Owned and equipped";
            if (node.Owned && node.Usable) return "Owned";
            if (node.Owned) return "Owned. Needs arena " + node.Arena + " to field";
            if (node.State == SuitNodeState.Available)
                return node.Unlock == SuitUnlockKind.DropOnly
                    ? "Drop only"
                    : node.Cost > 0 ? "Unlock for " + node.Cost : "Unlock: cost not set yet";
            if (!node.Usable) return "Reach arena " + node.Arena;
            string parent = "its parent";
            foreach (var other in siblings)
                if (other.Variant == node.ParentVariant) parent = NodeName(node.BaseId, other.Variant);
            return "Needs " + parent + " first";
        }

        /// <summary>The one primary button: Equip when it can be, otherwise a disabled state saying why.</summary>
        public static SuitNodeButton Button(SuitNodeView node)
        {
            if (node.State == SuitNodeState.Equipped)
                return new SuitNodeButton { Action = SuitNodeAction.None, Label = "Equipped", Enabled = false };
            if (node.CanEquip)
                return new SuitNodeButton { Action = SuitNodeAction.Equip, Label = "Equip", Enabled = true };
            if (node.Owned)
                return new SuitNodeButton { Action = SuitNodeAction.None, Label = "Needs arena " + node.Arena, Enabled = false };
            // No currency exists yet, so unlocking is never enabled.
            return new SuitNodeButton { Action = SuitNodeAction.Unlock, Label = "Unlock", Enabled = false };
        }

        // ===== BALANCE-DERIVED =====

        public static bool TrySuitType(string baseId, out SuitType suit)
        {
            suit = SuitType.None;
            if (string.IsNullOrEmpty(baseId) || !baseId.StartsWith("suit.", StringComparison.Ordinal)) return false;
            return Enum.TryParse(baseId.Substring(5), true, out suit) && Enum.IsDefined(typeof(SuitType), suit);
        }

        private struct StatDef
        {
            public string Label;
            public bool HigherIsBetter;
            public string Better;
            public string Worse;
            public Func<SuitStats, int> Read;
        }

        private static readonly StatDef[] Defs =
        {
            new StatDef { Label = "Bonus HP", HigherIsBetter = true, Better = "tougher", Worse = "frailer", Read = s => s.bonusHP },
            new StatDef { Label = "Damage", HigherIsBetter = true, Better = "hits harder", Worse = "hits softer", Read = s => s.attackDamage },
            new StatDef { Label = "Move time", HigherIsBetter = false, Better = "faster", Worse = "slower", Read = s => s.moveSpeedTicks },
            new StatDef { Label = "Attack cooldown", HigherIsBetter = false, Better = "attacks faster", Worse = "attacks slower", Read = s => s.attackCooldownMax },
            new StatDef { Label = "Food cost", HigherIsBetter = false, Better = "cheaper in food", Worse = "costs more food", Read = s => s.foodCost },
            new StatDef { Label = "Material cost", HigherIsBetter = false, Better = "cheaper in materials", Worse = "costs more materials", Read = s => s.materialCost },
            new StatDef { Label = "Fight priority", HigherIsBetter = true, Better = "wins ties", Worse = "loses ties", Read = s => s.fightPriority },
        };

        /// <summary>
        /// The seven SuitStats fields for this variant (the era slot is the variant index),
        /// each against the parent's when parentVariant is not negative.
        /// </summary>
        public static StatRow[] StatRows(GameBalanceData balance, string baseId, int variant, int parentVariant)
        {
            TrySuitType(baseId, out SuitType suit);
            SuitStats own = balance.GetSuitStats(suit, variant);
            bool hasParent = parentVariant >= 0;
            SuitStats parent = hasParent ? balance.GetSuitStats(suit, parentVariant) : default;

            var rows = new StatRow[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                StatDef def = Defs[i];
                int value = def.Read(own);
                int delta = hasParent ? value - def.Read(parent) : 0;
                StatTrend trend = delta == 0 ? StatTrend.Same
                    : (delta > 0) == def.HigherIsBetter ? StatTrend.Better : StatTrend.Worse;
                rows[i] = new StatRow
                {
                    Label = def.Label, Value = value, HasParent = hasParent, Delta = delta, Trend = trend,
                    DeltaText = !hasParent ? "" : delta == 0 ? "same" : (delta > 0 ? "+" : "-") + Math.Abs(delta),
                    BetterPhrase = def.Better, WorsePhrase = def.Worse
                };
            }
            return rows;
        }

        /// <summary>One plain-words line. A root uses the suit's description; a variant says how it differs.</summary>
        public static string RoleLine(StatRow[] rows, bool isRoot, string suitName, string description)
        {
            if (isRoot) return string.IsNullOrEmpty(description) ? "The standard " + suitName + "." : description;
            var better = new List<string>();
            var worse = new List<string>();
            foreach (var row in rows)
            {
                if (row.Trend == StatTrend.Better) better.Add(row.BetterPhrase);
                else if (row.Trend == StatTrend.Worse) worse.Add(row.WorsePhrase);
            }
            if (better.Count == 0 && worse.Count == 0) return "Plays like " + suitName + " for now.";
            string line = better.Count > 0 ? Capitalise(string.Join(", ", better)) : Capitalise(string.Join(", ", worse));
            if (better.Count > 0 && worse.Count > 0) line += ", but " + string.Join(", ", worse);
            return line + ".";
        }

        /// <summary>
        /// The districts that accept the suit, asked of GameBalanceData.CanEquipSuitAtNode
        /// rather than restated, so a rule change there shows up here.
        /// </summary>
        public static List<DistrictType> EquipDistricts(GameBalanceData balance, string baseId)
        {
            var result = new List<DistrictType>();
            if (!TrySuitType(baseId, out SuitType suit)) return result;
            foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
                if (district != DistrictType.None && balance.CanEquipSuitAtNode(suit, district)) result.Add(district);
            return result;
        }

        private static string Capitalise(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
