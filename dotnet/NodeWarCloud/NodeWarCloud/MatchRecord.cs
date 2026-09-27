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
        public MatchPlayerSnapshot[] players;
        public MatchRecordState state;
        public List<MatchReport> reports = new List<MatchReport>();
        public long pendingUnixSeconds;
        public long settlementUnixSeconds;
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
        // Durable until copied to log-N. Retries save the winning record's log,
        // never a concurrent upload that lost the record write-lock race.
        public string pendingLogBase64;
    }

    public static class MatchRecords
    {
        public static MatchRecord Create(string matchId, string[] playerIds, PlayerState[] players,
            long nowUnixSeconds, ushort protocol, ushort sim, int content)
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
                createdUnixSeconds = nowUnixSeconds, protocol = protocol, sim = sim, content = content,
                state = MatchRecordState.Open
            };
        }
    }
}
