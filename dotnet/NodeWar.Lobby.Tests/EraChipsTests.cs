using System.Collections.Generic;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class EraChipsTests
    {
        private const string Base = "suit.warrior";

        private static PlayerState State()
        {
            return new PlayerState
            {
                Rank = new RankRecord { Arena = 2, HighestArena = 5 },
                Inventory = new InventoryRecord
                {
                    OwnedVariants = new List<string>
                    {
                        CatalogIds.Variant(Base, 0), CatalogIds.Variant(Base, 2), CatalogIds.Variant(Base, 4)
                    },
                    Equipped = new EquippedRecord
                    {
                        Variants = new Dictionary<string, string> { [Base] = CatalogIds.Variant(Base, 0) }
                    }
                }
            };
        }

        [Test]
        public void SixErasUseCanonicalIdsInOrder()
        {
            var chips = EraChips.ForItem(State(), Base);
            Assert.AreEqual(6, chips.Length);
            for (int era = 0; era < chips.Length; era++)
            {
                Assert.AreEqual(era, chips[era].Era);
                Assert.AreEqual(CatalogIds.Variant(Base, era), chips[era].ID);
            }
        }

        [Test]
        public void EquippedIsHighlightedButNotSelectable()
        {
            var chip = EraChips.ForItem(State(), Base)[0];
            Assert.IsTrue(chip.Equipped);
            Assert.IsTrue(chip.Owned);
            Assert.IsTrue(chip.Usable);
            Assert.IsFalse(chip.Selectable);
        }

        [Test]
        public void OwnedAtCurrentArenaIsSelectable()
        {
            var chip = EraChips.ForItem(State(), Base)[2];
            Assert.IsTrue(chip.Owned);
            Assert.IsTrue(chip.Usable);
            Assert.IsFalse(chip.Equipped);
            Assert.IsTrue(chip.Selectable);
        }

        [Test]
        public void DemotionPreservesOwnershipButHighestArenaDoesNotAllowEquip()
        {
            var chip = EraChips.ForItem(State(), Base)[4];
            Assert.IsTrue(chip.Owned);
            Assert.IsFalse(chip.Usable);
            Assert.IsFalse(chip.Selectable);
        }

        [Test]
        public void EquippedAboveCurrentArenaRemainsHighlightedAndDisabled()
        {
            var state = State();
            state.Inventory.Equipped.Variants[Base] = CatalogIds.Variant(Base, 4);
            var chip = EraChips.ForItem(state, Base)[4];
            Assert.IsTrue(chip.Equipped);
            Assert.IsFalse(chip.Usable);
            Assert.IsFalse(chip.Selectable);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void UnownedIsLockedRegardlessOfArena(int era)
        {
            var chip = EraChips.ForItem(State(), Base)[era];
            Assert.IsFalse(chip.Owned);
            Assert.IsFalse(chip.Equipped);
            Assert.IsFalse(chip.Selectable);
        }

        [TestCase("state")]
        [TestCase("inventory")]
        [TestCase("owned")]
        public void MissingOwnershipIsNullSafeAndLocked(string missing)
        {
            var state = State();
            if (missing == "state") state = null;
            else if (missing == "inventory") state.Inventory = null;
            else state.Inventory.OwnedVariants = null;
            var chips = EraChips.ForItem(state, Base);
            Assert.AreEqual(6, chips.Length);
            Assert.That(chips, Has.All.Property("Owned").False);
            Assert.That(chips, Has.All.Property("Selectable").False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingEquippedDoesNotInventDefault(bool missingRecord)
        {
            var state = State();
            if (missingRecord) state.Inventory.Equipped = null;
            else state.Inventory.Equipped.Variants = null;
            var chips = EraChips.ForItem(state, Base);
            Assert.That(chips, Has.All.Property("Equipped").False);
            Assert.IsTrue(chips[0].Selectable);
        }

        [Test]
        public void MissingRankDoesNotAllowEquip()
        {
            var state = State();
            state.Rank = null;
            Assert.That(EraChips.ForItem(state, Base), Has.All.Property("Usable").False);
        }

        [TestCase(null)]
        [TestCase("")]
        public void ItemWithoutBaseHasNoChips(string baseId)
        {
            Assert.IsEmpty(EraChips.ForItem(State(), baseId));
            Assert.IsEmpty(EraChips.SkinsForItem(State(), baseId));
        }

        [Test]
        public void OtherItemsOwnershipAndEquippedEntriesDoNotLeak()
        {
            Assert.That(EraChips.ForItem(State(), "district.rampart"), Has.All.Property("Owned").False);
            Assert.That(EraChips.ForItem(State(), "district.rampart"), Has.All.Property("Equipped").False);
        }

        [Test]
        public void DefaultSkinAloneIsHiddenEvenWithDuplicatesOrOtherItems()
        {
            var state = State();
            state.Inventory.OwnedSkins = new List<string>
            {
                CatalogIds.DefaultSkin(Base), CatalogIds.DefaultSkin(Base), "skin.suit.archer.gold", "bad"
            };
            Assert.IsEmpty(EraChips.SkinsForItem(state, Base));
        }

        [Test]
        public void MultipleOwnedSkinsAreSortedAndEquippedFromServerMap()
        {
            var state = State();
            state.Rank.Arena = 0;
            state.Inventory.OwnedSkins = new List<string> { "skin.suit.warrior.gold", CatalogIds.DefaultSkin(Base) };
            state.Inventory.Equipped.Skins = new Dictionary<string, string> { [Base] = "skin.suit.warrior.gold" };
            var chips = EraChips.SkinsForItem(state, Base);
            Assert.AreEqual(2, chips.Length);
            Assert.AreEqual(CatalogIds.DefaultSkin(Base), chips[0].ID);
            Assert.IsTrue(chips[0].Selectable);
            Assert.IsTrue(chips[1].Equipped);
            Assert.IsFalse(chips[1].Selectable);
        }

        [Test]
        public void SkinsAreNullSafe()
        {
            Assert.IsEmpty(EraChips.SkinsForItem(null, Base));
            Assert.IsEmpty(EraChips.SkinsForItem(new PlayerState(), Base));
            Assert.IsEmpty(EraChips.SkinsForItem(State(), Base));
            var state = State();
            state.Inventory.Equipped = null;
            state.Inventory.OwnedSkins = new List<string> { "skin.suit.warrior.gold", CatalogIds.DefaultSkin(Base) };
            Assert.That(EraChips.SkinsForItem(state, Base), Has.All.Property("Selectable").True);
        }
    }
}
