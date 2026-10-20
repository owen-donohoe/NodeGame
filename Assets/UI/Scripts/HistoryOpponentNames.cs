using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>One in-flight/completed profile lookup per opponent, scoped to an account session.</summary>
    public sealed class HistoryOpponentNames
    {
        private readonly IProfileService profiles;
        private readonly Dictionary<string, Task<string>> names = new Dictionary<string, Task<string>>();
        public HistoryOpponentNames(IProfileService profiles) { this.profiles = profiles; }
        public void Clear() => names.Clear();

        public Task<string> ResolveAsync(MatchHistoryEntry entry)
        {
            if (HasHandle(entry.OpponentName, entry.OpponentTag, entry.opponentPlayerId))
                return Task.FromResult(entry.OpponentName + "#" + entry.OpponentTag);
            string id = entry.opponentPlayerId;
            if (string.IsNullOrEmpty(id)) return Task.FromResult("Unknown");
            if (!names.TryGetValue(id, out var lookup)) names[id] = lookup = Lookup(id);
            return lookup;
        }

        private async Task<string> Lookup(string id)
        {
            try
            {
                var card = (await profiles.GetAsync(id))?.Card;
                return card != null && HasHandle(card.Name, card.Tag, id) ? card.Name + "#" + card.Tag : "Unknown";
            }
            catch (Exception) { return "Unknown"; }
        }

        private static bool HasHandle(string name, string tag, string id) =>
            !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(tag) && name != id;
    }
}
