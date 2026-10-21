namespace NodeWar.Lobby
{
    public enum LanRoomPage { Choice, Join, Connecting, Host, Guest }
    public sealed class LanRoomState
    {
        public LanRoomPage Page { get; private set; }
        public LobbyMode Mode { get; private set; } = LobbyMode.Unranked1v1;
        public void Host() { Mode = LobbyMode.Unranked1v1; Page = LanRoomPage.Host; }
        public void Join() => Page = LanRoomPage.Join;
        public void Connect() => Page = LanRoomPage.Connecting;
        public void Joined() => Page = LanRoomPage.Guest;
        public void Leave() => Page = LanRoomPage.Choice;
        public void Kicked() => Page = LanRoomPage.Choice;
        public void SelectMode(int index) { if (Page == LanRoomPage.Host) Mode = ModeAt(index); }
        public static string PlayerId(string accountId, bool localAccount, string sessionId) =>
            localAccount || string.IsNullOrEmpty(accountId) ? "lan-" + sessionId : accountId;
        public static LobbyMode ModeAt(int index) => index == 1 ? LobbyMode.Team2v2 : index == 2 ? LobbyMode.FreeForAll4 : LobbyMode.Unranked1v1;
        public bool PlaceholderPlay => ModeDefinition.For(Mode).SeatCount == 4;
    }
}
