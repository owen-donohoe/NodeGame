using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud
{
    public interface IHandleRegistryStore
    {
        Task<(Dictionary<string, string> Entries, string WriteLock)> ReadShardAsync(string shard);
        Task WriteShardAsync(string shard, Dictionary<string, string> entries, string expectedWriteLock);
        Task<int> CreateEmptyShardsAsync();
    }

    public static class HandleRegistry
    {
        public const int ShardCount = 64;

        public static string Shard(string nameNorm)
        {
            uint hash = 2166136261;
            foreach (byte value in Encoding.UTF8.GetBytes(nameNorm))
                hash = unchecked((hash ^ value) * 16777619);
            return (hash % ShardCount).ToString("x2");
        }

        public static string Key(string nameNorm, string tag) => nameNorm + "#" + tag;
    }

    public static class EmbeddedBlockedNames
    {
        private static readonly Lazy<string[]> names = new Lazy<string[]>(Load);
        public static IReadOnlyList<string> Names => names.Value;

        public static string[] Parse(string text) => text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.Length != 0 && !line.StartsWith("#", StringComparison.Ordinal))
            .Select(line => line.ToLowerInvariant()).ToArray();

        private static string[] Load()
        {
            using var stream = typeof(EmbeddedBlockedNames).Assembly.GetManifestResourceStream("NodeWarCloud.Admin.blocked-names.txt")
                ?? throw new InvalidDataException("Embedded blocked names are missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
    }

    public sealed class CloudSaveHandleRegistryStore : IHandleRegistryStore
    {
        private readonly IGameApiClient api;
        private readonly IExecutionContext context;

        public CloudSaveHandleRegistryStore(IGameApiClient api, IExecutionContext context)
        { this.api = api; this.context = context; }

        private static string ItemId(string shard)
        {
            if (!int.TryParse(shard, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int number) || number < 0 || number >= HandleRegistry.ShardCount
                || shard != number.ToString("x2")) throw new ArgumentException("Invalid handle shard.", nameof(shard));
            return "handles-" + shard;
        }

        public async Task<(Dictionary<string, string> Entries, string WriteLock)> ReadShardAsync(string shard)
        {
            var response = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, ItemId(shard), new List<string> { "entries" });
            var item = response.Data.Results.SingleOrDefault(i => i.Key == "entries");
            return (item == null ? null : JsonConvert.DeserializeObject<Dictionary<string, string>>(
                JsonConvert.SerializeObject(item.Value)), item?.WriteLock);
        }

        public async Task WriteShardAsync(string shard, Dictionary<string, string> entries, string expectedWriteLock)
        {
            if (string.IsNullOrEmpty(expectedWriteLock))
                throw new InvalidOperationException("A handle shard write needs the lock from its read.");
            await WriteAsync(shard, entries, expectedWriteLock);
        }

        private async Task WriteAsync(string shard, Dictionary<string, string> entries, string writeLock)
        {
            try
            {
                await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                    ItemId(shard), new SetItemBody("entries", entries, writeLock));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Handle registry changed.", ex); }
        }

        // Operator bootstrap only. Run once, before enabling handle writes: Cloud Save
        // has no create-if-absent primitive, so concurrent bootstrap is not safe.
        public async Task<int> CreateEmptyShardsAsync()
        {
            int created = 0;
            for (int i = 0; i < HandleRegistry.ShardCount; i++)
            {
                string shard = i.ToString("x2");
                if ((await ReadShardAsync(shard)).Entries != null) continue;
                await WriteAsync(shard, new Dictionary<string, string>(StringComparer.Ordinal), null);
                created++;
            }
            return created;
        }
    }
}
