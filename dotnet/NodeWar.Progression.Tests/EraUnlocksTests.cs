using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class EraUnlocksTests
    {
        [Test]
        public void GrantsOnlyUnownedActiveVariantsUpToHighestArenaInOrdinalOrder()
        {
            var catalog = new[]
            {
                Variant("z.variant", 1), Variant("a.variant", 0), Variant("owned", 1),
                Variant("future", 2), Variant("retired", 0, true),
                new CatalogItem { Id = "skin", Kind = CatalogItemKind.Skin, BaseId = "suit.warrior" }
            };
            var owned = new List<string> { "owned" };
            CollectionAssert.AreEqual(new[] { "a.variant", "z.variant" }, EraUnlocks.GrantsFor(catalog, 1, owned));
            CollectionAssert.AreEqual(new[] { "owned" }, owned);
        }

        [Test]
        public void OwnershipComparisonIsOrdinalRegardlessOfCollectionComparer()
        {
            var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ITEM" };
            CollectionAssert.AreEqual(new[] { "item" },
                EraUnlocks.GrantsFor(new[] { Variant("item", 0) }, 0, owned));
        }

        [Test]
        public void ApplyingGrantsMakesSubsequentGrantCalculationEmpty()
        {
            var catalog = new[] { Variant("base", 0), Variant("advanced", 1) };
            var owned = new List<string>();
            owned.AddRange(EraUnlocks.GrantsFor(catalog, 1, owned));
            Assert.IsEmpty(EraUnlocks.GrantsFor(catalog, 1, owned));
            Assert.IsEmpty(EraUnlocks.GrantsFor(Array.Empty<CatalogItem>(), 1, owned));
        }

        [Test]
        public void DemotionKeepsGrantsButDisablesHigherEraUntilRepromotion()
        {
            var advanced = Variant("advanced", 1);
            var demoted = Arenas.ApplyRRDelta(new RankState(300, 1, 1), -1, new ArenaConfig());
            CollectionAssert.AreEqual(new[] { "advanced" },
                EraUnlocks.GrantsFor(new[] { advanced }, demoted.HighestArena, new List<string>()));
            Assert.IsFalse(EraUnlocks.IsUsable(advanced, demoted.Arena));
            var promoted = Arenas.ApplyRRDelta(demoted, 1, new ArenaConfig());
            Assert.IsTrue(EraUnlocks.IsUsable(advanced, promoted.Arena));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void UsabilityDependsOnlyOnCurrentArenaEvenForRetiredVariants(int arena, bool expected)
        {
            Assert.AreEqual(expected, EraUnlocks.IsUsable(Variant("variant", 1, true), arena));
        }

        [Test]
        public void SkinsCannotBePassedToVariantUsability()
        {
            Assert.Throws<ArgumentException>(() => EraUnlocks.IsUsable(
                new CatalogItem { Kind = CatalogItemKind.Skin }, 0));
        }

        [Test]
        public void CanonicalRoster_HasSixEras_NoRetiredGrants_AndKeepsLegacyOwnershipOnDemotion()
        {
            var catalog = new List<CatalogItem>();
            foreach (string key in new[] { "farm", "mine", "village", "barracks", "forge", "market",
                "pier", "town", "infirmary", "fortress", "camp", "shrine", "arsenal", "sanctuary", "watchtower", "rampart" })
                for (int era = 0; era < 6; era++)
                    catalog.Add(new CatalogItem { Id = "district." + key + ".e" + era,
                        BaseId = "district." + key, Era = era, Kind = CatalogItemKind.Variant,
                        Retired = key == "camp" || key == "shrine" || key == "arsenal" ||
                            key == "sanctuary" || key == "watchtower" || key == "rampart" });
            var owned = new List<string> { "district.camp.e5", "district.rampart.e3" };
            var grants = EraUnlocks.GrantsFor(catalog, 5, owned);
            Assert.AreEqual(60, grants.Count);
            Assert.IsFalse(grants.Contains("district.camp.e0"));
            owned.AddRange(grants);
            CollectionAssert.Contains(owned, "district.camp.e5");
            CollectionAssert.Contains(owned, "district.rampart.e3");
            Assert.IsEmpty(EraUnlocks.GrantsFor(catalog, 5, owned));
            foreach (var item in catalog)
                if (!item.Retired && item.Era > 2) Assert.IsFalse(EraUnlocks.IsUsable(item, 2));
        }

        private static CatalogItem Variant(string id, int era, bool retired = false)
        {
            return new CatalogItem { Id = id, Kind = CatalogItemKind.Variant,
                BaseId = "suit." + id, Era = era, Retired = retired };
        }
    }
}
