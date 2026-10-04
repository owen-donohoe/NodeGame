using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NodeWar.BalanceRig
{
    public static class Report
    {
        public const string Header =
            "seed,ticks,winner,capped,"
            + "p0_breaches,p1_breaches,p0_breach_ticks,p1_breach_ticks,first_breach_tick,"
            + "p0_nodes,p1_nodes,p0_villagers,p1_villagers,"
            + "p0_food,p1_food,p0_materials,p1_materials,p0_metal,p1_metal";

        public static string Row(MatchResult r)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            void Add(object v) { if (sb.Length > 0) sb.Append(','); sb.Append(Convert.ToString(v, inv)); }

            Add(r.seed); Add(r.ticks); Add(r.winner); Add(r.capped ? 1 : 0);
            Add(r.breaches[0]); Add(r.breaches[1]);
            Add(string.Join(";", r.breachTicks[0])); Add(string.Join(";", r.breachTicks[1]));
            Add(r.FirstBreachTick);
            Add(r.nodesOwned[0]); Add(r.nodesOwned[1]);
            Add(r.villagersAlive[0]); Add(r.villagersAlive[1]);
            Add(r.food[0]); Add(r.food[1]);
            Add(r.materials[0]); Add(r.materials[1]);
            Add(r.metal[0]); Add(r.metal[1]);
            return sb.ToString();
        }

        public static void WriteCsv(string path, IList<MatchResult> results)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var lines = new List<string>(results.Count + 1) { Header };
            foreach (MatchResult r in results) lines.Add(Row(r));
            File.WriteAllLines(path, lines);
        }

        /// <summary>Nearest-rank percentile of a sorted list, p in 0..100.</summary>
        public static int Percentile(List<int> sorted, int p)
        {
            if (sorted.Count == 0) return -1;
            int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, rank - 1))];
        }

        public static string Summary(IList<MatchResult> results, double seconds)
        {
            var inv = CultureInfo.InvariantCulture;
            int n = results.Count;
            int p0 = 0, p1 = 0, capped = 0;
            double breaches0 = 0, breaches1 = 0;
            var lengths = new List<int>();
            var firstBreach = new List<int>();

            foreach (MatchResult r in results)
            {
                if (r.capped) capped++;
                else
                {
                    if (r.winner == 0) p0++;
                    else if (r.winner == 1) p1++;
                    lengths.Add(r.ticks);
                }
                breaches0 += r.breaches[0];
                breaches1 += r.breaches[1];
                if (r.FirstBreachTick >= 0) firstBreach.Add(r.FirstBreachTick);
            }
            lengths.Sort();
            firstBreach.Sort();

            string Pct(double k) => n == 0 ? "n/a" : (100.0 * k / n).ToString("0.0", inv) + "%";
            string Quartiles(List<int> v) => v.Count == 0 ? "none"
                : "p25=" + Percentile(v, 25) + " p50=" + Percentile(v, 50)
                  + " p75=" + Percentile(v, 75) + " p90=" + Percentile(v, 90);

            var sb = new StringBuilder();
            sb.AppendLine("matches:        " + n);
            sb.AppendLine("P0 wins:        " + p0 + " (" + Pct(p0) + ")");
            sb.AppendLine("P1 wins:        " + p1 + " (" + Pct(p1) + ")");
            sb.AppendLine("capped:         " + capped + " (" + Pct(capped) + ")");
            sb.AppendLine("length (ticks, finished matches only, n=" + lengths.Count + "): " + Quartiles(lengths));
            sb.AppendLine("first breach (ticks, matches with a breach, n=" + firstBreach.Count + "): " + Quartiles(firstBreach));
            sb.AppendLine("mean breaches:  P0 suffered " + (n == 0 ? "n/a" : (breaches0 / n).ToString("0.00", inv))
                + ", P1 suffered " + (n == 0 ? "n/a" : (breaches1 / n).ToString("0.00", inv))
                + " (a breach is against that side's core)");
            sb.AppendLine("speed:          " + (seconds > 0 ? (n / seconds).ToString("0.0", inv) : "n/a")
                + " matches/s (" + seconds.ToString("0.0", inv) + " s)");
            return sb.ToString();
        }
    }
}
