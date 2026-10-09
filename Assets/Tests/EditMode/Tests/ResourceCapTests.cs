using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class ResourceCapTests
    {
        private static SimulationState Worker(GameBalanceData balance, DistrictType district)
        {
            MatchFactory.Configure(balance, BoardFixtures.LandGrid3x3());
            var state = TestBoardFactory.BuildThreeNodeBoard(balance);
            state.nodes[1].districtType = state.nodes[1].baseDistrictType = district;
            state.nodes[1].ownerID = 0;
            state.nodes[1].claimBar = balance.claimThreshold;
            state.nodes[1].materialAllocation = 1;
            state.villagers[0].currentNodeID = state.villagers[0].previousNodeID = 1;
            return state;
        }

        private static void Ticks(SimulationState state, int count)
        {
            for (int i = 0; i < count; i++) GameSimulation.SimulateTick(state);
        }

        [TestCase(DistrictType.Farm)]
        [TestCase(DistrictType.Mine)]
        public void ProductionHitsCapWastesCyclesAndResumesAfterSpending(DistrictType district)
        {
            var b = GameBalanceData.Default();
            var state = Worker(b, district);
            state.players[0].food = state.players[0].materials = 29;
            int cycle = b.GetDistrictStats(district, 0).productionTicks;
            Ticks(state, cycle * 3);
            Assert.AreEqual(district == DistrictType.Farm ? 30 : 29, state.players[0].food);
            Assert.AreEqual(district == DistrictType.Mine ? 30 : 29, state.players[0].materials);
            Assert.AreEqual(cycle, state.villagers[0].productionTicksRemaining, "wasted completion still cycles");
            state.players[0].food--;
            state.players[0].materials--;
            Ticks(state, cycle);
            Assert.AreEqual(district == DistrictType.Farm ? 30 : 28, state.players[0].food);
            Assert.AreEqual(district == DistrictType.Mine ? 30 : 28, state.players[0].materials);
        }

        [Test]
        public void ForgeAtMetalCapKeepsMaterialsAndCyclesUntilThereIsRoom()
        {
            var b = GameBalanceData.Default();
            var state = Worker(b, DistrictType.Forge);
            state.players[0].materials = 5;
            state.players[0].metal = 9;
            int cycle = b.GetDistrictStats(DistrictType.Forge, 0).productionTicks;
            Ticks(state, cycle * 3);
            Assert.AreEqual(10, state.players[0].metal);
            Assert.AreEqual(4, state.players[0].materials, "full metal must not consume materials");
            Assert.AreEqual(cycle, state.villagers[0].productionTicksRemaining);
            state.players[0].metal--;
            Ticks(state, cycle);
            Assert.AreEqual(10, state.players[0].metal);
            Assert.AreEqual(3, state.players[0].materials);
        }

        [TestCase(true)] [TestCase(false)]
        public void StorehouseBanksEvenWhenPoolIsFull(bool foodFull)
        {
            var b = GameBalanceData.Default(); var state = Worker(b, DistrictType.Storehouse);
            // D3 idle presence collects; this test isolates bank production.
            state.villagers[0].currentNodeID = state.villagers[0].previousNodeID = 0;
            state.nodes[1].structureKind = StructureKind.Minion; state.nodes[1].structureHP = b.minionHP;
            state.nodes[1].minionProductionRemaining = 80; state.nodes[1].storehouseInitialised = true;
            state.players[0].food = foodFull ? 30 : 0; state.players[0].materials = foodFull ? 0 : 30;
            Ticks(state, 160);
            Assert.AreEqual(1, state.nodes[1].bankFood); Assert.AreEqual(1, state.nodes[1].bankMaterials);
            Assert.AreEqual(foodFull ? 30 : 0, state.players[0].food);
            Assert.AreEqual(foodFull ? 0 : 30, state.players[0].materials);
            Assert.AreEqual(80, state.nodes[1].minionProductionRemaining);
        }

        [Test]
        public void HistoricalMarketAtFullStocksDoesNotProduce()
        {
            var b = GameBalanceData.Default(); var state = Worker(b, DistrictType.Market);
            state.players[0].food = state.players[0].materials = 30; Ticks(state, 160);
            Assert.AreEqual(30, state.players[0].food); Assert.AreEqual(30, state.players[0].materials);
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
        }

        [TestCase(DistrictType.Farm, 0)]
        [TestCase(DistrictType.Mine, 0)]
        [TestCase(DistrictType.Forge, 0)]
        [TestCase(DistrictType.Farm, -1)]
        [TestCase(DistrictType.Mine, -1)]
        [TestCase(DistrictType.Forge, -1)]
        public void NonpositiveCapsKeepUncappedProduction(DistrictType district, int cap)
        {
            var b = GameBalanceData.Default();
            b.foodCap = b.materialsCap = b.metalCap = cap;
            var state = Worker(b, district);
            state.players[0].food = state.players[0].materials = state.players[0].metal = 100;
            var stats = b.GetDistrictStats(district, 0);
            Ticks(state, stats.productionTicks);
            Assert.AreEqual(district == DistrictType.Farm ? 101 : 100, state.players[0].food);
            Assert.AreEqual(district == DistrictType.Mine ? 101 : district == DistrictType.Forge ? 99 : 100, state.players[0].materials);
            Assert.AreEqual(district == DistrictType.Forge ? 101 : 100, state.players[0].metal);
        }

        [TestCase(30, 30, 10, 30, 30, 10)]
        [TestCase(0, 0, 0, 100, 100, 100)]
        [TestCase(-1, -2, -3, 100, 100, 100)]
        [TestCase(150, 150, 150, 100, 100, 100)]
        public void StartingResourcesRespectEachCap(int foodCap, int materialsCap, int metalCap,
            int food, int materials, int metal)
        {
            var b = GameBalanceData.Default();
            b.foodCap = foodCap; b.materialsCap = materialsCap; b.metalCap = metalCap;
            var board = BoardFixtures.LandGrid3x3();
            board.startingFood = board.startingMaterials = board.startingMetal = 100;
            var state = MatchFactory.Build(b, board, null, null);
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(food, state.players[p].food);
                Assert.AreEqual(materials, state.players[p].materials);
                Assert.AreEqual(metal, state.players[p].metal);
            }
        }

        [Test]
        public void FullStockStillCarriesTempoRemainderIntoNextCycle()
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = new[] { 1 };
            b.tempoProductionPercent = new[] { 200 };
            var state = Worker(b, DistrictType.Farm);
            GameSimulation.SimulateTick(state);
            state.players[0].food = b.foodCap;
            state.villagers[0].productionTicksRemaining = 1;
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(30, state.players[0].food);
            Assert.AreEqual(state.villagers[0].productionTicksMax - 1, state.villagers[0].productionTicksRemaining);
        }
    }
}
