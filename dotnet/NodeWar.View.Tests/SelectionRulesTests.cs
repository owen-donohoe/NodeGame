using System.Collections.Generic;
using NodeWar.Input;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class SelectionRulesTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NearestPickIgnoresRaycastOrderAndUnselectableOrBehindCandidates(bool reverse)
        {
            var candidates = new List<VillagerPickCandidate> {
                new VillagerPickCandidate(1, new GesturePoint(6f, 0f), true, true),
                new VillagerPickCandidate(2, new GesturePoint(2f, 0f), true, true),
                new VillagerPickCandidate(3, new GesturePoint(0f, 0f), false, true),
                new VillagerPickCandidate(4, new GesturePoint(0f, 0f), true, false)
            };
            if (reverse) candidates.Reverse();
            Assert.AreEqual(2, SelectionRules.NearestVillager(candidates, new GesturePoint(0f, 0f)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NearestPickTiesUseVillagerId(bool reverse)
        {
            var candidates = new List<VillagerPickCandidate> {
                new VillagerPickCandidate(7, new GesturePoint(2f, 0f), true, true),
                new VillagerPickCandidate(3, new GesturePoint(-2f, 0f), true, true)
            };
            if (reverse) candidates.Reverse();
            Assert.AreEqual(3, SelectionRules.NearestVillager(candidates, new GesturePoint(0f, 0f)));
        }

        [Test]
        public void NoSelectableVillagerFallsThroughToNode()
        {
            var candidates = new[] { new VillagerPickCandidate(1, new GesturePoint(0f, 0f), false, true) };
            Assert.AreEqual(-1, SelectionRules.NearestVillager(candidates, new GesturePoint(0f, 0f)));
        }

        [Test]
        public void NearestPickUsesTouchPositionInsteadOfScreenOrigin()
        {
            var candidates = new[] {
                new VillagerPickCandidate(1, new GesturePoint(0f, 0f), true, true),
                new VillagerPickCandidate(2, new GesturePoint(4f, 5f), true, true)
            };
            Assert.AreEqual(2, SelectionRules.NearestVillager(candidates, new GesturePoint(5f, 5f)));
        }

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
