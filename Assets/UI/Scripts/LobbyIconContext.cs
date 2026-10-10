namespace NodeWar.Lobby
{
    /// <summary>Optional presentation location. Zero preserves existing theme entries.</summary>
    public enum LobbyIconContext
    {
        Anywhere = 0,
        TopBar,
        PageHeader,
        NavBar,
        HeadsUpDisplay,
        NodeSheet,
        InlineText,
        EmotePicker,
        EmoteBubble,
        OffScreenIndicator,
        ShopCard,
        // 11 is reserved: the retired SuitTree context. Never reuse it; saved themes may still hold it.
        Workshop = 12,
        Profile,
        Home
    }
}
