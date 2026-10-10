using System;
using System.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutChoicesTests
    {
        [Test]
        public void Tree_OnlyDefinedNodes()
        {
            var choices = LoadoutChoices.Variants("suit.scout", null);
            CollectionAssert.AreEqual(new[] { "suit.scout.e0", "suit.scout.e1", "suit.scout.e2" }, choices.Select(c => c.ID));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, choices.Select(c => c.OrderKey));
            Assert.AreEqual("Variant 2", choices[2].Label);
            Assert.AreEqual(1, choices[2].UnlockState.UnlockEra);
        }

        [TestCase("district.farm")]
        [TestCase("suit.villager")]
        [TestCase("suit.minion")]
        [TestCase(null)]
        public void NonTreeItems_HaveNoVariants(string baseId) => Assert.IsEmpty(LoadoutChoices.Variants(baseId, UnlockStateTests.State()));

        [Test]
        public void OwnedFirst_StableIdTieBreak()
        {
            var state = UnlockStateTests.State();
            state.Inventory.OwnedVariants.Add("suit.scout.e2");
            Assert.AreEqual("suit.scout.e2", LoadoutChoices.Variants("suit.scout", state)[0].ID);
            var unlock = new UnlockState(UnlockStatus.Owned, 0, true);
            var choices = new[] { new LoadoutChoice("z", "base", "same", 0, unlock, false), new LoadoutChoice("a", "base", "same", 0, unlock, false) };
            Array.Sort(choices, LoadoutChoices.Compare);
            Assert.AreEqual("a", choices[0].ID);
        }

        [Test]
        public void SkinChoices_IncludeSingleDefaultAndOwnedUnknownToLocalList()
        {
            var state = UnlockStateTests.State();
            state.Inventory.OwnedSkins.Add("skin.suit.scout.gold");
            var choices = LoadoutChoices.Skins("suit.scout", null, state);
            CollectionAssert.AreEquivalent(new[] { "skin.suit.scout.default", "skin.suit.scout.gold" }, choices.Select(c => c.ID));
            Assert.AreEqual("skin.suit.scout.gold", choices[0].ID);
            Assert.AreEqual(1, LoadoutChoices.Skins("suit.scout", null, null).Length);
            Assert.IsEmpty(LoadoutChoices.Skins(null, null, state));
        }

        [Test]
        public void WrongBaseAndDuplicateSkins_AreExcluded()
        {
            var choices = LoadoutChoices.Skins("suit.scout", new[] { "bad", "skin.suit.medic.gold", "skin.suit.scout.default", "skin.suit.scout.gold", "skin.suit.scout.gold" }, null);
            Assert.AreEqual(2, choices.Length);
            Assert.That(choices.All(c => c.BaseID == "suit.scout"));
        }

        [Test]
        public void PreviewOnly_NeverCanUse()
        {
            var choice = new LoadoutChoice("skin.suit.scout.fake", "suit.scout", "Preview", 0, new UnlockState(UnlockStatus.Owned, 0, true), false, true);
            Assert.IsTrue(choice.PreviewOnly);
            Assert.IsFalse(choice.UnlockState.CanUse);
            Assert.IsFalse(choice.Selectable);
        }

        [Test]
        public void DisplayOverride_DoesNotGrantUse()
        {
            var locked = new UnlockState(UnlockStatus.CannotUnlock, 2, false);
            Assert.IsFalse(locked.WithDisplayStatus(UnlockStatus.Owned).CanUse);
        }

        [Test]
        public void EquippedChoice_IsNotSelectable()
        {
            var state = UnlockStateTests.State();
            state.Inventory.OwnedVariants.Add("suit.scout.e0");
            state.Inventory.Equipped = new EquippedRecord { Variants = new System.Collections.Generic.Dictionary<string, string> { ["suit.scout"] = "suit.scout.e0" } };
            Assert.IsTrue(LoadoutChoices.Variants("suit.scout", state)[0].Equipped);
            Assert.IsFalse(LoadoutChoices.Variants("suit.scout", state)[0].Selectable);
        }
    }
}
