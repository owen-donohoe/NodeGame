using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NodeWar.BalanceRig
{
    public sealed class RigOptions
    {
        public int matches = 100;
        public int seed = 1;
        public int cap = 6000;
        public string outPath = "balance-rig.csv";
        public string balancePath;
        public string boardPath;

        /// <summary>
        /// Districts each player brings beyond the board's base draft pool.
        /// Barracks by default: BotPlayer cannot equip a soldier without one,
        /// and the shipped bot-match board gives the bot none.
        /// </summary>
        public string loadout = "Barracks";

        /// <summary>Ticks between a bot issuing a command and it applying. 0 is the live bot path.</summary>
        public int delay = 0;

        /// <summary>Play every seed twice, the second time with the board mirrored and the two players' setups swapped. --matches N then means N seeds and 2N matches.</summary>
        public bool swapSeats = true;

        /// <summary>Copy the v2 balance fields from GameBalanceData.Default() onto the loaded export.</summary>
        public bool v2Overlay;

        /// <summary>Seed to replay with a state line every 200 ticks and the draft, or -1.</summary>
        public int trace = -1;

        private static bool ParseOnOff(string key, string value)
        {
            if (value.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            throw new ArgumentException(key + " takes on or off, not '" + value + "'.");
        }

        public static RigOptions Parse(string[] args)
        {
            var o = new RigOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                string Next()
                {
                    if (i + 1 >= args.Length) throw new ArgumentException(key + " needs a value.");
                    return args[++i];
                }
                int Int() => int.Parse(Next(), CultureInfo.InvariantCulture);

                switch (key)
                {
                    case "--matches": o.matches = Int(); break;
                    case "--seed": o.seed = Int(); break;
                    case "--cap": o.cap = Int(); break;
                    case "--out": o.outPath = Next(); break;
                    case "--balance": o.balancePath = Next(); break;
                    case "--board": o.boardPath = Next(); break;
                    case "--loadout": o.loadout = Next(); break;
                    case "--delay": o.delay = Int(); break;
                    case "--swap-seats": o.swapSeats = ParseOnOff(key, Next()); break;
                    case "--v2-overlay": o.v2Overlay = true; break;
                    case "--trace": o.trace = Int(); break;
                    default: throw new ArgumentException("Unknown argument '" + key + "'.");
                }
            }
            if (o.matches < 0 || o.cap <= 0 || o.delay < 0) throw new ArgumentException("--matches, --cap and --delay must be positive.");
            return o;
        }
    }

    public static class Program
    {
        /// <summary>
        /// One match per seed, or two with seat swap on: the seed as supplied,
        /// then its mirrored pair. Rows come out seed by seed, seat 0 first.
        /// </summary>
        private static RunHooks Observed => new RunHooks { timeline = true };

        public static List<MatchResult> RunMatches(RigSetup setup, RigOptions options, Action<int> progress = null)
        {
            var results = new List<MatchResult>(options.matches * (options.swapSeats ? 2 : 1));
            for (int i = 0; i < options.matches; i++)
            {
                PreparedMatch first = MatchRunner.Prepare(setup, options.seed + i);
                results.Add(MatchRunner.Run(first, options.cap, options.delay, null, Observed));
                if (options.swapSeats)
                    results.Add(MatchRunner.Run(MatchRunner.SwapSeats(first), options.cap, options.delay, null, Observed));
                progress?.Invoke(i + 1);
            }
            return results;
        }

        public const string Usage =
            "NodeWar.BalanceRig --matches N --seed S --cap TICKS --out path.csv\n"
            + "                   [--loadout Barracks,...|none] [--delay TICKS] [--swap-seats on|off] [--v2-overlay] [--balance file.json] [--board file.asset]";

        public static int Main(string[] args)
        {
            RigOptions options;
            try { options = RigOptions.Parse(args); }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(Usage);
                return 2;
            }

            RigSetup setup = RigSetupLoader.Load(options.balancePath, options.boardPath, options.loadout, options.v2Overlay);
            Console.WriteLine("balance: " + setup.balanceSource + " (source hash " + setup.sourceBalanceHash + ", effective hash " + setup.balanceHash + ")");
            if (options.v2Overlay) Console.WriteLine("v2 overlay: " + (setup.overlaidFields.Length == 0 ? "nothing to overlay" : string.Join(",", setup.overlaidFields)));
            Console.WriteLine("board:   " + setup.boardSource + " (" + setup.board.gridCols + "x" + setup.board.gridRows + ")");
            Console.WriteLine("loadout: " + (setup.loadoutNodes.Length == 0 ? "none" : string.Join(",", setup.loadoutNodes))
                + "; eras 0; input delay " + options.delay + "; swap seats " + (options.swapSeats ? "on (" + options.matches + " seeds, " + 2 * options.matches + " matches)" : "off") + "; cap " + options.cap + " ticks");

            if (options.trace >= 0)
            {
                var draft = MatchRunner.RandomDraft(setup, new Random(options.trace));
                foreach (var dp in draft)
                    Console.WriteLine("draft P" + dp.playerID + " " + dp.districtType + " at (" + dp.gridX + "," + dp.gridZ
                        + ") node " + (dp.gridZ * setup.board.gridCols + dp.gridX));
                MatchResult traced = MatchRunner.Run(setup, options.trace, options.cap, options.delay, Console.Out);
                Console.WriteLine(Report.Header);
                Console.WriteLine(Report.Row(traced));
                return 0;
            }

            var clock = Stopwatch.StartNew();
            List<MatchResult> results = RunMatches(setup, options,
                done => { if (done % 100 == 0) Console.Error.WriteLine("  " + done + "/" + options.matches); });
            clock.Stop();

            Report.WriteCsv(options.outPath, results);
            Console.WriteLine("csv:     " + options.outPath + " (seeds " + options.seed + ".." + (options.seed + options.matches - 1) + ")");
            Console.WriteLine();
            Console.Write(Report.Summary(results, clock.Elapsed.TotalSeconds));
            return 0;
        }
    }
}
