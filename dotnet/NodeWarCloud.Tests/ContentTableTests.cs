using System.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class ContentTableTests
    {
        [Test]
        public void EveryCatalogBaseHasADebutEra_WithinTheEraCount()
        {
            foreach (string baseId in PersistenceVocabularyTests.Bases)
            {
                int era = ContentTable.DebutEra(baseId);
                Assert.That(era, Is.InRange(0, CatalogIds.EraCount - 1), baseId);
            }
            Assert.That(ContentTable.DebutEra("district.rampart"), Is.EqualTo(ContentTable.UnknownEra));
            Assert.That(ContentTable.DebutEra(null), Is.EqualTo(ContentTable.UnknownEra));
        }

        [Test]
        public void StartersAreSortedEraZeroBases_AndHoldTheWholeCoreLoop()
        {
            Assert.That(ContentTable.Starters, Is.Ordered.Using((System.Collections.Generic.IComparer<string>)System.StringComparer.Ordinal));
            foreach (string baseId in ContentTable.Starters)
            {
                Assert.That(ContentTable.DebutEra(baseId), Is.Zero, baseId);
                Assert.That(ContentTable.IsStarter(baseId), Is.True, baseId);
            }
            Assert.That(ContentTable.Starters, Is.SupersetOf(new[]
            {
                "district.farm", "district.mine", "district.town", "district.barracks", "suit.warrior", "suit.guardian"
            }));
            Assert.That(ContentTable.IsStarter("district.village"), Is.False);
        }

        [Test]
        public void DebutingPartitionsTheTable()
        {
            var all = Enumerable.Range(0, CatalogIds.EraCount).SelectMany(ContentTable.Debuting).ToList();
            Assert.That(all, Is.Unique);
            Assert.That(all, Is.EquivalentTo(PersistenceVocabularyTests.Bases));
        }
    }
}
