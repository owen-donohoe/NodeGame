using System;
using System.Collections.Generic;
using System.Globalization;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>A static potential, never a live quantity or action.</summary>
    public readonly struct DistrictFact
    {
        public string Label { get; }
        public string Value { get; }
        public string Unit { get; }
        public DistrictFact(string label, string value, string unit = "")
        { Label = label; Value = value; Unit = unit; }
    }

    /// <summary>Balance-backed facts for lobby previews. Names/descriptions belong to DistrictFallback.</summary>
    public static class DistrictInfoFacts
    {
        public static int WorkerCapacity(GameBalanceData balance) => balance.maxWorkersPerNode;

        // Production pays one resource per worker cycle. There is no yield tuning field.
        public static string ProductionCycleText(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Farm: return "+1 {food} each cycle";
                case DistrictType.Mine: return "+1 {materials} each cycle";
                case DistrictType.Market: return "alternates {food} and {materials}";
                default: return "";
            }
        }

        public static DistrictFact[] Describe(GameBalanceData balance, DistrictType type, int variant)
        {
            if (DistrictFallback.Describe(type).Name == null) return Array.Empty<DistrictFact>();
            var facts = new List<DistrictFact>();
            var stats = balance.GetDistrictStats(type, variant);
            void Number(string label, int value, string unit = "") =>
                facts.Add(new DistrictFact(label, value.ToString(CultureInfo.InvariantCulture), unit));
            void Text(string label, string value) => facts.Add(new DistrictFact(label, value));

            if (type == DistrictType.Farm || type == DistrictType.Mine || type == DistrictType.Forge || type == DistrictType.Storehouse)
            {
                Number("Production cycle", stats.productionTicks, "ticks");
                Number("Yield per cycle", 1, type == DistrictType.Farm ? "food" : type == DistrictType.Mine ? "materials" :
                    type == DistrictType.Forge ? "metal" : "food / materials (alternating)");
                if (StructurePresentation.WorkerPresentation(type)) Number("Worker capacity", WorkerCapacity(balance), "workers");
                if (type == DistrictType.Forge) Number("Input per cycle", 1, "materials");
                if (type == DistrictType.Storehouse) Text("Production", "Automatic; no workers");
                Number("Bank capacity", balance.bankCapacity, "resources");
                Number("Collection per unit", balance.collectProgressPerUnit, "progress");
                Number("Collection rate", balance.collectProgressPerTick, "progress / tick");
            }
            switch (type)
            {
                case DistrictType.None:
                    Text("Role", "Connector"); break;
                case DistrictType.Village:
                    Number("Initial recruit cost", balance.recruitBaseCost, "food");
                    Number("Cost increase per recruit", balance.recruitCostPerRecruit, "food");
                    if (balance.TryRecruitCostAndCooldown(0, 0, out _, out int cooldown)) Number("Initial recruit cooldown", cooldown, "ticks");
                    Number("Population capacity", balance.maxVillagersPerPlayer, "villagers"); break;
                case DistrictType.Workshop:
                    Number("Forge cost", balance.minionMetalCost, "metal");
                    Number("Forge cooldown", stats.forgeCooldownTicks, "ticks");
                    Number("Minion health", balance.minionHP, "HP");
                    Number("Minion move time", balance.minionMoveSpeedTicks, "ticks");
                    Number("Population capacity", balance.maxVillagersPerPlayer, "villagers"); break;
                case DistrictType.Core:
                    Number("Opening breach threshold", balance.breachThreshold, "breaches");
                    Number("Respawn wait", balance.respawnTicks, "ticks");
                    Number("Initial respawn cost", balance.PaidRespawnCost(0, 0), "food"); break;
                case DistrictType.Pier:
                    Number("Owned travel divisor", stats.pierTravelDivisor); break;
                case DistrictType.Town:
                    Number("Grant once per player", stats.townBonusVillagers, "villagers"); break;
                case DistrictType.Infirmary:
                    Number("Healing interval", stats.healIntervalTicks, "ticks");
                    Number("Acolyte worker capacity", 2, "workers");
                    Number("Respawn boost per worker", stats.respawnBoostPerWorker, "timer ticks / tick");
                    Number("Respawn discount per worker", stats.respawnCostReductionPercent, "%"); break;
                case DistrictType.Fortress:
                    if (GameBalanceData.FortressStatsValid(stats))
                        for (int level = 0; level < stats.fortressResistancePercent.Length; level++)
                        {
                            Number("Level " + level + " resistance potential", stats.fortressResistancePercent[level], "%");
                            if (level == 0) continue;
                            Number("Level " + level + " upgrade materials cost", stats.fortressMaterialsCosts[level], "materials");
                            Number("Level " + level + " upgrade metal alternative", stats.fortressMetalCosts[level], "metal");
                        }
                    else Text("Upgrades", "Unavailable in supplied balance");
                    break;
            }
            foreach (SuitType suit in Enum.GetValues(typeof(SuitType)))
            {
                if (!balance.CanEquipSuitAtNode(suit, type)) continue;
                var suitStats = balance.GetSuitStats(suit, variant);
                Text("Equip " + StructurePresentation.SuitLabel(suit), "Requires drafted suit");
                Number("Equip " + StructurePresentation.SuitLabel(suit) + " food cost", suitStats.foodCost, "food");
                Number("Equip " + StructurePresentation.SuitLabel(suit) + " material cost", suitStats.materialCost, "materials");
            }
            // Same maximum read by StructurePresentation.HealthInformation, without a fake node.
            if (stats.healthMax > 0)
            {
                Number("Maximum health", stats.healthMax, "HP");
                Number("Health regeneration", stats.healthRegenPerTick, "HP / tick");
                Text("Effect condition", "Requires full health");
            }
            return facts.ToArray();
        }
    }
}
