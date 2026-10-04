using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>The visible arenas, indexed from zero in stored rank records.</summary>
    public static class RankTable
    {
        public static readonly IReadOnlyList<int> Thresholds =
            Array.AsReadOnly(new[] { 0, 300, 700, 1200, 1800, 2500 });

        public static readonly IReadOnlyList<string> Names =
            Array.AsReadOnly(new[] { "Arena 1", "Arena 2", "Arena 3", "Arena 4", "Arena 5", "Arena 6" });
    }
}
