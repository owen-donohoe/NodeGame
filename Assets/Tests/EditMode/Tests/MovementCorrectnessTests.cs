using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class MovementCorrectnessTests
    {
        [Test]
        public void Villager_IssuesMoveCommand_TransitionsToMovingState()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(state);

            // Player 0 orders villager 0 to move from its Core (node 0) to the
            // neutral connector (node 1). Commands are applied before the tick,
            // matching TickRunner's real order: commands first, then SimulateTick.
            GameCommand moveCommand = new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };
            CommandProcessor.ProcessCommand(state, moveCommand);

            // Edge 0->1 has travelWeight = 1, and balance.baseMoveSpeedTicks = 4,
            // so crossing it costs 1 * 4 = 4 ticks of moveProgress. One tick only
            // advances moveProgress from 0 to 1, far short of the 4 needed to
            // arrive -- the villager must still be Moving, not yet at node 1.
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(VillagerState.Moving, state.villagers[0].state);
        }

        [Test]
        public void Villager_IssuesMoveCommand_TransitionsToMovingState_Determinism()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            GameCommand moveCommand = new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };

            // Run A: fresh state, same command, one tick.
            SimulationState stateA = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateA);
            CommandProcessor.ProcessCommand(stateA, moveCommand);
            GameSimulation.SimulateTick(stateA);
            int hashA = SimulationStateHasher.ComputeHash(stateA);

            // Run B: completely independent fresh state, identical setup and command.
            SimulationState stateB = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateB);
            CommandProcessor.ProcessCommand(stateB, moveCommand);
            GameSimulation.SimulateTick(stateB);
            int hashB = SimulationStateHasher.ComputeHash(stateB);

            Assert.AreEqual(hashA, hashB);
        }
    }
}
