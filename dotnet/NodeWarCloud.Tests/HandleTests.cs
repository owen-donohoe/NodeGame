using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class HandleTests
    {
        [TestCase("Ab_c-09", true)]
        [TestCase("ab", false)]
        [TestCase("abcdefghijklmnop", true)]
        [TestCase("abcdefghijklmnopq", false)]
        [TestCase("abc def", false)]
        [TestCase("ÃƒÆ’Ã‚Â©clair", false)]
        [TestCase(null, false)]
        public void NameFormat(string name, bool valid) => Assert.That(HandleRules.IsValidName(name), Is.EqualTo(valid));

        [TestCase("a1", "A1", true)]
        [TestCase("1234", "1234", true)]
        [TestCase("12345", "12345", false)]
        [TestCase("a_", "A_", false)]
        [TestCase("", "", false)]
        public void TagFormatAndNormalization(string input, string normalized, bool valid)
        {
            Assert.That(HandleRules.NormalizeTag(input), Is.EqualTo(normalized));
            Assert.That(HandleRules.IsValidTag(normalized), Is.EqualTo(valid));
            Assert.That(HandleRules.NormalizeName("Player-One"), Is.EqualTo("player-one"));
            Assert.That(HandleRules.Display(new ProfileRecord { Name = "Player", Tag = "A1" }), Is.EqualTo("Player#A1"));
        }

        [TestCase("xx4d_m1nxx")]
        [TestCase("m0d3r470r")]
        [TestCase("n0d3-w4r")]
        [TestCase("0ff1c14l")]
        [TestCase("5upp0r7")]
        public void BlockedNamesFoldLeetAndSeparators(string name) =>
            Assert.That(HandleRules.IsBlocked(name, EmbeddedBlockedNames.Names), Is.True);

        [Test]
        public void BlocklistParsesCommentsAndBlankLines()
        {
            Assert.That(EmbeddedBlockedNames.Parse(" # comment\r\n\nAdmin\n support \n"),
                Is.EqualTo(new[] { "admin", "support" }));
            Assert.That(HandleRules.IsBlocked("player", EmbeddedBlockedNames.Names), Is.False);
        }

        [Test]
        public void ShardingUsesFnv1aAndLowercaseHex()
        {
            Assert.That(HandleRegistry.Shard("hello"), Is.EqualTo("2b")); // FNV-1a 0x4f9f2cab
            for (int i = 0; i < 100; i++)
                Assert.That(Convert.ToInt32(HandleRegistry.Shard("player" + i), 16), Is.InRange(0, 63));
            Assert.That(HandleRegistry.Key("player", "AB"), Is.EqualTo("player#AB"));
        }

        private static T Copy<T>(T value) => value == null ? default(T) :
            JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

        private sealed class ProfileStore : IProfileStore
        {
            public ProfileRecord Profile;
            public WalletRecord Wallet = new WalletRecord { Premium = 100 };
            private int profileVersion = 1, walletVersion = 1, ratingVersion = 1;
            private int readRatingVersion;
            private bool readAbsent;
            public int Writes, WalletWrites;
            public Action BeforeWrite;

            public Task<(ProfileRecord Profile, string ProfileLock, WalletRecord Wallet, string WalletLock)> ReadProfileWalletLockedAsync()
            {
                readRatingVersion = ratingVersion;
                readAbsent = Profile == null;
                return Task.FromResult((Copy(Profile), Profile == null ? null : profileVersion.ToString(),
                    Copy(Wallet), Wallet == null ? null : walletVersion.ToString()));
            }

            public Task WriteProfileWalletLockedAsync(ProfileRecord profile, string profileLock, WalletRecord wallet, string walletLock)
            {
                var hook = BeforeWrite;
                BeforeWrite = null;
                hook?.Invoke();
                if ((profileLock == null && (!readAbsent || Profile != null || ratingVersion != readRatingVersion)) ||
                    (profileLock != null && profileLock != profileVersion.ToString()) ||
                    (wallet != null && walletLock != walletVersion.ToString())) throw new RecordConflictException("Changed.");
                if (profileLock == null) ratingVersion++;
                Profile = Copy(profile);
                profileVersion++;
                if (wallet != null) { Wallet = Copy(wallet); walletVersion++; WalletWrites++; }
                Writes++;
                return Task.CompletedTask;
            }

            public void ConcurrentGrant() { Wallet.Premium += 10; walletVersion++; }
            public Task<PlayerState> ReadAsync() => Task.FromResult(new PlayerState
            {
                Profile = Copy(Profile), Wallet = Copy(Wallet), Rating = PlayerStateDefaults.Rating(),
                Rank = PlayerStateDefaults.Rank(), Inventory = PlayerStateDefaults.Inventory(), History = PlayerStateDefaults.History()
            });
            public Task WriteAsync(PlayerState state) => throw new InvalidOperationException("Only locked profile writes are allowed.");
        }

        private sealed class Registry : IHandleRegistryStore
        {
            public readonly Dictionary<string, Dictionary<string, HandleReservation>> Shards = new Dictionary<string, Dictionary<string, HandleReservation>>();
            private readonly Dictionary<string, int> versions = new Dictionary<string, int>();
            public int Writes;
            public Func<Task> BeforeWrite;
            public bool FailWrites;

            public Task<(Dictionary<string, HandleReservation> Entries, string WriteLock)> ReadShardAsync(string shard) =>
                Task.FromResult(Shards.TryGetValue(shard, out var entries)
                    ? (Copy(entries), versions[shard].ToString())
                    : ((Dictionary<string, HandleReservation>)null, (string)null));

            public async Task WriteShardAsync(string shard, Dictionary<string, HandleReservation> entries, string expectedWriteLock)
            {
                if (string.IsNullOrEmpty(expectedWriteLock)) throw new InvalidOperationException("Lock required.");
                var hook = BeforeWrite;
                BeforeWrite = null;
                if (hook != null) await hook();
                if (FailWrites || !versions.TryGetValue(shard, out int version) || version.ToString() != expectedWriteLock)
                    throw new RecordConflictException("Shard changed.");
                Shards[shard] = Copy(entries);
                versions[shard]++;
                Writes++;
            }

            public Task<int> CreateEmptyShardsAsync()
            {
                int count = 0;
                for (int i = 0; i < HandleRegistry.ShardCount; i++)
                {
                    string shard = i.ToString("x2");
                    if (Shards.ContainsKey(shard)) continue;
                    Shards[shard] = new Dictionary<string, HandleReservation>();
                    versions[shard] = 1;
                    count++;
                }
                return Task.FromResult(count);
            }

            public string Owner(string name, string tag = "A") => Shards[HandleRegistry.Shard(name)].TryGetValue(
                HandleRegistry.Key(name, tag), out var owner) ? owner.PlayerId : null;
        }

        private sealed class Admins : IAdminDirectory
        {
            public Task<bool> IsAdminAsync(string playerId) => Task.FromResult(playerId == "boss");
        }

        private Registry registry;
        private Dictionary<string, ProfileStore> players;
        private HandleService handles;

        [SetUp]
        public async Task SetUp()
        {
            registry = new Registry();
            await registry.CreateEmptyShardsAsync();
            players = new Dictionary<string, ProfileStore>
                { ["p1"] = new ProfileStore(), ["p2"] = new ProfileStore() };
            handles = new HandleService(registry, id => players[id], EmbeddedBlockedNames.Names, () => "AUTO");
        }

        [Test]
        public async Task FirstHandleIsFreeAndPublicProfileHidesPrivateFields()
        {
            Assert.That(await handles.GetPublicProfileAsync("p1"), Is.Null);
            var state = await handles.SetHandleAsync("p1", "Player", "a", "r1", () => 100);
            Assert.That(state.Profile.NameNorm, Is.EqualTo("player"));
            Assert.That(state.Profile.Tag, Is.EqualTo("A"));
            Assert.That(state.Profile.LastFreeRenameUnixSeconds, Is.Zero);
            Assert.That(state.Wallet.Premium, Is.EqualTo(100));
            Assert.That(state.Rating, Is.Not.Null);
            Assert.That(players["p1"].WalletWrites, Is.Zero);
            Assert.That(registry.Owner("player"), Is.EqualTo("p1"));
            var profile = await handles.GetPublicProfileAsync("p1");
            Assert.That(profile.Name, Is.EqualTo("Player"));
            Assert.That(profile.Tag, Is.EqualTo("A"));
            Assert.That(profile.NameNorm, Is.Null);
            Assert.That(profile.LastFreeRenameUnixSeconds, Is.Zero);
        }

        [Test]
        public async Task CasingOnlyIsFreeWithoutRegistryWrites()
        {
            await handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            int writes = registry.Writes;
            var state = await handles.SetHandleAsync("p1", "SECOND", "a", "r3", () => 201);
            Assert.That(state.Profile.Name, Is.EqualTo("SECOND"));
            Assert.That(state.Profile.LastFreeRenameUnixSeconds, Is.EqualTo(200));
            Assert.That(state.Wallet.Premium, Is.EqualTo(100));
            Assert.That(players["p1"].WalletWrites, Is.Zero);
            Assert.That(registry.Writes, Is.EqualTo(writes));
        }

        [Test]
        public async Task FreeRenameThenPaidRenameAndCooldownExpiry()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            var paid = await handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201);
            Assert.That(paid.Wallet.Premium, Is.EqualTo(50));
            Assert.That(paid.Wallet.Recent.Single().Reason, Is.EqualTo("rename"));
            Assert.That(paid.Wallet.Recent.Single().Id, Is.EqualTo("r3"));
            Assert.That(paid.Profile.LastFreeRenameUnixSeconds, Is.EqualTo(200));
            var free = await handles.SetHandleAsync("p1", "Fourth", "A", "r4", () => 200 + HandleRules.FreeRenameIntervalSeconds);
            Assert.That(free.Wallet.Premium, Is.EqualTo(50));
            Assert.That(free.Profile.LastFreeRenameUnixSeconds, Is.EqualTo(200 + HandleRules.FreeRenameIntervalSeconds));
        }

        [Test]
        public async Task InsufficientPremiumWritesNothing()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            players["p1"].Wallet.Premium = 49;
            int writes = players["p1"].Writes, registryWrites = registry.Writes;
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201));
            Assert.That(players["p1"].Writes, Is.EqualTo(writes));
            Assert.That(registry.Writes, Is.EqualTo(registryWrites));
            Assert.That(players["p1"].Wallet.Recent, Is.Empty);
            Assert.That(players["p1"].Profile.Name, Is.EqualTo("Second"));
        }

        [Test]
        public async Task RegistryCasRaceHasExactlyOneWinner()
        {
            registry.BeforeWrite = async () => { await handles.SetHandleAsync("p2", "Player", "A", "r2", () => 100); };
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100));
            Assert.That(players["p1"].Profile, Is.Null);
            Assert.That(players["p2"].Profile.Name, Is.EqualTo("Player"));
            Assert.That(registry.Owner("player"), Is.EqualTo("p2"));
            await Task.CompletedTask;
        }

        [Test]
        public async Task StaleReservationIsReclaimedAndOldKeyReleased()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            registry.Shards[HandleRegistry.Shard("second")]["second#A"] = new HandleReservation { PlayerId = "p2", ReservedAtUnixSeconds = 100 };
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 401);
            Assert.That(registry.Owner("second"), Is.EqualTo("p1"));
            Assert.That(registry.Owner("first"), Is.Null);
        }

        [Test]
        public void UninitializedRegistryRefusesWithoutWritingProfile()
        {
            var missing = new HandleService(new Registry(), id => players[id], EmbeddedBlockedNames.Names);
            var error = Assert.ThrowsAsync<HandleRefusedException>(() => missing.SetHandleAsync("p1", "Player", "A", "r1", () => 100));
            Assert.That(error.Message, Does.Contain("registry not initialized"));
            Assert.That(players["p1"].Writes, Is.Zero);
        }

        [Test]
        public async Task ProfileWalletConflictRollsBackReservationAndRetriesFreshWallet()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            players["p1"].BeforeWrite = () => players["p1"].ConcurrentGrant();
            var state = await handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201);
            Assert.That(state.Wallet.Premium, Is.EqualTo(60));
            Assert.That(state.Wallet.Recent, Has.Count.EqualTo(1));
            Assert.That(registry.Owner("third"), Is.EqualTo("p1"));
            Assert.That(registry.Owner("second"), Is.Null);
        }

        [Test]
        public async Task ExhaustedProfileConflictsReleaseReservationAndLeaveOldProfile()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            Action conflict = null;
            conflict = () => { players["p1"].ConcurrentGrant(); players["p1"].BeforeWrite = conflict; };
            players["p1"].BeforeWrite = conflict;
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201));
            Assert.That(registry.Owner("third"), Is.Null);
            Assert.That(players["p1"].Profile.Name, Is.EqualTo("Second"));
            Assert.That(players["p1"].Wallet.Recent, Is.Empty);
        }

        [Test]
        public async Task FailedOldKeyCleanupLeavesReclaimableEntry()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            players["p1"].BeforeWrite = () => registry.FailWrites = true;
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            Assert.That(registry.Owner("first"), Is.EqualTo("p1"));
            registry.FailWrites = false;
            await handles.SetHandleAsync("p2", "First", "A", "r3", () => 401);
            Assert.That(registry.Owner("first"), Is.EqualTo("p2"));
        }

        [Test]
        public async Task AutomaticTagsRetryTakenCandidatesUpToTenTimes()
        {
            await handles.SetHandleAsync("p2", "Player", "A", "r1", () => 100);
            int calls = 0;
            var auto = new HandleService(registry, id => players[id], EmbeddedBlockedNames.Names,
                () => ++calls < 10 ? "A" : "FREE");
            var state = await auto.SetHandleAsync("p1", "Player", "", "r2", () => 100);
            Assert.That(state.Profile.Tag, Is.EqualTo("FREE"));
            Assert.That(calls, Is.EqualTo(10));
            calls = 0;
            auto = new HandleService(registry, id => players[id], EmbeddedBlockedNames.Names, () => { calls++; return "A"; });
            Assert.ThrowsAsync<HandleRefusedException>(() => auto.SetHandleAsync("p1", "Player", null, "r3", () => 100));
            Assert.That(calls, Is.EqualTo(10));
        }

        [Test]
        public async Task PaidRequestCannotBeReusedForAnotherRename()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            await handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201);
            var repeat = await handles.SetHandleAsync("p1", "Third", "A", "r3", () => 201);
            Assert.That(repeat.Wallet.Premium, Is.EqualTo(50));
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Fourth", "A", "r3", () => 202));
        }

        [Test]
        public void InvalidAndBlockedInputsWriteNothing()
        {
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "ab", "A", "r", () => 100));
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Player", "A_B", "r", () => 100));
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "4d_m1n", "A", "r", () => 100));
            Assert.ThrowsAsync<HandleRefusedException>(() => handles.SetHandleAsync("p1", "Player", "A", "", () => 100));
            Assert.That(players["p1"].Writes, Is.Zero);
            Assert.That(registry.Writes, Is.Zero);
        }

        [Test]
        public async Task AdminSetIsFreeBypassesBlocklistAndPreservesCooldownButValidatesFormat()
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            var admin = new AdminHandleService(new Admins(), handles, registry);
            var state = await admin.SetHandleAsync("boss", "p1", "Admin", "a", () => 201);
            Assert.That(state.Profile.Name, Is.EqualTo("Admin"));
            Assert.That(state.Profile.LastFreeRenameUnixSeconds, Is.EqualTo(200));
            Assert.That(state.Wallet.Premium, Is.EqualTo(100));
            Assert.ThrowsAsync<HandleRefusedException>(() => admin.SetHandleAsync("boss", "p1", "ab", "A", () => 202));
            Assert.ThrowsAsync<HandleRefusedException>(() => admin.SetHandleAsync("boss", "p1", "Player", "*", () => 202));
            Assert.ThrowsAsync<HandleRefusedException>(() => admin.SetHandleAsync("boss", "", "Player", "A", () => 202));
        }

        [Test]
        public async Task AdminInitCreatesOnlyMissingShardsAndNonAdminIsRefused()
        {
            var empty = new Registry();
            var admin = new AdminHandleService(new Admins(), handles, empty);
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.InitRegistryAsync("p1"));
            Assert.That(empty.Shards, Is.Empty);
            Assert.That(await admin.InitRegistryAsync("boss"), Is.EqualTo(64));
            empty.Shards["00"]["held#A"] = new HandleReservation { PlayerId = "p1", ReservedAtUnixSeconds = 100 };
            Assert.That(await admin.InitRegistryAsync("boss"), Is.Zero);
            Assert.That(empty.Shards["00"]["held#A"].PlayerId, Is.EqualTo("p1"));
            empty.Shards.Remove("01");
            Assert.That(await admin.InitRegistryAsync("boss"), Is.EqualTo(1));
            Assert.That(empty.Shards["00"]["held#A"].PlayerId, Is.EqualTo("p1"));
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => admin.SetHandleAsync("p1", "p2", "Admin", "A", () => 100));
            Assert.That(players["p2"].Writes, Is.Zero);
        }

        [Test]
        public async Task LocalStoreIncludesProfileInWholeState()
        {
            var store = new InMemoryPlayerRecordStore();
            await store.WriteAsync(new PlayerState { Profile = new ProfileRecord { Name = "Player", Tag = "A", NameNorm = "player" } });
            Assert.That((await store.ReadAsync()).Profile.Name, Is.EqualTo("Player"));
        }

        [Test]
        public void CloudRegistryRejectsMissingLocksBeforeAnyIo()
        {
            var cloud = new CloudSaveHandleRegistryStore(null, null);
            Assert.ThrowsAsync<InvalidOperationException>(() => cloud.WriteShardAsync("00", new Dictionary<string, HandleReservation>(), null));
            Assert.ThrowsAsync<InvalidOperationException>(() => cloud.WriteShardAsync("00", new Dictionary<string, HandleReservation>(), ""));
        }

        [Test]
        public void CloudProfileRejectsUnfencedCreationAndUnlockedWalletBeforeAnyIo()
        {
            var cloud = new CloudSavePlayerRecordStore(null, null, "p1");
            var profile = new ProfileRecord { Name = "Player", NameNorm = "player", Tag = "A" };
            Assert.ThrowsAsync<InvalidOperationException>(() => cloud.WriteProfileWalletLockedAsync(profile, null, null, null));
            Assert.ThrowsAsync<InvalidOperationException>(() => cloud.WriteProfileWalletLockedAsync(profile, "", null, null));
            Assert.ThrowsAsync<InvalidOperationException>(() => cloud.WriteProfileWalletLockedAsync(profile, "lock", new WalletRecord(), null));
        }

        [Test]
        public async Task ChangingOnlyTagReleasesOldKeyWithinSameShard()
        {
            await handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100);
            var state = await handles.SetHandleAsync("p1", "Player", "B", "r2", () => 200);
            Assert.That(state.Profile.LastFreeRenameUnixSeconds, Is.EqualTo(200));
            Assert.That(registry.Owner("player", "A"), Is.Null);
            Assert.That(registry.Owner("player", "B"), Is.EqualTo("p1"));
        }

        [TestCase(299, false)]
        [TestCase(300, false)]
        [TestCase(301, true)]
        public async Task UnheldReservationIsReclaimedOnlyAfterGrace(long age, bool reclaim)
        {
            registry.Shards[HandleRegistry.Shard("player")]["player#A"] = new HandleReservation
                { PlayerId = "p2", ReservedAtUnixSeconds = 100 };
            if (reclaim)
            {
                await handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100 + age);
                Assert.That(registry.Owner("player"), Is.EqualTo("p1"));
                Assert.That(players["p1"].Profile.Name, Is.EqualTo("Player"));
            }
            else
            {
                var error = Assert.ThrowsAsync<HandleRefusedException>(() =>
                    handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100 + age));
                Assert.That(error.Message, Does.Contain("taken"));
                Assert.That(registry.Owner("player"), Is.EqualTo("p2"));
                Assert.That(players["p1"].Writes, Is.Zero);
            }
        }

        [Test]
        public async Task HoldingProfilePreventsReclaimEvenAfterGrace()
        {
            await handles.SetHandleAsync("p2", "Player", "A", "r1", () => 100);
            Assert.ThrowsAsync<HandleRefusedException>(() =>
                handles.SetHandleAsync("p1", "Player", "A", "r2", () => 1000));
            Assert.That(registry.Owner("player"), Is.EqualTo("p2"));
            Assert.That(players["p1"].Writes, Is.Zero);
        }

        [TestCase(150, false)]
        [TestCase(151, true)]
        public async Task SlowReservationRetriesBeforeCommitWithoutDoubleCharging(long delay, bool retry)
        {
            await handles.SetHandleAsync("p1", "First", "A", "r1", () => 100);
            await handles.SetHandleAsync("p1", "Second", "A", "r2", () => 200);
            long now = 201;
            registry.BeforeWrite = () => { now += delay; return Task.CompletedTask; };
            var state = await handles.SetHandleAsync("p1", "Third", "A", "r3", () => now);
            var reservation = registry.Shards[HandleRegistry.Shard("third")]["third#A"];
            Assert.That(reservation.ReservedAtUnixSeconds, Is.EqualTo(retry ? now : 201));
            Assert.That(state.Wallet.Premium, Is.EqualTo(50));
            Assert.That(players["p1"].WalletWrites, Is.EqualTo(1));
            Assert.That(players["p1"].Writes, Is.EqualTo(3));
            Assert.That(state.Wallet.Recent.Single().AtUnixSeconds, Is.EqualTo(retry ? now : 201));
            Assert.That(registry.Owner("second"), Is.Null);
        }

        [Test]
        public async Task PendingReservationMustNotBeReclaimedBeforeProfileCommit()
        {
            // p1 reserves; p2 sees no committed p1 profile but must respect its fresh reservation.
            // The synchronous profile hook launches p2 to completion while p1 is pending.
            Task<PlayerState> second = null;
            players["p1"].BeforeWrite = () => second = handles.SetHandleAsync("p2", "Player", "A", "r2", () => 100);
            await handles.SetHandleAsync("p1", "Player", "A", "r1", () => 100);
            Assert.ThrowsAsync<HandleRefusedException>(() => second);
            Assert.That(players.Values.Count(p => p.Profile?.NameNorm == "player" && p.Profile.Tag == "A"), Is.EqualTo(1));
        }
    }
}
