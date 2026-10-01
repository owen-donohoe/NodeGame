using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.Simulation;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.Matchmaker.Model;
using MatchmakingResults = Unity.Services.CloudCode.Apis.Matchmaker.MatchmakingResults;

namespace NodeWar.Cloud.Tests
{
    public class MatchAllocationTests
    {
        private CountingStore matches;
        private Dictionary<string, PlayerState> states;
        private List<string> reads;
        private BalanceCatalog balances;
        private int content;
        private MatchAllocation allocation;
        private Dictionary<string, ClaimStore> claims;
        private MatchmakerAllocatorModule module;
        private const long Now = 1234567;

        [SetUp]
        public void SetUp()
        {
            var balance = GameBalanceData.Default();
            content = BalanceHasher.Hash(balance);
            balances = RefereeTests.Catalog(balance);
            matches = new CountingStore();
            states = new Dictionary<string, PlayerState>
            {
                ["p0"] = MatchRecordTests.Player(), ["p1"] = MatchRecordTests.Player()
            };
            reads = new List<string>();
            claims = new Dictionary<string, ClaimStore> { ["p0"] = new ClaimStore(states["p0"]), ["p1"] = new ClaimStore(states["p1"]) };
            allocation = new MatchAllocation(ReadPlayer, matches, balances, id => claims[id]);
            module = new MatchmakerAllocatorModule(_ => matches, (_, id) => ReadPlayer(id), balances, () => Now, (_, id) => claims[id]);
        }

        [Test]
        public async Task AllocationStoresBothServerSnapshotsVersionsAndServerTime()
        {
            states["p0"].Rating.R = 1600;
            states["p0"].Rating.LastMatchUnixSeconds = 100;
            states["p0"].Rank.RR = 50;
            states["p0"].Rank.HighestArena = 2;
            states["p0"].Inventory.OwnedVariants.Add("suit.warrior.e0");
            states["p1"].Rating.R = 1750;
            states["p1"].Rating.Rd = 120;
            states["p1"].Rating.Sigma = 0.08;
            states["p1"].Rank.Arena = 1;
            states["p1"].Rank.RR = 400;
            states["p1"].Inventory.OwnedVariants.Add("suit.warrior.e1");

            var result = await allocation.Allocate("m", Roster(), Now);
            Assert.That(result.ok, Is.True, result.error);
            var record = (await matches.ReadAsync("m")).Record;
            Assert.Multiple(() =>
            {
                Assert.That(record.matchId, Is.EqualTo("m"));
                Assert.That(record.playerIds, Is.EqualTo(new[] { "p0", "p1" }));
                Assert.That(record.protocol, Is.EqualTo(ProtocolVersion.Current));
                Assert.That(record.sim, Is.EqualTo(1));
                Assert.That(record.content, Is.EqualTo(content));
                Assert.That(record.createdUnixSeconds, Is.EqualTo(Now));
                Assert.That(record.state, Is.EqualTo(MatchRecordState.Open));
                Assert.That(record.players[0].Rating.R, Is.EqualTo(1600));
                Assert.That(record.players[0].Rating.LastMatchUnixSeconds, Is.EqualTo(100));
                Assert.That(record.players[0].Rank.RR, Is.EqualTo(50));
                Assert.That(record.players[0].Rank.HighestArena, Is.EqualTo(2));
                Assert.That(record.players[0].OwnedVariants, Is.EqualTo(new[] { "suit.warrior.e0" }));
                Assert.That(record.players[1].Rating.R, Is.EqualTo(1750));
                Assert.That(record.players[1].Rating.Rd, Is.EqualTo(120));
                Assert.That(record.players[1].Rating.Sigma, Is.EqualTo(0.08));
                Assert.That(record.players[1].Rank.Arena, Is.EqualTo(1));
                Assert.That(record.players[1].Rank.RR, Is.EqualTo(400));
                Assert.That(record.players[1].OwnedVariants, Is.EqualTo(new[] { "suit.warrior.e1" }));
                Assert.That(reads, Is.EqualTo(new[] { "p0", "p1" }));
                Assert.That(matches.Writes, Is.EqualTo(1));
                Assert.That(matches.LastLock, Is.Null);
            });
            states["p0"].Rating.R = 0;
            states["p1"].Inventory.OwnedVariants.Clear();
            Assert.That((await matches.ReadAsync("m")).Record.players[0].Rating.R, Is.EqualTo(1600));
            Assert.That((await matches.ReadAsync("m")).Record.players[1].OwnedVariants, Is.Not.Empty);
        }

        [Test]
        public async Task RetryPreservesRecordWithoutAnotherPlayerReadOrWrite()
        {
            await allocation.Allocate("m", Roster(), Now);
            string before = JsonConvert.SerializeObject((await matches.ReadAsync("m")).Record);
            states["p1"].Rank.Arena = 5;
            var result = await allocation.Allocate("m", null, Now + 100);
            Assert.That(result.ok, Is.True);
            Assert.That(matches.Writes, Is.EqualTo(1));
            Assert.That(reads.Count, Is.EqualTo(2));
            Assert.That(JsonConvert.SerializeObject((await matches.ReadAsync("m")).Record), Is.EqualTo(before));
        }

        [TestCase("protocol")]
        [TestCase("sim")]
        [TestCase("content")]
        public async Task VersionMismatchRefusesWithoutReadingPlayers(string field)
        {
            var players = Roster();
            if (field == "protocol") players[1].Protocol++;
            if (field == "sim") players[1].Sim++;
            if (field == "content") players[1].Content ^= 1;
            await AssertRefused(await allocation.Allocate("m", players, Now), "different");
            Assert.That(reads, Is.Empty);
        }

        [TestCase("protocol", -1)]
        [TestCase("protocol", 1)]
        [TestCase("sim", -1)]
        [TestCase("sim", 1)]
        public async Task MatchingButUnsupportedVersionsRefuseBeforeClaims(string field, int offset)
        {
            var roster = Roster();
            foreach (var player in roster)
            {
                if (field == "protocol") player.Protocol = (ushort)(ProtocolVersion.Current + offset);
                else player.Sim = (ushort)(SimulationVersion.Current + offset);
            }
            await AssertRefused(await allocation.Allocate("m", roster, Now), "Unsupported");
            Assert.That(reads, Is.Empty);
            Assert.That(states["p0"].ActiveMatch, Is.Null);
            Assert.That(states["p1"].ActiveMatch, Is.Null);
        }

        [Test]
        public async Task UnknownContentRefusesWithoutReadingPlayers()
        {
            var players = Roster();
            players[0].Content = players[1].Content = content ^ 1;
            await AssertRefused(await allocation.Allocate("m", players, Now), "Unknown");
            Assert.That(reads, Is.Empty);
        }

        [TestCase(0, 2)]
        [TestCase(2, 0)]
        [TestCase(int.MinValue, int.MaxValue)]
        public async Task ArenaGapUsesServerRankAndCannotOverflow(int first, int second)
        {
            states["p0"].Rank.Arena = first;
            states["p1"].Rank.Arena = second;
            await AssertRefused(await allocation.Allocate("m", Roster(), Now), "arena");
        }

        [TestCase("one")]
        [TestCase("three")]
        [TestCase("duplicate")]
        [TestCase("blank")]
        [TestCase("null")]
        public async Task InvalidRosterRefusesWithoutReadingPlayers(string kind)
        {
            var players = Roster();
            if (kind == "one") players = new[] { players[0] };
            if (kind == "three") players = new[] { players[0], players[1], players[0] };
            if (kind == "duplicate") players[1].PlayerId = "p0";
            if (kind == "blank") players[1].PlayerId = " ";
            if (kind == "null") players[1] = null;
            await AssertRefused(await allocation.Allocate("m", players, Now), "distinct");
            Assert.That(reads, Is.Empty);
        }

        [TestCase("protocol", 0)]
        [TestCase("sim", 0)]
        [TestCase("content", 0)]
        [TestCase("protocol", 1)]
        [TestCase("sim", 1)]
        [TestCase("content", 1)]
        public async Task MissingCustomDataIsAnErrorOnEitherPlayer(string field, int index)
        {
            var players = SdkPlayers();
            ((JObject)players[index].CustomData).Remove(field);
            var response = await module.Allocate(null, Request(players));
            Assert.That(response.Status, Is.EqualTo(AllocateStatus.Error));
            StringAssert.Contains("custom data", response.Message);
            Assert.That(matches.Writes, Is.Zero);
            Assert.That((await matches.ReadAsync("m")).Record, Is.Null);
        }

        [TestCase("protocol", "-1")]
        [TestCase("protocol", "65536")]
        [TestCase("sim", "1.5")]
        [TestCase("sim", "true")]
        [TestCase("content", "2147483648")]
        [TestCase("content", "null")]
        [TestCase("content", "{}")]
        [TestCase("content", "[]")]
        [TestCase("protocol", "\"not-a-number\"")]
        public async Task MalformedCustomDataDoesNotBecomeADefault(string field, string json)
        {
            var players = SdkPlayers();
            foreach (var player in players) ((JObject)player.CustomData)[field] = JToken.Parse(json);
            var response = await module.Allocate(null, Request(players));
            Assert.That(response.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(response.Message, Is.Not.Empty);
            Assert.That(matches.Writes, Is.Zero);
            Assert.That((await matches.ReadAsync("m")).Record, Is.Null);
        }

        [TestCase("typed")]
        [TestCase("json")]
        [TestCase("element")]
        public async Task SdkAdapterAcceptsNumbersAndStringsAndReturnsCustomAssignment(string representation)
        {
            var players = SdkPlayers();
            players[1] = new Player("p1", new Dictionary<string, object>
            {
                ["protocol"] = ProtocolVersion.Current.ToString(), ["sim"] = 1.0,
                ["content"] = content.ToString(CultureInfo.InvariantCulture),
                ["arena"] = 1000, ["rating"] = -1000
            });
            object raw = players;
            if (representation == "json") raw = JArray.FromObject(players);
            if (representation == "element")
                raw = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(JsonConvert.SerializeObject(players));
            var request = Request(raw);
            var response = await module.Allocate(null, request);
            Assert.That(response.Status, Is.EqualTo(AllocateStatus.Created), response.Message);
            Assert.That(response.AllocationData["matchId"], Is.EqualTo("m"));
            var record = (await matches.ReadAsync("m")).Record;
            Assert.That(record.protocol, Is.EqualTo(ProtocolVersion.Current));
            Assert.That(record.sim, Is.EqualTo(1));
            Assert.That(record.content, Is.EqualTo(content));
            Assert.That(record.players[1].Rank.Arena, Is.Zero);
            Assert.That(record.players[1].Rating.R, Is.EqualTo(1500));
            Assert.That(record.createdUnixSeconds, Is.EqualTo(Now));
            // Retry through the SDK boundary must not even need the old ticket data.
            Assert.That((await module.Allocate(null, new AllocateRequest("m", null))).Status,
                Is.EqualTo(AllocateStatus.Created));
            Assert.That(matches.Writes, Is.EqualTo(1));

            var poll = await module.Poll(null, new PollRequest("m", response.AllocationData, DateTimeOffset.MinValue));
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated), poll.Message);
            Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.Custom));
            Assert.That(poll.AssignmentData.CustomData["matchId"], Is.EqualTo("m"));
            Assert.That(poll.AssignmentData.Ip, Is.Null);
            Assert.That(poll.AssignmentData.Port, Is.Zero);
        }

        [Test]
        public async Task PollBeforeAllocationReturnsErrorAndDoesNotCreateAnything()
        {
            var response = await module.Poll(null, new PollRequest("m",
                new Dictionary<string, object> { ["matchId"] = "different" }, DateTimeOffset.MinValue));
            Assert.That(response.Status, Is.EqualTo(PollStatus.Error));
            Assert.That(response.Message, Does.Contain("does not exist"));
            Assert.That(response.AssignmentData, Is.Null);
            Assert.That(matches.Writes, Is.Zero);
            Assert.That(reads, Is.Empty);
        }

        [Test]
        public async Task NullSdkRequestsReturnErrors()
        {
            Assert.That((await module.Allocate(null, null)).Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That((await module.Poll(null, null)).Status, Is.EqualTo(PollStatus.Error));
            Assert.That(matches.Writes, Is.Zero);
        }

        [Test]
        public async Task StorageFailureDoesNotReportCreatedOrAllocated()
        {
            var failing = new MatchmakerAllocatorModule(_ => throw new InvalidOperationException("private detail"),
                (_, id) => ReadPlayer(id), balances, () => Now, (_, id) => claims[id]);
            var create = await failing.Allocate(null, Request(SdkPlayers()));
            var poll = await failing.Poll(null, new PollRequest("m", null, DateTimeOffset.MinValue));
            Assert.That(create.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Error));
            Assert.That(create.Message, Does.Not.Contain("private detail"));
            Assert.That(poll.Message, Does.Not.Contain("private detail"));
        }

        [Test]
        public async Task ConcurrentAllocationForSamePlayerLosesClaimAndCreatesNoRecord()
        {
            claims["p0"].BeforeClaimWrite = async () =>
                Assert.That((await allocation.Allocate("winner", Roster(), Now)).ok, Is.True);
            Assert.That((await allocation.Allocate("loser", Roster(), Now)).ok, Is.False);
            Assert.That((await matches.ReadAsync("loser")).Record, Is.Null);
            Assert.That(states["p0"].ActiveMatch.matchId, Is.EqualTo("winner"));
            Assert.That(states["p1"].ActiveMatch.matchId, Is.EqualTo("winner"));
        }

        [Test]
        public async Task ExpiredClaimAllowsNewMatchAtExactExpiry()
        {
            await allocation.Allocate("old", Roster(), Now);
            long expiry = Now + ActiveMatchClaims.LifetimeSeconds;
            Assert.That(states["p0"].ActiveMatch.expiresUnixSeconds, Is.EqualTo(expiry));
            Assert.That((await allocation.Allocate("new", Roster(), expiry)).ok, Is.True);
            Assert.That(states["p0"].ActiveMatch.matchId, Is.EqualTo("new"));
            Assert.That(states["p1"].ActiveMatch.expiresUnixSeconds, Is.EqualTo(expiry + ActiveMatchClaims.LifetimeSeconds));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task SecondClaimFailureRollsBackFirst(bool throws)
        {
            if (throws) claims["p1"].BeforeClaimWrite = () => throw new InvalidOperationException("storage unavailable");
            else states["p1"].ActiveMatch = new ActiveMatchRecord { matchId = "other", expiresUnixSeconds = Now + 100 };
            Assert.That((await allocation.Allocate("m", Roster(), Now)).ok, Is.False);
            Assert.That((await matches.ReadAsync("m")).Record, Is.Null);
            Assert.That(states["p0"].ActiveMatch.matchId, Is.Null);
            if (!throws) Assert.That(states["p1"].ActiveMatch.matchId, Is.EqualTo("other"));
        }

        [Test]
        public async Task SameMatchRetryPreservesClaimExpiry()
        {
            await allocation.Allocate("m", Roster(), Now);
            await allocation.Allocate("m", Roster(), Now + 100);
            foreach (var state in states.Values)
            {
                Assert.That(state.ActiveMatch.matchId, Is.EqualTo("m"));
                Assert.That(state.ActiveMatch.expiresUnixSeconds, Is.EqualTo(Now + ActiveMatchClaims.LifetimeSeconds));
            }
        }

        [Test]
        public async Task RetryAfterCrashWithOneClaimKeepsOriginalLeaseForBothPlayers()
        {
            await ActiveMatchClaims.Claim(claims["p0"], "m", Now);
            Assert.That((await allocation.Allocate("m", Roster(), Now + 50)).ok, Is.True);
            var record = (await matches.ReadAsync("m")).Record;
            Assert.That(record.createdUnixSeconds, Is.EqualTo(Now));
            foreach (var state in states.Values)
                Assert.That(state.ActiveMatch.expiresUnixSeconds,
                    Is.EqualTo(record.createdUnixSeconds + ActiveMatchClaims.LifetimeSeconds));
        }

        [TestCase("p0")]
        [TestCase("p1")]
        public async Task AllocateModuleRefusesEitherBlockedPlayer(string id)
        {
            states[id].Discipline = new DisciplineRecord { BlockedUntilUnixSeconds = Now + 1 };
            var result = await module.Allocate(null, Request(SdkPlayers()));
            Assert.That(result.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(result.Message, Does.Contain("temporarily blocked"));
            Assert.That(matches.Writes, Is.Zero);
            foreach (var state in states.Values) Assert.That(state.ActiveMatch?.matchId, Is.Null);
        }

        [TestCase(-1)]
        [TestCase(0)]
        public async Task AllocateAcceptsOnceBlockHasPassed(long delta)
        {
            states["p0"].Discipline = new DisciplineRecord { Level = 7, BlockedUntilUnixSeconds = Now + delta };
            var result = await module.Allocate(null, Request(SdkPlayers()));
            Assert.That(result.Status, Is.EqualTo(AllocateStatus.Created));
            Assert.That(matches.Writes, Is.EqualTo(1));
        }

        [Test]
        public async Task BlockArrivingDuringClaimAcquisitionRefusesAndReleasesClaims()
        {
            claims["p0"].BeforeClaimWrite = () =>
            {
                states["p0"].Discipline = new DisciplineRecord { BlockedUntilUnixSeconds = Now + 120 };
                return Task.CompletedTask;
            };
            await AssertRefused(await allocation.Allocate("m", Roster(), Now), "temporarily blocked");
            foreach (var state in states.Values) Assert.That(state.ActiveMatch?.matchId, Is.Null);
        }

        private Task<PlayerState> ReadPlayer(string id)
        {
            reads.Add(id);
            return Task.FromResult(states[id]);
        }

        private AllocationPlayer[] Roster() => new[]
        {
            new AllocationPlayer { PlayerId = "p0", Protocol = ProtocolVersion.Current, Sim = 1, Content = content },
            new AllocationPlayer { PlayerId = "p1", Protocol = ProtocolVersion.Current, Sim = 1, Content = content }
        };

        private List<Player> SdkPlayers() => new List<Player>
        {
            new Player("p0", new JObject { ["protocol"] = ProtocolVersion.Current, ["sim"] = 1, ["content"] = content }),
            new Player("p1", new JObject { ["protocol"] = ProtocolVersion.Current, ["sim"] = 1, ["content"] = content })
        };

        private static AllocateRequest Request(object players) => new AllocateRequest("m",
            new MatchmakingResults(null, "m", "pool-id", "ranked-pool", "ranked",
                new Dictionary<string, object> { ["Players"] = players }));

        private async Task AssertRefused(MatchAllocationResult result, string reason)
        {
            Assert.That(result.ok, Is.False);
            StringAssert.Contains(reason, result.error);
            Assert.That(matches.Writes, Is.Zero);
            Assert.That((await matches.ReadAsync("m")).Record, Is.Null);
        }

        private sealed class ClaimStore : ISettlementPlayerStore
        {
            private readonly PlayerState state;
            private int version = 1;
            public Func<Task> BeforeClaimWrite;
            public ClaimStore(PlayerState state) { this.state = state; }
            public Task<LockedPlayerState> ReadForSettlementAsync() => Task.FromResult(new LockedPlayerState(
                JsonConvert.DeserializeObject<PlayerState>(JsonConvert.SerializeObject(state)),
                new Dictionary<string, string> { [PlayerStateKeys.Rating] = version.ToString() }));
            public async Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read)
            {
                var hook = BeforeClaimWrite;
                BeforeClaimWrite = null;
                if (hook != null) await hook();
                if (read.WriteLocks[PlayerStateKeys.Rating] != version.ToString())
                    throw new RecordConflictException("Claim changed.");
                state.ActiveMatch = claim;
                version++;
            }
            public Task WriteForSettlementAsync(PlayerState value, IReadOnlyDictionary<string, string> tokens) =>
                throw new NotSupportedException();
        }

        private sealed class CountingStore : IMatchRecordStore
        {
            private readonly InMemoryMatchRecordStore inner = new InMemoryMatchRecordStore();
            public int Writes;
            public string LastLock;
            public Task<LockedMatchRecord> ReadAsync(string id) => inner.ReadAsync(id);
            public Task<MatchPresence[]> ReadPresenceAsync(string id) => inner.ReadPresenceAsync(id);
            public Task WritePresenceAsync(string id, int slot, MatchPresence presence) => inner.WritePresenceAsync(id, slot, presence);
            public async Task WriteAsync(MatchRecord record, string expectedWriteLock)
            {
                await inner.WriteAsync(record, expectedWriteLock);
                Writes++;
                LastLock = expectedWriteLock;
            }
            public Task SaveLog(string id, int playerIndex, string base64) => inner.SaveLog(id, playerIndex, base64);
        }
    }
}
