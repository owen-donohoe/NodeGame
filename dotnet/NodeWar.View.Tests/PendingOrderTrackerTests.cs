using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class PendingOrderTrackerTests
    {
        private static readonly int[] Route = { 1, 2 };

        private static VillagerData[] Villagers(int count)
        {
            VillagerData[] villagers = new VillagerData[count];
            for (int i = 0; i < count; i++)
            {
                villagers[i].villagerID = i;
                villagers[i].state = VillagerState.Idle;
                villagers[i].targetNodeID = -1;
            }
            return villagers;
        }

        [Test]
        public void An_issued_order_stays_while_the_simulation_has_not_caught_up()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            tracker.Issue(0, 2, Route, 10f);

            tracker.Sweep(10.1f, Villagers(2));

            Assert.AreEqual(1, tracker.Orders.Count);
        }

        [Test]
        public void The_order_drops_once_the_simulation_shows_that_destination()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(2);
            tracker.Issue(0, 2, Route, 10f);

            villagers[0].state = VillagerState.Moving;
            villagers[0].targetNodeID = 2;
            tracker.Sweep(10.2f, villagers);

            Assert.AreEqual(0, tracker.Orders.Count);
        }

        [Test]
        public void A_different_destination_in_the_simulation_does_not_drop_it()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(1);
            tracker.Issue(0, 2, Route, 10f);

            villagers[0].state = VillagerState.Moving;
            villagers[0].targetNodeID = 3;
            tracker.Sweep(10.2f, villagers);

            Assert.AreEqual(1, tracker.Orders.Count);
        }

        [Test]
        public void A_stale_target_on_an_idle_villager_does_not_count_as_handover()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(1);
            villagers[0].targetNodeID = 2;
            tracker.Issue(0, 2, Route, 10f);

            tracker.Sweep(10.2f, villagers);

            Assert.AreEqual(1, tracker.Orders.Count);
        }

        [Test]
        public void A_newer_order_replaces_the_older_one_for_the_same_villager_only()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            tracker.Issue(0, 2, Route, 10f);
            tracker.Issue(1, 2, Route, 10f);
            tracker.Issue(0, 3, new[] { 3 }, 10.5f);

            Assert.AreEqual(2, tracker.Orders.Count);
            Assert.AreEqual(1, tracker.Orders[0].villagerID);
            Assert.AreEqual(0, tracker.Orders[1].villagerID);
            Assert.AreEqual(3, tracker.Orders[1].targetNode);
        }

        [Test]
        public void Cancel_removes_only_that_villagers_order()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            tracker.Issue(0, 2, Route, 10f);
            tracker.Issue(1, 2, Route, 10f);

            tracker.Cancel(0);

            Assert.AreEqual(1, tracker.Orders.Count);
            Assert.AreEqual(1, tracker.Orders[0].villagerID);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void A_dead_or_consumed_villager_drops_the_order(bool consumed, bool dead)
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(1);
            tracker.Issue(0, 2, Route, 10f);

            villagers[0].isConsumed = consumed;
            if (dead) villagers[0].state = VillagerState.Dead;
            tracker.Sweep(10.1f, villagers);

            Assert.AreEqual(0, tracker.Orders.Count);
        }

        [Test]
        public void A_refused_order_times_out()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(1);
            tracker.Issue(0, 2, Route, 10f);

            tracker.Sweep(11.49f, villagers);
            Assert.AreEqual(1, tracker.Orders.Count);

            tracker.Sweep(11.5f, villagers);
            Assert.AreEqual(0, tracker.Orders.Count);
        }

        [Test]
        public void An_out_of_range_villager_drops_the_order_instead_of_throwing()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            tracker.Issue(5, 2, Route, 10f);

            tracker.Sweep(10.1f, Villagers(2));

            Assert.AreEqual(0, tracker.Orders.Count);
        }

        [Test]
        public void Sweep_keeps_the_survivors_when_dropping_from_the_middle()
        {
            PendingOrderTracker tracker = new PendingOrderTracker(1.5f);
            VillagerData[] villagers = Villagers(3);
            tracker.Issue(0, 2, Route, 10f);
            tracker.Issue(1, 2, Route, 10f);
            tracker.Issue(2, 2, Route, 10f);

            villagers[1].state = VillagerState.Moving;
            villagers[1].targetNodeID = 2;
            tracker.Sweep(10.1f, villagers);

            Assert.AreEqual(2, tracker.Orders.Count);
            Assert.AreEqual(0, tracker.Orders[0].villagerID);
            Assert.AreEqual(2, tracker.Orders[1].villagerID);
        }
    }
}
