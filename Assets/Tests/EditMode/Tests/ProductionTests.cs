using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// The production phase, and the respawn cost that Sanctuary is meant to
    /// reduce. Neither had coverage of its output -- the existing tests cover
    /// how a villager is assigned to work, not what the work produces.
    ///
    /// Production is where the simulation's integer arithmetic is most
    /// load-bearing: every rate is a tick count counted down to zero, every
    /// conversion is a resource moved one at a time, and a rounding difference
    /// between two peers compounds for the rest of the match.
    ///
    /// Tick counts come from GameBalanceData.Default(): food 30, materials 40,
    /// metal 50, market food 45 then materials 60.
    /// </summary>
    public class ProductionTests
    {
        private const int WorkNode = 1;

        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        /// <summary>
        /// The three-node board with the neutral connector turned into an owned
        /// district of the given type, and player 0's villager standing on it.
        /// UpdateVillagerClaimStates puts the villager to work on the first
        /// tick, so no explicit state is set here.
        /// </summary>
        private static SimulationState BoardWithWorkerOn(GameBalanceData balance, DistrictType district)
        {
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[WorkNode].districtType = district;
            state.nodes[WorkNode].baseDistrictType = district;
            state.nodes[WorkNode].ownerID = 0;
            state.nodes[WorkNode].claimBar = balance.claimThreshold;

            state.villagers[0].currentNodeID = WorkNode;
            state.villagers[0].previousNodeID = WorkNode;

            return state;
        }

        private static void Tick(SimulationState state, int count)
        {
            for (int i = 0; i < count; i++) GameSimulation.SimulateTick(state);
        }

        // ===== FARM AND MINE =====

        [Test]
        public void Production_AFarmWorkerProducesOneFoodPerCycleAndKeepsGoing()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Farm);

            Tick(state, balance.GetDistrictStats(DistrictType.Farm, 0).productionTicks - 1);
            Assert.AreEqual(0, state.players[0].food, "paid out early");

            Tick(state, 1);
            Assert.AreEqual(1, state.players[0].food);
            Assert.AreEqual(SuitType.Farmer, state.villagers[0].suit);

            // The timer resets to productionTicksMax rather than stopping, so
            // the second loaf costs exactly what the first did.
            Tick(state, balance.GetDistrictStats(DistrictType.Farm, 0).productionTicks);
            Assert.AreEqual(2, state.players[0].food);
        }

        [Test]
        public void Production_AMineWorkerProducesMaterialsOnItsOwnClock()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Mine);

            Tick(state, balance.GetDistrictStats(DistrictType.Mine, 0).productionTicks);

            Assert.AreEqual(1, state.players[0].materials);
            Assert.AreEqual(0, state.players[0].food);
            Assert.AreEqual(SuitType.Miner, state.villagers[0].suit);
        }

        // ===== FORGE =====

        [Test]
        public void Production_AForgeWithNoAllocationConsumesNothingAndProducesNothing()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Forge);

            state.players[0].materials = 5;
            state.nodes[WorkNode].materialAllocation = 0;

            Tick(state, balance.GetDistrictStats(DistrictType.Forge, 0).productionTicks * 2);

            // The cycle still runs and still resets -- it just yields nothing.
            // The stock must be untouched: a forge that eats material without
            // returning metal is the kind of leak nobody notices for weeks.
            Assert.AreEqual(5, state.players[0].materials);
            Assert.AreEqual(0, state.players[0].metal);
        }

        [Test]
        public void Production_AForgeWithAllocationTurnsOneMaterialIntoOneMetal()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Forge);

            state.players[0].materials = 5;
            state.nodes[WorkNode].materialAllocation = 1;

            Tick(state, balance.GetDistrictStats(DistrictType.Forge, 0).productionTicks);

            Assert.AreEqual(4, state.players[0].materials);
            Assert.AreEqual(1, state.players[0].metal);
        }

        [Test]
        public void Production_AForgeWithNoMaterialsLeftProducesNothingRatherThanGoingNegative()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Forge);

            state.players[0].materials = 0;
            state.nodes[WorkNode].materialAllocation = 1;

            Tick(state, balance.GetDistrictStats(DistrictType.Forge, 0).productionTicks * 2);

            Assert.AreEqual(0, state.players[0].materials);
            Assert.AreEqual(0, state.players[0].metal);
        }

        // ===== MARKET =====

        [Test]
        public void Production_AMarketAlternatesFoodThenMaterialsOnTwoDifferentClocks()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Market);

            // The market is the one district whose cycle length changes between
            // cycles: it starts on the food clock, and paying out switches it to
            // the longer material clock and back again. The switch is decided by
            // comparing productionTicksMax against the food clock, so a market
            // worker whose timer was reset by anything else resumes on food.
            Tick(state, balance.GetDistrictStats(DistrictType.Market, 0).productionTicks);
            Assert.AreEqual(1, state.players[0].food);
            Assert.AreEqual(0, state.players[0].materials);

            Tick(state, balance.GetDistrictStats(DistrictType.Market, 0).secondaryProductionTicks);
            Assert.AreEqual(1, state.players[0].food);
            Assert.AreEqual(1, state.players[0].materials);

            Tick(state, balance.GetDistrictStats(DistrictType.Market, 0).productionTicks);
            Assert.AreEqual(2, state.players[0].food);
            Assert.AreEqual(1, state.players[0].materials);

            Assert.AreEqual(SuitType.Merchant, state.villagers[0].suit);
        }

        // ===== RESPAWN COST =====

        [Test]
        public void GetRespawnCost_TheSanctuaryReductionIsANoOpAtTheDefaultBalance()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithWorkerOn(balance, DistrictType.Sanctuary);

            Assert.AreEqual(1, balance.respawnCostFood, "this test is about that 1");

            int withoutWorkers = CommandProcessor.GetRespawnCost(state, 0);

            // One tick to let UpdateVillagerClaimStates put the villager to work
            // in the Sanctuary, which is what CountSanctuaryWorkers counts.
            Tick(state, 1);
            Assert.AreEqual(VillagerState.Working, state.villagers[0].state);

            int withOneWorker = CommandProcessor.GetRespawnCost(state, 0);

            // THIS IS PINNING A DEAD FEATURE, not a rule. The reduction is
            // `(baseCost * reductionPercent) / 100` in integers, and baseCost is
            // 1, so one worker computes (1 * 25) / 100 == 0 and four workers
            // compute (1 * 100) / 100 == 1, which the `finalCost < 1` floor
            // takes straight back to 1. At the shipped balance the Sanctuary
            // cost reduction cannot change the cost by any number of workers.
            //
            // Whether that matters depends on what respawnCostFood is meant to
            // become. If it stays 1, the percentage is the wrong shape and a
            // flat per-worker reduction would at least be honest. Sanctuary's
            // other effect -- sanctuaryRespawnBoostPerWorker, which shortens the
            // wait -- is real and is not what this pins.
            Assert.AreEqual(1, withoutWorkers);
            Assert.AreEqual(1, withOneWorker);
        }
    }
}
