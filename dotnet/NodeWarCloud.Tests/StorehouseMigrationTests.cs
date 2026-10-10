using System.Collections.Generic;
using System.Linq;
using NodeWar.Backend;
using NodeWar.Progression;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class StorehouseMigrationTests
    {
        [Test]
        public void MarketIdsRetiredNotReused()
        {
            Assert.AreEqual("Market", System.Enum.GetName(typeof(DistrictType), 13));
            Assert.AreEqual("Storehouse", System.Enum.GetName(typeof(DistrictType), 18));
            Assert.IsFalse(DistrictRoster.IsActive((DistrictType)13)); Assert.IsTrue(DistrictRoster.IsActive((DistrictType)18));
            Assert.AreEqual(18, DistrictMigration.CanonicalType(13));
            CollectionAssert.AreEqual(new[] { "node_storehouse", "" }, DistrictMigration.Deck(new[] { "node_market", "node_storehouse" }, 2));
            var state = new PlayerState { Rank = new RankRecord { Arena = 3, HighestArena = 3 }, Inventory = PlayerStateDefaults.Inventory() };
            state.Inventory.OwnedVariants.Add("district.market.e3");
            state.Inventory.Equipped.Variants["district.market"] = "district.market.e3";
            Assert.IsTrue(DistrictMigration.Apply(state));
            CollectionAssert.Contains(state.Inventory.OwnedVariants, "district.market.e3");
            CollectionAssert.Contains(state.Inventory.OwnedVariants, "district.storehouse.e3");
            Assert.AreEqual("district.storehouse.e3", state.Inventory.Equipped.Variants["district.storehouse"]);
            Assert.IsFalse(state.Inventory.Equipped.Variants.ContainsKey("district.market"));
            Assert.IsFalse(DistrictMigration.Apply(state));
            var catalog = NodeWar.Cloud.ServerCatalog.Items;
            Assert.That(catalog.Where(i => i.BaseId == "district.market").Select(i => i.Retired), Is.Not.Empty.And.All.True);
            Assert.AreEqual(7, catalog.Count(i => i.BaseId == "district.storehouse" && !i.Retired));
            Assert.IsEmpty(CatalogValidation.ValidateAgainstPrevious(CatalogTests.CommittedCatalog(), catalog));
            var rules = new NodeWar.Cloud.InventoryRules(catalog);
            Assert.Throws<InventoryValidationException>(() => rules.Equip(state, new EquippedRecord {
                Variants = new Dictionary<string, string> { ["district.market"] = "district.market.e3" } }));
        }
    }
}
