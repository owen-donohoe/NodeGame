using System;
using System.Diagnostics;
using NodeWar.MatchLog;

namespace NodeWar.Cloud
{
    public sealed class RefereeVerdict
    {
        public bool ok;
        public string error;
        public int endTick;
        public bool gameOver;
        public int winner = -1;
        public int finalHash;
        public int firstMismatchTick = -1;
        public long elapsedMs;
        public int ticksReplayed;
        public int[] breaches;

        public static RefereeVerdict Refused(string error) => new RefereeVerdict { error = error };
    }

    /// <summary>Checks log consistency, not command authenticity or eligibility for rating.</summary>
    public sealed class Referee
    {
        public const int MaxLogBytes = 512 * 1024;
        // MatchFactory.Configure mutates simulation statics. All referee instances
        // in this process must share this gate, including ones using other balances.
        private static readonly object replayLock = new object();
        private readonly BalanceCatalog balances;

        private readonly NodeWar.Simulation.IBoardCatalog boards;

        public Referee(BalanceCatalog balances, NodeWar.Simulation.IBoardCatalog boards = null)
        {
            this.boards = boards ?? NodeWar.Simulation.PremadeMaps.Catalog;
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
        }

        private string CheckMap(NodeWar.MatchLog.MatchLog log)
        {
            if (log.setup == null) return "log has no match setup";
            if (!boards.TryGet(log.setup.MapId, out NodeWar.Simulation.BoardConfigData shipped))
                return "unknown map " + log.setup.MapId;
            int shippedHash = NodeWar.Simulation.BoardHasher.Hash(shipped);
            if (NodeWar.Simulation.BoardHasher.Hash(log.board) != shippedHash || log.setup.BoardHash != shippedHash)
                return "recorded board does not match the server's map catalog for " + log.setup.MapId;
            return null;
        }

        public RefereeVerdict Verify(byte[] logBytes)
        {
            var watch = Stopwatch.StartNew();
            var verdict = new RefereeVerdict();
            try
            {
                if (logBytes != null && logBytes.Length > MaxLogBytes)
                {
                    verdict.error = "log exceeds 512 KB";
                    return verdict;
                }
                if (!MatchLogFormat.TryRead(logBytes, out var log, out string error))
                {
                    verdict.error = error;
                    return verdict;
                }
                if (!balances.TryGet(log.header.content, out var balance))
                {
                    verdict.error = "unknown balance";
                    return verdict;
                }
                // A log of this simulation must name a map this server ships, and its board must
                // BE that map's board. The hash a client writes is never the authority: an
                // attacker can recompute it for a board of their own. Logs from other
                // simulation versions get MatchReplay's version refusal instead.
                if (log.header.sim == NodeWar.Simulation.SimulationVersion.Current)
                {
                    string mapError = CheckMap(log);
                    if (mapError != null)
                    {
                        verdict.error = mapError;
                        return verdict;
                    }
                }
                ReplayOutcome outcome;
                lock (replayLock)
                    outcome = MatchReplay.Run(log, balance);

                verdict.ok = outcome.ok;
                verdict.error = outcome.error;
                verdict.endTick = outcome.endTick;
                verdict.gameOver = outcome.gameOver;
                verdict.winner = outcome.winner;
                verdict.finalHash = outcome.finalHash;
                verdict.firstMismatchTick = outcome.firstMismatchTick;
                verdict.ticksReplayed = outcome.endTick;
                verdict.breaches = outcome.breaches;
                return verdict;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
                                       ex is IndexOutOfRangeException || ex is ArithmeticException)
            {
                // Syntactically readable input can still be unsuitable for the sim.
                verdict.error = "invalid match log";
                return verdict;
            }
            finally
            {
                // Includes parsing and time queued behind another replay.
                verdict.elapsedMs = watch.ElapsedMilliseconds;
            }
        }
    }
}
