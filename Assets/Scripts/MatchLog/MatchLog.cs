using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.MatchLog
{
    public enum MatchKind : byte { Local = 0, Bot = 1, Networked = 2 }
    public enum MatchEndReason : byte
    {
        Win = 1,
        Disconnect = 2,
        Abandoned = 3,
        Surrender = 4 // Reserved; no match driver produces this yet.
    }

    public sealed class MatchLogHeader
    {
        public ushort protocol;
        public ushort sim;
        public int content;
        public string matchId;
        public string[] playerIds; // Exactly two; an empty entry means no identity.
        public byte localPlayer;
        public int tier;
        public int seed;
        public long startUnixSeconds;
        public MatchKind kind;
    }

    public sealed class PlayerLoadout
    {
        public int[] suits;
        public int[] nodes;
    }

    public struct LoggedTick
    {
        public int tick; // state.tickCount before applying commands.
        public GameCommand[] commands;
    }

    public struct HashCheckpoint
    {
        public int tick; // state.tickCount after SimulateTick.
        public int hash;
    }

    public sealed class MatchResult
    {
        public MatchEndReason reason;
        public int winner = -1;
        public int endTick;
        public int finalHash;
        public int firstDesyncTick = -1;
    }

    public sealed class MatchLog
    {
        public MatchLogHeader header;
        public BoardConfigData board;
        public PlayerLoadout[] loadouts; // Exactly two, in player order.
        public DraftPlacement[] draft;
        public List<LoggedTick> ticks;
        public List<HashCheckpoint> hashes;
        public MatchResult result; // Null means the file has no RESULT chunk.
    }
}
