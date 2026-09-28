using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using Unity.Services.Matchmaker.Model;

[assembly: InternalsVisibleTo("NodeWarCloud.Tests")]

namespace NodeWar.Cloud
{
    public sealed class MatchmakerAllocatorModule : IMatchmakerAllocator
    {
        private static readonly InventoryRules Inventory = new InventoryRules(ServerCatalog.Items);
        private readonly Func<IExecutionContext, IMatchRecordStore> matchStore;
        private readonly Func<IExecutionContext, string, Task<PlayerState>> readPlayer;
        private readonly BalanceCatalog balances;
        private readonly Func<IExecutionContext, string, ISettlementPlayerStore> playerStores;
        private readonly Func<long> serverTime;

        public MatchmakerAllocatorModule(IGameApiClient api) : this(
            context => new CloudSaveMatchRecordStore(api, context),
            (context, playerId) => new InventoryPlayerStateService(
                new CloudSavePlayerRecordStore(api, context, playerId), Inventory).GetAsync(),
            BalanceCatalog.Embedded, () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            (context, id) => new CloudSavePlayerRecordStore(api, context, id)) { }

        internal MatchmakerAllocatorModule(Func<IExecutionContext, IMatchRecordStore> matchStore,
            Func<IExecutionContext, string, Task<PlayerState>> readPlayer,
            BalanceCatalog balances, Func<long> serverTime,
            Func<IExecutionContext, string, ISettlementPlayerStore> playerStores)
        {
            this.matchStore = matchStore;
            this.readPlayer = readPlayer;
            this.balances = balances;
            this.serverTime = serverTime;
            this.playerStores = playerStores;
        }

        public const string PlayerCallRefused = "Only Matchmaker may call this function.";

        public static bool IsPlayerCall(IExecutionContext context) => !string.IsNullOrEmpty(context?.PlayerId);

        [CloudCodeFunction("Matchmaker_Allocate")]
        public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
        {
            // Matchmaker calls as a service. A call carrying a player identity is a
            // client forging a roster: verified live on 2026-09-28 that a player
            // could otherwise create a match record against any opponent.
            if (IsPlayerCall(context))
                return new AllocateResponse(AllocateStatus.Error) { Message = PlayerCallRefused };
            if (string.IsNullOrWhiteSpace(request?.MatchId))
                return new AllocateResponse(AllocateStatus.Error) { Message = "Match ID is required." };
            try
            {
                var matches = matchStore(context);
                // An already-created match wins over a retried request's roster or ticket data.
                if ((await matches.ReadAsync(request.MatchId)).Record != null) return Created(request.MatchId);
                var allocation = new MatchAllocation(id => readPlayer(context, id), matches, balances,
                    id => playerStores(context, id));
                var result = await allocation.Allocate(request.MatchId, Players(request), serverTime());
                return result.ok ? Created(request.MatchId) :
                    new AllocateResponse(AllocateStatus.Error) { Message = result.error };
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return new AllocateResponse(AllocateStatus.Error) { Message = "Invalid matchmaking player data." };
            }
            catch (Exception)
            {
                return new AllocateResponse(AllocateStatus.Error) { Message = "Unable to allocate match." };
            }
        }

        [CloudCodeFunction("Matchmaker_Poll")]
        public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
        {
            if (IsPlayerCall(context))
                return new PollResponse(PollStatus.Error) { Message = PlayerCallRefused };
            if (string.IsNullOrWhiteSpace(request?.MatchId))
                return new PollResponse(PollStatus.Error) { Message = "Match ID is required." };
            try
            {
                if ((await matchStore(context).ReadAsync(request.MatchId)).Record == null)
                    return new PollResponse(PollStatus.Error) { Message = "Match record does not exist." };
                return new PollResponse(PollStatus.Allocated)
                {
                    AssignmentData = AssignmentData.Custom(MatchIdData(request.MatchId))
                };
            }
            catch (Exception)
            {
                return new PollResponse(PollStatus.Error) { Message = "Unable to read match record." };
            }
        }

        private static AllocateResponse Created(string matchId) => new AllocateResponse(AllocateStatus.Created)
        { AllocationData = MatchIdData(matchId) };

        private static Dictionary<string, object> MatchIdData(string matchId) =>
            new Dictionary<string, object> { ["matchId"] = matchId };

        private static AllocationPlayer[] Players(AllocateRequest request)
        {
            var properties = request.MatchmakingResults?.MatchProperties;
            if (properties == null || !properties.TryGetValue("Players", out object raw) || raw == null)
                return null;
            var players = raw as IEnumerable<Player> ?? Token(raw).ToObject<List<Player>>();
            return players?.Select(player =>
            {
                if (player == null) return null;
                var data = player.CustomData == null ? null : Token(player.CustomData) as JObject;
                int? protocol = Integer(data?["protocol"]);
                int? sim = Integer(data?["sim"]);
                return new AllocationPlayer
                {
                    PlayerId = player.Id,
                    Protocol = protocol >= 0 && protocol <= ushort.MaxValue ? (ushort?)protocol : null,
                    Sim = sim >= 0 && sim <= ushort.MaxValue ? (ushort?)sim : null,
                    Content = Integer(data?["content"])
                };
            }).ToArray();
        }

        private static JToken Token(object value) => value is JsonElement element ?
            JToken.Parse(element.GetRawText()) : value as JToken ?? JToken.FromObject(value);

        private static int? Integer(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float &&
                token.Type != JTokenType.String)) return null;
            string text = token.Type == JTokenType.String ? token.Value<string>() :
                token.ToString(Newtonsoft.Json.Formatting.None);
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value) ||
                value < int.MinValue || value > int.MaxValue || value != decimal.Truncate(value)) return null;
            return (int)value;
        }
    }
}
