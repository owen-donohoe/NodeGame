using System.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StageArtTests
    {
        [Test]
        public void EveryCatalogDistrictAndSuitIncludingSideVariantsResolvesToNonemptyPaper()
        {
            for (int type = 1; type < CatalogKeys.DistrictTableLength; type++)
            {
                CollectionAssert.AreEqual(new[] { type }, StageLoadoutTypes.Districts(new[] { CatalogKeys.DistrictLobbyId(type) }));
                Nonempty(StageArt.District(type));
                Assert.That(StageArt.Identity(type, false), Is.EqualTo(CatalogKeys.DistrictLobbyId(type)));
            }
            for (int type = 1; type < CatalogKeys.SuitTableLength; type++)
            {
                CollectionAssert.AreEqual(new[] { type }, StageLoadoutTypes.Suits(new[] { CatalogKeys.SuitLobbyId(type) }));
                Nonempty(StageArt.Suit(type));
                Assert.That(StageArt.Identity(type, true), Is.EqualTo(CatalogKeys.SuitLobbyId(type)));
                foreach (var variant in SuitTree.Nodes.Where(node => node.BaseId == CatalogKeys.SuitBase(type)))
                    CollectionAssert.AreEqual(new[] { type }, StageLoadoutTypes.Suits(new[] { variant.Id }), variant.Id);
            }
        }
        private static void Nonempty(PaperKind kind)
        {
            byte[] mask = PaperRasterizer.RasterizeCoverage(kind, 64, out byte[] alpha);
            Assert.That(mask.Count(pixel => pixel == 1), Is.GreaterThan(50));
            Assert.That(mask.Count(pixel => pixel == 2), Is.GreaterThan(50));
            Assert.That(alpha.Count(pixel => pixel > 0), Is.GreaterThan(100));
        }
    }
}
