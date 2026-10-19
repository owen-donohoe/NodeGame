using System;
using System.Collections.Generic;
using System.Globalization;
using NodeWar.Simulation;
using NodeWar.UI;

namespace NodeWar.Lobby
{
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
        public HistogramRow(string label, int filled, int gain, int loss, long delta, int value, int unit)
        { Label = label; Filled = filled; Gain = gain; Loss = loss; Delta = delta; Value = value; Unit = unit; }
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
                !better && delta != 0 ? changed : 0, delta, value, scale);
        }
        public static HistogramRow[] SuitRows(LoadoutStatRow[] suit, LoadoutStatRow[] baseSuit)
        {
            var rows = new List<HistogramRow>();
            if (suit == null) return rows.ToArray();
            for (int i = 0; i < suit.Length; i++)
            {
                var own = suit[i];
                var parent = baseSuit != null && i < baseSuit.Length ? baseSuit[i] : own;
                int villager = own.Value - own.Delta;
                long peak = Math.Max(Math.Abs((long)own.Value), Math.Max(Math.Abs((long)parent.Value), Math.Abs((long)villager)));
                peak = Math.Max(peak, Math.Max(Math.Abs((long)own.Value - parent.Value), Math.Abs((long)own.Value - villager)));
                int unit = NiceUnit(peak);
                rows.Add(Row(own.Label + " vs base suit", parent.Value, own.Value, own.HigherIsBetter, unit));
                rows.Add(Row(own.Label + " vs villager", villager, own.Value, own.HigherIsBetter, unit));
            }
            return rows.ToArray();
        }

        public static HistogramRow[] DistrictRows(GameBalanceData balance, DistrictType type, int variant)
        {
            var rows = new List<HistogramRow>();
            var baseline = DistrictInfoFacts.Describe(balance, type, 0);
            foreach (var fact in DistrictInfoFacts.Describe(balance, type, variant))
            {
                if (!int.TryParse(fact.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) continue;
                int reference = 0;
                foreach (var parent in baseline)
                    if (parent.Label == fact.Label && parent.Unit == fact.Unit)
                        int.TryParse(parent.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out reference);
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
