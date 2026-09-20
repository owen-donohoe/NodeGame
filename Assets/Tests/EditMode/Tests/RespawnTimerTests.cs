using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// The respawn countdown, which is the half of Sanctuary that works.
    ///
    /// SimulationFixTests already covers what a respawn *resets* -- stats,
    /// production, the Rampart bonus. What it does not cover is the clock:
    /// how fast the counter runs, that it is driven by the tick loop rather
    /// than by a command, and that a Sanctuary worker makes it run faster.
    ///
    /// The decrement is `1 + workers * sanctuaryRespawnBoostPerWorker`, applied
    /// once per tick per dead villager. It is plain integer subtraction with no
    /// rounding anywhere, which is exactly why it works where the cost
    /// reduction beside it does not.
    /// </summary>
    public class RespawnTimerTests
    {
        private const int MiddleNode = 1;

        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        /// <summary>
        /// Player 0 with a dead villager waiting at its core, and optionally a
        /// second villager put to work in a Sanctuary on the middle node.
        /// </summary>
        private static SimulationState BoardWithADeadVillager(GameBalanceData balance,
                                                              int respawnTicksRemaining,
                                                              bool withSanctuaryWorker)
        {
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            VillagerData dead = TestBoardFactory.MakeIdleVillager(0, ownerID: 0, currentNodeID: 0, balance);
            dead.state = VillagerState.Dead;
            dead.hp = 0;
            dead.respawnTicksRemaining = respawnTicksRemaining;

            VillagerData enemy = TestBoardFactory.MakeIdleVillager(1, ownerID: 1, currentNodeID: 2, balance);

            if (!withSanctuaryWorker)
            {
                state.villagers = new VillagerData[] { dead, enemy };
                return state;
            }

            state.nodes[MiddleNode].districtType = DistrictType.Sanctuary;
            state.nodes[MiddleNode].baseDistrictType = DistrictType.Sanctuary;
            state.nodes[MiddleNode].ownerID = 0;
            state.nodes[MiddleNode].claimBar = balance.claimThreshold;

            VillagerData acolyte = TestBoardFactory.MakeIdleVillager(2, ownerID: 0, currentNodeID: MiddleNode, balance);

            state.villagers = new VillagerData[] { dead, enemy, acolyte };
            return state;
        }

        [Test]
        public void Respawn_CountsDownOneTickAtATimeWithoutACommand()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithADeadVillager(balance, 4, withSanctuaryWorker: false);

            GameSimulation.SimulateTick(state);
            Assert.AreEqual(3, state.villagers[0].respawnTicksRemaining);

            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Dead, state.villagers[0].state, "came back early");

            // The countdown is a property of the tick loop, not of the Respawn
            // command -- that command is the paid, early one. Reaching zero
            // brings the villager back on its own.
            GameSimulation.SimulateTick(state);
            Assert.AreNotEqual(VillagerState.Dead, state.villagers[0].state);
            Assert.AreEqual(state.players[0].coreNodeID, state.villagers[0].currentNodeID);
            Assert.AreEqual(balance.baseHP, state.villagers[0].hp);
        }

        [Test]
        public void Respawn_ASanctuaryWorkerMakesTheCounterRunFaster()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithADeadVillager(balance, 4, withSanctuaryWorker: true);

            // One Acolyte means the decrement is 1 + 1 = 2, so the same four
            // ticks of waiting are served in two. The Sanctuary worker is put to
            // work by UpdateVillagerClaimStates inside the first tick, which
            // runs before TickRespawns -- so the boost applies from tick one and
            // not from tick two.
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Working, state.villagers[2].state);
            Assert.AreEqual(2, state.villagers[0].respawnTicksRemaining);

            GameSimulation.SimulateTick(state);
            Assert.AreNotEqual(VillagerState.Dead, state.villagers[0].state);
        }

        [Test]
        public void Respawn_AConsumedVillagerNeverComesBack()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = BoardWithADeadVillager(balance, 1, withSanctuaryWorker: false);

            state.villagers[0].isConsumed = true;

            for (int i = 0; i < 10; i++) GameSimulation.SimulateTick(state);

            // Consumed is the permanent version of dead -- the villager was
            // spent, not killed. Its counter must not run at all, or a spent
            // villager would walk back out of the core after the respawn time.
            Assert.AreEqual(VillagerState.Dead, state.villagers[0].state);
            Assert.AreEqual(1, state.villagers[0].respawnTicksRemaining);
        }
    }
}
