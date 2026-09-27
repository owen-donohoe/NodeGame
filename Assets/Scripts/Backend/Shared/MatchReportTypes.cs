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
}
