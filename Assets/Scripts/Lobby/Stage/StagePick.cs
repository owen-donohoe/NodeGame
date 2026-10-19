namespace NodeWar.Lobby
{
    /// <summary>Compact stage index; callers map it back to chosen-slot storage.</summary>
    public readonly struct StagePick
    {
        public bool IsSuit { get; }
        public int Index { get; }
        public StagePick(bool isSuit, int index) { IsSuit = isSuit; Index = index; }
    }
}
