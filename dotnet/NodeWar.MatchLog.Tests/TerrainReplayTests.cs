using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    /// <summary>
    /// A match on the shipped hourglass, with a Pier, recorded as it is played, written,
    /// read and replayed by the referee's own routine. The log must reproduce every hash
    /// the live game computed, and must stop doing so when a movement command is changed.
    /// </summary>
    public class TerrainReplayTests
    {
        private static readonly GameBalanceData Balance = GameBalanceData.Default();

        private static readonly DraftPlacement[] Draft =
        {
            new DraftPlacement { playerID = 0, districtType = DistrictType.Pier, gridX = 1, gridZ = 3 },
            new DraftPlacement { playerID = 1, districtType = DistrictType.Farm, gridX = 4, gridZ = 2 },
            new DraftPlacement { playerID = 0, districtType = DistrictType.Village, gridX = 4, gridZ = 4 }
        };

        private static GameCommand Move(int villager, int player, int node) => new GameCommand
        {
            type = CommandType.Move, playerID = player, villagerID = villager, targetNodeID = node
        };

        // Sends both sides out over the board, across the Pier and round the lake.
        private static GameCommand[] Script(SimulationState state)
        {
            int last = state.nodes.Length - 1;
            switch (state.tickCount)
            {
                case 3: return new[] { Move(0, 0, last), Move(3, 1, 0) };
                case 40: return new[] { Move(1, 0, last / 2), Move(4, 1, last / 3), Move(2, 0, 4) };
                case 120: return new[] { Move(5, 1, last - 2), Move(0, 0, 7) };
                case 260: return new[] { Move(1, 0, 2), Move(3, 1, last - 5) };
                default: return null;
            }
        }

        private static MatchLog Record(int maxTicks, out int[] liveHashes, out int[] liveTicks)
        {
            BoardConfigData board = PremadeMaps.Hourglass01();
            PlayerLoadout[] loadouts =
            {
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = new int[0] }
            };
            var header = new MatchLogHeader
            {
                protocol = 5, sim = (ushort)SimulationVersion.Current, content = BalanceHasher.Hash(Balance),
                matchId = "hourglass", playerIds = new[] { "", "" }, kind = MatchKind.Bot
            };
            var recorder = new MatchRecorder(header, MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, header.content),
                board, loadouts, Draft);

            MatchFactory.Configure(Balance, board);
            SimulationState state = MatchFactory.Build(Balance, board, Draft, new[]
            {
                new PlayerSetup { suits = loadouts[0].suits, districts = loadouts[0].districts },
                new PlayerSetup { suits = loadouts[1].suits, districts = loadouts[1].districts }
            });
            Assert.AreEqual(19, state.nodes.Length, "18 land nodes and the Pier");

            var hashes = new System.Collections.Generic.List<int>();
            var ticks = new System.Collections.Generic.List<int>();
            while (state.tickCount < maxTicks && !state.gameOver)
            {
                GameCommand[] commands = Script(state);
                recorder.RecordTick(state.tickCount, commands);
                if (commands != null)
                    foreach (GameCommand c in commands) CommandProcessor.ProcessCommand(state, c);
                GameSimulation.SimulateTick(state);
                if (state.tickCount % 50 == 0)
                {
                    int hash = SimulationStateHasher.ComputeHash(state);
                    recorder.RecordHash(state.tickCount, hash);
                    hashes.Add(hash);
                    ticks.Add(state.tickCount);
                }
            }
            recorder.Finish(new MatchResult
            {
                reason = state.gameOver ? MatchEndReason.Win : MatchEndReason.Abandoned,
                winner = state.gameOver ? state.winnerID : -1,
                endTick = state.tickCount,
                finalHash = SimulationStateHasher.ComputeHash(state),
                firstDesyncTick = -1
            });
            liveHashes = hashes.ToArray();
            liveTicks = ticks.ToArray();
            return recorder.Log;
        }

        private static MatchLog RoundTrip(MatchLog log)
        {
            Assert.IsTrue(MatchLogFormat.TryRead(MatchLogFormat.Write(log), out MatchLog read, out string error), error);
            return read;
        }

        [Test]
        public void Hourglass_LogMatchesLiveHashes()
        {
            MatchLog recorded = Record(900, out int[] liveHashes, out int[] liveTicks);
            Assert.GreaterOrEqual(liveHashes.Length, 15);

            MatchLog log = RoundTrip(recorded);
            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsTrue(outcome.ok, outcome.error);
            Assert.AreEqual(recorded.result.endTick, outcome.endTick);
            Assert.AreEqual(recorded.result.finalHash, outcome.finalHash, "the replay ends where the live game did");
            Assert.AreEqual(liveHashes.Length, log.hashes.Count);
            for (int i = 0; i < liveHashes.Length; i++)
            {
                Assert.AreEqual(liveTicks[i], log.hashes[i].tick);
                Assert.AreEqual(liveHashes[i], log.hashes[i].hash, "checkpoint at tick " + liveTicks[i]);
            }
        }

        [Test]
        public void Hourglass_LogMatchesLiveHashes_Determinism()
        {
            Record(600, out int[] a, out _);
            Record(600, out int[] b, out _);
            CollectionAssert.AreEqual(a, b, "two independent recordings hash alike");
        }

        [Test]
        public void Hourglass_LogMatchesLiveHashes_ChangingAMovementCommandBreaksACheckpoint()
        {
            MatchLog log = RoundTrip(Record(900, out _, out _));
            LoggedTick first = log.ticks[0];
            int original = first.commands[0].targetNodeID;
            first.commands[0].targetNodeID = original == 4 ? 5 : 4;

            ReplayOutcome outcome = MatchReplay.Run(log, Balance);

            Assert.IsFalse(outcome.ok);
            Assert.Greater(outcome.firstMismatchTick, 0, "named at a checkpoint, not at a refusal");
            StringAssert.Contains("hash", outcome.error);
        }
    }
}
