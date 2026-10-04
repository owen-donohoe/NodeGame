using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud.Tests
{
    public class DisciplineStorageTests
    {
        private readonly Dictionary<string, JToken> values = new Dictionary<string, JToken>();
        private readonly Dictionary<string, int> versions = new Dictionary<string, int>();
        private readonly List<SetItemBody[]> batches = new List<SetItemBody[]>();
        private Action BeforeWrite;
        private IGameApiClient api;
        private IExecutionContext context;
        private CloudSavePlayerRecordStore store;

        [SetUp]
        public void SetUp()
        {
            values.Clear();
            versions.Clear();
            batches.Clear();
            BeforeWrite = null;
            context = Proxy<IExecutionContext>((method, _) => method.ReturnType == typeof(string) ? "test" : null);
            object cloudSave = null;
            api = Proxy<IGameApiClient>((method, _) =>
            {
                if (method.Name != "get_CloudSaveData") throw new NotSupportedException(method.Name);
                if (cloudSave == null)
                {
                    cloudSave = DispatchProxy.Create(method.ReturnType, typeof(ApiProxy));
                    ((ApiProxy)cloudSave).Call = CloudSaveCall;
                }
                return cloudSave;
            });
            store = new CloudSavePlayerRecordStore(api, context);
        }

        [Test]
        public async Task GetPlayerStateCreatesDisciplineSeparatelyAndReturnsItWithoutRepeatWrites()
        {
            var module = new PlayerStateModule(api);
            var result = await module.GetPlayerState(context);
            Assert.That(result.Discipline.Level, Is.Zero);
            Assert.That(result.Discipline.NonReports, Is.Empty);
            Assert.That(result.Discipline.StruckMatchIds, Is.Empty);
            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches[0].Select(i => i.Key), Is.EquivalentTo(PlayerStateKeys.All));
            Assert.That(batches[1].Select(i => i.Key), Is.EquivalentTo(new[] { "discipline", "rating" }));
            Assert.That(batches[1].Single(i => i.Key == "rating").WriteLock, Is.Not.Null.And.Not.Empty);
            await module.GetPlayerState(context);
            Assert.That(batches, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ExistingDisciplineUsesOnlyItsOwnConditionalKey()
        {
            await new PlayerStateModule(api).GetPlayerState(context);
            batches.Clear();
            var read = await store.ReadDisciplineAsync();
            read.State.Discipline.Level = 3;
            read.State.Discipline.BlockedUntilUnixSeconds = 220;
            await store.WriteDisciplineAsync(read.State.Discipline, read);
            Assert.That(batches.Single().Select(i => i.Key), Is.EqualTo(new[] { "discipline" }));
            Assert.That(batches.Single().Single().WriteLock, Is.Not.Null.And.Not.Empty);
            Assert.That((await new PlayerStateModule(api).GetPlayerState(context)).Discipline.Level, Is.EqualTo(3));
            Assert.That((await store.ReadForSettlementAsync()).State.Discipline.BlockedUntilUnixSeconds, Is.EqualTo(220));
        }

        [Test]
        public async Task ConcurrentFirstStrikeCannotBeOverwrittenByDefaultCreation()
        {
            await new PlayerStateModule(api).GetPlayerState(context);
            values.Remove(PlayerStateKeys.Discipline);
            versions.Remove(PlayerStateKeys.Discipline);
            batches.Clear();
            BeforeWrite = () =>
            {
                // The competing first discipline write atomically advances rating's
                // lock, so this default initializer must re-read its new discipline.
                Put(PlayerStateKeys.Discipline, new DisciplineRecord { Level = 3, BlockedUntilUnixSeconds = 220 });
                Put(PlayerStateKeys.Rating, values[PlayerStateKeys.Rating]);
            };
            var result = await new PlayerStateModule(api).GetPlayerState(context);
            Assert.That(result.Discipline.Level, Is.EqualTo(3));
            Assert.That(result.Discipline.BlockedUntilUnixSeconds, Is.EqualTo(220));
            Assert.That(values[PlayerStateKeys.Discipline]["Level"].Value<int>(), Is.EqualTo(3));
            Assert.That(batches, Is.Empty);
        }

        [Test]
        public async Task SettlementIgnoresDisciplineAndDoesNotOverwriteAConcurrentStrike()
        {
            await new PlayerStateModule(api).GetPlayerState(context);
            var settlement = await store.ReadForSettlementAsync();
            var discipline = await store.ReadDisciplineAsync();
            discipline.State.Discipline.Level = 5;
            await store.WriteDisciplineAsync(discipline.State.Discipline, discipline);
            batches.Clear();
            settlement.State.Rank.RR = 42;
            await store.WriteForSettlementAsync(settlement.State, settlement.WriteLocks);
            Assert.That(batches.Single().Select(i => i.Key), Is.EquivalentTo(PlayerStateKeys.All));
            Assert.That((await store.ReadAsync()).Discipline.Level, Is.EqualTo(5));
            Assert.That((await store.ReadAsync()).Rank.RR, Is.EqualTo(42));
        }

        [Test]
        public async Task CompetingFirstDisciplineWritesConflictInsteadOfLosingAStrike()
        {
            await new PlayerStateModule(api).GetPlayerState(context);
            values.Remove(PlayerStateKeys.Discipline);
            versions.Remove(PlayerStateKeys.Discipline);
            var first = await store.ReadDisciplineAsync();
            var second = await store.ReadDisciplineAsync();
            await store.WriteDisciplineAsync(new DisciplineRecord { Level = 1 }, first);
            Assert.ThrowsAsync<RecordConflictException>(() => store.WriteDisciplineAsync(new DisciplineRecord(), second));
            Assert.That((await store.ReadAsync()).Discipline.Level, Is.EqualTo(1));
        }

        private object CloudSaveCall(MethodInfo method, object[] args)
        {
            object data = null;
            if (method.Name == "GetProtectedItemsAsync")
            {
                var keys = args.OfType<List<string>>().Single();
                Type dataType = method.ReturnType.GetGenericArguments().Single().GetProperty("Data").PropertyType;
                data = Activator.CreateInstance(dataType, true);
                dataType.GetProperty("Results").SetValue(data, keys.Where(values.ContainsKey)
                    .Select(key =>
                    {
                        var item = (Item)Activator.CreateInstance(typeof(Item), true);
                        item.Key = key;
                        item.Value = values[key];
                        item.WriteLock = versions[key].ToString();
                        return item;
                    }).ToList());
            }
            else if (method.Name == "SetProtectedItemBatchAsync")
            {
                var items = args.OfType<SetItemBatchBody>().Single().Data.ToArray();
                var hook = BeforeWrite;
                BeforeWrite = null;
                hook?.Invoke();
                foreach (var item in items)
                    if (item.WriteLock != null && (!versions.TryGetValue(item.Key, out int version) || item.WriteLock != version.ToString()))
                        throw new RecordConflictException("Protected key changed.");
                foreach (var item in items) Put(item.Key, item.Value);
                batches.Add(items);
            }
            else throw new NotSupportedException(method.Name);
            Type resultType = method.ReturnType.GetGenericArguments().Single();
            object response = Activator.CreateInstance(resultType);
            if (data != null)
            {
                var property = resultType.GetProperty("Data");
                property.SetValue(response, data);
            }
            return typeof(Task).GetMethod(nameof(Task.FromResult)).MakeGenericMethod(resultType).Invoke(null, new[] { response });
        }

        private void Put(string key, object value)
        {
            values[key] = JToken.FromObject(value);
            versions[key] = versions.TryGetValue(key, out int version) ? version + 1 : 1;
        }

        private static T Proxy<T>(Func<MethodInfo, object[], object> call) where T : class
        {
            var proxy = DispatchProxy.Create<T, ApiProxy>();
            ((ApiProxy)(object)proxy).Call = call;
            return proxy;
        }

        public class ApiProxy : DispatchProxy
        {
            public Func<MethodInfo, object[], object> Call;
            protected override object Invoke(MethodInfo method, object[] args) => Call(method, args);
        }
    }
}
