using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class DeterminismBaselineTests
    {
        [Test]
        public void EmptyTick_100Iterations_ProducesDeterministicHash()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            // No commands issued: both villagers stay Idle on their own Cores.
            // healIntervalTicks (30) fires at ticks 30/60/90, but both villagers
            // are already at maxHP so healing is a no-op -- only tickCount changes.
            const int tickCount = 100;

            SimulationState stateA = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateA);
            for (int i = 0; i < tickCount; i++)
            {
                GameSimulation.SimulateTick(stateA);
            }
            int hashA = SimulationStateHasher.ComputeHash(stateA);

            SimulationState stateB = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateB);
            for (int i = 0; i < tickCount; i++)
            {
                GameSimulation.SimulateTick(stateB);
            }
            int hashB = SimulationStateHasher.ComputeHash(stateB);

            Assert.AreEqual(hashA, hashB);
        }

        [Test]
        public void MoveAndCombat_ProducesDeterministicHash()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            // Both villagers move onto the shared neutral node (node 1) from
            // opposite Cores. Each crosses exactly one edge: travelWeight (1) *
            // balance.baseMoveSpeedTicks (4) = 4 ticks to arrive. They start
            // simultaneously and move at the same speed, so both land on node 1
            // on tick 4, at which point TickCombat puts them both into Fighting.
            const int ticksToArrive = 4;

            GameCommand moveVillager0 = new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };
            GameCommand moveVillager1 = new GameCommand
            {
                type = CommandType.Move,
                playerID = 1,
                villagerID = 1,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };

            // Run A: fresh state, same two commands, four ticks.
            SimulationState stateA = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateA);
            CommandProcessor.ProcessCommand(stateA, moveVillager0);
            CommandProcessor.ProcessCommand(stateA, moveVillager1);
            for (int i = 0; i < ticksToArrive; i++)
            {
                GameSimulation.SimulateTick(stateA);
            }
            int hashA = SimulationStateHasher.ComputeHash(stateA);

            // Run B: completely independent fresh state, identical setup and commands.
            SimulationState stateB = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateB);
            CommandProcessor.ProcessCommand(stateB, moveVillager0);
            CommandProcessor.ProcessCommand(stateB, moveVillager1);
            for (int i = 0; i < ticksToArrive; i++)
            {
                GameSimulation.SimulateTick(stateB);
            }
            int hashB = SimulationStateHasher.ComputeHash(stateB);

            Assert.AreEqual(hashA, hashB);
        }
    }
}
