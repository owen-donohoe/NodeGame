using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The Workshop's shape language. Every district the draft offers must land
    /// in the family the prototype gives it, and the grid order must follow the
    /// prototype's round, square, triangle.
    /// </summary>
    [TestFixture]
    public class ItemFamilyTests
    {
        [TestCase("node_market", ItemFamily.Family.Round)]
        [TestCase("node_infirmary", ItemFamily.Family.Round)]
        [TestCase("node_town", ItemFamily.Family.Round)]
        [TestCase("node_fortress", ItemFamily.Family.Square)]
        [TestCase("node_barracks", ItemFamily.Family.Triangle)]
        [TestCase("node_farm", ItemFamily.Family.Round)]
        [TestCase("node_forge", ItemFamily.Family.Round)]
        [TestCase("node_pier", ItemFamily.Family.Square)]
        public void ForDistrict_PlacesEveryOfferedDistrict(string districtID, ItemFamily.Family expected)
        {
            Assert.AreEqual(expected, ItemFamily.ForDistrict(districtID));
        }

        /// <summary>A district added later must not throw or vanish from the grid.</summary>
        [TestCase("node_not_yet_designed")]
        [TestCase("")]
        [TestCase(null)]
        public void ForDistrict_UnknownIDIsSquare(string districtID)
        {
            Assert.AreEqual(ItemFamily.Family.Square, ItemFamily.ForDistrict(districtID));
        }

        [Test]
        public void SortKey_OrdersRoundSquareTriangleThenCombat()
        {
            Assert.Less(ItemFamily.SortKey(ItemFamily.Family.Round), ItemFamily.SortKey(ItemFamily.Family.Square));
            Assert.Less(ItemFamily.SortKey(ItemFamily.Family.Square), ItemFamily.SortKey(ItemFamily.Family.Triangle));
            Assert.Less(ItemFamily.SortKey(ItemFamily.Family.Triangle), ItemFamily.SortKey(ItemFamily.Family.Combat));
        }

        [Test]
        public void EveryFamilyHasANoteAndAStyleClass()
        {
            foreach (ItemFamily.Family family in System.Enum.GetValues(typeof(ItemFamily.Family)))
            {
                Assert.IsNotEmpty(ItemFamily.NoteFor(family));
                StringAssert.StartsWith("lb-art--", ItemFamily.ClassFor(family));
            }
        }
    }
}
