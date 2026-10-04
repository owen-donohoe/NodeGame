using System.Collections.Generic;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class InventoryClampTests
    {
        [Test]
        public void DistrictDemotionChoosesHighestOwnedEraNotHighestPossibleEraAndKeepsSkins()
        {
            var inventory = PlayerStateDefaults.Inventory();
            inventory.OwnedVariants.AddRange(new[] { "district.farm.e0", "district.farm.e1", "district.farm.e4", "district.mine.e0" });
            inventory.Equipped.Variants["district.farm"] = "district.farm.e4";
            inventory.Equipped.Variants["district.mine"] = "district.mine.e0";
            inventory.Equipped.Skins["district.farm"] = "skin.district.farm.default";
            var owned = new List<string>(inventory.OwnedVariants);

            Assert.That(InventoryClamp.ClampToArena(inventory, 3), Is.True);
            Assert.That(inventory.Equipped.Variants["district.farm"], Is.EqualTo("district.farm.e1"));
            Assert.That(inventory.Equipped.Variants["district.mine"], Is.EqualTo("district.mine.e0"));
            Assert.That(inventory.Equipped.Skins["district.farm"], Is.EqualTo("skin.district.farm.default"));
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
