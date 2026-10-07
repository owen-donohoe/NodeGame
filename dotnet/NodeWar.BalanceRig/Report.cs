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
            "seed,pair_id,seat,ticks,winner,capped,"
            + "p0_breaches,p1_breaches,p0_breach_ticks,p1_breach_ticks,first_breach_tick,"
            + "p0_nodes,p1_nodes,p0_villagers,p1_villagers,"
            + "p0_food,p1_food,p0_materials,p1_materials,p0_metal,p1_metal,"
            + "p0_barracks_end,p1_barracks_end,p0_barracks_ever,p1_barracks_ever,"
            + "p0_soldiers_end,p1_soldiers_end,p0_soldiers_peak,p1_soldiers_peak,"
            + "p0_ticks_3_idle_soldiers,p1_ticks_3_idle_soldiers,final_tick_breaches,won_below_base_threshold,"
            + "p0_ticks_idle_on_barracks,p1_ticks_idle_on_barracks,"
            + "p0_villages_end,p1_villages_end,p0_farms_end,p1_farms_end,p0_mines_end,p1_mines_end";

        public static string Row(MatchResult r)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            void Add(object v) { if (sb.Length > 0) sb.Append(','); sb.Append(Convert.ToString(v, inv)); }

            Add(r.seed); Add(r.pairID); Add(r.seat); Add(r.ticks); Add(r.winner); Add(r.capped ? 1 : 0);
            Add(r.breaches[0]); Add(r.breaches[1]);
            Add(string.Join(";", r.breachTicks[0])); Add(string.Join(";", r.breachTicks[1]));
            Add(r.FirstBreachTick);
            Add(r.nodesOwned[0]); Add(r.nodesOwned[1]);
            Add(r.villagersAlive[0]); Add(r.villagersAlive[1]);
            Add(r.food[0]); Add(r.food[1]);
            Add(r.materials[0]); Add(r.materials[1]);
            Add(r.metal[0]); Add(r.metal[1]);
            Add(r.ownsBarracksEnd[0] ? 1 : 0); Add(r.ownsBarracksEnd[1] ? 1 : 0);
            Add(r.everOwnedBarracks[0] ? 1 : 0); Add(r.everOwnedBarracks[1] ? 1 : 0);
            Add(r.soldiersEnd[0]); Add(r.soldiersEnd[1]);
            Add(r.peakSoldiers[0]); Add(r.peakSoldiers[1]);
            Add(r.ticksThreeIdleSoldiers[0]); Add(r.ticksThreeIdleSoldiers[1]);
            Add(r.finalTickBreaches); Add(r.wonBelowBaseThreshold ? 1 : 0);
            Add(r.ticksIdleOnBarracks[0]); Add(r.ticksIdleOnBarracks[1]);
            Add(r.villagesEnd[0]); Add(r.villagesEnd[1]); Add(r.farmsEnd[0]); Add(r.farmsEnd[1]);
            Add(r.minesEnd[0]); Add(r.minesEnd[1]);
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
            AppendPairs(sb, results);
            AppendDiagnostics(sb, results, inv);
            return sb.ToString();
        }

        /// <summary>
        /// Seat-swapped pairs, judged by who held the seed's original seat 0.
        /// A pair whose winner follows the seat rather than the setup says the
        /// tie-break or the board, not the draft, decided it; nothing here
        /// assumes the two matches come out equal.
        /// </summary>
        private static void AppendPairs(StringBuilder sb, IList<MatchResult> results)
        {
            var bySeat0 = new Dictionary<int, MatchResult>();
            var bySeat1 = new Dictionary<int, MatchResult>();
            foreach (MatchResult r in results)
                (r.seat == 0 ? bySeat0 : bySeat1)[r.pairID] = r;
            if (bySeat1.Count == 0) return;

            int pairs = 0, setupWinsBoth = 0, setupLosesBoth = 0, split = 0, seatDecided = 0, undecided = 0;
            foreach (var kv in bySeat0)
            {
                if (!bySeat1.TryGetValue(kv.Key, out MatchResult swapped)) continue;
                pairs++;
                MatchResult a = kv.Value;
                if (a.winner < 0 || swapped.winner < 0) { undecided++; continue; }
                bool firstSetupWonA = a.winner == 0;
                bool firstSetupWonB = swapped.winner == 1;
                if (firstSetupWonA && firstSetupWonB) setupWinsBoth++;
                else if (!firstSetupWonA && !firstSetupWonB) setupLosesBoth++;
                else split++;
                if (a.winner == swapped.winner) seatDecided++;
            }
            sb.AppendLine("seat-swapped pairs: " + pairs + " (first setup won both: " + setupWinsBoth
                + ", lost both: " + setupLosesBoth + ", split: " + split
                + "; winner followed the seat in " + seatDecided + "; a capped side in " + undecided + ")");
        }

        private static void AppendDiagnostics(StringBuilder sb, IList<MatchResult> results, CultureInfo inv)
        {
            int zero = 0, zeroLowPeak = 0, zeroNeverBarracks = 0, zeroIdle3 = 0, dropWins = 0, decided = 0;
            int sides = 0, sidesNoBarracks = 0, sidesPeakUnder3 = 0, sidesPeak3Plus = 0;
            long finalTick = 0;
            foreach (MatchResult r in results)
            {
                if (r.winner >= 0)
                {
                    decided++;
                    finalTick += r.finalTickBreaches;
                    if (r.wonBelowBaseThreshold) dropWins++;
                }
                if (!r.capped || r.breaches[0] + r.breaches[1] > 0) continue;

                zero++;
                bool anyIdle3 = false, neverBarracks = false;
                for (int p = 0; p < 2; p++)
                {
                    sides++;
                    if (!r.everOwnedBarracks[p]) { sidesNoBarracks++; neverBarracks = true; }
                    if (r.peakSoldiers[p] < 3) sidesPeakUnder3++; else sidesPeak3Plus++;
                    if (r.ticksThreeIdleSoldiers[p] > 0) anyIdle3 = true;
                }
                if (r.peakSoldiers[0] < 3 && r.peakSoldiers[1] < 3) zeroLowPeak++;
                if (neverBarracks) zeroNeverBarracks++;
                if (anyIdle3) zeroIdle3++;
            }
            sb.AppendLine("0-breach stalemates: " + zero
                + " (either side never owned a Barracks: " + zeroNeverBarracks
                + "; neither side ever had 3 soldiers: " + zeroLowPeak
                + "; either side ever held 3 idle soldiers: " + zeroIdle3 + ")");
            sb.AppendLine("  per side in those: never owned Barracks " + sidesNoBarracks + "/" + sides
                + ", peak soldiers < 3: " + sidesPeakUnder3 + "/" + sides + ", peak soldiers >= 3: " + sidesPeak3Plus + "/" + sides);
            sb.AppendLine("decided: " + decided + ", won with the loser below the base breach threshold (sudden-death drop): " + dropWins
                + "; mean breaches landing on the decisive tick: " + (decided == 0 ? "n/a" : ((double)finalTick / decided).ToString("0.00", inv)));
        }
    }
}
