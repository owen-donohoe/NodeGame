using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// TickEventLog: what a tick reports it did.
    ///
    /// The log is output only, so two things are tested. That each moment is
    /// reported once, where it happened, with the right player -- and that
    /// recording changes nothing, which is the property that lets the log sit
    /// beside the simulation without joining the determinism contract.
    ///
    /// Numbers come from GameBalanceData.Default(): baseHP 5, baseAttackDamage 1,
    /// baseAttackCooldownMax 20, baseMoveSpeedTicks 4, baseClaimPerTick 17,
    /// decrementMultiplier 4, claimThreshold 10000, respawnTicks 50,
    /// respawnCostFood 1.
    /// </summary>
    public class TickEventTests
    {
        private const int P0Core = 0;
        private const int NeutralNode = 1;
        private const int P1Core = 2;

        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        private static GameCommand Move(int playerID, int villagerID, int targetNodeID)
        {
            return new GameCommand
            {
                type = CommandType.Move,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = targetNodeID,
                issuedOnTick = 0,
                value = 0
            };
        }

        private static int CountOf(TickEventLog log, TickEventType type)
        {
            int count = 0;
            for (int i = 0; i < log.Count; i++)
                if (log[i].type == type) count++;
            return count;
        }

        private static TickEvent FirstOf(TickEventLog log, TickEventType type)
        {
            for (int i = 0; i < log.Count; i++)
                if (log[i].type == type) return log[i];

            Assert.Fail("No " + type + " event was recorded.");
            return default;
        }

        // ===== COMBAT =====

        [Test]
        public void CombatStarted_ReportedOnceForTheNodeWhereTheFightBegins()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            TickEventLog log = new TickEventLog();

            // Both villagers walk onto the neutral node from opposite Cores and
            // arrive together on tick 4 (one edge, weight 1, speed 4).
            CommandProcessor.ProcessCommand(state, Move(0, 0, NeutralNode));
            CommandProcessor.ProcessCommand(state, Move(1, 1, NeutralNode));

            int started = 0;
            for (int t = 0; t < 4; t++)
            {
                log.Clear();
                GameSimulation.SimulateTick(state, log);
                started += CountOf(log, TickEventType.CombatStarted);
            }

            Assert.AreEqual(1, started);
            Assert.AreEqual(NeutralNode, FirstOf(log, TickEventType.CombatStarted).nodeID);
        }

        [Test]
        public void CombatStarted_NotRepeatedWhileTheFightGoesOn()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            TickEventLog log = new TickEventLog();

            CommandProcessor.ProcessCommand(state, Move(0, 0, NeutralNode));
            CommandProcessor.ProcessCommand(state, Move(1, 1, NeutralNode));
            for (int t = 0; t < 4; t++) GameSimulation.SimulateTick(state);

            // Ten more ticks of the same fight: first blows land at tick 23, so
            // nobody has died and the fight is simply continuing.
            int started = 0;
            for (int t = 0; t < 10; t++)
            {
                log.Clear();
                GameSimulation.SimulateTick(state, log);
                started += CountOf(log, TickEventType.CombatStarted);
            }

            Assert.AreEqual(0, started);
        }

        [Test]
        public void CombatStarted_NotRepeatedWhenAFighterJoins()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // A second player-0 villager held back on its Core.
            state.villagers = new VillagerData[]
            {
                state.villagers[0],
                state.villagers[1],
                TestBoardFactory.MakeIdleVillager(2, ownerID: 0, currentNodeID: P0Core, balance)
            };

            TickEventLog log = new TickEventLog();

            CommandProcessor.ProcessCommand(state, Move(0, 0, NeutralNode));
            CommandProcessor.ProcessCommand(state, Move(1, 1, NeutralNode));
            for (int t = 0; t < 4; t++) GameSimulation.SimulateTick(state);
            Assert.AreEqual(VillagerState.Fighting, state.villagers[0].state, "setup: the fight has begun");

            // It walks into the fight already under way, arriving on tick 8.
            CommandProcessor.ProcessCommand(state, Move(0, 2, NeutralNode));

            int started = 0;
            for (int t = 0; t < 4; t++)
            {
                log.Clear();
                GameSimulation.SimulateTick(state, log);
                started += CountOf(log, TickEventType.CombatStarted);
            }

            Assert.AreEqual(VillagerState.Fighting, state.villagers[2].state, "setup: the newcomer joined");
            Assert.AreEqual(0, started);
        }

        [Test]
        public void VillagerDied_CarriesTheNodeItFellOnAndItsOwner()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Both already standing on the neutral node. Player 0's villager has
            // one HP left, and player 1's swings on the first combat tick: Phase
            // A arms its cooldown at attackCooldownMax (1), Phase C takes it to
            // zero and strikes, Phase D removes the fallen.
            VillagerData weak = TestBoardFactory.MakeIdleVillager(0, ownerID: 0, currentNodeID: NeutralNode, balance);
            weak.hp = 1;
            VillagerData quick = TestBoardFactory.MakeIdleVillager(1, ownerID: 1, currentNodeID: NeutralNode, balance);
            quick.attackCooldownMax = 1;
            state.villagers = new VillagerData[] { weak, quick };

            TickEventLog log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);

            Assert.AreEqual(1, CountOf(log, TickEventType.VillagerDied));

            TickEvent died = FirstOf(log, TickEventType.VillagerDied);
            Assert.AreEqual(0, died.villagerID);
            Assert.AreEqual(NeutralNode, died.nodeID);
            Assert.AreEqual(0, died.playerID);
        }

        // ===== RESPAWNS =====

        [Test]
        public void VillagerRespawned_ByTheTimerIsUnpaidAndLandsOnTheCore()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].hp = 0;
            state.villagers[0].respawnTicksRemaining = 1;

            TickEventLog log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);

            TickEvent respawned = FirstOf(log, TickEventType.VillagerRespawned);
            Assert.AreEqual(0, respawned.villagerID);
            Assert.AreEqual(0, respawned.playerID);
            Assert.AreEqual(P0Core, respawned.nodeID);
            Assert.AreEqual(0, respawned.value, "a timer respawn is not paid");
        }

        [Test]
        public void VillagerRespawned_ByThePaidCommandIsReportedFromCommandProcessor()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Dead;
            state.villagers[0].hp = 0;
            state.villagers[0].respawnTicksRemaining = balance.respawnTicks;
            state.players[0].food = balance.respawnCostFood;

            // A paid respawn never reaches SimulateTick: CommandProcessor brings
            // the villager back itself. It has to reach the log from there.
            TickEventLog log = new TickEventLog();
            CommandProcessor.ProcessCommand(state, new GameCommand
            {
                type = CommandType.Respawn,
                playerID = 0,
                villagerID = 0,
                targetNodeID = -1,
                issuedOnTick = 0,
                value = 0
            }, log);

            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state, "setup: the respawn was accepted");

            TickEvent respawned = FirstOf(log, TickEventType.VillagerRespawned);
            Assert.AreEqual(0, respawned.villagerID);
            Assert.AreEqual(1, respawned.value, "a command respawn is paid");
        }

        // ===== CLAIMING =====

        [Test]
        public void NodeNeutralised_NamesWhoLostItAndWhoPushed()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Player 0 owns the neutral node by the barest margin, and player 1
            // stands on it. Pushing against an owner runs at decrementMultiplier
            // times the base rate (68), so one tick takes the bar through zero.
            state.nodes[NeutralNode].ownerID = 0;
            state.nodes[NeutralNode].claimBar = 1;
            state.villagers[1].currentNodeID = NeutralNode;
            state.villagers[1].previousNodeID = NeutralNode;

            TickEventLog log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);

            Assert.AreEqual(-1, state.nodes[NeutralNode].ownerID, "setup: the node went neutral");

            TickEvent neutralised = FirstOf(log, TickEventType.NodeNeutralised);
            Assert.AreEqual(NeutralNode, neutralised.nodeID);
            Assert.AreEqual(0, neutralised.playerID, "who lost it");
            Assert.AreEqual(1, neutralised.value, "who pushed");
        }

        [Test]
        public void NodeClaimed_NamesTheNewOwnerAndTheOneBefore()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // One tick short of the threshold, with player 0 standing on it.
            state.nodes[NeutralNode].claimBar = balance.claimThreshold - 1;
            state.villagers[0].currentNodeID = NeutralNode;
            state.villagers[0].previousNodeID = NeutralNode;

            TickEventLog log = new TickEventLog();
            GameSimulation.SimulateTick(state, log);

            TickEvent claimed = FirstOf(log, TickEventType.NodeClaimed);
            Assert.AreEqual(NeutralNode, claimed.nodeID);
            Assert.AreEqual(0, claimed.playerID);
            Assert.AreEqual(-1, claimed.value, "it was unowned before");
        }

        // ===== BREACH =====

        [Test]
        public void Breach_NamesTheDefender()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Player 1's only villager is dead and far from coming back, so its
            // Core is undefended when player 0 walks in on tick 4.
            state.villagers[1].state = VillagerState.Dead;
            state.villagers[1].hp = 0;
            state.villagers[1].respawnTicksRemaining = balance.respawnTicks;
            state.villagers[0].currentNodeID = NeutralNode;
            state.villagers[0].previousNodeID = NeutralNode;

            CommandProcessor.ProcessCommand(state, Move(0, 0, P1Core));

            TickEventLog log = new TickEventLog();
            int breaches = 0;
            for (int t = 0; t < 4; t++)
            {
                log.Clear();
                GameSimulation.SimulateTick(state, log);
                breaches += CountOf(log, TickEventType.Breach);
            }

            Assert.AreEqual(1, breaches);
            Assert.AreEqual(1, state.players[1].breachCount, "setup: the breach happened");

            TickEvent breach = FirstOf(log, TickEventType.Breach);
            Assert.AreEqual(P1Core, breach.nodeID);
            Assert.AreEqual(0, breach.villagerID);
            Assert.AreEqual(1, breach.playerID, "the defender");
        }

        // ===== RECORDING CHANGES NOTHING =====

        /// <summary>
        /// One scripted match run twice, once recording and once not. Movement,
        /// a fight, two deaths, a paid respawn, a timer respawn and a claim all
        /// happen, so every step that writes to the log is exercised. The two
        /// runs must agree on the hash at every tick, not only the last: a
        /// divergence that later healed would be missed by the final hash alone.
        /// </summary>
        [Test]
        public void RecordingEvents_LeavesEveryTickHashUnchanged_Determinism()
        {
            GameBalanceData balance = UseDefaultBalance();

            SimulationState recorded = TestBoardFactory.BuildThreeNodeBoard(balance);
            SimulationState plain = TestBoardFactory.BuildThreeNodeBoard(balance);
            recorded.players[1].food = balance.respawnCostFood;
            plain.players[1].food = balance.respawnCostFood;

            TickEventLog log = new TickEventLog();
            int[] seen = new int[6];

            const int ticks = 900;
            for (int t = 0; t < ticks; t++)
            {
                log.Clear();

                // Tick 0: both walk into each other on the neutral node; both
                // fall together at tick 103. Tick 110: player 1 pays to bring
                // theirs back. Tick 200: player 0, back by timer, walks out
                // again and claims the node unopposed (~589 ticks at 17).
                if (t == 0)
                {
                    Apply(recorded, plain, log, Move(0, 0, NeutralNode));
                    Apply(recorded, plain, log, Move(1, 1, NeutralNode));
                }
                else if (t == 110)
                {
                    Apply(recorded, plain, log, new GameCommand
                    {
                        type = CommandType.Respawn, playerID = 1, villagerID = 1,
                        targetNodeID = -1, issuedOnTick = t, value = 0
                    });
                }
                else if (t == 200)
                {
                    Apply(recorded, plain, log, Move(0, 0, NeutralNode));
                }

                GameSimulation.SimulateTick(recorded, log);
                GameSimulation.SimulateTick(plain);

                for (int i = 0; i < log.Count; i++) seen[(int)log[i].type]++;

                Assert.AreEqual(SimulationStateHasher.ComputeHash(plain),
                                SimulationStateHasher.ComputeHash(recorded),
                                "recording changed the state at tick " + (t + 1));
            }

            // The script did what it says, or the comparison proved little.
            Assert.AreEqual(1, seen[(int)TickEventType.CombatStarted], "CombatStarted");
            Assert.AreEqual(2, seen[(int)TickEventType.VillagerDied], "VillagerDied");
            Assert.AreEqual(2, seen[(int)TickEventType.VillagerRespawned], "VillagerRespawned");
            Assert.AreEqual(1, seen[(int)TickEventType.NodeClaimed], "NodeClaimed");
        }

        private static void Apply(SimulationState recorded, SimulationState plain,
                                  TickEventLog log, GameCommand command)
        {
            CommandProcessor.ProcessCommand(recorded, command, log);
            CommandProcessor.ProcessCommand(plain, command);
        }
    }
}
