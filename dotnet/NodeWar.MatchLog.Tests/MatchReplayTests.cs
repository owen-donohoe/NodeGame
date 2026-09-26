using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    public class MatchReplayTests
    {
        private static readonly GameBalanceData Balance = GameBalanceData.Default();

        [Test]
        public void HonestLog_Replays()
        {
            MatchLog log = RoundTrip(Record(300, Script));

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsTrue(outcome.ok, outcome.error);
            Assert.AreEqual(300, outcome.endTick);
            Assert.AreEqual(log.result.finalHash, outcome.finalHash);
            Assert.AreEqual(-1, outcome.firstMismatchTick);
            Assert.GreaterOrEqual(log.hashes.Count, 5);
        }

        [Test]
        public void WinningLog_Replays_AndAWrongWinnerIsCaught()
        {
            MatchLog log = RoundTrip(Record(3000, Rush));
            Assert.AreEqual(MatchEndReason.Win, log.result.reason, "The rush script must end the match.");

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);
            Assert.IsTrue(outcome.ok, outcome.error);
            Assert.IsTrue(outcome.gameOver);
            Assert.AreEqual(log.result.winner, outcome.winner);

            log.result.winner = 1 - log.result.winner;
            Assert.IsFalse(MatchReplay.Run(log, Balance).ok);
        }

        [Test]
        public void TamperedCommand_FailsAtTheNextCheckpoint()
        {
            MatchLog log = RoundTrip(Record(300, Script));
            LoggedTick first = log.ticks[0];
            first.commands[0].targetNodeID = first.commands[0].targetNodeID == 21 ? 17 : 21;

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsFalse(outcome.ok);
            Assert.AreEqual(log.hashes[0].tick, outcome.firstMismatchTick);
        }

        [Test]
        public void TamperedHash_IsCaughtAtThatTick()
        {
            MatchLog log = RoundTrip(Record(300, Script));
            HashCheckpoint cp = log.hashes[2];
            log.hashes[2] = new HashCheckpoint { tick = cp.tick, hash = cp.hash + 1 };

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsFalse(outcome.ok);
            Assert.AreEqual(cp.tick, outcome.firstMismatchTick);
        }

        [Test]
        public void TamperedFinalHash_IsCaught()
        {
            MatchLog log = RoundTrip(Record(300, Script));
            log.result.finalHash++;
            Assert.IsFalse(MatchReplay.Run(log, Balance).ok);
        }

        [Test]
        public void EndTickPastGameOver_IsRefused()
        {
            MatchLog log = RoundTrip(Record(3000, Rush));
            log.result.reason = MatchEndReason.Abandoned;
            log.result.endTick += 10; // the win came well inside MaxTicks

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsFalse(outcome.ok);
            StringAssert.Contains("Game ended", outcome.error);
        }

        [Test]
        public void Refusals_BeforeRunning()
        {
            MatchLog log = RoundTrip(Record(120, Script));

            MatchLog wrongSim = RoundTrip(log);
            wrongSim.header.sim = (ushort)(SimulationVersion.Current + 1);
            StringAssert.Contains("simulation version", MatchReplay.Run(wrongSim, Balance).error);

            MatchLog noResult = RoundTrip(log);
            noResult.result = null;
            Assert.IsFalse(MatchReplay.Run(noResult, Balance).ok);

            MatchLog tooLong = RoundTrip(log);
            tooLong.result.endTick = MatchReplay.MaxTicks + 1;
            Assert.IsFalse(MatchReplay.Run(tooLong, Balance).ok);

            MatchLog outOfOrder = RoundTrip(log);
            outOfOrder.ticks.Reverse();
            Assert.IsFalse(MatchReplay.Run(outOfOrder, Balance).ok);

            MatchLog commandPastEnd = RoundTrip(log);
            commandPastEnd.result.endTick = commandPastEnd.ticks[commandPastEnd.ticks.Count - 1].tick;
            Assert.IsFalse(MatchReplay.Run(commandPastEnd, Balance).ok);
        }

        // --- Recording, the way GameManager and the runners do it ---

        private delegate GameCommand[] CommandScript(SimulationState state);

        private static MatchLog Record(int maxTicks, CommandScript script)
        {
            BoardConfigData board = BoardConfigData.Default();
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 5 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 2, gridZ = 1 }
            };
            PlayerLoadout[] loadouts =
            {
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = new int[0] },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = new int[0] }
            };
            var header = new MatchLogHeader
            {
                protocol = 1, sim = (ushort)SimulationVersion.Current, content = 0, matchId = "test",
                playerIds = new[] { "", "" }, kind = MatchKind.Bot
            };

            var recorder = new MatchRecorder(header, board, loadouts, draft);
            MatchFactory.Configure(Balance, board);
            SimulationState state = MatchFactory.Build(Balance, board, draft, new[]
            {
                new PlayerSetup { suits = loadouts[0].suits, nodes = loadouts[0].nodes },
                new PlayerSetup { suits = loadouts[1].suits, nodes = loadouts[1].nodes }
            });

            while (state.tickCount < maxTicks && !state.gameOver)
            {
                GameCommand[] commands = script(state);
                recorder.RecordTick(state.tickCount, commands);
                if (commands != null)
                    foreach (GameCommand c in commands) CommandProcessor.ProcessCommand(state, c);
                GameSimulation.SimulateTick(state);
                if (state.tickCount % 50 == 0)
                    recorder.RecordHash(state.tickCount, SimulationStateHasher.ComputeHash(state));
            }

            recorder.Finish(new MatchResult
            {
                reason = state.gameOver ? MatchEndReason.Win : MatchEndReason.Abandoned,
                winner = state.gameOver ? state.winnerID : -1,
                endTick = state.tickCount,
                finalHash = SimulationStateHasher.ComputeHash(state),
                firstDesyncTick = -1
            });
            return recorder.Log;
        }

        private static GameCommand[] Script(SimulationState state)
        {
            switch (state.tickCount)
            {
                case 3: return new[] { Move(0, 0, 21), Move(3, 1, 6) };
                case 40: return new[] { Move(1, 0, 17), Move(4, 1, 10), Move(0, 0, 13) };
                case 90: return new[] { Move(5, 1, 14) };
                case 160: return new[] { Move(2, 0, 21), Move(3, 1, 18) };
                default: return null;
            }
        }

        /// <summary>
        /// P1 walks its villagers off to the corner beside its core, leaving the
        /// core open; P0 sends everyone at it, again whenever one is idle.
        /// Three breaches end the match.
        /// </summary>
        private static GameCommand[] Rush(SimulationState state)
        {
            if (state.tickCount == 0)
                return new[] { Move(3, 1, 3), Move(4, 1, 3), Move(5, 1, 3) };
            if (state.tickCount % 20 != 0) return null;
            int enemyCore = state.players[1].coreNodeID;
            var commands = new System.Collections.Generic.List<GameCommand>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == 0 && v.state == VillagerState.Idle && !v.isConsumed)
                    commands.Add(Move(i, 0, enemyCore));
            }
            return commands.Count > 0 ? commands.ToArray() : null;
        }

        private static GameCommand Move(int villager, int player, int target)
        {
            return new GameCommand
            {
                type = CommandType.Move, playerID = player, villagerID = villager, targetNodeID = target
            };
        }

        private static MatchLog RoundTrip(MatchLog log)
        {
            Assert.IsTrue(MatchLogFormat.TryRead(MatchLogFormat.Write(log), out MatchLog read, out string error), error);
            return read;
        }
    }
}
