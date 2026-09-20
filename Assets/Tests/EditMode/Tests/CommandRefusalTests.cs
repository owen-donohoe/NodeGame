using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// CommandProcessor's refusal paths.
    ///
    /// Every guard in CommandProcessor is a silent `return`. That is the right
    /// shape for lockstep -- both peers must refuse the same command for the
    /// same reason, and neither may throw -- but it means a guard that stops
    /// working is invisible. Nothing errors; the two peers simply stop agreeing.
    ///
    /// So these assert on the state hash rather than on fields: a refused
    /// command must leave SimulationStateHasher.ComputeHash exactly where it
    /// was. That catches a guard that half-applies a command as well as one
    /// that applies it outright, and it catches it in the same terms the desync
    /// check uses in a real match.
    ///
    /// The malformed inputs here are not hypothetical. Commands arrive off the
    /// wire from the other machine, and InputSerializer reconstructs them from
    /// bytes -- a truncated or stale packet is a command with a villagerID that
    /// does not exist.
    /// </summary>
    public class CommandRefusalTests
    {
        private const int NeutralNode = 1;

        private static GameBalanceData UseDefaultBalance()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return balance;
        }

        /// <summary>
        /// Processes the command and asserts it changed nothing at all. The
        /// message names the case so a failure reads as "this guard stopped
        /// working" rather than "a hash differed".
        /// </summary>
        private static void AssertRefused(SimulationState state, GameCommand command, string why)
        {
            int before = SimulationStateHasher.ComputeHash(state);

            Assert.DoesNotThrow(() => CommandProcessor.ProcessCommand(state, command), why);

            Assert.AreEqual(before, SimulationStateHasher.ComputeHash(state), why);
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

        // ===== MOVE =====

        [TestCase(-1, TestName = "Move_VillagerIDBelowZero")]
        [TestCase(2, TestName = "Move_VillagerIDOffTheEnd")]
        [TestCase(9999, TestName = "Move_VillagerIDFarOffTheEnd")]
        public void Move_OutOfRangeVillagerIDIsRefusedWithoutThrowing(int villagerID)
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // The board has exactly two villagers, so index 2 is one off the end.
            AssertRefused(state, Move(0, villagerID, NeutralNode), "villagerID " + villagerID);
        }

        [TestCase(-1, TestName = "Move_TargetNodeBelowZero")]
        [TestCase(3, TestName = "Move_TargetNodeOffTheEnd")]
        public void Move_OutOfRangeTargetNodeIsRefusedWithoutThrowing(int targetNodeID)
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            AssertRefused(state, Move(0, 0, targetNodeID), "targetNodeID " + targetNodeID);
        }

        [Test]
        public void Move_APlayerCannotOrderTheOtherPlayersVillager()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Villager 0 belongs to player 0. This is the guard that stops a
            // malicious or corrupted packet from driving the opponent's army.
            AssertRefused(state, Move(1, 0, NeutralNode), "player 1 ordering player 0's villager");
        }

        [Test]
        public void Move_ADeadVillagerCannotBeOrdered()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Dead;

            AssertRefused(state, Move(0, 0, NeutralNode), "dead villager");
        }

        [Test]
        public void Move_AConsumedVillagerCannotBeOrdered()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].isConsumed = true;

            AssertRefused(state, Move(0, 0, NeutralNode), "consumed villager");
        }

        [Test]
        public void Move_ToTheNodeItIsAlreadyStandingOnIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Villager 0 is on node 0. RepathFromNode returns on
            // fromNode == destination, so the villager is not put into Moving
            // with a one-node path -- which TickMovement would then have to
            // treat as corrupt.
            AssertRefused(state, Move(0, 0, 0), "ordered to its own node");
        }

        // ===== SET ALLOCATION =====

        private static GameCommand Allocate(int playerID, int nodeID, int value)
        {
            return new GameCommand
            {
                type = CommandType.SetAllocation,
                playerID = playerID,
                villagerID = 0,
                targetNodeID = nodeID,
                issuedOnTick = 0,
                value = value
            };
        }

        [Test]
        public void SetAllocation_OnANodeTheOtherPlayerOwnsIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[NeutralNode].districtType = DistrictType.Forge;
            state.nodes[NeutralNode].ownerID = 1;

            AssertRefused(state, Allocate(0, NeutralNode, 3), "forge owned by the other player");
        }

        [Test]
        public void SetAllocation_OnANodeThatIsNotAForgeIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[NeutralNode].districtType = DistrictType.Farm;
            state.nodes[NeutralNode].ownerID = 0;

            AssertRefused(state, Allocate(0, NeutralNode, 3), "allocation on a farm");
        }

        [Test]
        public void SetAllocation_NegativeValueIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[NeutralNode].districtType = DistrictType.Forge;
            state.nodes[NeutralNode].ownerID = 0;

            AssertRefused(state, Allocate(0, NeutralNode, -1), "negative allocation");
        }

        [TestCase(-1, TestName = "SetAllocation_NodeIDBelowZero")]
        [TestCase(3, TestName = "SetAllocation_NodeIDOffTheEnd")]
        public void SetAllocation_OutOfRangeNodeIDIsRefusedWithoutThrowing(int nodeID)
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            AssertRefused(state, Allocate(0, nodeID, 3), "nodeID " + nodeID);
        }

        [Test]
        public void SetAllocation_OnYourOwnForgeIsAccepted()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.nodes[NeutralNode].districtType = DistrictType.Forge;
            state.nodes[NeutralNode].ownerID = 0;

            // The one accepting case, present so the refusals above are known to
            // be refusing rather than the command being inert.
            CommandProcessor.ProcessCommand(state, Allocate(0, NeutralNode, 3));

            Assert.AreEqual(3, state.nodes[NeutralNode].materialAllocation);
        }

        // ===== EQUIP =====

        private static GameCommand Equip(int playerID, int villagerID, SuitType suit)
        {
            return new GameCommand
            {
                type = CommandType.Equip,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = 0,
                issuedOnTick = 0,
                value = (int)suit
            };
        }

        [Test]
        public void Equip_AVillagerThatIsNotIdleIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Moving;

            AssertRefused(state, Equip(0, 0, SuitType.Warrior), "equipping a moving villager");
        }

        [Test]
        public void Equip_ANonCombatSuitIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // Production suits are assigned by arrival, never equipped. A command
            // asking for one is malformed, not a request to reassign work.
            AssertRefused(state, Equip(0, 0, SuitType.None), "equipping SuitType.None");
        }

        [Test]
        public void Equip_ASuitThePlayerNeverDraftedIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // The fixture leaves draftedSuits null, which PlayerHasSuitDrafted
            // reads as "drafted nothing". Nothing is spent and no suit is worn.
            state.players[0].food = 9999;
            state.players[0].materials = 9999;

            AssertRefused(state, Equip(0, 0, SuitType.Warrior), "suit not drafted");
        }

        [TestCase(-1, TestName = "Equip_VillagerIDBelowZero")]
        [TestCase(2, TestName = "Equip_VillagerIDOffTheEnd")]
        public void Equip_OutOfRangeVillagerIDIsRefusedWithoutThrowing(int villagerID)
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            AssertRefused(state, Equip(0, villagerID, SuitType.Warrior), "villagerID " + villagerID);
        }

        // ===== RESPAWN =====

        private static GameCommand Respawn(int playerID, int villagerID)
        {
            return new GameCommand
            {
                type = CommandType.Respawn,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = 0,
                issuedOnTick = 0,
                value = 0
            };
        }

        [Test]
        public void Respawn_ALivingVillagerIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.players[0].food = 9999;

            AssertRefused(state, Respawn(0, 0), "respawning a living villager");
        }

        [Test]
        public void Respawn_WithoutTheFoodIsRefusedAndSpendsNothing()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Dead;
            state.players[0].food = CommandProcessor.GetRespawnCost(state, 0) - 1;

            // One short. The cost must not be part-paid: an affordability check
            // that deducts first and refunds on failure is the classic way for
            // two peers to end up with different resource counts.
            AssertRefused(state, Respawn(0, 0), "one food short of the respawn cost");
        }

        [Test]
        public void Respawn_TheOtherPlayersDeadVillagerIsRefused()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            state.villagers[0].state = VillagerState.Dead;
            state.players[1].food = 9999;

            AssertRefused(state, Respawn(1, 0), "player 1 respawning player 0's villager");
        }

        // ===== UNKNOWN =====

        [Test]
        public void ProcessCommand_ANoneTypedCommandDoesNothing()
        {
            GameBalanceData balance = UseDefaultBalance();
            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);

            // CommandType.None is what a zeroed or truncated packet deserializes
            // to. It must fall off the end of the switch, not into Move.
            AssertRefused(state, new GameCommand
            {
                type = CommandType.None,
                playerID = 0,
                villagerID = 0,
                targetNodeID = NeutralNode,
                issuedOnTick = 0,
                value = 0
            }, "CommandType.None");
        }
    }
}
