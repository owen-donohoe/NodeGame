namespace NodeWar.Lobby
{
    public readonly struct HomeModeDisplay
    {
        public string Name { get; }
        public string Colour { get; }
        public LobbyIconKind Icon { get; }

        public HomeModeDisplay(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.OneVsOne: Name = "1v1 Private"; Colour = "#A8D8EF"; Icon = LobbyIconKind.Invite; break;
                case GameMode.Bot: Name = "Bot"; Colour = "#C8E5A0"; Icon = LobbyIconKind.Suit; break;
                case GameMode.Testing: Name = "Testing"; Colour = "#E3B8EB"; Icon = LobbyIconKind.Tools; break;
                default: Name = "1v1 Ranked"; Colour = "#F4CC72"; Icon = LobbyIconKind.Swords; break;
            }
        }

        public HomeModeDisplay(LobbyModeSelection selection)
        {
            switch (selection.Mode)
            {
                case LobbyMode.Unranked1v1: Name = "1v1"; Colour = "#A8D8EF"; Icon = LobbyIconKind.Swords; break;
                case LobbyMode.Team2v2: Name = "2v2"; Colour = "#A8D8EF"; Icon = LobbyIconKind.Invite; break;
                case LobbyMode.FreeForAll4: Name = "1v1v1v1"; Colour = "#E3B8EB"; Icon = LobbyIconKind.Swords; break;
                case LobbyMode.Bot: Name = "Bot"; Colour = "#C8E5A0"; Icon = LobbyIconKind.Suit; break;
                case LobbyMode.Lan: Name = "LAN"; Colour = "#A8D8EF"; Icon = LobbyIconKind.Invite; break;
                default:
                    Name = selection.IsPrivate ? "1v1 Private" : "1v1 Ranked";
                    Colour = "#F4CC72"; Icon = LobbyIconKind.Swords; break;
            }
        }
    }
}
