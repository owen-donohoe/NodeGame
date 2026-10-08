using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class TickOrderTests
    {
        private static int Capture()
        {
            int hash = 0;
            foreach (bool relabel in new[] { false, true })
            {
                var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
                int target = relabel ? 1 : 0, neighbour = relabel ? 0 : 1;
                s.nodes[target].links = new[] { new Link { toNodeID = neighbour, travelWeight = 1 } };
                s.nodes[neighbour].claimBar = 990;
                s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, target, VillagerState.Claiming), CoreRulesFixture.Body(b, 1, 0, neighbour, VillagerState.Claiming) };
                GameSimulation.SimulateTick(s);
                Assert.AreEqual(0, s.nodes[neighbour].ownerID); Assert.AreEqual(1000, s.nodes[neighbour].claimBar);
                Assert.AreEqual(10, s.nodes[target].claimBar, "This tick uses the pre-capture neighbour owner");
                GameSimulation.SimulateTick(s); Assert.AreEqual(22, s.nodes[target].claimBar);
                hash = CoreRulesFixture.Fold(hash, s);
            }
            return hash;
        }
        private static int Breach()
        {
            int hash = 0;
            foreach (int net in new[] { 2, 0 })
            {
                var b = CoreRulesFixture.Balance(200); var s = CoreRulesFixture.Board(b);
                CoreRulesFixture.Neighbours(s, 7, net, 0);
                s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 7, VillagerState.Breaching) }; s.villagers[0].targetNodeID = 7;
                GameSimulation.SimulateTick(s); Assert.AreEqual(net == 2 ? 75 : 50, s.players[1].breachBar);
                Assert.IsFalse(s.villagers[0].isConsumed); hash = CoreRulesFixture.Fold(hash, s);
            }
            var finish = CoreRulesFixture.Balance(200); finish.breachBarMax = 75; var consumed = CoreRulesFixture.Board(finish);
            CoreRulesFixture.Neighbours(consumed, 7, 2, 0); consumed.players[1].breachCount = 2;
            consumed.villagers = new[] { CoreRulesFixture.Body(finish, 0, 0, 7, VillagerState.Breaching) }; consumed.villagers[0].targetNodeID = 7;
            GameSimulation.SimulateTick(consumed); Assert.IsTrue(consumed.villagers[0].isConsumed); Assert.AreEqual(-1, consumed.villagers[0].targetNodeID);
            Assert.AreEqual(3, consumed.players[1].breachCount); Assert.IsTrue(consumed.gameOver); Assert.AreEqual(0, consumed.winnerID);
            hash = CoreRulesFixture.Fold(hash, consumed);
            var decay = CoreRulesFixture.Board(finish); decay.players[1].breachBar = 100;
            GameSimulation.SimulateTick(decay); Assert.AreEqual(93, decay.players[1].breachBar); return CoreRulesFixture.Fold(hash, decay);
        }
        [Test] public void SameTickNeighbourCapture_DoesNotCascade() => Capture();
        [Test] public void SameTickNeighbourCapture_DoesNotCascade_Determinism() => CoreRulesFixture.Determinism(Capture);
        [Test] public void BreachUsesFrontierButNotClaimTempo() => Breach();
        [Test] public void BreachUsesFrontierButNotClaimTempo_Determinism() => CoreRulesFixture.Determinism(Breach);

        [Test]
        public void ClaimingBeforeProduction_NewFarmWorkerAdvancesOnTheClaimTick()
        {
            SimulationState state = ClaimFarm();
            int cycle = GameBalanceData.Default().GetDistrictStats(DistrictType.Farm, 0).productionTicks;

            Assert.AreEqual(0, state.nodes[1].ownerID);
            Assert.AreEqual(VillagerState.Working, state.villagers[0].state);
            Assert.AreEqual(SuitType.Farmer, state.villagers[0].suit);
            Assert.AreEqual(cycle, state.villagers[0].productionTicksMax);
            // Swapping claiming and production leaves the new timer at cycle:
            // the villager is still Idle when the earlier production pass runs.
            Assert.AreEqual(cycle - 1, state.villagers[0].productionTicksRemaining);
            Assert.AreEqual(1, state.tickCount);
        }

        [Test]
        public void ClaimingBeforeProduction_NewFarmWorkerAdvancesOnTheClaimTick_Determinism()
        {
            Assert.AreEqual(SimulationStateHasher.ComputeHash(ClaimFarm()),
                SimulationStateHasher.ComputeHash(ClaimFarm()));
        }

        private static SimulationState ClaimFarm()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            // One tick claims the neutral Farm. The other player stays on its
            // Core, so combat cannot interfere. No commands or extra ticks needed.
            state.nodes[1].districtType = state.nodes[1].baseDistrictType = DistrictType.Farm;
            state.nodes[1].claimBar = balance.claimThreshold - 1;
            state.villagers[0].currentNodeID = state.villagers[0].previousNodeID = 1;

            GameSimulation.SimulateTick(state);
            return state;
        }

        // The remaining adjacent pairs in GameSimulation.SimulateTick cannot be
        // distinguished by a one-tick observable fixture under current rules:
        //
        // Production -> healing: TickProduction writes resources and production
        // timers; TickHealing writes HP. Neither reads the fields the other writes.
        // Both read villager state/consumption and node district/ownership, but
        // neither changes those. The passes commute, including Market alternation.
        //
        // Healing -> respawns: TickHealing skips Dead and consumed villagers.
        // TickRespawns only changes non-consumed Dead villagers, restoring both
        // HP and maxHP to baseHP via ResetToCore. If respawns runs first, those
        // villagers are already at full HP and healing still cannot change them.
        // All other villagers are untouched by respawns. The HP reset therefore
        // makes both orders equivalent, even on a due healing tick or Shrine.
        //
        // Respawns -> win-check: TickWinCondition reads only player breachCount
        // and writes gameOver/winnerID. TickRespawns never changes breachCount or
        // reads either terminal field, so it runs identically in either order.
        // Post-combat resume remains after both passes and sees identical state.
        //
        // Do not add tests that merely show both effects occurred: they would pass
        // with the phases swapped and would falsely claim to pin canonical order.
        // Revisit these pairs if their read/write dependencies change.
    }
}
