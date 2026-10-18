using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public interface IHandleService
    {
        Task<PlayerState> SetHandleAsync(string name, string tag, string requestId);
    }

    /// <summary>Pure handle presentation and migration from the local username.</summary>
    public static class HandleDisplay
    {
        public static string Display(ProfileRecord profile, string fallback) =>
            profile != null ? profile.Name + "#" + profile.Tag : fallback;

        public static string FirstRunName(string localName)
        {
            var name = new System.Text.StringBuilder();
            foreach (char c in localName ?? "")
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-')
                    name.Append(c);
                if (name.Length == 16) break;
            }
            if (name.Length < 3) name.Append("Player");
            return name.ToString();
        }
    }

    /// <summary>
    /// The client's view of its own server-side state. Every call can fail
    /// (offline, not signed in, server refused), and callers show what comes
    /// back rather than guessing ahead of it.
    /// </summary>
    public interface IPlayerStateService
    {
        /// <summary>The player's whole state, created on the server on first call.</summary>
        Task<PlayerState> GetAsync();
    }

    /// <summary>
    /// Offline stand-in for the backend: the server's own rules over a store
    /// held in memory. Permanent tooling for Editor work without a network and
    /// for tests, not a stopgap.
    /// </summary>
    public sealed class LocalPlayerStateService : IPlayerStateService
    {
        private readonly IPlayerRecordStore store;
        private readonly Func<PlayerState, bool> updateInventory;

        public LocalPlayerStateService() : this(new InMemoryPlayerRecordStore()) { }

        public LocalPlayerStateService(IPlayerRecordStore store, Func<PlayerState, bool> updateInventory = null)
        {
            this.store = store;
            this.updateInventory = updateInventory;
        }

        public Task<PlayerState> GetAsync()
        {
            return PlayerStateLogic.GetOrCreateAsync(store, updateInventory);
        }
    }

    /// <summary>
    /// A record store in memory. Records are held as separate objects, as
    /// Cloud Save holds separate keys. Reads hand back the stored objects, not
    /// copies, which is fine while nothing but the logic writes to them.
    /// </summary>
    public sealed class InMemoryPlayerRecordStore : IPlayerRecordStore
    {
        private readonly PlayerState records = new PlayerState();

        /// <summary>How many times WriteAsync has been called. For tests.</summary>
        public int WriteCount { get; private set; }

        public Task<PlayerState> ReadAsync()
        {
            return Task.FromResult(new PlayerState
            {
                Rating = records.Rating,
                Rank = records.Rank,
                Inventory = records.Inventory,
                History = records.History,
                Discipline = records.Discipline,
                Wallet = records.Wallet,
                Profile = records.Profile
            });
        }

        public Task WriteAsync(PlayerState written)
        {
            WriteCount++;
            if (written.Rating != null) records.Rating = written.Rating;
            if (written.Rank != null) records.Rank = written.Rank;
            if (written.Inventory != null) records.Inventory = written.Inventory;
            if (written.History != null) records.History = written.History;
            if (written.Discipline != null) records.Discipline = written.Discipline;
            if (written.Wallet != null) records.Wallet = written.Wallet;
            if (written.Profile != null) records.Profile = written.Profile;
            return Task.CompletedTask;
        }
    }
}
