using System;
using System.Linq;
using NodeWar.Backend;
using NodeWar.Simulation;
using NodeWar.UI;
using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class DistrictInfoFactsTests
    {
        [Test] public void ActiveDistricts_HaveFactsAndFallbackDescription()
        {
            foreach (int id in CatalogKeys.CatalogDistrictTypes)
            {
                var type = (DistrictType)id;
                Assert.That(DistrictInfoFacts.Describe(GameBalanceData.Default(), type, 0), Is.Not.Empty, type.ToString());
                Assert.That(DistrictFallback.Describe(type).Description, Is.Not.Empty);
            }
        }
        [Test] public void ProductionFacts_UseSuppliedBalance()
        {
            var b = GameBalanceData.Default(); b.maxWorkersPerNode = 7;
            b.districtStats = new[] { new DistrictStats { districtType = DistrictType.Farm, era = 2, productionTicks = 123 } };
            var facts = DistrictInfoFacts.Describe(b, DistrictType.Farm, 2);
            Assert.That(facts.Single(f => f.Label == "Production cycle").Value, Is.EqualTo("123"));
            Assert.That(facts.Single(f => f.Label == "Worker capacity").Value, Is.EqualTo("7"));
            Assert.That(facts.Single(f => f.Label == "Yield per cycle").Unit, Is.EqualTo("food"));
        }
        [Test] public void BankFacts_UseSuppliedCapacity()
        {
            var b = GameBalanceData.Default(); b.bankCapacity = 37; b.collectProgressPerUnit = 23;
            var facts = DistrictInfoFacts.Describe(b, DistrictType.Storehouse, 0);
            Assert.That(facts.Single(f => f.Label == "Bank capacity").Value, Is.EqualTo("37"));
            Assert.That(facts.Single(f => f.Label == "Collection per unit").Value, Is.EqualTo("23"));
            Assert.That(facts.Any(f => f.Label == "Worker capacity"), Is.False);
        }
        [Test] public void HealthFacts_MatchStructurePresentationMaximum()
        {
            var b = GameBalanceData.Default();
            b.districtStats = new[] { new DistrictStats { districtType = DistrictType.Infirmary, era = 1, healthMax = 765, healthRegenPerTick = 9 } };
            var facts = DistrictInfoFacts.Describe(b, DistrictType.Infirmary, 1);
            Assert.That(facts.Single(f => f.Label == "Maximum health").Value, Is.EqualTo("765"));
            Assert.That(StructurePresentation.HealthInformation(new NodeData { districtType = DistrictType.Infirmary, districtEra = 1 }, b, "Healing"), Does.Contain(" / 765"));
            Assert.That(facts.Single(f => f.Label == "Effect condition").Value, Is.EqualTo("Requires full health"));
        }
        [Test] public void DoesNotExposeLiveCommands()
        {
            foreach (var method in typeof(DistrictInfoFacts).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(GameCommand)));
                Assert.That(method.GetParameters().Any(p => p.ParameterType == typeof(SimulationState) || typeof(Delegate).IsAssignableFrom(p.ParameterType)), Is.False);
            }
        }
        [Test] public void ActionsAndEquipFacts_UseSuppliedBalance()
        {
            var b = GameBalanceData.Default(); b.recruitBaseCost = 17; b.minionMetalCost = 19;
            Assert.That(DistrictInfoFacts.Describe(b, DistrictType.Village, 0).Single(f => f.Label == "Initial recruit cost").Value, Is.EqualTo("17"));
            Assert.That(DistrictInfoFacts.Describe(b, DistrictType.Workshop, 0).Single(f => f.Label == "Forge cost").Value, Is.EqualTo("19"));
            var equips = DistrictInfoFacts.Describe(b, DistrictType.Barracks, 0);
            Assert.That(equips.Any(f => f.Label == "Equip Warrior food cost"), Is.True);
        }
        [Test] public void VariantFacts_UseExistingEntryAndFallbackWithoutLiveHealth()
        {
            var b = GameBalanceData.Default(); b.districtStats = new[] {
                new DistrictStats { districtType = DistrictType.Storehouse, era = 0, productionTicks = 43 },
                new DistrictStats { districtType = DistrictType.Storehouse, era = 2, productionTicks = 91 }
            };
            Assert.That(DistrictInfoFacts.Describe(b, DistrictType.Storehouse, 2).Single(f => f.Label == "Production cycle").Value, Is.EqualTo("91"));
            Assert.That(DistrictInfoFacts.Describe(b, DistrictType.Storehouse, 5).Single(f => f.Label == "Production cycle").Value, Is.EqualTo("43"));
            Assert.That(DistrictInfoFacts.Describe(b, DistrictType.Storehouse, 2).Any(f => f.Label == "Effect condition"), Is.False);
        }
        [Test] public void FortressAndInfirmaryFacts_DescribePotentialFromSuppliedBalance()
        {
            var b = GameBalanceData.Default(); b.districtStats = new[] {
                new DistrictStats { districtType = DistrictType.Fortress, fortressMaterialsCosts = new[] { 0, 11, 12, 13 },
                    fortressMetalCosts = new[] { 0, 21, 22, 23 }, fortressResistancePercent = new[] { 0, 31, 32, 33 } },
                new DistrictStats { districtType = DistrictType.Infirmary, healIntervalTicks = 77, respawnBoostPerWorker = 4, respawnCostReductionPercent = 19 }
            };
            var fortress = DistrictInfoFacts.Describe(b, DistrictType.Fortress, 0);
            Assert.That(fortress.Single(f => f.Label == "Level 2 upgrade materials cost").Value, Is.EqualTo("12"));
            Assert.That(fortress.Single(f => f.Label == "Level 2 upgrade metal alternative").Value, Is.EqualTo("22"));
            Assert.That(fortress.Single(f => f.Label == "Level 2 resistance potential").Value, Is.EqualTo("32"));
            var infirmary = DistrictInfoFacts.Describe(b, DistrictType.Infirmary, 0);
            Assert.That(infirmary.Single(f => f.Label == "Healing interval").Value, Is.EqualTo("77"));
            Assert.That(infirmary.Single(f => f.Label == "Respawn discount per worker").Value, Is.EqualTo("19"));
        }
    }
}
