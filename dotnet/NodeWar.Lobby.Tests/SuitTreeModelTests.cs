using System.Collections.Generic;
using System.Linq;
using NodeWar.Backend;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    [TestFixture]
    public class SuitTreeModelTests
    {
        private const string Warrior = "suit.warrior";

        private static PlayerState State(int arena, string equipped, params string[] owned)
        {
            var state = new PlayerState
            {
                Rank = new RankRecord { Arena = arena, HighestArena = arena },
                Inventory = PlayerStateDefaults.Inventory()
            };
            state.Inventory.OwnedVariants.AddRange(owned);
            if (equipped != null) state.Inventory.Equipped.Variants[Warrior] = equipped;
            return state;
        }

        private static GameBalanceData Balance(params SuitStats[] stats) =>
            new GameBalanceData { suitStats = stats };

        [Test]
        public void NewPlayerSeesRootsAvailableAndVariantsLocked()
        {
            var nodes = SuitTreeModel.Nodes(null, Warrior);
            Assert.That(nodes.Select(n => n.Variant), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(nodes[0].State, Is.EqualTo(SuitNodeState.Available));
            Assert.That(nodes[1].State, Is.EqualTo(SuitNodeState.Locked));
            Assert.That(nodes[2].State, Is.EqualTo(SuitNodeState.Locked));
        }

        [Test]
        public void EachStateIsReported()
        {
            var nodes = SuitTreeModel.Nodes(State(1, "suit.warrior.e0", "suit.warrior.e0", "suit.warrior.e1"), Warrior);
            Assert.That(nodes[0].State, Is.EqualTo(SuitNodeState.Equipped));
            Assert.That(nodes[1].State, Is.EqualTo(SuitNodeState.Owned));
            Assert.That(nodes[2].State, Is.EqualTo(SuitNodeState.Available), "arena 1 reached, parent owned, not owned");
        }

        [Test]
        public void VariantStaysLockedWithoutItsParentOrItsArena()
        {
            Assert.That(SuitTreeModel.Nodes(State(1, null), Warrior)[1].State, Is.EqualTo(SuitNodeState.Locked), "parent not owned");
            Assert.That(SuitTreeModel.Nodes(State(0, "suit.warrior.e0", "suit.warrior.e0"), Warrior)[1].State,
                Is.EqualTo(SuitNodeState.Locked), "arena not reached");
        }

        [Test]
        public void OwnedVariantAboveTheCurrentArenaIsLockedButStillOwned()
        {
            var node = SuitTreeModel.Nodes(State(0, "suit.warrior.e0", "suit.warrior.e0", "suit.warrior.e2"), Warrior)[2];
            Assert.That(node.State, Is.EqualTo(SuitNodeState.Locked));
            Assert.That(node.Owned, Is.True);
            Assert.That(node.CanEquip, Is.False);
        }

        [Test]
        public void EquippedPreTreeEraIsNeverShownAsEquipped()
        {
            var nodes = SuitTreeModel.Nodes(State(5, "suit.warrior.e4", "suit.warrior.e0", "suit.warrior.e4"), Warrior);
            Assert.That(nodes.Any(n => n.State == SuitNodeState.Equipped), Is.False);
        }

        [Test]
        public void PrimaryButtonIsEquipWhenOwnedElseADisabledState()
        {
            var nodes = SuitTreeModel.Nodes(State(1, "suit.warrior.e0", "suit.warrior.e0", "suit.warrior.e1", "suit.warrior.e2"), Warrior);
            var equipped = SuitTreeModel.Button(nodes[0]);
            Assert.That((equipped.Action, equipped.Label, equipped.Enabled), Is.EqualTo((SuitNodeAction.None, "Equipped", false)));
            var equip = SuitTreeModel.Button(nodes[1]);
            Assert.That((equip.Action, equip.Label, equip.Enabled), Is.EqualTo((SuitNodeAction.Equip, "Equip", true)));

            var unowned = SuitTreeModel.Nodes(State(1, "suit.warrior.e0", "suit.warrior.e0"), Warrior)[1];
            var unlock = SuitTreeModel.Button(unowned);
            Assert.That(unlock.Action, Is.EqualTo(SuitNodeAction.Unlock));
            Assert.That(unlock.Enabled, Is.False, "no currency exists yet");

            var demoted = SuitTreeModel.Nodes(State(0, "suit.warrior.e0", "suit.warrior.e0", "suit.warrior.e1"), Warrior)[1];
            Assert.That(SuitTreeModel.Button(demoted).Label, Is.EqualTo("Needs arena 1"));
            Assert.That(SuitTreeModel.Button(demoted).Enabled, Is.False);
        }

        [Test]
        public void HowToGetSaysArenaParentOrCost()
        {
            var fresh = SuitTreeModel.Nodes(State(0, null), Warrior);
            Assert.That(SuitTreeModel.HowToGet(fresh[0], fresh), Is.EqualTo("Unlock: cost not set yet"));
            Assert.That(SuitTreeModel.HowToGet(fresh[1], fresh), Is.EqualTo("Reach arena 1"));
            var noParent = SuitTreeModel.Nodes(State(1, null), Warrior);
            Assert.That(SuitTreeModel.HowToGet(noParent[1], noParent), Is.EqualTo("Needs Warrior first"));
            var owned = SuitTreeModel.Nodes(State(1, "suit.warrior.e0", "suit.warrior.e0"), Warrior);
            Assert.That(SuitTreeModel.HowToGet(owned[0], owned), Is.EqualTo("Owned and equipped"));
        }

        [Test]
        public void LayoutHasOneBandPerArenaAndSiblingsSideBySide()
        {
            var layout = SuitTreeModel.Layout(Warrior);
            Assert.That(layout.Select(l => l.Band), Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(layout[0].X, Is.EqualTo(0.5f));
            Assert.That(layout[1].X, Is.LessThan(layout[2].X));
            Assert.That(layout[1].ParentVariant, Is.EqualTo(0));
            Assert.That(SuitTreeModel.BandArenas(Warrior), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void ArenaTabsAndStripFollowTheDebutArenas()
        {
            Assert.That(SuitTreeModel.DebutArenas(), Is.EqualTo(new[] { 0 }));
            Assert.That(SuitTreeModel.SuitsDebutingAt(0), Is.EquivalentTo(SuitTreeModel.TreeSuits()));
            Assert.That(SuitTreeModel.SuitsDebutingAt(3), Is.Empty);
            Assert.That(SuitTreeModel.TreeSuits(), Has.Count.EqualTo(5));
        }

        [Test]
        public void CopiedRowsShowNoChangeAndASayNothingRoleLine()
        {
            var copy = new SuitStats { suitType = SuitType.Warrior, era = 0, bonusHP = 2, attackDamage = 1, moveSpeedTicks = 4 };
            var rows = SuitTreeModel.StatRows(Balance(copy), Warrior, 1, 0);
            Assert.That(rows, Has.Length.EqualTo(7));
            Assert.That(rows.All(r => r.Trend == StatTrend.Same && r.DeltaText == "same"), Is.True);
            Assert.That(SuitTreeModel.RoleLine(rows, false, "Warrior", null), Is.EqualTo("Plays like Warrior for now."));
        }

        [Test]
        public void DeltasAreAgainstTheParentWithLowerBetterForTimes()
        {
            var root = new SuitStats { suitType = SuitType.Warrior, era = 0, bonusHP = 2, moveSpeedTicks = 4, attackCooldownMax = 20 };
            var vanguard = new SuitStats { suitType = SuitType.Warrior, era = 1, bonusHP = 4, moveSpeedTicks = 5, attackCooldownMax = 20 };
            var rows = SuitTreeModel.StatRows(Balance(root, vanguard), Warrior, 1, 0);
            var hp = rows.Single(r => r.Label == "Bonus HP");
            Assert.That((hp.Value, hp.Delta, hp.Trend, hp.DeltaText), Is.EqualTo((4, 2, StatTrend.Better, "+2")));
            var move = rows.Single(r => r.Label == "Move time");
            Assert.That((move.Delta, move.Trend, move.DeltaText), Is.EqualTo((1, StatTrend.Worse, "+1")));
            Assert.That(rows.Single(r => r.Label == "Attack cooldown").Trend, Is.EqualTo(StatTrend.Same));
            Assert.That(SuitTreeModel.RoleLine(rows, false, "Warrior", null), Is.EqualTo("Tougher, but slower."));
        }

        [Test]
        public void RootShowsValuesWithoutDeltasAndUsesTheDescription()
        {
            var root = new SuitStats { suitType = SuitType.Warrior, era = 0, bonusHP = 2 };
            var rows = SuitTreeModel.StatRows(Balance(root), Warrior, 0, -1);
            Assert.That(rows[0].Value, Is.EqualTo(2));
            Assert.That(rows.All(r => !r.HasParent && r.DeltaText == ""), Is.True);
            Assert.That(SuitTreeModel.RoleLine(rows, true, "Warrior", "Frontline fighter"), Is.EqualTo("Frontline fighter"));
            Assert.That(SuitTreeModel.RoleLine(rows, true, "Warrior", ""), Is.EqualTo("The standard Warrior."));
        }

        [Test]
        public void MissingVariantRowFallsBackToEraZeroLikeTheBalanceLookup()
        {
            var root = new SuitStats { suitType = SuitType.Warrior, era = 0, bonusHP = 3 };
            var rows = SuitTreeModel.StatRows(Balance(root), Warrior, 2, 0);
            Assert.That(rows.All(r => r.Delta == 0), Is.True);
            Assert.That(SuitTreeModel.StatRows(new GameBalanceData(), Warrior, 1, 0).All(r => r.Value == 0), Is.True);
        }

        [Test]
        // Every combat suit has exactly one equip district.
        public void SuitTree_ListsOnlyBarracksForCombatSuits()
        {
            foreach (string suit in new[] { "warrior", "guardian", "scout", "berserker", "medic" })
                Assert.That(SuitTreeModel.EquipDistricts(new GameBalanceData(), "suit." + suit), Is.EqualTo(new[] { DistrictType.Barracks }));
            var balance = new GameBalanceData();
            Assert.That(SuitTreeModel.EquipDistricts(balance, Warrior),
                Is.EquivalentTo(new[] { DistrictType.Barracks }));
            Assert.That(SuitTreeModel.EquipDistricts(balance, "suit.medic"), Is.EqualTo(new[] { DistrictType.Barracks }));
            Assert.That(SuitTreeModel.EquipDistricts(balance, "suit.berserker"), Is.EqualTo(new[] { DistrictType.Barracks }));
            Assert.That(SuitTreeModel.EquipDistricts(balance, "district.farm"), Is.Empty);
        }

        [Test]
        public void EveryTreeSuitMapsToACombatSuitType()
        {
            foreach (string baseId in SuitTreeModel.TreeSuits())
            {
                Assert.That(SuitTreeModel.TrySuitType(baseId, out SuitType suit), Is.True, baseId);
                Assert.That(GameBalanceData.IsCombatSuit(suit), Is.True, baseId);
            }
        }

        [Test]
        public void NamesAreTheSuitThenLetteredVariants()
        {
            Assert.That(SuitTreeModel.NodeName(Warrior, 0), Is.EqualTo("Warrior"));
            Assert.That(SuitTreeModel.NodeName(Warrior, 1), Is.EqualTo("Warrior A"));
            Assert.That(SuitTreeModel.NodeName(Warrior, 2), Is.EqualTo("Warrior B"));
        }

        [Test]
        public void EraChipsFollowTheTreeForSuitsSoTheyAgreeWithTheServer()
        {
            var state = State(1, "suit.warrior.e0", "suit.warrior.e0", "suit.warrior.e1", "suit.warrior.e2", "suit.warrior.e4");
            var chips = EraChips.ForItem(state, Warrior);
            Assert.That(chips[1].Usable, Is.True);
            Assert.That(chips[2].Usable, Is.True, "both siblings open at arena 1");
            Assert.That(chips[4].Usable, Is.False, "outside the table");
            Assert.That(EraChips.ForItem(state, "district.farm")[2].Usable, Is.False, "districts keep the era rule");
        }
    }
}
