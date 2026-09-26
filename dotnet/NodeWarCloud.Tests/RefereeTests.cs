using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.MatchLog;
using NodeWar.Simulation;
using NUnit.Framework;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud.Tests
{
    // Recording also configures simulation statics; finish recording before
    // starting parallel Verify calls and never parallelize this fixture.
    [NonParallelizable]
    public class RefereeTests
    {
        [Test]
        public void HonestWinningLog_VerifiesActualResult()
        {
            GameBalanceData balance = GameBalanceData.Default();
            Log log = Record(balance, 3000, Rush);
            Assert.That(log.result.reason, Is.EqualTo(MatchEndReason.Win));
            RefereeVerdict verdict = new Referee(Catalog(balance)).Verify(MatchLogFormat.Write(log));
            Assert.Multiple(() =>
            {
                Assert.That(verdict.ok, Is.True, verdict.error);
                Assert.That(verdict.error, Is.Null);
                Assert.That(verdict.gameOver, Is.True);
                Assert.That(verdict.winner, Is.EqualTo(log.result.winner));
                Assert.That(verdict.endTick, Is.EqualTo(log.result.endTick));
                Assert.That(verdict.ticksReplayed, Is.EqualTo(log.result.endTick));
                Assert.That(verdict.finalHash, Is.EqualTo(log.result.finalHash));
                Assert.That(verdict.firstMismatchTick, Is.EqualTo(-1));
            });
        }

        [Test]
        public void TamperedCommand_FailsAtFirstAffectedCheckpoint()
        {
            GameBalanceData balance = GameBalanceData.Default();
            Log log = Record(balance, 300, state => state.tickCount == 3
                ? new[] { Move(0, 0, 21), Move(3, 1, 6) } : null);
            log.ticks[0].commands[0].targetNodeID = 17;
            RefereeVerdict verdict = new Referee(Catalog(balance)).Verify(MatchLogFormat.Write(log));
            Assert.That(verdict.ok, Is.False);
            Assert.That(verdict.firstMismatchTick, Is.EqualTo(50));
            Assert.That(verdict.endTick, Is.EqualTo(50));
            Assert.That(verdict.ticksReplayed, Is.EqualTo(50));
        }

        [Test]
        public void UnknownBalance_IsRefusedBeforeReplay()
        {
            GameBalanceData balance = GameBalanceData.Default();
            Log log = Record(balance, 100, Patrol);
            log.header.content = unchecked(log.header.content + 1);
            AssertRefused(new Referee(Catalog(balance)).Verify(MatchLogFormat.Write(log)), "unknown balance");
        }

        [Test]
        public void OversizeLog_IsRefusedBeforeParsing()
        {
            AssertRefused(new Referee(Catalog()).Verify(new byte[Referee.MaxLogBytes + 1]), "512 KB");
        }

        [TestCase(null)]
        [TestCase(new byte[0])]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void GarbageLog_IsRefusedWithoutThrowing(byte[] bytes)
        {
            AssertRefused(new Referee(Catalog()).Verify(bytes));
        }

        [TestCase(null)]
        [TestCase("not base64!")]
        [TestCase("A")]
        public void Module_BadBase64_IsRefusedWithoutThrowing(string value)
        {
            AssertRefused(new RefereeModule().VerifyMatch(null, value), "bad base64");
        }

        [Test]
        public void Module_OversizeBase64_IsRefusedBeforeDecoding()
        {
            AssertRefused(new RefereeModule().VerifyMatch(null,
                Convert.ToBase64String(new byte[Referee.MaxLogBytes + 3])), "512 KB");
        }

        [Test]
        public void Module_ValidBase64OfGarbage_IsRefused()
        {
            AssertRefused(new RefereeModule().VerifyMatch(null, Convert.ToBase64String(new byte[20])));
        }

        [Test]
        public void BalanceFilenameMismatch_IsSkippedAndLogged()
        {
            GameBalanceData balance = GameBalanceData.Default();
            string json = JsonConvert.SerializeObject(balance);
            var warnings = new List<string>();
            var catalog = new BalanceCatalog(new[]
            {
                new KeyValuePair<string, string>(unchecked(BalanceHasher.Hash(balance) + 1) + ".json", json)
            }, warnings.Add);
            Assert.That(warnings, Has.Count.EqualTo(1));
            StringAssert.Contains("hash does not match", warnings[0]);
            AssertRefused(new Referee(catalog).Verify(MatchLogFormat.Write(Record(balance, 100, Patrol))),
                "unknown balance");
        }

        [Test]
        public void InvalidBalanceFiles_DoNotHideValidOnes()
        {
            GameBalanceData balance = GameBalanceData.Default();
            var warnings = new List<string>();
            var catalog = new BalanceCatalog(new[]
            {
                new KeyValuePair<string, string>("invalid.json", "{}"),
                new KeyValuePair<string, string>("123.json", "not json"),
                BalanceFile(balance)
            }, warnings.Add);
            Assert.That(warnings, Has.Count.EqualTo(2));
            RefereeVerdict verdict = new Referee(catalog).Verify(MatchLogFormat.Write(Record(balance, 100, Patrol)));
            Assert.That(verdict.ok, Is.True, verdict.error);
        }

        [Test]
        public void BalancePublicFieldsAndIntegerEnums_RoundTripThroughCatalog()
        {
            GameBalanceData balance = GameBalanceData.Default();
            balance.suitStats = new[]
            {
                new SuitStats { suitType = SuitType.Warrior, bonusHP = 2, attackDamage = 3,
                    moveSpeedTicks = 2, attackCooldownMax = 12, foodCost = 1, materialCost = 2, fightPriority = 7 }
            };
            var file = BalanceFile(balance);
            Assert.That(JObject.Parse(file.Value)["suitStats"][0]["suitType"].Type, Is.EqualTo(JTokenType.Integer));
            RefereeVerdict verdict = new Referee(Catalog(balance)).Verify(MatchLogFormat.Write(Record(balance, 100, Patrol)));
            Assert.That(verdict.ok, Is.True, verdict.error);
        }

        [Test]
        public void EmbeddedCatalog_IsLoadedOnce_AndAcceptsWhateverBalancesArePresent()
        {
            Assert.That(BalanceCatalog.Embedded, Is.SameAs(BalanceCatalog.Embedded));
            var assembly = typeof(BalanceCatalog).Assembly;
            foreach (string name in assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("NodeWar.Cloud.Balances.") && n.EndsWith(".json")))
            {
                using var stream = assembly.GetManifestResourceStream(name);
                using var reader = new StreamReader(stream);
                GameBalanceData balance = JsonConvert.DeserializeObject<GameBalanceData>(reader.ReadToEnd());
                string expectedName = "NodeWar.Cloud.Balances." + BalanceHasher.Hash(balance).ToString(CultureInfo.InvariantCulture) + ".json";
                if (name != expectedName) continue; // Catalog intentionally skips mismatched filenames.
                RefereeVerdict verdict = new Referee(BalanceCatalog.Embedded)
                    .Verify(MatchLogFormat.Write(Record(balance, 100, Patrol)));
                Assert.That(verdict.ok, Is.True, verdict.error);
            }
        }

        [Test]
        public async Task EightConcurrentReplays_AcrossInstancesAndBalances_MatchSequentialResults()
        {
            var balances = new GameBalanceData[8];
            var bytes = new byte[8][];
            for (int i = 0; i < bytes.Length; i++)
            {
                balances[i] = GameBalanceData.Default();
                balances[i].baseMoveSpeedTicks += i;
                balances[i].baseClaimPerTick += i;
                BoardConfigData board = PatrolBoard();
                board.ownedMultiplier += i * 10;
                board.unownedMultiplier += i * 15;
                bytes[i] = MatchLogFormat.Write(Record(balances[i], 2000 + i * 50, Patrol, board));
            }
            BalanceCatalog catalog = Catalog(balances);
            var referees = Enumerable.Range(0, 8).Select(_ => new Referee(catalog)).ToArray();
            var sequential = Enumerable.Range(0, 8).Select(i => referees[i].Verify(bytes[i])).ToArray();
            foreach (var verdict in sequential) Assert.That(verdict.ok, Is.True, verdict.error);

            using var ready = new CountdownEvent(8);
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, 8).Select(i => Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                return referees[i].Verify(bytes[i]);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            bool allReady = ready.Wait(TimeSpan.FromSeconds(10));
            start.Set();
            RefereeVerdict[] parallel = await Task.WhenAll(tasks);
            Assert.That(allReady, Is.True, "All workers must be waiting at the start gate.");
            for (int i = 0; i < 8; i++)
            {
                JObject expected = JObject.FromObject(sequential[i]);
                JObject actual = JObject.FromObject(parallel[i]);
                expected.Remove("elapsedMs");
                actual.Remove("elapsedMs");
                Assert.That(JToken.DeepEquals(actual, expected), Is.True,
                    "Concurrent replay " + i + ": " + actual);
            }
        }

        [Test]
        public void LongMatch_12000Ticks_VerifiesAndWritesCloudSpikeArtifact()
        {
            GameBalanceData balance = GameBalanceData.Default();
            Log log = Record(balance, 12000, Patrol, PatrolBoard());
            Assert.That(log.result.endTick, Is.EqualTo(12000));
            Assert.That(log.ticks.Last().tick, Is.GreaterThanOrEqualTo(11980));
            Assert.That(log.ticks.SelectMany(t => t.commands).Count(c => c.type == CommandType.SetAllocation),
                Is.GreaterThanOrEqualTo(200));
            foreach (int player in new[] { 0, 1 })
                Assert.That(log.ticks.SelectMany(t => t.commands).Count(c => c.type == CommandType.Move && c.playerID == player),
                    Is.EqualTo(600));
            byte[] bytes = MatchLogFormat.Write(log);
            RefereeVerdict verdict = new Referee(Catalog(balance)).Verify(bytes);
            Assert.That(verdict.ok, Is.True, verdict.error);
            Assert.That(verdict.ticksReplayed, Is.EqualTo(12000));
            string directory = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../.."));
            string path = Path.Combine(directory, "long-match.nwml");
            File.WriteAllBytes(path, bytes);
            // This synthetic log needs its in-test balance for the cloud spike;
            // it does not claim to use the shipped Unity asset.
            var file = BalanceFile(balance);
            File.WriteAllText(Path.Combine(directory, file.Key), file.Value);
            TestContext.WriteLine("12000 ticks: elapsedMs=" + verdict.elapsedMs + ", logBytes=" + bytes.Length);
            TestContext.WriteLine("Log: " + path);
            TestContext.WriteLine("Synthetic balance: " + Path.Combine(directory, file.Key));
        }

        private static void AssertRefused(RefereeVerdict verdict, string error = null)
        {
            Assert.That(verdict.ok, Is.False);
            Assert.That(verdict.error, Is.Not.Empty);
            if (error != null) StringAssert.Contains(error, verdict.error);
            Assert.That(verdict.ticksReplayed, Is.Zero);
            Assert.That(verdict.firstMismatchTick, Is.EqualTo(-1));
        }

        private static BalanceCatalog Catalog(params GameBalanceData[] balances) =>
            new BalanceCatalog(balances.Select(BalanceFile), message => Assert.Fail(message));

        private static KeyValuePair<string, string> BalanceFile(GameBalanceData balance) =>
            new KeyValuePair<string, string>(BalanceHasher.Hash(balance).ToString(CultureInfo.InvariantCulture) + ".json",
                JsonConvert.SerializeObject(balance, Formatting.Indented));

        private static Log Record(GameBalanceData balance, int maxTicks, Func<SimulationState, GameCommand[]> script,
            BoardConfigData? boardOverride = null)
        {
            BoardConfigData board = boardOverride ?? BoardConfigData.Default();
            DraftPlacement[] draft =
            {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 5 },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 2, gridZ = 1 }
            };
            PlayerLoadout[] loadouts =
            {
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = Array.Empty<int>() },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = Array.Empty<int>() }
            };
            var header = new MatchLogHeader
            {
                protocol = 1, sim = (ushort)SimulationVersion.Current, content = BalanceHasher.Hash(balance),
                matchId = "referee-test", playerIds = new[] { "p0", "p1" }, kind = MatchKind.Bot
            };
            var recorder = new MatchRecorder(header, board, loadouts, draft);
            MatchFactory.Configure(balance, board);
            SimulationState state = MatchFactory.Build(balance, board, draft, new[]
            {
                new PlayerSetup { suits = loadouts[0].suits, nodes = loadouts[0].nodes },
                new PlayerSetup { suits = loadouts[1].suits, nodes = loadouts[1].nodes }
            });
            while (state.tickCount < maxTicks && !state.gameOver)
            {
                GameCommand[] commands = script(state);
                recorder.RecordTick(state.tickCount, commands);
                if (commands != null)
                    foreach (GameCommand command in commands) CommandProcessor.ProcessCommand(state, command);
                GameSimulation.SimulateTick(state);
                if (state.tickCount % 50 == 0)
                    recorder.RecordHash(state.tickCount, SimulationStateHasher.ComputeHash(state));
            }
            recorder.Finish(new MatchResult
            {
                reason = state.gameOver ? MatchEndReason.Win : MatchEndReason.Abandoned,
                winner = state.gameOver ? state.winnerID : -1,
                endTick = state.tickCount, finalHash = SimulationStateHasher.ComputeHash(state), firstDesyncTick = -1
            });
            return recorder.Log;
        }

        private static BoardConfigData PatrolBoard()
        {
            BoardConfigData board = BoardConfigData.Default();
            board.initialPlacements = board.initialPlacements.Concat(new[]
            {
                new BoardConfigData.InitialNodePlacement { gridX = 0, gridZ = 5, districtType = DistrictType.Forge,
                    ownerID = 0, claimBar = 10000 },
                new BoardConfigData.InitialNodePlacement { gridX = 3, gridZ = 1, districtType = DistrictType.Forge,
                    ownerID = 1, claimBar = -10000 }
            }).ToArray();
            return board;
        }

        private static GameCommand[] Patrol(SimulationState state)
        {
            if (state.tickCount % 20 != 0) return null;
            bool outward = state.tickCount % 40 == 0;
            var commands = new List<GameCommand> { Move(0, 0, outward ? 20 : 23), Move(3, 1, outward ? 7 : 4) };
            if (state.tickCount % 100 == 0)
            {
                int allocation = state.tickCount / 100 % 3;
                commands.Add(new GameCommand { type = CommandType.SetAllocation, playerID = 0, targetNodeID = 20, value = allocation });
                commands.Add(new GameCommand { type = CommandType.SetAllocation, playerID = 1, targetNodeID = 7, value = allocation });
            }
            return commands.ToArray();
        }

        private static GameCommand[] Rush(SimulationState state)
        {
            if (state.tickCount == 0) return new[] { Move(3, 1, 3), Move(4, 1, 3), Move(5, 1, 3) };
            if (state.tickCount % 20 != 0) return null;
            var commands = new List<GameCommand>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == 0 && v.state == VillagerState.Idle && !v.isConsumed)
                    commands.Add(Move(i, 0, state.players[1].coreNodeID));
            }
            return commands.ToArray();
        }

        private static GameCommand Move(int villager, int player, int target) => new GameCommand
        {
            type = CommandType.Move, playerID = player, villagerID = villager, targetNodeID = target
        };
    }
}
