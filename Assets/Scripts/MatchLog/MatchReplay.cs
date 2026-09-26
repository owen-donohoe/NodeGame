using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    /// <summary>What replaying a log found. <c>ok</c> is false on any mismatch or refusal.</summary>
    public sealed class ReplayOutcome
    {
        public bool ok;
        public string error;

        /// <summary>The first logged hash checkpoint the replay disagreed with, or -1.</summary>
        public int firstMismatchTick = -1;

        public int endTick;
        public bool gameOver;
        public int winner = -1;
        public int finalHash;
    }

    /// <summary>
    /// Replays a match log headlessly and checks it against itself: the board
    /// it describes, driven by the commands it records, must pass through every
    /// hash it records and end where its result says.
    ///
    /// This is the referee's core and the start of every other headless use of
    /// a log. It proves a log is internally consistent, not that it is honest:
    /// a client that fabricates commands and recomputes the hashes produces a
    /// consistent log. Signed commands (Stage 7) close that.
    ///
    /// Runs through <see cref="MatchFactory"/>, so it sets the simulation's
    /// statics: never run two replays at once in one process.
    /// </summary>
    public static class MatchReplay
    {
        /// <summary>
        /// About five and a half hours at 10 ticks a second. A limit on what a
        /// log may ask the replay to run, so a hostile end tick cannot keep a
        /// server busy.
        /// </summary>
        public const int MaxTicks = 200000;

        public static ReplayOutcome Run(MatchLog log, GameBalanceData balance)
        {
            var outcome = new ReplayOutcome();

            if (log == null || log.header == null) return Refuse(outcome, "No log.");
            if (log.header.sim != SimulationVersion.Current)
                return Refuse(outcome, "Log is from simulation version " + log.header.sim
                    + "; this build runs " + SimulationVersion.Current + ".");
            if (log.result == null) return Refuse(outcome, "Log has no result.");

            int endTick = log.result.endTick;
            if (endTick < 0 || endTick > MaxTicks) return Refuse(outcome, "End tick out of range.");
            if (log.loadouts == null || log.loadouts.Length != 2 || log.loadouts[0] == null || log.loadouts[1] == null)
                return Refuse(outcome, "Log needs two loadouts.");

            string orderError = CheckOrder(log, endTick);
            if (orderError != null) return Refuse(outcome, orderError);

            MatchFactory.Configure(balance, log.board);
            SimulationState state = MatchFactory.Build(balance, log.board, log.draft, new[]
            {
                new PlayerSetup { suits = log.loadouts[0].suits, nodes = log.loadouts[0].nodes,
                    suitEras = log.loadouts[0].suitEras, districtEras = log.loadouts[0].districtEras },
                new PlayerSetup { suits = log.loadouts[1].suits, nodes = log.loadouts[1].nodes,
                    suitEras = log.loadouts[1].suitEras, districtEras = log.loadouts[1].districtEras }
            });

            int nextTick = 0;
            int nextHash = 0;
            while (state.tickCount < endTick)
            {
                // The live runners stop ticking once the game is over, so an
                // honest log never asks for a tick past it.
                if (state.gameOver)
                    return Fail(outcome, state, "Game ended at tick " + state.tickCount
                        + " but the log runs to " + endTick + ".");

                if (nextTick < log.ticks.Count && log.ticks[nextTick].tick == state.tickCount)
                {
                    GameCommand[] commands = log.ticks[nextTick].commands;
                    for (int i = 0; i < commands.Length; i++)
                        CommandProcessor.ProcessCommand(state, commands[i]);
                    nextTick++;
                }

                GameSimulation.SimulateTick(state);

                if (nextHash < log.hashes.Count && log.hashes[nextHash].tick == state.tickCount)
                {
                    if (SimulationStateHasher.ComputeHash(state) != log.hashes[nextHash].hash)
                    {
                        outcome.firstMismatchTick = state.tickCount;
                        return Fail(outcome, state, "State hash differs at tick " + state.tickCount + ".");
                    }
                    nextHash++;
                }
            }

            Finish(outcome, state);
            if (outcome.finalHash != log.result.finalHash)
                return Fail(outcome, state, "Final state hash differs.");
            if (log.result.reason == MatchEndReason.Win && (!state.gameOver || state.winnerID != log.result.winner))
                return Fail(outcome, state, "The replay does not end in the logged win.");

            outcome.ok = true;
            return outcome;
        }

        /// <summary>
        /// Ticks and hash checkpoints strictly increasing and inside the match.
        /// Checked before anything runs, so the replay loop can walk both lists
        /// with one index each and never skip an entry.
        /// </summary>
        private static string CheckOrder(MatchLog log, int endTick)
        {
            int last = -1;
            for (int i = 0; i < log.ticks.Count; i++)
            {
                int tick = log.ticks[i].tick;
                if (tick <= last || tick >= endTick) return "Command ticks out of order or past the end.";
                if (log.ticks[i].commands == null) return "Command tick without commands.";
                last = tick;
            }

            last = 0;
            for (int i = 0; i < log.hashes.Count; i++)
            {
                int tick = log.hashes[i].tick;
                if (tick <= last || tick > endTick) return "Hash checkpoints out of order or past the end.";
                last = tick;
            }
            return null;
        }

        private static ReplayOutcome Refuse(ReplayOutcome outcome, string error)
        {
            outcome.ok = false;
            outcome.error = error;
            return outcome;
        }

        private static ReplayOutcome Fail(ReplayOutcome outcome, SimulationState state, string error)
        {
            Finish(outcome, state);
            return Refuse(outcome, error);
        }

        private static void Finish(ReplayOutcome outcome, SimulationState state)
        {
            outcome.endTick = state.tickCount;
            outcome.gameOver = state.gameOver;
            outcome.winner = state.gameOver ? state.winnerID : -1;
            outcome.finalHash = SimulationStateHasher.ComputeHash(state);
        }
    }
}
