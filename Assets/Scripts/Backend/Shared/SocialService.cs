using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public enum PlayerPresence { Offline, Online, InBattle }

    public sealed class PlayerCard
    {
        public string PlayerId;
        public string Name;
        public string Tag;
        public int Arena;
        public int RR;
        public PlayerPresence Presence;
    }

    public sealed class LiveMatch
    {
        public string MatchId;
        public PlayerCard A;
        public PlayerCard B;
    }

    public interface ISocialService
    {
        Task<List<LiveMatch>> GetLiveMatchesAsync();
        Task<List<PlayerCard>> GetFriendsAsync();
    }

    /// <summary>Deterministic placeholder roster; every call returns independent DTOs.</summary>
    public sealed class LocalSocialService : ISocialService
    {
        public Task<List<PlayerCard>> GetFriendsAsync() => Task.FromResult(new List<PlayerCard>
        {
            Sample("sample-ash"), Sample("sample-brook"), Sample("sample-cedar")
        });

        public Task<List<LiveMatch>> GetLiveMatchesAsync() => Task.FromResult(new List<LiveMatch>
        {
            new LiveMatch { MatchId = "sample-live-1", A = Sample("sample-ash"), B = Sample("sample-dune") }
        });

        public static PlayerCard Sample(string id)
        {
            switch (id)
            {
                case "sample-ash": return new PlayerCard { PlayerId = id, Name = "Ash", Tag = "ASH", Arena = 1, RR = 240, Presence = PlayerPresence.InBattle };
                case "sample-brook": return new PlayerCard { PlayerId = id, Name = "Brook", Tag = "BRK", Arena = 1, RR = 200, Presence = PlayerPresence.Online };
                case "sample-cedar": return new PlayerCard { PlayerId = id, Name = "Cedar", Tag = "CDR", Arena = 0, RR = 80, Presence = PlayerPresence.Offline };
                case "sample-dune": return new PlayerCard { PlayerId = id, Name = "Dune", Tag = "DNE", Arena = 1, RR = 220, Presence = PlayerPresence.InBattle };
                default: return null;
            }
        }
    }
}
