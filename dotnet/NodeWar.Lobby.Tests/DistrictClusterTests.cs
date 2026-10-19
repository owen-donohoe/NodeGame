using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class DistrictClusterTests
    {
        private static void AssertState(DistrictCluster cluster, DistrictClusterState state, int selected = -1)
        {
            Assert.AreEqual(state, cluster.State);
            Assert.AreEqual(selected, cluster.SelectedSlot);
            Assert.AreEqual(state == DistrictClusterState.Replacing, cluster.Shaking);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(state == DistrictClusterState.Replacing || i == selected ? 1 : 0,
                    cluster.TargetSeparation(i));
            Assert.AreEqual(0, cluster.TargetSeparation(-1));
            Assert.AreEqual(0, cluster.TargetSeparation(5));
        }

        [Test]
        public void Selection_SwitchesFilledSlots_AndDeselects()
        {
            var cluster = new DistrictCluster(4);
            AssertState(cluster, DistrictClusterState.Rest);
            cluster.Select(0);
            AssertState(cluster, DistrictClusterState.Selected, 0);
            cluster.Select(3);
            AssertState(cluster, DistrictClusterState.Selected, 3);
            cluster.Select(3);
            AssertState(cluster, DistrictClusterState.Selected, 3);
            cluster.Deselect();
            AssertState(cluster, DistrictClusterState.Rest);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidSelectionsAndPicks_AreNoOps(bool selected)
        {
            var cluster = new DistrictCluster(4);
            if (selected) cluster.Select(2);
            foreach (int slot in new[] { -1, 4, 5, int.MaxValue }) cluster.Select(slot);
            for (int i = -1; i <= 5; i++)
                Assert.AreEqual(DistrictClusterAction.None, cluster.Pick(i).Action);
            AssertState(cluster, selected ? DistrictClusterState.Selected : DistrictClusterState.Rest,
                selected ? 2 : -1);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UseWithEmpty_ReturnsFillEmpty_AndRestsWithoutShake(bool selected)
        {
            var cluster = new DistrictCluster(4);
            if (selected) cluster.Select(1);
            Assert.AreEqual(DistrictClusterAction.FillEmpty, cluster.Use(true).Action);
            AssertState(cluster, DistrictClusterState.Rest);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UseFull_SeparatesAndShakes_RejectsOtherEvents(bool selected)
        {
            var cluster = new DistrictCluster(5);
            if (selected) cluster.Select(1);
            Assert.AreEqual(DistrictClusterAction.None, cluster.Use(false).Action);
            AssertState(cluster, DistrictClusterState.Replacing);
            cluster.Select(2);
            cluster.Deselect();
            Assert.AreEqual(DistrictClusterAction.None, cluster.Use(true).Action);
            Assert.AreEqual(DistrictClusterAction.None, cluster.Use(false).Action);
            Assert.AreEqual(DistrictClusterAction.None, cluster.Pick(-1).Action);
            Assert.AreEqual(DistrictClusterAction.None, cluster.Pick(5).Action);
            AssertState(cluster, DistrictClusterState.Replacing);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Pick_ReplacesChosenSlot_ThenFuses(int slot)
        {
            var cluster = new DistrictCluster(5);
            cluster.Use(false);
            var result = cluster.Pick(slot);
            Assert.AreEqual(DistrictClusterAction.Replaced, result.Action);
            Assert.AreEqual(slot, result.Slot);
            AssertState(cluster, DistrictClusterState.Rest);
        }

        [TestCase(DistrictClusterState.Rest)]
        [TestCase(DistrictClusterState.Selected)]
        [TestCase(DistrictClusterState.Replacing)]
        public void Cancel_FromEveryState_Fuses(DistrictClusterState state)
        {
            var cluster = new DistrictCluster(5);
            if (state == DistrictClusterState.Selected) cluster.Select(0);
            if (state == DistrictClusterState.Replacing) cluster.Use(false);
            cluster.Cancel();
            cluster.Cancel();
            cluster.Deselect();
            AssertState(cluster, DistrictClusterState.Rest);
        }

        [TestCase(-1)]
        [TestCase(0)]
        public void EmptyCluster_HasNoSelectableSlots(int equipped)
        {
            var cluster = new DistrictCluster(equipped);
            for (int i = 0; i < 5; i++) cluster.Select(i);
            AssertState(cluster, DistrictClusterState.Rest);
        }
    }
}
