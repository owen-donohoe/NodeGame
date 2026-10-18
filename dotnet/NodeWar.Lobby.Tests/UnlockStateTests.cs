using System.Collections.Generic;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class UnlockStateTests
    {
        internal static PlayerState State() => new PlayerState
        {
            Rank = new RankRecord { Arena = 1, HighestArena = 1 },
            Inventory = new InventoryRecord { OwnedVariants = new List<string>(), OwnedSkins = new List<string>() }
        };

        [Test]
        public void TreeVariant_RequiresParentAndHighestArena()
        {
            var state = State();
            Assert.AreEqual(UnlockStatus.CannotUnlock, UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state).Status);
            state.Inventory.OwnedVariants.Add("suit.scout.e0");
            state.Rank.HighestArena = 0;
            Assert.AreEqual(UnlockStatus.CannotUnlock, UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state).Status);
            state.Rank.HighestArena = 1;
            var result = UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state);
            Assert.AreEqual(UnlockStatus.CanUnlock, result.Status);
            Assert.AreEqual(1, result.UnlockEra);
            Assert.IsFalse(result.CanUse);
        }

        [Test]
        public void OwnedButDemoted_CannotUse()
        {
            var state = State();
            state.Inventory.OwnedVariants.Add("suit.scout.e2");
            state.Rank.Arena = 0;
            var result = UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state);
            Assert.AreEqual(UnlockStatus.Owned, result.Status);
            Assert.AreEqual(1, result.UnlockEra);
            Assert.IsFalse(result.CanUse);
        }

        [Test]
        public void UnownedBase_DoesNotGrantServerVariant()
        {
            Assert.IsFalse(UnlockStates.ForBase("suit_scout", State()).CanUse);
            Assert.IsFalse(UnlockStates.ForVariant("suit.scout", "suit.scout.e0", State()).CanUse);
        }

        [TestCase("suit_scout", "suit.scout")]
        [TestCase("node_village", "district.village")]
        public void OwnedBase_CanUseBelowDebut(string lobbyId, string baseId)
        {
            var state = State();
            state.Rank.Arena = 0;
            state.Rank.HighestArena = 0;
            state.Inventory.OwnedVariants.Add(CatalogIds.Variant(baseId, 0));
            var result = UnlockStates.ForBase(lobbyId, state);
            Assert.AreEqual(UnlockStatus.Owned, result.Status);
            Assert.AreEqual(ContentTable.DebutEra(baseId), result.UnlockEra);
            Assert.IsTrue(result.CanUse);
        }

        [TestCase(1, UnlockStatus.CanUnlock)]
        [TestCase(0, UnlockStatus.CannotUnlock)]
        public void UnownedBase_UsesDebutAndHighestArena(int highestArena, UnlockStatus status)
        {
            var state = State();
            state.Rank.HighestArena = highestArena;
            state.Rank.Arena = 0;
            var result = UnlockStates.ForBase("node_village", state);
            Assert.AreEqual(status, result.Status);
            Assert.AreEqual(ContentTable.DebutEra("district.village"), result.UnlockEra);
            Assert.IsFalse(result.CanUse);
        }

        [TestCase("suit_warrior")]
        [TestCase("suit_guardian")]
        [TestCase("node_farm")]
        [TestCase("node_mine")]
        [TestCase("node_town")]
        [TestCase("node_barracks")]
        public void NullState_StarterBaseIsOwned(string lobbyId)
        {
            var result = UnlockStates.ForBase(lobbyId, null);
            Assert.AreEqual(UnlockStatus.Owned, result.Status);
            Assert.IsTrue(result.CanUse);
        }

        [Test]
        public void NullState_VillageIsNotOwned()
        {
            var result = UnlockStates.ForBase("node_village", null);
            Assert.AreEqual(UnlockStatus.CanUnlock, result.Status);
            Assert.IsFalse(result.CanUse);
        }

        [Test]
        public void UnknownLobbyId_IsInvalid()
        {
            var result = UnlockStates.ForBase("bad", State());
            Assert.AreEqual(UnlockStatus.CannotUnlock, result.Status);
            Assert.AreEqual(0, result.UnlockEra);
            Assert.IsFalse(result.CanUse);
        }

        [Test]
        public void SkinOwned_IgnoresArena()
        {
            var state = State();
            state.Rank = null;
            state.Inventory.OwnedSkins.Add("skin.suit.scout.gold");
            Assert.IsTrue(UnlockStates.ForSkin("suit.scout", "skin.suit.scout.gold", state).CanUse);
        }

        [Test]
        public void MissingInventory_CannotEquip()
        {
            foreach (var state in new[] { null, new PlayerState(), new PlayerState { Inventory = new InventoryRecord() } })
            {
                Assert.IsFalse(UnlockStates.ForVariant("suit.scout", "suit.scout.e0", state).CanUse);
                Assert.IsFalse(UnlockStates.ForSkin("suit.scout", "skin.suit.scout.default", state).CanUse);
            }
        }

        [Test]
        public void InvalidAndWrongBaseIds_CannotEquip()
        {
            var state = State();
            state.Inventory.OwnedVariants.Add("suit.scout.e5");
            state.Inventory.OwnedSkins.Add("skin.suit.scout.gold");
            Assert.IsFalse(UnlockStates.ForVariant("suit.scout", "suit.scout.e5", state).CanUse);
            Assert.IsFalse(UnlockStates.ForSkin("suit.medic", "skin.suit.scout.gold", state).CanUse);
            Assert.IsFalse(UnlockStates.ForBase("bad", state).CanUse);
        }

        [Test]
        public void NonTreeVariant_UsesCatalogIndexAndCurrentArena()
        {
            var state = State();
            state.Inventory.OwnedVariants.Add("district.farm.e2");
            Assert.IsFalse(UnlockStates.ForVariant("district.farm", "district.farm.e2", state).CanUse);
            state.Rank.Arena = 2;
            Assert.IsTrue(UnlockStates.ForVariant("district.farm", "district.farm.e2", state).CanUse);
        }

        [Test]
        public void MissingRank_FallsBackToOneWithoutGrantingOwnership()
        {
            var state = State();
            state.Rank = null;
            state.Inventory.OwnedVariants.Add("suit.scout.e0");
            Assert.AreEqual(UnlockStatus.CanUnlock, UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state).Status);
            Assert.IsFalse(UnlockStates.ForVariant("suit.scout", "suit.scout.e2", state).CanUse);
        }
    }
}
