using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    public sealed class BadgeBoard
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        public event Action Changed;

        public void Set(string key, int count)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (count <= 0) { Clear(key); return; }
            if (counts.TryGetValue(key, out int previous) && previous == count) return;
            counts[key] = count;
            Changed?.Invoke();
        }

        public void Clear(string key)
        {
            if (key != null && counts.Remove(key)) Changed?.Invoke();
        }

        public int Count(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            long total = 0;
            foreach (var entry in counts)
                if (entry.Key == key || entry.Key.StartsWith(key + ".", StringComparison.Ordinal)) total += entry.Value;
            return (int)Math.Min(int.MaxValue, total);
        }
    }
}
