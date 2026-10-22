namespace NodeWar.Lobby
{
    /// <summary>Knob travel for the shared toggle, from the design numbers rather than measured layout.</summary>
    public static class LobbyToggleMath
    {
        public const float TrackWidth = 48f, TrackHeight = 28f, Border = 2f, KnobSize = 20f, Inset = 2f;
        public static float InnerWidth => TrackWidth - 2f * Border;
        /// <summary>Left edge of the knob inside the track border: the inset when off, the opposite inner edge when on.</summary>
        public static float KnobX(bool on) => on ? InnerWidth - KnobSize - Inset : Inset;
    }
}
