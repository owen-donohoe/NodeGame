using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// The caller's own ranked match history, newest first. Every call can
    /// fail (offline, not signed in); the caller shows what comes back, never
    /// a guess ahead of it.
    /// </summary>
    public interface IMatchHistoryService
    {
        /// <summary>The caller's history, as the server has it right now.</summary>
        Task<List<MatchHistoryEntry>> GetAsync();
    }

    /// <summary>
    /// Offline stand-in for the backend: hands back a settable list. Defaults
    /// to empty, matching a player with no ranked matches yet.
    /// </summary>
    public sealed class LocalMatchHistoryService : IMatchHistoryService
    {
        /// <summary>Returned by GetAsync. Defaults to empty.</summary>
        public List<MatchHistoryEntry> Entries { get; set; } = new List<MatchHistoryEntry>();

        public Task<List<MatchHistoryEntry>> GetAsync()
        {
            return Task.FromResult(Entries);
        }
    }
}
