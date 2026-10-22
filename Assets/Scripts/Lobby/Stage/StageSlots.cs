namespace NodeWar.Lobby
{
    public readonly struct StageSlot
    {
        public int Row { get; }
        public int Column { get; }
        public bool Filled { get; }

        public StageSlot(int row, int column, bool filled)
        {
            Row = row;
            Column = column;
            Filled = filled;
        }
    }

    public static class StageSlots
    {
        public const int Count = 5;
        public static bool HasEmpty(int equipped, int capacity) => System.Math.Max(0, equipped) < System.Math.Max(0, System.Math.Min(Count, capacity));

        public static StageSlot[] Layout(int equipped)
        {
            int count = equipped < 0 ? 0 : equipped > Count ? Count : equipped;
            var slots = new StageSlot[Count];
            for (int i = 0; i < Count; i++)
                slots[i] = new StageSlot(i < 3 ? 1 : 0, i < 3 ? i : i - 3, i < count);
            return slots;
        }
    }
}
