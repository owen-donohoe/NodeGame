using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class PaidRespawnTests
    {
        private GameBalanceData balance;
        private SimulationState state;

        [SetUp]
        public void SetUp()
        {
            balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.players[0].food = state.players[1].food = 100;
        }

        private void Pay(int player = 0)
        {
            state.villagers[player].state = VillagerState.Dead;
            CommandProcessor.ProcessCommand(state, new GameCommand
            { type = CommandType.Respawn, playerID = player, villagerID = player });
        }

        [Test]
        public void SuccessfulPaymentsEscalatePerPlayerAndCopyRestoresCounter()
        {
            for (int n = 0; n < 3; n++)
            {
                Assert.AreEqual(n + 1, CommandProcessor.GetRespawnCost(state, 0));
                int before = state.players[0].food;
                Pay();
                Assert.AreEqual(before - (n + 1), state.players[0].food);
                Assert.AreEqual(n + 1, state.players[0].paidRespawns);
                Assert.AreEqual(0, state.players[1].paidRespawns);
            }
            Assert.AreEqual(1, CommandProcessor.GetRespawnCost(state, 1));
            Pay(1);
            Assert.AreEqual(1, state.players[1].paidRespawns);
            var copy = new SimulationState();
            copy.CopyFrom(state);
            int hash = SimulationStateHasher.ComputeHash(copy);
            Pay();
            state.CopyFrom(copy);
            Assert.AreEqual(3, state.players[0].paidRespawns);
            Assert.AreEqual(4, CommandProcessor.GetRespawnCost(state, 0));
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void MultipleCommandsChargeTheNextPriceAndRefusalDoesNotIncrement()
        {
            state.players[0].food = 2;
            state.villagers[0].state = VillagerState.Dead;
            state.villagers[1].ownerID = 0;
            state.villagers[1].state = VillagerState.Dead;
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 1 });
            Assert.AreEqual(1, state.players[0].food);
            Assert.AreEqual(1, state.players[0].paidRespawns);
            Assert.AreEqual(VillagerState.Dead, state.villagers[1].state);
            state.players[0].food = 2;
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 1 });
            Assert.AreEqual(0, state.players[0].food);
            Assert.AreEqual(2, state.players[0].paidRespawns);
        }

        [Test]
        public void InvalidAliveConsumedAndEnemyCommandsDoNotIncrement()
        {
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = -1 });
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].isConsumed = true;
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
            state.villagers[1].state = VillagerState.Dead;
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 1 });
            Assert.AreEqual(0, state.players[0].paidRespawns);
            Assert.AreEqual(100, state.players[0].food);
        }

        [Test]
        public void TimerRespawnDoesNotIncrementOrSpend()
        {
            state.players[0].paidRespawns = 2;
            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].respawnTicksRemaining = 1;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
            Assert.AreEqual(2, state.players[0].paidRespawns);
            Assert.AreEqual(100, state.players[0].food);
        }

        [Test]
        public void InfirmaryDiscountIsAppliedAfterEscalationWithFloorRounding()
        {
            state.nodes[1].districtType = DistrictType.Infirmary; state.nodes[1].districtHealth = 3000;
            state.nodes[1].ownerID = 0;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].state = VillagerState.Working; state.villagers[0].suit = SuitType.Acolyte;
            state.villagers[1].ownerID = 0;
            state.villagers[1].state = VillagerState.Dead;
            state.players[0].paidRespawns = 3;
            Assert.AreEqual(4, CommandProcessor.GetRespawnCost(state, 0)); // 4 - floor(4*20/100)
            CommandProcessor.ProcessCommand(state, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 1 });
            Assert.AreEqual(96, state.players[0].food);
            Assert.AreEqual(4, state.players[0].paidRespawns);
            Assert.AreEqual(4, CommandProcessor.GetRespawnCost(state, 0)); // 5 - floor(5*20/100)
        }

        [TestCase(0, 0, 1)] [TestCase(1, 0, 2)] [TestCase(2, 0, 3)]
        [TestCase(3, 25, 3)] [TestCase(4, 25, 4)] [TestCase(3, 50, 2)]
        [TestCase(3, 100, 1)] [TestCase(3, 150, 1)]
        public void CostUsesEscalationAndPreservesMinimum(int paid, int reduction, int expected)
        {
            Assert.AreEqual(expected, balance.PaidRespawnCost(paid, reduction));
        }

        [Test]
        public void LargeCostsSaturateInsteadOfWrappingOrOverflowingDiscount()
        {
            balance.respawnCostFood = int.MaxValue;
            Assert.AreEqual(int.MaxValue, balance.PaidRespawnCost(int.MaxValue, 25));
            Assert.AreEqual(1, balance.PaidRespawnCost(int.MaxValue, 100));
        }
    }
}
