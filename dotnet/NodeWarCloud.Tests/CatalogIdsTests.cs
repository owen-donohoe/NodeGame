using System.Globalization;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class CatalogIdsTests
    {
        [Test]
        public void AllGeneratedIds_RoundTrip()
        {
            foreach (string baseId in CatalogTests.ExpectedBases())
            {
                for (int era = 0; era < CatalogIds.EraCount; era++)
                {
                    Assert.That(CatalogIds.TryParseVariant(CatalogIds.Variant(baseId, era), out string parsed, out int parsedEra), Is.True);
                    Assert.That(parsed, Is.EqualTo(baseId));
                    Assert.That(parsedEra, Is.EqualTo(era));
                }
                Assert.That(CatalogIds.TryParseSkin(CatalogIds.DefaultSkin(baseId), out string skinBase), Is.True);
                Assert.That(skinBase, Is.EqualTo(baseId));
            }
        }

        [Test]
        public void Names_AreInvariantLowerCase()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(CatalogIds.SuitBase("MINER"), Is.EqualTo("suit.miner"));
                Assert.That(CatalogIds.DistrictBase("SHRINE"), Is.EqualTo("district.shrine"));
                Assert.That(CatalogIds.Variant("suit.miner", 5), Is.EqualTo("suit.miner.e5"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("suit.warrior.e-1")]
        [TestCase("suit.warrior.e6")]
        [TestCase("suit.warrior.e01")]
        [TestCase("suit.warrior.e+1")]
        [TestCase("suit.warrior.e1\n")]
        [TestCase("suit.Warrior.e1")]
        [TestCase("suit..e1")]
        [TestCase("other.warrior.e1")]
        [TestCase("suit.warrior.e999999999999")]
        [TestCase("skin.suit.warrior.default")]
        public void BadVariants_ReturnFalseAndEmptyOutputs(string id)
        {
            Assert.That(CatalogIds.TryParseVariant(id, out string baseId, out int era), Is.False);
            Assert.That(baseId, Is.Null);
            Assert.That(era, Is.EqualTo(-1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("skin.suit.warrior.")]
        [TestCase("skin.suit.warrior.default.extra")]
        [TestCase("skin.suit.warrior.Default")]
        [TestCase("skin.suit.warrior.default\n")]
        [TestCase("suit.warrior.e0")]
        public void BadSkins_ReturnFalseAndEmptyOutput(string id)
        {
            Assert.That(CatalogIds.TryParseSkin(id, out string baseId), Is.False);
            Assert.That(baseId, Is.Null);
        }

        [Test]
        public void CosmeticSkinNames_ParseWithoutImplyingOwnership()
        {
            Assert.That(CatalogIds.TryParseSkin("skin.district.rampart.gold", out string baseId), Is.True);
            Assert.That(baseId, Is.EqualTo("district.rampart"));
        }
    }
}
