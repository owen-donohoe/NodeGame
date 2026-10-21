namespace NodeWar.Lobby
{
    public static class QueueSearchPolicy
    {
        public static bool IsFake(LobbyMode mode) => mode == LobbyMode.Team2v2 || mode == LobbyMode.FreeForAll4;
        public static bool CanInvite(LobbyMode mode) => mode == LobbyMode.Team2v2;
        public static bool OfferDue(LobbyMode mode, int seconds) => IsFake(mode) && seconds >= NodeWar.Backend.RankedQueuePresenter.BotOfferSeconds;
        public static string Elapsed(int seconds) => seconds >= 3600
            ? (seconds / 3600) + ":" + (seconds / 60 % 60).ToString("00") + ":" + (seconds % 60).ToString("00")
            : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
}
