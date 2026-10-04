using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Which move orders are issued but not yet visible in the simulation, and
    /// when each stops being worth showing.
    ///
    /// Lockstep applies a command two or more ticks after it is issued, so for
    /// that long the ordered villager still looks idle. PendingOrderView fills
    /// the gap with a provisional route; this is its lifecycle, kept apart from
    /// Unity so it can be tested. Time is passed in rather than read, which is
    /// also why it is not a MonoBehaviour.
    ///
    /// An order ends one of four ways: the simulation now shows that destination
    /// (the real route takes over), a newer order replaces it, the villager can
    /// no longer act on it, or the timeout runs out -- the only exit for a
    /// command the simulation refused, since a refusal leaves no trace.
    /// </summary>
    public class PendingOrderTracker
    {
        public struct Order
        {
            public int villagerID;
            public int targetNode;
            public float issuedAt;

            /// <summary>
            /// Nodes the provisional line passes through after the villager's
            /// own position. Plain data the view built once at issue time.
            /// </summary>
            public int[] routeNodes;
        }

        private readonly List<Order> orders = new List<Order>();
        private readonly float timeoutSeconds;

        public PendingOrderTracker(float timeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds;
        }

        public IReadOnlyList<Order> Orders => orders;

        /// <summary>Adds an order, replacing any earlier one for the same villager.</summary>
        public void Issue(int villagerID, int targetNode, int[] routeNodes, float now)
        {
            Cancel(villagerID);

            orders.Add(new Order
            {
                villagerID = villagerID,
                targetNode = targetNode,
                issuedAt = now,
                routeNodes = routeNodes
            });
        }

        public void Cancel(int villagerID)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                if (orders[i].villagerID == villagerID) orders.RemoveAt(i);
            }
        }

        public void Clear()
        {
            orders.Clear();
        }

        /// <summary>
        /// Drops every order that has been overtaken. Run before drawing, so the
        /// frame the simulation's route appears is the frame the provisional one
        /// is gone -- neither a gap nor two lines.
        /// </summary>
        public void Sweep(float now, VillagerData[] villagers)
        {
            for (int i = orders.Count - 1; i >= 0; i--)
            {
                if (IsOver(orders[i], now, villagers)) orders.RemoveAt(i);
            }
        }

        private bool IsOver(Order order, float now, VillagerData[] villagers)
        {
            if (now - order.issuedAt >= timeoutSeconds) return true;
            if (villagers == null) return true;
            if (order.villagerID < 0 || order.villagerID >= villagers.Length) return true;

            VillagerData villager = villagers[order.villagerID];

            if (villager.isConsumed || villager.state == VillagerState.Dead) return true;

            return villager.state == VillagerState.Moving && villager.targetNodeID == order.targetNode;
        }
    }
}
