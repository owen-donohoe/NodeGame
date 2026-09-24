using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class PathfindingMultiplierTests
    {
        [Test]
        public void Move_ChangesRouteWhenOnlyOwnedMultiplierDiffers()
        {
            SimulationState stateA = BuildRouteBoard();
            SimulationState stateB = BuildRouteBoard();
            stateB.ownedMultiplier = 200;

            GameCommand move = MoveToOppositeCorner();
            CommandProcessor.ProcessCommand(stateA, move);
            CommandProcessor.ProcessCommand(stateB, move);

            // The owned route costs 5 + 20 versus the neutral route's 10 + 20.
            // Raising only the owned multiplier makes that route cost 20 + 20.
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, stateA.villagers[0].movePath);
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, stateB.villagers[0].movePath);

            // Interleaving queries must not let one match's tuning leak into another.
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(stateA, 0, 0, 3));
        }

        [TestCase(50)]
        [TestCase(200)]
        public void Move_WithSameMultiplier_Determinism(int ownedMultiplier)
        {
            SimulationState stateA = BuildRouteBoard();
            SimulationState stateB = BuildRouteBoard();
            stateA.ownedMultiplier = ownedMultiplier;
            stateB.ownedMultiplier = ownedMultiplier;

            GameCommand move = MoveToOppositeCorner();
            CommandProcessor.ProcessCommand(stateA, move);
            CommandProcessor.ProcessCommand(stateB, move);

            Assert.AreEqual(SimulationStateHasher.ComputeHash(stateA),
                            SimulationStateHasher.ComputeHash(stateB));
        }

        [TestCase(nameof(SimulationState.ownedMultiplier))]
        [TestCase(nameof(SimulationState.partiallyOwnedMultiplier))]
        [TestCase(nameof(SimulationState.unownedMultiplier))]
        [TestCase(nameof(SimulationState.enemyPartiallyOwnedMultiplier))]
        [TestCase(nameof(SimulationState.enemyOwnedMultiplier))]
        public void ComputeHash_DiffersWhenOnlyOneMultiplierDiffers(string fieldName)
        {
            SimulationState stateA = BuildRouteBoard();
            SimulationState stateB = BuildRouteBoard();
            int originalHash = SimulationStateHasher.ComputeHash(stateA);
            Assert.AreEqual(originalHash, SimulationStateHasher.ComputeHash(stateB));

            // Change exactly one field before any commands or ticks: the hash must
            // detect the tuning mismatch independently of its effect on movement.
            var field = typeof(SimulationState).GetField(fieldName);
            field.SetValue(stateB, (int)field.GetValue(stateB) + 1);

            Assert.AreNotEqual(originalHash, SimulationStateHasher.ComputeHash(stateB), fieldName);
        }

        private static SimulationState BuildRouteBoard()
        {
            SimulationState state = TestBoardFactory.BuildSquareBoard(GameBalanceData.Default());
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = 10000;

            // Weight 10 keeps percentage differences visible after ceiling division.
            for (int i = 0; i < state.nodes.Length; i++)
                for (int j = 0; j < state.nodes[i].edges.Length; j++)
                    state.nodes[i].edges[j].travelWeight = 10;

            return state;
        }

        private static GameCommand MoveToOppositeCorner()
        {
            return new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 3,
                issuedOnTick = 0
            };
        }
    }
}
