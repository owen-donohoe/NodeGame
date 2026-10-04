using System.Collections.Generic;
using NodeWar.Input;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class SelectionRulesTests
    {
        [TestCase(InputAction.AddRemove)]
        [TestCase(InputAction.Replace)]
        public void TapWithNoSelectionSelectsVillager(InputAction action)
        {
            var selected = new List<int>();
            SelectionRules.TapVillager(selected, 3, action);
            CollectionAssert.AreEqual(new[] { 3 }, selected);
        }

        [Test]
        public void DefaultTapsGrowGroupAndRemoveTappedMember()
        {
            var selected = new List<int> { 1 };
            SelectionRules.TapVillager(selected, 3, InputAction.AddRemove);
            CollectionAssert.AreEqual(new[] { 1, 3 }, selected);
            SelectionRules.TapVillager(selected, 1, InputAction.AddRemove);
            CollectionAssert.AreEqual(new[] { 3 }, selected);
        }

        [Test]
        public void ReplaceTapOnUnselectedVillagerReplacesGroup()
        {
            var selected = new List<int> { 1, 2 };
            SelectionRules.TapVillager(selected, 3, InputAction.Replace);
            CollectionAssert.AreEqual(new[] { 3 }, selected);
        }

        [TestCase(InputAction.AddRemove)]
        [TestCase(InputAction.Replace)]
        public void SelectedVillagerTapAlwaysRemovesOnlyThatMember(InputAction action)
        {
            var selected = new List<int> { 1, 2, 3 };
            SelectionRules.TapVillager(selected, 2, action);
            CollectionAssert.AreEqual(new[] { 1, 3 }, selected);
        }

        [TestCase(InputAction.AddRemove)]
        [TestCase(InputAction.Replace)]
        public void TappingLastSelectedVillagerClearsSelection(InputAction action)
        {
            var selected = new List<int> { 2 };
            SelectionRules.TapVillager(selected, 2, action);
            Assert.IsEmpty(selected);
        }
    }
}
