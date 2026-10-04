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
                    default: throw new ArgumentException("Unknown argument '" + key + "'.");
                }
            }
            if (o.matches < 0 || o.cap <= 0 || o.delay < 0) throw new ArgumentException("--matches, --cap and --delay must be positive.");
            return o;
        }
    }

    public static class Program
    {
        public const string Usage =
            "NodeWar.BalanceRig --matches N --seed S --cap TICKS --out path.csv\n"
            + "                   [--loadout Barracks,...|none] [--delay TICKS] [--balance file.json] [--board file.asset]";

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

            RigSetup setup = RigSetupLoader.Load(options.balancePath, options.boardPath, options.loadout);
            Console.WriteLine("balance: " + setup.balanceSource + " (hash " + setup.balanceHash + ")");
            Console.WriteLine("board:   " + setup.boardSource + " (" + setup.board.gridCols + "x" + setup.board.gridRows + ")");
            Console.WriteLine("loadout: " + (setup.loadoutNodes.Length == 0 ? "none" : string.Join(",", setup.loadoutNodes))
                + "; eras 0; input delay " + options.delay + "; cap " + options.cap + " ticks");

            var results = new List<MatchResult>(options.matches);
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < options.matches; i++)
            {
                results.Add(MatchRunner.Run(setup, options.seed + i, options.cap, options.delay));
                if ((i + 1) % 100 == 0) Console.Error.WriteLine("  " + (i + 1) + "/" + options.matches);
            }
            clock.Stop();

            Report.WriteCsv(options.outPath, results);
            Console.WriteLine("csv:     " + options.outPath + " (seeds " + options.seed + ".." + (options.seed + options.matches - 1) + ")");
            Console.WriteLine();
            Console.Write(Report.Summary(results, clock.Elapsed.TotalSeconds));
            return 0;
        }
    }
}
