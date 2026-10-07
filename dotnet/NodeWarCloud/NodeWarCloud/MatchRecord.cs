using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    // Server-only stored fields. Never rename: names are the persisted schema.
    public sealed class MatchRecord
    {
        public string matchId;
        public string[] playerIds;
        public long createdUnixSeconds;
        public ushort protocol;
        public ushort sim;
        public int content;
        // The map this match is on, assigned by the server's allocator from its own
        // catalog, and the fingerprint of that map's board. Null / 0 in records stored
        // before maps existed; those stay readable but are never admitted as v3 matches.
        public string mapId;
        public int boardHash;
        public MatchPlayerSnapshot[] players;
        public MatchRecordState state;
        public List<MatchReport> reports = new List<MatchReport>();
        public long pendingUnixSeconds;
        public long settlementUnixSeconds;
        // Indexed like players/playerIds. Set once, in the same write that moves
        // the record to Settled. Null for Open/Pending/Void/Disputed records.
        public MatchOutcome[] outcomes;
        // The host's (slot 0's) Relay join code, readable only by the two
        // players through Rendezvous. Null until published.
        public string joinCode;
        // Server time each slot first confirmed the connection; 0 means not
        // confirmed. Older records may have null here, treated as all-zero.
        public long[] confirmedUnixSeconds = new long[2];
        // Later of both confirmations. 0 means connection is unconfirmed;
        // an accepted report also proves the match started for leaving rules.
        public long connectedUnixSeconds;
        // The slot that forfeited, or -1. Committed under the record's write
        // lock before any player write, like agreement, so every settlement
        // pass (Leave or ReportMatch) settles the same winner.
        public int forfeitedBy = -1;
        // The slot a hold resolved against, or -1. Committed with forfeitedBy
        // under the record's write lock before any player write.
        public int abandonedBy = -1;
        // Set only by the pending timeout, so retries can distinguish D19
        // non-reports from other voids without guessing from the report count.
        public bool pendingTimeoutVoid;
        // Durable completion guard, set under the record lock only after
        // discipline succeeds (or none is due), before releasing claims.
        public bool disciplineApplied;
        // Both players left before the match ended and it was decided on Core
        // health (forfeitedBy holds the side with more breaches). Never a strike.
        public bool bothLeft;
    }

    // A settled player's result on this match. Never re-derived from current
    // state: it is the outcome this match itself produced.
    public sealed class MatchOutcome
    {
        public bool won;
        public int rrDelta;
        public int rrAfter;
        public int arenaAfter;
        public bool promoted;
        public bool demoted;
    }

    public sealed class MatchPlayerSnapshot
    {
        public RatingRecord Rating;
        public RankRecord Rank;
        public List<string> OwnedVariants;
    }

    public sealed class MatchReport
    {
        public int playerIndex;
        public int winner = -1;
        public int endTick;
        public int finalHash;
        public bool accepted;
        public string error;
        // A log that replayed cleanly but stopped before the match ended (the
        // player left), with each player's Core breaches at its last tick.
        // Refused for settlement on its own; two of them decide a match both
        // players left.
        public bool unfinished;
        public int[] breaches;
        // Durable until copied to log-N. Retries save the winning record's log,
        // never a concurrent upload that lost the record write-lock race.
        public string pendingLogBase64;
    }

    public static class MatchRecords
    {
        public static MatchRecord Create(string matchId, string[] playerIds, PlayerState[] players,
            long nowUnixSeconds, ushort protocol, ushort sim, int content, string mapId = null, int boardHash = 0)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("Match ID is required.", nameof(matchId));
            if (playerIds == null || playerIds.Length != 2 || string.IsNullOrWhiteSpace(playerIds[0]) ||
                string.IsNullOrWhiteSpace(playerIds[1]) || playerIds[0] == playerIds[1])
                throw new ArgumentException("Two distinct player IDs are required.", nameof(playerIds));
            if (players == null || players.Length != 2)
                throw new ArgumentException("Two initialized player states are required.", nameof(players));

            var snapshots = new MatchPlayerSnapshot[2];
            for (int i = 0; i < 2; i++)
            {
                var player = players[i];
                if (player?.Rating == null || player.Rank == null || player.Inventory?.OwnedVariants == null)
                    throw new ArgumentException("Load server player state before creating a match.", nameof(players));
                snapshots[i] = new MatchPlayerSnapshot
                {
                    Rating = new RatingRecord { R = player.Rating.R, Rd = player.Rating.Rd,
                        Sigma = player.Rating.Sigma, LastMatchUnixSeconds = player.Rating.LastMatchUnixSeconds },
                    Rank = new RankRecord { RR = player.Rank.RR, Arena = player.Rank.Arena,
                        HighestArena = player.Rank.HighestArena },
                    OwnedVariants = new List<string>(player.Inventory.OwnedVariants)
                };
            }
            return new MatchRecord
            {
                matchId = matchId, playerIds = (string[])playerIds.Clone(), players = snapshots,
                createdUnixSeconds = nowUnixSeconds, protocol = protocol, sim = sim, content = content, mapId = mapId, boardHash = boardHash,
                state = MatchRecordState.Open
            };
        }
    }
}
