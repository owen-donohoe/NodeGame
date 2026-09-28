using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    public sealed class AllocationPlayer
    {
        public string PlayerId;
        public ushort? Protocol;
        public ushort? Sim;
        public int? Content;
    }

    public sealed class MatchAllocationResult
    {
        public bool ok;
        public string error;
    }

    /// <summary>Match creation rules, with server storage and time supplied by the caller.</summary>
    public sealed class MatchAllocation
    {
        private readonly Func<string, Task<PlayerState>> readPlayer;
        private readonly IMatchRecordStore matches;
        private readonly BalanceCatalog balances;

        public MatchAllocation(Func<string, Task<PlayerState>> readPlayer,
            IMatchRecordStore matches, BalanceCatalog balances)
        {
            this.readPlayer = readPlayer;
            this.matches = matches;
            this.balances = balances;
        }

        public async Task<MatchAllocationResult> Allocate(string matchId,
            IReadOnlyList<AllocationPlayer> players, long nowUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(matchId)) return Error("Match ID is required.");
            // Retries keep the original pre-match snapshots, even if players have since changed.
            if ((await matches.ReadAsync(matchId)).Record != null) return Success();
            if (players == null || players.Count != 2 ||
                string.IsNullOrWhiteSpace(players[0]?.PlayerId) ||
                string.IsNullOrWhiteSpace(players[1]?.PlayerId) || players[0].PlayerId == players[1].PlayerId)
                return Error("Two distinct player IDs are required.");

            foreach (var player in players)
                if (!player.Protocol.HasValue || !player.Sim.HasValue || !player.Content.HasValue)
                    return Error("Missing or invalid ticket custom data: protocol, sim and content are required.");
            var first = players[0];
            var second = players[1];
            if (first.Protocol != second.Protocol || first.Sim != second.Sim || first.Content != second.Content)
                return Error("Players have different protocol, sim or content versions.");
            if (!balances.TryGet(first.Content.Value, out _)) return Error("Unknown content hash.");

            var states = new[] { await readPlayer(first.PlayerId), await readPlayer(second.PlayerId) };
            foreach (var state in states)
                if (state?.Rating == null || state.Rank == null || state.Inventory?.OwnedVariants == null)
                    return Error("Server player state is incomplete.");
            if (Math.Abs((long)states[0].Rank.Arena - states[1].Rank.Arena) > 1)
                return Error("Players must be within one arena.");

            var record = MatchRecords.Create(matchId, new[] { first.PlayerId, second.PlayerId }, states,
                nowUnixSeconds, first.Protocol.Value, first.Sim.Value, first.Content.Value);
            try
            {
                await matches.WriteAsync(record, null);
            }
            catch (RecordConflictException)
            {
                if ((await matches.ReadAsync(matchId)).Record == null) throw;
            }
            return Success();
        }

        private static MatchAllocationResult Success() => new MatchAllocationResult { ok = true };
        private static MatchAllocationResult Error(string error) => new MatchAllocationResult { error = error };
    }
}
