using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// Uploads a finished match's log for server-side settlement. The server
    /// is the referee: this call reports what happened, it does not decide it.
    /// </summary>
    public interface IMatchReportService
    {
        /// <summary>Reports a match log; may fail (offline, refused, invalid). Display the result, never assume settlement.</summary>
        Task<MatchReportingResult> ReportAsync(string matchId, byte[] log);
    }

    /// <summary>
    /// Offline stand-in for the backend: validates arguments the way the
    /// server would refuse them, then hands back a configurable result.
    /// Records every call for tests. No settlement logic -- that lives on the
    /// server (MatchReporting in NodeWarCloud).
    /// </summary>
    public sealed class LocalMatchReportService : IMatchReportService
    {
        /// <summary>One recorded call, for tests to inspect.</summary>
        public sealed class Call
        {
            public string MatchId;
            public byte[] Log;
        }

        private readonly List<Call> calls = new List<Call>();

        /// <summary>Calls made so far, in order.</summary>
        public IReadOnlyList<Call> Calls => calls;

        /// <summary>Returned by ReportAsync for every call. Defaults to a refused-looking Pending result.</summary>
        public MatchReportingResult Result { get; set; } = new MatchReportingResult
        {
            state = MatchRecordState.Pending,
            message = "local fake"
        };

        public Task<MatchReportingResult> ReportAsync(string matchId, byte[] log)
        {
            if (string.IsNullOrEmpty(matchId)) throw new ArgumentException("matchId must not be null or empty.", nameof(matchId));
            if (log == null || log.Length == 0) throw new ArgumentException("log must not be null or empty.", nameof(log));

            calls.Add(new Call { MatchId = matchId, Log = log });
            return Task.FromResult(Result);
        }
    }
}
