using System;
using System.Linq;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class CatalogTests
    {
        [Test]
        public void ValidCatalogAllowsSkinsAndRetiredVariantsToShareABase()
        {
            Assert.IsEmpty(CatalogValidation.Validate(new[]
            {
                Item("variant.0", 0), Item("variant.5", 5), Item("old", 0, true),
                Skin("skin.one"), Skin("skin.two")
            }, 6));
            Assert.IsEmpty(CatalogValidation.Validate(Array.Empty<CatalogItem>(), 6));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Upper")]
        [TestCase("a-b")]
        [TestCase("a..b")]
        [TestCase(".a")]
        [TestCase("a.")]
        [TestCase("a\n")]
        [TestCase("é")]
        public void RejectsInvalidIdFormats(string id)
        {
            StringAssert.Contains("Invalid item ID", CatalogValidation.Validate(new[] { Item(id) }, 6).Single());
        }

        [TestCase("a")]
        [TestCase("suit.warrior_2.era0")]
        [TestCase("0._")]
        public void AcceptsValidIdFormats(string id)
        {
            Assert.IsEmpty(CatalogValidation.Validate(new[] { Item(id) }, 6));
        }

        [Test]
        public void DuplicateIdsAreRejectedEvenWhenRetired()
        {
            StringAssert.Contains("Duplicate item ID", CatalogValidation.Validate(
                new[] { Item("same"), Item("same", 1, true) }, 6).Single());
        }

        [Test]
        public void ActiveVariantBaseAndEraMustBeUnique()
        {
            StringAssert.Contains("Duplicate active variant", CatalogValidation.Validate(
                new[] { Item("one"), Item("two") }, 6).Single());
            var otherBase = Item("two");
            otherBase.BaseId = "suit.archer";
            Assert.IsEmpty(CatalogValidation.Validate(new[] { Item("one"), otherBase }, 6));
        }

        [TestCase(-1)]
        [TestCase(6)]
        public void VariantEraMustBeWithinArenaRangeEvenWhenRetired(int era)
        {
            StringAssert.Contains("out-of-range era", CatalogValidation.Validate(
                new[] { Item("item", era, true) }, 6).Single());
        }

        [TestCase(-2)]
        [TestCase(0)]
        [TestCase(5)]
        public void SkinEraMustBeMinusOne(int era)
        {
            var skin = Skin("skin");
            skin.Era = era;
            StringAssert.Contains("must have era -1", CatalogValidation.Validate(new[] { skin }, 6).Single());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void BaseIdMustBeNonEmpty(string baseId)
        {
            var item = Item("item");
            item.BaseId = baseId;
            StringAssert.Contains("BaseId", CatalogValidation.Validate(new[] { item }, 6).Single());
        }

        [Test]
        public void InvalidKindsAndNullItemsProduceErrors()
        {
            var item = Item("item");
            item.Kind = (CatalogItemKind)99;
            Assert.AreEqual(2, CatalogValidation.Validate(new[] { item, null }, 6).Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => CatalogValidation.Validate(new[] { item }, 0));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void IdentityStabilityAllowsRetirementUnretirementAndAdditions(bool before, bool after)
        {
            Assert.IsEmpty(CatalogValidation.ValidateAgainstPrevious(
                new[] { Item("item", 0, before) }, new[] { Item("item", 0, after), Item("new", 1) }));
        }

        [Test]
        public void RemovalAndRenameAreRejectedEvenForRetiredItems()
        {
            var previous = new[] { Item("item", 0, true) };
            StringAssert.Contains("removed or renamed", CatalogValidation.ValidateAgainstPrevious(
                previous, Array.Empty<CatalogItem>()).Single());
            StringAssert.Contains("removed or renamed", CatalogValidation.ValidateAgainstPrevious(
                previous, new[] { Item("renamed", 0, true) }).Single());
        }

        [TestCase("kind")]
        [TestCase("base")]
        [TestCase("era")]
        public void ReusingIdForDifferentIdentityIsRejected(string field)
        {
            var replacement = Item("item");
            if (field == "kind") replacement.Kind = CatalogItemKind.Skin;
            if (field == "base") replacement.BaseId = "suit.Warrior";
            if (field == "era") replacement.Era = 1;
            StringAssert.Contains("must not be reused", CatalogValidation.ValidateAgainstPrevious(
                new[] { Item("item", 0, true) }, new[] { replacement }).Single());
        }

        [Test]
        public void DuplicateIdsCannotHideIdentityChanges()
        {
            Assert.IsNotEmpty(CatalogValidation.ValidateAgainstPrevious(new[] { Item("item") },
                new[] { Item("item"), Item("item", 1) }));
            Assert.IsNotEmpty(CatalogValidation.ValidateAgainstPrevious(
                new[] { Item("item"), Item("item", 1) }, new[] { Item("item") }));
        }

        private static CatalogItem Item(string id, int era = 0, bool retired = false)
        {
            return new CatalogItem { Id = id, Kind = CatalogItemKind.Variant,
                BaseId = "suit.warrior", Era = era, Retired = retired };
        }

        private static CatalogItem Skin(string id)
        {
            return new CatalogItem { Id = id, Kind = CatalogItemKind.Skin, BaseId = "suit.warrior", Era = -1 };
        }
    }
}
