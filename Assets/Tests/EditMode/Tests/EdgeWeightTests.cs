using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Covers issue #7: the default/fallback edge weight is sourced from
    /// state.defaultEdgeWeight (itself seeded from BoardConfigData at
    /// SimulationState construction) instead of a hardcoded literal, and the
    /// corrupt movement-path guard in TickMovement recovers deterministically
    /// instead of indexing off the end of movePath.
    /// </summary>
    public class EdgeWeightTests
    {
        [Test]
        public void GetEdgeWeight_NonAdjacentNodes_ReturnsStateDefaultEdgeWeight()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(state);

            // Nodes 0 and 2 are not directly connected (only via node 1 in
            // TestBoardFactory's three-node board), so GetEdgeWeight must fall
            // back to state.defaultEdgeWeight. Set it to a value distinctive
            // from any real edge weight in the test board (all 1) and from
            // the old hardcoded literal (3), to prove the literal is gone.
            const int distinctiveWeight = 99;
            state.defaultEdgeWeight = distinctiveWeight;

            int result = GameSimulation.GetEdgeWeight(state, 0, 2);

            Assert.AreEqual(distinctiveWeight, result);
        }

        [Test]
        public void TickMovement_CorruptEmptyPath_DoesNotThrowAndLeavesMovingState()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(state);

            // Force villager 0 into a corrupt state: Moving with an empty
            // movePath. This violates the "a Moving villager always has a
            // further node ahead on its path" invariant and, before the
            // guard, indexed off the end of movePath (movePath[movePathIndex])
            // and threw an IndexOutOfRangeException -- taking a peer down
            // mid-tick during a lockstep match.
            state.villagers[0].state = VillagerState.Moving;
            state.villagers[0].movePath = new int[0];
            state.villagers[0].movePathIndex = 0;

            Assert.DoesNotThrow(() => GameSimulation.SimulateTick(state));

            // The guard treats a corrupt path as "arrived": villager 0 sits on
            // node 0 (its own, owned Core), so ApplyArrivalState's Core branch
            // routes it to Idle -- either way, it must no longer be Moving.
            Assert.AreNotEqual(VillagerState.Moving, state.villagers[0].state);
        }

        [Test]
        public void ComputeHash_DiffersWhenOnlyDefaultEdgeWeightDiffers()
        {
            GameBalanceData balance = GameBalanceData.Default();

            SimulationState stateA = TestBoardFactory.BuildThreeNodeBoard(balance);
            SimulationState stateB = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateA);
            Assert.IsNotNull(stateB);

            // Otherwise-identical states, differing only in defaultEdgeWeight,
            // must produce different hashes -- proving SimulationStateHasher
            // covers the field and a peer-to-peer config mismatch would be
            // caught as a desync instead of diverging silently.
            stateA.defaultEdgeWeight = 4;
            stateB.defaultEdgeWeight = 12;

            int hashA = SimulationStateHasher.ComputeHash(stateA);
            int hashB = SimulationStateHasher.ComputeHash(stateB);

            Assert.AreNotEqual(hashA, hashB);
        }
    }
}
