using System;
using System.Diagnostics;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    [TestFixture]
    [Category("Performance")]
    public class SimulationPerformanceTests
    {
        private const int Ticks = 200;
        private const int ReplayTicks = 20;
        private const int HashIterations = 1000;
        private const int VillagersPerPlayer = 56;

        // Lead measurements, .NET 8 Debug, 2026-10-02:
        // SimulateTick x200: 325,136 bytes / 98 ms;
        // CopyFrom + 20 replay ticks: 48,216 bytes / 13.9 ms;
        // ComputeHash x1000: 0 bytes / 21.7 ms.
        // Allocation ceilings are roughly 4x measured; time ceilings roughly 20x
        // allow slow shared CI hosts. These are regression budgets, not an SLA.
        // Current code intentionally allocates combat scratch arrays/lists each tick.
        private const long TickAllocationBudget = 1280L * 1024;
        private const long ReplayAllocationBudget = 192L * 1024;
        [SetUp]
        public void WarmHotPaths()
        {
            SimulationState warm = PopulatedBoard();
            var copy = new SimulationState();
            copy.CopyFrom(warm);
            for (int i = 0; i < ReplayTicks; i++) GameSimulation.SimulateTick(copy);
            for (int i = 0; i < 100; i++) SimulationStateHasher.ComputeHash(copy);
        }

        [Test]
        public void PopulatedBoard_TwoHundredTicksStayWithinBudgets()
        {
            SimulationState state = PopulatedBoard();
            int foodBefore = state.players[0].food;
            int combatHPBefore = state.villagers[0].hp;
            Measure("SimulateTick x 200", () => Advance(state, Ticks), TickAllocationBudget, 2000);

            Assert.AreEqual(Ticks, state.tickCount);
            Assert.IsFalse(state.gameOver);
            Assert.AreEqual(28, state.nodes.Length);
            Assert.AreEqual(112, state.villagers.Length);
            Assert.Greater(state.players[0].food, foodBefore, "Production must be active in this workload.");
            Assert.Less(state.villagers[0].hp, combatHPBefore, "Combat must stay active in this workload.");
        }

        [Test]
        public void CopyFromAndTwentyTickReplayStayWithinBudgetsAndMatchReference()
        {
            SimulationState snapshot = PopulatedBoard();
            var expected = new SimulationState();
            expected.CopyFrom(snapshot);
            Advance(expected, ReplayTicks);
            int expectedHash = SimulationStateHasher.ComputeHash(expected);

            // Live has diverged before rollback; CopyFrom must restore the snapshot.
            var live = new SimulationState();
            live.CopyFrom(snapshot);
            Advance(live, ReplayTicks + 5);
            Measure("CopyFrom + 20 replay ticks", () =>
            {
                live.CopyFrom(snapshot);
                Advance(live, ReplayTicks);
            }, ReplayAllocationBudget, 300);

            Assert.AreEqual(ReplayTicks, live.tickCount);
            Assert.IsFalse(live.gameOver);
            Assert.AreEqual(expectedHash, SimulationStateHasher.ComputeHash(live));
            Assert.AreEqual(0, snapshot.tickCount);
        }

        [Test]
        public void ComputeHash_OneThousandCallsAllocateNothingAndStayWithinTimeBudget()
        {
            SimulationState state = PopulatedBoard();
            int expected = SimulationStateHasher.ComputeHash(state);
            int actual = 0;
            Measure("ComputeHash x 1000", () =>
            {
                for (int i = 0; i < HashIterations; i++)
                    actual = SimulationStateHasher.ComputeHash(state);
            }, 0, 500);
            Assert.AreEqual(expected, actual);
        }

        private static void Measure(string operation, Action action, long allocationBudget, int timeBudgetMilliseconds)
        {
            // Delegate, clock, diagnostics and assertions are outside allocation span.
            var watch = new Stopwatch();
            GC.GetAllocatedBytesForCurrentThread();
            watch.Start();
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            watch.Stop();
            TestContext.WriteLine(operation + ": " + allocated + " bytes, " + watch.Elapsed.TotalMilliseconds + " ms");
            Assert.LessOrEqual(allocated, allocationBudget, operation + " managed allocation regression");
            Assert.Less(watch.Elapsed.TotalMilliseconds, timeBudgetMilliseconds, operation + " coarse time regression");
        }

        private static void Advance(SimulationState state, int ticks)
        {
            for (int i = 0; i < ticks; i++) GameSimulation.SimulateTick(state);
        }

        private static SimulationState PopulatedBoard()
        {
            GameBalanceData balance = GameBalanceData.Default();
            // Keep the established stress workload producing from its large stocks.
            // Cap behavior is covered separately by ResourceCapTests.
            balance.foodCap = balance.materialsCap = balance.metalCap = 0;
            BoardConfigData board = BoardConfigData.Default();
            board.startingVillagersPerPlayer = VillagersPerPlayer;
            board.startingFood = board.startingMaterials = board.startingMetal = 100000;
            MatchFactory.Configure(balance, board);
            SimulationState state = MatchFactory.Build(balance, board, null, new PlayerSetup[2]);

            // Stress fixture on the real 4x7 topology: every ordinary node is an
            // owned district except the contested middle. This is deliberately
            // more populated than tick 0; it is a regression workload, not balance.
            for (int n = 0; n < state.nodes.Length; n++)
            {
                if (state.nodes[n].districtType == DistrictType.Core || n == 14) continue;
                SetDistrict(state, n, n < 14 ? 0 : 1, DistrictType.Farm, balance.claimThreshold);
            }
            for (int owner = 0; owner < 2; owner++)
            {
                int home = owner == 0 ? 4 : 20;
                SetDistrict(state, home + 1, owner, DistrictType.Mine, balance.claimThreshold);
                SetDistrict(state, home + 2, owner, DistrictType.Forge, balance.claimThreshold);
            }

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                int home = v.ownerID == 0 ? 4 : 20;
                v.maxHP = v.hp = 1000000; // Keep contested fights alive for all N ticks.
                v.attackDamage = 1;
                v.attackCooldownMax = v.attackCooldownRemaining = 1;
                v.fightPriority = i % 3; // Exercise deterministic equal-priority sorting.
                v.currentNodeID = v.previousNodeID = home;
                switch (i % 8)
                {
                    case 0:
                    case 1:
                        v.currentNodeID = v.previousNodeID = 14;
                        v.state = VillagerState.Fighting;
                        v.suit = SuitType.Warrior;
                        break;
                    case 2:
                        v.state = VillagerState.Working;
                        v.suit = SuitType.Farmer;
                        v.productionTicksMax = v.productionTicksRemaining = 5;
                        break;
                    case 3:
                        v.currentNodeID = v.previousNodeID = home + 1;
                        v.state = VillagerState.Working;
                        v.suit = SuitType.Miner;
                        v.productionTicksMax = v.productionTicksRemaining = 5;
                        break;
                    case 4:
                        v.state = VillagerState.Moving;
                        v.targetNodeID = home + 1;
                        v.movePath = new[] { home, home + 1 };
                        v.moveSpeedTicks = 1000000; // Moving throughout the measured span.
                        break;
                    case 5:
                        v.hp -= 10000; // Exercise normal healing without entering combat.
                        break;
                    case 6:
                        v.state = VillagerState.Dead;
                        v.hp = 0;
                        v.respawnTicksRemaining = 10;
                        break;
                    case 7:
                        v.currentNodeID = v.previousNodeID = home + 2;
                        v.state = VillagerState.Working;
                        v.suit = SuitType.Smelter;
                        v.productionTicksMax = v.productionTicksRemaining = 5;
                        break;
                }
                state.villagers[i] = v;
            }
            return state;
        }

        private static void SetDistrict(SimulationState state, int node, int owner, DistrictType type, int threshold)
        {
            state.nodes[node].ownerID = owner;
            state.nodes[node].claimBar = owner == 0 ? threshold : -threshold;
            state.nodes[node].districtType = state.nodes[node].baseDistrictType = type;
            state.nodes[node].materialAllocation = 1;
        }
    }
}
