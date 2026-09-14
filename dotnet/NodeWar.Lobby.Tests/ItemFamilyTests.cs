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
        [TestCase("node_shrine", ItemFamily.Family.Round)]
        [TestCase("node_sanctuary", ItemFamily.Family.Round)]
        [TestCase("node_rampart", ItemFamily.Family.Square)]
        [TestCase("node_camp", ItemFamily.Family.Triangle)]
        [TestCase("node_barracks", ItemFamily.Family.Triangle)]
        [TestCase("node_arsenal", ItemFamily.Family.Triangle)]
        [TestCase("node_watchtower", ItemFamily.Family.Triangle)]
        public void ForNode_PlacesEveryOfferedDistrict(string nodeID, ItemFamily.Family expected)
        {
            Assert.AreEqual(expected, ItemFamily.ForNode(nodeID));
        }

        /// <summary>A district added later must not throw or vanish from the grid.</summary>
        [TestCase("node_not_yet_designed")]
        [TestCase("")]
        [TestCase(null)]
        public void ForNode_UnknownIDIsSquare(string nodeID)
        {
            Assert.AreEqual(ItemFamily.Family.Square, ItemFamily.ForNode(nodeID));
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
