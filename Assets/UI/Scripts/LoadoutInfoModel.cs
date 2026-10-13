using System;
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
    /// <summary>Read-only preview data, independent of tree layout and player inventory.</summary>
    public static class LoadoutInfoModel
    {
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
            public Func<SuitStats, int> Read;
        }

        private static readonly StatDef[] Defs =
        {
            new StatDef { Label = "Bonus HP", HigherIsBetter = true, Read = s => s.bonusHP },
            new StatDef { Label = "Damage", HigherIsBetter = true, Read = s => s.attackDamage },
            new StatDef { Label = "Move time", HigherIsBetter = false, Read = s => s.moveSpeedTicks },
            new StatDef { Label = "Attack cooldown", HigherIsBetter = false, Read = s => s.attackCooldownMax },
            new StatDef { Label = "Food cost", HigherIsBetter = false, Read = s => s.foodCost },
            new StatDef { Label = "Material cost", HigherIsBetter = false, Read = s => s.materialCost },
            new StatDef { Label = "Fight priority", HigherIsBetter = true, Read = s => s.fightPriority },
        };

    }
}
