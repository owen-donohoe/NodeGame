using System;

namespace NodeWar.Progression
{
    public sealed class ArenaConfig
    {
        public int[] Thresholds { get; set; } = { 0, 300, 700, 1200, 1800, 2500 };

        public void Validate()
        {
            if (Thresholds == null || Thresholds.Length == 0 || Thresholds[0] != 0)
                throw new ArgumentException("Arena thresholds must start at zero.", nameof(Thresholds));
            for (int i = 1; i < Thresholds.Length; i++)
                if (Thresholds[i] <= Thresholds[i - 1])
                    throw new ArgumentException("Arena thresholds must be strictly increasing.", nameof(Thresholds));
        }
    }

    public readonly struct RankState
    {
        public int RR { get; }
        public int Arena { get; }
        public int HighestArena { get; }

        public RankState(int rr, int arena, int highestArena)
        {
            RR = rr;
            Arena = arena;
            HighestArena = highestArena;
        }
    }

    public static class Arenas
    {
        public static int ArenaForRR(int rr, ArenaConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            cfg.Validate();
            rr = Math.Max(0, rr);
            for (int i = cfg.Thresholds.Length - 1; i > 0; i--)
                if (rr >= cfg.Thresholds[i]) return i;
            return 0;
        }

        public static RankState ApplyRRDelta(RankState state, int delta, ArenaConfig cfg)
        {
            // Widen before adding so losses can floor safely and gains never wrap.
            int rr = checked((int)Math.Max(0L, (long)state.RR + delta));
            int arena = ArenaForRR(rr, cfg);
            return new RankState(rr, arena, Math.Max(state.HighestArena, arena));
        }
    }
}
