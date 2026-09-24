using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Pathfinding, which had no coverage and is the one piece of the
    /// simulation whose inputs are not all in SimulationState.
    ///
    /// Dijkstra here has two tie-breaks and neither is written down: the
    /// frontier scan takes the lowest node index among equal distances, and
    /// relaxation is strict (`newDist &lt; dist[neighbor]`), so the first
    /// predecessor found for an equal-cost route is the one kept. Both are
    /// deterministic today and both are invisible -- a refactor to a priority
    /// queue would keep every path the same length and change which one two
    /// peers walk. That is a desync with no failing assertion anywhere to
    /// announce it, so it gets one here.
    /// </summary>
    public class PathfindingTests
    {
        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        // ===== SHAPE =====

        [Test]
        public void FindPath_StartEqualsEnd_ReturnsTheSingleNode()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            int[] path = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 1, endNode: 1);

            // Length 1, which is below the `path.Length < 2` bar every caller in
            // CommandProcessor applies -- so "go where you already are" is
            // refused by the caller rather than by the search.
            Assert.AreEqual(1, path.Length);
            Assert.AreEqual(1, path[0]);
        }

        [Test]
        public void FindPath_NoRoute_ReturnsAnEmptyArrayRatherThanNull()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Cut node 2 off: it keeps its edge to node 1, but node 1 no longer
            // offers one back, so the search can never reach it. A one-way edge
            // is exactly what a hand-authored board gets wrong.
            state.nodes[1].edges = new Edge[] { new Edge { toNode = 0, travelWeight = 1 } };

            int[] path = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 2);

            Assert.IsNotNull(path);
            Assert.AreEqual(0, path.Length);
        }

        [Test]
        public void Move_ToAnUnreachableNodeLeavesTheVillagerWhereItIs()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[1].edges = new Edge[] { new Edge { toNode = 0, travelWeight = 1 } };

            int before = SimulationStateHasher.ComputeHash(state);

            CommandProcessor.ProcessCommand(state, new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 2,
                issuedOnTick = 0,
                value = 0
            });

            // The empty path is refused before ApplyMove, so the villager is not
            // left Moving along a path it cannot walk.
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
            Assert.AreEqual(before, SimulationStateHasher.ComputeHash(state));
        }

        // ===== TIE-BREAKING =====

        [Test]
        public void FindPath_EqualCostRoutes_TakeTheLowerNodeIndex()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildSquareBoard(balance);

            // 0 to 3 has two routes of identical cost, through node 1 or node 2.
            int[] path = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 3);

            Assert.AreEqual(new int[] { 0, 1, 3 }, path);
        }

        [Test]
        public void FindPath_IsStableAcrossRepeatedCallsOnTheSameState()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildSquareBoard(balance);

            int[] first = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 3);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 3));
            }
        }

        // ===== OWNERSHIP PREFERENCE =====

        /// <summary>
        /// The same square as BuildSquareBoard but with every edge weighted 4.
        ///
        /// The preference multipliers are integer percentages applied with
        /// ceiling division and a floor of 1, so on a weight-1 board 50%, 75%
        /// and 100% all cost exactly 1 and the preference cannot be observed at
        /// all. Weight 4 is the smallest that separates every tier: owned 2,
        /// unowned 4, enemy 8.
        /// </summary>
        private static SimulationState BuildWeightedSquare(GameBalanceData balance)
        {
            SimulationState state = TestBoardFactory.BuildSquareBoard(balance);

            for (int i = 0; i < state.nodes.Length; i++)
            {
                Edge[] edges = state.nodes[i].edges;
                for (int e = 0; e < edges.Length; e++) edges[e].travelWeight = 4;
            }

            return state;
        }

        [Test]
        public void FindPath_PrefersYourOwnTerritoryOverTheShorterIndex()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BuildWeightedSquare(balance);

            // Node 2 belongs to player 0, node 1 is neutral. Both routes are two
            // hops, so only the multiplier can decide -- and it has to beat the
            // lower-index tie-break, which would otherwise pick node 1.
            state.nodes[2].ownerID = 0;
            state.nodes[2].claimBar = balance.claimThreshold;

            int[] path = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 3);

            Assert.AreEqual(new int[] { 0, 2, 3 }, path);
        }

        [Test]
        public void FindPath_AvoidsEnemyTerritoryWhenAnEqualLengthRouteExists()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BuildWeightedSquare(balance);

            // Node 1 is the enemy's, node 2 is neutral. Player 0 takes the long
            // way round even though node 1 wins every tie-break it is in.
            state.nodes[1].ownerID = 1;
            state.nodes[1].claimBar = -balance.claimThreshold;

            int[] path = Pathfinding.FindPath(state, askingOwnerId: 0, startNode: 0, endNode: 3);

            Assert.AreEqual(new int[] { 0, 2, 3 }, path);
        }

        [Test]
        public void GetOwnershipStatus_ReadsTheClaimBarFromTheAskingPlayersSide()
        {
            NodeData node = new NodeData { nodeID = 0, ownerID = -1, claimBar = 500 };

            // One unowned node, leaning toward player 0. The same bar has to read
            // as "mine, partly" to one player and "theirs, partly" to the other,
            // which is the whole reason the sign is flipped per player rather
            // than stored twice.
            Assert.AreEqual(Pathfinding.NodeOwnership.PartiallyOwned,
                            Pathfinding.GetOwnershipStatus(node, 0));
            Assert.AreEqual(Pathfinding.NodeOwnership.EnemyPartiallyOwned,
                            Pathfinding.GetOwnershipStatus(node, 1));

            node.claimBar = 0;
            Assert.AreEqual(Pathfinding.NodeOwnership.Unowned, Pathfinding.GetOwnershipStatus(node, 0));
            Assert.AreEqual(Pathfinding.NodeOwnership.Unowned, Pathfinding.GetOwnershipStatus(node, 1));
        }
    }
}
