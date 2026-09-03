using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The stand-in for art that does not exist.
    ///
    /// One property matters and it is easy to lose: the same item must get the
    /// same tint every time. The obvious implementation - string.GetHashCode -
    /// is randomised per process on .NET Core, so a district would tint
    /// differently on every launch and nothing would fail except the design.
    /// These tests pin the mapping so that swap cannot happen quietly.
    /// </summary>
    [TestFixture]
    public class ItemTintTests
    {
        [Test]
        public void IndexFor_IsStableForTheSameID()
        {
            Assert.AreEqual(ItemTint.IndexFor("node_barracks"), ItemTint.IndexFor("node_barracks"));
        }

        /// <summary>
        /// The mapping written down. If ItemTint's hash is ever replaced these
        /// break, which is the point: every district silently changing colour is
        /// a decision, not a refactor.
        /// </summary>
        [Test]
        public void IndexFor_MatchesThePinnedMapping()
        {
            Assert.AreEqual(1, ItemTint.IndexFor("node_barracks"));
            Assert.AreEqual(6, ItemTint.IndexFor("node_market"));
            Assert.AreEqual(5, ItemTint.IndexFor("suit_scout"));
            Assert.AreEqual(6, ItemTint.IndexFor("suit_guardian"));
        }

        /// <summary>
        /// Fourteen items into eight tints collide, necessarily. Recorded here
        /// rather than treated as a defect: the alternative is tinting by
        /// position in the list, which would break the thing that actually
        /// matters - an item wearing the same tint in its slot as in the list.
        /// The monogram is the identity channel; the tint only reinforces it.
        /// </summary>
        [Test]
        public void IndexFor_CollidesForSomePairs_WhichIsExpected()
        {
            Assert.AreEqual(ItemTint.IndexFor("suit_guardian"),
                            ItemTint.IndexFor("suit_berserker"));

            Assert.AreNotEqual(ItemTint.MonogramFor("Guardian", "suit_guardian"),
                               ItemTint.MonogramFor("Berserker", "suit_berserker"));
        }

        [Test]
        public void IndexFor_IsAlwaysInRange()
        {
            string[] ids =
            {
                "node_arsenal", "node_barracks", "node_camp", "node_crossroads",
                "node_market", "node_rampart", "node_sanctuary", "node_shrine",
                "node_watchtower",
                "suit_warrior", "suit_guardian", "suit_scout", "suit_berserker",
                "suit_medic",
                "", null, "x", "a very long identifier that nobody would ever use"
            };

            for (int i = 0; i < ids.Length; i++)
            {
                int index = ItemTint.IndexFor(ids[i]);

                Assert.GreaterOrEqual(index, 0, ids[i]);
                Assert.Less(index, ItemTint.Count, ids[i]);
            }
        }

        [Test]
        public void IndexFor_SeparatesTheIDsThatDifferOnlyAtTheEnd()
        {
            // node_camp and node_market sit next to each other in the list; a
            // hash that ignored later characters would give them one tint.
            Assert.AreNotEqual(ItemTint.IndexFor("node_camp"), ItemTint.IndexFor("node_market"));
        }

        [Test]
        public void ClassFor_NamesAThemeClass()
        {
            Assert.AreEqual("tile-tint--1", ItemTint.ClassFor("node_barracks"));
        }

        [Test]
        public void MonogramFor_UsesTheDisplayNameFirst()
        {
            Assert.AreEqual("B", ItemTint.MonogramFor("Barracks", "node_barracks"));
        }

        [Test]
        public void MonogramFor_FallsBackToTheIDThenToADash()
        {
            // A definition asset with no displayName still gets a tile.
            Assert.AreEqual("N", ItemTint.MonogramFor("", "node_barracks"));
            Assert.AreEqual("N", ItemTint.MonogramFor(null, "node_barracks"));
            Assert.AreEqual("-", ItemTint.MonogramFor(null, null));
            Assert.AreEqual("-", ItemTint.MonogramFor("___", ""));
        }

        [Test]
        public void MonogramFor_SkipsLeadingPunctuationAndUpperCases()
        {
            Assert.AreEqual("C", ItemTint.MonogramFor("crossroads", "node_crossroads"));
            Assert.AreEqual("W", ItemTint.MonogramFor("  watchtower", "node_watchtower"));
        }
    }
}
