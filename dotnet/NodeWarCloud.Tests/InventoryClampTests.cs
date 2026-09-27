using System.Collections.Generic;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class InventoryClampTests
    {
        [Test]
        public void DemotionChoosesHighestOwnedEraNotHighestPossibleEraAndKeepsSkins()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "suit.warrior.e0", "suit.warrior.e1", "suit.warrior.e4", "suit.scout.e0" });
            inventory.Equipped.Variants["suit.warrior"] = "suit.warrior.e4";
            inventory.Equipped.Variants["suit.scout"] = "suit.scout.e0";
            inventory.Equipped.Skins["suit.warrior"] = "skin.suit.warrior.default";
            var owned = new List<string>(inventory.OwnedVariants);

            Assert.That(InventoryClamp.ClampToArena(inventory, 3), Is.True);
            Assert.That(inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e1"));
            Assert.That(inventory.Equipped.Variants["suit.scout"], Is.EqualTo("suit.scout.e0"));
            Assert.That(inventory.Equipped.Skins["suit.warrior"], Is.EqualTo("skin.suit.warrior.default"));
            Assert.That(inventory.OwnedVariants, Is.EqualTo(owned));
            Assert.That(InventoryClamp.ClampToArena(inventory, 3), Is.False);
        }

        [Test]
        public void LegacyInventoryReceivesStarterIfNoEligibleOwnedVariantRemains()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.Add("district.farm.e2");
            inventory.Equipped.Variants["district.farm"] = "district.farm.e2";
            InventoryClamp.ClampToArena(inventory, 0);
            Assert.That(inventory.Equipped.Variants["district.farm"], Is.EqualTo("district.farm.e0"));
            Assert.That(inventory.OwnedVariants, Is.EquivalentTo(new[] { "district.farm.e0", "district.farm.e2" }));
        }
    }
}
