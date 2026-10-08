using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>Read-only intent derived from the current simulation, including rollback.</summary>
    public static class OrderPresentation
    {
        public struct Route
        {
            public int[] nodes;
            public bool intentConnector;
            public bool intentMarker;
        }
        public struct Appearance
        {
            public float alpha;
            public bool visible, amber, dashed;
        }
        public static bool Interrupted(VillagerData v) =>
            v.targetNodeID >= 0 && !v.isConsumed && v.state != VillagerState.Dead &&
            v.state != VillagerState.Moving && v.state != VillagerState.Breaching;

        public static Appearance Style(VillagerData v, float interruptedSeconds, bool reducedMotion)
        {
            bool interrupted = Interrupted(v);
            float t = reducedMotion ? 1f : System.Math.Max(0f, System.Math.Min(1f, interruptedSeconds / 0.35f));
            return new Appearance { visible = v.targetNodeID >= 0 && !v.isConsumed && v.state != VillagerState.Dead,
                amber = interrupted, dashed = interrupted, alpha = interrupted ? 1f - 0.75f * t : 1f };
        }

        public static Route BuildRoute(SimulationState state, VillagerData v, bool mine, int revealLegs)
        {
            var empty = new Route { nodes = new int[0] };
            if (v.targetNodeID < 0 || v.targetNodeID >= state.nodes.Length || v.isConsumed || v.state == VillagerState.Dead)
                return empty;
            int[] path;
            int index = 0;
            if (v.state == VillagerState.Moving)
            {
                path = v.movePath;
                index = v.movePathIndex;
                if (path == null || index < 0 || index + 1 >= path.Length) return empty;
            }
            else if (Interrupted(v))
            {
                if (v.currentNodeID == v.targetNodeID && v.currentNodeID == state.players[1 - v.ownerID].coreNodeID)
                    return mine ? new Route { nodes = new[] { v.currentNodeID }, intentMarker = true } : empty;
                path = Pathfinding.FindPath(state, v.ownerID, v.currentNodeID, v.targetNodeID);
                // At a contested destination retain the final approach, from replicated
                // movement state, until arrival action begins. Never use a cached curve.
                if (path.Length == 1 && v.movePath != null && v.movePathIndex > 0 &&
                    v.movePathIndex < v.movePath.Length && v.movePath[v.movePathIndex] == v.targetNodeID)
                    path = new[] { v.movePath[v.movePathIndex - 1], v.targetNodeID };
                if (path.Length == 0)
                    return mine ? new Route { nodes = new[] { v.currentNodeID, v.targetNodeID }, intentConnector = true } : empty;
            }
            else return empty;
            int count = path.Length - index;
            if (!mine) count = System.Math.Min(count, System.Math.Max(0, revealLegs) + 1);
            var nodes = new int[count];
            System.Array.Copy(path, index, nodes, 0, count);
            return new Route { nodes = nodes };
        }
    }
}
