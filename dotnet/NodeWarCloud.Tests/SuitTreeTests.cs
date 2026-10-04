using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class SuitTreeTests
    {
        private static readonly string[] CombatSuits = { "Warrior", "Guardian", "Scout", "Berserker", "Medic" };

        [Test]
        public void EveryCombatSuitIsARootPlusTwoSiblingsOneArenaLater()
        {
            foreach (string name in CombatSuits)
            {
                string baseId = CatalogIds.SuitBase(name);
                var nodes = SuitTree.Nodes.Where(n => n.BaseId == baseId).OrderBy(n => n.Variant).ToList();
                Assert.That(nodes.Select(n => n.Variant), Is.EqualTo(new[] { 0, 1, 2 }), baseId);
                Assert.That(nodes[0].ParentVariant, Is.EqualTo(-1), baseId);
                foreach (var sibling in nodes.Skip(1))
                {
                    Assert.That(sibling.ParentVariant, Is.EqualTo(0), baseId);
                    Assert.That(sibling.Arena, Is.EqualTo(nodes[0].Arena + 1), baseId);
                }
                Assert.That(nodes.All(n => n.Variant < GameBalanceData.EraCount && n.Variant < CatalogIds.EraCount));
            }
            Assert.That(SuitTree.Nodes, Has.Count.EqualTo(CombatSuits.Length * 3));
        }

        [Test]
        public void DebutArenasAreAsSpecifiedAndEveryNodeIdParses()
        {
            int[] debut = { 0, 0, 0, 1, 1 };
            for (int i = 0; i < CombatSuits.Length; i++)
                Assert.That(SuitTree.Root(CatalogIds.SuitBase(CombatSuits[i])).Arena, Is.EqualTo(debut[i]), CombatSuits[i]);
            foreach (var node in SuitTree.Nodes)
            {
                Assert.That(CatalogIds.TryParseVariant(node.Id, out string b, out int v), Is.True, node.Id);
                Assert.That((b, v), Is.EqualTo((node.BaseId, node.Variant)));
                Assert.That(node.Unlock, Is.EqualTo(SuitUnlockKind.Cost));
            }
        }

        [Test]
        public void AvailabilityFollowsTheNodeArenaAndOnlyTreeNodesAreAvailable()
        {
            Assert.That(SuitTree.IsAvailable("suit.warrior.e0", 0), Is.True);
            Assert.That(SuitTree.IsAvailable("suit.warrior.e1", 0), Is.False);
            Assert.That(SuitTree.IsAvailable("suit.warrior.e1", 1), Is.True);
            Assert.That(SuitTree.IsAvailable("suit.berserker.e0", 0), Is.False);
            Assert.That(SuitTree.IsAvailable("suit.berserker.e0", 1), Is.True);
            Assert.That(SuitTree.IsAvailable("suit.berserker.e2", 1), Is.False);
            Assert.That(SuitTree.IsAvailable("suit.berserker.e2", 2), Is.True);
            Assert.That(SuitTree.IsAvailable("suit.warrior.e3", 5), Is.False, "outside the table");
            Assert.That(SuitTree.IsAvailable("suit.farmer.e0", 5), Is.False, "not a tree suit");
            Assert.That(SuitTree.IsAvailable("garbage", 5), Is.False);
            Assert.That(SuitTree.IsTreeSuit("suit.medic"), Is.True);
            Assert.That(SuitTree.IsTreeSuit("suit.farmer"), Is.False);
            Assert.That(SuitTree.IsTreeSuit("district.farm"), Is.False);
        }

        [Test]
        public void CanOwnNeedsTheArenaAndTheParent()
        {
            SuitTree.TryGet("suit.warrior", 1, out var variant);
            SuitTree.TryGet("suit.warrior", 0, out var root);
            Assert.That(SuitTree.CanOwn(root, 0, new List<string>()), Is.True);
            Assert.That(SuitTree.CanOwn(variant, 0, new[] { "suit.warrior.e0" }), Is.False, "arena not reached");
            Assert.That(SuitTree.CanOwn(variant, 1, new List<string>()), Is.False, "parent not owned");
            Assert.That(SuitTree.CanOwn(variant, 1, new[] { "suit.warrior.e0" }), Is.True);
        }

        [Test]
        public void OwnershipProblemsFlagsTreeSuitVariantsTheTableCannotAccountFor()
        {
            var owned = new List<string>
            {
                "suit.warrior.e0", "suit.warrior.e1", "suit.warrior.e3", "suit.warrior.e5", // e3/e5: pre-tree eras
                "suit.guardian.e1", // no parent
                "suit.scout.e2", "suit.scout.e0", // scout.e2 needs arena 1
                "suit.farmer.e4", "district.farm.e5" // outside the tree: not reported
            };
            Assert.That(SuitTree.OwnershipProblems(owned, 0),
                Is.EqualTo(new[] { "suit.guardian.e1", "suit.scout.e2", "suit.warrior.e1", "suit.warrior.e3", "suit.warrior.e5" }));
            Assert.That(SuitTree.OwnershipProblems(owned, 1),
                Is.EqualTo(new[] { "suit.guardian.e1", "suit.warrior.e3", "suit.warrior.e5" }));
        }

        [Test]
        public void EraZeroInventoriesMigrateAsTheRoot()
        {
            var inventory = PlayerStateDefaults.Inventory();
            foreach (string name in CombatSuits)
            {
                string baseId = CatalogIds.SuitBase(name);
                inventory.OwnedVariants.Add(CatalogIds.Variant(baseId, 0));
                inventory.Equipped.Variants[baseId] = CatalogIds.Variant(baseId, 0);
            }
            var before = new Dictionary<string, string>(inventory.Equipped.Variants);
            Assert.That(InventoryClamp.ClampToArena(inventory, 0), Is.False);
            Assert.That(inventory.Equipped.Variants, Is.EqualTo(before));
            Assert.That(SuitTree.OwnershipProblems(inventory.OwnedVariants, 1), Is.Empty);
            Assert.That(SuitTree.OwnershipProblems(inventory.OwnedVariants, 0),
                Is.EqualTo(new[] { "suit.berserker.e0", "suit.medic.e0" }), "roots owned before their debut arena");
        }

        [Test]
        public void SuitAboveTheArenaIsDemotedToTheRootAndOwnershipIsKept()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "suit.warrior.e0", "suit.warrior.e2", "suit.berserker.e0", "suit.berserker.e1" });
            inventory.Equipped.Variants["suit.warrior"] = "suit.warrior.e2";
            inventory.Equipped.Variants["suit.berserker"] = "suit.berserker.e1";
            inventory.Equipped.Skins["suit.warrior"] = "skin.suit.warrior.default";
            var owned = new List<string>(inventory.OwnedVariants);

            Assert.That(InventoryClamp.ClampToArena(inventory, 0), Is.True);
            Assert.That(inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e0"));
            Assert.That(inventory.Equipped.Variants["suit.berserker"], Is.EqualTo("suit.berserker.e0"));
            Assert.That(inventory.Equipped.Skins["suit.warrior"], Is.EqualTo("skin.suit.warrior.default"));
            Assert.That(inventory.OwnedVariants, Is.EqualTo(owned));
            Assert.That(InventoryClamp.ClampToArena(inventory, 0), Is.False);
        }

        [Test]
        public void RootBeforeItsDebutArenaIsTheFloorAndIsNotRewritten()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.Add("suit.medic.e0");
            inventory.Equipped.Variants["suit.medic"] = "suit.medic.e0";
            Assert.That(InventoryClamp.ClampToArena(inventory, 0), Is.False);
            Assert.That(inventory.Equipped.Variants["suit.medic"], Is.EqualTo("suit.medic.e0"));
        }

        [Test]
        public void SuitAtOrBelowTheArenaIsLeftAlone()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "suit.warrior.e0", "suit.warrior.e2" });
            inventory.Equipped.Variants["suit.warrior"] = "suit.warrior.e2";
            Assert.That(InventoryClamp.ClampToArena(inventory, 1), Is.False);
            Assert.That(inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e2"));
        }

        [Test]
        public void PreTreeEraAboveTwoIsDemotedToTheRootEvenAtHighArenaButStaysOwned()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "suit.scout.e0", "suit.scout.e1", "suit.scout.e4" });
            inventory.Equipped.Variants["suit.scout"] = "suit.scout.e4";
            Assert.That(InventoryClamp.ClampToArena(inventory, 5), Is.True);
            Assert.That(inventory.Equipped.Variants["suit.scout"], Is.EqualTo("suit.scout.e0"));
            Assert.That(inventory.OwnedVariants, Does.Contain("suit.scout.e4"));
        }

        [Test]
        public void LegacySuitWithoutARootGetsTheRootGranted()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.Add("suit.guardian.e2");
            inventory.Equipped.Variants["suit.guardian"] = "suit.guardian.e2";
            InventoryClamp.ClampToArena(inventory, 0);
            Assert.That(inventory.Equipped.Variants["suit.guardian"], Is.EqualTo("suit.guardian.e0"));
            Assert.That(inventory.OwnedVariants, Is.EquivalentTo(new[] { "suit.guardian.e0", "suit.guardian.e2" }));
        }

        [Test]
        public void DistrictClampIsUnchangedBesideSuits()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "district.farm.e0", "district.farm.e1", "suit.warrior.e0", "suit.warrior.e1" });
            inventory.Equipped.Variants["district.farm"] = "district.farm.e1";
            inventory.Equipped.Variants["suit.warrior"] = "suit.warrior.e1";
            Assert.That(InventoryClamp.ClampToArena(inventory, 0), Is.True);
            Assert.That(inventory.Equipped.Variants["district.farm"], Is.EqualTo("district.farm.e0"));
            Assert.That(inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e0"));
        }

        [Test]
        public void OnlyOneVariantIsEquippedPerSuit()
        {
            var equipped = PlayerStateDefaults.Inventory().Equipped;
            equipped.Variants["suit.warrior"] = "suit.warrior.e1";
            equipped.Variants["suit.warrior"] = "suit.warrior.e2";
            Assert.That(equipped.Variants.Keys.Count(k => k == "suit.warrior"), Is.EqualTo(1));
            Assert.That(equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e2"));
        }

        [TestCase("suit.warrior.e1", 0, false)]
        [TestCase("suit.warrior.e1", 1, true)]
        [TestCase("suit.berserker.e0", 0, false)]
        [TestCase("suit.berserker.e0", 1, true)]
        [TestCase("suit.warrior.e3", 5, false)]
        [TestCase("suit.miner.e1", 1, true)]
        [TestCase("suit.miner.e2", 1, false)]
        public void ServerEquipUsesTheTreeForSuitsAndTheEraForTheRest(string id, int arena, bool allowed)
        {
            var rules = new InventoryRules(ServerCatalog.Items);
            var state = new PlayerState
            {
                Rank = new RankRecord { Arena = arena, HighestArena = 5 },
                Inventory = PlayerStateDefaults.Inventory()
            };
            state.Inventory.OwnedVariants.Add(id);
            CatalogIds.TryParseVariant(id, out string baseId, out _);
            var changes = new EquippedRecord { Variants = new Dictionary<string, string> { { baseId, id } } };
            if (allowed)
            {
                Assert.That(rules.Equip(state, changes), Is.True);
                Assert.That(state.Inventory.Equipped.Variants[baseId], Is.EqualTo(id));
            }
            else
            {
                var ex = Assert.Throws<InventoryValidationException>(() => rules.Equip(state, changes));
                Assert.That(ex.Message, Does.Contain("locked"));
            }
        }

        [Test]
        public async Task OfflineFakeAgreesWithTheServerForTreeSuits()
        {
            var store = new InMemoryPlayerRecordStore();
            var fake = new LocalInventoryService(store, new[] { "suit.warrior" });
            var state = await store.ReadAsync();
            state.Rank = new RankRecord { Arena = 0, HighestArena = 5 };
            state.Inventory = PlayerStateDefaults.Inventory();
            fake.GrantDefaults(state);
            await store.WriteAsync(state);
            var changes = new EquippedRecord { Variants = new Dictionary<string, string> { { "suit.warrior", "suit.warrior.e1" } } };
            Assert.ThrowsAsync<InventoryValidationException>(async () => await fake.EquipAsync(changes));
            state.Rank.Arena = 1;
            await store.WriteAsync(state);
            var result = await fake.EquipAsync(changes);
            Assert.That(result.Inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e1"));
        }
    }
}
