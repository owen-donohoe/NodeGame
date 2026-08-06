using System.Collections.Generic;
using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.Simulation
{
    public static class Pathfinding
    {
        public static float OwnedMultiplier = 0.5f;
        public static float PartiallyOwnedMultiplier = 0.75f;
        public static float UnownedMultiplier = 1.0f;
        public static float EnemyPartiallyOwnedMultiplier = 1.5f;
        public static float EnemyOwnedMultiplier = 2.0f;

        /// <summary>
        /// Dijkstra's algorithm from startNode to endNode using edge weights.
        /// Returns array of node IDs representing the path (inclusive of start and end).
        /// Returns empty array if no path found.
        /// </summary>
        public static int[] FindPath(SimulationState state, int askingOwnerId, int startNode, int endNode)
        {
            if (startNode == endNode)
                return new int[] { startNode };          // trivial case: no travel needed

            int nodeCount = state.nodes.Length;
            int[] dist = new int[nodeCount];              // dist[i] = cheapest known cost from startNode to node i
            int[] cameFrom = new int[nodeCount];           // cameFrom[i] = predecessor of i on the cheapest path found so far
            bool[] visited = new bool[nodeCount];          // visited[i] = "we've locked in the final answer for i"

            for (int i = 0; i < nodeCount; i++)
            {
                dist[i] = int.MaxValue;                    // start assuming every node is unreachable
                cameFrom[i] = -1;
            }
            dist[startNode] = 0;                           // cost to reach the start from itself is 0

            for (int step = 0; step < nodeCount; step++)    // Dijkstra never needs more than nodeCount "settle" steps
            {
                // --- Find cheapest unvisited node ("extract-min") ---
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
                // This linear scan is what makes this O(V²) instead of O(E log V).
                // Totally fine for a node-graph the size of a strategy map (tens to low
                // hundreds of nodes). If you ever have thousands of nodes, swap this
                // scan for a binary heap / priority queue. Not needed yet — don't add
                // that complexity preemptively.

                if (current == -1) break;                  // everything left unvisited is unreachable — stop early
                if (current == endNode) return ReconstructPath(cameFrom, startNode, endNode); // early exit once target is settled

                visited[current] = true;                    // lock in current's distance as final (classic Dijkstra guarantee)

                // --- Relax current's neighbors ---
                Edge[] edges = state.nodes[current].edges;
                for (int i = 0; i < edges.Length; i++)
                {
                    int neighbor = edges[i].toNode;
                    if (visited[neighbor]) continue;

                    int travelWeight = edges[i].travelWeight;
                    float multiplier = GetPreferenceMultiplier(state, neighbor, askingOwnerId);
                    int scaledCost = System.Math.Max(1, (int)System.Math.Ceiling(travelWeight * multiplier));

                    int newDist = dist[current] + scaledCost;
                    if (newDist < dist[neighbor])
                    {
                        dist[neighbor] = newDist;
                        cameFrom[neighbor] = current;
                    }
                }
            }
            return new int[0];
        }// loop exhausted, target never got visited -> no path

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
            if (node.ownerID != -1) return NodeOwnership.EnemyOwned; // owned by the other player

            int signForAskingPlayer = (askingPlayerID == 0) ? 1 : -1;
            int leaning = node.claimBar * signForAskingPlayer;

            if (leaning > 0) return NodeOwnership.PartiallyOwned;
            if (leaning < 0) return NodeOwnership.EnemyPartiallyOwned;
            return NodeOwnership.Unowned;
        }

        private static float GetPreferenceMultiplier(SimulationState state, int nodeID, int askingPlayerID)
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