using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Board presence earns route information. Each consumer rebuilds its own
    /// scratch at its cadence, so a frame and a tick never depend on run order.
    /// The line toggle and camera fade belong to the renderer, not this gate.
    /// </summary>
    public sealed class OpponentRouteGate
    {
        private int[] hopsFromPlayer;
        private int[] bfsQueue;
        private int nodeCount;

        public bool WithinHops(int node, int limit)
        {
            if (limit <= 0) return true;
            if (node < 0 || node >= nodeCount) return false;

            int hops = hopsFromPlayer[node];
            return hops >= 0 && hops <= limit;
        }

        /// <summary>
        /// Multi-source breadth-first search out from every node the local player
        /// owns or is standing on. Hops, not travel cost: the question is board
        /// presence, not how long a walk would take.
        /// </summary>
        public void ComputeHopsFromPlayer(SimulationState state, int localPlayerID, int limit)
        {
            nodeCount = state.nodes.Length;

            if (hopsFromPlayer == null || hopsFromPlayer.Length < nodeCount)
            {
                hopsFromPlayer = new int[nodeCount];
                bfsQueue = new int[nodeCount];
            }

            for (int i = 0; i < nodeCount; i++)
                hopsFromPlayer[i] = -1;

            int tail = 0;

            for (int i = 0; i < nodeCount; i++)
            {
                if (state.nodes[i].ownerID != localPlayerID) continue;
                hopsFromPlayer[i] = 0;
                bfsQueue[tail++] = i;
            }

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != localPlayerID) continue;
                if (v.isConsumed || v.state == VillagerState.Dead) continue;

                int node = v.currentNodeID;
                if (node < 0 || node >= nodeCount) continue;
                if (hopsFromPlayer[node] >= 0) continue;

                hopsFromPlayer[node] = 0;
                bfsQueue[tail++] = node;
            }

            int head = 0;

            while (head < tail)
            {
                int current = bfsQueue[head++];
                if (hopsFromPlayer[current] >= limit) continue;

                Edge[] edges = state.nodes[current].edges;
                for (int e = 0; e < edges.Length; e++)
                {
                    int next = edges[e].toNode;
                    if (next < 0 || next >= nodeCount) continue;
                    if (hopsFromPlayer[next] >= 0) continue;

                    hopsFromPlayer[next] = hopsFromPlayer[current] + 1;
                    bfsQueue[tail++] = next;
                }
            }
        }

    }
}
