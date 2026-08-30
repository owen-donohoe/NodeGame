using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.Simulation
{
    public static class Pathfinding
    {
        // Integer percentages: 50 = 0.5x, 75 = 0.75x, 100 = 1.0x, 150 = 1.5x, 200 = 2.0x
        public static int OwnedMultiplier = 50;
        public static int PartiallyOwnedMultiplier = 75;
        public static int UnownedMultiplier = 100;
        public static int EnemyPartiallyOwnedMultiplier = 150;
        public static int EnemyOwnedMultiplier = 200;

        /// <summary>
        /// Dijkstra's algorithm from startNode to endNode using edge weights.
        /// Returns array of node IDs representing the path (inclusive of start and end).
        /// Returns empty array if no path found.
        /// </summary>
        public static int[] FindPath(SimulationState state, int askingOwnerId, int startNode, int endNode)
        {
            if (startNode == endNode)
                return new int[] { startNode };

            int nodeCount = state.nodes.Length;
            int[] dist = new int[nodeCount];
            int[] cameFrom = new int[nodeCount];
            bool[] visited = new bool[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                dist[i] = int.MaxValue;
                cameFrom[i] = -1;
            }
            dist[startNode] = 0;

            for (int step = 0; step < nodeCount; step++)
            {
                int current = -1;
                int currentDist = int.MaxValue;
                for (int i = 0; i < nodeCount; i++)
                {
                    if (!visited[i] && dist[i] < currentDist)
                    {
                        current = i;
                        currentDist = dist[i];
                    }
                }

                if (current == -1) break;
                if (current == endNode) return ReconstructPath(cameFrom, startNode, endNode);

                visited[current] = true;

                Edge[] edges = state.nodes[current].edges;
                for (int i = 0; i < edges.Length; i++)
                {
                    int neighbor = edges[i].toNode;
                    if (visited[neighbor]) continue;

                    int travelWeight = edges[i].travelWeight;
                    int multiplier = GetPreferenceMultiplier(state, neighbor, askingOwnerId);

                    // Integer percentage: (weight * multiplier) / 100, minimum 1
                    int scaledCost = (travelWeight * multiplier + 99) / 100; // ceiling division
                    if (scaledCost < 1) scaledCost = 1;

                    int newDist = dist[current] + scaledCost;
                    if (newDist < dist[neighbor])
                    {
                        dist[neighbor] = newDist;
                        cameFrom[neighbor] = current;
                    }
                }
            }
            return new int[0];
        }

        private static int[] ReconstructPath(int[] cameFrom, int start, int end)
        {
            List<int> path = new List<int>();
            int current = end;

            while (current != start)
            {
                path.Add(current);
                current = cameFrom[current];
            }
            path.Add(start);
            path.Reverse();

            return path.ToArray();
        }

        public enum NodeOwnership
        {
            Owned,
            PartiallyOwned,
            Unowned,
            EnemyPartiallyOwned,
            EnemyOwned
        }

        public static NodeOwnership GetOwnershipStatus(NodeData node, int askingPlayerID)
        {
            if (node.ownerID == askingPlayerID) return NodeOwnership.Owned;
            if (node.ownerID != -1) return NodeOwnership.EnemyOwned;

            int signForAskingPlayer = (askingPlayerID == 0) ? 1 : -1;
            int leaning = node.claimBar * signForAskingPlayer;

            if (leaning > 0) return NodeOwnership.PartiallyOwned;
            if (leaning < 0) return NodeOwnership.EnemyPartiallyOwned;
            return NodeOwnership.Unowned;
        }

        private static int GetPreferenceMultiplier(SimulationState state, int nodeID, int askingPlayerID)
        {
            switch (GetOwnershipStatus(state.nodes[nodeID], askingPlayerID))
            {
                case NodeOwnership.Owned: return OwnedMultiplier;
                case NodeOwnership.PartiallyOwned: return PartiallyOwnedMultiplier;
                case NodeOwnership.EnemyPartiallyOwned: return EnemyPartiallyOwnedMultiplier;
                case NodeOwnership.EnemyOwned: return EnemyOwnedMultiplier;
                default: return UnownedMultiplier;
            }
        }
    }
}