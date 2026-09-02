using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class MovementCorrectnessTests
    {
        [Test]
        public void Villager_IssuesMoveCommand_TransitionsToMovingState()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState state = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(state);

            // Player 0 orders villager 0 to move from its Core (node 0) to the
            // neutral connector (node 1). Commands are applied before the tick,
            // matching TickRunner's real order: commands first, then SimulateTick.
            GameCommand moveCommand = new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };
            CommandProcessor.ProcessCommand(state, moveCommand);

            // Edge 0->1 has travelWeight = 1, and balance.baseMoveSpeedTicks = 4,
            // so crossing it costs 1 * 4 = 4 ticks of moveProgress. One tick only
            // advances moveProgress from 0 to 1, far short of the 4 needed to
            // arrive -- the villager must still be Moving, not yet at node 1.
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(VillagerState.Moving, state.villagers[0].state);
        }

        [Test]
        public void Villager_IssuesMoveCommand_TransitionsToMovingState_Determinism()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            GameCommand moveCommand = new GameCommand
            {
                type = CommandType.Move,
                playerID = 0,
                villagerID = 0,
                targetNodeID = 1,
                issuedOnTick = 0,
                value = 0
            };

            // Run A: fresh state, same command, one tick.
            SimulationState stateA = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateA);
            CommandProcessor.ProcessCommand(stateA, moveCommand);
            GameSimulation.SimulateTick(stateA);
            int hashA = SimulationStateHasher.ComputeHash(stateA);

            // Run B: completely independent fresh state, identical setup and command.
            SimulationState stateB = TestBoardFactory.BuildThreeNodeBoard(balance);
            Assert.IsNotNull(stateB);
            CommandProcessor.ProcessCommand(stateB, moveCommand);
            GameSimulation.SimulateTick(stateB);
            int hashB = SimulationStateHasher.ComputeHash(stateB);

            Assert.AreEqual(hashA, hashB);
        }

        // ===== RETARGETING A VILLAGER ALREADY IN TRANSIT =====
        //
        // A villager crossing an edge has no position of its own: currentNodeID
        // is the node it last stood on, and moveProgress counts ticks along the
        // leg movePath[movePathIndex] -> movePath[movePathIndex + 1]. Retargeting
        // used to re-path from currentNodeID and zero moveProgress, which rewound
        // the villager onto that node.
        //
        // Every test below uses BuildSquareBoard, where baseMoveSpeedTicks (4)
        // times an edge weight of 1 makes every crossing exactly four ticks.

        private const int TicksPerEdge = 4;

        private static SimulationState SquareBoard()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            return TestBoardFactory.BuildSquareBoard(balance);
        }

        private static GameCommand Move(int playerID, int villagerID, int targetNodeID, int tick)
        {
            return new GameCommand
            {
                type = CommandType.Move,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = targetNodeID,
                issuedOnTick = tick,
                value = 0
            };
        }

        [Test]
        public void Retarget_AlongTheSameEdge_KeepsTheGroundCovered()
        {
            SimulationState state = SquareBoard();

            // The route from node 0 to node 3 runs through node 1.
            CommandProcessor.ProcessCommand(state, Move(0, 0, 3, 0));
            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(2, state.villagers[0].moveProgress, "two ticks of a four-tick edge");

            // Node 1 is already on the way, so this changes where the villager
            // stops, not which edge it is crossing. The crossing must survive.
            CommandProcessor.ProcessCommand(state, Move(0, 0, 1, 2));

            Assert.AreEqual(2, state.villagers[0].moveProgress);
            Assert.AreEqual(0, state.villagers[0].movePathIndex);
            Assert.AreEqual(0, state.villagers[0].movePath[0]);
            Assert.AreEqual(1, state.villagers[0].movePath[1]);
            Assert.AreEqual(1, state.villagers[0].targetNodeID);
        }

        [Test]
        public void Retarget_TheOtherWay_WalksBackExactlyWhatItCovered()
        {
            SimulationState state = SquareBoard();

            CommandProcessor.ProcessCommand(state, Move(0, 0, 1, 0));
            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);
            Assert.AreEqual(2, state.villagers[0].moveProgress);

            // The route to node 2 shares nothing with the route to node 1 but the
            // start, so the villager has to come back to node 0 first.
            CommandProcessor.ProcessCommand(state, Move(0, 0, 2, 2));

            // The return is an ordinary forward leg along the reversed edge:
            // movePath[0] is the node it turned around before ever reaching, and
            // currentNodeID -- unchanged -- is what it is walking back to.
            Assert.AreEqual(1, state.villagers[0].movePath[0]);
            Assert.AreEqual(0, state.villagers[0].movePath[1]);
            Assert.AreEqual(2, state.villagers[0].movePath[2]);
            Assert.AreEqual(0, state.villagers[0].currentNodeID);
            Assert.AreEqual(2, state.villagers[0].moveProgress, "two ticks out, two ticks back");

            // Two ticks to regain node 0 -- exactly what the crossing cost.
            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(0, state.villagers[0].currentNodeID);
            Assert.AreEqual(1, state.villagers[0].movePathIndex);
            Assert.AreEqual(0, state.villagers[0].moveProgress);
            Assert.AreEqual(VillagerState.Moving, state.villagers[0].state);

            // Then a full edge on to node 2.
            for (int i = 0; i < TicksPerEdge; i++) GameSimulation.SimulateTick(state);

            Assert.AreEqual(2, state.villagers[0].currentNodeID);
        }

        [Test]
        public void Retarget_ToTheNodeItLeft_CancelsTheOrder()
        {
            SimulationState state = SquareBoard();

            CommandProcessor.ProcessCommand(state, Move(0, 0, 1, 0));
            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);

            // Ordering it back where it came from is how a player cancels a move.
            // For a standing villager an order to its own node is rejected; for a
            // crossing one it is the whole point.
            CommandProcessor.ProcessCommand(state, Move(0, 0, 0, 2));

            Assert.AreEqual(2, state.villagers[0].movePath.Length);
            Assert.AreEqual(2, state.villagers[0].moveProgress);

            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(0, state.villagers[0].currentNodeID);
            Assert.AreEqual(VillagerState.Idle, state.villagers[0].state, "node 0 is its own Core");
        }

        [Test]
        public void Retarget_EveryTick_StillMakesProgress()
        {
            SimulationState state = SquareBoard();

            // Re-issuing an order used to reset the edge clock, so a player
            // tapping faster than one edge duration pinned the villager forever.
            for (int tick = 0; tick < TicksPerEdge; tick++)
            {
                CommandProcessor.ProcessCommand(state, Move(0, 0, 3, tick));
                GameSimulation.SimulateTick(state);
            }

            Assert.AreEqual(1, state.villagers[0].currentNodeID, "four ticks is one whole edge");
        }

        [Test]
        public void CombatDuringAReversal_LeavesItOnTheNodeItIsStandingOn()
        {
            SimulationState state = SquareBoard();

            CommandProcessor.ProcessCommand(state, Move(0, 0, 1, 0));
            GameSimulation.SimulateTick(state);
            GameSimulation.SimulateTick(state);
            CommandProcessor.ProcessCommand(state, Move(0, 0, 2, 2));

            Assert.AreEqual(1, state.villagers[0].movePath[0], "walking back from node 1");

            // An enemy arrives on the node it is walking back to. Combat zeroes
            // moveProgress, which on a reversal leg would otherwise read as
            // standing on node 1 -- a node this villager never reached, and would
            // then walk a whole edge back from.
            state.villagers[1].currentNodeID = 0;
            GameSimulation.SimulateTick(state);

            Assert.AreEqual(VillagerState.Fighting, state.villagers[0].state);
            Assert.AreEqual(0, state.villagers[0].currentNodeID);
            Assert.AreEqual(0, state.villagers[0].movePath[state.villagers[0].movePathIndex],
                "the abandoned leg is dropped, not walked from");
        }

        [Test]
        public void Retarget_TheOtherWay_Determinism()
        {
            SimulationState stateA = SquareBoard();
            SimulationState stateB = SquareBoard();

            SimulationState[] runs = new SimulationState[] { stateA, stateB };
            for (int r = 0; r < runs.Length; r++)
            {
                SimulationState s = runs[r];
                CommandProcessor.ProcessCommand(s, Move(0, 0, 1, 0));
                GameSimulation.SimulateTick(s);
                GameSimulation.SimulateTick(s);
                CommandProcessor.ProcessCommand(s, Move(0, 0, 2, 2));
                for (int i = 0; i < 8; i++) GameSimulation.SimulateTick(s);
            }

            Assert.AreEqual(SimulationStateHasher.ComputeHash(stateA),
                            SimulationStateHasher.ComputeHash(stateB));
        }
    }
}
