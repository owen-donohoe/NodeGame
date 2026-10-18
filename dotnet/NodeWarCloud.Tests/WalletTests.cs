using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class WalletTests
    {
        /// <summary>One wallet with a version that every write bumps, as Cloud Save's write lock does.</summary>
        private sealed class WalletStore : IWalletStore
        {
            public WalletRecord Stored;
            private int version = 1;
            public int Writes;
            // Fires once before the lock check, to model a concurrent write landing between read and write.
            public Action BeforeWrite;

            public Task<(WalletRecord Wallet, string WriteLock)> ReadWalletLockedAsync() =>
                Task.FromResult((Stored == null ? null : Copy(Stored), Stored == null ? null : version.ToString()));

            public Task WriteWalletLockedAsync(WalletRecord wallet, string expectedWriteLock)
            {
                var hook = BeforeWrite;
                BeforeWrite = null;
                hook?.Invoke();
                if (expectedWriteLock != version.ToString()) throw new RecordConflictException("Wallet changed.");
                Stored = Copy(wallet);
                version++;
                Writes++;
                return Task.CompletedTask;
            }

            public void ConcurrentGrant(long coins)
            {
                Stored.Coins += coins;
                version++;
            }
        }

        private sealed class Admins : IAdminDirectory
        {
            private readonly HashSet<string> ids;
            public Admins(params string[] ids) { this.ids = new HashSet<string>(ids); }
            public Task<bool> IsAdminAsync(string playerId) => Task.FromResult(playerId != null && ids.Contains(playerId));
        }

        private static WalletRecord Copy(WalletRecord wallet) =>
            JsonConvert.DeserializeObject<WalletRecord>(JsonConvert.SerializeObject(wallet));

        private static WalletTransaction Tx(string id, long coins = 0, long premium = 0) =>
            new WalletTransaction { Id = id, Reason = "test", Coins = coins, Premium = premium, AtUnixSeconds = 100 };

        [Test]
        public void Ledger_AppliesSignedAmounts_AndRecordsTheTransaction()
        {
            var wallet = WalletLedger.Starting();
            Assert.That(WalletLedger.Apply(wallet, Tx("a", coins: 250, premium: -5)), Is.EqualTo(WalletApplyResult.Applied));
            Assert.That(wallet.Coins, Is.EqualTo(WalletLedger.StartingCoins + 250));
            Assert.That(wallet.Premium, Is.EqualTo(WalletLedger.StartingPremium - 5));
            Assert.That(wallet.Recent.Single().Id, Is.EqualTo("a"));
        }

        [Test]
        public void Ledger_RepeatedRequestId_AppliesOnce()
        {
            var wallet = WalletLedger.Starting();
            WalletLedger.Apply(wallet, Tx("a", coins: 10));
            Assert.That(WalletLedger.Apply(wallet, Tx("a", coins: 10)), Is.EqualTo(WalletApplyResult.Duplicate));
            Assert.That(wallet.Coins, Is.EqualTo(WalletLedger.StartingCoins + 10));
            Assert.That(wallet.Recent, Has.Count.EqualTo(1));
        }

        [Test]
        public void Ledger_RefusesBelowZero_AndOverflow_WithoutChanging()
        {
            var wallet = new WalletRecord { Coins = 5, Premium = 1 };
            Assert.That(WalletLedger.Apply(wallet, Tx("a", coins: -6)), Is.EqualTo(WalletApplyResult.Insufficient));
            Assert.That(WalletLedger.Apply(wallet, Tx("b", coins: 10, premium: -2)), Is.EqualTo(WalletApplyResult.Insufficient));
            Assert.That(WalletLedger.Apply(wallet, Tx("c", coins: long.MaxValue)), Is.EqualTo(WalletApplyResult.Insufficient));
            Assert.That(wallet.Coins, Is.EqualTo(5));
            Assert.That(wallet.Premium, Is.EqualTo(1));
            Assert.That(wallet.Recent, Is.Empty);
        }

        [Test]
        public void Ledger_KeepsOnlyTheNewestTransactions()
        {
            var wallet = new WalletRecord();
            for (int i = 0; i < WalletLedger.RecentLimit + 5; i++) WalletLedger.Apply(wallet, Tx("t" + i, coins: 1));
            Assert.That(wallet.Recent, Has.Count.EqualTo(WalletLedger.RecentLimit));
            Assert.That(wallet.Recent.First().Id, Is.EqualTo("t5"));
            Assert.That(wallet.Recent.Last().Id, Is.EqualTo("t" + (WalletLedger.RecentLimit + 4)));
        }

        [Test]
        public void Ledger_RequiresRequestIdAndReason()
        {
            Assert.Throws<ArgumentException>(() => WalletLedger.Apply(new WalletRecord(), Tx(null, coins: 1)));
            Assert.Throws<ArgumentException>(() =>
                WalletLedger.Apply(new WalletRecord(), new WalletTransaction { Id = "a", Coins = 1 }));
        }

        [Test]
        public async Task NewPlayer_StartsWithTheStartingWallet_AndExistingWalletIsNotReset()
        {
            var store = new InMemoryPlayerRecordStore();
            var service = new LocalPlayerStateService(store);
            var state = await service.GetAsync();
            Assert.That(state.Wallet.Coins, Is.EqualTo(WalletLedger.StartingCoins));
            Assert.That(state.Wallet.Premium, Is.EqualTo(WalletLedger.StartingPremium));

            state.Wallet.Coins = 7;
            int writes = store.WriteCount;
            state = await service.GetAsync();
            Assert.That(state.Wallet.Coins, Is.EqualTo(7));
            Assert.That(store.WriteCount, Is.EqualTo(writes));
        }

        [Test]
        public async Task PlayerFromBeforeWallets_GetsOnlyAWalletWritten()
        {
            var store = new InMemoryPlayerRecordStore();
            await new LocalPlayerStateService(store).GetAsync();
            var stored = await store.ReadAsync();
            stored.Rank.RR = 42;
            var legacy = new LegacyStore(store);
            var state = await new LocalPlayerStateService(legacy).GetAsync();
            Assert.That(state.Wallet, Is.Not.Null);
            Assert.That(state.Rank.RR, Is.EqualTo(42));
            Assert.That(legacy.Written.Wallet, Is.Not.Null);
            Assert.That(legacy.Written.Rating, Is.Null);
            Assert.That(legacy.Written.Rank, Is.Null);
            Assert.That(legacy.Written.Inventory, Is.Null);
            Assert.That(legacy.Written.History, Is.Null);
        }

        private sealed class LegacyStore : IPlayerRecordStore
        {
            private readonly IPlayerRecordStore inner;
            public PlayerState Written;
            public LegacyStore(IPlayerRecordStore inner) { this.inner = inner; }
            public async Task<PlayerState> ReadAsync()
            {
                var state = await inner.ReadAsync();
                state.Wallet = null;
                return state;
            }
            public Task WriteAsync(PlayerState records) { Written = records; return inner.WriteAsync(records); }
        }

        [Test]
        public async Task Service_RetriesOnConflict_AgainstTheFreshWallet()
        {
            var store = new WalletStore { Stored = WalletLedger.Starting() };
            store.BeforeWrite = () => store.ConcurrentGrant(1000);
            var wallet = await new WalletService(store, () => 100).ApplyAsync("r1", "test", 5, 0);
            Assert.That(wallet.Coins, Is.EqualTo(WalletLedger.StartingCoins + 1000 + 5));
            Assert.That(store.Stored.Coins, Is.EqualTo(wallet.Coins));
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public async Task Service_RepeatedRequest_DoesNotWrite()
        {
            var store = new WalletStore { Stored = WalletLedger.Starting() };
            var service = new WalletService(store, () => 100);
            await service.ApplyAsync("r1", "test", 5, 0);
            var wallet = await service.ApplyAsync("r1", "test", 5, 0);
            Assert.That(wallet.Coins, Is.EqualTo(WalletLedger.StartingCoins + 5));
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void Service_RefusesOverspend_AndMissingWallet()
        {
            var store = new WalletStore { Stored = new WalletRecord { Premium = 3 } };
            Assert.ThrowsAsync<WalletRefusedException>(() => new WalletService(store, () => 1).ApplyAsync("r", "x", 0, -4));
            Assert.That(store.Writes, Is.Zero);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WalletService(new WalletStore(), () => 1).ApplyAsync("r", "x", 1, 0));
        }

        [Test]
        public void Admin_RefusesNonAdmins_WithoutTouchingAnyWallet()
        {
            var store = new WalletStore { Stored = WalletLedger.Starting() };
            var admin = new AdminService(new Admins("boss"), _ => store, () => 1, _ => null, ServerCatalog.Items);
            Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                admin.GrantCurrencyAsync("player", null, 100, 100, "free money", "r1"));
            Assert.That(store.Writes, Is.Zero);
        }

        [Test]
        public async Task AdminList_ParsesIds_AndTheEmbeddedListLoads()
        {
            var ids = EmbeddedAdminDirectory.Parse("[\"abc\", \"\", \" \", \"def\"]");
            Assert.That(ids, Is.EquivalentTo(new[] { "abc", "def" }));
            Assert.Throws<System.IO.InvalidDataException>(() => EmbeddedAdminDirectory.Parse("null"));
            Assert.That(await new EmbeddedAdminDirectory().IsAdminAsync(""), Is.False);
            Assert.That(await new EmbeddedAdminDirectory().IsAdminAsync("not-an-admin"), Is.False);
        }

        [Test]
        public async Task Admin_GrantsToTarget_OrSelf_AndRecordsWhoDidIt()
        {
            var stores = new Dictionary<string, WalletStore>
            {
                ["boss"] = new WalletStore { Stored = WalletLedger.Starting() },
                ["player"] = new WalletStore { Stored = WalletLedger.Starting() }
            };
            var admin = new AdminService(new Admins("boss"), id => stores[id], () => 1, _ => null, ServerCatalog.Items);

            await admin.GrantCurrencyAsync("boss", "player", 0, 100, "testing crates", "r1");
            await admin.GrantCurrencyAsync("boss", "", 50, 0, "self", "r2");

            Assert.That(stores["player"].Stored.Premium, Is.EqualTo(WalletLedger.StartingPremium + 100));
            Assert.That(stores["player"].Stored.Recent.Single().Reason, Is.EqualTo("admin:boss:testing crates"));
            Assert.That(stores["boss"].Stored.Coins, Is.EqualTo(WalletLedger.StartingCoins + 50));
        }
    }
}
