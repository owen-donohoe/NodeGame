using System;
using System.Collections.Generic;
using System.Globalization;
using NodeWar.Simulation;
using NodeWar.UI;

namespace NodeWar.Lobby
{
    /// <summary>What one cell of a stacked stat bar shows, from the lowest baseline up.</summary>
    public enum StatSegment { Empty, Villager, BaseSuit, Gain, Loss }

    /// <summary>
    /// One bar per stat: solid up to the villager, a lighter tone up to the base suit, then this
    /// item's change against the base as green (better) or dashed red (worse) cells.
    /// </summary>
    public static class StatBar
    {
        public static StatSegment[] Cells(int villager, int baseValue, int thisValue, bool higherIsBetter, int unit)
        {
            int scale = Math.Max(1, unit);
            int v = Count(villager, scale), b = Count(baseValue, scale), t = Count(thisValue, scale);
            var cells = new StatSegment[StatHistogram.MaxCells];
            Span(villager, baseValue, v, b, out int low, out int high);
            bool baseHigher = Math.Abs((long)baseValue) >= Math.Abs((long)villager);
            for (int i = 0; i < low; i++) cells[i] = StatSegment.Villager;
            // Whatever the base suit adds over the villager is a lighter tone; what it gives up is a loss.
            bool baseBetter = baseHigher == higherIsBetter;
            for (int i = low; i < high; i++) cells[i] = baseBetter ? StatSegment.BaseSuit : StatSegment.Loss;
            Span(baseValue, thisValue, b, t, out low, out high);
            bool thisHigher = Math.Abs((long)thisValue) >= Math.Abs((long)baseValue);
            bool thisBetter = thisHigher == higherIsBetter;
            for (int i = low; i < high; i++) cells[i] = thisBetter ? StatSegment.Gain : StatSegment.Loss;
            return cells;
        }

        private static int Count(int value, int unit) => Math.Min(StatHistogram.MaxCells, StatHistogram.Cells(value, unit));

        // Two different values in one cell bucket still show one changed cell.
        private static void Span(int a, int b, int cellsA, int cellsB, out int low, out int high)
        {
            low = Math.Min(cellsA, cellsB); high = Math.Max(cellsA, cellsB);
            if (a != b && low == high) low = Math.Max(0, high - 1);
        }
    }

    /// <summary>Common filled cells plus the gained or struck portion; value and delta keep their raw units.</summary>
    public readonly struct HistogramRow
    {
        public string Label { get; }
        public int Filled { get; }
        public int Gain { get; }
        public int Loss { get; }
        public long Delta { get; }
        public int Value { get; }
        public int Unit { get; }
        public StatSegment[] Segments { get; }
        public bool Better { get; }
        public HistogramRow(string label, int filled, int gain, int loss, long delta, int value, int unit,
            StatSegment[] segments = null, bool better = false)
        { Label = label; Filled = filled; Gain = gain; Loss = loss; Delta = delta; Value = value; Unit = unit; Segments = segments; Better = better; }
    }

    /// <summary>Bounded integer cells, with the same scale for both comparisons of a suit stat.</summary>
    public static class StatHistogram
    {
        public const int MaxCells = 10;

        public static int Unit(params int[] values)
        {
            long peak = 0;
            if (values != null) foreach (int value in values) peak = Math.Max(peak, Math.Abs((long)value));
            return NiceUnit(peak);
        }
        private static int NiceUnit(long peak)
        {
            for (long scale = 1; ; scale *= 10)
                foreach (int step in new[] { 1, 2, 5 })
                    if (Count(peak, scale * step) <= MaxCells) return (int)(scale * step);
        }
        private static long Count(long value, long unit) => (Math.Abs(value) + unit - 1) / unit;
        public static int Cells(int value, int unit) => (int)Math.Min(int.MaxValue, Count(value, Math.Max(1, unit)));

        public static HistogramRow Row(string label, int reference, int value, bool higherIsBetter, int unit)
        {
            long delta = (long)value - reference;
            long peak = Math.Max(Math.Max(Math.Abs((long)reference), Math.Abs((long)value)), Math.Abs(delta));
            int scale = Math.Max(Math.Max(1, unit), NiceUnit(peak));
            int changed = (int)Count(delta, scale);
            int filled = (int)Math.Max(0, Math.Max(Count(reference, scale), Count(value, scale)) - changed);
            bool better = higherIsBetter ? delta > 0 : delta < 0;
            return new HistogramRow(label, filled, better ? changed : 0,
                !better && delta != 0 ? changed : 0, delta, value, scale,
                StatBar.Cells(reference, reference, value, higherIsBetter, scale), better);
        }
        public static HistogramRow[] SuitRows(LoadoutStatRow[] suit, LoadoutStatRow[] baseSuit)
        {
            var rows = new List<HistogramRow>();
            if (suit == null) return rows.ToArray();
            for (int i = 0; i < suit.Length; i++)
            {
                var own = suit[i];
                if (own.Label != "Bonus HP" && own.Label != "Damage" && own.Label != "Move time" &&
                    own.Label != "Attack cooldown" && own.Label != "Food cost" && own.Label != "Material cost") continue;
                var parent = baseSuit != null && i < baseSuit.Length ? baseSuit[i] : own;
                int villager = own.Value - own.Delta;
                if (own.Value == 0 && parent.Value == 0 && villager == 0) continue;
                long peak = Math.Max(Math.Abs((long)own.Value), Math.Max(Math.Abs((long)parent.Value), Math.Abs((long)villager)));
                peak = Math.Max(peak, Math.Max(Math.Abs((long)own.Value - parent.Value), Math.Abs((long)own.Value - villager)));
                int unit = NiceUnit(peak);
                var row = Row(own.Label, parent.Value, own.Value, own.HigherIsBetter, unit);
                rows.Add(new HistogramRow(row.Label, row.Filled, row.Gain, row.Loss, row.Delta, row.Value, row.Unit,
                    StatBar.Cells(villager, parent.Value, own.Value, own.HigherIsBetter, row.Unit), row.Better));
            }
            return rows.ToArray();
        }

        // Describe already selects the district's own production, ability and durability facts.
        // Shared collection/population rules and the costs of other items obscure that comparison.
        private static bool RelevantDistrictFact(string label) =>
            !label.StartsWith("Equip ", StringComparison.Ordinal) && label != "Population capacity" &&
            label != "Collection per unit" && label != "Collection rate";

        public static HistogramRow[] DistrictRows(GameBalanceData balance, DistrictType type, int variant)
        {
            var rows = new List<HistogramRow>();
            var baseline = DistrictInfoFacts.Describe(balance, type, 0);
            foreach (var fact in DistrictInfoFacts.Describe(balance, type, variant))
            {
                if (!RelevantDistrictFact(fact.Label)) continue;
                if (!int.TryParse(fact.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) continue;
                int reference = 0;
                foreach (var parent in baseline)
                    if (parent.Label == fact.Label && parent.Unit == fact.Unit)
                        int.TryParse(parent.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference);
                if (value == 0 && reference == 0) continue;
                string label = fact.Label;
                bool lower = label.IndexOf("cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("cycle", StringComparison.OrdinalIgnoreCase) >= 0 && label != "Yield per cycle" ||
                    label.IndexOf("wait", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("interval", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("move time", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.EndsWith("metal alternative", StringComparison.Ordinal) ||
                    label == "Collection per unit" || label == "Opening breach threshold";
                int unit = NiceUnit(Math.Max(Math.Max(Math.Abs((long)reference), Math.Abs((long)value)), Math.Abs((long)value - reference)));
                rows.Add(Row(label, reference, value, !lower, unit));
            }
            return rows.ToArray();
        }
    }
}
