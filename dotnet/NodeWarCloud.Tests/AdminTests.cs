using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class AdminTests
    {
        private sealed class Admins : IAdminDirectory
        {
            public Task<bool> IsAdminAsync(string playerId) => Task.FromResult(playerId == "boss");
        }

        private sealed class Store : ILockedPlayerRecordStore
        {
            public InventoryRecord Stored = PlayerStateDefaults.Inventory();
            private int version = 1;
            public int Reads, Attempts, Writes;
            public Action BeforeWrite;
            private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
            public Task<PlayerState> ReadAsync() => Task.FromResult(new PlayerState { Inventory = Copy(Stored) });
            public Task WriteAsync(PlayerState records) => throw new InvalidOperationException("Expected an inventory-only locked write.");
            public Task WriteDefaultsLockedAsync(PlayerState records, string token) =>
                throw new InvalidOperationException("Must not initialize defaults.");
            public async Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync()
            {
                Reads++;
                return (await ReadAsync(), version.ToString());
            }
            public Task WriteInventoryLockedAsync(InventoryRecord inventory, string expectedWriteLock)
            {
                Attempts++;
                var hook = BeforeWrite;
                BeforeWrite = null;
                hook?.Invoke();
                if (expectedWriteLock != version.ToString()) throw new RecordConflictException("Inventory changed.");
                Stored = Copy(inventory);
                version++;
                Writes++;
                return Task.CompletedTask;
            }
            public void ConcurrentChange()
            {
                Stored.OwnedVariants.Add("suit.scout.e0");
                Stored.Equipped.Variants["suit.warrior"] = "suit.warrior.e0";
                version++;
            }
        }

        private static AdminService Service(Func<string, ILockedPlayerRecordStore> stores) =>
            new AdminService(new Admins(), _ => null, () => 1, stores, ServerCatalog.Items);

        [Test]
        public void NonAdminIsRefusedBeforeAccessingTarget()
        {
            var store = new Store();
            var service = Service(_ => store);
            Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GrantItemsAsync("player", "boss", new[] { "suit.warrior.e0" }, false));
            Assert.That(store.Reads, Is.Zero);
            Assert.That(store.Writes, Is.Zero);
        }

        [TestCase("missing.item")]
        [TestCase("district.camp.e0")]
        [TestCase(null)]
        public void InvalidItemRefusesEntireBatchBeforeAnyReadOrWrite(string badId)
        {
            var store = new Store();
            var service = Service(_ => store);
            var ex = Assert.ThrowsAsync<ArgumentException>(() =>
                service.GrantItemsAsync("boss", null, new[] { "suit.warrior.e0", badId }, false));
            Assert.That(ex.Message, Does.Contain(badId ?? "<null>"));
            Assert.That(store.Reads, Is.Zero);
            Assert.That(store.Writes, Is.Zero);
            Assert.That(store.Stored.OwnedVariants, Is.Empty);
        }

        [TestCase(null, "boss")]
        [TestCase("", "boss")]
        [TestCase("other", "other")]
        public async Task GrantsOnceToTargetOrSelf(string target, string expected)
        {
            var stores = new Dictionary<string, Store> { ["boss"] = new Store(), ["other"] = new Store() };
            var service = Service(id => stores[id]);
            string[] ids = { "suit.warrior.e0", "skin.suit.warrior.default", "suit.warrior.e0" };
            var result = await service.GrantItemsAsync("boss", target, ids, false);
            await service.GrantItemsAsync("boss", target, ids, false);
            Assert.That(result.OwnedVariants, Is.EqualTo(new[] { ids[0] }));
            Assert.That(result.OwnedSkins, Is.EqualTo(new[] { ids[1] }));
            Assert.That(result.Equipped.Variants, Is.Empty);
            Assert.That(stores[expected].Writes, Is.EqualTo(1));
            Assert.That(stores[expected == "boss" ? "other" : "boss"].Reads, Is.Zero);
        }

        [Test]
        public async Task AllActiveGrantsEveryActiveVariantAndSkinOnce()
        {
            var store = new Store();
            var service = Service(_ => store);
            var result = await service.GrantItemsAsync("boss", "", null, true);
            var active = ServerCatalog.Items.Where(item => !item.Retired).ToList();
            Assert.That(result.OwnedVariants,
                Is.EquivalentTo(active.Where(item => item.Kind == CatalogItemKind.Variant).Select(item => item.Id)));
            Assert.That(result.OwnedSkins,
                Is.EquivalentTo(active.Where(item => item.Kind == CatalogItemKind.Skin).Select(item => item.Id)));
            await service.GrantItemsAsync("boss", "", null, true);
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public async Task ConflictReappliesGrantToFreshInventory()
        {
            var store = new Store();
            store.BeforeWrite = store.ConcurrentChange;
            var result = await Service(_ => store).GrantItemsAsync("boss", "", new[] { "suit.warrior.e0" }, false);
            Assert.That(result.OwnedVariants, Is.EquivalentTo(new[] { "suit.warrior.e0", "suit.scout.e0" }));
            Assert.That(result.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e0"));
            Assert.That(store.Stored.OwnedVariants, Is.EquivalentTo(result.OwnedVariants));
            Assert.That(store.Reads, Is.EqualTo(2));
            Assert.That(store.Attempts, Is.EqualTo(2));
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void ConflictsStopAfterThreeAttempts()
        {
            var store = new Store();
            Action conflict = null;
            conflict = () => { store.ConcurrentChange(); store.BeforeWrite = conflict; };
            store.BeforeWrite = conflict;
            Assert.ThrowsAsync<RecordConflictException>(() =>
                Service(_ => store).GrantItemsAsync("boss", "", new[] { "suit.warrior.e0" }, false));
            Assert.That(store.Reads, Is.EqualTo(3));
            Assert.That(store.Attempts, Is.EqualTo(3));
            Assert.That(store.Writes, Is.Zero);
            Assert.That(store.Stored.OwnedVariants, Does.Not.Contain("suit.warrior.e0"));
        }

        [Test]
        public void MissingInventoryRequiresSignInWithoutWriting()
        {
            var store = new Store { Stored = null };
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                Service(_ => store).GrantItemsAsync("boss", "other", new[] { "suit.warrior.e0" }, false));
            Assert.That(ex.Message, Does.Contain("must sign in once first"));
            Assert.That(store.Writes, Is.Zero);
        }
    }
}
