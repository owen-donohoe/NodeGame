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

        private static CatalogItem Variant(string id, int era, bool retired = false)
        {
            return new CatalogItem { Id = id, Kind = CatalogItemKind.Variant,
                BaseId = "suit." + id, Era = era, Retired = retired };
        }
    }
}
