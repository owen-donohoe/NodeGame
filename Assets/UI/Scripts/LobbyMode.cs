using System;

namespace NodeWar.Lobby
{
    public enum LobbyMode { Ranked1v1, Team2v2, FreeForAll4, Bot, Lan }
    public enum LobbyPlayAction { Ranked, PrivateInvite, TeamChoice, FreeForAllChoice, Bot, Lan }

    public readonly struct LobbyModeSelection
    {
        public LobbyMode Mode { get; }
        public bool Private { get; }
        public bool IsPrivate => Mode == LobbyMode.Ranked1v1 && Private;
        public LobbyModeSelection(LobbyMode mode, bool isPrivate = false)
        {
            Mode = Enum.IsDefined(typeof(LobbyMode), mode) ? mode : LobbyMode.Ranked1v1;
            Private = isPrivate;
        }
        public GameMode LegacyMode => Mode == LobbyMode.Bot ? GameMode.Bot : Mode == LobbyMode.Lan || IsPrivate ? GameMode.OneVsOne : GameMode.Locked;
        public LobbyPlayAction PlayAction
        {
            get
            {
                switch (Mode)
                {
                    case LobbyMode.Team2v2: return LobbyPlayAction.TeamChoice;
                    case LobbyMode.FreeForAll4: return LobbyPlayAction.FreeForAllChoice;
                    case LobbyMode.Bot: return LobbyPlayAction.Bot;
                    case LobbyMode.Lan: return LobbyPlayAction.Lan;
                    default: return IsPrivate ? LobbyPlayAction.PrivateInvite : LobbyPlayAction.Ranked;
                }
            }
        }
        public static LobbyModeSelection FromProfile(PlayerProfileData data)
        {
            if (data.lobbyModeVersion > 0) return new LobbyModeSelection((LobbyMode)data.selectedLobbyModeIndex, data.privateLobbyMode);
            GameMode legacy = (GameMode)data.selectedGameModeIndex;
            return new LobbyModeSelection(legacy == GameMode.Bot ? LobbyMode.Bot : LobbyMode.Ranked1v1, legacy == GameMode.OneVsOne);
        }
        public void Store(ref PlayerProfileData data)
        {
            data.lobbyModeVersion = 1;
            data.selectedLobbyModeIndex = (int)Mode;
            data.privateLobbyMode = Private;
            data.selectedGameModeIndex = (int)LegacyMode;
        }
    }
}
