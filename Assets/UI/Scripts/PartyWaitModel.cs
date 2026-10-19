using System;

namespace NodeWar.Lobby
{
    public sealed class PartyWaitModel
    {
        public LobbyMode Mode { get; }
        public int Capacity => Mode == LobbyMode.FreeForAll4 ? 4 : 2;
        public int Filled { get; private set; } = 1;
        public long ElapsedMilliseconds { get; private set; }
        public bool Cancelled { get; private set; }
        public bool CanQueueRandom => !Cancelled && Mode == LobbyMode.Team2v2 && ElapsedMilliseconds >= 15000;
        public bool BattleReady => !Cancelled && Mode == LobbyMode.FreeForAll4 && Filled == Capacity;

        public PartyWaitModel(LobbyMode mode)
        {
            if (mode != LobbyMode.Team2v2 && mode != LobbyMode.FreeForAll4) throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
        }
        public void Advance(long elapsedMilliseconds)
        {
            if (!Cancelled) ElapsedMilliseconds = Math.Max(ElapsedMilliseconds, elapsedMilliseconds);
        }
        public void SetFilled(int count)
        {
            if (!Cancelled) Filled = Math.Max(1, Math.Min(Capacity, count));
        }
        public void Cancel() { Cancelled = true; }
    }
}
