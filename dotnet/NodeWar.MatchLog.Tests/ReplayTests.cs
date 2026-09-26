using System;
using NUnit.Framework;
using NodeWar.Simulation;
using NodeWar.Tests;

namespace NodeWar.MatchLog
{
    public class ReplayTests
    {
        [Test]
        public void RecordedCommands_ReproduceEveryCheckpointAndFinalHash()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            MatchLog setup = TestLogs.Full();
            setup.board = ReplayConfig();
            setup.draft = Array.Empty<DraftPlacement>();
            MatchRecorder recorder = new MatchRecorder(setup.header, setup.board, setup.loadouts, setup.draft);
            SimulationState original = BuildBoard(balance, setup.board);
            for (int i = 0; i < 120; i++)
            {
                int tickBefore = original.tickCount;
                GameCommand[] commands = CommandsAt(tickBefore);
                recorder.RecordTick(tickBefore, commands);
                ApplyAndTick(original, commands);
                if (tickBefore == 0)
                    Assert.AreEqual(7, original.nodes[2].materialAllocation, "The allocation command must actually apply.");
                if (original.tickCount % 50 == 0)
                    recorder.RecordHash(original.tickCount, SimulationStateHasher.ComputeHash(original));
            }
            Assert.AreEqual(120, original.tickCount);
            recorder.Finish(new MatchResult
            {
                reason = MatchEndReason.Abandoned, winner = -1, endTick = original.tickCount,
                finalHash = SimulationStateHasher.ComputeHash(original), firstDesyncTick = -1
            });

            MatchLog replay = TestLogs.Read(recorder.ToBytes());
            SimulationState restored = BuildBoard(balance, replay.board);
            int tickEntry = 0;
            int checkpoint = 0;
            while (restored.tickCount < replay.result.endTick)
            {
                GameCommand[] commands = null;
                if (tickEntry < replay.ticks.Count && replay.ticks[tickEntry].tick == restored.tickCount)
                    commands = replay.ticks[tickEntry++].commands;
                ApplyAndTick(restored, commands);
                if (checkpoint < replay.hashes.Count && replay.hashes[checkpoint].tick == restored.tickCount)
                {
                    Assert.AreEqual(replay.hashes[checkpoint].hash, SimulationStateHasher.ComputeHash(restored),
                        "Replay checkpoint " + restored.tickCount);
                    checkpoint++;
                }
            }
            Assert.AreEqual(replay.ticks.Count, tickEntry);
            Assert.AreEqual(2, checkpoint);
            Assert.AreEqual(replay.hashes.Count, checkpoint);
            Assert.AreEqual(replay.result.finalHash, SimulationStateHasher.ComputeHash(restored));

            SimulationState idle = BuildBoard(balance, replay.board);
            for (int i = 0; i < 120; i++) ApplyAndTick(idle, null);
            Assert.AreNotEqual(SimulationStateHasher.ComputeHash(idle), replay.result.finalHash,
                "A replay test must distinguish applied commands from an idle match.");
        }

        private static BoardConfigData ReplayConfig()
        {
            BoardConfigData config = BoardConfigData.Default();
            config.gridCols = 2; config.gridRows = 2; config.defaultEdgeWeight = 1;
            config.startingVillagersPerPlayer = 1;
            config.initialPlacements = new[]
            {
                new BoardConfigData.InitialNodePlacement
                { gridX = 0, gridZ = 0, districtType = DistrictType.Core, ownerID = 0, claimBar = 10000 },
                new BoardConfigData.InitialNodePlacement
                { gridX = 1, gridZ = 0, districtType = DistrictType.Forge, ownerID = 0, claimBar = 10000 },
                new BoardConfigData.InitialNodePlacement
                { gridX = 0, gridZ = 1, districtType = DistrictType.Forge, ownerID = 1, claimBar = -10000 },
                new BoardConfigData.InitialNodePlacement
                { gridX = 1, gridZ = 1, districtType = DistrictType.Core, ownerID = 1, claimBar = -10000 }
            };
            return config;
        }

        private static SimulationState BuildBoard(GameBalanceData balance, BoardConfigData config)
        {
            SimulationState state = TestBoardFactory.BuildSquareBoard(balance);
            // Fixture setup only: keep the factory's graph and villagers while
            // giving each player a forge on which SetAllocation is meaningful.
            foreach (BoardConfigData.InitialNodePlacement p in config.initialPlacements)
            {
                int node = p.gridZ * config.gridCols + p.gridX;
                state.nodes[node].districtType = p.districtType;
                state.nodes[node].baseDistrictType = p.districtType;
                state.nodes[node].ownerID = p.ownerID;
                state.nodes[node].claimBar = p.claimBar;
            }
            return state;
        }

        private static GameCommand[] CommandsAt(int tick)
        {
            switch (tick)
            {
                case 0: return new[]
                {
                    TestLogs.Command(CommandType.SetAllocation, 1, -1, 2, tick, 7),
                    TestLogs.Command(CommandType.Move, 0, 0, 1, tick),
                    TestLogs.Command(CommandType.Move, 1, 1, 2, tick)
                };
                case 12: return new[] { TestLogs.Command(CommandType.SetAllocation, 0, -1, 1, tick, 11) };
                case 25: return new[]
                {
                    TestLogs.Command(CommandType.Move, 1, 1, 3, tick),
                    TestLogs.Command(CommandType.Move, 0, 0, 0, tick)
                };
                case 55: return new[]
                {
                    TestLogs.Command(CommandType.SetAllocation, 0, -1, 1, tick, 13),
                    TestLogs.Command(CommandType.SetAllocation, 1, -1, 2, tick, 17)
                };
                case 80: return new[]
                {
                    TestLogs.Command(CommandType.Move, 0, 0, 1, tick),
                    TestLogs.Command(CommandType.Move, 1, 1, 2, tick)
                };
                default: return null;
            }
        }

        private static void ApplyAndTick(SimulationState state, GameCommand[] commands)
        {
            if (commands != null)
                foreach (GameCommand command in commands) CommandProcessor.ProcessCommand(state, command);
            GameSimulation.SimulateTick(state);
        }
    }
}
