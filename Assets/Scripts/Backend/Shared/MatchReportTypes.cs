namespace NodeWar.Backend
{
    /// <summary>
    /// Where a ranked match stands on the server. Stored by value in match
    /// records and returned to the client, so never reorder or remove a member.
    /// </summary>
    public enum MatchRecordState { Open, Pending, Settled, Void, Disputed }

    /// <summary>
    /// What ReportMatch returns. Shared so the client reads the same shape the
    /// Cloud Code module writes. Null state means the upload was refused before
    /// it reached a match record.
    /// </summary>
    public sealed class MatchReportingResult
    {
        public MatchRecordState? state;
        public string message;
        public PlayerState playerState;
    }

    /// <summary>
    /// One match in the caller's own history, newest first. `won`, `rrDelta`
    /// and `arenaAfter` are only meaningful when `state` is `Settled`; they are
    /// null otherwise (Open, Pending, Void or Disputed carry no outcome).
    /// </summary>
    public sealed class MatchHistoryEntry
    {
        public string matchId;
        public string opponentPlayerId;
        public MatchRecordState state;
        public long createdUnixSeconds;
        public bool? won;
        public int? rrDelta;
        public int? arenaAfter;
    }
}
