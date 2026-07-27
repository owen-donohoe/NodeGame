using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.Simulation
{
    public static class Pathfinding
    {
        /// <summary>
        /// BFS from startNode to endNode. Returns array of node IDs representing the path
        /// (inclusive of start and end). Returns empty array if no path found.
        /// </summary>
        public static int[] FindPath(SimulationState state, int startNode, int endNode)
        {
            if (startNode == endNode)
                return new int[] { startNode };

            int nodeCount = state.nodes.Length;
            bool[] visited = new bool[nodeCount];
            int[] cameFrom = new int[nodeCount];

            for (int i = 0; i < nodeCount; i++)
                cameFrom[i] = -1;

            Queue<int> frontier = new Queue<int>();
            frontier.Enqueue(startNode);
            visited[startNode] = true;

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();

                int[] neighbors = state.nodes[current].connectedNodes;
                for (int i = 0; i < neighbors.Length; i++)
                {
                    int neighbor = neighbors[i];
                    if (visited[neighbor]) continue;

                    visited[neighbor] = true;
                    cameFrom[neighbor] = current;

                    if (neighbor == endNode)
                    {
                        // Reconstruct path
                        return ReconstructPath(cameFrom, startNode, endNode);
                    }

                    frontier.Enqueue(neighbor);
                }
            }

            // No path found
            return new int[0];
        }

        private static int[] ReconstructPath(int[] cameFrom, int start, int end)
        {
            // Count path length
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
    }
}