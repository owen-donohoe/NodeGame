using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class BreachTempoTests
    {
        private GameBalanceData balance;

        [SetUp]
        public void SetUp()
        {
            balance = GameBalanceData.Default();
            Install();
        }

        private void Install()
        {
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
        }

        private SimulationState Attackers(int count, int defender = 1)
        {
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.players[0].nextBreacherID = state.players[1].nextBreacherID = -1;
            var template = state.villagers[0];
            state.villagers = new VillagerData[count];
            for (int i = 0; i < count; i++)
            {
                var v = template;
                v.villagerID = i;
                v.ownerID = 1 - defender;
                v.currentNodeID = state.players[defender].coreNodeID;
                v.state = VillagerState.Breaching;
                state.villagers[i] = v;
            }
            return state;
        }

        private static void Ticks(SimulationState state, int n)
        {
            for (int i = 0; i < n; i++) GameSimulation.SimulateTick(state);
        }

        private static void Add(SimulationState state, VillagerData villager)
        {
            int old = state.villagers.Length;
            Array.Resize(ref state.villagers, old + 1);
            villager.villagerID = old;
            state.villagers[old] = villager;
        }

        [TestCase(1, 80)] [TestCase(2, 49)] [TestCase(3, 38)] [TestCase(4, 32)] [TestCase(6, 32)]
        public void SwarmCompletesOnExpectedTick(int count, int ticks)
        {
            var state = Attackers(count);
            Ticks(state, ticks - 1);
            Assert.AreEqual(0, state.players[1].breachCount);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[1].breachCount);
            Assert.AreEqual(0, state.players[1].breachBar);
            Assert.IsTrue(state.villagers[0].isConsumed);
            Assert.AreEqual(count == 1 ? -1 : 1, state.players[1].nextBreacherID);
        }

        [Test]
        public void ThreeAttackersFinishAllBreachesIn167Ticks()
        {
            var state = Attackers(3);
            Ticks(state, 166);
            Assert.AreEqual(2, state.players[1].breachCount);
            Assert.IsFalse(state.gameOver);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(3, state.players[1].breachCount);
            Assert.IsTrue(state.gameOver);
            Assert.AreEqual(0, state.winnerID);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ConsumptionUsesFreshSuitHpIdOrder(int ordering)
        {
            var state = Attackers(3);
            if (ordering == 0)
            {
                state.villagers[0].suit = SuitType.Warrior;
                state.villagers[0].hp = 1;
                state.villagers[1].suit = SuitType.Farmer;
                state.villagers[2].suit = SuitType.Warrior;
            }
            else if (ordering == 1) state.villagers[1].hp = 2;
            else
            {
                // Reverse array order: ranking is by ID, never by array encounter.
                var first = state.villagers[0];
                state.villagers[0] = state.villagers[2];
                state.villagers[2] = first;
            }
            int index = ordering == 2 ? 2 : 1;
            int id = state.villagers[index].villagerID;
            state.players[1].nextBreacherID = 999; // Stale cache cannot dictate consumption.
            state.players[1].breachBar = 3999;
            var log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);
            Assert.IsTrue(state.villagers[index].isConsumed);
            Assert.AreNotEqual(id, state.players[1].nextBreacherID);
        }

        [Test]
        public void CandidateCacheMatchesNextTickConsumptionAndRollback()
        {
            var state = Attackers(3);
            state.villagers[1].hp = 2;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[1].nextBreacherID);
            var snapshot = new SimulationState();
            snapshot.CopyFrom(state);
            int hash = SimulationStateHasher.ComputeHash(snapshot);
            int expected = snapshot.players[1].nextBreacherID;
            state.players[1].breachBar = 3999;
            GameSimulation.SimulateTick(state);
            Assert.IsTrue(state.villagers[expected].isConsumed);
            state.CopyFrom(snapshot);
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(state));
            Assert.AreEqual(108, state.players[1].breachBar);
            Assert.AreEqual(expected, state.players[1].nextBreacherID);
        }

        [Test]
        public void ExcessRateStillCompletesAtMostOncePerCorePerTick()
        {
            balance.breachSwarmRate = new[] { int.MaxValue };
            Install();
            var state = Attackers(4);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[1].breachCount);
            Assert.AreEqual(0, state.players[1].breachBar);
        }

        [TestCase(0, 300)] [TestCase(200, 100)] [TestCase(1000, 0)]
        public void NoAttackersDecaysOrKeepsProgress(int decay, int expected)
        {
            balance.breachBarDecayPerTick = decay;
            Install();
            var state = Attackers(0);
            state.players[1].breachBar = 300;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(expected, state.players[1].breachBar);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
        }

        [Test]
        public void MoveFromBreachingLeavesCoreAndDecayStartsImmediately()
        {
            var state = Attackers(1);
            Ticks(state, 5);
            CommandProcessor.ProcessCommand(state, new GameCommand
            { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 1 });
            Assert.AreEqual(VillagerState.Moving, state.villagers[0].state);
            Ticks(state, balance.baseMoveSpeedTicks);
            Assert.AreEqual(1, state.villagers[0].currentNodeID);
            Assert.AreEqual(0, state.players[1].breachBar);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
        }

        [TestCase(false)] [TestCase(true)]
        public void DefenderArrivalOrRespawnInterruptsAndSurvivorResumes(bool respawn)
        {
            var state = Attackers(1);
            Ticks(state, 5);
            var defender = TestBoardFactory.BuildThreeNodeBoard(balance).villagers[1];
            defender.hp = defender.maxHP = 1;
            if (respawn)
            {
                defender.state = VillagerState.Dead;
                defender.respawnTicksRemaining = 1;
            }
            else
            {
                defender.currentNodeID = 1;
                defender.state = VillagerState.Moving;
                defender.movePath = new[] { 1, 2 };
                defender.moveProgress = balance.baseMoveSpeedTicks - 1;
            }
            Add(state, defender);
            GameSimulation.SimulateTick(state);
            if (respawn)
            {
                Assert.AreEqual(300, state.players[1].breachBar);
                Assert.AreEqual(VillagerState.Idle, state.villagers[1].state);
                GameSimulation.SimulateTick(state);
            }
            Assert.AreEqual(VillagerState.Fighting, state.villagers[0].state);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
            state.villagers[0].attackCooldownRemaining = 1;
            state.villagers[0].attackDamage = 100;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Breaching, state.villagers[0].state);
            Assert.AreEqual(0, state.players[1].nextBreacherID); // End-of-tick resume refresh.
            Assert.AreEqual(0, state.players[1].breachCount);
        }

        [Test]
        public void CompletionPrecedesSameTickDefenderRespawn()
        {
            var state = Attackers(1);
            state.players[1].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            var defender = TestBoardFactory.BuildThreeNodeBoard(balance).villagers[1];
            defender.state = VillagerState.Dead;
            defender.respawnTicksRemaining = 1;
            Add(state, defender);
            var log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);
            Assert.AreEqual(1, state.players[1].breachCount);
            Assert.AreEqual(VillagerState.Idle, state.villagers[1].state);
            Assert.AreEqual(TickEventType.Breach, log[0].type);
            Assert.AreEqual(TickEventType.VillagerRespawned, log[1].type);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
        }

        [TestCase(0, 0)] [TestCase(1, 0)]
        [TestCase(0, 1)] [TestCase(1, 1)]
        [TestCase(0, 2)] [TestCase(1, 2)]
        public void DropAloneCannotWinNextBreachDoes(int defender, int breaches)
        {
            var state = Attackers(1, defender);
            state.tickCount = 2399;
            state.players[0].breachCount = state.players[1].breachCount = breaches;
            GameSimulation.SimulateTick(state);
            Assert.IsFalse(state.gameOver);
            state.players[defender].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            GameSimulation.SimulateTick(state);
            Assert.IsTrue(state.gameOver);
            Assert.AreEqual(1 - defender, state.winnerID);
        }

        [TestCase(0)] [TestCase(1)]
        public void SimultaneousLossesCancelUntilNextBreach(int nextDefender)
        {
            var state = Attackers(1);
            var opposite = state.villagers[0];
            opposite.ownerID = 1;
            opposite.currentNodeID = 0;
            Add(state, opposite);
            state.players[0].breachBar = state.players[1].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            state.players[0].breachCount = state.players[1].breachCount = 2;
            GameSimulation.SimulateTick(state);
            Assert.IsFalse(state.gameOver);
            Assert.AreEqual(3, state.players[0].breachCount);
            Assert.AreEqual(3, state.players[1].breachCount);
            GameSimulation.SimulateTick(state);
            Assert.IsFalse(state.gameOver);
            var next = opposite;
            next.ownerID = 1 - nextDefender;
            next.currentNodeID = state.players[nextDefender].coreNodeID;
            next.isConsumed = false;
            next.state = VillagerState.Breaching;
            Add(state, next);
            state.players[nextDefender].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1 - nextDefender, state.winnerID);
        }

        [Test]
        public void BreachFreesSlot_VillageCaptureDoesNotSpawn()
        {
            VillageCaptureAfterBreach();
        }

        [Test]
        public void BreachFreesSlot_VillageCaptureDoesNotSpawn_Determinism()
        {
            int first = SimulationStateHasher.ComputeHash(VillageCaptureAfterBreach());
            Assert.AreEqual(first, SimulationStateHasher.ComputeHash(VillageCaptureAfterBreach()));
        }

        private SimulationState VillageCaptureAfterBreach()
        {
            balance.maxVillagersPerPlayer = 2;
            Install();
            var state = Attackers(1);
            var claimer = state.villagers[0];
            claimer.currentNodeID = 1;
            claimer.state = VillagerState.Claiming;
            Add(state, claimer);
            state.nodes[1].districtType = state.nodes[1].baseDistrictType = DistrictType.Village;
            state.nodes[1].claimBar = balance.claimThreshold - 1;
            state.players[1].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(2, state.villagers.Length);
            Assert.IsTrue(state.villagers[0].isConsumed);
            Assert.AreEqual(0, state.nodes[1].ownerID);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
            Assert.AreEqual(1, NodeActionRules.CountPopulation(state, 0));
            return state;
        }

        [TestCase(0, 1199, 17)] [TestCase(0, 1200, 25)] [TestCase(0, 1800, 34)]
        [TestCase(1, 1200, -25)] [TestCase(1, 1800, -34)]
        public void ClaimTempoStartsInclusively(int owner, int tick, int expected)
        {
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.tickCount = tick - 1;
            state.villagers[owner].currentNodeID = 1;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(expected, state.nodes[1].claimBar);
        }

        [TestCase(DistrictType.Farm)] [TestCase(DistrictType.Mine)]
        [TestCase(DistrictType.Forge)] [TestCase(DistrictType.Market)]
        public void ProductionCarriesNegativeRemainderIntoNextDuration(DistrictType district)
        {
            balance.tempoStageTicks = new[] { 1 };
            balance.tempoClaimPercent = balance.tempoRespawnPercent = new[] { 100 };
            balance.tempoProductionPercent = new[] { 200 };
            Install();
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = district;
            state.nodes[1].ownerID = 0;
            state.nodes[1].materialAllocation = 1;
            state.players[0].materials = 10;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].state = VillagerState.Working;
            int duration = balance.GetDistrictStats(district, 0).productionTicks;
            state.villagers[0].productionTicksMax = duration;
            state.villagers[0].productionTicksRemaining = 1;
            GameSimulation.SimulateTick(state);
            int next = district == DistrictType.Market ? balance.GetDistrictStats(district, 0).secondaryProductionTicks : duration;
            Assert.AreEqual(next, state.villagers[0].productionTicksMax);
            Assert.AreEqual(next - 1, state.villagers[0].productionTicksRemaining);
            Assert.AreEqual(district == DistrictType.Farm || district == DistrictType.Market ? 1 : 0, state.players[0].food);
            Assert.AreEqual(district == DistrictType.Forge ? 1 : 0, state.players[0].metal);
        }

        [Test]
        public void ProductionAndRespawnIntegrateTempoOver600Ticks()
        {
            balance.tempoStageTicks = new[] { 1 };
            balance.tempoClaimPercent = new[] { 100 };
            balance.tempoRespawnPercent = new[] { 125 };
            balance.tempoProductionPercent = new[] { 110 };
            Install();
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Farm;
            state.nodes[1].ownerID = 0;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].state = VillagerState.Working;
            state.villagers[0].productionTicksMax = state.villagers[0].productionTicksRemaining = 30;
            state.villagers[1].state = VillagerState.Dead;
            state.villagers[1].respawnTicksRemaining = 1000;
            Ticks(state, 600);
            Assert.AreEqual(22, state.players[0].food);
            Assert.AreEqual(30, state.villagers[0].productionTicksRemaining);
            Assert.AreEqual(250, state.villagers[1].respawnTicksRemaining);
        }

        [Test]
        public void SanctuaryBoostStaysAdditiveAfterTempo()
        {
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Sanctuary;
            state.nodes[1].ownerID = 0;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].state = VillagerState.Working;
            var dead = state.villagers[0];
            dead.state = VillagerState.Dead;
            dead.respawnTicksRemaining = 1000;
            Add(state, dead);
            state.tickCount = 1799;
            GameSimulation.SimulateTick(state);
            int expected = balance.TimerDecrement(balance.tempoRespawnPercent, 1800) + 1;
            Assert.AreEqual(1000 - expected, state.villagers[2].respawnTicksRemaining);
        }

        [Test]
        public void TempoEventsAreAppendOnlyAndUseExistingValueField()
        {
            var state = Attackers(0);
            var copy = new SimulationState();
            copy.CopyFrom(state);
            state.tickCount = copy.tickCount = 1199;
            var log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);
            GameSimulation.SimulateTick(copy);
            Assert.AreEqual(SimulationStateHasher.ComputeHash(copy), SimulationStateHasher.ComputeHash(state));
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(TickEventType.TempoStage, log[0].type);
            Assert.AreEqual(0, log[0].value);
            log.Clear();
            state.tickCount = 2399;
            GameSimulation.SimulateTick(state, log);
            Assert.AreEqual(TickEventType.SuddenDeath, log[0].type);
            Assert.AreEqual(1, log[0].value);
            log.Clear();
            state.tickCount = 2999;
            GameSimulation.SimulateTick(state, log);
            Assert.AreEqual(0, log.Count, "Default has no second threshold drop.");
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyModePreservesInstantArrivalAndPostCombatBreach(bool postCombat)
        {
            balance.breachBarMax = 0;
            Install();
            var state = Attackers(1);
            state.villagers[0].state = postCombat ? VillagerState.Fighting : VillagerState.Moving;
            if (!postCombat)
            {
                state.villagers[0].currentNodeID = 1;
                state.villagers[0].movePath = new[] { 1, 2 };
                state.villagers[0].moveProgress = balance.baseMoveSpeedTicks - 1;
            }
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[1].breachCount);
            Assert.IsTrue(state.villagers[0].isConsumed);
            Assert.AreEqual(-1, state.players[1].nextBreacherID);
            state.tickCount = 3000;
            state.players[1].breachCount = 2;
            GameSimulation.SimulateTick(state);
            Assert.IsFalse(state.gameOver); // Legacy ignores sudden-death drops.
        }

        [Test]
        public void LegacyNoTempoRetainsVersionOneBaselineHashPaths()
        {
            balance.breachBarMax = 0;
            balance.tempoStageTicks = balance.tempoClaimPercent = balance.tempoRespawnPercent = balance.tempoProductionPercent = null;
            Install();
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            Ticks(state, 100);
            Assert.AreEqual(411123996, SimulationStateHasher.ComputeHash(state));
            state = TestBoardFactory.BuildThreeNodeBoard(balance);
            for (int p = 0; p < 2; p++) CommandProcessor.ProcessCommand(state,
                new GameCommand { type = CommandType.Move, playerID = p, villagerID = p, targetNodeID = 1 });
            Ticks(state, 4);
            Assert.AreEqual(2101726457, SimulationStateHasher.ComputeHash(state));
        }

        [Test]
        public void BreachRateDoesNotScaleWithLateTempo()
        {
            var state = Attackers(1);
            state.tickCount = 1799;
            Ticks(state, 79);
            Assert.AreEqual(0, state.players[1].breachCount);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[1].breachCount);
        }

        [TestCase(1199, 63)]
        [TestCase(1799, 75)]
        public void DefaultLateTempoSlowsFreeRespawns(int tickBeforeDeath, int wait)
        {
            Assert.IsTrue(balance.TempoAndBreachValid(out string reason), reason);
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.tickCount = tickBeforeDeath;
            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].respawnTicksRemaining = balance.respawnTicks;
            Ticks(state, wait - 1);
            Assert.AreEqual(VillagerState.Dead, state.villagers[0].state);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
            Assert.AreEqual(0, state.players[0].paidRespawns);
        }

        [Test]
        public void BalancedFrontierReplacesWatchtowerClaimBoostBeforeTempo()
        {
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.tickCount = 1199;
            state.nodes[0].districtType = DistrictType.Watchtower;
            state.villagers[0].state = VillagerState.Working;
            var claimer = state.villagers[0];
            claimer.currentNodeID = 1;
            claimer.state = VillagerState.Claiming;
            Add(state, claimer);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(25, state.nodes[1].claimBar); // net frontier 0: 17 * 100 / 100, then * 150 / 100
        }

        [Test]
        public void InvalidFeaturesUseLegacyOrNormalRatesWithoutCrashing()
        {
            balance.breachSwarmRate = null;
            balance.tempoStageTicks = new[] { 1, 1 };
            balance.suddenDeathThresholds = null;
            Install();
            var state = Attackers(1);
            state.tickCount = 2999;
            state.villagers[0].state = VillagerState.Fighting;
            var log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);
            Assert.IsTrue(state.villagers[0].isConsumed);
            Assert.AreEqual(1, state.players[1].breachCount);
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(TickEventType.Breach, log[0].type);
        }

        [Test]
        public void MarketCarriesRemainderIntoFoodAfterMaterialCycle()
        {
            balance.tempoStageTicks = new[] { 1 };
            balance.tempoProductionPercent = new[] { 200 };
            Install();
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = DistrictType.Market;
            state.nodes[1].ownerID = 0;
            state.villagers[0].currentNodeID = 1;
            state.villagers[0].state = VillagerState.Working;
            state.villagers[0].productionTicksMax = balance.GetDistrictStats(DistrictType.Market, 0).secondaryProductionTicks;
            state.villagers[0].productionTicksRemaining = 1;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(1, state.players[0].materials);
            Assert.AreEqual(44, state.villagers[0].productionTicksRemaining);
        }
    }
}
