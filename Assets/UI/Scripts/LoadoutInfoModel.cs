using System;
using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.Lobby
{
    public enum StatTrend { Same, Better, Worse }

    /// <summary>Raw suit value and delta from the base villager; positive improvement is better.</summary>
    public readonly struct LoadoutStatRow
    {
        public string Label { get; }
        public int Value { get; }
        public int Delta { get; }
        public int Improvement { get; }
        public StatTrend Trend => Improvement == 0 ? StatTrend.Same : Improvement > 0 ? StatTrend.Better : StatTrend.Worse;
        public LoadoutStatRow(string label, int value, int delta, int improvement)
        { Label = label; Value = value; Delta = delta; Improvement = improvement; }
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

    /// <summary>Read-only preview data, independent of tree layout and player inventory.</summary>
    public static class LoadoutInfoModel
    {
        public static string SuitDescription(string description) => description ?? "";

        internal static bool TrySuitType(string baseId, out SuitType suit)
        {
            suit = SuitType.None;
            if (string.IsNullOrEmpty(baseId) || !baseId.StartsWith("suit.", StringComparison.Ordinal)) return false;
            return Enum.TryParse(baseId.Substring(5), true, out suit) && Enum.IsDefined(typeof(SuitType), suit);
        }

        public static LoadoutStatRow[] SuitStats(GameBalanceData balance, string baseId, int variant)
        {
            if (!TrySuitType(baseId, out SuitType suit)) return Array.Empty<LoadoutStatRow>();
            // None can have an explicit balance row. Otherwise use the unsuited
            // villager fields: GetSuitStats returns zero for a missing table row.
            if (!balance.TryGetSuitStats(SuitType.None, 0, out var baseline))
                baseline = new SuitStats { suitType = SuitType.None, attackDamage = balance.baseAttackDamage,
                    moveSpeedTicks = balance.baseMoveSpeedTicks, attackCooldownMax = balance.baseAttackCooldownMax };
            var own = suit == SuitType.None ? baseline : balance.GetSuitStats(suit, variant);
            var rows = new LoadoutStatRow[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                var def = Defs[i];
                int value = def.Read(own), delta = value - def.Read(baseline);
                rows[i] = new LoadoutStatRow(def.Label, value, delta, def.HigherIsBetter ? delta : -delta);
            }
            return rows;
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
        internal static StatRow[] Compare(GameBalanceData balance, string baseId, int variant, int parentVariant)
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
        internal static string RoleLine(StatRow[] rows, bool isRoot, string suitName, string description)
        {
            if (isRoot) return string.IsNullOrEmpty(description) ? "The standard " + suitName + "." : SuitDescription(description);
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

        private static string Capitalise(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
