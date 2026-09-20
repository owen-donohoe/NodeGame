using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Claiming and the win check, neither of which had any coverage.
    ///
    /// Claiming is the only phase that changes who owns a node, and the rules
    /// it applies are arithmetic on a shared integer bar -- the exact case
    /// where two peers disagreeing by one costs the match. The win check is
    /// four lines and decides the outcome, including who wins a tie.
    ///
    /// Two of these also pin the canonical tick order from the far side: they
    /// assert an effect that is only possible if movement and combat resolved
    /// before claiming within the same tick. Reordering the phases in
    /// GameSimulation.SimulateTick fails them, which is the point -- nothing
    /// else in the suite notices a reorder.
    ///
    /// Numbers come from GameBalanceData.Default(): baseClaimPerTick 17,
    /// decrementMultiplier 4, claimThreshold 10000, maxClaimersPerNode 4,
    /// breachThreshold 3.
    /// </summary>
    public class ClaimingAndWinTests
    {
        /// <summary>
        /// The neutral connector in TestBoardFactory's three-node board. Its
        /// only neighbours are the two Cores, so no adjacent Watchtower can
        /// scale the claim rate and every number below is the base rate.
        /// </summary>
        private const int NeutralNode = 1;

        /// <summary>
        /// The three-node board with claimerCount player-0 villagers parked on
        /// the neutral node, and player 1's single villager left Idle on its own
        /// Core where it cannot interfere. villagerID stays equal to the array
        /// index, which the combat tiebreakers rely on.
        /// </summary>
        private static SimulationState BoardWithPlayerZeroOnNeutral(GameBalanceData balance, int claimerCount)
        {
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            VillagerData[] villagers = new VillagerData[claimerCount + 1];
            for (int i = 0; i < claimerCount; i++)
                villagers[i] = TestBoardFactory.MakeIdleVillager(i, ownerID: 0, currentNodeID: NeutralNode, balance);

            villagers[claimerCount] = TestBoardFactory.MakeIdleVillager(claimerCount, ownerID: 1, currentNodeID: 2, balance);

            state.villagers = villagers;
            return state;
        }

        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        // ===== RATE =====

        [Test]
        public void Claim_OneClaimerAddsTheBaseRateForOneTick()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, 1);

            // The villager starts Idle on a node it does not own, so
            // UpdateVillagerClaimStates converts it to Claiming at the top of
            // TickClaiming and it claims on this same tick, not the next.
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(VillagerState.Claiming, state.villagers[0].state);
            Assert.AreEqual(balance.baseClaimPerTick, state.nodes[NeutralNode].claimBar);
        }

        [Test]
        public void Claim_RateScalesLinearlyWithClaimers()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, 3);

            GameSimulation.SimulateTick(state);

            Assert.AreEqual(3 * balance.baseClaimPerTick, state.nodes[NeutralNode].claimBar);
        }

        [Test]
        public void Claim_MaxClaimersPerNodeCapsTheRate()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, balance.maxClaimersPerNode + 2);

            GameSimulation.SimulateTick(state);

            // The two villagers over the cap contribute nothing: the cap is
            // applied when a villager is admitted to Claiming, so the surplus
            // stays Idle and the rate is the cap's worth, not the crowd's.
            Assert.AreEqual(balance.maxClaimersPerNode * balance.baseClaimPerTick,
                            state.nodes[NeutralNode].claimBar);
            Assert.AreEqual(VillagerState.Idle, state.villagers[balance.maxClaimersPerNode].state);
        }

        // ===== OWNERSHIP TRANSITIONS =====

        [Test]
        public void Claim_ReachingTheThresholdFlipsTheOwnerButDiscardsTheClampedBar()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, 1);

            // One tick short of the threshold, so the next 17 overshoots it.
            state.nodes[NeutralNode].claimBar = balance.claimThreshold - 1;

            GameSimulation.SimulateTick(state);

            Assert.AreEqual(0, state.nodes[NeutralNode].ownerID);

            // THIS IS PINNING A BUG, not a rule. The bar should read 10000 here
            // and reads 9999 -- the value it had before the tick that won it.
            //
            // GameSimulation.cs:525-530 clamps `node.claimBar` on a local copy
            // of the struct, calls CompleteClaimForPlayer (which writes ownerID
            // straight into state.nodes), and then re-reads the array into the
            // same local to pick up any slot upgrade. The re-read restores the
            // pre-clamp bar, and the write-back at the end of the loop persists
            // it. Lines 560-565 are the same shape for player 1.
            //
            // Both peers run the identical code, so this is not a desync. What
            // it does mean is that a claimed node's bar sits one tick's worth
            // short of full for the rest of the match, and an enemy retaking it
            // starts from a number the previous claimer's last tick happened to
            // leave. The fix is one line -- publish the clamp with
            // `state.nodes[nodeIndex] = node;` before CompleteClaimForPlayer --
            // but it is a Simulation/ change and wants its own review.
            Assert.AreEqual(balance.claimThreshold - 1, state.nodes[NeutralNode].claimBar);
        }

        [Test]
        public void Claim_AgainstAnEnemyNodeUsesTheDecrementMultiplier()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, 1);

            // Owned by player 1, and far enough below zero that one tick cannot
            // cross it -- so what is measured is the rate alone.
            state.nodes[NeutralNode].ownerID = 1;
            state.nodes[NeutralNode].claimBar = -1000;

            GameSimulation.SimulateTick(state);

            int expected = -1000 + (balance.decrementMultiplier * balance.baseClaimPerTick);
            Assert.AreEqual(expected, state.nodes[NeutralNode].claimBar);
            Assert.AreEqual(1, state.nodes[NeutralNode].ownerID);
        }

        [Test]
        public void Claim_CrossingZeroStopsAtNeutralRatherThanRunningOn()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithPlayerZeroOnNeutral(balance, 1);

            // One tick's decrement is 4 * 17 = 68, so from -34 the raw sum is
            // +34. Taking an enemy node is two jobs: neutralise it, then claim
            // it from zero. The overshoot is dropped rather than carried -- if
            // it were carried, a node's cost would depend on the remainder the
            // previous owner happened to leave.
            state.nodes[NeutralNode].ownerID = 1;
            state.nodes[NeutralNode].claimBar = -34;

            GameSimulation.SimulateTick(state);

            Assert.AreEqual(0, state.nodes[NeutralNode].claimBar);
            Assert.AreEqual(-1, state.nodes[NeutralNode].ownerID);
        }

        [Test]
        public void Claim_CoreNodesAreNeverClaimable()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Player 0's villager standing on player 1's Core, and vice versa.
            // Cores are skipped outright by TickClaiming, and
            // UpdateVillagerClaimStates forces anyone standing on one to Idle
            // regardless of who owns it.
            state.villagers[0].currentNodeID = 2;
            state.villagers[0].previousNodeID = 2;
            state.villagers[1].currentNodeID = 0;
            state.villagers[1].previousNodeID = 0;

            GameSimulation.SimulateTick(state);

            Assert.AreEqual(-balance.claimThreshold, state.nodes[2].claimBar);
            Assert.AreEqual(1, state.nodes[2].ownerID);
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state);
        }

        // ===== TICK ORDER, OBSERVED FROM ITS EFFECTS =====

        [Test]
        public void TickOrder_MovementResolvesBeforeClaimingInTheSameTick()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            CommandProcessor.ProcessCommand(state, new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = NeutralNode,
                issuedOnTick = 0,
                value = 0
            });

            // Tick until the villager stops Moving, then read the bar as of that
            // same tick. Written this way rather than against a fixed tick count
            // so it pins the ordering and not the edge arithmetic.
            int arrivalTick = -1;
            for (int tick = 1; tick <= 20 && arrivalTick < 0; tick++)
            {
                GameSimulation.SimulateTick(state);
                if (state.villagers[0].state != VillagerState.Moving) arrivalTick = tick;
            }

            Assert.AreNotEqual(-1, arrivalTick, "villager never arrived");
            Assert.AreEqual(NeutralNode, state.villagers[0].currentNodeID);

            // Claiming ran after movement, so the tick it arrived on is already
            // a claiming tick. With the phases swapped this bar would still be
            // zero here and only move one tick later.
            Assert.AreEqual(balance.baseClaimPerTick, state.nodes[NeutralNode].claimBar);
        }

        [Test]
        public void TickOrder_CombatResolvesBeforeClaimingSoAContestedBarDoesNotMove()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // One villager each, both standing on the neutral node.
            state.villagers[0].currentNodeID = NeutralNode;
            state.villagers[0].previousNodeID = NeutralNode;
            state.villagers[1].currentNodeID = NeutralNode;
            state.villagers[1].previousNodeID = NeutralNode;

            GameSimulation.SimulateTick(state);

            // Combat claims them both first, and TickClaiming only counts
            // villagers in the Claiming state -- so neither side is claiming and
            // the bar is frozen for as long as the fight lasts. A node cannot be
            // taken while it is contested.
            Assert.AreEqual(VillagerState.Fighting, state.villagers[0].state);
            Assert.AreEqual(VillagerState.Fighting, state.villagers[1].state);
            Assert.AreEqual(0, state.nodes[NeutralNode].claimBar);
        }

        // ===== WIN CONDITION =====

        [Test]
        public void Win_BreachThresholdEndsTheMatchForTheOtherPlayer()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.players[1].breachCount = balance.breachThreshold;

            GameSimulation.SimulateTick(state);

            Assert.IsTrue(state.gameOver);
            Assert.AreEqual(0, state.winnerID);
        }

        [Test]
        public void Win_OneShortOfTheThresholdIsNotAWin()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.players[1].breachCount = balance.breachThreshold - 1;

            GameSimulation.SimulateTick(state);

            Assert.IsFalse(state.gameOver);
        }

        [Test]
        public void Win_BothAtTheThresholdIsResolvedByPlayerOrderNotByChance()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.players[0].breachCount = balance.breachThreshold;
            state.players[1].breachCount = balance.breachThreshold;

            GameSimulation.SimulateTick(state);

            // TickWinCondition scans players in index order and returns on the
            // first one at the threshold, so player 0 is read as breached first
            // and player 1 wins. Arbitrary, but it has to be the same arbitrary
            // answer on both machines, which is what this pins.
            Assert.IsTrue(state.gameOver);
            Assert.AreEqual(1, state.winnerID);
        }

        // ===== DETERMINISM COMPANIONS =====

        [Test]
        public void Claim_ReachingTheThresholdFlipsTheOwnerButDiscardsTheClampedBar_Determinism()
        {
            GameBalanceData balance = UseDefaultBalance();

            SimulationState first = BoardWithPlayerZeroOnNeutral(balance, 1);
            SimulationState second = BoardWithPlayerZeroOnNeutral(balance, 1);

            first.nodes[NeutralNode].claimBar = balance.claimThreshold - 1;
            second.nodes[NeutralNode].claimBar = balance.claimThreshold - 1;

            for (int tick = 0; tick < 10; tick++)
            {
                GameSimulation.SimulateTick(first);
                GameSimulation.SimulateTick(second);

                Assert.AreEqual(SimulationStateHasher.ComputeHash(first),
                                SimulationStateHasher.ComputeHash(second),
                                "diverged on tick " + tick);
            }
        }

        [Test]
        public void Claim_CrossingZeroStopsAtNeutralRatherThanRunningOn_Determinism()
        {
            GameBalanceData balance = UseDefaultBalance();

            SimulationState first = BoardWithPlayerZeroOnNeutral(balance, 2);
            SimulationState second = BoardWithPlayerZeroOnNeutral(balance, 2);

            first.nodes[NeutralNode].ownerID = 1;
            first.nodes[NeutralNode].claimBar = -34;
            second.nodes[NeutralNode].ownerID = 1;
            second.nodes[NeutralNode].claimBar = -34;

            for (int tick = 0; tick < 10; tick++)
            {
                GameSimulation.SimulateTick(first);
                GameSimulation.SimulateTick(second);

                Assert.AreEqual(SimulationStateHasher.ComputeHash(first),
                                SimulationStateHasher.ComputeHash(second),
                                "diverged on tick " + tick);
            }
        }
    }
}
