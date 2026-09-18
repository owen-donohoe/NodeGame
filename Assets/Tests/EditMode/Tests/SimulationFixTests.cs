using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class SimulationFixTests
    {
        [TestCase(20, 4, 3)]
        [TestCase(30, 4, 4)]
        public void Healing_UsesEachVillagersOwnInterval(int tick, int shrineHP, int normalHP)
        {
            SimulationState state = RunHealing(tick);
            Assert.AreEqual(shrineHP, state.villagers[0].hp);
            Assert.AreEqual(normalHP, state.villagers[1].hp);
        }

        [TestCase(20)]
        [TestCase(30)]
        public void Healing_UsesEachVillagersOwnInterval_Determinism(int tick)
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(RunHealing(tick)),
                SimulationStateHasher.ComputeHash(RunHealing(tick)));
        }

        private static SimulationState RunHealing(int tick)
        {
            GameBalanceData balance = SetDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Shrine;
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = balance.claimThreshold;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].hp = 3;
            state.villagers[1].hp = 3;
            // No commands: wait for the first Shrine interval, then the first normal one.
            for (int i = 0; i < tick; i++) GameSimulation.SimulateTick(state);
            return state;
        }

        private static GameBalanceData SetDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        [TestCase(DistrictType.Farm, SuitType.Farmer, 30)]
        [TestCase(DistrictType.Mine, SuitType.Miner, 40)]
        [TestCase(DistrictType.Forge, SuitType.Smelter, 50)]
        [TestCase(DistrictType.Market, SuitType.Merchant, 45)]
        [TestCase(DistrictType.Sanctuary, SuitType.Acolyte, 0)]
        [TestCase(DistrictType.Watchtower, SuitType.Watcher, 0)]
        public void Arrival_AssignsProductionSuitAndHonorsWorkerCap(
            DistrictType district, SuitType suit, int ticks)
        {
            foreach (bool atCap in new[] { false, true })
            {
                SimulationState state = RunArrival(district, atCap);
                VillagerData arrived = state.villagers[0];
                Assert.AreEqual(1, arrived.currentNodeID);
                Assert.AreEqual(suit, arrived.suit);
                Assert.AreEqual(atCap ? VillagerState.Idle : VillagerState.Working, arrived.state);
                Assert.AreEqual(atCap ? 0 : ticks, arrived.productionTicksMax);
                // Production runs after arrival, except passive Sanctuary/Watchtower work.
                Assert.AreEqual(atCap || ticks == 0 ? 0 : ticks - 1, arrived.productionTicksRemaining);
            }
        }

        [TestCase(DistrictType.Farm)]
        [TestCase(DistrictType.Mine)]
        [TestCase(DistrictType.Forge)]
        [TestCase(DistrictType.Market)]
        [TestCase(DistrictType.Sanctuary)]
        [TestCase(DistrictType.Watchtower)]
        public void Arrival_AssignsProductionSuitAndHonorsWorkerCap_Determinism(DistrictType district)
        {
            foreach (bool atCap in new[] { false, true })
                Assert.AreEqual(SimulationStateHasher.ComputeHash(RunArrival(district, atCap)),
                    SimulationStateHasher.ComputeHash(RunArrival(district, atCap)));
        }

        private static SimulationState RunArrival(DistrictType district, bool atCap)
        {
            GameBalanceData balance = SetDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = district;
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = balance.claimThreshold;
            state.nodes[1].materialAllocation = 10; // Keep Forge production active.
            state.players[0].materials = 10;
            if (atCap)
            {
                System.Array.Resize(ref state.villagers, 2 + balance.maxWorkersPerNode);
                for (int i = 2; i < state.villagers.Length; i++)
                {
                    state.villagers[i] = TestBoardFactory.MakeIdleVillager(i, 0, 1, balance);
                    state.villagers[i].state = VillagerState.Working;
                    state.villagers[i].productionTicksMax = 100;
                    state.villagers[i].productionTicksRemaining = 100;
                }
            }
            // One edge at four ticks; observe the arrival tick, before another work tick.
            CommandProcessor.ProcessCommand(state, new GameCommand
            {
                type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 1
            });
            for (int i = 0; i < balance.baseMoveSpeedTicks; i++)
                GameSimulation.SimulateTick(state);
            return state;
        }

        [Test]
        public void Combat_AssignsRoundRobinByPriorityThenIDAcrossTwoNodes()
        {
            SimulationState state = RunCombatTargets();
            // Attackers stay in ID order; targets use priority descending, ID ascending.
            int[] expected = { 4, 2, 9, 6, 8, 10, 5, 6, 1, 0, 7, 10 };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(VillagerState.Fighting, state.villagers[i].state);
                Assert.AreEqual(expected[i], state.villagers[i].combatTargetID, "villager " + i);
            }
            Assert.AreEqual(VillagerState.Dead, state.villagers[12].state);
            Assert.AreEqual(-1, state.villagers[12].combatTargetID);
            Assert.AreEqual(-1, state.villagers[13].combatTargetID);
        }

        [Test]
        public void Combat_AssignsRoundRobinByPriorityThenIDAcrossTwoNodes_Determinism()
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(RunCombatTargets()),
                SimulationStateHasher.ComputeHash(RunCombatTargets()));
        }

        private static SimulationState RunCombatTargets()
        {
            GameBalanceData balance = SetDefaultBalance();
            SimulationState state = TestBoardFactory.BuildSquareBoard(balance);
            // Interleaved IDs expose grouping/order mistakes; each node has unequal sides.
            int[] nodes = { 1, 1, 1, 2, 1, 2, 2, 2, 1, 1, 2, 2, 1, 2 };
            int[] owners = { 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0 };
            int[] priorities = { 0, 0, 2, 0, 2, 1, 1, 1, 2, 2, 0, 0, 99, 99 };
            state.villagers = new VillagerData[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                state.villagers[i] = TestBoardFactory.MakeIdleVillager(i, owners[i], nodes[i], balance);
                state.villagers[i].fightPriority = priorities[i];
            }
            state.villagers[12].state = VillagerState.Dead;
            state.villagers[12].respawnTicksRemaining = 50;
            state.villagers[13].isConsumed = true;
            // No commands: one tick detects both fights and assigns without dealing damage.
            GameSimulation.SimulateTick(state);
            return state;
        }
    }
}
