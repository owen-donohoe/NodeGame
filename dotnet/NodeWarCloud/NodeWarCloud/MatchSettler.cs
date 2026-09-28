using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Progression;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Settles a match's rating, rank and inventory changes for both players,
    /// given an explicit winner. Extracted from <see cref="MatchReporting"/> so
    /// <see cref="MatchRendezvous"/> can settle a forfeit the same way an
    /// agreed report does: same idempotency guard (Rating.SettledMatchIds),
    /// same claim re-check, same grants/clamp/history bookkeeping.
    /// </summary>
    public sealed class MatchSettler
    {
        private const int RetryLimit = 3;
        private readonly Func<string, ISettlementPlayerStore> players;
        private readonly InventoryRules inventory;

        public MatchSettler(Func<string, ISettlementPlayerStore> players, InventoryRules inventory)
        {
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        // Arena thresholds come from the one table the client also displays.
        private static readonly SettlementConfig Config = new SettlementConfig
        {
            Rank = new RankConfig { ArenaThresholds = RankTable.Thresholds.ToArray() }
        };

        /// <summary>
        /// Settles <paramref name="record"/> with <paramref name="winner"/> (0 or
        /// 1) as the winning player index. Writes <see cref="MatchRecord.outcomes"/>
        /// and both players' server records. Returns false (writing nothing to
        /// either player) when a claim no longer matches this match -- the
        /// caller must void the record rather than settle it.
        /// </summary>
        public async Task<bool> Settle(MatchRecord record, int winner, long now)
        {
            var inputs = record.players.Select(p => new SettlementPlayer
            {
                Rating = new Rating(p.Rating.R, p.Rating.Rd, p.Rating.Sigma),
                LastMatchUnixSeconds = p.Rating.LastMatchUnixSeconds,
                Rank = new RankState(p.Rank.RR, p.Rank.Arena, p.Rank.HighestArena)
            }).ToArray();
            var outcome = MatchSettlement.Settle(new SettlementInput
            {
                Players = inputs, Winner = winner,
                NowUnixSeconds = record.settlementUnixSeconds
            }, Config);

            record.outcomes = new MatchOutcome[2];
            for (int p = 0; p < 2; p++)
            {
                var settledOutcome = outcome.Players[p];
                record.outcomes[p] = new MatchOutcome
                {
                    won = winner == p,
                    rrDelta = settledOutcome.RRDelta,
                    rrAfter = settledOutcome.Rank.RR,
                    arenaAfter = settledOutcome.Rank.Arena,
                    promoted = settledOutcome.Promoted,
                    demoted = settledOutcome.Demoted
                };
            }

            for (int p = 0; p < 2; p++)
            {
                var store = players(record.playerIds[p]);
                for (int attempt = 0; attempt < RetryLimit; attempt++)
                {
                    var read = await store.ReadForSettlementAsync();
                    var state = read.State;
                    if (state?.History?.MatchIds == null || state.Inventory == null || state.Rating == null)
                        throw new InvalidOperationException("Match players must have initialized server records.");
                    // The idempotency guard is this bounded, 200-entry list on the
                    // rating record, not the 20-entry history: a player who plays
                    // enough matches to evict this one from history must still not
                    // be settled twice for it.
                    var settledIds = state.Rating.SettledMatchIds ?? new List<string>();
                    if (settledIds.Contains(record.matchId)) break;
                    // The rating lock also fences a new claim acquired since HoldsClaims.
                    // Re-check after a settlement write conflict before using old snapshots.
                    if (state.ActiveMatch?.matchId != record.matchId || state.ActiveMatch.expiresUnixSeconds <= now)
                        return false;
                    var settled = outcome.Players[p];
                    settledIds.Insert(0, record.matchId);
                    if (settledIds.Count > 200) settledIds.RemoveRange(200, settledIds.Count - 200);
                    state.Rating = new RatingRecord { R = settled.Rating.R, Rd = settled.Rating.RD,
                        Sigma = settled.Rating.Sigma, LastMatchUnixSeconds = settled.LastMatchUnixSeconds,
                        SettledMatchIds = settledIds };
                    state.Rank = new RankRecord { RR = settled.Rank.RR, Arena = settled.Rank.Arena,
                        HighestArena = settled.Rank.HighestArena };
                    inventory.GrantDefaults(state);
                    InventoryClamp.ClampToArena(state.Inventory, state.Rank.Arena);
                    state.History.MatchIds.Insert(0, record.matchId);
                    if (state.History.MatchIds.Count > 20)
                        state.History.MatchIds.RemoveRange(20, state.History.MatchIds.Count - 20);
                    try
                    {
                        await store.WriteForSettlementAsync(state, read.WriteLocks);
                        break;
                    }
                    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
                }
            }
            return true;
        }
    }
}
