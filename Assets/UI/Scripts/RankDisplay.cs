using System;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Read-only arena progress derived from the server's visible RR.</summary>
    public readonly struct RankDisplay
    {
        public int RR { get; }
        public int Arena { get; }
        public string Name => RankTable.Names[Arena];
        public int RRIntoArena { get; }
        public int? Span { get; }
        public float Fill { get; }

        public RankDisplay(int rr)
        {
            RR = Math.Max(0, rr);
            int arena = 0;
            while (arena + 1 < RankTable.Thresholds.Count && RR >= RankTable.Thresholds[arena + 1])
                arena++;
            Arena = arena;
            RRIntoArena = RR - RankTable.Thresholds[arena];
            Span = arena + 1 < RankTable.Thresholds.Count
                ? RankTable.Thresholds[arena + 1] - RankTable.Thresholds[arena]
                : (int?)null;
            Fill = Span.HasValue ? (float)RRIntoArena / Span.Value : 1f;
        }
    }
}
