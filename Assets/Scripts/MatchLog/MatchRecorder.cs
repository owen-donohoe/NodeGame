using System;
using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    /// <summary>
    /// Captures the order actually applied by the driver. Regrouping commands by
    /// player here would replay a different match when players interleave.
    /// </summary>
    public sealed class MatchRecorder
    {
        private int firstDesyncTick = -1;
        private bool hasDesync;

        public MatchRecorder(MatchLogHeader header, BoardConfigData board,
            PlayerLoadout[] loadouts, DraftPlacement[] draft)
        {
            Log = new MatchLog
            {
                header = header,
                board = board,
                loadouts = loadouts,
                draft = draft,
                ticks = new List<LoggedTick>(),
                hashes = new List<HashCheckpoint>()
            };
        }

        public MatchLog Log { get; }
        public bool IsFinished { get; private set; }

        /// <summary>
        /// Drivers may reuse their input buffers next tick, so retain a copy.
        /// Empty ticks are implicit in the tick numbers and need no entry.
        /// </summary>
        public void RecordTick(int tickBefore, GameCommand[] applied)
        {
            if (IsFinished || applied == null || applied.Length == 0) return;
            Log.ticks.Add(new LoggedTick
            {
                tick = tickBefore,
                commands = (GameCommand[])applied.Clone()
            });
        }

        public void RecordHash(int tickAfter, int hash)
        {
            if (IsFinished) return;
            Log.hashes.Add(new HashCheckpoint { tick = tickAfter, hash = hash });
        }

        public void RecordDesync(int tick)
        {
            if (IsFinished || hasDesync) return;
            firstDesyncTick = tick;
            hasDesync = true;
        }

        /// <summary>
        /// Several lifecycle callbacks may end a match. The first owns the
        /// result; later disconnect or shutdown callbacks cannot replace it.
        /// </summary>
        public void Finish(MatchResult result)
        {
            if (IsFinished) return;
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.firstDesyncTick == -1 && hasDesync)
                result.firstDesyncTick = firstDesyncTick;
            Log.result = result;
            IsFinished = true;
        }

        public byte[] ToBytes()
        {
            return MatchLogFormat.Write(Log);
        }
    }
}
