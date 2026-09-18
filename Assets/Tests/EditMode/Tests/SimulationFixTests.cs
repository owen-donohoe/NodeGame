using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class SimulationFixTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Respawn_ResetsStatsAndProduction(bool paid)
        {
            SimulationState state = RunRespawn(paid);
            GameBalanceData balance = GameBalanceData.Default();
            VillagerData v = state.villagers[0];
            Assert.AreEqual(VillagerState.Idle, v.state);
            Assert.AreEqual(0, v.currentNodeID);
            Assert.AreEqual(0, v.previousNodeID);
            Assert.AreEqual(balance.baseHP, v.hp);
            Assert.AreEqual(balance.baseHP, v.maxHP);
            Assert.AreEqual(0, v.fightPriority);
            Assert.AreEqual(0, v.productionTicksRemaining);
            Assert.AreEqual(0, v.productionTicksMax);
            Assert.AreEqual(SuitType.None, v.suit);
            Assert.AreEqual(balance.baseAttackDamage, v.attackDamage);
            Assert.AreEqual(balance.baseMoveSpeedTicks, v.moveSpeedTicks);
            Assert.AreEqual(balance.baseAttackCooldownMax, v.attackCooldownMax);
            Assert.AreEqual(balance.baseAttackCooldownMax, v.attackCooldownRemaining);
            Assert.AreEqual(0, v.respawnTicksRemaining);
            Assert.IsFalse(v.hasRampartBonus);
            Assert.AreEqual(paid ? 0 : 1, state.players[0].food);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Respawn_ResetsStatsAndProduction_Determinism(bool paid)
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(RunRespawn(paid)),
                SimulationStateHasher.ComputeHash(RunRespawn(paid)));
        }

        private static SimulationState RunRespawn(bool paid)
        {
            GameBalanceData balance = SetDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            // A dead, previously equipped worker with stale stats and one timer tick left.
            state.players[0].food = 1;
            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].respawnTicksRemaining = 1;
            state.villagers[0].hp = 0;
            state.villagers[0].maxHP = 12;
            state.villagers[0].fightPriority = 7;
            state.villagers[0].productionTicksRemaining = 9;
            state.villagers[0].productionTicksMax = 30;
            state.villagers[0].suit = SuitType.Guardian;
            state.villagers[0].attackDamage = 4;
            state.villagers[0].moveSpeedTicks = 8;
            state.villagers[0].attackCooldownMax = 40;
            state.villagers[0].attackCooldownRemaining = 3;
            if (paid)
                CommandProcessor.ProcessCommand(state, new GameCommand
                { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
            else
                GameSimulation.SimulateTick(state);
            return state;
        }

        [Test]
        public void Rampart_DeathRemovesBonusAcrossRepeatedRespawns()
        {
            RunRampartCycles(true);
        }

        [Test]
        public void Rampart_DeathRemovesBonusAcrossRepeatedRespawns_Determinism()
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(RunRampartCycles(false)),
                SimulationStateHasher.ComputeHash(RunRampartCycles(false)));
        }

        private static SimulationState RunRampartCycles(bool verify)
        {
            GameBalanceData balance = SetDefaultBalance();
            balance.respawnTicks = 2; // Death tick decrements to one; next tick respawns.
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Rampart;
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = balance.claimThreshold;
            state.villagers[1].currentNodeID = 1;
            state.villagers[1].attackDamage = 100;
            state.villagers[1].attackCooldownMax = 1;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Each life walks onto its own Rampart and dies to the waiting enemy.
                CommandProcessor.ProcessCommand(state, new GameCommand
                { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 1 });
                for (int i = 0; i < balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(state);
                if (verify)
                {
                    Assert.AreEqual(VillagerState.Dead, state.villagers[0].state);
                    Assert.AreEqual(balance.baseHP, state.villagers[0].maxHP, "death cycle " + cycle);
                    Assert.IsFalse(state.villagers[0].hasRampartBonus);
                }
                GameSimulation.SimulateTick(state);
                if (verify)
                {
                    Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
                    Assert.AreEqual(balance.baseHP, state.villagers[0].maxHP);
                    Assert.AreEqual(balance.baseHP, state.villagers[0].hp);
                }
            }
            return state;
        }

        [Test]
        public void Rampart_BreachRemovesBonus()
        {
            SimulationState state = RunRampartBreach();
            Assert.IsTrue(state.villagers[0].isConsumed);
            Assert.IsFalse(state.villagers[0].hasRampartBonus);
            Assert.AreEqual(GameBalanceData.Default().baseHP, state.villagers[0].maxHP);
            Assert.AreEqual(1, state.players[1].breachCount);
        }

        [Test]
        public void Rampart_BreachRemovesBonus_Determinism()
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(RunRampartBreach()),
                SimulationStateHasher.ComputeHash(RunRampartBreach()));
        }

        private static SimulationState RunRampartBreach()
        {
            GameBalanceData balance = SetDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Rampart;
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = balance.claimThreshold;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].hasRampartBonus = true;
            state.villagers[0].maxHP += balance.rampartMaxHPBonus;
            state.villagers[0].hp += balance.rampartMaxHPBonus;
            state.villagers[1].state = VillagerState.Dead;
            state.villagers[1].isConsumed = true; // Empty enemy Core allows immediate breach.
            CommandProcessor.ProcessCommand(state, new GameCommand
            { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 2 });
            for (int i = 0; i < balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(state);
            return state;
        }

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
